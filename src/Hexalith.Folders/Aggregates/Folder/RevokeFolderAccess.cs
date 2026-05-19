namespace Hexalith.Folders.Aggregates.Folder;

public sealed record RevokeFolderAccess(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    IReadOnlyList<FolderAccessOperation> Operations,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?>? ClientTenantIds = null) : IFolderAccessCommand
{
    public string CommandType => nameof(RevokeFolderAccess);

    public IReadOnlyDictionary<string, string?> ClientControlledTenantIds => ClientTenantIds ?? EmptyClientTenantIds.Value;

    public IFolderCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };

    public IFolderAccessCommand WithAuthoritativeTenant(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
