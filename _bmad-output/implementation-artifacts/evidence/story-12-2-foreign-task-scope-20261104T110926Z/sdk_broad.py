"""Requalify SDK owner broad tests with the unchanged official published ABI fixture."""
from pathlib import Path
import hashlib,json,subprocess
W=Path(__file__).parent
root=Path('/home/administrator/projects/hexalith/folders')
cache=Path('/tmp/story12-2-tenant-maintenance-20261103-updu69yz/official-fixture-cache')
archive=cache/'hexalith.tenants.contracts/7.0.0/hexalith.tenants.contracts.7.0.0.nupkg'
assert hashlib.sha256(archive.read_bytes()).hexdigest()=='e21f5ac7b5f332f1921f70a249ab2032b9978d56ffdd6641a0a4b94d04850bdc'
steps=[
 ('scope-official-abi-materialization',['dotnet','msbuild','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','-t:CopyPublishedTenantQuerySelectorFixture','-m:1','-nr:false','-v:quiet','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-p:RestorePackagesPath='+str(cache),'-p:NuGetPackageRoot='+str(cache)+'/', '-p:OutDir='+str(root/'references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0')+'/']),
 ('scope-sdk-server-broad',['dotnet','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll','-result-xml',str(W/'scope-sdk-server-broad-private.xml')])]
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists()
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
 if label=='scope-official-abi-materialization':assert json.loads((W/(label+'-receipt.json')).read_text())['exit_code']==0
print(json.dumps({'sdk_broad_closed':True}),flush=True)
