using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Streams;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Composes real public SDK transports around the existing counted component HTTP terminal.</summary>
internal sealed class NativeSequenceReaderTestTransport : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>Creates the SDK-owned Dapr routing handler with its exact configured API token.</summary>
    internal NativeSequenceReaderTestTransport(HttpMessageHandler terminal, string? token = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("native-reader-component", client => client.BaseAddress = new Uri("http://localhost:3500"))
            .AddEventStoreDaprServiceInvocation("eventstore", token).ConfigurePrimaryHttpMessageHandler(() => terminal);
        _provider = services.BuildServiceProvider();
        Client = _provider.GetRequiredService<IHttpClientFactory>().CreateClient("native-reader-component");
    }

    /// <summary>Gets the isolated client configured through the supported public SDK.</summary>
    internal HttpClient Client { get; }

    /// <inheritdoc/>
    public void Dispose() { Client.Dispose(); _provider.Dispose(); }

    /// <summary>Creates the real metadata reader; credentials are explicit component transport inputs only.</summary>
    internal static INativeSourceMetadataReader Reader(HttpClient client)
    {
        var assertions = SourceHeadBoundaryProxy.Create<IEventStoreGatewayWorkloadAssertionSource>((_, _) => ValueTask.FromResult<string?>("component-assertion"));
        var proofs = SourceHeadBoundaryProxy.Create<IEventStoreGatewayStreamReadProofSource>((_, _) => ValueTask.FromResult<string?>("component-resource-proof"));
        return new GatewayNativeSourceMetadataReader(new EventStoreGatewayClient(client,
            Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }), assertions, proofs), TimeProvider.System);
    }
}
