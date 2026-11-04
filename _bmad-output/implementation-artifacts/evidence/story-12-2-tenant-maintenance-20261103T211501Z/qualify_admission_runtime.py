"""Parent-only serial rebuild after explicit agent source/output release."""
from pathlib import Path
import subprocess,json
W=Path(__file__).parent
assert Path('/tmp/story12-2-admission-rebase-20261103/handoff.json').exists(),'Agent handoff required; parent must also confirm output release.'
steps=[
 ('admission-runtime-restore',['dotnet','restore','Hexalith.Folders.slnx','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet']),
 ('admission-runtime-build',['dotnet','build','Hexalith.Folders.slnx','-c','Debug','--no-restore','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet']),
 ('admission-memories-build',['dotnet','build','references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj','-c','Debug','--no-restore','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet'])
]
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists(),label
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
 assert json.loads((W/(label+'-receipt.json')).read_text())['exit_code']==0,label
subprocess.run(['python3',str(W/'audit.py'),'admission-runtime'],check=True)
for label,dll,classes in [
 ('admission-reader-tests','tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll',['FoldersDevelopmentOwnerMetadataTests','CurrentSdkSourceHeadBoundaryTests','NativeSourceHeadReaderTests']),
 ('admission-fixture-tests','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll',['FolderDurabilityTenantMaintenanceTests','FolderDurabilitySourceReadConfigurationTests','FolderDurabilityBootstrapEvidenceTests','FolderDurabilityGatewayScopeTests','FolderDurabilityBootstrapProjectionTests'])]:
 assert not (W/(label+'-receipt.json')).exists(),label
 namespace='Hexalith.Folders.Server.Tests.' if label=='admission-reader-tests' else 'Hexalith.Folders.AppHost.Tests.'
 selectors=[argument for cls in classes for argument in ['-class',namespace+cls]]
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--','dotnet',dll,*selectors,'-result-xml',str(W/(label+'-private.xml'))],check=True)
 d=json.loads((W/(label+'-receipt.json')).read_text());assert d['exit_code']==0 and sum(int(a['passed']) for a in d['summary']['assemblies'])>0,label
print(json.dumps({'admission_runtime_and_controls_qualified':True}),flush=True)
