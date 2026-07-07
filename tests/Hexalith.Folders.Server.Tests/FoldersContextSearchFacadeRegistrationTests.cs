using Hexalith.Folders.Server;
using Hexalith.Memories.Client.Rest;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Story 10.5 egress correction (2026-07-07, bmad-correct-course): the context-search facade routes the Memories
/// search egress through THIS app's Dapr sidecar service-invocation API, so the production deny-by-default
/// <c>folders -&gt; memories</c> invoke allow-rule + mTLS are the operative network-layer control. These hermetic
/// tests pin the composed base address so the egress cannot silently regress to a direct, Dapr-bypassing URL
/// (which would over-claim Dapr enforcement).
/// </summary>
public sealed class FoldersContextSearchFacadeRegistrationTests
{
    private static MemoriesClientOptions ResolveOptions(params (string Key, string Value)[] settings)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(static s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton(configuration)
            .AddFoldersContextSearchFacade()
            .BuildServiceProvider();

        return provider.GetRequiredService<IOptions<MemoriesClientOptions>>().Value;
    }

    [Fact]
    public void ComposesDaprSidecarInvokeEndpointFromDaprHttpPort()
        => ResolveOptions(("DAPR_HTTP_PORT", "3555")).Endpoint
            .ShouldBe(new Uri("http://localhost:3555/v1.0/invoke/memories/method/"));

    [Fact]
    public void PrefersDaprHttpEndpointWhenPresent()
        => ResolveOptions(("DAPR_HTTP_ENDPOINT", "http://127.0.0.1:3600")).Endpoint
            .ShouldBe(new Uri("http://127.0.0.1:3600/v1.0/invoke/memories/method/"));

    [Fact]
    public void DefaultsToDapr3500InvokeEndpointWhenNoDaprConfigPresent()
        => ResolveOptions().Endpoint
            .ShouldBe(new Uri("http://localhost:3500/v1.0/invoke/memories/method/"));

    [Fact]
    public void ExplicitMemoriesBaseAddressOverrideWinsAsDirectUrl()
        => ResolveOptions(
                ("Memories:BaseAddress", "https://memories.example.test/"),
                ("DAPR_HTTP_PORT", "3555")).Endpoint
            .ShouldBe(new Uri("https://memories.example.test/"));
}
