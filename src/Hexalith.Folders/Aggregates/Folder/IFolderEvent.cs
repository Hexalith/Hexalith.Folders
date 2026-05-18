namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderEvent
{
    string ManagedTenantId { get; }

    string OrganizationId { get; }

    string FolderId { get; }

    string CorrelationId { get; }

    string TaskId { get; }

    string IdempotencyKey { get; }

    string IdempotencyFingerprint { get; }
}
