using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
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
        const string clientName = "folders-bound-owner-metadata";
        builder.Services.AddHttpClient(clientName, http =>
        {
            string endpoint = builder.Configuration["DAPR_HTTP_ENDPOINT"]
                ?? "http://127.0.0.1:" + builder.Configuration["DAPR_HTTP_PORT"];
            http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/v1.0/invoke/eventstore/method/");
            http.Timeout = TimeSpan.FromSeconds(3);
        });
        builder.Services.AddSingleton<INativeSourceMetadataReader>(provider =>
        {
            INativeSourceMetadataReader reader = new BoundOwnerNativeSourceMetadataReader(
                provider.GetRequiredService<IHttpClientFactory>(), clientName, provider.GetRequiredService<IWorkloadAssertionIssuer>(),
                "folders", builder.Configuration["Authentication:BoundStreamRead:PrivateKey"]
                    ?? throw new InvalidOperationException("The owner proof key is missing."), ["folders"], TimeProvider.System);
            return builder.Configuration.GetValue<bool>("Folders:EventStore:OwnerMetadataReads:DiagnosticsEnabled")
                && provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value is
                    { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true }
                && provider.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? new FoldersDevelopmentOwnerMetadataDiagnostics(reader,
                    provider.GetRequiredService<ILogger<FoldersDevelopmentOwnerMetadataDiagnostics>>()) : reader;
        });
        builder.Services.Replace(ServiceDescriptor.Singleton<IOrganizationStreamSequenceReader, GatewayOrganizationStreamSequenceReader>());
#else
        throw new InvalidOperationException("Private owner metadata requires a local Debug source build.");
#endif
    }
}
