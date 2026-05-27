namespace Hexalith.Folders.Aggregates.Folder;

public sealed record ConfigureBranchRefPolicy(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string RepositoryBindingId,
    string PolicyRef,
    string DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string>? ProtectedRefPatterns,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(ConfigureBranchRefPolicy);

    public IFolderCommand WithManagedTenantId(string managedTenantId)
        => this with { ManagedTenantId = managedTenantId };
}
