namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Enforces PD10 authentication, authority freshness, authorization, and optional task binding before observation.
/// </summary>
internal static class Pd10ProtectedOperationExecutor
{
    /// <summary>
    /// Executes a protected observation only after all pre-lookup gates and optional task binding succeed.
    /// </summary>
    /// <typeparam name="T">The protected value type.</typeparam>
    /// <param name="context">Derived authorization facts.</param>
    /// <param name="verifyTaskFolderBinding">Task-to-folder binding verifier, invoked only after parent authorization.</param>
    /// <param name="observeProtectedResource">Protected lookup/read delegate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The closed authorization outcome and optional protected value.</returns>
    public static async ValueTask<Pd10ProtectedOperationResult<T>> ExecuteAsync<T>(
        Pd10AuthorizationContext context,
        Func<CancellationToken, ValueTask<bool>>? verifyTaskFolderBinding,
        Func<CancellationToken, ValueTask<T>> observeProtectedResource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observeProtectedResource);

        Pd10AuthorizationOutcome outcome = Evaluate(context);
        if (!outcome.IsAllowed)
        {
            return new(outcome, default);
        }

        if (context.RequiresTaskFolderBinding)
        {
            ArgumentNullException.ThrowIfNull(verifyTaskFolderBinding);
            bool isBound = await verifyTaskFolderBinding(cancellationToken).ConfigureAwait(false);
            if (!isBound)
            {
                return new(Pd10AuthorizationOutcome.SafeDenial, default);
            }
        }

        T value = await observeProtectedResource(cancellationToken).ConfigureAwait(false);
        return new(Pd10AuthorizationOutcome.Allowed, value);
    }

    private static Pd10AuthorizationOutcome Evaluate(Pd10AuthorizationContext context)
    {
        if (!context.IsAuthenticated)
        {
            return Pd10AuthorizationOutcome.AuthenticationRequired;
        }

        if (context.AuthorityEvidence != Pd10AuthorityEvidenceState.Fresh)
        {
            return Pd10AuthorizationOutcome.AuthorityUnavailable;
        }

        return context.TenantAllowed && context.FolderAllowed && context.FamilyAllowed && context.ScopeAllowed
            ? Pd10AuthorizationOutcome.Allowed
            : Pd10AuthorizationOutcome.SafeDenial;
    }
}
