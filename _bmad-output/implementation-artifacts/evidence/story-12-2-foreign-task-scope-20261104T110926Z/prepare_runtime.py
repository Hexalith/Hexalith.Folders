"""Run serial current-cohort qualification only after implementation output release."""
from pathlib import Path
import subprocess,json
W=Path(__file__).parent
assert (W/'handoff.json').exists(),'Explicit completed implementation handoff required'
steps=[
 ('scope-runtime-restore',['dotnet','restore','Hexalith.Folders.slnx','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet']),
 ('scope-runtime-build',['dotnet','build','Hexalith.Folders.slnx','-c','Debug','--no-restore','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet']),
 ('scope-memories-build',['dotnet','build','references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj','-c','Debug','--no-restore','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet'])]
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists(),label
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
 assert json.loads((W/(label+'-receipt.json')).read_text())['exit_code']==0,label
subprocess.run(['python3',str(W/'audit.py'),'scope-runtime'],check=True)
for label,dll,classes in [
 ('scope-reader-tests','tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll',['FoldersDevelopmentOwnerMetadataTests','CurrentSdkSourceHeadBoundaryTests','NativeSourceHeadReaderTests']),
 ('scope-fixture-tests','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll',['FolderDurabilityTenantMaintenanceTests','FolderDurabilitySourceReadConfigurationTests','FolderDurabilityBootstrapEvidenceTests','FolderDurabilityGatewayScopeTests','FolderDurabilityBootstrapProjectionTests'])]:
 assert not (W/(label+'-receipt.json')).exists(),label
 ns='Hexalith.Folders.Server.Tests.' if label=='scope-reader-tests' else 'Hexalith.Folders.AppHost.Tests.'
 selection=[v for cls in classes for v in ['-class',ns+cls]]
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--','dotnet',dll,*selection,'-result-xml',str(W/(label+'-private.xml'))],check=True)
 d=json.loads((W/(label+'-receipt.json')).read_text());assert d['exit_code']==0 and sum(int(a['passed']) for a in d['summary']['assemblies'])>0,label
print(json.dumps({'current_scope_runtime_qualified':True}),flush=True)
