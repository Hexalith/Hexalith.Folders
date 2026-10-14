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
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();
    }
}
