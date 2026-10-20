namespace Hexalith.Folders.Authorization;

public sealed record LayeredFolderAuthorizationAllowedContext(
    string AuthoritativeTenantId,
    string ActorSafeIdentifier,
    string ActionToken,
    string? OperationScope,
    string? CorrelationId,
    string? TaskId,
    string? FreshnessWatermark,
    IReadOnlyList<AuthorizationLayer> PolicyLayers)
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

    /// <summary>The authenticated principal used for the authorization decision.</summary>
    public string? PrincipalId { get; init; }
}
