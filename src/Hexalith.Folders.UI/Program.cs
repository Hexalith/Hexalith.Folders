using Hexalith.Folders.Client;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();
app.MapGet("/", () => $"{FoldersClientModule.Name} read-only console scaffold");
app.Run();
