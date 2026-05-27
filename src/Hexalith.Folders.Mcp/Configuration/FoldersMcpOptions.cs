namespace Hexalith.Folders.Mcp.Configuration;

/// <summary>
/// Server-side configuration for the Folders MCP adapter. Bound from the <c>folders</c> configuration
/// section (with environment-variable bindings) at startup. Carries the transport endpoint and the two
/// token-sourcing inputs; it never holds tenant authority and is never echoed to any output channel.
/// </summary>
/// <remarks>
/// The MCP server is a thin adapter over <c>Hexalith.Folders.Client</c>: it introduces no request fields,
/// lifecycle states, or error categories beyond the Contract Spine. These options only select the wire
/// endpoint and how the bearer token is sourced (Adapter Parity Contract credential dimension, AC #7).
/// </remarks>
public sealed class FoldersMcpOptions
{
    /// <summary>The configuration section bound to these options.</summary>
    public const string SectionName = "folders";

    /// <summary>
    /// Gets or sets the absolute base address of the Folders server. Sourced from
    /// <c>folders:baseAddress</c> or the <c>HEXALITH_FOLDERS_BASE_ADDRESS</c> environment variable.
    /// </summary>
    public string? BaseAddress { get; set; }

    /// <summary>Gets or sets the authentication sub-options.</summary>
    public FoldersMcpAuthOptions Auth { get; set; } = new();
}

/// <summary>
/// Authentication inputs for the Folders MCP adapter. Exactly one of <see cref="Token"/> (inline) or
/// <see cref="TokenFile"/> (path to a file containing the token) is expected; both have an
/// environment-variable binding. When neither resolves to a non-blank token, tool calls fail before any
/// HTTP call with failure <c>kind = "credential_missing"</c>.
/// </summary>
public sealed class FoldersMcpAuthOptions
{
    /// <summary>
    /// Gets or sets the inline bearer token. Sourced from <c>folders:auth:token</c> or the
    /// <c>HEXALITH_TOKEN</c> environment variable. Never logged or echoed.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the path to a file whose contents are the bearer token. Sourced from
    /// <c>folders:auth:tokenFile</c> or the <c>HEXALITH_FOLDERS_AUTH_TOKENFILE</c> environment variable.
    /// The path and the file contents are never logged or echoed.
    /// </summary>
    public string? TokenFile { get; set; }
}
