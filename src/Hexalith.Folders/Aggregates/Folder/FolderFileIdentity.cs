namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Event-folded stable owner identity and explicit Projects binding; the path digest is never an identity or exposed by the owner read.</summary>
/// <param name="OwnerFileId">The owner-allocated opaque File identity.</param>
/// <param name="ProjectFileReferenceId">The separately supplied Projects-owned reference.</param>
/// <param name="PathMetadataDigest">The last accepted descriptive policy digest; it does not identify the target.</param>
/// <param name="Active">Whether this incarnation remains live under its exact Folder.</param>
public sealed record FolderFileIdentity(string OwnerFileId, string? ProjectFileReferenceId, string PathMetadataDigest, bool Active)
{
    /// <summary>Gets the last exact native accepted mutation identity, never a locally allocated revision.</summary>
    public string? NativeMutationId { get; init; }

    /// <summary>Gets the exact native mutation address digest; it is never a public File identity or owner authority.</summary>
    public string? MutationTargetDigest { get; init; }
}
