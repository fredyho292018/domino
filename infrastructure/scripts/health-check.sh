#!/usr/bin/env bash
set -euo pipefail
# Local host endpoint only; no token and no dependency details.
curl --fail --silent --show-error --max-time 10 http://127.0.0.1:38080/actuator/health |
  python3 -c 'import json,sys; assert json.load(sys.stdin)=={"status":"UP"}; print("LOCAL_HEALTH=PASS")'
