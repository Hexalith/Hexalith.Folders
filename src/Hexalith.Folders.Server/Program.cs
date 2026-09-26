using Hexalith.Folders.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddFoldersServerHost();

WebApplication app = builder.Build();
app.UseFoldersServerPipeline();

app.Run();
