namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsQueryResult(
    EffectivePermissionsResultCode Code,
    string? FolderId,
    IReadOnlyList<EffectivePermissionLevel> Permissions,
    string AuthorizationOutcome,
    EffectivePermissionsFreshness Freshness,
    string? CorrelationId,
    string OperationId,
    string? TaskContextId,
    string? WorkspaceContextId);
