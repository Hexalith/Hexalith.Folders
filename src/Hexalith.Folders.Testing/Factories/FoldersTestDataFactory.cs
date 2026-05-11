namespace Hexalith.Folders.Testing.Factories;

public static class FoldersTestDataFactory
{
    public static TestFolderContext FolderContext(TestFolderContextOverrides? overrides = null)
    {
        overrides ??= new TestFolderContextOverrides();

        string managedTenantId = overrides.ManagedTenantId ?? $"tenant-{Guid.NewGuid():N}";
        string organizationId = overrides.OrganizationId ?? Guid.NewGuid().ToString("N");
        string folderId = overrides.FolderId ?? Guid.NewGuid().ToString("N");
        string taskId = overrides.TaskId ?? Guid.NewGuid().ToString("N");
        string correlationId = overrides.CorrelationId ?? Guid.NewGuid().ToString("N");
        string idempotencyKey = overrides.IdempotencyKey ?? Guid.NewGuid().ToString("N");

        return new TestFolderContext(
            managedTenantId,
            organizationId,
            folderId,
            taskId,
            correlationId,
            idempotencyKey);
    }

    public static TestAuthorizationContext AuthorizationContext(TestAuthorizationContextOverrides? overrides = null)
    {
        overrides ??= new TestAuthorizationContextOverrides();

        string managedTenantId = overrides.ManagedTenantId ?? $"tenant-{Guid.NewGuid():N}";
        string subject = overrides.Subject ?? $"subject-{Guid.NewGuid():N}";
        IReadOnlyList<string> permissions = overrides.Permissions ?? ["folders:*"];

        return new TestAuthorizationContext(subject, managedTenantId, permissions);
    }
}
