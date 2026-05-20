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
}
