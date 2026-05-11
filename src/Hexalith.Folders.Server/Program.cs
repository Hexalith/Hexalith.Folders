using Hexalith.Folders.Contracts;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.MapGet("/", () => $"{FoldersContractMetadata.ModuleName} server scaffold");

app.Run();
