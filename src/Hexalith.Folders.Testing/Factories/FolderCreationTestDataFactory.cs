using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Testing.Factories;

public static class FolderCreationTestDataFactory
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
    {
        CreateFolder command = new(
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

        EnsureAccepted(command);
        return command;
    }

    public static FolderStreamName FolderStreamName(
        string managedTenantId = "tenant-a",
        string folderId = "folder-a")
        => Aggregates.Folder.FolderStreamName.Create(managedTenantId, folderId);

    private static void EnsureAccepted(CreateFolder command)
    {
        FolderCommandValidationResult result = FolderCommandValidator.Validate(command);
        if (!result.IsAccepted)
        {
            throw new ArgumentException($"Invalid folder creation test command: {result.Code}.", nameof(command));
        }
    }
}
