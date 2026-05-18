namespace Hexalith.Folders.Aggregates.Folder;

public sealed record CreateFolder(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId) : IFolderCommand
{
    public string CommandType => nameof(CreateFolder);

    public IFolderCommand WithManagedTenantId(string managedTenantId) => this with { ManagedTenantId = managedTenantId };
}
