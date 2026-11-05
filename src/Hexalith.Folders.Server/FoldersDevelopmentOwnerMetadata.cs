using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server;

/// <summary>Selects the real bound owner metadata path only for an explicitly private source Development proof.</summary>
internal static class FoldersDevelopmentOwnerMetadata
{
    /// <summary>Enables the bound SDK metadata adapter only for the explicit private source Development catalog.</summary>
    /// <param name="builder">The actual Folders host builder.</param>
    internal static void AddFoldersDevelopmentOwnerMetadata(this WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("Folders:EventStore:OwnerMetadataReads:Enabled")) return;
#if DEBUG && FOLDERS_OWNER_METADATA_SOURCE
        if (!builder.Environment.IsDevelopment()
            || string.IsNullOrWhiteSpace(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"]))
            throw new InvalidOperationException("Private owner metadata requires the selected Development catalog.");
        ServiceDescriptor[] registrations = builder.Services.Where(descriptor => descriptor.ServiceType == typeof(INativeSourceMetadataReader)).ToArray();
        if (registrations.Length != 1 || registrations[0].IsKeyedService
            || registrations[0].Lifetime != ServiceLifetime.Transient || registrations[0].ImplementationFactory is not { } factory)
            throw new InvalidOperationException("Private owner metadata requires the existing public SDK reader factory.");
        builder.Services.AddEventStoreGatewayAuthoritativeEventStreamReader("folders");
        builder.Services.Replace(ServiceDescriptor.Describe(typeof(INativeSourceMetadataReader), provider =>
        {
            if (factory(provider) is not GatewayNativeSourceMetadataReader reader)
                throw new InvalidOperationException("Private owner metadata requires the actual public SDK reader.");
            if (!builder.Configuration.GetValue<bool>("Folders:EventStore:OwnerMetadataReads:DiagnosticsEnabled")
                || provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value is not
                    { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true }
                || !provider.GetRequiredService<IHostEnvironment>().IsDevelopment()) return reader;
            try
            {
                return provider.GetService<ILogger<FoldersDevelopmentOwnerMetadataDiagnostics>>() is { } logger
                    ? new FoldersDevelopmentOwnerMetadataDiagnostics(reader, logger) : reader;
            }
            catch { return reader; /* Optional diagnostic service resolution cannot replace the SDK reader. */ }
        }, registrations[0].Lifetime));
        builder.Services.Replace(ServiceDescriptor.Singleton<IOrganizationStreamSequenceReader, GatewayOrganizationStreamSequenceReader>());
#else
        throw new InvalidOperationException("Private owner metadata requires a local Debug source build.");
#endif
    }
}
