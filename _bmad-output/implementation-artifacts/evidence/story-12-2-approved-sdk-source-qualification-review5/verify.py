import subprocess, pathlib, json, xml.etree.ElementTree as ET, time
root=pathlib.Path('/home/administrator/projects/hexalith/folders')
records=[]
projects=[('domain','Hexalith.Folders.Tests',[]),('server','Hexalith.Folders.Server.Tests',[]),('workers','Hexalith.Folders.Workers.Tests',[]),('apphost-helpers','Hexalith.Folders.AppHost.Tests',['-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityHttpTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilitySharedRebuildTests']),('docs','Hexalith.Folders.Contracts.Tests',['-class','Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests','-class','Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests'])]
for name,project,filters in projects:
    stem=pathlib.Path('/tmp/story12-2-review5-consumer-q4k0gea1/final-'+name)
    cmd=['dotnet',str(root/'tests'/project/'bin/Debug/net10.0'/(project+'.dll')),*filters,'-result-xml',str(stem)+'.xml']
    started=time.time()
    with open(str(stem)+'.log','w') as log:
        r=subprocess.run(cmd,cwd=root,stdout=log,stderr=subprocess.STDOUT)
    item={'name':name,'command':cmd,'exit_code':r.returncode,'seconds':time.time()-started,'log':str(stem)+'.log','xml':str(stem)+'.xml'}
    if pathlib.Path(item['xml']).exists():
        x=ET.parse(item['xml']).getroot();item['assemblies']=[dict(a.attrib) for a in x.iter('assembly')]
    records.append(item)
    pathlib.Path('/tmp/story12-2-review5-consumer-q4k0gea1/components.json').write_text(json.dumps(records,indent=2)+'\n')
    print(json.dumps(item),flush=True)
cmd=['dotnet','test','--project','tests/Hexalith.Folders.IntegrationTests','--no-build']
with open('/tmp/story12-2-review5-consumer-q4k0gea1/integration.log','w') as log:
    r=subprocess.run(cmd,cwd=root,stdout=log,stderr=subprocess.STDOUT)
item={'name':'integration','command':cmd,'exit_code':r.returncode,'log':'/tmp/story12-2-review5-consumer-q4k0gea1/integration.log'}
records.append(item)
pathlib.Path('/tmp/story12-2-review5-consumer-q4k0gea1/components.json').write_text(json.dumps(records,indent=2)+'\n')
print(json.dumps(item),flush=True)
raise SystemExit(any(x['exit_code'] for x in records))
