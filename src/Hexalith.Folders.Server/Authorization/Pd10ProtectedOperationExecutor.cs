namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Enforces PD10 authentication, authority freshness, authorization, and optional task binding before observation.
/// </summary>
internal static class Pd10ProtectedOperationExecutor
{
    private static readonly HashSet<V2AccessState> FreshNegativeStates =
    [
        V2AccessState.WrongTenant,
        V2AccessState.Revoked,
        V2AccessState.Disabled,
        V2AccessState.Unknown,
        V2AccessState.HiddenResource,
        V2AccessState.AbsentResource,
        V2AccessState.InsufficientScope,
    ];

    private static readonly HashSet<V2AccessState> PositiveStates =
    [
        V2AccessState.TenantAdministrator,
        V2AccessState.TenantMember,
        V2AccessState.DelegatedServiceAgent,
        V2AccessState.TenantScopedOperator,
        V2AccessState.AuditReviewer,
        V2AccessState.IncidentAdministrator,
    ];

    /// <summary>
    /// Executes a protected observation only after all pre-lookup gates and optional task binding succeed.
    /// </summary>
    /// <typeparam name="T">The protected value type.</typeparam>
    /// <param name="context">Derived authorization facts.</param>
    /// <param name="validateRequestEnvelope">Optional envelope validator, invoked only after authorization and before task binding or observation.</param>
    /// <param name="verifyTaskFolderBinding">Task-to-folder binding verifier, invoked only after parent authorization.</param>
    /// <param name="observeProtectedResource">Protected lookup/read delegate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The closed authorization outcome and optional protected value.</returns>
    public static async ValueTask<Pd10ProtectedOperationResult<T>> ExecuteAsync<T>(
        Pd10AuthorizationContext context,
        Func<CancellationToken, ValueTask<bool>>? validateRequestEnvelope,
        Func<CancellationToken, ValueTask<Pd10TaskFolderBindingState>>? verifyTaskFolderBinding,
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

        if (validateRequestEnvelope is not null
            && !await validateRequestEnvelope(cancellationToken).ConfigureAwait(false))
        {
            return new(Pd10AuthorizationOutcome.Allowed, default);
        }

        if (context.RequiresTaskFolderBinding)
        {
            ArgumentNullException.ThrowIfNull(verifyTaskFolderBinding);
            Pd10TaskFolderBindingState binding = await verifyTaskFolderBinding(cancellationToken).ConfigureAwait(false);
            if (binding == Pd10TaskFolderBindingState.Unavailable)
            {
                return new(Pd10AuthorizationOutcome.AuthorityUnavailable, default);
            }

            if (binding != Pd10TaskFolderBindingState.Bound)
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

        if (context.AuthorityEvidence != Pd10AuthorityEvidenceState.Fresh
            || context.AccessState == V2AccessState.Stale
            || (!FreshNegativeStates.Contains(context.AccessState) && !PositiveStates.Contains(context.AccessState)))
        {
            return Pd10AuthorizationOutcome.AuthorityUnavailable;
        }

        if (FreshNegativeStates.Contains(context.AccessState))
        {
            return Pd10AuthorizationOutcome.SafeDenial;
        }

        return context.TenantAllowed
            && context.FolderAllowed
            && context.FamilyAllowed
            && context.ScopeAllowed
            && context.DelegationAllowed
            ? Pd10AuthorizationOutcome.Allowed
            : Pd10AuthorizationOutcome.SafeDenial;
    }
}
