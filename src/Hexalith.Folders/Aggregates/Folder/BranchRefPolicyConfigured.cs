namespace Hexalith.Folders.Aggregates.Folder;

public sealed record BranchRefPolicyConfigured(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string RepositoryBindingId,
    string PolicyRef,
    string DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string> ProtectedRefPatterns,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset OccurredAt) : IFolderEvent;
