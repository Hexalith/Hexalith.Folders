namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleEvidenceScope(
    string? ManagedTenantId,
    string? PrincipalId,
    string? ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark);
