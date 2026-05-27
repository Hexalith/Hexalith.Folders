namespace Hexalith.Folders.Aggregates.Folder;

public sealed record PrepareWorkspace(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string WorkspaceId,
    string RepositoryBindingId,
    string BranchRefPolicyRef,
    string WorkspacePolicyRef,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(PrepareWorkspace);

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
