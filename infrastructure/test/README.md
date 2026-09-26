# TEST deployment template

This file is a future source of truth, not an instruction to overwrite SERVER-3 now.
Exactly one API instance. Project `teamfho-domino` is TEST/DEVELOPMENT by explicit owner decision.
Future PROD must use a different project.

`compose.env.example` contains empty deployment-variable names only. Supply a full 40-character SHA,
actual `getent group domino-secrets` GID, and protected absolute file paths. Do not store a real `.env` in Git.
The project ID and in-container credential path are explicit in Compose; the cursor comes from the protected
host env file. Run Compose with appropriate root permission to read that env file; Java remains UID 10001.

External networks must already exist: `cuban-domino-backend-network` (internal bridge) and
`cuban-domino-api-egress` (normal outbound bridge). Redis joins only the former; API joins both.
No profile is invented: use the default main application configuration, not test-classpath application-test.yaml.

`bash infrastructure/scripts/verify-compose.sh` validates with temporary empty placeholder files only;
it never reads `/etc/cuban-domino-club`, never creates networks and never starts containers.
Future deployment must additionally verify credentials/project and a valid stable Base64 cursor secret.
