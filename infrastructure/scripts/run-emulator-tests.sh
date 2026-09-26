#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root/server/domino"
mkdir -p build
jar=build/cloud-firestore-emulator-v1.22.0.jar
hash=9b6498b7f62714d67f48f59b3818883cd682dbcd46b9f59511de81c97bb5166c
python3 - <<'PY'
import socket
with socket.socket() as s:
    s.bind(('127.0.0.1',18085))
PY
if [[ ! -f "$jar" ]]; then
  curl --fail --location --silent --show-error --output "$jar" https://storage.googleapis.com/firebase-preview-drop/emulator/cloud-firestore-emulator-v1.22.0.jar
fi
printf '%s  %s\n' "$hash" "$jar" | sha256sum --check --status
java -jar "$jar" --host 127.0.0.1 --port 18085 --project_id demo-domino-f0 --single_project_mode true > build/infra1-emulator.log 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true' EXIT
python3 - "$pid" <<'PY'
import os,socket,sys,time
for _ in range(180):
    os.kill(int(sys.argv[1]),0)
    try:
        with socket.create_connection(('127.0.0.1',18085),timeout=.2): break
    except OSError: time.sleep(.25)
else: raise SystemExit('Emulator startup timeout')
PY
bash gradlew :emulatorTest --no-daemon --console=plain --rerun-tasks
