namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionsTaskScope(
    EffectivePermissionsTaskScopeStatus Status,
    string? OpaqueTaskId,
    string? OpaqueWorkspaceId,
    IReadOnlySet<string> AllowedActions);
