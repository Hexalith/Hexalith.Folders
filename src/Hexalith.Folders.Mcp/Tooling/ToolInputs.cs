using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Client.Serialization;

namespace Hexalith.Folders.Mcp.Tooling;

/// <summary>
/// Shared translations from raw MCP tool-input strings to typed SDK inputs. Keeps query option handling
/// uniform across tools (no new request fields are introduced; these only map spine-defined inputs).
/// </summary>
internal static class ToolInputs
{
    /// <summary>Maps a freshness value only when it is accepted by the selected generated operation.</summary>
    public static ReadConsistencyClass? ParseFreshness(string? freshness, string operationId)
    {
        if (OperationFreshness.TryParse(operationId, freshness, out ReadConsistencyClass? parsed))
        {
            return parsed;
        }

        throw new McpUsageException("The supplied freshness value is not accepted by this operation.");
    }
}
