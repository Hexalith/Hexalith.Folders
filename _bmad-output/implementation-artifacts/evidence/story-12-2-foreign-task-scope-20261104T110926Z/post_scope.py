"""Close remaining serial current-consumer and package gates after the owned live run."""
from pathlib import Path
import json,subprocess
W=Path(__file__).parent
assert (W/'strict-foreign-scope-3-receipt.json').exists()
root=Path('/home/administrator/projects/hexalith/folders')
steps=[]
for label,project,selection in [
 ('scope-workers-broad','Hexalith.Folders.Workers.Tests',[]),
 ('scope-integration-broad','Hexalith.Folders.IntegrationTests',[]),
 ('scope-gateway-broad','Hexalith.Folders.EventStore.Tests',[]),
 ('scope-contracts-broad','Hexalith.Folders.Contracts.Tests',[]),
 ('scope-client-generation','Hexalith.Folders.Client.Tests',['-class','Hexalith.Folders.Client.Tests.ClientGenerationTests'])]:
 steps.append((label,['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll',*selection,'-result-xml',str(W/(label+'-private.xml'))]))
steps.extend([
 ('scope-release-contract-spine-gate',['pwsh','-NoLogo','-NoProfile','-File','tests/tools/run-contract-spine-gates.ps1']),
 ('scope-post-package-debug-restore',['dotnet','restore','Hexalith.Folders.slnx','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet'])])
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists(),label
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
subprocess.run(['python3',str(W/'audit.py'),'scope-final'],check=True)
print(json.dumps({'serial_post_scope_gates_closed':True}),flush=True)
