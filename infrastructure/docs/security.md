# Security contract

- `/etc/cuban-domino-club/firebase-test-service-account.json`: root:domino-secrets, 0640;
  bind to `/run/secrets/firebase-test-service-account.json` read-only. Add actual group GID to API.
- `/etc/cuban-domino-club/social-cursor.env`: root:root, 0600; Compose reads/injects environment.
  Contains a stable random 32-byte Base64 `DOMINO_SOCIAL_CURSOR_KEY`. Do not regenerate on restart.
- Secret directory root:root 0700. No world-readable files, image secrets, Docker socket or root Java.
- Environment injection means Docker administrators can inspect the cursor. Never dump full `docker inspect`
  or resolved `docker compose config` into logs. Use quiet validation and selected metadata.
- Explicit `FIREBASE_PROJECT_ID` and `GOOGLE_CLOUD_PROJECT` must match credential project `teamfho-domino`.
  IAM runtime: `roles/datastore.user` plus custom permission `firebaseauth.users.get`; no Owner/Editor.
- No real Firebase, Firestore or production Redis in CI. No GitHub SSH/Firebase/Cloudflare secrets.

The lightweight scanner checks tracked content and pending infrastructure for obvious private-key blocks,
service-account JSON indicators, token fields and cursor assignments, reporting filenames only.
The image check scans packaged application classes/resources and restricts /app to the JAR. This is not
an exhaustive dependency vulnerability or entropy-based secrets audit. Standard public CA certificates
and dependency binaries are not classified as application credentials.

Health contract: GET `/actuator/health` returns only `{"status":"UP"}`. REST requires Firebase identity;
WebSocket `/ws/v1/realtime` allows upgrade but requires AUTH within the configured timeout.
Do not start the production entrypoint in CI just to test the image: it activates Firestore listeners/workers.
