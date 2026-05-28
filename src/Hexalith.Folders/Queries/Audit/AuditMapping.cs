using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Audit;

internal static class AuditMapping
{
    public static AuditQueryResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => AuditQueryResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.TenantAccessDenied => AuditQueryResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.FolderAclDenied or LayeredAuthorizationOutcomeCodes.SafeNotFound => AuditQueryResultCode.FolderAclDenied,
            LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => AuditQueryResultCode.AuditAccessDenied,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => AuditQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => AuditQueryResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied => AuditQueryResultCode.AuditAccessDenied,
            _ => AuditQueryResultCode.ReadModelUnavailable,
        };
}
