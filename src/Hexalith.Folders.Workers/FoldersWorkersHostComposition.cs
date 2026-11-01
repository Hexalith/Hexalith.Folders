using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Workers;

/// <summary>Composes the actual Workers host with shared durable tenant authority.</summary>
public static class FoldersWorkersHostComposition
{
    /// <summary>Registers observability, callbacks, and the EventStore tenant projection store.</summary>
    /// <param name="builder">The Workers application builder.</param>
    public static void AddFoldersWorkersHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddServiceDefaults();
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.AddEventStoreHttpHostSecurity();
        Hexalith.EventStore.ServiceDefaults.Routing.DevelopmentRouteCatalogBootstrap.AddEventStoreDevelopmentRouteCatalogBootstrap(
            builder.Services, builder.Configuration, async (_, token) =>
            {
                var actual = await Hexalith.EventStore.ServiceDefaults.Routing.DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration,
                    builder.Configuration["EventStore:DomainService:AppId"] ?? FoldersWorkersModule.AppId,
                    [], [], [], [], [], token).ConfigureAwait(false);
                var executable = typeof(FoldersWorkersHostComposition).Assembly;
                return actual with { ExecutableOwners = new Dictionary<string, string>
                {
                    [executable.FullName!] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(executable.Location))),
                } };
            });
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();
    }

    /// <summary>Composes the Workers HTTP boundary, channel callbacks and exact probes.</summary>
    /// <param name="app">The built Workers host.</param>
    /// <returns>The same host.</returns>
    public static WebApplication UseFoldersWorkersPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseCloudEvents();
        app.UseAuthentication();
        app.UseEventStoreHttpHostSecurity();
        app.UseAuthorization();
        app.MapSubscribeHandler().RequireEventStoreChannel();
        app.MapFoldersTenantEventWorkerEndpoints();
        app.MapDefaultEndpoints(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);
        return app;
    }
}
