# CUBAN DOMINO CLUB — S1.4A.3 DISTRIBUTED INVALIDATION REPORT

Implemented authorization infrastructure only. No Social Presence, client protocol, Unity UI, Party or Spectator was added. Existing G3 presence code remains unchanged. No commit, push, production calls, deployment or swarm execution.

## Architecture and source evidence

- `social/Friendships.kt`: `SocialPair.authorizationRevision` defaults to 0; `resolve` increments only successful acceptance, preserving both users' atomic entitlement limits. `removeInTransaction` combines Block cleanup and the revision/outbox write without read-after-write operations. No request-generation reuse.
- `social/FirestoreSocialRepository.kt`: `block` includes revision and outbox in the original transaction; effective Unblock does likewise. `patchPrivacy` reuses `socialSettings.revision` and emits only for presenceVisibility/matchActivityVisibility changes. No-op or unrelated settings changes create no event.
- `social/FirestoreFriendships.kt`: retried callbacks return their own committed event list. `ApiFutures.transform` attaches local invalidation to SDK completion, including success after the caller stops waiting. The A.2 direct-ApiException retry normalization remains intact.
- `social/SocialInvalidation.kt`: internal version-1 events, deterministic IDs from scope + revision, server commit timestamp. Transaction retries create one logical durable event, not one per attempt. No token/profile/match payload.
- `social/FirestoreSocialInvalidationFeed.kt`: outbox doubles as durable feed. Published events remain queryable. Cursor orders server commit time and document ID; inclusive server readTime fence avoids same-timestamp gaps. Batches are at most 20. No global social sequence or per-process persistent watermark documents.
- `social/SocialInvalidationRuntime.kt`: listener-driven pending dispatch; duplicate publication is safe. Mark published only after publish succeeds. Both dispatchers may publish the same event without losing it. Failed acknowledgement leaves the event available for retry. Separate command-rate and Pub/Sub failure domains. Publication retry 1/2/4/8/16/30 seconds; subscriber recovery 1s x2 to 30s. Bounded callback executors; no thread per event.
- `social/LocalSocialAuthorizationIndex.kt`: lazy target/viewer/pair/connection indexes, 50 handles/connection and 10,000 local connections maximum. Synchronous invalidation precedes asynchronous consumer-specific durable authorization. Generation checks reject in-flight stale results. Duplicate/lower revisions cannot restore delivery. Identical reader/viewer/target checks are coalesced within a batch. Rejected work and repository failures remain closed.
- `realtime/RealtimeHandler.kt`: cleanup invokes local `closeConnection`; no writer/queue/protocol changes. Account changes also clear previous handles in registration. No global socket registry.

### Safety and recovery timing

With active handles the independent durable feed reconciles every five seconds, one bounded page per tick. Backlog recovery closes delivery until caught up. Delivery checks a monotonic 30-second lease without any network request. A silent Pub/Sub loss therefore cannot preserve permission indefinitely. Redis recovery requests reconciliation; it never makes Pub/Sub metadata authoritative.

Cross-instance revocation is bounded/eventually consistent, **not a claim of globally instantaneous or linearizable revocation**. Under load or blocked I/O the five-second cadence can slip; expired recovery/authorization leases deny delivery. This is infrastructure for future consumers; no actual Presence payload is delivered in A.3.

At startup or after a period with zero handles, old permissions do not survive. The runtime invalidates first, obtains a fresh server read fence, and reauthorizes registered handles durably. It does not scan unused idle history. Active instances keep an in-memory cursor. New processes do not inherit old authorization snapshots. Firestore failure stops authorization progress and closes delivery.

### Retention

Events are retained throughout A.3 validation. No delete-after-publish, TTL or cleanup job was added. Safe production retention/cleanup remains pending; a future bounded-retention design must force fresh durable reauthorization when a cursor falls outside its retained window. This follows the explicit section 158 preference for retaining events over unsafe deletion. Storage growth is a documented limitation, not an implemented retention guarantee.

## Measured cost

Repository-operation instrumentation against Emulator, excluding retries unless triggered, watch-delivery billing, transport setup and unrelated bootstrap reads. These are not billing guarantees.

| Mutation | Reads | Writes | Additional reads | Additional writes |
|---|---:|---:|---:|---:|
| Block stranger | 8 | 4 | 0 | 2 |
| Block friendship + mutual Follow | 8 | 13 | 0 | 2 |
| Block pending request + mutual Follow | 9 | 11 | 0 | 1 (pair already written) |
| Unblock | 3 | 4 | 1 | 2 |
| Accept | 17 | 8 | 0 | 1 |
| Unfriend | 5 | 7 | 0 | 2 |
| Relevant Privacy | 4 | 2 | 0 | 1 |

The maxima 9 reads/13 writes are separate valid Block scenarios, not a claim that both maxima occur together. Failed/no-op mutations add no outbox event. Follow, Unfollow, pending request mutations and unrelated privacy edits add zero authorization events.

Dispatch queries measured 1 read for one row and 20 for twenty rows. Publication acknowledgement adds 1 write/event, zero reads. Feed recovery measured 1/20 reads for 1/20 events and the minimum 1 for an empty query. A 20-event full page may require an additional empty query to establish catch-up. The pending listener also incurs initial/change document reads; these watch reads are **not included** in the synchronous query instrumentation. Idle pending dispatch does not repeatedly query Firestore. Active recovery incurs up to 12 empty queries/minute/instance when no events change; with no handles recovery queries are zero.

For a fixed 20-event backlog, the feed costs the same 20 reads (+ possible empty completion read) whether an instance has 100, 1,000 or 10,000 handles. Reauthorization scales with distinct affected consumer/viewer/target groups, not all handles indiscriminately. The concrete five-document test reader used 5 reads for 1, 20 and 50 equivalent handles. For 100/1,000/10,000 distinct affected groups this reader would require 500/5,000/50,000 reads; this is an upper-scenario estimate, not a measurement or future Presence implementation. Initial registration is independently authorized; coalescing is for invalidation batches.

Application Pub/Sub operations: one PUBLISH/event, zero Redis commands per received event, one channel subscription on reconnect (transport/client handshake excluded). No heartbeat/gameplay Firestore reads were added.

## Memory and execution bounds

Conservative estimate ~1.5 KiB/handle including strings, hash entries, memberships and handle metadata: 100 ~150 KiB; 1,000 ~1.46 MiB; 10,000 ~14.65 MiB. This is not a measured production heap. Callback closures/consumer data and JVM allocator overhead vary. Reauthorization uses two workers and a 256-entry queue; Pub/Sub delivery uses one worker and a 128-entry queue. Rejection leaves handles closed and eligible for a bounded later refresh. The index allocates nothing for unregistered connections.

## Validation evidence and limitations

The complete backend and Emulator suites were rerun after integration. Focused cases cover five atomic rollback paths, deterministic SDK retry, duplicate Block concurrency, legacy defaults, zero-noop emission, cursor pagination, retained published events, coalesced reads, two independent runtime/index instances sharing Emulator + local Redis, no-broker feed recovery, malformed messages, actual Pub/Sub connection kill and automatic reconnect. Tests use demo-domino-f0 on 127.0.0.1:18085 and a disposable Redis on 127.0.0.1:16379. The ordinary test task forces a non-production emulator endpoint.

Counter categories below may overlap scenarios; they must not be added together as independent test methods. Complete class counts are in the appendix. A.1 writer sources remain unchanged; the only realtime change is lifecycle cleanup. A.2 rate policy, both-user friend capacity, Block precedence and its 50-race Emulator regression remain preserved.

Unity Social client tests: 78 PASS. Static Unity runtime/editor source compilation: PASS, existing warnings. Unity EditMode, PlayMode, manual UI and Console inspection were not run; Console errors are not asserted to be zero. No Unity source or protocol changed. The candidate is therefore qualified by documented validation limits.

Actual two-process chaos, Redis server restart/load, production throughput and production retention cleanup remain outside A.3 / pending A.4 review. The reconnect test kills Pub/Sub connections only in the disposable test broker; no existing Redis was stopped. The test-only durable authorization reader exercises visibility decisions without adding a production Presence service. No credentials or tokens are recorded here.

Micrometer aggregate counters cover outbox creation, publication/failure, receipt, duplicate/stale revision, reauthorization/revocation and feed recovery. No UID, pair, event or public ID labels. Safe logs expose only failure category. No global health redesign.

## Schema/security

`socialPairs.authorizationRevision`: non-negative Long, lazily default 0, monotonic checked addition; no migration. `socialAuthorizationOutbox/{eventId}`: eventId/type/revision/pairId-or-targetUid/schemaVersion/createdAt/published. The same collection is the feed. Existing deny-all client Firestore rules cover the new collection; the Emulator rules test explicitly checks it. No new composite index is required by these single-field predicates/orderings; Emulator queries validated. No rules/index configuration or deployment changes.

## Required report fields

```text
BRANCH=main
SOURCE_SHA_BEFORE=27585269d35f853fccf7921769c9f7cd51e2e43d
SOURCE_SHA_AFTER=27585269d35f853fccf7921769c9f7cd51e2e43d
TARGET_PRIVACY_REVISION_SOURCE=players/{uid}/socialSettings/current.revision
PAIR_AUTHORIZATION_REVISION_FIELD=socialPairs/{canonicalPairId}.authorizationRevision
PAIR_REVISION_DEFAULT_FOR_LEGACY_DOCS=0
OUTBOX_COLLECTION=socialAuthorizationOutbox
OUTBOX_EVENT_ID_STRATEGY=type + hash(canonical pair or target) + committed revision
OUTBOX_SCHEMA_VERSION=1
OUTBOX_DELIVERY_SEMANTICS=AT_LEAST_ONCE
OUTBOX_RETENTION_POLICY=Retain during A.3; no automatic deletion; cleanup pending safe recovery-window proof
OUTBOX_IDLE_BEHAVIOR=Bounded pending listener; no pending-query polling while idle; retry 1/2/4/8/16/30 seconds
FEED_ORDERING=Firestore server commit timestamp ASC + eventId ASC
FEED_CURSOR_OR_WATERMARK=Per-instance in-memory timestamp + document ID; inclusive server readTime fence after catch-up
FEED_RECOVERY_BATCH_SIZE=20
PUBSUB_CHANNEL=domino:v1:social:authorization-invalidated
PUBSUB_SCHEMA_VERSION=1
PUBSUB_RECONNECT_BACKOFF=Spring listener 1s x2 to 30s; Lettuce transport auto-reconnect
MAX_HANDLES_PER_CONNECTION=50
LOCAL_INDEX_MEMORY_100=~150 KiB estimate
LOCAL_INDEX_MEMORY_1000=~1.46 MiB estimate
LOCAL_INDEX_MEMORY_10000=~14.65 MiB estimate
REAUTHORIZATION_FAILURE_BEHAVIOR=FAIL_CLOSED
BLOCK_MAX_TRANSACTION_READS=9
BLOCK_MAX_TRANSACTION_WRITES=13
ACCEPT_MAX_TRANSACTION_READS=17
ACCEPT_MAX_TRANSACTION_WRITES=8
BLOCK_INVALIDATION_EXTRA_READS=0
BLOCK_INVALIDATION_EXTRA_WRITES=2 (1 if pair was already updated by pending-request cleanup)
UNBLOCK_INVALIDATION_EXTRA_READS=1
UNBLOCK_INVALIDATION_EXTRA_WRITES=2
ACCEPT_INVALIDATION_EXTRA_READS=0
ACCEPT_INVALIDATION_EXTRA_WRITES=1
UNFRIEND_INVALIDATION_EXTRA_READS=0
UNFRIEND_INVALIDATION_EXTRA_WRITES=2
PRIVACY_INVALIDATION_EXTRA_READS=0
PRIVACY_INVALIDATION_EXTRA_WRITES=1
OUTBOX_DISPATCH_1_READS=1
OUTBOX_DISPATCH_20_READS=20
FEED_RECOVERY_EMPTY_READS=1
FEED_RECOVERY_20_READS=20
FEED_RECOVERY_1_EVENT_READS=1
FEED_RECOVERY_20_EVENTS_READS=20
FEED_RECOVERY_NO_EVENTS_READS=1
INVALIDATION_PUBLISH_REDIS_OPS=1
INVALIDATION_RECEIVE_REDIS_OPS=0
PUBSUB_RECONNECT_REDIS_OPS=1 channel subscription; transport handshake commands excluded
S1_4A_3_FOCUSED_TESTS=21 PASS (9 unit + 3 Redis + 8 Emulator + 1 distributed Emulator/Redis)
BACKEND_TESTS=605 PASS / 15 SKIPPED (local Redis opt-ins enabled)
FIRESTORE_EMULATOR_TESTS=40 PASS
REDIS_PUBSUB_HEALTHY_TESTS=1 PASS
REDIS_PUBSUB_FAILURE_TESTS=1 PASS
REDIS_PUBSUB_RECOVERY_TESTS=1 PASS real CLIENT KILL/reconnect
CROSS_INSTANCE_BLOCK_TESTS=2 PASS scenarios (Redis components + Emulator/runtime)
CROSS_INSTANCE_PRIVACY_TESTS=2 PASS scenarios
CROSS_INSTANCE_UNFRIEND_TESTS=2 PASS scenarios
DUPLICATE_EVENT_TESTS=2 PASS scenarios
OUT_OF_ORDER_EVENT_TESTS=2 PASS scenarios
MISSED_PUBSUB_TESTS=2 PASS (feed paging + runtime without broker)
TRANSACTION_RETRY_TESTS=1 A.3 forced ABORTED PASS + existing A.2 retry regression
LOCAL_INDEX_TESTS=5 PASS
SOCIAL_CLIENT_TESTS=78 PASS
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
UNITY_COMPILATION=STATIC_COMPILATION PASS
CONSOLE_ERRORS=NOT_MEASURED (Unity not launched)
SEARCH_REDIS_DOWN=503
PROFILE_REDIS_DOWN=AVAILABLE_WITH_READ_LIMIT
BLOCK_REDIS_DOWN=AVAILABLE
UNBLOCK_REDIS_DOWN=AVAILABLE_WITH_SAFETY_LIMIT
SEND_FRIEND_REQUEST_REDIS_DOWN=AVAILABLE_WITH_SEND_ACCEPT_LIMIT
ACCEPT_FRIEND_REQUEST_REDIS_DOWN=AVAILABLE_WITH_SEND_ACCEPT_LIMIT
FOLLOW_REDIS_DOWN=503
UNFOLLOW_REDIS_DOWN=AVAILABLE_WITH_SAFETY_LIMIT
PRIVACY_TIGHTENING_REDIS_DOWN=AVAILABLE_WITH_SAFETY_LIMIT
PRIVACY_LOOSENING_REDIS_DOWN=503
REDIS_DEGRADATION_500_RESPONSES=0
APPLICATION_PRODUCER_DIRECT_SEND=0
PAIR_AUTHORIZATION_REVISION_SCHEMA_CHANGE=New Long authorizationRevision, legacy default 0, no eager migration
OUTBOX_SCHEMA_CHANGE=New server-only collection: eventId,type,revision,pairId/targetUid,schemaVersion,createdAt,published
FEED_SCHEMA_CHANGE=Same outbox collection; no second durable copy
RETENTION_DURATION=No expiration in A.3 validation; production cleanup policy deferred
RECOVERY_WINDOW=All retained events after local cursor; new processes have no surviving permissions and establish a fresh fence
WATERMARK_RETENTION_INTERACTION=Never delete required events; no cleanup implemented
REDIS_COMMAND_PATH_DOWN=A.2 operation policies unchanged; Pub/Sub failure domain independent
REDIS_PUBSUB_DOWN=Durable mutations + local invalidation continue; feed reconciliation and 30s delivery lease prevent indefinite stale permission
FIRESTORE_DOWN=Durable mutations cannot commit; failed recovery/reauthorization closes delivery; retry bounded
SINGLE_BACKEND_INSTANCE_DOWN=Local sockets/handles lost; peer instances independent; fresh startup fence and durable reauthorization
MISSED_PUBSUB_EVENT=Feed detects within scheduled recovery; delivery lease closes if recovery stalls
DUPLICATE_PUBSUB_EVENT=Revision comparison ignores duplicates
OUT_OF_ORDER_PUBSUB_EVENT=Lower revision cannot replace newer snapshot
OUTBOX_DISPATCHER_DOWN=Committed feed remains available independently of published flag; other dispatchers may retry
S1_4A_3_FILES_MODIFIED=16
FILES_MODIFIED=16
PREEXISTING_USER_FILES=97
GENERATED_BY_VALIDATION_FILES=Ignored build/test outputs and Generated/S14A3 evidence; see classification
UNEXPECTED_FILES=0
S1_4A_3_SUCCESS=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_4A_3_READY_FOR_CHECKPOINT=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
NEXT=S1.4A.3 REVIEW/CHECKPOINT
PAIR_AUTHORIZATION_REVISION_IMPLEMENTED=YES
PAIR_REVISION_MONOTONIC=YES
SOCIAL_AUTH_OUTBOX_IMPLEMENTED=YES
OUTBOX_CREATED_IN_SAME_TRANSACTION=YES
OUTBOX_RETRY_SAFE=YES
OUTBOX_MULTI_DISPATCHER_SAFE=YES
DURABLE_INVALIDATION_FEED_IMPLEMENTED=YES
REDIS_PUBSUB_IMPLEMENTED=YES
LOCAL_AUTHORIZATION_INDEX_IMPLEMENTED=YES
INDEX_BY_TARGET=YES
INDEX_BY_VIEWER=YES
INDEX_BY_PAIR=YES
INDEX_LAZY_ALLOCATION=YES
INDEX_CLEANUP_ON_CONNECTION_CLOSE=YES
INDEX_CLEANUP_ON_ACCOUNT_SWITCH=YES
INVALIDATE_BEFORE_REAUTHORIZE=YES
LEGACY_DOCUMENT_COMPATIBILITY=YES
PREEXISTING_USER_FILES_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
PREVIOUS_S1_4_REPORTS_PRESERVED=YES
S1_4A_3_IMPLEMENTED=YES
LOCAL_IMMEDIATE_INVALIDATION=PASS
CROSS_INSTANCE_INVALIDATION=PASS
BLOCK_INVALIDATION=PASS
UNBLOCK_INVALIDATION=PASS
FRIEND_ACCEPT_INVALIDATION=PASS
UNFRIEND_INVALIDATION=PASS
PRIVACY_INVALIDATION=PASS
MISSED_PUBSUB_EVENT_SAFETY=PASS
DUPLICATE_EVENT_SAFETY=PASS
OUT_OF_ORDER_EVENT_SAFETY=PASS
BLOCK_OUTBOX_ATOMICITY=PASS
UNBLOCK_OUTBOX_ATOMICITY=PASS
FRIEND_ACCEPT_OUTBOX_ATOMICITY=PASS
UNFRIEND_OUTBOX_ATOMICITY=PASS
PRIVACY_OUTBOX_ATOMICITY=PASS
MISSED_PUBSUB_RECOVERY=PASS
INSTANCE_STARTUP_RECOVERY=PASS
PUBSUB_RECONNECT_RECOVERY=PASS
PUBSUB_FAST_PATH=PASS
PUBSUB_RECONNECT=PASS
PUBSUB_MESSAGE_VALIDATION=PASS
S1_4A_1_REGRESSION=PASS
S1_4A_2_REGRESSION=PASS
AUTH_REGRESSION=PASS
PROFILE_REGRESSION=PASS
ENTITLEMENTS_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
S1_3_REGRESSION=PASS
I4_REGRESSION=PASS
M5_REGRESSION=PASS
MATCHMAKING_REGRESSION=PASS
MONETIZATION_REGRESSION=PASS
F0_F01_REGRESSION=PASS
HEARTBEAT_REGRESSION=PASS
RECONNECT_REGRESSION=PASS
MATCH_WEBSOCKET_REGRESSION=PASS
FOLLOW_BLOCK_CONCURRENCY=PASS
BLOCK_PRECEDENCE=PASS
SINGLE_WRITER_PER_CONNECTION=PASS
CRITICAL_FIFO=PASS
CONTROL_FAIRNESS=PASS
MATCH_UPDATE_COMMAND_ORDER=PASS
SLOW_SOCKET=PASS
WRITER_FAILURE_CLEANUP=PASS
BLOCK_INCREMENTS_PAIR_REVISION=YES
UNBLOCK_INCREMENTS_PAIR_REVISION=YES
FRIEND_ACCEPT_INCREMENTS_PAIR_REVISION=YES
UNFRIEND_INCREMENTS_PAIR_REVISION=YES
FRIEND_REQUEST_GENERATION_REUSED_AS_AUTH_REVISION=NO
GLOBAL_SOCIAL_REVISION_IMPLEMENTED=NO
FOLLOW_INCREMENTS_PAIR_REVISION=NO
UNFOLLOW_INCREMENTS_PAIR_REVISION=NO
FULL_HISTORY_SCAN_REQUIRED=NO
PUBSUB_RAW_PAYLOAD_FORWARDED_TO_CLIENT=NO
PUBSUB_IS_AUTHORIZATION_AUTHORITY=NO
GLOBAL_SOCKET_REGISTRY_IMPLEMENTED=NO
DELIVERY_ALLOWED_WHILE_INVALIDATED=NO
STALE_EVENT_CAN_RESTORE_PERMISSION=NO
PUBSUB_EVENT_IS_DURABLE_AUTHORITY=NO
TRANSACTION_RETRY_DUPLICATES_LOGICAL_EVENT=NO
RATE_LIMIT_429_OPENS_CIRCUIT=NO
MIGRATION_REQUIRED=NO
FIRESTORE_RULES_CHANGED=NO
FIRESTORE_INDEXES_CHANGED=NO
FIRESTORE_RULES_DEPLOYED=NO
FIRESTORE_INDEXES_DEPLOYED=NO
RETENTION_CLEANUP_IMPLEMENTED=NO
PRESENCE_IMPLEMENTED=NO
ONLINE_IMPLEMENTED=NO
OFFLINE_IMPLEMENTED=NO
IN_MATCH_IMPLEMENTED=NO
PRESENCE_REDIS_STATE_IMPLEMENTED=NO
PRESENCE_WEBSOCKET_PROTOCOL_IMPLEMENTED=NO
UNITY_PRESENCE_UI_IMPLEMENTED=NO
S1_4A_4_STARTED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
BOT_SWARM_STARTED=NO
BACKEND_DEPLOYED=NO
UNITY_DEPLOYED=NO
GOOGLE_PLAY_UPLOAD=NO
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_3=NO
S1_4A_COMPLETE=NO
FOLLOW_INVALIDATION_EVENTS=0
UNFOLLOW_INVALIDATION_EVENTS=0
SEND_REQUEST_INVALIDATION_EVENTS=0
DECLINE_REQUEST_INVALIDATION_EVENTS=0
CANCEL_REQUEST_INVALIDATION_EVENTS=0
FIRESTORE_AUTH_READS_PER_HEARTBEAT=0
SOCIAL_INVALIDATION_POLLING_PER_HEARTBEAT=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
CHECKPOINT_GATE_1=PASS
CHECKPOINT_GATE_2=PASS
CHECKPOINT_GATE_3=PASS
CHECKPOINT_GATE_4=PASS
CHECKPOINT_GATE_5=PASS
CHECKPOINT_GATE_6=PASS
CHECKPOINT_GATE_7=PASS
CHECKPOINT_GATE_8=PASS
CHECKPOINT_GATE_9=PASS
CHECKPOINT_GATE_10=PASS
```

## Exact changed-file classification

### S1_4A_3_INTENTIONAL

- `client/Validation/S1_4A_3_DISTRIBUTED_INVALIDATION_REPORT.md`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreFriendships.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialInvalidationFeed.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialRepository.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/FriendshipController.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/Friendships.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/LocalSocialAuthorizationIndex.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialController.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialInvalidation.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialInvalidationRuntime.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialInvalidationDistributedEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialInvalidationEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialInvalidationRedisTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialInvalidationTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRulesEmulatorTests.kt`

### PRE_EXISTING_USER_CHANGE — SHA-256 unchanged

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

Ignored only: `client/Validation/Generated/S14A3/**`, existing ignored `Generated/S11Tests/**`, static-compile DLL/RSP artifacts in `client/Validation/Generated/`, and `server/domino/build/**` (JUnit XML/HTML, class files, local Emulator logs and A.3 test logs). None is staged.

UNEXPECTED=0. The inventory compares every Git-modified/untracked file to the initial 97-path baseline, with SHA-256 verification.

## Exact test class counts

| Suite/class | Passed | Skipped |
|---|---:|---:|
| com.teamfho.domino.catalog.DuelCatalogTests | 16 | 0 |
| com.teamfho.domino.catalog.GameCatalogApiTests | 2 | 0 |
| com.teamfho.domino.catalog.GameCatalogTests | 31 | 0 |
| com.teamfho.domino.catalog.GameCatalogV3Test | 1 | 0 |
| com.teamfho.domino.config.FirebaseAdminConfigurationTests | 9 | 0 |
| com.teamfho.domino.DominoApplicationTests | 1 | 0 |
| com.teamfho.domino.economy.reward.AdMobSsvTests | 13 | 0 |
| com.teamfho.domino.economy.reward.MonetizationPolicyTests | 23 | 0 |
| com.teamfho.domino.economy.reward.PersistentMonetizationPolicyTests | 24 | 0 |
| com.teamfho.domino.economy.reward.PersistentPolicyControllerTests | 1 | 0 |
| com.teamfho.domino.economy.reward.PersistentPolicyRewardIntegrationTests | 4 | 0 |
| com.teamfho.domino.economy.reward.RewardConsumptionTests | 26 | 0 |
| com.teamfho.domino.economy.reward.RewardControllerTests | 24 | 0 |
| com.teamfho.domino.economy.reward.RewardIntentTests | 13 | 0 |
| com.teamfho.domino.entitlement.EntitlementHistoryTests | 5 | 0 |
| com.teamfho.domino.entitlement.EntitlementHttpTests | 2 | 0 |
| com.teamfho.domino.entitlement.EntitlementTests | 10 | 0 |
| com.teamfho.domino.match.MatchFoundationTests | 31 | 0 |
| com.teamfho.domino.match.MatchHistoryHttpTests | 4 | 0 |
| com.teamfho.domino.match.ReplayHttpTests | 3 | 0 |
| com.teamfho.domino.match.ReplayTests | 5 | 0 |
| com.teamfho.domino.matchmaking.MatchmakingContractTests | 3 | 0 |
| com.teamfho.domino.matchmaking.PartnersMatchmakingTests | 0 | 3 |
| com.teamfho.domino.matchmaking.RedisMatchmakingTests | 0 | 8 |
| com.teamfho.domino.online.OnlineHttpTests | 2 | 0 |
| com.teamfho.domino.online.OnlineMatchTests | 14 | 0 |
| com.teamfho.domino.online.OnlineParticipantProfileTests | 3 | 0 |
| com.teamfho.domino.online.OnlineTransportTests | 1 | 0 |
| com.teamfho.domino.online.OnlineTurnTests | 20 | 0 |
| com.teamfho.domino.online.ParticipantPersistenceTests | 3 | 0 |
| com.teamfho.domino.online.PartnersOnlineTests | 3 | 1 |
| com.teamfho.domino.online.RedisTurnIndexTests | 0 | 1 |
| com.teamfho.domino.online.TurnIndexTests | 5 | 0 |
| com.teamfho.domino.player.PlayerBootstrapServiceTests | 7 | 0 |
| com.teamfho.domino.player.PlayerControllerTests | 33 | 0 |
| com.teamfho.domino.player.PlayerDisplayNameTests | 39 | 0 |
| com.teamfho.domino.player.PlayerFoundationRepositoryTests | 35 | 0 |
| com.teamfho.domino.RealFirestoreGuardTests | 2 | 0 |
| com.teamfho.domino.realtime.ConnectionOutboundTests | 20 | 0 |
| com.teamfho.domino.realtime.OutboundPayloadTests | 1 | 0 |
| com.teamfho.domino.realtime.OutboundTransportTests | 2 | 0 |
| com.teamfho.domino.realtime.RealtimeHandlerTests | 19 | 0 |
| com.teamfho.domino.realtime.RealtimeProtocolTests | 4 | 0 |
| com.teamfho.domino.realtime.RedisPresenceTests | 0 | 1 |
| com.teamfho.domino.security.FirebaseAdminTokenVerifierTests | 21 | 0 |
| com.teamfho.domino.security.FirebaseSecurityTests | 23 | 0 |
| com.teamfho.domino.social.FollowRateFailureTests | 1 | 0 |
| com.teamfho.domino.social.FollowRedisRateTests | 0 | 1 |
| com.teamfho.domino.social.FollowTests | 9 | 0 |
| com.teamfho.domino.social.FriendshipTests | 13 | 0 |
| com.teamfho.domino.social.SocialHttpTests | 6 | 0 |
| com.teamfho.domino.social.SocialInvalidationRedisTests | 3 | 0 |
| com.teamfho.domino.social.SocialInvalidationTests | 9 | 0 |
| com.teamfho.domino.social.SocialRateGateRedisTests | 2 | 0 |
| com.teamfho.domino.social.SocialRateGateTests | 36 | 0 |
| com.teamfho.domino.social.SocialTests | 17 | 0 |
| com.teamfho.domino.ValidationServerCleanupTests | 1 | 0 |
| com.teamfho.domino.entitlement.EntitlementEmulatorTests | 3 | 0 |
| com.teamfho.domino.entitlement.EntitlementRulesEmulatorTests | 1 | 0 |
| com.teamfho.domino.match.ReplayEmulatorTests | 1 | 0 |
| com.teamfho.domino.online.FirestoreEmulatorTests | 6 | 0 |
| com.teamfho.domino.social.FollowEmulatorTests | 4 | 0 |
| com.teamfho.domino.social.FriendshipEmulatorTests | 4 | 0 |
| com.teamfho.domino.social.SocialEmulatorTests | 5 | 0 |
| com.teamfho.domino.social.SocialInvalidationDistributedEmulatorTests | 1 | 0 |
| com.teamfho.domino.social.SocialInvalidationEmulatorTests | 8 | 0 |
| com.teamfho.domino.social.SocialRateGateEmulatorTests | 3 | 0 |
| com.teamfho.domino.social.SocialRulesEmulatorTests | 1 | 0 |
| com.teamfho.domino.social.SocialTransactionRetryEmulatorTests | 3 | 0 |

## S1.4A.3 CHECKPOINT CANDIDATE

```text
BRANCH=main
SOURCE_SHA_BEFORE=27585269d35f853fccf7921769c9f7cd51e2e43d
SOURCE_SHA_AFTER=27585269d35f853fccf7921769c9f7cd51e2e43d
S1_4A_3_IMPLEMENTED=YES
S1_4A_3_SUCCESS=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_4A_3_READY_FOR_CHECKPOINT=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
CHECKPOINT_GATE_1=PASS
CHECKPOINT_GATE_2=PASS
CHECKPOINT_GATE_3=PASS
CHECKPOINT_GATE_4=PASS
CHECKPOINT_GATE_5=PASS
CHECKPOINT_GATE_6=PASS
CHECKPOINT_GATE_7=PASS
CHECKPOINT_GATE_8=PASS
CHECKPOINT_GATE_9=PASS
CHECKPOINT_GATE_10=PASS
S1_4A_3_FOCUSED_TESTS=21 PASS (9 unit + 3 Redis + 8 Emulator + 1 distributed Emulator/Redis)
BACKEND_TESTS=605 PASS / 15 SKIPPED (local Redis opt-ins enabled)
FIRESTORE_EMULATOR_TESTS=40 PASS
SOCIAL_CLIENT_TESTS=78 PASS
UNITY_COMPILATION=STATIC_COMPILATION PASS
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_MEASURED (Unity not launched)
PREEXISTING_USER_FILES_PRESERVED=YES
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4A_COMPLETE=NO
S1_4A_4_STARTED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
NEXT=S1.4A.3 REVIEW/CHECKPOINT
```
