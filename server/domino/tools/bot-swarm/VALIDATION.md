# I3.1 validation checkpoints

Latest resumed F0.1 matrix and bounded real run: [F01_RESUMED_REPORT.md](F01_RESUMED_REPORT.md).
The entries below are historical evidence from before that continuation; their earlier blockers do not describe the current automated matrix. Human Unity acceptance remains pending in the new report.

## Latest result — 2026-09-19

The 2026-09-16 two-client DUEL run completed successfully after its 900-second
duration: 3 completed matches, 20 completed rounds, 432 commands, 2 rejected
commands, 10 resyncs, 6 per-player history confirmations and automatic requeue.
Maximum simultaneous matches: 1. Both clients closed through normal cancellation.
No fatal errors or sequence gaps were reported by that run. Four matches were
started; the fourth was still active at the duration boundary.

Read-only inspection confirmed the first completed match was FINISHED, with
2 distinct remote humans, zero engine bots, 2 administrative test account
markers, the match validation marker, and 2 marked history entries. The three
completed matches remain retained:

- bf02fdcb-f134-4c6c-a3f9-9453a9580e5e
- fc5a30d4-2e62-4ff7-b5e7-f0da48f366f5
- 0752bc6c-ea46-4470-9f84-f4a354efb834

On resuming September 19, the mandatory single bootstrap returned HTTP 503.
One diagnostic attempt using the two existing accounts confirmed
`STORAGE_ERROR=RESOURCE_EXHAUSTED` at `REGISTER_TEST_IDENTITY`.
No ten-client run or additional identity provisioning was started. The validation
backend PID 14616 was stopped after verifying its process identity, to prevent
continuing discovery queries. Redis remains running.

Failure context: prior to DUEL_1V1 escalation to 10 clients; current load clients=0;
current load active/completed matches=0; historical completed duel matches=3;
operation=bootstrap followed by test-registry access; timestamp approximately
2026-09-19 13:07 America/Chicago. One RESOURCE_EXHAUSTED code was directly
observed in the diagnostic; this is not a claim about all project-wide errors.

DUEL_2_CLIENTS=PASS

DUEL_10_CLIENTS=BLOCKED_BEFORE_START

DUEL_20_CLIENTS=NOT_RUN

PARTNERS_4_CLIENTS=NOT_RUN

PARTNERS_10_CLIENTS=NOT_RUN

PARTNERS_20_CLIENTS=NOT_RUN

HUMAN_VS_SWARM_DUEL=NOT_RUN

HUMAN_PLUS_3_SWARM_PARTNERS=NOT_RUN

REQUEUE=PASS

RECONNECT=NOT_RUN

GRACEFUL_SHUTDOWN=PASS (duration boundary)

MONETIZATION_ACTIVITY=0 (swarm endpoint allowlist; no reward calls)

Latest regression evidence remains 456 backend PASS, 2 skipped, 23 swarm PASS;
the final regression after the full real matrix is still pending.

COMMIT=NONE; PUSH=NONE; DEPLOY=NONE.

The earlier checkpoint below is retained as historical evidence and is superseded
by this section where results differ.

Date: 2026-09-15

SOURCE_SHA=82c1a985ee31a631ede0b233f1f1a25adf84bd4d

This working tree includes earlier pending M5 changes. No commit or staging was
performed. This document records partial evidence, not final I3.1 acceptance.

| Check | Observed result |
|---|---|
| Backend tests with local Redis enabled | 456 PASS, 2 SKIPPED, 0 failures (458 total) |
| Swarm unit tests | 23 PASS, 0 failures |
| Provision source compilation | PASS |
| Backend bootJar | PASS |
| Swarm classes/dependency in backend bootJar | Absent |
| Stable Firebase slots provisioned | 2 |
| Administrative Firestore test markers | Created/reused for those 2 slots |
| Production environment and remote HTTP guards | Unit tests PASS |
| Duplicate local identity lease | Unit test PASS |
| Monetization/direct match creation rejected before token retrieval | Unit test PASS |
| Real duel 2 clients | BLOCKED: bootstrap HTTP 503, Firestore RESOURCE_EXHAUSTED |
| Real duel 10/20 clients | NOT_RUN |
| Real partners 4/10/20 clients | NOT_RUN |
| Human versus swarm / human plus 3 swarm | NOT_RUN |
| Real requeue/reconnect/shutdown | NOT_RUN |
| Real match/history marker propagation | NOT_RUN; in-memory backend tests PASS |
| Backend restart recovery | NOT_RUN |
| Unity visual regression during this checkpoint | NOT_RUN |

Docker Desktop was resumed and the existing loopback Redis service is running.
The Redis opt-in regression suite passed. Real execution is now blocked by
Firestore returning `RESOURCE_EXHAUSTED` during administrative access to the
existing test marker. Firebase getUser succeeds. The two-client attempt failed
at bootstrap with HTTP 503, exhausted its bounded retries and exited unsuccessfully:
2 clients started, 16 errors, zero matches and zero monetization activity.
No successful real gameplay validation is claimed. The exact quota/resource
limit has not been identified; the user was asked to check Firebase/Google Cloud.

Backend validation used runtime port 8081 because 8080 belongs to an unrelated
Steam process; no saved Unity API configuration was changed. The validation
backend was then stopped to prevent repeated discovery reads while Firestore
is unavailable. Redis remains running. The provisioner now reports only the
safe storage status code, not raw exception responses or credentials.

The backend HTTP regression initially returned 503 because the new participant
profile lookup reached Firestore in an in-memory test. A server-side profile
provider now permits the integration fixture to supply test profiles; normal
runtime resolution still reads server-owned Firestore records. The full backend
suite then passed.

AdsSettings and ApiSettings hashes are unchanged from the start of this work.
The pre-existing AdsSettings trailing whitespace reported by `git diff --check`
was not edited. No source Unity changes were added for this checkpoint.

COMMIT=NONE

PUSH=NONE

DEPLOY=NONE

Next: resolve the Firestore resource limit, restart the validation backend with
the latest compiled code, then run the real matrix.
Do not label I3.1 complete until those results are recorded.
