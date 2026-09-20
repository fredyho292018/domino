# CUBAN DOMINO CLUB — S1.4A.1 OUTBOUND WRITER REPORT

Date: 2026-09-20. Branch: main.
Source before/after: `87b785b1276eb25397f80439a123bb3bb59e9a72` (unchanged; no staging, commit, push or deploy).

## Result and scope

S1.4A.1 implemented and ready for review/checkpoint. Transport-only changes: one serialized writer for each admitted connection, bounded Critical/Control queues, future ephemeral coalescing/invalidation infrastructure, safe native Tomcat closure, and asynchronous lifecycle cleanup. No Social Presence protocol, Redis redesign, Firestore changes, gameplay rules, or Unity functional changes.

Validation: **555 backend tests passed, 15 optional tests skipped** (570 total). **50 transport-related tests passed**, comprising 23 new dedicated outbound tests, 19 handler cases, 4 malformed-envelope cases, 1 actual online transport flow and 3 matchmaking contract cases. Handler additions include blocked-writer isolation and authentication/close races. **2,338 client checks passed** across eight existing validation programs. Runtime/Editor static compilation passed. Actual Unity Edit Mode, Play Mode and manual checks were NOT_RUN; no claim of a freshly observed zero-error Unity Console is made.

## Production send audit and migration

Before: `RealtimeHandler.send()` serialized and called `WebSocketSession.sendMessage()` while producers held `synchronized(connection)`; `Connection.sequence` incremented in that path.

After: `RealtimeHandler.send()` classifies and admits an immutable encoded payload to `ConnectionOutbound`. Only its writer calls `OutboundTransport.send()`. Actual socket I/O is outside the connection producer monitor and queue lock. Existing command/notification synchronization remains to preserve producer ordering; no unrelated business-service refactor.

Mechanical search of `server/domino/src/main` for `sendMessage(`, `sendText(`, `sendBinary(`, `basicRemote`, and `asyncRemote` finds exactly one application-frame transport call:

| File under server/domino/src/main/kotlin/com/teamfho/domino/realtime | Method | Purpose | Classification |
|---|---|---|---|
| OutboundTransport.kt | send | TextMessage send, invoked only by ConnectionOutbound.run | OUTBOUND_WRITER_INTERNAL |
| OutboundTransport.kt | close | WsSession.doClose(..., true); fallback session.close for non-native test adapter | CONNECTION_CLOSE_ONLY |
| RealtimeHandler.kt | afterConnectionEstablished | Reject over-capacity connection before admission, no writer/lease exists | CONNECTION_CLOSE_ONLY |

The latter two rows are close operations, not remaining application sends. Tomcat/framework close frames are not gameplay frames. Production send occurrences=1; direct application-producer writes=0. Test recording adapters and the explicit old synchronous microbenchmark are test-only and excluded from production count.

### Complete existing message classification

| Protocol type | Class | Policy |
|---|---|---|
| MATCH_UPDATE | CRITICAL | FIFO, no coalescing |
| COMMAND_ACCEPTED | CRITICAL | Same FIFO as causal update |
| COMMAND_REJECTED | CRITICAL | Same FIFO; no invented update for rejected command |
| MATCH_FOUND | CRITICAL | Existing matchmaking notification |
| MATCHMAKING_STATUS | CRITICAL | Existing status notification |
| AUTHENTICATED | CONTROL | Admission/auth barrier, sent before Critical |
| AUTH_FAILED | CONTROL | Drain error, then close |
| PRESENCE_READY | CONTROL | Existing G3 message; no new Social Presence |
| PONG | CONTROL | Existing heartbeat response |
| GLOBAL_ACTIVITY_SNAPSHOT | CONTROL | Initial subscription response, retained FIFO |
| GLOBAL_ACTIVITY_UPDATED | CONTROL | Retained Control FIFO; no new ephemeral coalescing |
| SYSTEM_ERROR | CONTROL | Existing protocol/unavailable error, then close |

No existing message moved into an ephemeral class. `SOCIAL_EPHEMERAL_FUTURE` is exercised only by abstraction tests (`TEST_STATE`/`TEST_CLEANUP`); no production social message, subscription or authorization was added.

## Writer architecture and lifecycle

- Java 21 virtual writer thread per admitted connection. No per-message executor tasks.
- Lazy ArrayDeque Critical/Control queues and connection-local LinkedHashMap for ephemeral targets. No payload buffers preallocated to configured capacity.
- ReentrantLock/Condition protects admission, selection, byte counts, generation, and lifecycle. Idle writer parks; deterministic wait-count test verifies no repeated polling wakeups.
- Lifecycle OPEN → DRAINING (protocol error graceful drain) → CLOSING → CLOSED, or direct OPEN → CLOSING on failure. One owner schedules native close; all other close triggers are no-ops.
- At most one temporary virtual close task per admitted connection. A shared two-thread daemon deadline scheduler retains at most a send deadline and an oldest-Critical deadline per connection; cancelled tasks are removed. In-flight callbacks can occupy the two scheduler threads but never perform socket I/O.
- Handler cap of 1,000 admitted connections is now atomic. Registry slot remains occupied until writer/lease cleanup finishes, avoiding premature replacement while resources remain active.
- No new heartbeat. Existing rate/auth/heartbeat intervals and lease TTL are unchanged.
- Native close is outside producer/queue locks. Writer termination performs lease cleanup once, then registry removal. Lease handoff handles authentication finishing after a close; renewal and cleanup coordinate to avoid resurrecting the old connection lease. Other connections of the same UID retain their own leases.
- Re-auth on one socket remains prohibited. Each reconnect gets a new writer, sequence, queues and server connection identity; old queued work cannot move to another generation.
- Send failure never retries a gameplay frame indefinitely. Cleanup exceptions are recorded with a safe aggregate category; registry removal remains in finally. Existing lease TTL remains fallback for unavailable persistence, without implementing A.2.

### Limits and accounting

| Resource | Count | Encoded UTF-8 payload budget |
|---|---:|---:|
| Critical queued | 64 | 262,144 bytes (256 KiB) |
| Control queued | 16 | 32,768 bytes (32 KiB) |
| Ephemeral queued | 50 targets | 16,384 bytes (16 KiB) |
| One ephemeral payload | 1 | 2,048 bytes |
| One in-flight payload | 1 | 131,072 bytes (128 KiB) |

Accounting means exact serialized JSON **payload** UTF-8 length. It excludes the bounded type/version/sequence/timestamp envelope, Java object/string overhead, temporary serialization objects and native/TLS/socket buffers. Unicode boundary tests use multi-byte `é`; character count is not substituted for byte count. Immutable payload encoding prevents producer mutation after admission. Inputs serialize before queue admission; temporary producer allocations are not claimed as queue memory.

Critical burst maximum=8 before pending Control; Control may win sooner after 20 ms wait. Social is selected only with no pending Critical or Control. Send deadline target=1,000 ms. Oldest queued Critical threshold=2,000 ms. Deadline callbacks check current generation/lifecycle under the same admission lock, so cancelled/stale callbacks cannot close a subsequent successful send.

## Ordering and wire compatibility

`OnlineTransportTests` uses the real in-memory OnlineMatchService command/commit path through RealtimeHandler. Every tested COMMAND_ACCEPTED follows its MATCH_UPDATE and its resultingSequence matches that update's snapshot cursor. COMMAND_REJECTED for an unauthorized command leaves state unchanged and emits no invented update. Mixed FIFO tests also put updates and responses on the same ordered Critical path. Existing concurrent command/receipt semantics remain in the unchanged service/repository tests.

Envelope fields remain type/version/sequence/timestamp/payload. Sequence is allocated for selected transmissions; coalesced, invalidated, or rejected unsent candidates consume none. Healthy transmitted frames are contiguous, starting at 1 for each new connection. A failed in-flight write terminates that connection rather than reusing its sequence.

Unity `RealtimeProtocol.Read` rejects sequence <= previous; it does not require contiguous Match sequences in the transport envelope. The new sender also preserves contiguous successful transport sequencing. MatchEvent.sequence, snapshot lastSequence, firstSequence, replay reducers and revision/idempotency logic are untouched.

4,784 representative emitted frames from three complete DUEL and three complete PARTNERS matches are compared to the old payload JSON and preserved exactly. Snapshots, private authorization, terminal state, history counts and final event cursors are exercised. No truncation, protocol splitting or oversized-message exception was required.

## Backpressure and boundaries

| Condition | Exact outcome |
|---|---|
| Critical count/bytes/frame exceeded | Reject admission, close 1013 with queue_pressure; clear queued work and rely on existing authoritative reconnect/resync |
| Control count/bytes/frame exceeded | Same explicit close policy, never silently discard and continue |
| Social key already queued | Replace only that key's pending immutable value; update exact bytes; count coalescing |
| Social count/total bytes/frame exceeded | REJECTED_STALE, mark stream stale; if replacing an old queued value remove that obsolete value; preserve Critical/Control capacity |
| Social recovery | snapshot exposes stale/recoveryReady; explicit acknowledge only once Social queue and in-flight work are empty and lifecycle OPEN; future caller must arrange authorized resnapshot |
| Invalidate queued target | Remove key and release accounted bytes; reliable cleanup can separately use Control |
| Send deadline or Critical age exceeded | Close 1013; no retry loop |
| Fatal send exception | Close 1011; one cleanup |

No silent Critical/Control drops occur in healthy flow. **An explicit failed-connection close abandons its pending messages**; they are not claimed as delivered. This is the requested close/resync behavior, not a guarantee of transport delivery after failure. Reported zero drops means zero silent drops on a continuing connection, not zero abandoned work in deliberately saturated/closed test connections.

| State | New Critical work | Invalidation | Close |
|---|---|---|---|
| QUEUED_NOT_STARTED | Enqueues FIFO within budget | Removes ephemeral candidate | Aborts pending work with explicit disconnect |
| SELECTED / IN_FLIGHT_SOCKET_WRITE | Queues; cannot preempt selected frame | Cannot recall frame after selection under queue lock (transmission commit point) | Native close terminates I/O; partial frame may fail |
| ALREADY_TRANSMITTED | New independent FIFO entry | Cannot retract received data | Does not undo prior delivery |

The selection lock is the exact transmission/authorization commitment boundary. Future A.3 must coordinate authorization at this boundary; A.1 does not claim it can retract an already selected/private payload.

## Native slow-client evidence

Actual Spring StandardWebSocketSession + embedded Tomcat **11.0.24**, bound to an ephemeral loopback port, raw WebSocket client whose receive window stops draining. No Firebase, Redis or Firestore involved.

- Adapter unwraps Spring session decorators, sets Tomcat BLOCKING_SEND_TIMEOUT and SESSION_CLOSE_TIMEOUT to the deadline, and calls WsSession.doClose(reason, reason, true).
- Test verifies a live blocked writer, calls native forced close, joins without cancellation, and confirms the writer terminates. Zero large frames completed in that probe.
- Second test uses the actual ConnectionOutbound deadline on a 120 KiB payload and asserts CLOSED plus exactly one cleanup.
- This is a deadline **target**, not an OS hard real-time SLA. JVM scheduling and native close overhead can add latency. Tomcat-specific behavior must be revalidated if the servlet/WebSocket implementation changes; no generic guarantee for arbitrary transports is claimed.
- No Future.cancel/thread interruption assumption, no infinite per-send retry, no second application writer.

## Tests and regression evidence

Backend command: `gradlew :test --console=plain`. Existing F0 guard routes any accidental Firestore construction to demo project / 127.0.0.1:1 and excludes REAL_FIRESTORE/EMULATOR tags. New tests use mocks, in-memory authoritative fixtures and loopback Tomcat only.

50 transport-related cases pass, 23 in new dedicated outbound classes. New tests cover single writer, FIFO, causal response order, Control burst/wait fairness, social priority/coalescing/invalidation, explicit stale recovery, count/byte/frame limits, Unicode, concurrent admission, idle parking, stale generation, close race, deadline/age expiry, cleanup, native blocked send, actual wire payload parity and local timings.

Full backend: 570 total = 555 PASS + 15 SKIPPED; 0 failures/errors. Skipped optional cases: RedisMatchmakingTests 8; PartnersMatchmakingTests 3; RedisTurnIndexTests 1; RedisPresenceTests 1; FollowRedisRateTests 1; PartnersOnlineTests external 100-match parity fixtures 1. No Redis listener was found at localhost:6379; these opt-in suites were not enabled or reported as passed. No Firestore Emulator suite was run because there are no persistence changes; production Firestore calls remain zero.

| Existing client validation | Exact passed checks |
|---|---:|
| Guest Auth | 43 |
| Player Foundation including realtime authentication/heartbeat/reconnect/global activity | 469 |
| Online Match | 48 |
| Matchmaking/M5 | 75 |
| I4 Replay | 1,595 (62 archived fixture matches / 31,061 events) |
| Social S1.1–S1.3 client | 66 |
| Monetization H6 | 26 |
| Firestore isolation F0 | 16 |
| Total | 2,338 |

Compile.ps1: runtime and UNITY_EDITOR static compilation PASS against installed Unity 6000.0.41f1 references. These are .NET harness checks, not Unity Test Runner Edit Mode cases.

UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_OBSERVED (Unity session not exercised)

The user's live Unity project/settings were not changed or connected for this task. Existing local configuration can point at real services, so no real-client connection was claimed from protected settings. Manual safe local validation remains an optional review limitation under section 89.

## Local performance and memory

The measurements below are local test samples, not production SLAs. Baseline reconstructs the old synchronous envelope/send-to-recording-adapter path. Writer measurement includes bounded batches and waiting for drain. Mockito, JIT, scheduler and test ordering affect results. The writer adds handoff overhead (last sample 2.695 ms vs 1.235 ms for 100 Critical messages, approximately 14.6 microseconds extra per message); this small absolute cost does not indicate a throughput SLA or remove the need for later production load tests. No Bot Swarm was started.

```text
LOCAL_PERF_MICROS baseline_sync_100=1235 single_enqueue_send=104 critical_100=2695 mixed_90=2459 coalescing_500_to_50=598 drain_50=832
OUTBOUND_PAYLOAD_MAX_UTF8_BYTES={DUEL_MATCH_UPDATE=6613, DUEL_SNAPSHOT=4900, DUEL_TERMINAL=6157, MATCHMAKING_STATUS=45, MATCH_FOUND=173, PARTNERS_MATCH_UPDATE=7347, PARTNERS_SNAPSHOT=6046, PARTNERS_TERMINAL=7347} WIRE_PARITY_FRAMES=4784
NATIVE_WRITER_DEADLINE_CLEANUP_MS=1005
NATIVE_SEND_CLOSE_UNBLOCK_MS=901 COMPLETED_FRAMES=0
```

Largest observed existing Critical payload=7,347 bytes. Payload size can vary slightly with timestamp precision; compare recorded measurement, not exact fixture byte constants.

Configured queued payload maximum=304 KiB/connection; 100 connections=29.6875 MiB; 1,000=296.875 MiB. Additional maximum in-flight payload=128 KiB/connection (125 MiB at 1,000), plus serialized envelope/string duplication, JVM structures/stacks/native buffers. These are payload accounting ceilings, **not measured heap usage**. Idle queues are empty, not eagerly allocated at those sizes. IDLE_WRITER_APPROX_MEMORY=NOT_MEASURED; actual retained heap requires a profiler and is not fabricated.

At 1,000 admitted connections: at most 1,000 writer virtual threads, at most 1,000 temporary close tasks during simultaneous closure, two shared deadline threads and approximately 2,000 retained deadline tasks bounded by admitted connections. No unbounded per-message work executor. Queue containers allocate initial small collection structures only.

Micrometer already existed: aggregate queue-message and queue-byte gauges by three fixed message classes, fixed-reason event counters for send failure, queue pressure, deadline, Critical age, coalescing/rejection/cleanup. Per-writer snapshot includes oldest queued Critical age. No UID/publicId/connectionId/matchId metric labels or payload/token logs.

## Known limits and remaining phases

- Native shutdown guarantee validated for current Tomcat 11.0.24; revalidate transport upgrades.
- No measured heap/1,000-live-socket load test and no real Unity Play Mode validation in this task.
- Optional Redis/persistence suites remain skipped as explicitly listed; passed in-memory/client tests do not imply those suites were executed.
- Explicit failure close abandons queued frames and relies on existing authoritative resync.
- Future ephemeral consumer must react to stale notification and coordinate authorization/invalidation at selection; no Social Presence consumer exists yet.
- Existing Redis failure semantics, multi-instance matchmaking distribution and social authorization invalidation are unchanged; they belong to A.2/A.3/A.4. A.1 does not complete S1.4A and does not unblock Presence automatically.

## Safety and file classification

All 97 pre-existing dirty/untracked file hashes match the initial snapshot. Android/Google Play preparation, Firebase resolver work, protected assets and previous S1.4 report preserved byte-for-byte. Nothing staged.

Counts below classify **Git-visible dirty/untracked files**, not ignored compiler caches. Validation outputs under client/Validation/Generated and server/domino/build are ignored, disposable artifacts; logs, baseline hashes and backend XML summaries are retained there. GENERATED_BY_VALIDATION_FILES=0 means no generated artifact added to the Git-visible dirty inventory.

S1_4A_1_INTENTIONAL=12; PRE_EXISTING_USER_CHANGE=97; GENERATED_BY_VALIDATION=0 Git-visible; UNEXPECTED=0. Total=109.

### Intentional files

| Path | Classification |
|---|---|
| `client/Validation/S1_4A_1_OUTBOUND_WRITER_REPORT.md` | S1_4A_1_INTENTIONAL |
| `server/domino/src/main/kotlin/com/teamfho/domino/realtime/ConnectionOutbound.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/main/kotlin/com/teamfho/domino/realtime/OutboundMetrics.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/main/kotlin/com/teamfho/domino/realtime/OutboundTransport.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/matchmaking/MatchmakingContractTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/online/OnlineTransportTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/realtime/ConnectionOutboundTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/realtime/OutboundPayloadTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/realtime/OutboundTransportTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/realtime/RealtimeHandlerTests.kt` | S1_4A_1_INTENTIONAL |
| `server/domino/src/test/kotlin/com/teamfho/domino/realtime/RealtimeProtocolTests.kt` | S1_4A_1_INTENTIONAL |

### Preserved pre-existing files (all initial SHA-256 values matched)

| Path | Classification |
|---|---|
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/AddressableAssetsData/Android.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/Assets/google-services.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/ProjectSettings/ProjectSettings.asset` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |
| `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md` | PRE_EXISTING_USER_CHANGE / HASH_MATCH |


## Detailed result fields

```text
BRANCH=main
SOURCE_SHA_BEFORE=87b785b1276eb25397f80439a123bb3bb59e9a72
SOURCE_SHA_AFTER=87b785b1276eb25397f80439a123bb3bb59e9a72
SINGLE_WRITER_PER_CONNECTION=YES
WRITER_EXECUTION_MODEL=JAVA_21_VIRTUAL_THREAD_PER_CONNECTION
DIRECT_PRODUCER_SOCKET_WRITES=0
CRITICAL_QUEUE=BOUNDED_FIFO
CONTROL_QUEUE=BOUNDED_FIFO
SOCIAL_EPHEMERAL_QUEUE=BOUNDED_LATEST_BY_CONNECTION_LOCAL_TARGET
SOCIAL_COALESCING_KEY=CONNECTION_LOCAL_TARGET_KEY
CRITICAL_MAX_MESSAGES=64
CRITICAL_MAX_BYTES=262144
CONTROL_MAX_MESSAGES=16
CONTROL_MAX_BYTES=32768
SOCIAL_MAX_TARGETS=50
SOCIAL_MAX_BYTES=16384
SOCIAL_MAX_FRAME_BYTES=2048
CRITICAL_BURST_BEFORE_CONTROL=8
SEND_DEADLINE=1000_MS_TARGET
CRITICAL_MAX_AGE=2000_MS
MAX_EXISTING_CRITICAL_PAYLOAD_BYTES=7347
CRITICAL_FIFO=PASS
CONTROL_FAIRNESS=PASS
MATCH_UPDATE_BEFORE_COMMAND_RESPONSE=PASS_WHERE_CAUSAL_UPDATE_EXISTS
TRANSPORT_SEQUENCE_MONOTONIC=PASS
UNSENT_EPHEMERAL_CONSUMES_SEQUENCE=NO
MATCH_EVENT_SEQUENCE_CHANGED=NO
REPLAY_SEQUENCE_CHANGED=NO
FIRST_LAST_SEQUENCE_CHANGED=NO
WIRE_CONTRACT_CHANGED=NO
SOCIAL_COALESCING=PASS
SOCIAL_SATURATION=REJECT_AND_MARK_STALE
SOCIAL_STALE_RECOVERY=EXPLICIT_ACK_AFTER_DRAIN
CRITICAL_SATURATION=CLOSE_1013_RESYNC
CONTROL_SATURATION=CLOSE_1013_RESYNC
SLOW_SOCKET_TEST=PASS
SEND_CLOSE_UNBLOCKS_WRITER=PASS
SEND_DEADLINE_ENFORCEABLE=YES_CURRENT_TOMCAT_TARGET
OPEN_CLOSE_MODEL=OPEN_DRAINING_CLOSING_CLOSED
WRITER_FAILURE_CLEANUP=PASS
CLOSE_RACE=PASS
LEASE_CLEANUP=PASS
ZOMBIE_WRITER_TASKS=0_OBSERVED
MULTI_CONNECTION_SAME_UID=PASS
WRITER_STARTED_PER_CONNECTION=1
WRITER_STOPPED_ON_CLOSE=PASS
QUEUE_RELEASED_ON_CLOSE=PASS
EPHEMERAL_MAP_RELEASED_ON_CLOSE=PASS
CONNECTION_REGISTRY_CLEANUP=PASS
DUPLICATE_CLOSE_SIDE_EFFECTS=0
AUTH_MESSAGES_MIGRATED=YES
HEARTBEAT_MESSAGES_MIGRATED=YES
MATCH_MESSAGES_MIGRATED=YES
MATCHMAKING_MESSAGES_MIGRATED=YES
GLOBAL_ACTIVITY_MIGRATED=YES_CONTROL_FIFO
ERROR_MESSAGES_MIGRATED=YES
REMAINING_SENDMESSAGE_CALLS=1
REMAINING_SENDMESSAGE_CLASSIFICATION=OUTBOUND_WRITER_INTERNAL
S1_4A_1_FOCUSED_TESTS=50_PASS_TRANSPORT_SUBSET;23_NEW_DEDICATED_CASES
BACKEND_TESTS=555_PASS_15_SKIPPED
CLIENT_CHECKS=2338_PASS
UNITY_STATIC_COMPILATION=PASS
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_OBSERVED
AUTH_REGRESSION=PASS
HEARTBEAT_REGRESSION=PASS
RECONNECT_REGRESSION=PASS
MATCH_WEBSOCKET_REGRESSION=PASS
MATCH_ORDERING_REGRESSION=PASS
MATCH_COMPLETION_REGRESSION=PASS_IN_MEMORY
MATCHMAKING_REGRESSION=PASS_IN_MEMORY_AND_CLIENT;OPTIONAL_REDIS_NOT_RUN
GLOBAL_ACTIVITY_REGRESSION=PASS
I4_REPLAY_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
S1_3_REGRESSION=PASS;OPTIONAL_REDIS_RATE_TEST_NOT_RUN
IDLE_WRITER_APPROX_MEMORY=NOT_MEASURED
QUEUE_STORAGE_ALLOCATION=LAZY
PROJECTED_QUEUE_PAYLOAD_100_CONNECTIONS=29.6875_MiB
PROJECTED_QUEUE_PAYLOAD_1000_CONNECTIONS=296.875_MiB
PRESENCE_IMPLEMENTED=NO
REDIS_FAILURE_ISOLATION_IMPLEMENTED=NO
DISTRIBUTED_INVALIDATION_IMPLEMENTED=NO
FIRESTORE_SCHEMA_CHANGES=0
FIRESTORE_RULE_CHANGES=0
FIRESTORE_INDEX_CHANGES=0
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
PREEXISTING_USER_FILES_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
FILES_MODIFIED=109_GIT_VISIBLE_DIRTY_OR_UNTRACKED_FILES
S1_4A_1_FILES_MODIFIED=12
PREEXISTING_USER_FILES=97
GENERATED_BY_VALIDATION_FILES=0_GIT_VISIBLE
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_1=NO
S1_4A_1_IMPLEMENTED=YES
S1_4A_1_SUCCESS=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_4A_1_READY_FOR_CHECKPOINT=YES_REVIEW_REQUIRED
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4A_COMPLETE=NO
S1_4A_2_STARTED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
NEXT=S1.4A.1_REVIEW_CHECKPOINT
```
