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

    public static CreateRepositoryBackedFolder CreateRepositoryBackedFolder(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string repositoryBindingId = "repository-binding-a",
        string providerBindingRef = "provider-binding-a",
        string repositoryProfileRef = "repository-profile-a",
        string branchRefPolicyRef = "branch-ref-policy-a",
        string folderMetadataDisplayName = "Folder A",
        string credentialScopeClass = "tenant_installation",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-binding-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            repositoryBindingId,
            providerBindingRef,
            repositoryProfileRef,
            branchRefPolicyRef,
            folderMetadataDisplayName,
            credentialScopeClass,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static BindRepository BindRepository(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string repositoryBindingId = "repository-binding-a",
        string providerBindingRef = "provider-binding-a",
        string externalRepositoryRef = "external-repository-a",
        string branchRefPolicyRef = "branch-ref-policy-a",
        string credentialScopeClass = "tenant_installation",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-bind-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            repositoryBindingId,
            providerBindingRef,
            externalRepositoryRef,
            branchRefPolicyRef,
            credentialScopeClass,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static PrepareWorkspace PrepareWorkspace(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string workspaceId = "workspace-a",
        string repositoryBindingId = "repository-binding-a",
        string branchRefPolicyRef = "branch-ref-policy-a",
        string workspacePolicyRef = "workspace-policy-a",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-workspace-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            workspaceId,
            repositoryBindingId,
            branchRefPolicyRef,
            workspacePolicyRef,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static LockWorkspace LockWorkspace(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string workspaceId = "workspace-a",
        string lockIntent = "exclusive_write",
        int requestedLeaseSeconds = 3600,
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-lock-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            workspaceId,
            lockIntent,
            requestedLeaseSeconds,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);

    public static ReleaseWorkspaceLock ReleaseWorkspaceLock(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        string folderId = "folder-a",
        string requestSchemaVersion = "v1",
        string workspaceId = "workspace-a",
        string? lockId = null,
        string? lockOwnershipProof = null,
        string releaseReasonCode = "caller_completed",
        string actorPrincipalId = "principal-a",
        string correlationId = "correlation-release-a",
        string taskId = "task-a",
        string idempotencyKey = "idempotency-release-a",
        string? payloadTenantId = null)
    {
        string resolvedLockId = lockId ?? DefaultLockId();
        string resolvedProof = lockOwnershipProof
            ?? FolderCommandValidator.DeriveWorkspaceLockOwnershipProof(
                managedTenantId,
                folderId,
                workspaceId,
                taskId,
                resolvedLockId);

        return new(
            managedTenantId,
            organizationId,
            folderId,
            requestSchemaVersion,
            workspaceId,
            resolvedLockId,
            resolvedProof,
            releaseReasonCode,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey,
            payloadTenantId);
    }

    public static string DefaultLockId()
    {
        LockWorkspace command = LockWorkspace();
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        return FolderCommandValidator.DeriveWorkspaceLockId(command, validation.IdempotencyFingerprint!);
    }
}
