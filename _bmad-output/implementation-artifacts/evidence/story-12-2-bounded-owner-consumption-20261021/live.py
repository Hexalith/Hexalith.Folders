from pathlib import Path
import subprocess,json,os,time,sys,xml.etree.ElementTree as ET
root=Path('/home/administrator/projects/hexalith/folders');out=Path(__file__).parent
attempt=sys.argv[1] if len(sys.argv)>1 else '1';lane=sys.argv[2] if len(sys.argv)>2 else 'real-provider';stem=out/(lane+'-live-attempt-'+attempt)
assert not Path(str(stem)+'.log').exists()
cmd=['dotnet','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll','-class','Hexalith.Folders.AppHost.Tests.'+('OperationalStatusBoundedHistoryAcceptanceTests' if lane=='capacity' else 'OperationalStatusDurabilityAcceptanceTests'),'-showLiveOutput','-result-xml',str(stem)+'.xml']
if len(sys.argv)>3:
 assert lane=='capacity' and sys.argv[3]=='10001', 'Only the configured10001 HTTP case can be selected;32769 coverage is component/SDK evidence.'
 cmd += ['-preEnumerateTheories','-displayName','*count: '+sys.argv[3]+')']
env=os.environ.copy();env['HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION']='true';start=time.time()
with open(str(stem)+'.log','w') as log:r=subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
item={'command':cmd,'environment':{key:env[key] for key in ('HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION','DOTNET_USE_POLLING_FILE_WATCHER') if key in env},'exit_code':r.returncode,'seconds':time.time()-start,'log':str(stem)+'.log','xml':str(stem)+'.xml'}
if Path(item['xml']).exists():item['assemblies']=[dict(a.attrib) for a in ET.parse(item['xml']).getroot().iter('assembly')]
if r.returncode==0:
 item['runner_exit_code']=r.returncode
 item['coverage_validation_errors']=[]
 if not item.get('assemblies') or sum(int(a['total']) for a in item['assemblies'])!=1:item['coverage_validation_errors'].append('Expected exactly one executed case.')
 if not item.get('assemblies') or not all(a['passed']=='1' and a['failed']=='0' and a['skipped']=='0' for a in item['assemblies']):item['coverage_validation_errors'].append('Expected one pass and zero failures/skips.')
 if item['coverage_validation_errors']:item['exit_code']=1
Path(str(stem)+'-result.json').write_text(json.dumps(item,indent=2)+'\n');print(json.dumps(item),flush=True)
raise SystemExit(item['exit_code'])
