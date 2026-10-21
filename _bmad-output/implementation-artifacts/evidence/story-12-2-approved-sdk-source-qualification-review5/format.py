from pathlib import Path
import subprocess,json,os,time
root=Path('/home/administrator/projects/hexalith/folders');out=Path(__file__).parent
rows=json.loads((root/'_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-review4/story12-2-sdk-review4-consumer-format.json').read_text());records=[]
for x in rows:
 cmd=x['command'];env=os.environ.copy();env.update(x['environment']);start=time.time();log=out/('format-'+x['mode']+'.log')
 with log.open('w') as f:r=subprocess.run(cmd,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
 records.append({'mode':x['mode'],'command':cmd,'environment':x['environment'],'exit_code':r.returncode,'seconds':time.time()-start,'log':str(log)})
 (out/'format.json').write_text(json.dumps(records,indent=2)+'\n');print(records[-1],flush=True)
 assert r.returncode==0,log
