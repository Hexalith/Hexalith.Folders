namespace Hexalith.Folders.Queries.ProviderReadiness;

public enum ProviderSupportEvidenceQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    AuthorizationDenied,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
    ProviderUnavailable,
}
