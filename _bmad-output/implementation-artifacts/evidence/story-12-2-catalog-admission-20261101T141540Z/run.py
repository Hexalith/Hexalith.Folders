import json,os,subprocess,sys,time
from datetime import datetime,timezone
from pathlib import Path
p=Path(__file__).parent; name=sys.argv[1]; command=sys.argv[2:]
start=datetime.now(timezone.utc).isoformat(); now=time.monotonic()
with (p/(name+'.raw.log')).open('w') as output:
 result=subprocess.run(command,stdout=output,stderr=subprocess.STDOUT)
receipt={'argv':command,'cwd':str(Path.cwd()),'startedAtUtc':start,'finishedAtUtc':datetime.now(timezone.utc).isoformat(),'seconds':time.monotonic()-now,'exitCode':result.returncode}
(p/(name+'.json')).write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt),flush=True)
lines=(p/(name+'.raw.log')).read_text(errors='replace').splitlines()
for line in lines:
 if any(x in line for x in ['Build succeeded','Build FAILED',' Error(s)',' Warning(s)',' error ','Total tests:','Passed:','Failed:','Skipped:','Time Elapsed']): print(line[:700])
sys.exit(result.returncode)
