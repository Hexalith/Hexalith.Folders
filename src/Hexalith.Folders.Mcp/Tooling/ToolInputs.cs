using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Mcp.Tooling;

/// <summary>
/// Shared translations from raw MCP tool-input strings to typed SDK inputs. Keeps query option handling
/// uniform across tools (no new request fields are introduced; these only map spine-defined inputs).
/// </summary>
internal static class ToolInputs
{
    /// <summary>Maps a freshness tool-input value to the typed read-consistency class.</summary>
    /// <param name="freshness">The raw freshness value (<c>snapshot_per_task</c>, <c>read_your_writes</c>, <c>eventually_consistent</c>), or <see langword="null"/>.</param>
    /// <returns>The mapped class, or <see langword="null"/> when unspecified or unrecognized.</returns>
    public static ReadConsistencyClass? ParseFreshness(string? freshness) => freshness switch
    {
        "snapshot_per_task" => ReadConsistencyClass.Snapshot_per_task,
        "read_your_writes" => ReadConsistencyClass.Read_your_writes,
        "eventually_consistent" => ReadConsistencyClass.Eventually_consistent,
        _ => null,
    };
}
