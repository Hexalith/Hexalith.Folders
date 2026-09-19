namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Unrouted PD10 v2 seam that makes authorization-before-observation structural for focused candidate tests.
/// Production route selection remains blocked by A6b, Section 9, and A8.
/// </summary>
internal static class V2ProtectedOperationAuthorizer
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

    /// <summary>Authorizes and, only on success, executes a protected observation.</summary>
    /// <typeparam name="T">Protected result type.</typeparam>
    /// <param name="context">Authorization and scope evidence.</param>
    /// <param name="observeAsync">Protected lookup/read/count/filter/provider/audit/search operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The closed authorization outcome and optional protected value.</returns>
    public static async ValueTask<V2ProtectedReadResult<T>> ExecuteAsync<T>(
        V2AuthorizationContext context,
        Func<CancellationToken, ValueTask<T>> observeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observeAsync);

        V2AuthorizationOutcome outcome = Evaluate(context);
        if (!outcome.Allowed)
        {
            return new(outcome, default);
        }

        T value = await observeAsync(cancellationToken).ConfigureAwait(false);
        return new(outcome, value);
    }

    private static V2AuthorizationOutcome Evaluate(V2AuthorizationContext context)
    {
        if (!context.Authenticated)
        {
            return V2AuthorizationOutcome.AuthenticationFailure();
        }

        if (context.AuthorityEvidence != V2AuthorityEvidenceState.Fresh || context.AccessState == V2AccessState.Stale)
        {
            return V2AuthorizationOutcome.AuthorityUnavailable();
        }

        if (FreshNegativeStates.Contains(context.AccessState)
            || !context.FamilyGrantSatisfied
            || (context.FolderScopeRequired && !context.FolderAuthorityEstablished)
            || (context.TaskBindingRequired && !context.TaskBelongsToFolder))
        {
            return V2AuthorizationOutcome.SafeDenial();
        }

        return V2AuthorizationOutcome.Allow();
    }
}
