"""Bounded SERVER-6 preservation read; functional identity never joins matchmaking."""
import pathlib,json,urllib.request,urllib.parse,subprocess,os,sys
R=pathlib.Path(__file__).resolve().parents[2];O=R/'client/Validation/Generated/SERVER7B'
try:
 phase=sys.argv[1];assert phase in ('before','after')
 cfg=json.loads((R/'client/DominoGame/Assets/google-services.json').read_text(encoding='utf-8-sig'));assert cfg['project_info']['project_id']=='teamfho-domino'
 a=json.loads((pathlib.Path.home()/'AppData/Local/TeamFHO/DominoSwarm/TEST/slot-01.json').read_text(encoding='utf-8-sig'));assert a['projectId']=='teamfho-domino' and a['environment']=='TEST'
 req=urllib.request.Request('https://securetoken.googleapis.com/v1/token?key='+cfg['client'][0]['api_key'][0]['current_key'],data=urllib.parse.urlencode({'grant_type':'refresh_token','refresh_token':a['refreshToken']}).encode())
 with urllib.request.urlopen(req,timeout=30) as f:auth=json.load(f)
 assert auth['user_id']==a['uid']
 def get(path):
  p=subprocess.run([str(pathlib.Path(os.environ['JAVA_HOME'])/'bin/java.exe'),str(R/'client/Validation/Server6MetadataGet.java')],input=auth['id_token']+'\n'+path+'\n',capture_output=True,text=True,timeout=60)
  status,_,body=p.stdout.partition('\n');print('READ_KIND='+('HISTORY' if path.startswith('players/') else 'REPLAY')+' HTTP_STATUS='+(status if status.isdigit() else 'UNAVAILABLE'));
  if status=='403':return {'denied':True}
  assert p.returncode==0 and status=='200';return json.loads(body)
 ids=[(R/f'client/Validation/Generated/SERVER6-match{i}/match-{i}.txt').read_text().strip() for i in range(1,6)]
 h=get('players/me/history?limit=20');found={x['history']['matchId'] for x in h['items']};assert set(ids)<=found
 available=[x['history']['matchId'] for x in h['items'] if x['history']['matchId'] in ids and x.get('replayAvailable') is True]
 assert len(available)>=1
 for mid in ids:
  snap=get('matches/'+mid+'/snapshot');assert snap['publicState']['matchId']==mid
 for mid in available:
  m=get('matches/'+mid+'/replay');assert m['matchId']==mid
 result={'phase':phase,'historyMatchesFound':5,'replayManifestsAvailable':len(available),'authoritativeSnapshotsAvailable':5,'result':'PASS','matchIds':ids,'matchmakingStarted':False}
 (O/f'server6-{phase}.json').write_text(json.dumps(result,indent=2));print('SERVER6_PRESERVATION=PASS_5_HISTORY_5_SNAPSHOTS_REPLAYS_'+str(len(available)))
except Exception as e:print('SERVER6_PRESERVATION=STOP_'+type(e).__name__);sys.exit(1)


