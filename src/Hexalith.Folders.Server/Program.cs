using Hexalith.Folders;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Server;
using Hexalith.Folders.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddFoldersServer();

// TODO Epic 7: replace AddInMemoryFolderRepository with an EventStore-backed repository
// before production deployment. The in-memory implementation loses all events on process
// restart and is only safe for dev hosts and integration tests.
builder.Services.AddInMemoryFolderRepository();

WebApplication app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapFoldersServerEndpoints();

app.Run();
