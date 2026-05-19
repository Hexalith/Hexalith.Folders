namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsQuery(
    string FolderId,
    string? AuthoritativeTenantId,
    string PrincipalId,
    string? CorrelationId,
    string? TaskContextId = null,
    string? WorkspaceContextId = null,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantIds = null);
