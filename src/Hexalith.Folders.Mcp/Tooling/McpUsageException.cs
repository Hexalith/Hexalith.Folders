using System;

namespace Hexalith.Folders.Mcp.Tooling;

/// <summary>
/// Signals a pre-SDK usage error raised while preparing a tool call (for example, a malformed request-body
/// JSON payload) before any HTTP call is made. The tool pipeline maps it to failure
/// <c>kind = "usage_error"</c>. The message is metadata-only and never echoes caller content.
/// </summary>
internal sealed class McpUsageException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="McpUsageException"/> class.</summary>
    /// <param name="message">A metadata-only message describing the usage error.</param>
    public McpUsageException(string message)
        : base(message)
    {
    }
}
