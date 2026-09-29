"""Root-only bounded SERVER-7R observer. No mutations, payloads or raw key names in output."""
import collections
import hashlib
import json
import os
from pathlib import Path
import subprocess
import socket
import sys
import time


def command(*args):
    p = subprocess.run(args, capture_output=True, text=True, timeout=20)
    if p.returncode:
        raise RuntimeError('READ_COMMAND_FAILED:' + args[0])
    return p.stdout.strip()


def emit(kind, **data):
    print(json.dumps(dict(kind=kind, timestamp=time.time(), **data)), flush=True)


_redis = None


def redis(*args):
    global _redis
    assert args[0] in ('SCAN','TYPE','PTTL','ZCARD','SCARD','ZRANGE','SMEMBERS','GET','DBSIZE')
    if _redis is None:
        networks=json.loads(command('docker','inspect','--format','{{json .NetworkSettings.Networks}}','cuban-domino-redis'))
        address=networks['cuban-domino-backend-network']['IPAddress']
        connection=socket.create_connection((address,6379),timeout=10)
        _redis=connection.makefile('rwb',buffering=0)
    encoded=[str(a).encode() for a in args]
    _redis.write(b'*'+str(len(encoded)).encode()+b'\r\n'+b''.join(b'$'+str(len(a)).encode()+b'\r\n'+a+b'\r\n' for a in encoded))
    def read():
        line=_redis.readline()
        if not line: raise RuntimeError('REDIS_EOF')
        kind,data=line[:1],line[1:-2]
        if kind==b'+':return data.decode()
        if kind==b':':return int(data)
        if kind==b'$':
            size=int(data)
            if size<0:return None
            value=_redis.read(size+2)
            if len(value)!=size+2:raise RuntimeError('REDIS_SHORT_READ')
            return value[:-2].decode()
        if kind==b'*':return [read() for _ in range(int(data))]
        raise RuntimeError('REDIS_READ_FAILED')
    return read()


def classify(k):
    p = 'domino:v1:{presence}:'
    m = p + 'mm:'
    if k.startswith(m):
        suffix = k[len(m):]
        kind = suffix.split(':')[0]
        if kind in ('u', 'q', 'job', 'lease', 'rate', 'members', 'queues', 'order', 'jobs'):
            return ('LEASE' if kind == 'lease' else 'MATCHMAKING', 'mm.' + kind)
    if k.startswith(p):
        kind = k[len(p):].split(':')[0]
        if kind in ('players', 'player', 'connection', 'activity'):
            return ('PRESENCE', 'presence.' + kind)
    if k == 'domino:v1:{turn-schedule}:leader':
        return 'LEASE', 'turn.leader'
    if k == 'domino:v1:{turn-schedule}:due':
        return 'MATCH', 'turn.due'
    if k.startswith('domino:v1:social:rate:'):
        return 'OTHER', 'social.rate'
    return 'UNKNOWN', 'unknown'


def inventory(context):
    hashes = set(context['playerHashes'])
    matches = set(context['matchIds'])
    keys = set()
    cursor = '0'
    for _ in range(1000):
        cursor, page = redis('SCAN', cursor, 'COUNT', '100')
        keys.update(page)
        if str(cursor) == '0':
            break
    else:
        raise RuntimeError('SCAN_BOUND_EXCEEDED')
    if len(keys) > 5000:
        raise RuntimeError('KEY_BOUND_EXCEEDED')
    rows = []
    for key in sorted(keys):
        category, subtype = classify(key)
        typ = redis('TYPE', key)
        ttl = redis('PTTL', key)
        row = dict(fingerprint=hashlib.sha256(key.encode()).hexdigest(), category=category,
                   subtype=subtype, type=typ, ttlMillis=ttl,
                   runOwned=any(h in key for h in hashes) or any(m in key for m in matches))
        if typ in ('zset', 'set'):
            row['cardinality'] = redis('ZCARD' if typ == 'zset' else 'SCARD', key)
            if row['cardinality'] <= 1000:
                members = redis('ZRANGE', key, '0', '-1') if typ == 'zset' else redis('SMEMBERS', key)
                row['runMembers'] = len(set(members) & (hashes | matches))
        if subtype == 'mm.u' and typ == 'string':
            payload = json.loads(redis('GET', key))
            state = payload.get('state')
            row['state'] = state if state in ('QUEUED', 'RESERVED', 'MATCHED', 'FAILED') else 'UNKNOWN'
        rows.append(row)
    emit('redis', dbsize=redis('DBSIZE'), scanned=len(keys), rows=rows,
         counts=dict(collections.Counter(r['category'] for r in rows)))


def cgroup_path(pid):
    for line in Path('/proc/%s/cgroup' % pid).read_text().splitlines():
        if line.startswith('0::'):
            return Path('/sys/fs/cgroup') / line[3:].lstrip('/')
    raise RuntimeError('CGROUP_V2_REQUIRED')


def stat(cg):
    return {k: int(v) for k, v in (line.split() for line in (cg / 'cpu.stat').read_text().splitlines())}


def cpu_delta(a, b, elapsed, quota, host_cpus):
    cores = (b['usage_usec'] - a['usage_usec']) / (elapsed * 1000000)
    if elapsed <= 0 or cores < 0:
        raise ValueError('INVALID_COUNTER_DELTA')
    return dict(coresUsed=cores, hostEquivalentPercent=cores * 100,
                hostCapacityPercent=cores * 100 / host_cpus,
                quotaPercent=cores * 100 / quota if quota else None)


def main():
    if os.geteuid() != 0:
        raise RuntimeError('ROOT_REQUIRED')
    context = json.loads(Path(sys.argv[1]).read_text())
    assert context['runId'] == '275abc49-45e7-40d0-8019-11531e6c8d48'
    assert len(context['matchIds']) == 22 and len(context['playerHashes']) == 100
    fmt = '{{json .HostConfig}}'
    # Docker configuration stays in memory; only CPU/memory fields are emitted.
    config = json.loads(command('docker', 'inspect', '--format', fmt, 'cuban-domino-api'))
    pid = int(command('docker', 'inspect', '--format', '{{.State.Pid}}', 'cuban-domino-api'))
    cg = cgroup_path(pid)
    limits = []
    parent = cg
    while parent != Path('/sys/fs'):
        f = parent / 'cpu.max'
        if f.exists():
            value = f.read_text().strip()
            q, p = value.split()
            limits.append(dict(level=parent.name, cpuMax=value))
        if parent == Path('/sys/fs/cgroup'):
            break
        parent = parent.parent
    quotas = [int(x['cpuMax'].split()[0]) / int(x['cpuMax'].split()[1]) for x in limits if not x['cpuMax'].startswith('max ')]
    cpuset = (cg / 'cpuset.cpus.effective').read_text().strip()
    allowed = sum(int(x.split('-')[1])-int(x.split('-')[0])+1 if '-' in x else 1 for x in cpuset.split(',') if x)
    quota = min(quotas + ([allowed] if allowed else [])) if quotas or allowed else None
    emit('cpuConfig', fields={k: config.get(k) for k in ('NanoCpus','CpuQuota','CpuPeriod','CpuShares','CpusetCpus','Memory')},
         ancestors=limits, cpusetEffective=cpuset, effectiveCpus=quota, hostLogicalCpus=os.cpu_count(),
         cpuWeight=(cg / 'cpu.weight').read_text().strip(),
         containerVisibleCpus=command('docker','exec','cuban-domino-api','getconf','_NPROCESSORS_ONLN'))
    inventory(context)
    a, started = stat(cg), time.monotonic()
    initial = started
    for _ in range(30):
        time.sleep(5)
        b, ended = stat(cg), time.monotonic()
        emit('cpuSample', elapsed=ended-started, counters=b,
             **cpu_delta(a,b,ended-started,quota,os.cpu_count()))
        a, started = b, ended
    inventory(context)
    emit('complete', observationSeconds=time.monotonic()-initial, newLoad=False, mutations=False)


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        emit('error', errorType=type(exc).__name__)
        sys.exit(1)
