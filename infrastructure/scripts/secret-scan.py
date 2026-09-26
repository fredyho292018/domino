"""Lightweight indicator scan; never print matching values. Not a full secret audit."""
import pathlib
import re
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
PRIVATE = re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\s*[\r\n]+')
JSON_KEY = re.compile(rb'"(?:private_key|refresh_token|client_secret)"\s*:\s*"[^"\s]{16,}')
CURSOR = re.compile(rb'(?m)^\s*DOMINO_SOCIAL_CURSOR_KEY\s*=\s*[A-Za-z0-9+/]{32,}={0,2}\s*$')


def unsafe(name, data):
    leaf = pathlib.PurePosixPath(name).name.lower()
    sensitive_name = (leaf == '.env' or leaf.startswith('.env.') or
                      'service-account' in leaf and leaf.endswith('.json') or
                      leaf in ('id_rsa', 'id_ed25519') or leaf.endswith(('.p12', '.pfx', '.key')))
    return (sensitive_name and not leaf.endswith('.example')) or bool(
        PRIVATE.search(data) or JSON_KEY.search(data) or CURSOR.search(data))


def scan_jar(path):
    bad = []
    with zipfile.ZipFile(path) as jar:
        for entry in jar.infolist():
            if entry.is_dir():
                continue
            # Dependencies are external binaries; inspect application resources/classes.
            if entry.filename.startswith('BOOT-INF/classes/'):
                if unsafe(entry.filename, jar.read(entry)):
                    bad.append(entry.filename)
    return bad


def main():
    if len(sys.argv) == 3 and sys.argv[1] == '--jar':
        bad = scan_jar(sys.argv[2])
    else:
        tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
        extra = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '-z',
                                        'infrastructure', '.github', 'server/domino/Dockerfile',
                                        'server/domino/.dockerignore'], cwd=ROOT).decode().split('\0')
        bad = []
        for name in sorted(set(tracked + extra) - {''}):
            path = ROOT / name
            if path.is_file() and unsafe(name, path.read_bytes()):
                bad.append(name)
    if bad:
        for name in bad:
            print('SECRET_INDICATOR_FILE=' + name)
        raise SystemExit(1)
    print('SECRET_INDICATOR_SCAN=PASS')


if __name__ == '__main__':
    main()
