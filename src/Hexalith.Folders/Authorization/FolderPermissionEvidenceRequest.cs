namespace Hexalith.Folders.Authorization;

public sealed record FolderPermissionEvidenceRequest(
    string ManagedTenantId,
    string PrincipalId,
    string ActorSafeIdentifier,
    string ActionToken,
    string? OperationScope,
    string? CorrelationId,
    string? TaskId,
    FolderOperationPolicyClass OperationPolicyClass,
    bool AllowBoundedStale);
