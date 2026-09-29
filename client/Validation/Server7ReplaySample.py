"""Exactly five registered completed LOAD match samples; credential values stay in memory."""
import pathlib,json,urllib.request,urllib.parse,subprocess,os,sys
R=pathlib.Path(__file__).resolve().parents[2];O=R/'client/Validation/Generated/SERVER7B';S=O/'replay-sample'
def main():
 reg=json.loads((O/'match-registry.json').read_text());assert len(reg['completedIds'])>=5
 plan=json.loads((O/'run.json').read_text());cfg=json.loads((R/'client/DominoGame/Assets/google-services.json').read_text(encoding='utf-8-sig'));assert cfg['project_info']['project_id']=='teamfho-domino'
 selected=set();S.mkdir(exist_ok=True)
 for g in plan['groups']:
  for slot in range(1,21):
   a=json.loads((pathlib.Path(g['directory'])/f'slot-{slot:02}.json').read_text());assert a['projectId']=='teamfho-domino' and a['environment']=='TEST'
   req=urllib.request.Request('https://securetoken.googleapis.com/v1/token?key='+cfg['client'][0]['api_key'][0]['current_key'],data=urllib.parse.urlencode({'grant_type':'refresh_token','refresh_token':a['refreshToken']}).encode())
   with urllib.request.urlopen(req,timeout=30) as f:auth=json.load(f)
   assert auth['user_id']==a['uid']
   def get(path):
    p=subprocess.run([str(pathlib.Path(os.environ['JAVA_HOME'])/'bin/java.exe'),str(R/'client/Validation/Server7MetadataGet.java')],input=auth['id_token']+'\n'+path+'\n',capture_output=True,text=True,timeout=60)
    status,_,body=p.stdout.partition('\n');assert p.returncode==0 and status=='200','METADATA_HTTP_FAILED';return json.loads(body)
   h=get('players/me/history?limit=20');rows=[x for x in h['items'] if x['history']['matchId'] in reg['completedIds'] and x['history']['matchId'] not in selected]
   if not rows:continue
   row=sorted(rows,key=lambda x:x['history']['matchId'])[0];mid=row['history']['matchId'];assert row['replayAvailable']
   m=get('matches/'+mid+'/replay');snap=get('matches/'+mid+'/snapshot');assert m['replayAvailable'] and snap['publicState']['status']=='FINISHED'
   events=[]
   while len(events)<m['lastSequence']:
    path='matches/'+mid+'/replay/events?after='+str(len(events))+'&limit=250'
    if m.get('accessSession'):path+='&session='+urllib.parse.quote(m['accessSession'],safe='')
    page=get(path);assert page['items'] and page['lastSequence']==m['lastSequence']
    for e in page['items']:assert e['sequence']==len(events)+1;events.append(e)
   m.pop('accessSession',None)
   snapshot={'publicState':snap['publicState'],'privateState':{'seat':snap['privateState']['seat']},'ruleSnapshot':snap['ruleSnapshot']}
   data={'manifest':m,'history':row,'snapshot':snapshot,'events':events}
   text=json.dumps(data);assert a['uid'] not in text and a['refreshToken'] not in text and auth['id_token'] not in text
   (S/(mid+'.json')).write_text(text);selected.add(mid);print('REPLAY_SAMPLE_FETCHED='+str(len(selected)),flush=True);break
 assert len(selected)==5
 print('REPLAY_SAMPLE_FETCH=PASS')
if __name__=='__main__':
 try:main()
 except Exception as e:print('SAMPLE_STOP='+type(e).__name__);sys.exit(1)
