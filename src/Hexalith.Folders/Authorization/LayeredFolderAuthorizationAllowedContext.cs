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
    public string? OrganizationId { get; init; }

    /// <summary>The authenticated principal used for the authorization decision.</summary>
    public string? PrincipalId { get; init; }
}
