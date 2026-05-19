using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Testing.Factories;

/// <summary>
/// Shipped folder-creation test data factory. Every command returned from
/// <see cref="Create"/> has been run through <see cref="FolderCommandValidator.Validate"/>,
/// so unsafe defaults (forbidden metadata terms, invalid identifiers, oversize tag
/// collections) raise <see cref="ArgumentException"/>. Use this helper from external
/// consumer tests and integration suites that need a known-valid command.
/// </summary>
/// <remarks>
/// The in-tree negative-control factory in
/// <c>tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs</c>
/// intentionally skips validation so rejection-path tests can submit malformed inputs.
/// </remarks>
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
            // Result-code-only error message; the offending command field could contain
            // unsafe bytes (that is exactly why the validator rejected) and must not be
            // echoed into the exception message.
            throw new ArgumentException(
                $"Invalid folder creation test command: {result.Code}.",
                nameof(command));
        }
    }
}
