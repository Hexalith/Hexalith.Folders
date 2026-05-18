namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderCommand
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string FolderId { get; }

    string ActorPrincipalId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string? PayloadTenantId { get; }

    string CommandType { get; }

    IFolderCommand WithManagedTenantId(string managedTenantId);
}
