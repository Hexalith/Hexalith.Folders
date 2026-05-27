namespace Hexalith.Folders.Aggregates.Folder;

public sealed record ReleaseWorkspaceLock(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string WorkspaceId,
    string LockId,
    string LockOwnershipProof,
    string ReleaseReasonCode,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(ReleaseWorkspaceLock);

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
