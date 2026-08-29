using System.Collections.Frozen;

namespace Hexalith.Folders.Authorization;

/// <summary>
/// Represents the actions that a task context may retain from effective folder permissions.
/// </summary>
/// <param name="Status">The availability and authorization status of the task scope.</param>
/// <param name="OpaqueTaskId">The opaque task identifier associated with the scope.</param>
/// <param name="OpaqueWorkspaceId">The optional opaque workspace identifier associated with the scope.</param>
/// <param name="AllowedActions">The actions allowed by the scope, matched with ordinal semantics.</param>
public sealed record EffectivePermissionsTaskScope(
    EffectivePermissionsTaskScopeStatus Status,
    string? OpaqueTaskId,
    string? OpaqueWorkspaceId,
    IReadOnlySet<string> AllowedActions)
{
    private readonly IReadOnlySet<string> _allowedActions = Freeze(AllowedActions);

    /// <summary>
    /// Gets an immutable snapshot of task-scoped actions using ordinal membership.
    /// </summary>
    public IReadOnlySet<string> AllowedActions
    {
        get => _allowedActions;
        init => _allowedActions = Freeze(value);
    }

    private static IReadOnlySet<string> Freeze(IReadOnlySet<string> allowedActions)
    {
        ArgumentNullException.ThrowIfNull(allowedActions);
        return allowedActions.ToFrozenSet(StringComparer.Ordinal);
    }
}
