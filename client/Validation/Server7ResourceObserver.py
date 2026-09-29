"""Read-only SERVER-7 observer. Run locally on the TEST host with Docker read access.
Prints only allowlisted resource counters; no environment, keys, UIDs or log bodies.
No service changes/restarts. Stops after 45 minutes or Ctrl+C.
"""
import subprocess,json,time,pathlib,datetime
NAMES=['cuban-domino-api','cuban-domino-redis']
def command(args):
 p=subprocess.run(args,capture_output=True,text=True,timeout=8)
 if p.returncode:raise RuntimeError('METRICS_COMMAND_FAILED')
 return p.stdout

def sample():
 row={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 mem={k:int(v.split()[0])*1024 for line in pathlib.Path('/proc/meminfo').read_text().splitlines() for k,v in [line.split(':',1)]}
 row['hostAvailableBytes']=mem['MemAvailable'];row['hostLoad']=[float(x) for x in pathlib.Path('/proc/loadavg').read_text().split()[:3]]
 row['hostCpuTicks']=list(map(int,pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:]))
 row['containers']=[]
 for name in NAMES:
  # Format is intentionally scoped; never inspect Config.Env or mounts/credentials.
  data=json.loads(command(['docker','inspect','--format','{"name":"{{.Name}}","restarts":{{.RestartCount}},"oom":{{.State.OOMKilled}},"running":{{.State.Running}},"health":"{{.State.Health.Status}}","memoryLimit":{{.HostConfig.Memory}},"nanoCpus":{{.HostConfig.NanoCpus}}}',name]))
  stats=json.loads(command(['docker','stats','--no-stream','--format','{{json .}}',name]))
  data['stats']={k:stats.get(k) for k in ('CPUPerc','MemUsage','MemPerc','NetIO','BlockIO','PIDs')}
  row['containers'].append(data)
 wanted={'used_memory','used_memory_peak','maxmemory','maxmemory_policy','evicted_keys','rejected_connections','connected_clients','total_error_replies','total_connections_received','total_commands_processed'}
 info=command(['docker','exec','cuban-domino-redis','redis-cli','--raw','INFO','all'])
 counters={}
 for line in info.splitlines():
  if ':' not in line:continue
  k,v=line.split(':',1)
  if k in wanted or k=='errorstat_OOM':counters[k]=v.strip()
 row['redis']=counters
 row['redisKeys']=int(command(['docker','exec','cuban-domino-redis','redis-cli','DBSIZE']).strip())
 row['jvmHeap']='NOT_MEASURED';row['activeWebSockets']='NOT_MEASURED'
 return row
if __name__=='__main__':
 end=time.monotonic()+2700
 while time.monotonic()<end:
  start=time.monotonic()
  try:print(json.dumps(sample()),flush=True)
  except KeyboardInterrupt:break
  except Exception:print(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'error':'METRICS_UNAVAILABLE'}),flush=True);break
  time.sleep(max(0,5-(time.monotonic()-start)))
