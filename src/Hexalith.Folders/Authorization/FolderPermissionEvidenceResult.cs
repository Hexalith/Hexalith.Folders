namespace Hexalith.Folders.Authorization;

public sealed record FolderPermissionEvidenceResult(
    FolderPermissionEvidenceStatus Status,
    string OutcomeCode,
    string? FreshnessWatermark,
    string FreshnessClass,
    bool Retryable)
{
    /// <summary>Gets the independently observed native Folder revision when the projection equals the current head.</summary>
    public string? ResourceVersion { get; init; }

    /// <summary>Gets the exact native Folder and Organization permission revision tuple.</summary>
    public string? PermissionVersion { get; init; }

    /// <summary>Gets the native resource observation instant, separate from the policy check.</summary>
    public DateTimeOffset? LastVerifiedAt { get; init; }

    /// <summary>Gets current native Tenant permission revision evidence; local projection age alone never populates it.</summary>
    public string? NativeTenantRevision { get; init; }

    public string? OrganizationId { get; init; }

    public static FolderPermissionEvidenceResult Allowed(
        string? freshnessWatermark,
        string freshnessClass = "fresh",
        string? organizationId = null)
        => new(
            FolderPermissionEvidenceStatus.Allowed,
            LayeredAuthorizationOutcomeCodes.Allowed,
            freshnessWatermark,
            freshnessClass,
            Retryable: false)
        {
            OrganizationId = organizationId,
        };

    public static FolderPermissionEvidenceResult FromStatus(
        FolderPermissionEvidenceStatus status,
        string? freshnessWatermark)
        => status switch
        {
            FolderPermissionEvidenceStatus.Allowed => Allowed(freshnessWatermark),
            FolderPermissionEvidenceStatus.Denied => new(status, LayeredAuthorizationOutcomeCodes.FolderAclDenied, freshnessWatermark, "fresh", Retryable: false),
            FolderPermissionEvidenceStatus.NotFoundSafe => new(status, LayeredAuthorizationOutcomeCodes.SafeNotFound, null, "fresh", Retryable: false),
            FolderPermissionEvidenceStatus.Stale => new(status, LayeredAuthorizationOutcomeCodes.FolderAclStale, freshnessWatermark, "stale", Retryable: false),
            FolderPermissionEvidenceStatus.Unavailable => new(status, LayeredAuthorizationOutcomeCodes.FolderAclUnavailable, null, "unavailable", Retryable: true),
            FolderPermissionEvidenceStatus.Malformed => new(status, LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed, null, "malformed", Retryable: false),
            _ => new(FolderPermissionEvidenceStatus.Malformed, LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed, null, "malformed", Retryable: false),
        };
}
