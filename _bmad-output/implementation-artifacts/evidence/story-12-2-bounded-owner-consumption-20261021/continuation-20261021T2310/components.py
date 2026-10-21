from pathlib import Path
import subprocess,json,time,xml.etree.ElementTree as ET,os
root=Path('/home/administrator/projects/hexalith/folders');out=Path(__file__).parent
lanes=[('domain','Hexalith.Folders.Tests',[]),('server','Hexalith.Folders.Server.Tests',[]),('workers','Hexalith.Folders.Workers.Tests',[]),('apphost-helpers','Hexalith.Folders.AppHost.Tests',['-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityHttpTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilitySharedRebuildTests']),('docs','Hexalith.Folders.Contracts.Tests',['-class','Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests','-class','Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests']),('integration','Hexalith.Folders.IntegrationTests',[])]
rows=[]
for name,assembly,filters in lanes:
 log=out/(name+'.log');xml=out/(name+'.xml');assert not log.exists()
 command=['dotnet',str(root/'tests'/assembly/'bin/Debug/net10.0'/(assembly+'.dll')),*filters,'-result-xml',str(xml)]
 start=time.time()
 env=os.environ.copy();env.pop('HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION',None)
 with log.open('w') as stream:r=subprocess.run(command,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT)
 row=dict(name=name,command=command,exit_code=r.returncode,seconds=time.time()-start,log=str(log),xml=str(xml))
 if xml.exists():row['assemblies']=[dict(a.attrib) for a in ET.parse(xml).getroot().iter('assembly')]
 rows.append(row);(out/'components.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps(row),flush=True)
 if r.returncode:raise SystemExit(r.returncode)
