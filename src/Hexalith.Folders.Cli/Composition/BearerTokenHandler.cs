using Hexalith.Folders.AdapterCore;

namespace Hexalith.Folders.Cli.Composition;

/// <summary>Attaches the CLI's resolved token only after validating the actual bearer destination.</summary>
internal sealed class BearerTokenHandler : SafeBearerTokenHandler
{
    /// <summary>Initializes a handler with the CLI's already resolved token.</summary>
    public BearerTokenHandler(string token)
        : base(() => token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
    }
}
