using Hexalith.Folders.AdapterCore;
using Hexalith.Folders.Client;
using Hexalith.Folders.Mcp.Credentials;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Mcp.Composition;

/// <summary>Registers the production MCP HTTP pipeline for the executable and its component tests.</summary>
internal static class FoldersClientRegistration
{
    /// <summary>Registers safe bearer transport without destination-bearing factory logging.</summary>
    public static IHttpClientBuilder Register(IServiceCollection services, Uri baseAddress)
    {
        BearerTransportPolicy.Validate(baseAddress);
        return services
            .AddFoldersClient(options => options.BaseAddress = baseAddress)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(BearerTransportPolicy.CreatePrimaryHandler)
            .AddHttpMessageHandler(static provider => new BearerTokenHandler(provider.GetRequiredService<McpCredentialResolver>()));
    }
}
