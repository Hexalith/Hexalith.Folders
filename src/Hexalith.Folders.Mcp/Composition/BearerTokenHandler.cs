using Hexalith.Folders.AdapterCore;
using Hexalith.Folders.Mcp.Credentials;

namespace Hexalith.Folders.Mcp.Composition;

/// <summary>Validates each bearer destination before lazily resolving the MCP token.</summary>
internal sealed class BearerTokenHandler : SafeBearerTokenHandler
{
    /// <summary>Initializes a handler with the configured MCP credential policy.</summary>
    public BearerTokenHandler(McpCredentialResolver credentials)
        : base((credentials ?? throw new ArgumentNullException(nameof(credentials))).ResolveToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
    }
}
