using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Metadata-only fold anchor, published in the same SDK batch as its views.</summary>
/// <param name="SchemaVersion">The stored schema version.</param>
/// <param name="ManagedTenantId">The owning tenant.</param>
/// <param name="FolderId">The source aggregate.</param>
/// <param name="Sequence">The contiguous source boundary.</param>
/// <param name="SourceDigest">The ordered source digest.</param>
/// <param name="ObservedAt">The latest event time, never a replay clock.</param>
/// <param name="State">The metadata-only domain fold needed for subsequent delivery.</param>
/// <param name="EventDigests">Exact delivery identity at every retained sequence.</param>
public sealed record OperationalStatusCheckpoint(int SchemaVersion, string ManagedTenantId, string FolderId,
    long Sequence, string SourceDigest, DateTimeOffset ObservedAt, FolderState State,
    IReadOnlyDictionary<long, string> EventDigests)
{
    /// <summary>Gets the unique retained message at each source position.</summary>
    public IReadOnlyDictionary<long, string> MessageIds { get; init; } = new Dictionary<long, string>();

    /// <summary>Gets canonical source-derived evidence for every workspace view.</summary>
    public IReadOnlyDictionary<string, string> WorkspaceEvidence { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets canonical source-derived evidence for every task view.</summary>
    public IReadOnlyDictionary<string, string> TaskEvidence { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets canonical integrity evidence binding the fold and view evidence to the source prefix.</summary>
    public string IntegrityDigest { get; init; } = string.Empty;

    /// <summary>Gets original source positions for irreversible task closure evidence.</summary>
    public IReadOnlyDictionary<string, long> TaskClosedSequences { get; init; } = new Dictionary<string, long>(StringComparer.Ordinal);
}
