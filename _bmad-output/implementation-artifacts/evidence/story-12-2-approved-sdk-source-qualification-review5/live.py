from pathlib import Path
import subprocess,json,os,time,sys,xml.etree.ElementTree as ET
root=Path('/home/administrator/projects/hexalith/folders');out=Path(__file__).parent
attempt=sys.argv[1] if len(sys.argv)>1 else '1';stem=out/('live-attempt-'+attempt)
assert not Path(str(stem)+'.log').exists()
cmd=['dotnet','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll','-class','Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests','-showLiveOutput','-result-xml',str(stem)+'.xml']
env=os.environ.copy();env['HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION']='true';start=time.time()
with open(str(stem)+'.log','w') as log:r=subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
item={'command':cmd,'environment':{'HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION':'true'},'exit_code':r.returncode,'seconds':time.time()-start,'log':str(stem)+'.log','xml':str(stem)+'.xml'}
if Path(item['xml']).exists():item['assemblies']=[dict(a.attrib) for a in ET.parse(item['xml']).getroot().iter('assembly')]
Path(str(stem)+'-result.json').write_text(json.dumps(item,indent=2)+'\n');print(json.dumps(item),flush=True)
raise SystemExit(r.returncode)
