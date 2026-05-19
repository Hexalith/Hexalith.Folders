namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsReadModelRequest(
    string ManagedTenantId,
    string FolderId,
    IReadOnlyList<EffectivePermissionPrincipal> PrincipalScopes,
    string? TaskContextId,
    string? WorkspaceContextId,
    string ReadConsistency);
