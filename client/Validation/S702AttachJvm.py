"""Bounded HotSpot attach for the exact API process. Run manually as root on TEST host.
Loads only our counters agent; no instrumentation transforms, remote sockets, dumps or flags.
"""
import hashlib,json,os,pathlib,signal,socket,subprocess,time

def main():
    assert os.geteuid()==0,'ROOT_REQUIRED'
    base=pathlib.Path('/home/deploy')
    jar=base/'s702-jvm-agent.jar'
    expected=(base/'s702-jvm-agent.sha256').read_text().strip()
    assert hashlib.sha256(jar.read_bytes()).hexdigest()==expected,'AGENT_HASH_MISMATCH'
    p=subprocess.run(['docker','inspect','--format','{{.State.Pid}}','cuban-domino-api'],capture_output=True,text=True,check=True)
    pid=int(p.stdout);root=pathlib.Path(f'/proc/{pid}')
    args=(root/'cmdline').read_bytes().split(b'\0')
    assert b'/app/domino.jar' in args and not any(b'DisableAttachMechanism' in x for x in args),'UNEXPECTED_JVM'
    uid=int(next(x for x in (root/'status').read_text().splitlines() if x.startswith('Uid:')).split()[2])
    tmp=root/'root/tmp';agent=tmp/'s702-jvm-agent.jar'
    with agent.open('xb') as f:f.write(jar.read_bytes())
    os.chown(agent,uid,uid);os.chmod(agent,0o600)
    output='/tmp/domino-s702-'+str(time.time_ns())+'.json'
    sock=tmp/'.java_pid1';trigger=tmp/'.attach_pid1';created=False
    try:
        if not sock.exists():
            trigger.touch(exist_ok=False);created=True;os.chown(trigger,uid,uid)
            os.kill(pid,signal.SIGQUIT)
            until=time.monotonic()+10
            while not sock.exists() and time.monotonic()<until:time.sleep(.1)
        if not sock.exists():raise RuntimeError('ATTACH_LISTENER_UNAVAILABLE')
        with socket.socket(socket.AF_UNIX) as client:
            client.settimeout(15);client.connect(str(sock))
            client.sendall(('1\0load\0instrument\0false\0/tmp/s702-jvm-agent.jar='+output+'\0').encode())
            reply=b''
            while len(reply)<4096:
                part=client.recv(4096-len(reply))
                if not part:break
                reply+=part
            if not reply.startswith(b'0\n') or b'return code: 0' not in reply:raise RuntimeError('AGENT_LOAD_FAILED')
        destination=base/'s702-jvm-samples.jsonl'
        with destination.open('x') as out:
            os.chmod(destination,0o644)
            last=None
            for _ in range(33):
                time.sleep(2)
                try:data=json.loads((root/'root'/output.lstrip('/')).read_text())
                except (FileNotFoundError,json.JSONDecodeError):continue
                assert set(data)=={'timestampMillis','jvmAvailableProcessors','heapUsed','heapCommitted','heapMax','nonHeapUsed','nonHeapCommitted','gcCount','gcTimeMillis'}
                if data['timestampMillis']!=last:
                    out.write(json.dumps(data)+'\n');out.flush();last=data['timestampMillis']
        print('JVM_METRICS_CAPTURED=YES')
    finally:
        if created:trigger.unlink(missing_ok=True)
        agent.unlink(missing_ok=True)

if __name__=='__main__':
    try:main()
    except Exception as e:print('JVM_CAPTURE_FAILED='+type(e).__name__);raise SystemExit(1)
