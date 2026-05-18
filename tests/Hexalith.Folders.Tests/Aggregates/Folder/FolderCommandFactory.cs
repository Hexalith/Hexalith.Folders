using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal static class FolderCommandFactory
{
    public static CreateFolder Create(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string displayName = "Folder A",
        string? description = "safe description",
        string? pathLabel = "folder-a",
        IReadOnlyList<string>? tags = null,
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            displayName,
            description,
            pathLabel,
            tags ?? ["alpha", "beta"],
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);
}
