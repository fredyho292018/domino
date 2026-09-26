"""Bounded host-side observer: 2s cadence, direct cgroup v2 deltas, no load generation.
Usage: python3 -I S702ResourceSampler.py /path/to/live-jvm.json (root on TEST host).
JVM file must be maintained by the bounded agent; stale/missing coverage is explicit.
"""
import importlib.util,json,os,pathlib,subprocess,sys,time,socket

def dependencies():
    spec=importlib.util.spec_from_file_location('capacity',pathlib.Path(__file__).with_name('CapacityMetrics.py'))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def container(name):
    fmt='{"pid":{{.State.Pid}},"restarts":{{.RestartCount}},"oom":{{.State.OOMKilled}},"running":{{.State.Running}},"health":"{{.State.Health.Status}}"}'
    return json.loads(subprocess.run(['docker','inspect','--format',fmt,name],capture_output=True,text=True,timeout=5,check=True).stdout)

def group(pid):
    line=next(x for x in pathlib.Path(f'/proc/{pid}/cgroup').read_text().splitlines() if x.startswith('0::'))
    return pathlib.Path('/sys/fs/cgroup')/line[3:].lstrip('/')

def read(cg):
    stat={k:int(v) for k,v in (x.split() for x in (cg/'cpu.stat').read_text().splitlines())}
    allowed=(cg/'cpuset.cpus.effective').read_text().strip()
    count=sum(int(x.split('-')[1])-int(x.split('-')[0])+1 if '-' in x else 1 for x in allowed.split(','))
    limits=[];p=cg
    while p!=pathlib.Path('/sys/fs'):
        if (p/'cpu.max').exists():limits.append((p/'cpu.max').read_text())
        if p==pathlib.Path('/sys/fs/cgroup'):break
        p=p.parent
    return {'usage':stat['usage_usec'],'limit':min(M.quota(x,count) for x in limits),
            'memoryBytes':int((cg/'memory.current').read_text()),'memoryMax':(cg/'memory.max').read_text().strip(),
            'throttledUsec':stat.get('throttled_usec'),'nrThrottled':stat.get('nr_throttled')}

def host():
    mem={k:int(v.split()[0])*1024 for k,v in (x.split(':',1) for x in pathlib.Path('/proc/meminfo').read_text().splitlines())}
    return {'ticks':list(map(int,pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:])),
            'HOST_LOAD_1M':float(pathlib.Path('/proc/loadavg').read_text().split()[0]),'hostAvailableBytes':mem['MemAvailable']}

class RedisInfo:
    def __init__(self):
        fmt='{{(index .NetworkSettings.Networks "cuban-domino-backend-network").IPAddress}}'
        address=subprocess.run(['docker','inspect','--format',fmt,'cuban-domino-redis'],capture_output=True,text=True,timeout=5,check=True).stdout.strip()
        self.socket=socket.create_connection((address,6379),timeout=3);self.stream=self.socket.makefile('rb')
    def read(self):
        self.socket.sendall(b'*2\r\n$4\r\nINFO\r\n$3\r\nall\r\n')
        header=self.stream.readline();assert header.startswith(b'$')
        size=int(header[1:]);assert 0<size<1000000
        raw=self.stream.read(size);assert self.stream.read(2)==b'\r\n'
        selected={}
        for line in raw.decode().splitlines():
            k,_,v=line.partition(':')
            if k in ('used_memory','used_memory_peak','maxmemory','evicted_keys','rejected_connections','total_error_replies'):selected[k]=int(v)
            if k=='errorstat_OOM':selected[k]=int(v.split('count=')[1].split(',')[0])
        selected.setdefault('errorstat_OOM',0)
        return selected

def run(jvm_path,samples=31):
    if not 2<=samples<=1801:raise ValueError('INVALID_SAMPLE_BOUND')
    previous={};prior_host=None;last=None;next_tick=time.monotonic();redis=RedisInfo()
    for _ in range(samples):
        began=time.monotonic();row={'timestamp':time.time(),'monotonic':began,'containers':{}}
        for name in ('cuban-domino-api','cuban-domino-redis'):
            state=container(name);current=read(group(state['pid']));current['sampleMonotonic']=time.monotonic();metrics={**state,**current}
            before=previous.get(name)
            if before and before['pid']==state['pid'] and before['limit']==current['limit']:
                try:metrics.update(M.cpu(before['usage'],current['usage'],current['sampleMonotonic']-before['sampleMonotonic'],current['limit']))
                except ValueError:metrics['cpuValid']=False
                else:metrics['cpuValid']=True
            else:metrics['cpuValid']=False
            previous[name]={**current,'pid':state['pid']};row['containers'][name]=metrics
        h=host();row.update({k:v for k,v in h.items() if k!='ticks'})
        try:row['HOST_CPU_PERCENT']=M.host_cpu(prior_host,h['ticks']) if prior_host else None
        except ValueError:row['HOST_CPU_PERCENT']=None
        prior_host=h['ticks'];last=began
        row['redis']=redis.read()
        try:
            data=json.loads(pathlib.Path(jvm_path).read_text());age=time.time()-data['timestampMillis']/1000
            keys=('timestampMillis','jvmAvailableProcessors','heapUsed','heapCommitted','heapMax','nonHeapUsed','nonHeapCommitted','gcCount','gcTimeMillis')
            row['jvm']={k:data[k] for k in keys};row['jvmFresh']=0<=age<=6
        except (OSError,ValueError,KeyError):row['jvmFresh']=False
        # Client socket count and registry outcomes must be joined by the coordinator, never inferred from Redis activity.
        row['activeWsSource']='COORDINATOR_REQUIRED';row['activeMatchesSource']='AUTHORITATIVE_REGISTRY_REQUIRED'
        row['sampleOverrun']=time.monotonic()-began>2
        print(json.dumps(row),flush=True);next_tick+=2;time.sleep(max(0,next_tick-time.monotonic()))

if __name__=='__main__':
    M=dependencies()
    try:run(sys.argv[1],int(sys.argv[2]) if len(sys.argv)>2 else 31)
    except Exception as e:print(json.dumps({'error':type(e).__name__,'valid':False}));raise SystemExit(1)
