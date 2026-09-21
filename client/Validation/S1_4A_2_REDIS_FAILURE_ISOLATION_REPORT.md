# S1.4A.2 — current status after A.2R

## S1.4A.2R â€” concurrency resolution (current)

This addendum supersedes the **checkpoint status**, not the historical evidence, of the original A.2 report below. HEAD remains `dc9b12281f0d0330bf564792cf784a6ba3d64d22`, main; no commit/push/deploy.

### Reproduction and baseline

Before source edits the exact existing method `com.teamfho.domino.social.FollowEmulatorTests.real concurrency directional shared counters block and privacy` ran ten times sequentially: **6 PASS / 4 FAIL** (runs 4,6,8,10). Failure durations: 9.833, 9.194, 9.033, 9.322 seconds (JUnit suite times; first failed method itself 9.821s). The first attempt passed; intermittent failures were retained, not retried until a desired result.

An isolated `git archive` of S1.3 `87b785b1276eb25397f80439a123bb3bb59e9a72` ran the same unmodified method ten times: **8 PASS / 2 FAIL**, runs 9 and 10, 9.842 and 10.219 seconds. No checkout/reset/stash or alternate branch operation touched the active worktree. Both used Java 21.0.6, Firestore SDK 3.42.0, Emulator 1.22.0 (SHA-256 `9b6498b7f62714d67f48f59b3818883cd682dbcd46b9f59511de81c97bb5166c`), loopback 18085, demo-domino-f0, identical Gradle test selector and default sequential test process settings. Each ten-run batch used a fresh Emulator; UUID test data separates repetitions. Gradle `--rerun` forces each selected method execution, without rerunning after failure to mask it.

Both failures have the same root: `ABORTED: Transaction lock timeout`, surfaced through nested `ExecutionException`, `FirestoreSocialTransaction.read`, `FriendshipService.counter/removeInTransaction`, `FirestoreSocialRepository.block`, and original test line 61 (Unfollow versus reverse-direction Block).

**A2_CAUSED_REGRESSION=NO.** The S1.3 comparison reproduces the same failure. Beyond unchanged source, the exact test constructs services/repositories directly: it does not instantiate Spring, controllers, the SocialRateGate or its bounded executor. The 15 A.2 files were reviewed: route gates/new Redis worker/circuit can affect HTTP behavior, but they are absent from this execution path; privacy service changes do not change the repository called by this test; test fixture adaptations do not affect this method; new tests do not run under the exact selector. Build/dependencies, Follow, Friendships, both Firestore repositories and the original test have no S1.3â†’pre-fix differences. Full-suite timing could influence frequency, but cannot explain away the isolated baseline reproduction.

### Root cause and transaction sets

There is real overlapping transaction contention plus a retry propagation defect. For example, Unfollow(Aâ†’B) reads A's counter before B's, whereas Block(B,A) reads B's before A's. Under overlapping execution each may wait for a document held by the other. The failing stack identifies the counter read. Emulator resolves the conflict by aborting a transaction; the application then incorrectly prevents the SDK from handling that retryable abort.

Notation: C(U)=`players/U/socialCounters/current`; F(U,V)=`players/U/following/V`; R(U,V)=`players/U/followers/V`; pair=canonical SocialPairIdentity(A,B).

| Transaction | Reads (conditional reads noted) | Atomic writes |
|---|---|---|
| Follow Aâ†’B | publicPlayerProfiles/publicIdB; F(A,B); C(A), C(B); players/A,B; developmentTestAccounts/A,B; blocks A/B and B/A; for a new edge, B/socialSettings/current and A/publicIdentity/current | F(A,B), R(B,A), C(A).followingCount, C(B).followerCount |
| Unfollow Aâ†’B | publicPlayerProfiles/publicIdB; F(A,B); when present C(A), C(B) | Delete F(A,B), R(B,A); decrement corresponding counters |
| Block Bâ†’A | B/blocks/A; A/blockedBy/B; socialPairs/pair; friendships/pair; pending friendRequests/id if present; C(B), C(A); F(B,A), F(A,B) | Create block/projection; delete both directional Follow edges and follower projections; remove friendship and both friend projections when present; cancel pending request/update pair when present; update both counters as necessary |

Overlaps: C(A), C(B), F(A,B), F(B,A) as applicable, and the blocking document read by enabled Follow. Both Follow directions write their shared counter documents. The existing canonical socialPairs path coordinates friendship/request operations but Follow does not use it. No new pair coordination read/document or Redis lock is needed for correctness: the existing transactional conflicts and block checks provide serialization **when retryable aborts reach the SDK correctly**.

The Google Firestore documentation describes server-side locking and automatic retry for transaction contention: [transaction contention](https://firebase.google.com/docs/firestore/transaction-data-contention). This is not proven to be an Emulator-only artifact, and no production experiment was performed. The retry propagation defect is in production Java/Kotlin code and applies to SDK read failures independently of the source of an ABORTED response.

### SDK audit and minimal correction

Installed SDK bytecode (`javap`, retained in ignored evidence) shows:

- `TransactionAsyncAdapter.updateCallback` retains the callback's thrown exception as-is.
- `ServerSideTransactionRunner.restartTransactionCallback` first requires a direct `ApiException`; a wrapped `ExecutionException` immediately rolls back/rejects rather than entering retry classification.
- Default maximum = 5 transaction attempts. ExponentialRetryAlgorithm uses the existing Firestore retry settings and randomized delay. No repository retry loop existed.
- SDK retryable codes: ABORTED, CANCELLED, UNKNOWN, DEADLINE_EXCEEDED, INTERNAL, UNAVAILABLE, UNAUTHENTICATED, RESOURCE_EXHAUSTED; INVALID_ARGUMENT only for its existing transaction-expired condition. This classification is unchanged.

Correction class **A â€” production transaction retry correction**: a small internal callback adapter strips only ExecutionException wrappers whose enclosed cause is an ApiException. It rethrows that original instance for SDK classification. Domain/application exceptions retain their original propagation; direct SDK exceptions already pass unchanged. Both Social repository callback entry points use it. No new retry loop, timeout increase, backoff override, lock, document or executor. Existing caller waits remain 20s for FriendshipRepository and 15s for SocialRepository. The SDK's bounded retries remain the only retry policy.

Actual attempt count of the original uninstrumented race was not captured and is not fabricated. The SDK inspection proves no additional retry is initiated for the wrapped read error. A deterministic SDK/Emulator regression separately measures wrapped-abort callback count=1, corrected transient-abort count=2 with exactly one committed increment, and persistent-abort count=5. Domain failure must remain count=1. The suite verifies these values; see final results below.

The correction does not eliminate contention or promise infinite success under overload. It restores the SDK's bounded response to transient contention, without changing transaction boundaries or adding external side effects. Callbacks still perform transaction-local reads/writes only. Measured SDK settings are initial delay 1s, multiplier 2, maximum delay 32s with jitter; TransactionOptions limits transaction attempts to 5 (the general RetrySettings printout also contains a separate maxAttempts=6). The unchanged 15s/20s caller waits do not guarantee that all five attempts fit in that interval, and a caller timeout does not itself cancel the underlying SDK future. No timeout or cancellation policy was changed in this fix.

### Validation design and changed files

The original test is untouched. Post-fix it ran ten times sequentially, with all ten results retained. The new stress test runs 50 iterations with two simultaneous tasks behind a barrier: 25 new Follow-vs-Block and 25 Unfollow-vs-Block cases, including a reverse Follow to remove. Every iteration verifies Block and blockedBy, absence of both Follow directions and follower projections, and both counters exactly zero. A losing Follow may return only the existing typed PLAYER_NOT_FOUND/404 due to the winning Block; all other exceptions fail the test. There are no test-level retries or arbitrary sleeps; barrier deadlines merely bound scheduling failures.

Files changed by A.2R:

1. `server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreFriendships.kt` â€” callback error normalization and use by atomic().
2. `server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialRepository.kt` â€” same adapter at its transaction boundary.
3. `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialTransactionRetryEmulatorTests.kt` â€” SDK retry proof and 50-race invariants.
4. `client/Validation/S1_4A_2_REDIS_FAILURE_ISOLATION_REPORT.md` â€” this dated resolution while preserving original evidence.

No A.2 functional file or original test assertion was rewritten. A.1 writer/shared threading is unchanged; its existing 23 passing tests remain the applicable evidence and were not unnecessarily rerun. The extra 50 races are counted as iterations within one JUnit test, not inflated into 50 separate tests.


### Final A.2R results

- Exact method: before 6/10 PASS, S1.3 baseline 8/10 PASS, after fix **10/10 PASS**. All failed runs retained.
- Full Emulator: **31 PASS / 0 FAIL**, including the original race, three A.2 tests and three new retry/stress tests.
- Stress: **50/50 PASS**, 25 Follow/Block and 25 Unfollow/Block; no test-level retry.
- Social unit regression: **82 PASS / 3 SKIPPED**. The 36 A.2 unit cases and 3 A.2 Emulator cases were rerun. The 2 unchanged Redis integration cases retain the prior A.2 PASS evidence and were not rerun against a service unnecessarily.
- SDK retry evidence: `A2R_RETRY wrappedAttempts=1 fixedAttempts=2 committedValue=1 defaultMaxAttempts=5 backoff=RetrySettings{totalTimeoutDuration=PT50S, initialRetryDelayDuration=PT1S, retryDelayMultiplier=2.0, maxRetryDelayDuration=PT32S, maxAttempts=6, jittered=true, initialRpcTimeoutDuration=PT50S, rpcTimeoutMultiplier=1.0, maxRpcTimeoutDuration=PT50S}; A2R_RETRY exhaustedAttempts=5 domainAttempts=1`.
- 97 protected user files and 14 preexisting A.2 source/test files verified unchanged by SHA-256. Only the A.2 report was intentionally updated. Total A.2 plus A.2R intentional paths: 18; no unexpected Git-visible files.
- No shared realtime threading/writer, rate gate, timeout or quota change. No production calls, commit, push or deploy.
- Raw evidence retained under ignored `client/Validation/Generated/S14A2R/`: 30 per-run XML/log pairs, baseline archive, SDK inspection, Emulator and targeted-unit logs. Isolated source copy remains ignored; no Git worktree/branch was created. Emulator cleanup PASS.

**A.2 is now ready for review/checkpoint.** The original failed report below is historical and is retained without erasing its evidence.

<!-- A2R_HISTORICAL_START -->
# S1.4A.2 — Redis failure isolation and Social rate gate

Date: 2026-09-20. Branch: `main`. Source before/after: `dc9b12281f0d0330bf564792cf784a6ba3d64d22`.

Implementation is present, but **NOT READY FOR CHECKPOINT**: final Emulator regression is 27 PASS / 1 FAIL. The failing existing `FollowEmulatorTests.real concurrency directional shared counters block and privacy` aborts with `Transaction lock timeout` during concurrent Unfollow/Block (line 61). It failed in two final runs. Its test, services and Firestore repositories are unchanged; that test calls the repositories/services directly without the new rate gate. This identifies an unchanged execution path, not proof that the old checkpoint always failed. No retry, weakened assertion, transaction change or skipped test was introduced to hide it.

## Implementation and authorities

`server/domino/src/main/kotlin/com/teamfho/domino/social/SocialRateGate.kt` owns the typed `SocialOperation` / `SocialOperationClass`, ALLOW / DENY_429 / UNAVAILABLE_503 decisions, local limiter, circuit, bounded Redis adapter and privacy comparator. Three controllers use this boundary. `RedisSocialRateLimiter.kt` contains the extracted existing Lua algorithms; no Firestore rate-limit collection, durable lock or economic authority was added. Firestore remains authoritative for graph, privacy, counters, friendship limits and the existing send quota.

| Operation | Redis healthy | Redis unavailable/open |
|---|---|---|
| Search NAME / exact friend code | Existing distributed quota | 503 before identity/search Firestore access |
| Profile, settings read, summary, blocks, requests, friends, followers/following | Existing distributed quota | Shared per-UID READ bucket |
| Send / Accept friend request | Existing distributed quota and durable checks | Shared per-UID SEND_ACCEPT bucket and same durable checks |
| Decline / Cancel / Unfriend / Unfollow / Unblock | Distributed quota | Shared per-UID SAFETY bucket |
| Block | Central unconditional safety bypass | Same bypass, even when local storage is full |
| Privacy tightening | Privacy preflight + distributed settings quota | Preflight SAFETY token, then allowed |
| Privacy loosening / mixed patch | Privacy preflight + distributed settings quota | Entire patch rejected 503 |
| Privacy no-op | Bounded preflight, revision validation | Same; successful no-op produces no write |

No presence endpoint/UI was added. EPHEMERAL is a typed reserved classification, unavailable on Redis failure; future presence semantics remain UNKNOWN, not fabricated OFFLINE.

Normal Redis Lua quotas are preserved: NAME 20/min burst 5; friend code and Unblock 30/min burst 30; other original actions 60/min burst 60; Follow rolling 30/min and 200/day. **Two explicit differences:** Unfollow has its own 60/min distributed key instead of consuming Follow's creation quota; privacy updates additionally require local SAFETY preflight even when Redis is healthy. This conservative preflight bounds the durable read needed to classify an untrusted patch. It can reject a privacy update before the distributed settings quota is exhausted. No-op uses only that preflight.

## Local limiter, circuit and timeout

READ = 12/min burst 4; SEND_ACCEPT = 6/min burst 2; SAFETY = 30/min burst 5. All use authenticated UID, backend-instance scope, monotonic time, lazy allocation and synchronized updates. Different buckets share one actor entry but not tokens. Maximum 10,000 entries: new ordinary actors stop at 9,000, preserving 1,000 admissions for safety. Existing actors keep their buckets. No active eviction; after five minutes without access entries expire lazily. Actor exhaustion = 429; infrastructure capacity exhaustion = 503. Block never depends on either capacity or tokens. These emergency quotas multiply across backend instances and are explicitly not global durable authority.

UID keys are fixed-length SHA-256 Base64URL digests, avoiding variable-length retained UID memory. Retained-memory estimate (not a heap measurement): 384 bytes/entry plus 65,536 bytes table allowance: 100 = 103,936 bytes; 1,000 = 449,536; 10,000 = 3,905,536 (~3.73 MiB). Temporary hashing allocations and runtime object-layout differences are excluded.

Circuit: three consecutive infrastructure failures; OPEN 5s; one HALF_OPEN probe; failed probes back off 10/20/30s, capped at 30. Valid distributed quota denial remains 429 and counts as healthy. Concurrent stale completions cannot close a newer open generation. Open decisions send zero Redis commands. No HTTP-request retry or background recovery poll.

Effective Social caller budget = 200ms. `BoundedSocialRedis` has at most two daemon workers, a `SynchronousQueue` (zero waiting tasks) and immediate rejection when full. A timed-out caller **does not cancel underlying Redis execution**; the occupied worker remains occupied until the adapter returns. Existing native Redis connect/command timeouts remain 1s; global Redis settings and Matchmaking/turn settings are unchanged. This bounds Social's outstanding invocations, not every command issued by unrelated Redis users. Under healthy bursts, more than two simultaneous calls can trigger controlled degradation; capacity tuning requires future workload evidence.

Metrics have fixed operation-class/result/origin labels only: `social.rate.gate.decisions`, `social.rate.limit.fallback`, `social.local.limiter.capacity.rejections`, gauges for entries, circuit state and infrastructure failures. Transition/fallback/capacity logs contain no UID, token, IP or credentials. No per-success noisy logging.

## Privacy and durable mutation evidence

`SocialPrivacyService.updateGated`: local preflight → existing identity initialization → current durable privacy → supplied revision equality → server-side direction classification → gate → existing `patchPrivacy` transaction. The transaction rechecks the same revision. A concurrent update therefore produces 409 rather than authorizing a changed interpretation. Existing settings revision is reused; no A.3 authorization-revision infrastructure was introduced.

The comparator covers discoverable true→false, EVERYONE→NO_ONE contacts, and EVERYONE→FRIENDS→NO_ONE visibility. Any loosening in a mixed patch rejects the whole patch on outage. Emulator covers each direction, mixed rejection, no-op zero writes and stale-revision rejection. Comparator tests cover every visibility pair. No separate artificially orchestrated read/commit race test was added; the existing transaction revision boundary is retained.

Send preserves existing atomic Firestore 5/min and 20/day quota; Emulator seeds each exhausted window and verifies 429 and unchanged quota. Accept retains both users' entitlement/capacity checks and Firestore transaction; concurrent acceptance with one remaining FREE slot produces exactly one success and friendCount=5. No Redis lock replaces that transaction.

Emulator HTTP lifecycle covers all listed available reads, Send/Accept/Decline/Cancel/Unfriend, Block and Unblock, Unfollow idempotency, graph/counter state and no implicit relation restoration. Search rejection measures 0 reads / 0 writes. Tightening writes only existing privacy state; loosening/mixed/no-op tests confirm no write. Existing Follow/Block multi-transaction concurrency regression remains the blocker identified above.

## Validation

| Validation | Result |
|---|---|
| A.2 unit cases | 36 PASS |
| Opt-in isolated Redis cases | 2 PASS |
| A.2 Emulator cases | 3 PASS |
| Total A.2 focused tests | 41 PASS |
| Full Emulator suite | 27 PASS / 1 FAIL / 0 SKIPPED |
| Social client assertions | 78 PASS |
| Player Foundation / realtime client assertions | 469 PASS |
| Guest Auth assertions | 43 PASS |
| Online client assertions | 48 PASS |
| Matchmaking/M5 client assertions | 75 PASS |
| Replay assertions | 1,595 PASS; 62 matches, 31,061 events |
| Monetization assertions | 26 PASS; zero ads/wallet mutations |
| F0 isolation assertions | 16 PASS; zero network calls |
| Total listed client assertions | 2,350 PASS |
| Static Unity-reference compilation | PASS (existing warnings) |
| Unity Editor / Play Mode / manual visual checks | NOT_RUN; no Unity runtime source/UI change |
| Unity Console errors | NOT_MEASURED; no live Console claim |

Normal backend final counts are recorded in the machine block below. All regular test access retains F0's real-Firestore guard. The optional old Redis tests are not labeled PASS when skipped. New opt-in Redis tests use only temporary `127.0.0.1:16379`, and a released ephemeral loopback port for actual connection refusal. Controlled adapter outage tests connect to the real local Redis before and after injection; no production Redis restart was performed.

A.1 backend regression includes 20 `ConnectionOutboundTests`, 1 `OutboundPayloadTests`, 2 `OutboundTransportTests`, all PASS. Writer code is unchanged: single writer, critical FIFO, control fairness, match ordering, slow socket and cleanup remain covered. Auth/profile/entitlements, S1.1/S1.2 unit and Emulator, I4, M5, Matchmaking, Monetization, F0/F0.1 and heartbeat/reconnect regressions pass in their executed suites. S1.3 unit tests pass; **S1.3 full Emulator regression FAILS** as specified above.

Client 503 tests exercise Search, Follow and privacy loosening. Each throws typed unavailable, performs exactly one request, does not refresh auth, and preserves the UID/session. The client has no cross-request relationship cache to corrupt. Existing runtime handling is unchanged.

Measured local timings (single development-machine run; not a throughput guarantee): real Redis healthy average 3,767 microseconds across 30 decisions; actual refused connection attempts 40/2/3ms; fake-healthy decision average 1,144ns; open-circuit local fallback average 2,641ns. A blocked-adapter test confirms only two underlying tasks and zero queued tasks after 100 rejected submissions. At 100/1,000/10,000 entries, measured admission totals were 3,143/7,981/25,255 microseconds. All measurements include test/harness overhead and are warmup-sensitive.

No new Firestore rate-limit writes; the preexisting durable Send quota is intentionally still written. No production Firestore/Redis access; no swarm; no billing/Ads/economy/gameplay changes. Emulator process/port cleanup is checked by its runner. Only the A.2 Redis container is removed.

## Reproduction and evidence

- Backend: `server/domino/gradlew.bat :test --console=plain` (F0 guard active).
- Emulator: `server/domino/tools/f0/RunFirestoreEmulatorTests.ps1` (demo-domino-f0, loopback 18085).
- Redis: disposable redis:7 bound to 127.0.0.1:16379, `DOMINO_A2_REDIS_TESTS=true`, Gradle `:test --tests '*SocialRateGate*'`.
- Client: existing `RunSocialClientTests`, `RunPlayerFoundationClientTests`, `RunOnlineMatchTests`, `RunMatchmakingTests`, `RunReplayTests`, `RunMonetizationTests`, `RunGuestAuthTests`, `RunFirestoreIsolationTests` and `Compile` PowerShell runners.
- Ignored evidence: `client/Validation/Generated/S14A2/` contains initial SHA-256 inventory, logs, focused XML and count inventories. Earlier failed Emulator attempts are retained for transparency.

## Review limitations

1. The S1.3 Emulator concurrency failure prevents acceptance/checkpoint. Resolve or independently disposition it before claiming all regressions pass. No durable persistence fix was smuggled into A.2.
2. Local fallback is per instance, not a distributed abuse quota. Follow/discovery/loosening fail closed without Redis.
3. Privacy preflight and separate Unfollow quota are the explicit policy differences described above.
4. Memory is estimated; timings are not a load benchmark. No Bot Swarm or production traffic was generated.
5. Static compilation and client tests do not prove Unity Play Mode/visual Console behavior; those were not run.

COMMIT=NONE; PUSH=NONE; DEPLOY=NONE. S1.4A.3, S1.4B, S2, Presence, Pub/Sub and Social outbox were not started.

<!-- FINAL_AUDIT -->

## Exact final changed-file classification

All paths are repository-relative. SHA-256 equality was verified for every baseline file; no source/settings staging occurred.

### S1_4A_2_INTENTIONAL (15)

- `client/Validation/S1_4A_2_REDIS_FAILURE_ISOLATION_REPORT.md`
- `client/Validation/SocialClientTests.cs`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FollowController.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FriendshipController.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/RedisSocialRateLimiter.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialController.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialRateGate.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialServices.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/FollowRateTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialHttpTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRateGateEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRateGateRedisTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRateGateTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialValidationServer.kt`

### PRE_EXISTING_USER_CHANGE (97; preserved byte-for-byte)

- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake`
- `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json`
- `client/DominoGame/Assets/AddressableAssetsData/Android.meta`
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin`
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta`
- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset`
- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml`
- `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml`
- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties`
- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta`
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle`
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta`
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle`
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta`
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json`
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta`
- `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset`
- `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset`
- `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset`
- `client/DominoGame/Assets/google-services.json`
- `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml`
- `client/DominoGame/ProjectSettings/GvhProjectSettings.xml`
- `client/DominoGame/ProjectSettings/ProjectSettings.asset`
- `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json`
- `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md`

### GENERATED_BY_VALIDATION

Git-visible: 0. Dedicated ignored evidence files: 23 (retained for review). Existing runner build/cache directories remain ignored; these are not added to the source diff. No preexisting generated Android files were removed.

- `client/Validation/Generated/S14A2/Compile.log`
- `client/Validation/Generated/S14A2/RunFirestoreIsolationTests.log`
- `client/Validation/Generated/S14A2/RunGuestAuthTests.log`
- `client/Validation/Generated/S14A2/RunMatchmakingTests.log`
- `client/Validation/Generated/S14A2/RunMonetizationTests.log`
- `client/Validation/Generated/S14A2/RunOnlineMatchTests.log`
- `client/Validation/Generated/S14A2/RunPlayerFoundationClientTests.log`
- `client/Validation/Generated/S14A2/RunReplayTests.log`
- `client/Validation/Generated/S14A2/RunSocialClientTests.log`
- `client/Validation/Generated/S14A2/TEST-com.teamfho.domino.social.FollowEmulatorTests.xml`
- `client/Validation/Generated/S14A2/TEST-com.teamfho.domino.social.SocialRateGateEmulatorTests.xml`
- `client/Validation/Generated/S14A2/TEST-com.teamfho.domino.social.SocialRateGateRedisTests.xml`
- `client/Validation/Generated/S14A2/TEST-com.teamfho.domino.social.SocialRateGateTests.xml`
- `client/Validation/Generated/S14A2/backend-counts.json`
- `client/Validation/Generated/S14A2/backend-final-counts.json`
- `client/Validation/Generated/S14A2/backend-final.log`
- `client/Validation/Generated/S14A2/backend.log`
- `client/Validation/Generated/S14A2/baseline.json`
- `client/Validation/Generated/S14A2/emulator-counts.json`
- `client/Validation/Generated/S14A2/emulator-final.log`
- `client/Validation/Generated/S14A2/emulator.log`
- `client/Validation/Generated/S14A2/final_audit.py`
- `client/Validation/Generated/S14A2/redis-focused.log`

UNEXPECTED=0. Temporary Redis container removed; Emulator process/port cleanup PASS.

## Final machine-readable result

```text
S1.4A.2 CHECKPOINT CANDIDATE
===========================
BRANCH=main
SOURCE_SHA_BEFORE=dc9b12281f0d0330bf564792cf784a6ba3d64d22
SOURCE_SHA_AFTER=dc9b12281f0d0330bf564792cf784a6ba3d64d22
S1_4A_2_IMPLEMENTED=YES
S1_4A_2_SUCCESS=NO
S1_4A_2_READY_FOR_CHECKPOINT=NO
SOCIAL_RATE_GATE_IMPLEMENTED=YES
LOCAL_FALLBACK_LIMITER_IMPLEMENTED=YES
SOCIAL_REDIS_CIRCUIT_BREAKER_IMPLEMENTED=YES
SEARCH_REDIS_DOWN=503
PROFILE_REDIS_DOWN=AVAILABLE_WITH_READ_LIMIT
BLOCK_REDIS_DOWN=AVAILABLE
SEND_FRIEND_REQUEST_REDIS_DOWN=AVAILABLE_WITH_LOCAL_AND_DURABLE_LIMITS
ACCEPT_FRIEND_REQUEST_REDIS_DOWN=AVAILABLE_WITH_LOCAL_AND_ATOMIC_FRIEND_LIMITS
FOLLOW_REDIS_DOWN=503
UNFOLLOW_REDIS_DOWN=AVAILABLE_WITH_SAFETY_LIMIT
PRIVACY_TIGHTENING_REDIS_DOWN=AVAILABLE_WITH_SAFETY_LIMIT
PRIVACY_LOOSENING_REDIS_DOWN=503
PRESENCE_REDIS_DOWN=UNKNOWN
RATE_LIMIT_429_OPENS_CIRCUIT=NO
REDIS_DEGRADATION_500_RESPONSES=0_IN_EXECUTED_HTTP_MATRIX
FIRESTORE_REMAINS_SOCIAL_AUTHORITY=YES
NEW_FIRESTORE_RATE_LIMIT_WRITES=0
BACKEND_TESTS=591 PASS / 17 SKIPPED
S1_4A_2_FOCUSED_TESTS=41 PASS
FIRESTORE_EMULATOR_TESTS=27 PASS / 1 FAIL
S1_4A_1_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
S1_3_REGRESSION=FAIL_EMULATOR_CONCURRENT_UNFOLLOW_BLOCK_LOCK_TIMEOUT
SOCIAL_CLIENT_TESTS=78 PASS
UNITY_STATIC_COMPILATION=PASS
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_MEASURED
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
S1_4A_2_FILES_MODIFIED=15
PREEXISTING_USER_FILES=97
GENERATED_BY_VALIDATION_FILES=0_GIT_VISIBLE
UNEXPECTED_FILES=0
PREEXISTING_USER_FILES_PRESERVED=YES_SHA256
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_2=NO
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
REDIS_PUBSUB_IMPLEMENTED=NO
SOCIAL_OUTBOX_IMPLEMENTED=NO
AUTHORIZATION_REVISIONS_IMPLEMENTED=NO
PRESENCE_IMPLEMENTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4A_COMPLETE=NO
S1_4A_3_STARTED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
NEXT=S1.4A.2 REVIEW — RESOLVE EMULATOR REGRESSION BEFORE CHECKPOINT
```

<!-- A2R_HISTORICAL_END -->

## Current resolution machine block

```text
S1.4A.2R CONCURRENCY RESOLUTION
==============================
SOURCE_SHA=dc9b12281f0d0330bf564792cf784a6ba3d64d22
ORIGINAL_FAILURE=ABORTED Transaction lock timeout wrapped in ExecutionException
ORIGINAL_TEST=FollowEmulatorTests.real concurrency directional shared counters block and privacy
ROOT_CAUSE=WRAPPED_API_EXCEPTION_PREVENTED_SDK_TRANSACTION_RETRY
A2_CAUSED_REGRESSION=NO
S1_3_BASELINE_RUNS=10
S1_3_BASELINE_PASS=8
S1_3_BASELINE_FAIL=2
CURRENT_ISOLATED_RUNS=10
CURRENT_ISOLATED_PASS=6
CURRENT_ISOLATED_FAIL=4
POST_FIX_ISOLATED_RUNS=10
POST_FIX_ISOLATED_PASS=10
POST_FIX_ISOLATED_FAIL=0
FOLLOW_TRANSACTION_DOCUMENTS=PROFILE,EDGE,COUNTERS,PLAYERS,TEST_MARKERS,BLOCKS,PRIVACY,IDENTITY
BLOCK_TRANSACTION_DOCUMENTS=BLOCK_PROJECTIONS,PAIR,FRIENDSHIP,PENDING_REQUEST,COUNTERS,FOLLOW_EDGES
OVERLAPPING_DOCUMENTS=SOCIAL_COUNTERS,FOLLOW_EDGES,BLOCK_DOCUMENT
PRODUCTION_FIX_REQUIRED=YES
FIX_CLASS=A_PRODUCTION_TRANSACTION_RETRY_CORRECTION
FILES_CHANGED_BY_FIX=2_PRODUCTION;1_NEW_EMULATOR_TEST;1_REPORT
ARBITRARY_SLEEP_ADDED=NO
TEST_WEAKENED=NO
REDIS_LOCK_ADDED=NO
FOLLOW_BLOCK_STRESS_RUNS=50
FOLLOW_BLOCK_STRESS_PASS=50
FOLLOW_BLOCK_STRESS_FAIL=0
FOLLOW_BLOCK_CONCURRENCY=PASS
BLOCK_PRECEDENCE=PASS
FIRESTORE_EMULATOR_TESTS=31 PASS
FIRESTORE_EMULATOR_FAILS=0
SOCIAL_UNIT_TESTS=82 PASS / 3 SKIPPED
S1_4A_2_FOCUSED_TESTS=39 PASS REEXECUTED;2 UNAFFECTED REDIS PASS FROM A2
S1_4A_2_REGRESSION=PASS
S1_3_REGRESSION=PASS
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
PREEXISTING_USER_FILES_PRESERVED=YES_97_SHA256
PREEXISTING_A2_SOURCE_FILES_PRESERVED=YES_14_SHA256
UNRELATED_USER_FILES_MODIFIED=NO
S1_4A_2_READY_FOR_CHECKPOINT=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4A_3_STARTED=NO
NEXT=S1.4A.2 CHECKPOINT
```
