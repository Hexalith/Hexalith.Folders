using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Authorization;

/// <summary>Latest folder-specific grant or revocation for one principal and action.</summary>
public sealed record FolderPermissionOverrideEntry(
    FolderAccessPrincipalKind PrincipalKind,
    string PrincipalId,
    string Action,
    bool IsGranted,
    long AccessSequence,
    DateTimeOffset OccurredAt);
