# Backend infrastructure (INFRA-1)

CI only. No deployment, image publication, SSH, cloud resources or credentials in GitHub.
The running SERVER-3 installation is not changed by these files.

Replay Emulator tests generate DUEL and PARTNERS matches deterministically in memory
using the source-controlled `ReplayFixture` (seed 9, fixed clock and synthetic identities).
They persist those inputs to the Emulator and verify replay parity, paging and zero replay writes.
No retained Swarm archives or developer validation outputs are required. The original INFRA-1
failure and INFRA-1R validation evidence remain in the validation report.

- `test/compose.yaml`: sanitized future TEST deployment contract.
- `scripts/`: syntax/security checks and existing emulator suite launcher.
- `.github/workflows/backend-ci.yml`: Java 21, compile, tests, emulator, bootJar, Docker image checks.
- `docs/`: architecture, security, manual deployment and rollback boundaries.

No separate local API Compose: production-classpath API needs Firebase Admin and starts durable listeners.
Starting it is not necessary to verify packaging. Use isolated Redis and the existing emulator tests instead.

Run shell scripts with `bash`; executable Git file modes are not required. Python 3, Java 21,
Docker Engine/Compose and the repository Gradle wrapper are prerequisites. CI uses an ephemeral Ubuntu runner.

## Tests

| Group | CI treatment |
|---|---|
| Domain, controller, mocked Firebase, in-memory | `:test` |
| Redis presence/turn/matchmaking | `DOMINO_REDIS_TESTS=true`; disposable localhost:6379 |
| Follow rate gate | `DOMINO_S13_REDIS_TESTS=true`; localhost:6379 |
| A.2, A.3, S1.4B Redis | respective flags enabled; localhost:16379 |
| Firestore transactions/privacy/rules/recovery | `:emulatorTest`; localhost:18085, demo-domino-f0 |
| Simulated-client unit tests | `:bot-swarm:test` only; never starts a swarm |
| Real Firestore opt-in | excluded by established `test` task; never run `realFirestoreTest` |
| M5 Unity differential | explicitly disabled: requires 100 Unity-generated traces; separate Unity validation |
| Real Auth/Unity/two-process chaos/load harnesses | not invoked; separate bounded validation, not hidden skips |

`test` excludes REAL_FIRESTORE and EMULATOR tags. Its SDK endpoint is forced to localhost:1 by Gradle.
`emulatorTest` forces localhost:18085 and demo project. Workflow also sets invalid ADC path, emulator Auth
endpoint, demo project and real-cloud opt-in false. No service-account GitHub secret is requested.

The emulator launcher mirrors `server/domino/tools/f0/RunFirestoreEmulatorTests.ps1`: same JAR version,
SHA-256, port and Gradle task. It refuses an occupied port and stops only its own emulator process.
Use disposable Redis: tests intentionally mutate Redis. Never map these ports to an existing/shared server.
The OS-worker Lua test explicitly invokes `docker exec domino-redis`; CI therefore creates that exact
container name on its disposable hosted runner. Do not run this CI recipe against an existing developer
container with the same name. `FIREBASE_PROJECT_ID` must remain unset: configuration tests explicitly
verify missing project ID rejection; the SDK project is separately forced to a demo project.

Actions use maintained major versions (checkout v5, setup-java v5, setup-gradle v5), restricted token
permissions and read-only PR cache. See their official repositories for upgrades; image/emulator checksums
are deliberately pinned and must be reviewed when updated. GitHub-hosted workflow execution remains
unverified until review/commit/push; local command evidence is in the validation report.

References: [checkout](https://github.com/actions/checkout),
[setup-java](https://github.com/actions/setup-java),
[setup-gradle](https://github.com/gradle/actions/tree/main/setup-gradle).
