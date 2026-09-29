"""Controlled SERVER-7B execution. No provisioning, no shell, bounded stop on any error."""
import pathlib,subprocess,json,os,time,datetime,re,sys
R=pathlib.Path(__file__).resolve().parents[2];O=R/'client/Validation/Generated/SERVER7B';BASE='https://domino-api-test.teamfho.com'
SSH=['C:/Windows/System32/OpenSSH/ssh.exe','-o','BatchMode=yes','-o','ConnectTimeout=8','-i',str(pathlib.Path.home()/'.ssh/id_ed25519'),'deploy@192.168.1.151']
def metrics():
 p=subprocess.run(SSH+['cat /home/deploy/server7-metrics-275abc49.jsonl'],capture_output=True,text=True,timeout=12)
 assert p.returncode==0,'METRICS_SSH_FAILED'
 rows=[json.loads(s) for s in p.stdout.splitlines() if s.strip()]
 assert rows and 'error' not in rows[-1],'METRICS_UNAVAILABLE'
 age=time.time()-datetime.datetime.fromisoformat(rows[-1]['utc']).timestamp();assert age<25,'METRICS_STALE'
 (O/'resources.json').write_text(json.dumps(rows,indent=2));return rows

def main():
 assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=R).decode().strip()=='1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38'
 assert json.loads((O/'server6-before.json').read_text())['result']=='PASS'
 plan=json.loads((O/'run.json').read_text());assert not plan['loadStarted']
 cfg=json.loads((R/'client/DominoGame/Assets/google-services.json').read_text(encoding='utf-8-sig'));assert cfg['project_info']['project_id']=='teamfho-domino'
 alluids=[]
 for g in plan['groups']:
  for s in range(1,21):
   a=json.loads((pathlib.Path(g['directory'])/f'slot-{s:02}.json').read_text());assert a['environment']=='TEST' and a['projectId']=='teamfho-domino' and a['isTestAccount'];alluids.append(a['uid'])
 assert len(set(alluids))==100
 baseline=metrics();assert (datetime.datetime.fromisoformat(baseline[-1]['utc'])-datetime.datetime.fromisoformat(baseline[0]['utc'])).total_seconds()>=60,'BASELINE_TOO_SHORT'
 ref=baseline[-1];assert all(c['health']=='healthy' and c['restarts']==0 and not c['oom'] for c in ref['containers']);assert ref['redis']['maxmemory_policy']=='noeviction'
 (O/'baseline-resources.json').write_text(json.dumps(baseline,indent=2))
 plan['loadStarted']=True;plan['startedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();(O/'run.json').write_text(json.dumps(plan,indent=2))
 children=[];started=time.monotonic();lastlaunch=-100;lastsample=-100;target=None;cpu_since=None;failure=None;stages=[];matches=set();completed=set();hold=O/'hold-until.txt';assigned={}
 try:
  while time.monotonic()-started<2100:
   elapsed=time.monotonic()-started
   groups=[];active=0
   for g,p,out,err in children:
    data=out.read_bytes();text=data[:data.rfind(b'\n')+1].decode('utf-8') if b'\n' in data else ''
    connected=set(re.findall(r'^SWARM_CLIENT_CONNECTED alias=(\S+)',text,re.M));m=set(re.findall(r'^SWARM_MATCH_FOUND alias=\S+ matchId=([\w-]+)',text,re.M));f=set(re.findall(r'^SWARM_MATCH_FINISHED alias=\S+ matchId=([\w-]+)',text,re.M));matches|=m;completed|=f
    for alias,mid in re.findall(r'^SWARM_MATCH_FOUND alias=(\S+) matchId=([\w-]+)',text,re.M):
     k=str(g)+':'+alias;assert k not in assigned or assigned[k]==mid,'SECOND_MATCH_ASSIGNMENT';assigned[k]=mid
    dash=[s for s in text.splitlines() if s.startswith(('DOMINO BOT SWARM ','SWARM_STOPPED '))]
    met=json.loads(dash[-1].split(' metrics=',1)[1] if ' metrics=' in dash[-1] else dash[-1].split(' ',1)[1]) if dash else {}
    n=met.get('activeAuthenticatedSockets',0);active+=n
    groups.append({'group':g,'connected':len(connected),'active':n,'metrics':met,'exitCode':p.poll()})
    if re.search(r'^SWARM_(CLIENT_FAILED|START_FAILED|STOP_REQUESTED)',text,re.M):raise RuntimeError('SWARM_CRITICAL_FAILURE')
    if p.poll() not in (None,0):raise RuntimeError('SWARM_EXIT_FAILURE')
   assert len(matches)<=25,'EXTRA_MATCH'
   protected=set(json.loads((O/'server6-before.json').read_text())['matchIds']);assert not matches&protected,'SERVER6_REGISTRY_CONTAMINATION'
   (O/'match-registry.json').write_text(json.dumps({'runId':plan['runId'],'matchIds':sorted(matches),'completedIds':sorted(completed),'assignments':assigned},indent=2))
   if elapsed-lastsample>=5:
    current=metrics()[-1];lastsample=elapsed
    assert current['hostAvailableBytes']>=1610612736,'HOST_MEMORY_STOP'
    for c in current['containers']:
     assert c['health']=='healthy' and c['running'] and c['restarts']==0 and not c['oom'],'CONTAINER_HEALTH_STOP'
     if c['name'].endswith('-api'):
      assert float(c['stats']['MemPerc'].rstrip('%'))<90,'API_MEMORY_STOP'
      cpu=float(c['stats']['CPUPerc'].rstrip('%'));cpu_since=(cpu_since if cpu_since is not None else elapsed) if cpu>=380 else None
      assert cpu_since is None or elapsed-cpu_since<30,'API_CPU_SUSTAINED_STOP'
    assert current['redis'].get('errorstat_OOM','')==ref['redis'].get('errorstat_OOM',''),'REDIS_MEMORY_REJECTION'
    assert int(current['redis']['evicted_keys'])==int(ref['redis']['evicted_keys']),'REDIS_EVICTION'
    assert int(current['redis']['total_error_replies'])==int(ref['redis']['total_error_replies']),'REDIS_ERROR_DELTA'
    with (O/'timeline.jsonl').open('a') as log:log.write(json.dumps({'elapsed':elapsed,'groups':groups,'active':active,'matches':len(matches),'completed':len(completed),'resourcesUtc':current['utc']})+'\n')
   if len(children)<5 and elapsed-lastlaunch>=15 and (not children or active==20*len(children)):
    group=plan['groups'][len(children)];g=group['group'];env=os.environ.copy();env.pop('GOOGLE_APPLICATION_CREDENTIALS',None)
    env.update(DOMINO_SWARM_FIREBASE_PROJECT_ID='teamfho-domino',DOMINO_SWARM_FIREBASE_API_KEY=cfg['client'][0]['api_key'][0]['current_key'],DOMINO_SWARM_IDENTITIES_DIR=group['directory'],DOMINO_SWARM_TEST_BASE_URL=BASE,DOMINO_REAL_FIRESTORE_TESTS='true',DOMINO_SWARM_STOP_FILE=str(O/f'group-{g:02}.stop'),DOMINO_SWARM_HOLD_UNTIL_FILE=str(hold))
    assert not env.get('FIREBASE_AUTH_EMULATOR_HOST') and not env.get('FIRESTORE_EMULATOR_HOST')
    args=[str(pathlib.Path(env['JAVA_HOME'])/'bin/java.exe'),'-cp',str(R/'server/domino/tools/bot-swarm/build/install/bot-swarm/lib/*'),'com.teamfho.swarm.MainKt','--clients=20','--mode=PARTNERS_2V2_ONLINE','--environment=TEST','--baseUrl='+BASE,'--requeue=false','--targetMatches=0','--durationSeconds=2100','--maxFailures=1','--summarySeconds=2','--seed='+str(20260926+g)]
    out=O/f'group-{g:02}.log';err=O/f'group-{g:02}.err'
    with out.open('xb') as fo,err.open('xb') as fe:p=subprocess.Popen(args,cwd=R/'server/domino',env=env,stdout=fo,stderr=fe,creationflags=subprocess.CREATE_NO_WINDOW)
    children.append((g,p,out,err));lastlaunch=elapsed;print('GROUP_STARTED='+str(g)+' ELAPSED='+str(round(elapsed,1)),flush=True)
   stage=20*len(children)
   if stage and active==stage and not any(s['users']==stage for s in stages):
    stages.append({'users':stage,'elapsed':elapsed,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()});(O/'stages.json').write_text(json.dumps(stages,indent=2));print('STAGE_AUTHENTICATED='+str(stage),flush=True)
   if target is None and active==100 and len(matches)==25:
    target=elapsed;hold.write_text(str(int((time.time()+600)*1000)));print('TARGET_REACHED=100_USERS_25_MATCHES',flush=True)
   if children and target is None and elapsed-lastlaunch>100:raise RuntimeError('STAGE_NOT_READY_TIMEOUT')
   if len(children)==5 and all(p.poll() is not None for _,p,_,_ in children):break
   time.sleep(.5)
  else:raise RuntimeError('RUN_TIMEOUT')
 except Exception as e:failure=str(e) if isinstance(e,(RuntimeError,AssertionError)) else type(e).__name__;print('GLOBAL_STOP='+failure,flush=True)
 finally:
  for g,p,_,_ in children:(O/f'group-{g:02}.stop').write_text('STOP')
  until=time.monotonic()+15
  for _,p,_,_ in children:
   try:p.wait(timeout=max(.1,until-time.monotonic()))
   except subprocess.TimeoutExpired:p.kill();p.wait(timeout=5)
  result={'runId':plan['runId'],'failure':failure,'groupsStarted':len(children),'matchesStarted':len(matches),'matchesCompletedObserved':len(completed),'targetAt':target,'duration':time.monotonic()-started,'orphanProcesses':sum(p.poll() is None for _,p,_,_ in children),'exitCodes':[p.returncode for _,p,_,_ in children]}
  (O/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
if __name__=='__main__':
 try:main()
 except Exception as e:print('PRELOAD_STOP='+type(e).__name__,flush=True);sys.exit(1)
