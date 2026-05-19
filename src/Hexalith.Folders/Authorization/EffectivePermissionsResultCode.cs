namespace Hexalith.Folders.Authorization;

public enum EffectivePermissionsResultCode
{
    Allowed,
    DeniedSafe,
    AuthenticationRequired,
    AuthorizationDenied,
    NotFoundSafe,
    ProjectionStale,
    ReadModelUnavailable,
}
