namespace Hexalith.Folders.Aggregates.Folder;

public sealed record CreateRepositoryBackedFolder(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string RepositoryProfileRef,
    string BranchRefPolicyRef,
    string FolderMetadataDisplayName,
    string CredentialScopeClass,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(CreateRepositoryBackedFolder);

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
