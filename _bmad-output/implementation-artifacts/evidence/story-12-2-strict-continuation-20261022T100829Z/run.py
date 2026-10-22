from pathlib import Path
import subprocess,sys,json,datetime,time,os
out=Path(__file__).resolve().parent
label=sys.argv[1];cmd=sys.argv[2:]
assert cmd and not (out/(label+'.json')).exists(), label
start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic()
env={k:v for k,v in os.environ.items() if k.startswith(('DOTNET_','HEXALITH_','EVENTSTORE_TEST_','FOLDERS_TASK36_'))}
with (out/(label+'.log')).open('w') as log:
 r=subprocess.run(cmd,cwd='/home/administrator/projects/hexalith/folders',stdout=log,stderr=subprocess.STDOUT)
data=dict(argv=cmd,cwd='/home/administrator/projects/hexalith/folders',started_utc=start,elapsed_seconds=time.monotonic()-clock,exit_code=r.returncode,environment=env)
(out/(label+'.json')).write_text(json.dumps(data,indent=2)+'\n');print(json.dumps(data),flush=True)
sys.exit(r.returncode)
