#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf -- "$tmp"' EXIT
# Empty synthetic env file only. No production paths are read and no service starts.
touch "$tmp/cursor.env" "$tmp/credential-placeholder"
export API_IMAGE_SHA=0000000000000000000000000000000000000000
export DOMINO_SECRETS_GID=10002
export SOCIAL_CURSOR_ENV_FILE="$tmp/cursor.env"
export FIREBASE_CREDENTIAL_FILE="$tmp/credential-placeholder"
docker compose -f "$root/infrastructure/test/compose.yaml" config --format json > "$tmp/compose.json"
python3 - "$tmp/compose.json" <<'PY'
import json,sys
c=json.load(open(sys.argv[1]));a=c['services']['api'];r=c['services']['redis']
assert set(c['services'])=={'api','redis'}
assert a['user']=='10001:10001' and a['read_only'] and not a.get('privileged',False)
assert float(a['cpus'])==4 and int(a['mem_limit'])==4294967296
assert 'ALL' in a['cap_drop'] and 'no-new-privileges:true' in a['security_opt']
assert len(a['ports'])==1 and a['ports'][0]['host_ip']=='127.0.0.1'
assert str(a['ports'][0]['published'])=='38080' and a['ports'][0]['target']==8080
assert not r.get('ports') and set(r['networks'])=={'backend'}
assert set(a['networks'])=={'backend','egress'}
assert a['volumes'][0]['read_only'] and len(a['volumes'])==1
assert 'docker.sock' not in str(a['volumes'])
assert a['environment']['FIREBASE_PROJECT_ID']=='teamfho-domino'
print('COMPOSE_SECURITY=PASS')
PY
