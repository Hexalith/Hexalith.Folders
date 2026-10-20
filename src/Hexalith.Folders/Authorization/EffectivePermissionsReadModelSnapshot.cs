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
    /// <summary>Gets the independently observed native Folder revision when the projection equals the current head.</summary>
    public string? ResourceVersion { get; init; }

    /// <summary>Gets the exact native Folder and Organization permission revision tuple.</summary>
    public string? PermissionVersion { get; init; }

    /// <summary>Gets the native resource observation instant, separate from the policy check.</summary>
    public DateTimeOffset? LastVerifiedAt { get; init; }

    /// <summary>Gets current native Tenant permission revision evidence; local projection age alone never populates it.</summary>
    public string? NativeTenantRevision { get; init; }

    public IReadOnlyList<string> DiagnosticSentinels { get; init; } = [];
}
