#!/usr/bin/env bash
set -euo pipefail
image=${1:?Supply cuban-domino-api FULL SHA image}
[[ "$image" =~ ^cuban-domino-api:[0-9a-f]{40}$ ]] || { echo INVALID_IMAGE_TAG; exit 1; }
root=$(cd "$(dirname "$0")/../.." && pwd)
tmp=$(mktemp -d)
container=''
cleanup() {
  if [[ -n "$container" ]]; then docker rm "$container" >/dev/null; fi
  rm -rf -- "$tmp"
}
trap cleanup EXIT
docker image inspect "$image" > "$tmp/image.json"
python3 - "$tmp/image.json" <<'PY'
import json,sys
i=json.load(open(sys.argv[1]))[0];c=i['Config']
assert i['Os']=='linux' and i['Architecture']=='amd64'
assert c['User']=='10001:10001'
assert '8080/tcp' in c['ExposedPorts']
assert c['Entrypoint']==['java','-jar','/app/domino.jar']
assert not c.get('Volumes')
assert not any(x.split('=',1)[0] in {'DOMINO_SOCIAL_CURSOR_KEY','GOOGLE_APPLICATION_CREDENTIALS'} for x in c.get('Env',[]))
print('IMAGE_METADATA=PASS')
PY
docker run --rm --network none --entrypoint /usr/bin/id "$image" -u | python3 -c 'import sys; assert sys.stdin.read().strip()=="10001"'
container=$(docker create --network none "$image")
# Never starts Spring or Firebase. Inspect the actual copied application artifact.
docker cp "$container:/app/domino.jar" "$tmp/domino.jar"
python3 "$root/infrastructure/scripts/secret-scan.py" --jar "$tmp/domino.jar"
docker run --rm --network none --entrypoint /bin/sh "$image" -c 'test "$(find /app -type f | wc -l)" -eq 1 && test ! -e /var/run/docker.sock'
echo IMAGE_SECURITY=PASS
