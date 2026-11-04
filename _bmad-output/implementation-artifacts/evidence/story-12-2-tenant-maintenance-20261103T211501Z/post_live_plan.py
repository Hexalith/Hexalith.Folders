"""Serial unaffected phase continuation; requires the strict run to have closed."""
from pathlib import Path
import json,subprocess
W=Path(__file__).parent
assert (W/'strict-tenant-maintenance-3-receipt.json').exists()
root=Path('/home/administrator/projects/hexalith/folders')
cache=W/'official-fixture-cache'
steps=[
 ('official-published-fixture-materialization',['dotnet','msbuild','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj','-t:CopyPublishedTenantQuerySelectorFixture','-m:1','-nr:false','-v:quiet','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-p:RestorePackagesPath='+str(cache),'-p:NuGetPackageRoot='+str(cache)+'/', '-p:OutDir='+str(root/'references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0')+'/']),
 ('official-published-abi-tests',['dotnet','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll','-class','Hexalith.EventStore.Server.Tests.Routing.PublishedTenantQuerySelectorAbiTests','-result-xml',str(W/'official-published-abi-tests-private.xml')]),
 ('repaired-domain-tests',['dotnet','tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll','-result-xml',str(W/'repaired-domain-tests-private.xml')]),
 ('repaired-server-tests',['dotnet','tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll','-result-xml',str(W/'repaired-server-tests-private.xml')]),
 ('current-release-contract-spine-gate',['pwsh','-NoLogo','-NoProfile','-File','tests/tools/run-contract-spine-gates.ps1']),
 ('post-package-debug-restore',['dotnet','restore','Hexalith.Folders.slnx','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','-nr:false','-v:quiet'])
]
for label,argv in steps:
 assert not (W/(label+'-receipt.json')).exists(),label
 if label=='official-published-abi-tests' and json.loads((W/'official-published-fixture-materialization-receipt.json').read_text())['exit_code']!=0:
  print(json.dumps({'skipped_for_failed_materialization':label}),flush=True);continue
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--',*argv],check=True)
print(json.dumps({'post_live_plan_finished':True}),flush=True)
