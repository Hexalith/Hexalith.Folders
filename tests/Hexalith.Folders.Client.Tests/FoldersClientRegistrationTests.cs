using System;
using System.Net.Http;

using Hexalith.Folders.Client;
using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

// Disambiguate the generated type from the "Hexalith.Folders.Client" namespace.
using GeneratedFoldersClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Client.Tests;

public sealed class FoldersClientRegistrationTests
{
    private static readonly Uri BaseAddress = new("https://folders.example/");

    [Fact]
    public void AddFoldersClientResolvesTypedClient()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersClient(options => options.BaseAddress = BaseAddress);

        using ServiceProvider provider = services.BuildServiceProvider();

        IClient client = provider.GetRequiredService<IClient>();
        client.ShouldBeOfType<GeneratedFoldersClient>();
    }

    [Fact]
    public void AddFoldersClientAppliesConfiguredBaseAddress()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersClient(options => options.BaseAddress = BaseAddress);

        using ServiceProvider provider = services.BuildServiceProvider();

        // AddHttpClient<IClient, Client> names the logical client after the service type ("IClient").
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient httpClient = factory.CreateClient(nameof(IClient));

        httpClient.BaseAddress.ShouldBe(BaseAddress);
    }

    [Fact]
    public void AddFoldersClientWithoutBaseAddressFailsValidationOnResolve()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersClient(static _ => { });

        using ServiceProvider provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IClient>());
    }

    [Fact]
    public void AddFoldersClientWithRelativeBaseAddressFailsValidationOnResolve()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersClient(options => options.BaseAddress = new Uri("/api/v1", UriKind.Relative));

        using ServiceProvider provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IClient>());
    }
}
