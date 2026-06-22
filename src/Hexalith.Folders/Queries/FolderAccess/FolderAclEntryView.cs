namespace Hexalith.Folders.Queries.FolderAccess;

/// <summary>
/// Metadata-only view of a single folder ACL entry, shaped for the canonical REST contract.
/// </summary>
/// <param name="AclEntryId">Deterministic opaque ACL entry id.</param>
/// <param name="SubjectRef">Subject reference (<c>{kind}:{principalId}</c>).</param>
/// <param name="PermissionLevel">Coarse permission level (read/write/administer).</param>
/// <param name="Effect">Effect (grant/revoke).</param>
public sealed record FolderAclEntryView(
    string AclEntryId,
    string SubjectRef,
    string PermissionLevel,
    string Effect);
