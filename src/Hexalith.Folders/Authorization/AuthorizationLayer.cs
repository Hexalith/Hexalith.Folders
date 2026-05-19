namespace Hexalith.Folders.Authorization;

public enum AuthorizationLayer
{
    JwtValidation,
    EventStoreClaimTransform,
    TenantAccessFreshness,
    FolderAcl,
    EventStoreValidator,
    DaprDenyByDefaultPolicy,
}
