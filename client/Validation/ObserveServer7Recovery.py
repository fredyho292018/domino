"""Post-stop resource observation only. Never restarts or launches clients."""
import pathlib,subprocess,json,time,datetime
O=pathlib.Path(__file__).resolve().parent/'Generated/SERVER7B'
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-o','BatchMode=yes','-o','ConnectTimeout=8','-i',str(pathlib.Path.home()/'.ssh/id_ed25519'),'deploy@192.168.1.151']
start=time.monotonic();count=0
while True:
 p=subprocess.run(ssh+['cat /home/deploy/server7-metrics-275abc49.jsonl'],capture_output=True,text=True,timeout=15)
 if p.returncode:raise SystemExit('RECOVERY_METRICS_READ_FAILED')
 a=[json.loads(s) for s in p.stdout.splitlines() if s.strip()];assert 'error' not in a[-1]
 assert time.time()-datetime.datetime.fromisoformat(a[-1]['utc']).timestamp()<25
 (O/'resources-after.json').write_text(json.dumps(a,indent=2))
 with (O/'recovery.jsonl').open('a') as f:f.write(json.dumps(a[-1])+'\n')
 count+=1
 if time.monotonic()-start>=300:break
 time.sleep(15)
print('RECOVERY_OBSERVATION_SECONDS='+str(round(time.monotonic()-start)))
print('RECOVERY_SAMPLES='+str(count))
