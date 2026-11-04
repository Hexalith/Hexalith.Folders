from pathlib import Path
import subprocess,json
W=Path(__file__).parent
steps=[
 ('final-cohort-domain-controls','tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll',['-namespace','Hexalith.Folders.Tests.Projections.OperationalStatus','-class','Hexalith.Folders.Tests.Authorization.TenantAccessAuthorizerTests','-class','Hexalith.Folders.Tests.Authorization.LayeredFolderAuthorizationServiceTests','-class','Hexalith.Folders.Tests.Queries.Folders.TaskStatusQueryHandlerTests']),
 ('final-cohort-reader-tests','tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll',['-class','Hexalith.Folders.Server.Tests.FoldersDevelopmentOwnerMetadataTests','-class','Hexalith.Folders.Server.Tests.CurrentSdkSourceHeadBoundaryTests','-class','Hexalith.Folders.Server.Tests.NativeSourceHeadReaderTests']),
 ('final-cohort-fixture-tests','tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll',['-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityTenantMaintenanceTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilitySourceReadConfigurationTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityBootstrapEvidenceTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityGatewayScopeTests','-class','Hexalith.Folders.AppHost.Tests.FolderDurabilityBootstrapProjectionTests'])
]
for label,dll,selection in steps:
 assert not (W/(label+'-receipt.json')).exists()
 subprocess.run(['python3',str(W/'qualify.py'),'run',label,'--','dotnet',dll,*selection,'-result-xml',str(W/(label+'-private.xml'))],check=True)
print(json.dumps({'final_controls_finished':True}),flush=True)
