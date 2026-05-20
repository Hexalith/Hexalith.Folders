using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

// Test-only command factory that intentionally skips validation so the test suite
// can exercise rejection paths with malformed inputs (invalid metadata, unsafe
// values, malformed identifiers). The shipped sibling factory
// `Hexalith.Folders.Testing.Factories.FolderCreationTestDataFactory.Create(...)`
// applies `FolderCommandValidator.Validate` and throws on unsafe defaults; use that
// helper from external consumers and integration tests, and use this one only from
// negative-control tests inside `tests/`.
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

    public static GrantFolderAccess GrantAccess(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        FolderAccessPrincipalKind principalKind = FolderAccessPrincipalKind.User,
        string principalId = "target-principal-a",
        string action = "read_metadata",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-access-a",
        string? payloadTenantId = null,
        IReadOnlyDictionary<string, string?>? clientTenantIds = null,
        IReadOnlyList<FolderAccessOperation>? operations = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            operations ?? [new FolderAccessOperation(FolderAccessOperationIntent.Grant, principalKind, principalId, action)],
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId,
            clientTenantIds);

    public static RevokeFolderAccess RevokeAccess(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        FolderAccessPrincipalKind principalKind = FolderAccessPrincipalKind.User,
        string principalId = "target-principal-a",
        string action = "read_metadata",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-access-b",
        string? payloadTenantId = null,
        IReadOnlyDictionary<string, string?>? clientTenantIds = null,
        IReadOnlyList<FolderAccessOperation>? operations = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            operations ?? [new FolderAccessOperation(FolderAccessOperationIntent.Revoke, principalKind, principalId, action)],
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId,
            clientTenantIds);

    public static ArchiveFolder Archive(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string archiveReasonCode = "caller_requested",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-archive-a",
        string? payloadTenantId = null,
        IReadOnlyDictionary<string, string?>? clientTenantIds = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            archiveReasonCode,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId,
            clientTenantIds);
}
