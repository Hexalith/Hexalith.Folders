using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

public sealed class ScopedLayeredFolderAuthorizationResultAccessor : ILayeredFolderAuthorizationResultAccessor
{
    public LayeredFolderAuthorizationResult? Current { get; private set; }

    public void BeginScope(LayeredFolderAuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (Current is not null)
        {
            // Nested scope = programming bug. Throwing here means the inner caller does not
            // silently overwrite the outer scope's authorization context (which would leave
            // the matching EndScope to clear evidence for both callers).
            throw new InvalidOperationException(
                "Scope already active. ScopedLayeredFolderAuthorizationResultAccessor does not support nested BeginScope.");
        }

        Current = result;
    }

    public void EndScope()
    {
        if (Current is null)
        {
            // Unmatched EndScope = programming bug. Throwing surfaces the mismatch at the
            // bug site rather than silently masking a try/finally that never began a scope.
            throw new InvalidOperationException(
                "EndScope called without a matching BeginScope.");
        }

        Current = null;
    }
}
