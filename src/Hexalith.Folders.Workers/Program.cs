using Hexalith.Folders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFoldersTenantAccess();

await builder.Build().RunAsync().ConfigureAwait(false);
