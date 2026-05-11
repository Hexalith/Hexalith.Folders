namespace Hexalith.Folders.Testing.Factories;

public sealed record TestFolderContext(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string TaskId,
    string CorrelationId,
    string IdempotencyKey)
{
    public string FolderStreamName => $"{ManagedTenantId}:folders:{FolderId}";

    public string OrganizationStreamName => $"{ManagedTenantId}:organizations:{OrganizationId}";
}
