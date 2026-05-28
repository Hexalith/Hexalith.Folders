namespace Hexalith.Folders.Queries.Audit;

public enum AuditQueryResultCode
{
    Allowed,
    AuthenticationRequired,
    TenantAccessDenied,
    FolderAclDenied,
    AuditAccessDenied,
    NotFoundSafe,
    ValidationError,
    ProjectionStale,
    ProjectionUnavailable,
    ReadModelUnavailable,
    Redacted,
    InternalError,
}
