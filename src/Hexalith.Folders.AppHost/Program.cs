using Hexalith.Folders.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);
builder.AddProject<Projects.Hexalith_Folders_UI>(FoldersAspireModule.FoldersUiAppId);

builder.Build().Run();
