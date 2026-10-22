using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

public sealed record WorkspaceFileContextItem(
    PathMetadata? Path,
    string Kind,
    long? ByteLength,
    string Sensitivity,
    string Redaction)
{
    /// <summary>Gets the opaque owner file incarnation for subsequent fenced changes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; init; }
    /// <summary>Gets the immutable authoritative incarnation version.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? Version { get; init; }
}
