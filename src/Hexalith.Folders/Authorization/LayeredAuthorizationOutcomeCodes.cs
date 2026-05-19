namespace Hexalith.Folders.Authorization;

public static class LayeredAuthorizationOutcomeCodes
{
    public const string Allowed = "allowed";
    public const string AuthenticationDenied = "authentication_denied";
    public const string ClaimTransformDenied = "claim_transform_denied";
    public const string TenantAccessDenied = "tenant_access_denied";
    public const string TenantProjectionStale = "tenant_projection_stale";
    public const string TenantProjectionUnavailable = "tenant_projection_unavailable";
    public const string FolderAclDenied = "folder_acl_denied";
    public const string FolderAclStale = "folder_acl_stale";
    public const string FolderAclUnavailable = "folder_acl_unavailable";
    public const string EventStoreValidatorDenied = "eventstore_validator_denied";
    public const string DaprPolicyDenied = "dapr_policy_denied";
    public const string AuthorizationEvidenceMalformed = "authorization_evidence_malformed";
    public const string SafeNotFound = "safe_not_found";
}
