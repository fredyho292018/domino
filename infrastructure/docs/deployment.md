# Future manual deployment (not executed by INFRA-1)

1. Review/commit changes, pass backend CI and record full Git SHA. Image identity:
   `cuban-domino-api:<FULL_GIT_SHA>`; never rely on latest. PR SHA may be GitHub's merge commit.
2. Build with repository wrapper `bash gradlew :bootJar` in server/domino; Docker build context is
   server/domino and platform linux/amd64. Verify with `verify-image.sh`. No registry publish in INFRA-1.
3. Save image archive and compare SHA-256 after transfer before loading. No remote Docker daemon.
4. Back up current Compose with timestamp. Verify external network isolation and root-owned secret metadata.
   Verify ADC project and explicit environment project before allowing startup. Never print secret contents.
5. Supply deployment variables outside Git. Validate `docker compose config --quiet`; review selected
   metadata only. Compare normalized Redis service to existing service before switching configuration.
6. Future authorized startup: `docker compose up -d --no-deps --wait api`. Never scale to two instances.
7. Check minimal loopback health, no LAN/IPv6 public bind, Redis connections, Auth rejection, restart
   recovery and unchanged Redis ID/start time. Measure startup Firestore costs or mark NOT_MEASURED.

Current SERVER-3 source SHA: 163282b1b29fdd7fa9ff1317f699b00e9b3d4417. Existing deployment stays untouched.
CI builds and checks only: no SSH, automatic deployment, registry publication or GitHub deployment secrets.
Future CD needs separate approval and design. No downtime-free guarantee for the single-instance deployment.
