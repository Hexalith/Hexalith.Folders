using System.Threading;

namespace Hexalith.Folders.Authorization;

/// <summary>
/// Carries an outer authorization decision through an in-process compatibility dispatch so an inner transport
/// adapter cannot repeat authorization with a different action token.
/// </summary>
public static class PreauthorizedRequestContext
{
    private static readonly AsyncLocal<PreauthorizedRequestState?> CurrentState = new();

    /// <summary>Begins a preauthorized compatibility dispatch.</summary>
    public static void Begin(PreauthorizedRequestState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (CurrentState.Value is not null)
        {
            throw new InvalidOperationException("A preauthorized request dispatch is already active.");
        }

        CurrentState.Value = state;
    }

    /// <summary>Ends the current preauthorized compatibility dispatch.</summary>
    public static void End() => CurrentState.Value = null;

    /// <summary>Gets the active state for transport adapters that must project the reused decision.</summary>
    public static PreauthorizedRequestState? Current => CurrentState.Value;

    /// <summary>Returns whether the active decision exactly covers the supplied tenant, principal, and folder scope.</summary>
    public static bool Covers(string? tenantId, string? principalId, string? folderId = null)
    {
        PreauthorizedRequestState? state = CurrentState.Value;
        return state is not null
            && string.Equals(state.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(state.PrincipalId, principalId, StringComparison.Ordinal)
            && (folderId is null || string.Equals(state.FolderId, folderId, StringComparison.Ordinal));
    }
}
