"""SERVER-7P private provisioning orchestration. Never emits identity material."""
import pathlib, subprocess, os, json, sys, uuid, hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
LOAD=pathlib.Path.home()/'AppData/Local/TeamFHO/DominoSwarm/LOAD'
OUT=ROOT/'client/Validation/Generated/SERVER7P'
BASE='https://domino-api-test.teamfho.com'
def records():
 result=[]
 for p in sorted(LOAD.glob('group-*/slot-*.json')):
  a=json.loads(p.read_text(encoding='utf-8-sig'))
  assert a['projectId']=='teamfho-domino' and a['environment']=='TEST' and a['testSource']=='BOT_SWARM' and a['isTestAccount'] is True and a['uid'] and a['refreshToken']
  result.append(a['uid'])
 assert len(result)==len(set(result)) and len(result)<=100
 functional=[json.loads(p.read_text(encoding='utf-8-sig'))['uid'] for p in (LOAD.parent/'TEST').glob('slot-*.json')]
 assert not set(result)&set(functional)
 return result

def invoke(task,group,offset,count):
 cfg=json.loads((ROOT/'client/DominoGame/Assets/google-services.json').read_text(encoding='utf-8-sig'))
 assert cfg['project_info']['project_id']=='teamfho-domino'
 env=os.environ.copy()
 assert not env.get('FIREBASE_AUTH_EMULATOR_HOST') and not env.get('FIRESTORE_EMULATOR_HOST')
 env.update(DOMINO_SWARM_FIREBASE_PROJECT_ID='teamfho-domino',DOMINO_SWARM_FIREBASE_API_KEY=cfg['client'][0]['api_key'][0]['current_key'],DOMINO_SWARM_IDENTITIES_DIR=str(LOAD/f'group-{group:02}'),DOMINO_SWARM_TEST_BASE_URL=BASE,DOMINO_REAL_FIRESTORE_TESTS='true',DOMINO_SWARM_LOAD_POPULATION='SERVER7')
 env.pop('DOMINO_SWARM_EMULATOR',None)
 args=f'--clients={count} --slotOffset={offset} --environment=TEST --baseUrl={BASE} --mode=PARTNERS_2V2_ONLINE --requeue=false'
 p=subprocess.run([str(pathlib.Path(env['JAVA_HOME'])/'bin/java.exe'),'-jar','gradle/wrapper/gradle-wrapper.jar',':bot-swarm:'+task,'--args='+args,'--console=plain','--no-daemon'],cwd=ROOT/'server/domino',env=env,capture_output=True,text=True,timeout=600)
 # Retain only explicitly safe control records. Never save raw Gradle/SDK output.
 safe=[s for s in (p.stdout+'\n'+p.stderr).splitlines() if s.startswith(('SWARM_IDENTITY_PROVISIONED slot=','SWARM_PROVISION_FAILED stage=','STORAGE_ERROR=','LOAD_SAMPLE_REST=','LOAD_SAMPLE_WSS=','MATCHMAKING_STARTED=','LOAD_SAMPLE=FAIL_SAFE'))]
 for line in safe: print(line,flush=True)
 return p.returncode,safe

def main():
 OUT.mkdir(exist_ok=True)
 mode=sys.argv[1]
 if mode=='manifest':
  assert len(records())==100
  target=OUT/'plan.json';assert not target.exists()
  groups=[{'group':g,'directory':str(LOAD/f'group-{g:02}'),'slots':list(range((g-1)*20+1,g*20+1)),'localSlots':list(range(1,21)),'clients':20,'log':f'group-{g:02}.log','stop':f'group-{g:02}.stop'} for g in range(1,6)]
  plan={'runId':str(uuid.uuid4()),'project':'teamfho-domino','environment':'TEST','baseUrl':BASE,'loadStarted':False,'groups':groups,'intendedMatches':[list(range(i,i+4)) for i in range(1,101,4)],'observedMatchIds':[]}
  target.write_text(json.dumps(plan,indent=2));print('MANIFEST=PASS');return
 if mode=='inventory': print('LOAD_DISTINCT_IDENTITIES='+str(len(records())));return
 if mode=='provision':
  # Durable batch-attempt marker blocks automatic repetition of a partial failure.
  for group in range(1,6):
   for offset in (0,10):
    groupdir=LOAD/f'group-{group:02}';assert groupdir.is_dir()
    pending=groupdir/f'batch-{offset+1:02}.pending';done=groupdir/f'batch-{offset+1:02}.done'
    if done.exists():
     assert all((groupdir/f'slot-{i:02}.json').exists() for i in range(offset+1,offset+11));records();continue
    assert not pending.exists(),'BATCH_REQUIRES_REVIEW'
    before=len(records());existing=sum((groupdir/f'slot-{i:02}.json').exists() for i in range(offset+1,offset+11))
    pending.write_text(json.dumps({'requested':10,'existing':existing,'before':before}),encoding='utf-8')
    code,safe=invoke('provision',group,offset,10)
    after=len(records());passed=sum(s.startswith('SWARM_IDENTITY_PROVISIONED slot=') for s in safe)
    result={'group':group,'offset':offset,'requested':10,'existing':existing,'newCredentialRecords':after-before,'verified':passed,'failedOrUnverified':10-passed,'exitCode':code,'total':after,'uncertainCreationIntents':len(list(groupdir.glob('*.creation-pending')))}
    (OUT/f'batch-{group}-{offset}.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
    if code or passed!=10:raise RuntimeError('BATCH_FAILED_STOP_NO_RETRY')
    pending.rename(done)
  assert len(records())==100
 elif mode=='sample':
  assert len(records())==100
  result=[]
  for group in range(1,6):
   for slot in ((10,20) if group==3 else (1,20)):
    code,safe=invoke('loadAuthSample',group,slot-1,1)
    ok=code==0 and 'LOAD_SAMPLE_REST=PASS' in safe and 'LOAD_SAMPLE_WSS=PASS' in safe
    result.append({'globalSlot':(group-1)*20+slot,'pass':ok})
    (OUT/'auth-sample.json').write_text(json.dumps(result,indent=2))
    if not ok:raise RuntimeError('SAMPLE_FAILED_STOP')
  print('LOAD_AUTH_SAMPLE_PASS=10\nAUTHENTICATED_CONCURRENT_USERS_AFTER=0')
 else:raise ValueError('MODE')
if __name__=='__main__':
 try:main()
 except Exception as e:
  print('SERVER7P_STOP='+type(e).__name__,flush=True);sys.exit(1)
