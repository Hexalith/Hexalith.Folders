using System.Threading;

namespace Hexalith.Folders.Authorization;

/// <summary>
/// Carries one freshly completed request-handler initial authorization into the immediately
/// following domain-processor service call so the identical pre-I/O check is not duplicated
/// within the same <c>/process</c> turn. Consumed at most once and invalidated on reauthorization.
/// </summary>
internal static class ProcessInitialAuthorizationScope
{
    private static readonly AsyncLocal<ScopeState?> CurrentState = new();
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    /// <summary>Enters a single-use initial authorization scope for the active <c>/process</c> turn.</summary>
    /// <param name="context">The exact authorization context already evaluated by the request handler.</param>
    /// <param name="result">The allowed authorization result produced for <paramref name="context"/>.</param>
    /// <returns>A disposable scope that clears any remaining state on exit.</returns>
    public static IDisposable Enter(LayeredFolderAuthorizationContext context, LayeredFolderAuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsAllowed || result.AllowedContext is null)
        {
            throw new InvalidOperationException("Only an allowed authorization result can enter an initial process scope.");
        }

        ScopeState? prior = CurrentState.Value;
        ScopeState state = new(context, result);
        CurrentState.Value = state;
        return new ScopeLease(prior, state);
    }

    /// <summary>Suppresses reuse for the duration of a fresh reauthorization or strict read check.</summary>
    /// <returns>A disposable scope restoring the prior suppression depth.</returns>
    public static IDisposable Suppress()
    {
        SuppressionDepth.Value++;
        return new SuppressionLease();
    }

    /// <summary>
    /// Permanently invalidates any unconsumed initial authorization in the current async flow so
    /// post-I/O or post-reauthorization calls can never fall back to a pre-I/O authorization result.
    /// </summary>
    public static void InvalidateCurrent()
    {
        ScopeState? state = CurrentState.Value;
        if (state is not null)
        {
            _ = state.TryTake();
            CurrentState.Value = null;
        }
    }

    /// <summary>
    /// Atomically consumes the single-use authorization result when it exactly matches the
    /// supplied mutation context.
    /// </summary>
    /// <param name="context">The downstream service's initial authorization context.</param>
    /// <param name="result">The consumed allowed result when the scope matches.</param>
    /// <returns><c>true</c> if the scope matched and was consumed; otherwise <c>false</c>.</returns>
    public static bool TryConsume(LayeredFolderAuthorizationContext context, out LayeredFolderAuthorizationResult? result)
    {
        ArgumentNullException.ThrowIfNull(context);
        result = null;
        if (SuppressionDepth.Value > 0)
        {
            return false;
        }

        ScopeState? state = CurrentState.Value;
        if (state is null || !state.Matches(context))
        {
            return false;
        }

        if (!state.TryTake())
        {
            return false;
        }

        CurrentState.Value = null;
        result = state.Result;
        return true;
    }

    private static string SafeActor(LayeredFolderAuthorizationContext context)
        => string.IsNullOrWhiteSpace(context.ActorSafeIdentifier)
            ? (context.PrincipalId ?? string.Empty).Trim()
            : context.ActorSafeIdentifier.Trim();

    private sealed class ScopeState(LayeredFolderAuthorizationContext context, LayeredFolderAuthorizationResult result)
    {
        private int _consumed;

        public LayeredFolderAuthorizationResult Result { get; } = result;

        public bool Matches(LayeredFolderAuthorizationContext candidate)
            => Volatile.Read(ref _consumed) == 0
                && string.Equals(context.AuthoritativeTenantId?.Trim(), candidate.AuthoritativeTenantId?.Trim(), StringComparison.Ordinal)
                && string.Equals(context.PrincipalId?.Trim(), candidate.PrincipalId?.Trim(), StringComparison.Ordinal)
                && string.Equals(SafeActor(context), SafeActor(candidate), StringComparison.Ordinal)
                && string.Equals(context.ActionToken, candidate.ActionToken, StringComparison.Ordinal)
                && string.Equals(context.OperationScope, candidate.OperationScope, StringComparison.Ordinal)
                && string.Equals(context.CorrelationId, candidate.CorrelationId, StringComparison.Ordinal)
                && string.Equals(context.TaskId, candidate.TaskId, StringComparison.Ordinal)
                && context.OperationPolicy == candidate.OperationPolicy;

        public bool TryTake() => Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;
    }

    private sealed class ScopeLease(ScopeState? prior, ScopeState owned) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _ = owned.TryTake();
            if (ReferenceEquals(CurrentState.Value, owned))
            {
                CurrentState.Value = prior;
            }
        }
    }

    private sealed class SuppressionLease : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SuppressionDepth.Value = Math.Max(0, SuppressionDepth.Value - 1);
        }
    }
}
