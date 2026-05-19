namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsReadModelSnapshot(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    EffectivePermissionsFolderLifecycleState LifecycleState,
    IReadOnlyList<EffectivePermissionEvidenceRow> EvidenceRows,
    EffectivePermissionsFreshness Freshness,
    bool RevocationFreshnessEstablished,
    EffectivePermissionsTaskScope? TaskScope)
{
    public IReadOnlyList<string> DiagnosticSentinels { get; init; } = [];
}
