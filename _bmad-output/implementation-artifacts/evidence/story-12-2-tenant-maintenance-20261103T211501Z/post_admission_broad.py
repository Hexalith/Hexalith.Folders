"""Serial broad rechecks for changed catalog acquisition; run only after the live fixture closes."""
from pathlib import Path
import subprocess,json
W=Path(__file__).parent;root=Path('/home/administrator/projects/hexalith/folders')
assert (W/'strict-tenant-maintenance-4-receipt.json').exists(),'Parent live ownership has not been released'
cache=W/'official-fixture-cache'
steps=[('admission-official-published-fixture',['dotnet','msbuild','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','-t:CopyPublishedTenantQuerySelectorFixture','-m:1','-nr:false','-v:quiet','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-p:RestorePackagesPath='+str(cache),'-p:NuGetPackageRoot='+str(cache)+'/', '-p:OutDir='+str(root/'references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0')+'/'])]
for label,dll,extra in [
 ('admission-sdk-server-tests','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll',[]),
 ('admission-domain-tests','tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll',[]),
 ('admission-server-tests','tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll',[]),
 ('admission-workers-tests','tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests.dll',[]),
 ('admission-integration-tests','tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll',[]),
 ('admission-gateway-tests','tests/Hexalith.Folders.EventStore.Tests/bin/Debug/net10.0/Hexalith.Folders.EventStore.Tests.dll',[]),
 ('admission-contracts-tests','tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll',[]),
 ('admission-client-generation-tests','tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll',['-class','Hexalith.Folders.Client.Tests.ClientGenerationTests'])]:
 steps.append((label,['dotnet',dll,*extra,'-result-xml',str(W/(label+'-private.xml'))]))
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists(),label
 if label=='admission-sdk-server-tests':assert json.loads((W/'admission-official-published-fixture-receipt.json').read_text())['exit_code']==0
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
print(json.dumps({'post_admission_broad_finished':True}),flush=True)
