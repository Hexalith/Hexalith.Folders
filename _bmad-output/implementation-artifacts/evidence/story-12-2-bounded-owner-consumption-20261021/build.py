from pathlib import Path
import json, subprocess, time, sys
root=Path('/home/administrator/projects/hexalith/folders'); out=Path(__file__).parent
attempt=sys.argv[1] if len(sys.argv)>1 else '1'
records=[]
for name,command in (
 ('restore',['dotnet','restore','Hexalith.Folders.slnx','-p:Configuration=Debug','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false']),
 ('build',['dotnet','build','Hexalith.Folders.slnx','-c','Debug','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','--no-restore','-m:1','-nr:false']),
):
 path=out/(name+'-attempt-'+attempt+'.log'); assert not path.exists()
 start=time.time()
 with path.open('w') as log: result=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT)
 records.append(dict(id=name,command=command,exit_code=result.returncode,seconds=time.time()-start,log=str(path)))
 (out/('builds-attempt-'+attempt+'.json')).write_text(json.dumps(records,indent=2)+'\n')
 print(json.dumps(records[-1]),flush=True)
 if result.returncode: raise SystemExit(result.returncode)
