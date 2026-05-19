namespace Hexalith.Folders.Authorization;

public enum EffectivePermissionsTaskScopeStatus
{
    Available,
    OutsideTenant,
    OutsideFolder,
    Unauthorized,
    Stale,
    Unavailable,
}
