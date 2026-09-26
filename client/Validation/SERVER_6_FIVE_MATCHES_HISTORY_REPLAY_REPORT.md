# SERVER-6 FIVE MATCH EXECUTION â€” STOPPED BEFORE MATCHMAKING

Branch: main. Source: 99002cad817e8208c15195de7b5bf6ea795fc7d5.

## Defect S6-01: bootstrap entitlement summary discarded by Unity codec

The actual Unity TEST scene authenticated using its existing Firebase session. Its UID matched the normal application identity and differed from all three TEST opponent slots (compared internally; identifiers and tokens not emitted). History returned zero entries, no next page. The synchronized PlayerService had a null Entitlements value, so the preflight stopped before queue entry.

`Assets/_Domino/Scripts/Infrastructure/Api/UnityApiJsonCodec.cs`, ReadSuccess, constructs PlayerBootstrapResponseDto with only player and wallet, ignoring the entitlements field. PlayerService then passes result.entitlements to ReceiveEntitlements during bootstrap. The backend PlayerController explicitly includes entitlements in its bootstrap contract. This establishes the client deserialization omission; the live bootstrap response body was not captured, and backend entitlement availability was not independently measured.

This is a client subscription-state propagation defect, not evidence of missing replay data, failed Firebase authentication, or entitlement bypass. No matches were created. The separate profile UI has a refresh path; it does not make the bootstrap omission disappear. No claim is made that the backend Replay endpoint is unavailable.

## Reproduction and evidence

1. Launch installed Unity 6000.0.41f1 with explicit real-TEST validation opt-in and the existing TEST preset.
2. Enter the normal client scene and wait for fresh player synchronization and authenticated realtime connection.
3. Verify project and four distinct identities internally.
4. Read current user History via existing ReplayClient: count 0.
5. Inspect PlayerService.Entitlements: null; stop with ENTITLEMENT_UNAVAILABLE.

Safe output: `Generated/SERVER6/execution.txt`.
Temporary validation driver retained as non-imported evidence: `Generated/SERVER6/Server6Execution.cs.evidence`.
The temporary Editor driver and generated meta were removed from Assets after Unity exited. No application correction was made. Generated evidence is ignored by Git.

Console errors during preflight: 0. No visual History/Replay review ran. No Swarm was launched. No server restart, server modification, Cloudflare/DNS change, or direct Firestore operation was performed by this run. Normal TEST bootstrap/history/realtime activity occurred; its underlying Firestore operation counts were not measured.

All 97 pre-existing user files retain their baseline hashes. Only this report is an intentional new versionable file.

## Results

```text
BRANCH=main
SOURCE_SHA=99002cad817e8208c15195de7b5bf6ea795fc7d5
TEST_FIREBASE_PROJECT_ID=teamfho-domino
CURRENT_UNITY_USER_RESOLVED=YES
UNITY_USER_DISTINCT_FROM_SLOT_01=YES
UNITY_USER_DISTINCT_FROM_SLOT_02=YES
UNITY_USER_DISTINCT_FROM_SLOT_03=YES
DISTINCT_MATCH_PLAYERS=4_IDENTITIES_VERIFIED_NO_MATCH_CREATED
MATCHES_REQUESTED=5
MATCHES_STARTED=0
MATCHES_COMPLETED=0
EXTRA_MATCHES_STARTED=0
CURRENT_UNITY_USER_IN_ALL_MATCHES=NOT_RUN
SWARM_MODE=NOT_STARTED
SERVER6_MATCH_CONCURRENCY=0
MATCH_WSS_AUTH=NOT_RUN_NO_MATCH
MATCH_WSS_STABLE=NOT_RUN
HISTORY_COUNT_BEFORE=0
HISTORY_COUNT_AFTER=NOT_RUN
SERVER6_HISTORY_MATCHES_FOUND=0
HISTORY_ORDERING=NOT_RUN
HISTORY_AUTHORIZATION=OWN_HISTORY_READ_PASS_OTHER_CHECKS_NOT_RUN
UNITY_HISTORY_ENTRIES_VISIBLE=NOT_RUN
HISTORY_VISUAL_REVIEW=NOT_RUN
FREE_REPLAY_CONTRACT=LATEST_3_ELIGIBLE_MATCHES_PER_USER
FREE_ELIGIBLE_REPLAY_COUNT=NOT_MEASURED
DURABLE_REPLAY_COUNT=NOT_RUN
UNITY_FREE_REPLAY_WINDOW=NOT_RUN
ENTITLEMENT_BYPASS=NO
REPLAY_TERMINAL_STATE_MATCHES_AUTHORITY=NOT_RUN
REPLAY_SEQUENCE_REGRESSIONS=NOT_MEASURED
MATCH_EVENT_SEQUENCE_REGRESSIONS=NOT_MEASURED
API_RESTART=NOT_RUN
HISTORY_AFTER_API_RESTART=NOT_RUN
REPLAY_AFTER_API_RESTART=NOT_RUN
FREE_REPLAY_WINDOW_AFTER_RESTART=NOT_RUN
DURABLE_REPLAY_COUNT_AFTER_RESTART=NOT_RUN
REDIS_RESTARTED=NO
SERVER6_REDIS_CLEANUP=NOT_RUN
REAL_TEST_FIRESTORE_READS=NOT_MEASURED
REAL_TEST_FIRESTORE_WRITES=NOT_MEASURED
REAL_PRODUCTION_FIRESTORE_CALLS=0
SERVER6_ERROR_LINES=NOT_MEASURED
SERVER6_UNEXPECTED_EXCEPTIONS=NOT_MEASURED
API_CPU_SAMPLE=NOT_MEASURED
API_MEMORY_SAMPLE=NOT_MEASURED
REDIS_CPU_SAMPLE=NOT_MEASURED
REDIS_MEMORY_SAMPLE=NOT_MEASURED
HOST_RAM_AVAILABLE=NOT_MEASURED_THIS_RUN
HOST_LOAD=NOT_MEASURED_THIS_RUN
RESOURCE_SAMPLE_ONLY=YES
CONSOLE_ERRORS=0_PREFLIGHT_ONLY
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
PREEXISTING_USER_FILES_PRESERVED=YES_97_OF_97
COMMIT=NONE
PUSH=NONE
SERVER_6_SUCCESS=NO
DEFECT_FOUND=S6-01_BOOTSTRAP_CODEC_DROPS_ENTITLEMENTS
NEXT=SERVER-6 DEFECT REVIEW
```

MATCH_1_COMPLETED=NO
MATCH_1_HISTORY=NOT_RUN
MATCH_1_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_1_REPLAY_CONSISTENCY=NOT_RUN

MATCH_2_COMPLETED=NO
MATCH_2_HISTORY=NOT_RUN
MATCH_2_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_2_REPLAY_CONSISTENCY=NOT_RUN

MATCH_3_COMPLETED=NO
MATCH_3_HISTORY=NOT_RUN
MATCH_3_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_3_REPLAY_CONSISTENCY=NOT_RUN

MATCH_4_COMPLETED=NO
MATCH_4_HISTORY=NOT_RUN
MATCH_4_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_4_REPLAY_CONSISTENCY=NOT_RUN

MATCH_5_COMPLETED=NO
MATCH_5_HISTORY=NOT_RUN
MATCH_5_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_5_REPLAY_CONSISTENCY=NOT_RUN


## S6-01 correction candidate (completed checks; current-user plan discrepancy)

Original SERVER-6 stop above is retained. Before changing production code, a focused automated test against the actual UnityApiJsonCodec failed with FREE_ENTITLEMENTS_DROPPED. Root cause is the manual response constructor omitting the existing entitlements DTO field, not a missing DTO declaration. PlayerController adds EntitlementSummary to PlayerBootstrapResponse; EffectiveEntitlements uses plan FREE/PREMIUM and trial flags/sources (TRIAL is not a separate plan). The JSON names already match Unity DTO fields.

Minimal production correction: UnityApiJsonCodec.ReadSuccess maps the existing entitlements field via Newtonsoft into EntitlementSummaryDto. NullValueHandling.Ignore preserves the int default for maximum:null on unlimited limits. Missing/null envelopes remain null; an empty envelope has no snapshot, so existing UI shows unavailable. Unknown fields are ignored; malformed array input is rejected. Server authorization and latest-three replay window remain unchanged.

Contract trace: PlayerController -> PlayerBootstrapResponse.entitlements -> JSON entitlements -> PlayerBootstrapResponseDto.entitlements -> UnityApiJsonCodec -> PlayerService.ReceiveEntitlements -> EntitlementProfilePresentation. HistoryReplayView uses server-provided replayAvailable/premiumLocked; it does not locally authorize replay using bootstrap fields. Auth/player/wallet behavior remains unchanged.

Validation completed:
- Defect reproduction: PASS (expected pre-fix failure FREE_ENTITLEMENTS_DROPPED).
- 9 focused scenarios: FREE, PREMIUM, TRIAL, missing, null, empty, future fields, unavailable, malformed: PASS. Normal DominoApiClient -> PlayerService state tested for FREE/PREMIUM/TRIAL with fixture-only transport.
- Auth/Profile/realtime client suite: 469 PASS, no real network.
- Replay: 1595 checks PASS across 62 retained emulator matches and 31061 events; no real Firestore.
- Backend entitlement tests: 17 PASS, 0 failed, 0 skipped. Local test context logged unavailable Redis/catalog background dependencies; tests passed. These logs are not remote server errors.
- First Gradle selector also selected bot-swarm:test and failed because no entitlement tests exist there; corrected to :test and the 17 backend tests passed.
- Unity attempted validation could not open the project because another Editor owns it. Edit/Play tests and the post-fix remote wire/state precheck remain NOT_RUN pending closure of that Editor. The startup failure is not an application console-error measurement.
- Server contract verified in source; actual remote response entitlements and effective current-user FREE state remain unverified for this correction.

Intentional S6-01 paths:
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiJsonCodec.cs (production)
- client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S601CodecValidation.cs and its Unity meta (tests)
- client/Validation/PlayerFoundationClientTests.cs (register focused checks)
- client/Validation/RunPlayerFoundationClientTests.ps1 (include checks)
- this report

S6_01_SUCCESS=NO_STRICT_REMOTE_FREE_GATE_NOT_MET_CURRENT_USER_IS_TRIAL
SERVER_6_RESUME_ALLOWED=NO
MATCHES_STARTED=0
MATCHES_COMPLETED=0
BOT_SWARM_MATCHMAKING_STARTED=NO
WIRE_CONTRACT_CHANGED=NO
SERVER_CHANGE_REQUIRED=NO_FROM_SOURCE_CONTRACT
REAL_TEST_FIRESTORE_CALLS=0_DURING_S601_SO_FAR
REAL_PRODUCTION_FIRESTORE_CALLS=0
COMMIT=NONE
PUSH=NONE
NEXT=S6-01 REVIEW


### Completed Unity and remote validation (supersedes pending status above)

After the user closed their Editor, Unity 6000.0.41f1 compiled and passed 9 focused Edit Mode cases, the same 9 codec-to-PlayerService cases in isolated Play Mode, and 2 retained Replay reconstructions (DUEL and PARTNERS) in Play Mode. Console errors: 0. No real network in isolated tests.

Only after these checks, the current user authenticated through the normal Firebase flow against teamfho-domino. Normal bootstrap populated PlayerService.Entitlements. One additional normal bootstrap response was checked in memory: HTTP 200, entitlements object present, same plan as synchronized Unity state. No UID/token/raw player response was recorded. History remains empty. Remote console errors: 0.

Actual server-supplied state: availability AVAILABLE, plan PREMIUM, trialActive true, source PROMOTIONAL_TRIAL, REPLAY_MAX unlimited=true / maximum=null. The codec maps the unused null maximum to the DTO's default 0; unlimited remains true. This is NOT a zero replay allowance. No administrative grant, entitlement edit, or limit change was performed. Normal server bootstrap trial policy was left intact; this run cannot establish when the trial was originally granted.

Sanitized representative JSON shape (synthetic placeholders, no user identifiers):

```json
{
  "player": {"uid": "<redacted>", "accountType": "GUEST", "displayName": "<redacted>", "language": "es", "status": "ACTIVE"},
  "wallet": {"coins": "<redacted>"},
  "entitlements": {
    "availability": "AVAILABLE",
    "trialGranted": false,
    "snapshot": {
      "plan": "PREMIUM",
      "trialActive": true,
      "sources": ["PROMOTIONAL_TRIAL"],
      "limits": {"REPLAY_MAX": {"unlimited": true, "maximum": null}}
    }
  }
}
```

The shape is abbreviated; it is not a reusable full fixture. Current trialGranted is not asserted by the illustration. Actual non-identifying entitlement envelope is retained under ignored Generated/S601/sanitized-entitlements.json.

The codec defect is corrected and regression-validated. The requested strict success gate additionally requires real-current-user FREE state; that claim cannot be made because this user is legitimately in trial. FREE maximum=3 and denied-access semantics remain covered by isolated codec/state and existing backend entitlement tests. SERVER-6 remains paused for review; no attempt was made to downgrade the user or bypass entitlements.

```text
DEFECT_REPRODUCED=YES
ROOT_CAUSE_IDENTIFIED=YES_MANUAL_CONSTRUCTOR_OMITTED_EXISTING_FIELD
SERVER_ENTITLEMENTS_PRESENT=YES
WIRE_ENTITLEMENTS_PRESENT=YES
UNITY_TRANSPORT_ENTITLEMENTS_PRESENT=YES
UNITY_CODEC_ENTITLEMENTS_PRESENT_BEFORE=NO
UNITY_CODEC_ENTITLEMENTS_PRESENT_AFTER=YES
UNITY_STATE_ENTITLEMENTS_PRESENT=YES
SERVER_CHANGE_REQUIRED=NO
WIRE_CONTRACT_CHANGED=NO
FREE_CODEC_TEST=PASS
PREMIUM_CODEC_TEST=PASS
TRIAL_CODEC_TEST=PASS
MISSING_ENTITLEMENTS_FAILS_SAFE=PASS
SERVER_TO_UNITY_ENTITLEMENT_MAPPING=PASS
AUTH_REGRESSION=PASS_469_CHECKS
ENTITLEMENT_REGRESSION=PASS_17_TESTS
HISTORY_REPLAY_REGRESSION=PASS_1595_CHECKS_62_RETAINED_MATCHES
UNITY_EDITMODE=PASS_9_CASES
UNITY_PLAYMODE=PASS_9_CODEC_STATE_CASES_2_REPLAYS
CONSOLE_ERRORS=0
REMOTE_BOOTSTRAP_ENTITLEMENTS=PASS
UNITY_FREE_REPLAY_ENTITLEMENT_STATE=NOT_APPLICABLE_CURRENT_USER_PREMIUM_TRIAL
FREE_REPLAY_FIXTURE_STATE=PASS_MAXIMUM_3
REAL_TEST_FIRESTORE_CALLS=NOT_MEASURED_BOUNDED_NORMAL_BOOTSTRAP_HISTORY
REAL_PRODUCTION_FIRESTORE_CALLS=0
SERVER_CONFIGURATION_CHANGED=NO
SERVER_API_RESTARTED=NO
SERVER_REDIS_RESTARTED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
MATCHES_STARTED=0
MATCHES_COMPLETED=0
BOT_SWARM_MATCHMAKING_STARTED=NO
PREEXISTING_USER_FILES_PRESERVED=YES_97_OF_97
UNEXPECTED_FILES=0
S6_01_CORRECTION_VALIDATED=YES
S6_01_SUCCESS=NO_STRICT_REMOTE_FREE_GATE_NOT_MET_CURRENT_USER_IS_TRIAL
SERVER_6_RESUME_ALLOWED=NO_PENDING_REVIEW
COMMIT=NONE
PUSH=NONE
NEXT=S6-01 REVIEW
```

## S6-04 — Windows launcher correction (2026-09-26)

Original execution preserved under `Generated/SERVER6-five/`: the child shell reported `El nombre de archivo, el nombre de directorio o la sintaxis de la etiqueta del volumen no son correctos.` Unity recorded EXTERNAL_STOP, zero started/completed matches and zero console errors. No managed exception type was captured for that child failure; the coordinator's catch hid its own failure detail.

Root cause: RunServer6FiveMatches.ps1 used Start-Process with cmd.exe /d /c and a manually quoted command containing both the quoted gradlew.bat path and quoted --args payload. Start-Process joins ArgumentList into a command line; cmd quote processing consumed the wrong boundaries. A safe --version reproduction with a quoted multi-word --args payload failed at cmd parsing, before a Swarm main function. A reproduction without that second quoted payload reached Gradle (its sandbox cache access then failed). Neither reproduction ran matchmaking.

Correction: SwarmProcessLauncher.ps1 invokes the existing repository Gradle wrapper JAR with the JDK java.exe, matching gradlew.bat's -jar mechanism. ProcessStartInfo.ArgumentList carries each JVM/Gradle argument independently; no shell. Explicit working directory, UseShellExecute=false, CreateNoWindow=true. stdout/stderr use concurrent asynchronous CopyToAsync to separate files. On STOP the coordinator signals its existing stop file, allows graceful exit, then kills the owned process tree if necessary and drains/disposes streams. Existing failed-run markers remain, preventing accidental replay of the five-match attempt.

Safe process mode: the existing coordinateIdentities validation task accepts only --launch-check (or its existing no-argument coordination mode). Launch-check validates TEST configuration and three saved identity scopes through IdentitySettings, then returns before refresh/authentication, HMAC coordination or any matchmaking. No credential is supplied in argv; identity files are read only by external Swarm tooling. Normal identity-refresh behavior and existing coordination mode are retained.

Actual successful launch metadata is in `Generated/S604/launch-metadata.json`:
- Executable: C:\Program Files\Java\jdk-21\bin\java.exe
- Working directory: D:\Fredy\development\2026\domino\server\domino
- Arguments (individual): -Xmx64m; -Xms64m; -Dorg.gradle.appname=gradlew; -jar; D:\Fredy\development\2026\domino\server\domino\gradle\wrapper\gradle-wrapper.jar; :bot-swarm:coordinateIdentities; --args=--launch-check; --console=plain; --no-daemon.
- Environment classification: project teamfho-domino, environment TEST, target https://domino-api-test.teamfho.com, identity directory outside repository, real-test opt-in. Launch-check used a noncredential placeholder API key and performed no authentication. WSS continues to derive from the TEST REST target using the existing Swarm transport.

Validation: 21 synthetic launcher checks PASS, including spaces, Windows backslashes, ampersands/parentheses, embedded quotes, trailing backslash, exact working directory and argument boundaries; missing executable/working/identity paths; concurrent large stdout/stderr output; owned process termination. Final tests rerun after cleanup changes, PASS. Dry-run constructed the actual ProcessStartInfo without starting/authenticating. Exactly one actual Swarm validation process then ran and exited 0: TEST, three identities, target valid, authentication NO, matchmaking NO. Final process inventory: zero MainKt/IdentityCoordinatorKt processes. Hash comparison of LOCAL and TEST slot JSON files: no changes. No ACL modifications.

File classification:
- S6_04_LAUNCHER_FIX: client/Validation/RunServer6FiveMatches.ps1; client/Validation/SwarmProcessLauncher.ps1; server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/Identity.kt; server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/IdentityCoordinator.kt.
- S6_04_TEST: client/Validation/RunSwarmLauncherTests.ps1.
- SERVER6_REPORT: this file (chronology appended, prior failures retained).
- GENERATED_VALIDATION: Generated/S604 safe reproduction outputs, synthetic Java probe files, launch metadata/stdout/stderr. No credentials in generated files.
- PREEXISTING_USER_FILE: all 96 other baseline files unchanged; ApiSettings.asset retains the separately authorized earlier LOCAL-to-TEST change. S6-04 changed none of the 97 baseline files.
- UNEXPECTED: none identified.

S6_04_SUCCESS=YES
SERVER_6_RESUME_ALLOWED=YES_AFTER_REVIEW
SWARM_LAUNCH_DRY_RUN=PASS
SWARM_PROCESS_START=PASS
SWARM_PROCESS_CONFIGURATION=TEST
SWARM_PROCESS_IDENTITY_DISCOVERY=3
SWARM_PROCESS_REMOTE_TARGET_VALID=YES
SWARM_PROCESS_EXIT=PASS
STOP_PROPAGATION_PRESERVED=YES
ORPHAN_SWARM_PROCESSES=0
IDENTITY_FILES_MODIFIED=0
ACL_CHANGED=NO
MATCHES_STARTED=0
MATCHES_COMPLETED=0
BOT_SWARM_MATCHMAKING_STARTED=NO
S6_01_CORRECTION_VALIDATED=YES (prior evidence retained)
S6_03B_IDENTITY_COORDINATION=PASS (prior evidence retained)
S6_03C_AUTHORIZED_PLAYER_CORRELATION=PASS (prior evidence retained)
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
NEXT=S6-04 REVIEW

## S6-05 — Concurrent coordinator log reading (2026-09-26)

Chronology: after S6-04, the authorized Match 1 attempt stopped at MATCH_1 / EXTERNAL_STOP with zero matches started or completed. Swarm output contained Gradle startup only, no assignment. The generic coordinator catch hid the original exception. A synthetic open-writer reproduction confirms that File.ReadAllText produces a Windows sharing IOException; the original exact exception was not captured. Original Generated/SERVER6-match1 files remain byte-for-byte unchanged, verified against Generated/S605/original-evidence-hashes.json.

Root cause location: RunServer6FiveMatches.ps1 polled stdout every 500 ms using File.ReadAllText and repeated that read after process exit. SwarmProcessLauncher.ps1 / Start-SwarmChild writes stdout and stderr via asynchronous CopyToAsync into FileStreams opened with FileAccess.Write and FileShare.ReadWrite. The writer remains unchanged.

Correction: SwarmLogReader.ps1 / Read-SwarmCompleteLog opens FileMode.Open, FileAccess.Read, FileShare.ReadWrite (no Delete). Each poll snapshots the observed byte length, reads up to temporary EOF, and passes only bytes through the last LF to a strict UTF-8 StreamReader. Thus incomplete control lines and split UTF-8 suffixes wait for the next poll. Missing files/directories and empty logs return pending empty text; genuine I/O errors propagate. Get-SwarmControlLog validates complete known control lines and excludes unrelated build output. Malformed known control records fail safely. The coordinator retains its existing ready/assignment/finished gates and stops on complete failure/stop records.

Write-SwarmSafeStop records only an allowlisted category and base exception type, never exception messages or contents. LOG_NOT_FOUND, LOG_SHARING, LOG_IO, LOG_PARSE, CHILD_EXIT, CHILD_TIMEOUT, CONTROL_PROTOCOL and OTHER are supported. It writes the existing stop marker before diagnostics. Unity's unchanged Guard checks that marker and reports EXTERNAL_STOP; this contract was inspected, and stop creation was exercised synthetically. No new live Unity matchmaking/STOP exercise was performed. Cleanup failures are also sanitized; direct Java ArgumentList launch, concurrent stdout/stderr draining and process-tree cleanup remain intact.

Validation:
- 19 focused log checks PASS: missing, empty, original sharing reproduction, open writer, partial record, append, split UTF-8 before/after completion, temporary EOF, normal close, malformed control record, redacted diagnostics, STOP marker, genuine exclusive-lock sharing failure, directory I/O failure, and four safe non-log categories.
- Existing Windows launcher: 21 checks PASS, including owned-process termination.
- IdentityCoordinationTests: 8 tests, zero failures/errors/skips.
- Exactly one real safe Swarm launch after those tests: coordinateIdentities --launch-check. 106 concurrent polls PASS; TEST identity discovery 3; remote TEST target valid; authentication NO; matchmaking NO; exit 0; orphan Swarm process count 0. This validates configured target/scope, not a new remote API connectivity test.
- LOCAL and TEST identity file hashes unchanged. No credentials printed. Real run evidence: Generated/S605/safe-process-result.txt and real-safe-process stdout/stderr.
- Original failed evidence hash set matches exactly. Of 97 preexisting user baseline files, 96 match; ApiSettings.asset retains the separately authorized earlier TEST selection. S6-05 changed none of these files.

Exact S6-05 file classification (5 unique files):
- Validation-tool correction: client/Validation/RunServer6FiveMatches.ps1; client/Validation/SwarmLogReader.ps1.
- Focused validation tests: client/Validation/RunSwarmLogReaderTests.ps1; client/Validation/RunSwarmSafeLogValidation.ps1.
- Report: client/Validation/SERVER_6_FIVE_MATCHES_HISTORY_REPLAY_REPORT.md.
Generated/S605 contains ignored test/evidence artifacts. No production gameplay, backend, Unity adapter, infrastructure, credentials, ACL, or Firestore rule changes.

```text
SERVER-6 S6-05 CONCURRENT LOG READER
====================================
ROOT_CAUSE=CONCURRENT_LOG_FILE_SHARING
COORDINATOR_LOG_READER=RunServer6FiveMatches.ps1 -> SwarmLogReader.ps1
LOG_WRITER=SwarmProcessLauncher.ps1 / Start-SwarmChild / CopyToAsync
ORIGINAL_READ_METHOD=File.ReadAllText
CORRECTED_READ_METHOD=FileStream snapshot / complete-LF-prefix / UTF8 StreamReader
FILE_SHARE_MODE=ReadWrite (no Delete)
SHARING_VIOLATION_REPRODUCED=YES
CONCURRENT_LOG_READER_TESTS=PASS
PARTIAL_CONTROL_LINE_IGNORED_UNTIL_COMPLETE=YES
LOG_NOT_YET_CREATED_HANDLED=YES
EMPTY_LOG_HANDLED=YES
APPENDED_LOG_CONTENT_VISIBLE=YES
SAFE_ERROR_CLASSIFICATION=PASS
SECRETS_IN_DIAGNOSTICS=0
S6_04_LAUNCHER_REGRESSION=PASS
IDENTITY_COORDINATION_REGRESSION=PASS
STOP_PROPAGATION_REGRESSION=PASS (synthetic marker + unchanged Unity contract)
SWARM_PROCESS_START=PASS
COORDINATOR_CONCURRENT_LOG_READ=PASS
SWARM_PROCESS_IDENTITY_DISCOVERY=3
SWARM_REMOTE_TARGET=TEST
SWARM_PROCESS_EXIT=PASS
ORPHAN_SWARM_PROCESSES=0
ORIGINAL_MATCH1_EVIDENCE_PRESERVED=YES
MATCHES_STARTED=0
MATCHES_COMPLETED=0
MATCH_FOUND=NO
HISTORY_COUNT_BEFORE=0 (approved baseline, not queried again)
PREEXISTING_USER_FILES_PRESERVED=YES_EXCEPT_PREVIOUSLY_AUTHORIZED_TEST_ASSET
UNEXPECTED_FILES=NONE_IDENTIFIED
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
FIRESTORE_RULES_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S6_05_SUCCESS=YES
SERVER_6_RESUME_ALLOWED=YES_AFTER_REVIEW
NEXT=S6-05 REVIEW
```

## SERVER-6 — Match 1 retry (2026-09-26, STOP for review)

Authorized exactly one retry using the approved S6-04 launcher and S6-05 reader. No code was modified. The original failed directory was moved, after checking its absolute workspace path and unused destination, to `Generated/SERVER6-match1-original-failed`; its complete file hash set still matches `Generated/S605/original-evidence-hashes.json`. The new evidence remains in `Generated/SERVER6-match1`, with a retry hash manifest. No files from the original run were overwritten.

Unity was outside Play. Entered Play, observed FHO synchronized/connected, invoked the existing Execute MATCH 1 only adapter and pressed the normal Buscar oponente button. Three authenticated Swarm clients and the current Unity player were correlated to one match. The existing adapter drove normal gameplay. Swarm reported 227 commands, one match started/finished, maximum simultaneous matches 1, three History confirmations, no extra queue joins beyond the initial three, and empty stderr. Existing resync counter remained at 3 throughout observed gameplay; it is not evidence of three unexpected disconnects. Gradle exited successfully after 13m48s. Process inventory after STOP found zero Swarm processes.

Unity recorded MATCH_1_COMPLETED=YES and MATCH_1_IN_HISTORY=YES, then stopped at REPLAY_PARTICIPANTS_MISMATCH with InvalidOperationException and zero captured console errors. The coordinator propagated STOP and reported CONTROL_PROTOCOL / RuntimeException. It did not retry or start Match 2.

The failed assertion combines manifest selfSeat equality with participants' seat/teamId/displayNameSnapshot/controlType equality against the authoritative public snapshot. The evidence does not isolate which subcondition failed. No correction or speculative diagnosis was applied. Earlier sequential assertions had passed: authoritative FINISHED, History membership, replayAvailable=true, TARGET_REACHED, History/manifest score and finishReason equality, finishedAt parse, and History WIN/LOSS consistency. Full Replay reconstruction and terminal-state comparison were never reached, so they are NOT_RUN. The final History exact-count query and History/Replay UI steps also were not reached. Do not claim exact History count 1 from membership alone.

Read-only Unity observation after STOP showed final score Nosotros 217 / Ellos 72, round 7. This score is visual evidence; the adapter failed before persisting its result block. No additional UI action or gameplay was performed after the failure.

A read-only SSH resource sample returned host available RAM 9868 MiB and load 0.98 / 0.38 / 0.26 at 07:30:37 UTC. Docker access via sudo -n required interactive authentication. No password was requested/captured by the tool. A manual request is pending; container samples, server log inspection and Redis cleanup verification remain NOT_VERIFIED. No Redis flush, direct Firestore query or manual game/history/replay write occurred. History and manifest reads above used the authenticated current-user API.

```text
SERVER-6 MATCH 1 RETRY
======================
SWARM_LAUNCHER=PASS
CONCURRENT_LOG_READER=PASS
SWARM_MAX_MATCHES=1
MATCH_CONCURRENCY=1
MATCHMAKING_STARTED=YES
MATCH_FOUND=YES
CURRENT_UNITY_USER_IN_MATCH_1=YES
DISTINCT_MATCH_PLAYERS=4
MATCHES_STARTED=1
MATCHES_COMPLETED=1
EXTRA_MATCHES_STARTED=0
MATCH_1_COMPLETED=YES
MATCH_1_RESULT=WIN (Unity visual result)
MATCH_1_SCORE=217-72 (Unity visual result)
MATCH_1_COMPLETION_REASON=TARGET_REACHED
HISTORY_COUNT_BEFORE=0
HISTORY_COUNT_AFTER_MATCH_1=AT_LEAST_1_EXACT_COUNT_NOT_VERIFIED
MATCH_1_IN_HISTORY=YES
MATCH_1_HISTORY_CONSISTENCY=PARTIAL_PASS_SCORE_REASON_TIMESTAMP_WINNER
MATCH_1_REPLAY_AVAILABLE=YES
MATCH_1_REPLAY_RECONSTRUCTION=NOT_RUN
MATCH_1_REPLAY_CONSISTENCY=FAIL_PARTICIPANTS_MISMATCH
MATCH_1_EVENT_SEQUENCE_REGRESSION=NO_OBSERVED_DURING_GAMEPLAY
MATCH_1_REPLAY_SEQUENCE_REGRESSION=NOT_VERIFIED
UNITY_HISTORY_ENTRIES_VISIBLE_AFTER_MATCH_1=NOT_RUN
UNITY_MATCH_1_REPLAY_OPEN=NOT_RUN
MATCH_1_REDIS_CLEANUP=NOT_VERIFIED_SUDO_REQUIRED
MATCH_1_SERVER_UNEXPECTED_EXCEPTIONS=NOT_VERIFIED_SUDO_REQUIRED
SWARM_UNEXPECTED_EXCEPTIONS=0_OBSERVED
CONSOLE_ERRORS=0
API_CPU_SAMPLE=NOT_AVAILABLE_SUDO_REQUIRED
API_MEMORY_SAMPLE=NOT_AVAILABLE_SUDO_REQUIRED
REDIS_CPU_SAMPLE=NOT_AVAILABLE_SUDO_REQUIRED
REDIS_MEMORY_SAMPLE=NOT_AVAILABLE_SUDO_REQUIRED
HOST_RAM_AVAILABLE=9868_MiB
HOST_LOAD=0.98_0.38_0.26
RESOURCE_SAMPLE_ONLY=YES
MANUAL_FIRESTORE_MATCH_WRITES=0
MANUAL_FIRESTORE_HISTORY_WRITES=0
MANUAL_FIRESTORE_REPLAY_WRITES=0
REAL_PRODUCTION_FIRESTORE_CALLS=0
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
MATCH_1_GATE=FAIL
SERVER_6_CONTINUE_TO_MATCH_2=NO
NEXT=MATCH 1 REVIEW
```

## S6-06 — Read-only Replay participant diagnosis

Root cause confirmed with the real Match 1 metadata. Primary owner: SERVER6_VALIDATOR. OnlineMatchService.snapshot (OnlineMatchService.kt:121) explicitly constructs PublicParticipant with teamId=null, whereas ReplayService.manifest (ReplayService.kt:96) and ReplayHistoryController.history (ReplayHistoryController.kt:35) copy persisted participant.teamId. Match 1 snapshot contains four null team IDs; History and Replay contain [0,1,0,1]. The actual frozen ruleSnapshot.effectiveModeJson.seatTeams is [[0,2],[1,3]], identical to History/Replay teams. No participant membership or ordering defect was found.

Actual safe seat mapping: 0 UNITY_USER; 1 SLOT_01; 2 SLOT_03; 3 SLOT_02. Each external slot was authenticated with its existing TEST identity and normal backend GET requests; its History selfSeat equals private snapshot seat. Current Unity seat 0 is inferred by elimination from those three distinct authenticated seats plus the approved Match 1 four-player correlation (not display-name matching). Raw UIDs/tokens were never output or persisted in diagnostic artifacts. No Unity credential was exported. External identity files were hash-checked unchanged. Current Unity Replay authorization PASS is retained from the prior successful manifest read; it was not freshly repeated with Unity credentials. SLOT_01's new manifest request was also authorized and replayAvailable=true.

Read operations: authenticated snapshot and History for the three TEST participants, manifest for SLOT_01, and one repeat to retain the frozen team definition. Python HTTP initially returned non-JSON 403; the standard Java HttpClient transport used by Swarm succeeded without any security/configuration change. Diagnostic scripts under ignored Generated/S606 only perform normal GETs after Firebase refresh and never persist refreshed credentials. Client reconstruction and replay-event page requests were NOT_RUN. The server's normal manifest implementation internally validates/reconstructs its archive as part of availability; no custom reconstruction was invoked and no archive was regenerated or written.

Evidence: Generated/S606/safe-metadata.json, safe-rule-confirmation.json, diagnosis.json. All four observed per-seat comparisons: seat=true, displayNameSnapshot=true, controlType=true, teamId=false. SLOT_01 selfSeat also matched. Original assertion necessarily fails on teamId for every participant; the same semantic team comparison against frozen rules passes. History count recorded for external slots is not the current Unity user's History count (two slots have older History). Only Match 1 metadata was compared.

Server trace: MatchParticipant carries Firebase playerUid, seatIndex, displayNameSnapshot and teamId. OnlineEngine.Builder.result produces the finished Match and PlayerMatchHistory (OnlineEngine.kt:199-201). FirestoreOnlineRepository.transact writes MatchCodec.map(write.state.match) to matches/{id}, runtime authoritative state, and histories atomically (OnlineRepository.kt:125-136). MatchCodec delegates to GameCatalogCodec. FirestoreMatchRepository.read decodes the root Match and verifies ruleSnapshot (FirestoreMatchRepository.kt:19). ReplaySource reads that Match; ReplayService projects participants via list.map, replacing seatIndex with public seat and omitting playerUid. History uses the same projection. There is no separately rewritten Replay participant collection. Arrays remain lists through codec/Firestore; the participant set is not derived from map iteration. Internal participant-list seat indices are constrained by joins and immutable-participant validation. Public identity is the explicit seat within this match, not array position or display name; the existing client reducer selects participants by seat. Preserve semantic seat ordering, but compare by explicit seat keys rather than relying on arbitrary enumeration or display name.

Client trace: ReplayClient.Manifest -> ApplicationServices.OnlineApi / OnlineMatchApi.SendAsync -> JObject.Parse(result.Body) (OnlineMatchClient.cs:66), not the bootstrap UnityApiJsonCodec. No participant reordering or team conversion occurs. Server6FiveMatches.Run (Server6FiveMatches.cs:86) checks manifest.selfSeat against current snapshot Seat, then All replay participants have Any authority participant whose seat, nullable teamId, displayNameSnapshot and controlType equal. It is not ordered-list equality and not UID equality. It lacks independent exact-count/unique-seat/set checks and incorrectly equates snapshot null teamId with persisted team identity. Display-name equality must be treated only as snapshot metadata consistency, never as stable identity proof; uniqueness is not established by the contract.

Test audit (read only; no suites or synthetic match executed): ReplayTests covers both two/four-player fixtures, deterministic streams, reconstruction, perspectives/private seats, authorization and corruption. ReplayEmulatorTests exercises real Firestore DTO persistence/decoding and read-only paged reconstruction for both modes. ReplayHttpTests checks no playerUid exposure. Client Validation/ReplayTests builds public manifests from persisted fixtures including teamId and reconstructs. Missing focused coverage: live OnlineMatchService snapshot null-team representation versus persisted History/Replay team mapping, this SERVER6 compound assertion, duplicate/missing seat rejection, shuffled collection behavior with explicit seats, and authenticated four-way identity correlation across these endpoints. Existing coverage does not validate the failed comparison.

Proposed minimal correction, NOT applied: in SERVER6 validator independently validate selfSeat, count=4, unique seat set {0,1,2,3}, and execution-bound participant correlation. Derive authoritative teams from the frozen ruleSnapshot.effectiveModeJson.seatTeams; verify Replay/History teams and participant.teamId against that mapping. Treat nullable online snapshot teamId according to its actual projection (if present, verify; do not invent equality to persisted non-null values). Keep display-name/control metadata checks separate from identity. Add focused tests covering null snapshot teams, wrong persisted team, missing/duplicate seats, swapped authenticated seat mapping, same display names and equivalent collections in different iteration orders. No backend persistence change or repair of Match 1 is proposed. The online snapshot teamId omission is a separate API projection inconsistency; this diagnosis establishes why the validator fails, not a mandate to change that contract.

```text
SERVER-6 S6-06 REPLAY PARTICIPANTS DIAGNOSIS
============================================
MATCH_1_ID_RESOLVED=YES
AUTH_PARTICIPANT_COUNT=4
AUTH_SEAT_0=UNITY_USER
AUTH_SEAT_1=SLOT_01
AUTH_SEAT_2=SLOT_03
AUTH_SEAT_3=SLOT_02
AUTH_SEAT_ORDER=0,1,2,3
AUTH_TEAM_MAPPING=RULE_TEAMS_0:[0,2];1:[1,3] (snapshot participant.teamId=null)
HISTORY_PARTICIPANT_COUNT=4
HISTORY_SEAT_ORDER=0,1,2,3
HISTORY_TEAM_MAPPING=0:[0,2];1:[1,3]
HISTORY_PARTICIPANTS_MATCH_AUTHORITY=YES_SEMANTICALLY_NOT_RAW_TEAMID
REPLAY_PARTICIPANT_COUNT=4
REPLAY_SEAT_ORDER=0,1,2,3
REPLAY_TEAM_MAPPING=0:[0,2];1:[1,3]
REPLAY_PARTICIPANT_SET_EQUALS_AUTHORITY=YES_BY_AUTHENTICATED_SEAT_MAPPING
REPLAY_SEAT_0_MATCH=YES_IDENTITY_NO_RAW_TEAMID
REPLAY_SEAT_1_MATCH=YES_IDENTITY_NO_RAW_TEAMID
REPLAY_SEAT_2_MATCH=YES_IDENTITY_NO_RAW_TEAMID
REPLAY_SEAT_3_MATCH=YES_IDENTITY_NO_RAW_TEAMID
REPLAY_ORDER_EQUALS_AUTHORITY=YES
REPLAY_TEAMS_EQUAL_AUTHORITY=YES_FROZEN_RULES
MATCH_IDENTITY_TYPE=PERSISTED_FIREBASE_UID_AND_SEAT
HISTORY_IDENTITY_TYPE=AUTHENTICATED_OWNER_UID_PUBLIC_SEAT
REPLAY_IDENTITY_TYPE=AUTHENTICATED_MEMBERSHIP_UID_PUBLIC_SEAT
VALIDATOR_IDENTITY_TYPE=SEAT_PLUS_TEAMID_DISPLAY_NAME_CONTROL_TYPE
REPLAY_PARTICIPANT_ORDER_CONTRACT=SEMANTIC_SEAT_ORDER_EXPLICIT_SEAT_KEY
REPLAY_PARTICIPANTS_SOURCE=PERSISTED_MATCH_PARTICIPANTS
REPLAY_PARTICIPANTS_TRANSFORMATIONS=LIST_MAP_SEATINDEX_TO_SEAT_UID_OMITTED_TEAMID_RETAINED
PARTICIPANT_VALIDATOR_FILE=client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FiveMatches.cs
PARTICIPANT_VALIDATOR_METHOD=Run
PARTICIPANT_COMPARISON_SEMANTICS=SELFSEAT_AND_ALL_ANY_SEAT_TEAMID_NAME_CONTROL_EQUALITY
PARTICIPANT_TEST_COVERAGE=PARTIAL_MISSING_LIVE_SNAPSHOT_REPLAY_TEAM_PROJECTION_COMPARISON
MATCH_1_REPLAY_AUTHORIZATION=PASS_PRIOR_CURRENT_UNITY_EVIDENCE_AND_NEW_SLOT01_METADATA
S6_06_DEFECT_OWNER=SERVER6_VALIDATOR
ROOT_CAUSE=NULL_ONLINE_TEAMID_COMPARED_TO_PERSISTED_REPLAY_TEAMID
SOURCE_CHANGE_REQUIRED=YES_VALIDATOR_ONLY_PROPOSED
PROPOSED_MINIMAL_FIX=COMPARE_EXPLICIT_SEAT_IDENTITIES_AND_FROZEN_RULE_TEAM_MAPPING
MATCH_1_FIRESTORE_MUTATED=NO
MATCH_1_REPLAY_MUTATED=NO
MATCH_1_HISTORY_MUTATED=NO
MATCHES_STARTED=1
MATCHES_COMPLETED=1
EXTRA_MATCHES_STARTED=0
MATCH_2_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
SERVER_6_RESUME_ALLOWED=NO
NEXT=S6-06 REVIEW
```

## S6-06R — validation-only correction and existing Match 1 revalidation

The original `REPLAY_PARTICIPANTS_MISMATCH` above remains preserved. The validator incorrectly compared the online snapshot's null teamId to the persisted teamId. The correction validates exactly four unique explicit seats, binds the current Unity user and three externally authenticated slots to their approved seats, and checks persisted team assignments against frozen Match rules. Display names are not identity. The approved Match 1 topology is also checked independently, rejecting an internally consistent but incorrect frozen mapping.

Changed files for S6-06R (8 unique; all validation/tests/report):

- `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FiveMatches.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6ParticipantValidator.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6ParticipantValidator.cs.meta`
- `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match1Revalidation.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match1Revalidation.cs.meta`
- `client/Validation/Server6ParticipantTests.cs`
- `client/Validation/RunServer6ParticipantTests.ps1`
- `client/Validation/SERVER_6_FIVE_MATCHES_HISTORY_REPLAY_REPORT.md`

Focused validator tests: 18 PASS, including the null-team false negative, wrong identity, same-team identity swap, wrong persisted team, duplicate identity/seat, missing/extra/unexpected seats, reordered collections, changed display names, wrong frozen mapping, wrong History team, wrong self/match and Spanish-locale evidence dates. Identity coordination: 8 tests PASS with forced Gradle execution. Launcher: 21 checks PASS. Concurrent log reader: 19 checks PASS.

An initial revalidation stopped at SCOPE because a parsed JSON date was formatted under the Spanish locale. No Match API read was reached. A repeat still used the old Unity assembly; both failures are preserved in Generated/S606R. The date handling was corrected and covered by a self-contained es-ES regression. After explicit Unity refresh/recompile, the existing Match 1 passed all checks.

Live evidence: `Generated/S606R/revalidation.txt`, `validated-metadata.json`, `identity-seats.json`, `history.png`, `replay.png`. Generated metadata excludes the Replay access session; no Firebase token or raw UID was exported. ReplayClient reconstructed 946 events, enforcing sequence and terminal consistency, then the normal HistoryReplayView opened the existing entry and Replay under the current Premium Trial user. Screenshots visually checked: one History entry, victory 217–72; Replay opens at event 0 of 946 with four participants and playback controls.

No matchmaking or gameplay commands were issued. Original failed-attempt and retry evidence hashes remain unchanged. Of the 97 protected baseline files, 96 match; the sole difference is the previously authorized ApiSettings TEST selection, unchanged by S6-06R. Redis/server-log checks remain unverified because sudo requires interactive authentication; no password was requested or collected. No services were restarted.

```text
SERVER-6 S6-06R PARTICIPANT VALIDATOR FIX
=========================================
ROOT_CAUSE=NULL_SNAPSHOT_TEAMID_VS_PERSISTED_REPLAY_TEAMID
DEFECT_OWNER=SERVER6_VALIDATOR
FILES_CHANGED_BY_S6_06R=8_VALIDATION_TEST_REPORT_FILES_LISTED_ABOVE
PARTICIPANT_IDENTITY_VALIDATION=EXPLICIT_SEAT_IDENTITY
TEAM_VALIDATION_SOURCE=FROZEN_MATCH_RULES
RAW_SNAPSHOT_TEAMID_REQUIRED=NO
DISPLAY_NAME_USED_AS_IDENTITY=NO
PARTICIPANT_VALIDATOR_REGRESSION_TESTS=PASS_18
WRONG_IDENTITY_REJECTED=YES
WRONG_SEAT_REJECTED=YES
WRONG_TEAM_REJECTED=YES
DUPLICATE_REJECTED=YES
MISSING_REJECTED=YES
EXTRA_REJECTED=YES
MATCH_1_REUSED_FOR_REVALIDATION=YES
MATCH_1_PARTICIPANT_VALIDATION=PASS
MATCH_1_HISTORY_CONSISTENCY=PASS
MATCH_1_REPLAY_AVAILABLE=YES
MATCH_1_REPLAY_RECONSTRUCTION=PASS
MATCH_1_REPLAY_CONSISTENCY=PASS
MATCH_1_RESULT=WIN
MATCH_1_SCORE=217-72
MATCH_1_COMPLETION_REASON=TARGET_REACHED
MATCH_1_EVENT_SEQUENCE_REGRESSION=NO
MATCH_1_REPLAY_SEQUENCE_REGRESSION=NO
UNITY_HISTORY_ENTRIES_VISIBLE_AFTER_MATCH_1=1
UNITY_MATCH_1_REPLAY_OPEN=PASS
MATCH_1_REDIS_CLEANUP=NOT_VERIFIED_SUDO_REQUIRED
MATCH_1_SERVER_UNEXPECTED_EXCEPTIONS=NOT_VERIFIED_SUDO_REQUIRED
MATCH_PRODUCTION_SOURCE_CHANGED=NO
HISTORY_PRODUCTION_SOURCE_CHANGED=NO
REPLAY_PRODUCTION_SOURCE_CHANGED=NO
MATCH_1_FIRESTORE_MUTATED=NO
MATCH_1_HISTORY_MUTATED=NO
MATCH_1_REPLAY_MUTATED=NO
MATCHES_STARTED=1
MATCHES_COMPLETED=1
EXTRA_MATCHES_STARTED=0
MATCH_2_STARTED=NO
PREEXISTING_USER_FILES_PRESERVED=YES_EXCEPT_PREVIOUSLY_AUTHORIZED_TEST_ASSET
UNEXPECTED_FILES=NONE_IDENTIFIED
COMMIT=NONE
PUSH=NONE
S6_06R_SUCCESS=YES
SERVER_6_CONTINUE_TO_MATCH_2=NO_PENDING_REVIEW
NEXT=S6-06R REVIEW
```

## SERVER-6 — Match 2 gate

Executed exactly one new TEST match with current Unity/FHO and the same three authenticated TEST slots. The runtime baseline contained Match 1 only. A dedicated Match 2 validation adapter and coordinator use the previously validated launcher/log reader, targetMatches=1, requeue=false and concurrency=1. No production source or configuration changed. Focused semantic participant tests remained PASS (18 checks).

New validation files: `Server6Match2.cs` and its `.meta` under the client Editor directory; `client/Validation/RunServer6Match2.ps1`, `ReadServer6Match2Seats.py`, and `Server6MetadataGet.java`. The read-only external helper binds each TEST account to its own backend snapshot seat before gameplay automation and verifies the same seats against History after completion. Tokens stay in memory/stdin and are not persisted in evidence. The S6-06R semantic validator is reused unchanged.

Match 2 assigned Unity to seat 3, slot-01 to 2, slot-02 to 0, slot-03 to 1. These assignments remained unchanged. Frozen teams: team 0 seats 0/2; team 1 seats 1/3. Persisted score order is team0/team1: [194,203]. Current Unity user won with team 1, therefore the user-facing result is a 203–194 victory. Completion reason TARGET_REACHED.

Replay reconstruction consumed 957 ordered events and matched the authoritative terminal scores, board, round, remaining tile counts, terminal result, participants and final sequence. History has exactly two entries, Match 2 then Match 1, consistent with finishedAt descending (document ID descending tie-break in the repository). Match 1 manifest remains available; its Replay was not reconstructed again. Screenshots visually confirmed both History entries and the Match 2 Replay at event 0 of 957.

Evidence: `Generated/SERVER6-match2/execution.txt`, `coordinator.log`, `identity-seats-initial.json`, `identity-seats-final.json`, `terminal-2.json`, `swarm-2.log`, `history.png`, `replay.png`. Swarm exited successfully: 235 commands, one new match started/finished, three History confirmations, maximum one simultaneous match, stderr empty and no unexpected exceptions recorded. Unity console errors: 0. Match 1 retry evidence hashes unchanged. Protected baseline remains 96/97 identical with only the previously authorized TEST ApiSettings exception.

Optional Redis/server-log checks remain NOT_VERIFIED_SUDO_REQUIRED as authorized; no sudo password requested. No API restart, manual Firestore writes, production calls, DNS/Cloudflare/server configuration changes, commit or push. Match 3 was not started.

```text
SERVER-6 MATCH 2 GATE
=====================
MATCHES_STARTED=2
MATCHES_COMPLETED=2
EXTRA_MATCHES_STARTED=0
MATCH_2_MATCHMAKING_STARTED=YES
MATCH_2_FOUND=YES
CURRENT_UNITY_USER_IN_MATCH_2=YES
DISTINCT_MATCH_PLAYERS=4
MATCH_2_COMPLETED=YES
MATCH_2_RESULT=WIN
MATCH_2_SCORE=194-203_TEAM0_TEAM1
MATCH_2_COMPLETION_REASON=TARGET_REACHED
HISTORY_COUNT_AFTER_MATCH_2=2
MATCH_1_STILL_IN_HISTORY=YES
MATCH_2_IN_HISTORY=YES
MATCH_2_HISTORY_CONSISTENCY=PASS
HISTORY_ORDERING_AFTER_MATCH_2=FINISHED_AT_DESC_MATCH2_THEN_MATCH1
MATCH_2_REPLAY_AVAILABLE=YES
MATCH_2_REPLAY_EVENTS=957
MATCH_2_REPLAY_RECONSTRUCTION=PASS
MATCH_2_REPLAY_CONSISTENCY=PASS
MATCH_2_EVENT_SEQUENCE_REGRESSION=NO
MATCH_2_REPLAY_SEQUENCE_REGRESSION=NO
MATCH_1_REPLAY_STILL_AVAILABLE=YES
UNITY_HISTORY_ENTRIES_VISIBLE_AFTER_MATCH_2=2
UNITY_MATCH_2_REPLAY_OPEN=PASS
MATCH_2_REDIS_CLEANUP=NOT_VERIFIED_SUDO_REQUIRED
MATCH_2_SERVER_UNEXPECTED_EXCEPTIONS=NOT_VERIFIED_SUDO_REQUIRED
SWARM_UNEXPECTED_EXCEPTIONS=0
CONSOLE_ERRORS=0
MANUAL_FIRESTORE_MATCH_WRITES=0
MANUAL_FIRESTORE_HISTORY_WRITES=0
MANUAL_FIRESTORE_REPLAY_WRITES=0
REAL_PRODUCTION_FIRESTORE_CALLS=0
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
MATCH_2_GATE=PASS
SERVER_6_CONTINUE_TO_MATCH_3=NO_PENDING_REVIEW
NEXT=MATCH 2 REVIEW
```

## SERVER-6 — Matches 3, 4 and 5; final validation before restart

Exactly three additional matches ran sequentially. Each used the current Unity user and the same three TEST accounts, concurrency=1, targetMatches=1 and requeue=false. The next match was launched only after the preceding full gate and coordinator passed. Totals: five started, five completed, zero extra matches; no Match 6.

| Match | Current-user result | Current-user team score | Replay events | Full gate |
|---|---|---|---|---|
| 1 | WIN | 217–72 | 946 | PASS |
| 2 | WIN | 203–194 | 957 | PASS |
| 3 | LOSS | 120–214 | 1008 | PASS |
| 4 | WIN | 216–167 | 1166 | PASS |
| 5 | LOSS | 0–236 | 535 | PASS |

All finished through normal authoritative gameplay with TARGET_REACHED. Raw team0/team1 scores for Matches 3–5 are [214,120], [216,167], [0,236]. Explicit authenticated seat evidence was collected before and after each match; the S6-06R validator checked identity/seat membership and persisted teams against frozen rules. No nullable online teamId equality was reintroduced.

Match 3/4/5 Swarm totals respectively: 252/278/132 commands, one new match started and completed per run, three History confirmations per run, maximum one active match, successful process exit, empty stderr. No unexpected exceptions or Unity console errors. The corresponding Replay opened in Unity after each gate. Existing Match 1/2 UI evidence plus Match 3/4/5 UI evidence establishes five accessible Replays.

The final read-only check before restart reconstructed all five again, compared terminal state/result/score/sequence with authority, checked all History entries and finishedAt descending order 5,4,3,2,1. PASS. Premium Trial and unlimited Replay were reconfirmed via the current authenticated bootstrap. Evidence: `Generated/SERVER6-final/before-restart.txt`; each new match has its own `Generated/SERVER6-matchN` execution, authenticated seat proof, terminal snapshot, Swarm logs and UI screenshots.

New validation tooling only: `Server6RemainingMatches.cs` + `.meta`, `Server6FinalValidation.cs` + `.meta` in the client Editor directory, `client/Validation/RunServer6RemainingMatch.ps1`, `client/Validation/ReadServer6MatchSeats.py`. No production contracts changed. Focused participant tests: 18 PASS. The 97 protected-file baseline still has only the previously authorized ApiSettings TEST exception.

Observational host sample during Match 3: available RAM 9791 MiB; load 0.17/0.24/0.26. Container CPU/RAM, Redis cleanup and server logs remain NOT_VERIFIED_SUDO_REQUIRED. This is not capacity evidence.

API restart attempted only after all five gates and final pre-restart validation passed. `sudo -n` rejected it because interactive authentication is required; no restart occurred through automation. User was asked to restart only cuban-domino-api manually and supply its health result. No password requested or collected. Post-restart durability and final visual scrolling review remain pending at this chronological point.

## SERVER-6 — API restart and durable five-match verification

The user restarted only cuban-domino-api and supplied its subsequent healthy status. Evidence source is explicitly user-provided terminal output, stored in Generated/SERVER6-final/api-restart-healthy.txt. Redis was not restarted.

The existing Unity session passed the connected TEST session guard after restart. Final read-only validation reconstructed all five Replays (Match 5: 535; Match 4: 1166; Match 3: 1008; Match 2: 957; Match 1: 946 events), compared terminal state with authoritative snapshots, and validated all five History entries in finishedAt descending order 5,4,3,2,1. History count=5, durable Replay count=5, sequence regressions=0, console errors=0. Premium Trial and unlimited Replay remain confirmed. Generated/SERVER6-final/after-restart.txt ends FINAL_VALIDATION=PASS.

Visual review remains pending: the simulator shows three full entries and part of a fourth within the clipped list viewport. Automated drags from both card and button regions and a wheel gesture did not move the list. This does not yet distinguish an input-automation limitation from a scrolling defect. The user was asked to attempt a manual swipe to reach Matches 2 and 1. No UI correction, additional match, or validation rerun was performed.

## S6-07 — History vertical scrolling correction (validation in progress)

The user confirmed the original visual failure: lower cards were inaccessible through sufficient vertical scrolling. HISTORY_VISUAL_REVIEW=FAIL_SCROLL is preserved. Data, backend and post-restart Replay durability had already passed; no matches were added.

Before correction, History used a ScrollRect on the same object as its 900-unit viewport and mask, with manually positioned cards and count-derived content height. It had no VerticalLayoutGroup or ContentSizeFitter. Runtime diagnosis confirmed vertical=true, horizontal=false, content=1300, viewport=900, and a valid drag event route. A dispatched drag moved 230.4 units, so an absent ScrollRect or universally broken drag handler is NOT the established cause. Mouse sensitivity was the default 1: a three-step wheel event moved only 2.999847 units. Native simulator gestures require separate verification.

The candidate separates ScrollRect/Viewport/Content, uses VerticalLayoutGroup plus ContentSizeFitter for card-count-driven height, preserves responsive width and safe-area scaling, and adopts the existing mode selector sensitivity of 40. The same wheel event now moves 119.9998 units. Redrawing History stops inertia and resets to top. Detail mode disables the history layout and resets its position so entering an older match does not retain the list offset. No Replay data/API/reconstruction logic was changed.

Focused Play Mode checks passed for 0, 1, 5 and 12 cards using an in-memory read-only History fixture. They cover actual layout, empty-state localization, top position, refresh bounds, routed wheel/drag events, overflow, ordered cards, last-button hit testing and containment in the viewport. The initial test incorrectly expected normalizedPosition=1 for an empty non-scrollable list; the assertion was corrected to check anchoredPosition=0. That test correction is retained in diagnostic chronology. No fake entries were persisted or sent to the server.

Evidence: Generated/S607/before-fix.txt and diagnostic.txt. Native begin-drag delivery was observed in native-input.txt, but visible manual movement and the oldest Replay interaction remain pending. Do not mark S6_07_SUCCESS or SERVER_6_SUCCESS yet.

Change classification for this correction:
- S6_07_HISTORY_SCROLL_FIX: Assets/_Domino/Scripts/Replay/HistoryReplayView.cs.
- S6_07_TEST: Assets/_Domino/Scripts/Client/Editor/S607HistoryValidation.cs and its Unity .meta.
- SERVER6_REPORT: this report.
- PREEXISTING_USER_FILE: all other existing working-tree changes listed in Generated/S607/status-before.txt.
- No backend, API, Firestore, server configuration, DNS or Cloudflare changes. No commit or push.

## S6-07 — Final visual validation and SERVER-6 closure

The user manually dragged the corrected iPhone 8 History list and confirmed reaching the oldest 217–72 match. The native-input observer recorded begin-drag delivery; visual inspection showed Matches 3, 2 and 1 at the bottom, with Match 1 fully visible and its button accessible. Matches 5 and 4 were visible at the top. The actual Match 1 button was clicked from that scrolled position, followed by Repetition; the Replay UI loaded and displayed 0 of 946 events with the expected four participants. Returning through detail to History restored the top position.

Native mouse wheel scrolling was verified in Editor Game View: the list visibly moved on both wheel operations and native-input.txt recorded NATIVE_WHEEL=YES. Device Simulator is used for touch input; its wheel attempt did not deliver a scroll event and is not presented as passing mouse evidence. Portrait visual inspection covered Apple iPhone 8 (16:9) and Apple iPhone X (notch/home-indicator safe area), plus resized Game View. Card widths and spacing were preserved without horizontal scrolling or overlapping cards; clipping at viewport edges is the intended scrolling mask. The iPhone 8 simulator was restored afterward.

Console displayed 0 errors (one existing warning). Working-tree status comparison against the S6-07 start added only HistoryReplayView.cs and the S607HistoryValidation.cs/.meta pair; the report was already untracked before this correction. Unrelated user files were not edited. No backend/API/Replay contract or persistence code changes occurred. The earlier five-match and API-restart durability PASS evidence remains valid. Optional privileged server log/Redis cleanup/resource checks remain NOT_VERIFIED_SUDO_REQUIRED as previously reported; they were not represented as verified.

The established source-level defect was default wheel sensitivity 1, producing insufficient movement. Directly dispatched drag events already worked before correction; an absent drag handler is not claimed. The candidate additionally replaces manual card placement with Unity layout and resets bounds/offset for History/detail transitions. Manual touch validation now passes.

```makefile
SERVER-6 S6-07 HISTORY SCROLL
=============================
ROOT_CAUSE=DEFAULT_WHEEL_SENSITIVITY_1_INSUFFICIENT_MOVEMENT
HISTORY_SCROLL_IMPLEMENTED_BEFORE=YES_MANUAL_CONTENT_LAYOUT
VERTICAL_SCROLL=YES
HORIZONTAL_SCROLL=NO
HISTORY_INITIAL_SCROLL_POSITION=TOP
TOUCH_SCROLL=PASS
MOUSE_WHEEL_SCROLL=PASS
UNITY_HISTORY_ENTRIES_VISIBLE_TOTAL=5
MATCH_5_REACHABLE=YES
MATCH_4_REACHABLE=YES
MATCH_3_REACHABLE=YES
MATCH_2_REACHABLE=YES
MATCH_1_REACHABLE=YES
HISTORY_SCROLL_REACHES_OLDEST_SERVER6_MATCH=YES
MATCH_1_REPLAY_FROM_SCROLLED_HISTORY=PASS
HISTORY_EMPTY_STATE_PRESERVED=YES
RESPONSIVE_VALIDATION=PASS_IPHONE_8_IPHONE_X
SAFE_AREA_VALIDATION=PASS
CONSOLE_ERRORS=0
BACKEND_SOURCE_CHANGED=NO
HISTORY_API_CHANGED=NO
REPLAY_API_CHANGED=NO
MATCHES_STARTED=5
MATCHES_COMPLETED=5
EXTRA_MATCHES_STARTED=0
PREEXISTING_USER_FILES_PRESERVED=YES
UNEXPECTED_FILES=NONE_IDENTIFIED
COMMIT=NONE
PUSH=NONE
S6_07_SUCCESS=YES
SERVER_6_SUCCESS=YES
NEXT=SERVER-6 FINAL REVIEW/CHECKPOINT
```


## Final checkpoint review — retained S6-02 / S6-03 chronology

This retrospective fills the intermediate chronology without replacing the original failed gates above. S6-02 / S6-02R found the Unity environment selection pointing at LOCAL. The separately authorized configuration selected TEST at https://domino-api-test.teamfho.com; LOCAL support and the environment-selection implementation remain unchanged. No production endpoint was configured.

S6-03 resolved the current Unity Firebase identity but stopped before matchmaking at identity discovery. The retained identity-diagnostic.txt records DirectoryNotFoundException for all three external slot reads and DISCOVERY_COUNT. S6-03A identified this as validation running in the wrong filesystem context, not evidence that the TEST accounts needed recreation. S6-03B moved opponent identity discovery to external tooling: Unity reads no Swarm credential files; ephemeral session-bound fingerprints establish distinctness without emitting raw UIDs. The coordination gate passed, then the old direct player-document check failed with FirestoreException. Firestore rules were not weakened.

S6-03C replaced direct Unity Firestore player reads with the existing authenticated backend bootstrap contract. The current Firebase UID is compared internally with the backend-resolved player; the client cannot select an arbitrary player UID. player-correlation.txt records PASS, History=0, Premium Trial and unlimited Replay, with zero matches started. No administrative identity lookup was used for correlation. Evidence retained under ignored Generated/SERVER6-current: preflight.txt, identity-diagnostic.txt, coordination.txt, player-correlation.txt. These successive failures precede the approved five-match execution and do not override its final success.

## Final checkpoint — exact intentional inventory

Review source: 99002cad817e8208c15195de7b5bf6ea795fc7d5 on main. Exactly 45 intentional source/test/config/report paths are listed below. All 96 unrelated pending files retain their original SERVER6 baseline hashes. ApiSettings.asset is the sole authorized baseline exception and is classified as TEST configuration, not an unrelated user edit. Per-file pending classification and the checkpoint hash snapshot are retained under ignored Generated/SERVER6-checkpoint. Generated evidence, runtime outputs, AppData identities, credential files and unrelated user files are excluded.

- `SERVER6_TEST_CONFIGURATION`: `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S601CodecValidation.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S601CodecValidation.cs.meta`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S607HistoryValidation.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S607HistoryValidation.cs.meta`
- `S6_03_IDENTITY_COORDINATION`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6CurrentSession.cs`
- `S6_03_IDENTITY_COORDINATION`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6CurrentSession.cs.meta`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FinalValidation.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FinalValidation.cs.meta`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FiveMatches.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6FiveMatches.cs.meta`
- `S6_06_REPLAY_VALIDATOR`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match1Revalidation.cs`
- `S6_06_REPLAY_VALIDATOR`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match1Revalidation.cs.meta`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match2.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6Match2.cs.meta`
- `S6_06_REPLAY_VALIDATOR`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6ParticipantValidator.cs`
- `S6_06_REPLAY_VALIDATOR`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6ParticipantValidator.cs.meta`
- `S6_03_IDENTITY_COORDINATION`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6PlayerCorrelation.cs`
- `S6_03_IDENTITY_COORDINATION`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6PlayerCorrelation.cs.meta`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6RemainingMatches.cs`
- `SERVER6_TESTS`: `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server6RemainingMatches.cs.meta`
- `S6_01_ENTITLEMENTS_FIX`: `client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiJsonCodec.cs`
- `S6_07_HISTORY_SCROLL`: `client/DominoGame/Assets/_Domino/Scripts/Replay/HistoryReplayView.cs`
- `SERVER6_TESTS`: `client/Validation/PlayerFoundationClientTests.cs`
- `SERVER6_TESTS`: `client/Validation/ReadServer6Match2Seats.py`
- `SERVER6_TESTS`: `client/Validation/ReadServer6MatchSeats.py`
- `SERVER6_TESTS`: `client/Validation/RunPlayerFoundationClientTests.ps1`
- `S6_04_WINDOWS_LAUNCHER`: `client/Validation/RunServer6FiveMatches.ps1`
- `SERVER6_TESTS`: `client/Validation/RunServer6Match2.ps1`
- `SERVER6_TESTS`: `client/Validation/RunServer6ParticipantTests.ps1`
- `SERVER6_TESTS`: `client/Validation/RunServer6RemainingMatch.ps1`
- `SERVER6_TESTS`: `client/Validation/RunSwarmLauncherTests.ps1`
- `SERVER6_TESTS`: `client/Validation/RunSwarmLogReaderTests.ps1`
- `SERVER6_TESTS`: `client/Validation/RunSwarmSafeLogValidation.ps1`
- `SERVER6_REPORT`: `client/Validation/SERVER_6_FIVE_MATCHES_HISTORY_REPLAY_REPORT.md`
- `SERVER6_TESTS`: `client/Validation/Server6CorrelationTests.cs`
- `SERVER6_TESTS`: `client/Validation/Server6MetadataGet.java`
- `SERVER6_TESTS`: `client/Validation/Server6ParticipantTests.cs`
- `S6_05_CONCURRENT_LOG_READER`: `client/Validation/SwarmLogReader.ps1`
- `S6_04_WINDOWS_LAUNCHER`: `client/Validation/SwarmProcessLauncher.ps1`
- `S6_03_IDENTITY_COORDINATION`: `server/domino/tools/bot-swarm/build.gradle.kts`
- `S6_03_IDENTITY_COORDINATION`: `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/Identity.kt`
- `S6_03_IDENTITY_COORDINATION`: `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/IdentityCoordination.kt`
- `S6_03_IDENTITY_COORDINATION`: `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/IdentityCoordinator.kt`
- `SERVER6_TESTS`: `server/domino/tools/bot-swarm/src/test/kotlin/com/teamfho/swarm/IdentityCoordinationTests.kt`

Review preserves the S6-01 existing entitlement wire contract and latest-three FREE policy, the S6-04 shell-free ArgumentList launcher, S6-05 FileShare.ReadWrite complete-line reader, S6-06R explicit-seat/frozen-team semantics, and S6-07 vertical-only layout. No Firestore rules change is included. Identity/credential value comparison and secret-pattern review found no embedded credentials or raw Firebase UIDs in these files. Earlier COMMIT=NONE/PUSH=NONE blocks describe their chronological runs; this section prepares the authorized final checkpoint.

No tests, remote matches, Replay suite, visual checks, API restart or deployment were repeated during checkpoint review. Five TEST matches and their persisted data remain intact. SERVER-7 has not started. Optional privileged server-log, Redis-cleanup and container-resource observations remain unverified as documented above.
