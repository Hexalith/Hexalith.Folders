using System.Reflection;

using Hexalith.Folders.Client;
using Hexalith.Folders.Mcp.Composition;
using Hexalith.Folders.Mcp.Configuration;
using Hexalith.Folders.Mcp.Credentials;
using Hexalith.Folders.Mcp.Tooling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Logging to stderr only — stdout is reserved for the MCP JSON-RPC protocol channel.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Resolve the transport endpoint (config error → fail fast at startup; the tool-call path then always has
// a valid client). Token resolution is intentionally NOT fail-fast: per AC #7 a missing token surfaces as
// a per-tool credential_missing failure rather than a startup crash.
string? baseAddress = Environment.GetEnvironmentVariable("HEXALITH_FOLDERS_BASE_ADDRESS")
    ?? builder.Configuration[$"{FoldersMcpOptions.SectionName}:baseAddress"];

if (string.IsNullOrWhiteSpace(baseAddress)
    || !Uri.TryCreate(baseAddress, UriKind.Absolute, out Uri? baseUri)
    || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
{
    await Console.Error.WriteLineAsync(
        "Error: a valid absolute Folders base address is required.\n"
        + "Set HEXALITH_FOLDERS_BASE_ADDRESS (e.g., https://folders.internal/) or folders:baseAddress.")
        .ConfigureAwait(false);
    return 1;
}

// Auth config values feed the resolver, which layers HEXALITH_TOKEN / HEXALITH_FOLDERS_AUTH_TOKENFILE on top.
FoldersMcpAuthOptions auth = new()
{
    Token = builder.Configuration[$"{FoldersMcpOptions.SectionName}:auth:token"],
    TokenFile = builder.Configuration[$"{FoldersMcpOptions.SectionName}:auth:tokenFile"],
};
builder.Services.AddSingleton(new McpCredentialResolver(auth));

// Typed SDK client over the configured endpoint, with the bearer token attached by a DelegatingHandler
// (the module never owns auth — Story 5.1/5.2 pattern). Dependency direction stays MCP → Client → Contracts.
builder.Services
    .AddFoldersClient(options => options.BaseAddress = baseUri)
    .AddHttpMessageHandler(static serviceProvider => new BearerTokenHandler(serviceProvider.GetRequiredService<McpCredentialResolver>()));

builder.Services.AddTransient<ToolPipeline>();

string version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "0.0.0";

builder.Services
    .AddMcpServer(options => options.ServerInfo = new()
    {
        Name = "hexalith-folders",
        Version = version,
        Description = "Hexalith Folders MCP server — tools and resources for the canonical folder lifecycle, wrapping the Folders SDK with cross-adapter behavioral parity (idempotency/correlation/task-id sourcing, canonical failure kinds, metadata-only output).",
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;
