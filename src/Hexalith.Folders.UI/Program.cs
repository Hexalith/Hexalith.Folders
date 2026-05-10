using Hexalith.Folders.Client;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => FoldersClientModule.Name);

WebApplication app = builder.Build();
app.MapGet("/", (string moduleName) => $"{moduleName} read-only console scaffold");
app.Run();
