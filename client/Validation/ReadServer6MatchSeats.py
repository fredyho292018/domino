"""Read-only authenticated TEST seat evidence; never emits credentials or UIDs."""
import pathlib, json, os, subprocess, urllib.request, urllib.parse, sys
from datetime import datetime, timezone

root = pathlib.Path(__file__).resolve().parents[2]
number=int(sys.argv[1]); assert 3<=number<=5
out = root / f'client/Validation/Generated/SERVER6-match{number}'
phase = sys.argv[2]
assert phase in ('initial', 'final')
def load(path): return json.loads(path.read_text(encoding='utf-8-sig'))
try:
    mid = (out/f'match-{number}.txt').read_text().strip()
    cfg = load(root/'client/DominoGame/Assets/google-services.json')
    assert cfg['project_info']['project_id'] == 'teamfho-domino'
    key = cfg['client'][0]['api_key'][0]['current_key']
    slots = []
    uids = set()
    for i in range(1, 4):
        account = load(pathlib.Path.home()/f'AppData/Local/TeamFHO/DominoSwarm/TEST/slot-{i:02}.json')
        assert account['projectId']=='teamfho-domino' and account['environment']=='TEST' and account['isTestAccount'] and account['testSource']=='BOT_SWARM'
        assert account['uid'] not in uids
        uids.add(account['uid'])
        body = urllib.parse.urlencode({'grant_type':'refresh_token','refresh_token':account['refreshToken']}).encode()
        req = urllib.request.Request('https://securetoken.googleapis.com/v1/token?key='+key,data=body,headers={'Content-Type':'application/x-www-form-urlencoded'})
        with urllib.request.urlopen(req,timeout=40) as response: auth=json.load(response)
        assert auth['user_id']==account['uid']
        def get(path):
            result=subprocess.run([os.environ['JAVA_HOME']+'/bin/java.exe',str(root/'client/Validation/Server6MetadataGet.java')],input=auth['id_token']+'\n'+path+'\n',capture_output=True,text=True,timeout=55)
            status,_,body=result.stdout.partition('\n')
            assert result.returncode==0 and status=='200'
            return json.loads(body)
        snapshot=get('matches/'+mid+'/snapshot')
        seat=snapshot['privateState']['seat']
        assert snapshot['publicState']['matchId']==mid
        if phase=='final':
            history=get('players/me/history?limit=20')
            row=next(x for x in history['items'] if x['history']['matchId']==mid)
            assert row['selfSeat']==seat
        slots.append({'label':f'SLOT_{i:02}','selfSeat':seat,'snapshotSeat':seat})
    assert len({x['selfSeat'] for x in slots})==3
    proof={'matchId':mid,'environment':'TEST','project':'teamfho-domino','source':'AUTHENTICATED_SELF_SEATS','observedAt':datetime.now(timezone.utc).isoformat(),'slots':slots}
    target=out/('identity-seats-'+phase+'.json')
    temporary=target.with_suffix('.tmp')
    with temporary.open('x') as file: json.dump(proof,file,indent=2)
    temporary.rename(target)
    print('AUTHENTICATED_SEATS_'+phase.upper()+'=PASS')
except Exception as error:
    print('AUTHENTICATED_SEATS=STOP TYPE='+type(error).__name__)
    sys.exit(1)
