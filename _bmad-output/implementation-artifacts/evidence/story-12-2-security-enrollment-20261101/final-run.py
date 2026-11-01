import datetime, json, pathlib, subprocess, sys, os
packet=pathlib.Path(__file__).resolve().parent
name=sys.argv[1]; argv=sys.argv[2:]
start=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (packet/(name+'.log')).open('w') as log:
 result=subprocess.run(argv,stdout=log,stderr=subprocess.STDOUT)
(packet/(name+'.json')).write_text(json.dumps(dict(environmentPins={key:os.environ[key] for key in ['TMPDIR'] if key in os.environ},argv=argv,cwd=str(pathlib.Path.cwd()),startedAtUtc=start,finishedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),exitCode=result.returncode),indent=2)+'\n')
print(name+': '+str(result.returncode))
sys.exit(result.returncode)
