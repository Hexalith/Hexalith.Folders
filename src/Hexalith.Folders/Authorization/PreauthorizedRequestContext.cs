using System.Threading;

namespace Hexalith.Folders.Authorization;

/// <summary>
/// Carries an outer authorization decision through an in-process compatibility dispatch so an inner transport
/// adapter cannot repeat authorization with a different action token.
/// </summary>
public static class PreauthorizedRequestContext
{
    private static readonly AsyncLocal<PreauthorizedRequestState?> CurrentState = new();
    private static readonly AsyncLocal<int> ReuseSuppressionDepth = new();

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

    /// <summary>Temporarily disables reuse while a protected mutation obtains fresh evidence.</summary>
    public static IDisposable SuppressReuse()
    {
        ReuseSuppressionDepth.Value++;
        return new ReuseSuppressionScope();
    }

    /// <summary>Returns whether the active decision exactly covers the supplied tenant, principal, and folder scope.</summary>
    public static bool Covers(string? tenantId, string? principalId, string? folderId = null)
    {
        PreauthorizedRequestState? state = CurrentState.Value;
        return ReuseSuppressionDepth.Value == 0
            && state is not null
            && string.Equals(state.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(state.PrincipalId, principalId, StringComparison.Ordinal)
            && (folderId is null || string.Equals(state.FolderId, folderId, StringComparison.Ordinal));
    }

    /// <summary>Returns whether the active decision covers the scope and exact historical action.</summary>
    public static bool Covers(string? tenantId, string? principalId, string? folderId, string actionToken)
        => Covers(tenantId, principalId, folderId)
            && string.Equals(CurrentState.Value?.HistoricalActionToken, actionToken, StringComparison.Ordinal);

    /// <summary>Returns whether a value uses the exact candidate opaque-identifier grammar.</summary>
    public static bool IsCandidateOpaqueIdentifier(string? value)
        => CurrentState.Value is not null
            && value is not null
            && value.Length is >= 16 and <= 128
            && char.IsAsciiLetterOrDigit(value[0])
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private sealed class ReuseSuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReuseSuppressionDepth.Value = Math.Max(0, ReuseSuppressionDepth.Value - 1);
        }
    }
}
