using Hexalith.Folders.Contracts;
using Hexalith.Folders.Server;
using Hexalith.Folders.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddFoldersServer();

WebApplication app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapFoldersServerEndpoints();

app.Run();
