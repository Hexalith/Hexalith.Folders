namespace Hexalith.Folders.Aggregates.Folder;

public sealed record ArchiveFolder(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RequestSchemaVersion,
    string ArchiveReasonCode,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?>? ClientTenantIds = null) : IFolderCommand
{
    public string CommandType => nameof(ArchiveFolder);

    public IReadOnlyDictionary<string, string?> ClientControlledTenantIds => ClientTenantIds ?? EmptyClientTenantIds.Value;

    public IFolderCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };

    public ArchiveFolder WithAuthoritativeTenant(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
