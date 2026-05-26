namespace Hexalith.Folders.Aggregates.Folder;

public sealed record BindRepository(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string RepositoryBindingId,
    string ProviderBindingRef,
    string ExternalRepositoryRef,
    string BranchRefPolicyRef,
    string CredentialScopeClass,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(BindRepository);

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
