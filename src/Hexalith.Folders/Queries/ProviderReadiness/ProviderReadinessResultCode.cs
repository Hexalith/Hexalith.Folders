namespace Hexalith.Folders.Queries.ProviderReadiness;

public enum ProviderReadinessResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    ValidationFailed,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
}
