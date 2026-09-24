using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Authorization;

public sealed class LayeredFolderAuthorizationService(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IFolderPermissionEvidenceProvider folderPermissionEvidenceProvider,
    IEventStoreAuthorizationValidator eventStoreAuthorizationValidator,
    IDaprPolicyEvidenceProvider daprPolicyEvidenceProvider,
    IUtcClock clock)
{
    private const string UnknownFreshness = "unknown";
    private const string NotRecordedTimingBucket = "not_recorded";

    public Task<LayeredFolderAuthorizationResult> AuthorizeAsync(
        LayeredFolderAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => AuthorizeCoreAsync(context, includeFolderAcl: true, cancellationToken);

    /// <summary>Runs the complete authorization stack for an operation that has no existing folder ACL scope.</summary>
    internal Task<LayeredFolderAuthorizationResult> AuthorizeTenantScopedAsync(
        LayeredFolderAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => AuthorizeCoreAsync(context, includeFolderAcl: false, cancellationToken);

    private async Task<LayeredFolderAuthorizationResult> AuthorizeCoreAsync(
        LayeredFolderAuthorizationContext context,
        bool includeFolderAcl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AuthorizationLayer> evaluatedLayers = [];
        string actorSafeIdentifier = SafeActorIdentifier(context);

        PreauthorizedRequestState? preauthorized = PreauthorizedRequestContext.Current;
        if (preauthorized is not null && PreauthorizedRequestContext.Covers(
                context.AuthoritativeTenantId,
                context.PrincipalId,
                context.OperationScope,
                context.ActionToken))
        {
            LayeredFolderAuthorizationAllowedContext allowedContext = new(
                context.AuthoritativeTenantId!,
                actorSafeIdentifier,
                context.ActionToken,
                context.OperationScope,
                context.CorrelationId,
                context.TaskId,
                FreshnessWatermark: preauthorized.FreshnessWatermark,
                AuthorizationOrder.LayeredFolderAuthorization)
            {
                OrganizationId = preauthorized.OrganizationId,
                PrincipalId = context.PrincipalId,
            };
            LayeredFolderAuthorizationDecisionSnapshot reusedDecision = Snapshot(
                AuthorizationLayer.JwtValidation,
                LayeredAuthorizationOutcomeCodes.Allowed,
                retryable: false,
                freshnessClass: "fresh",
                freshnessWatermark: preauthorized.FreshnessWatermark,
                context,
                actorSafeIdentifier);
            return new(true, reusedDecision, allowedContext, [AuthorizationLayer.JwtValidation]);
        }

        evaluatedLayers.Add(AuthorizationLayer.JwtValidation);
        if (string.IsNullOrWhiteSpace(context.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(context.PrincipalId))
        {
            return Deny(
                AuthorizationLayer.JwtValidation,
                LayeredAuthorizationOutcomeCodes.AuthenticationDenied,
                context,
                actorSafeIdentifier,
                evaluatedLayers);
        }

        if (IsReservedTenant(context.AuthoritativeTenantId))
        {
            return Deny(
                AuthorizationLayer.JwtValidation,
                LayeredAuthorizationOutcomeCodes.AuthenticationDenied,
                context,
                actorSafeIdentifier,
                evaluatedLayers);
        }

        evaluatedLayers.Add(AuthorizationLayer.EventStoreClaimTransform);
        if (HasClientControlledMismatch(context.AuthoritativeTenantId, context.ClientControlledTenantValues)
            || HasClientControlledMismatch(context.PrincipalId, context.ClientControlledPrincipalValues)
            || !IsClaimTransformEvidenceValid(context))
        {
            return Deny(
                AuthorizationLayer.EventStoreClaimTransform,
                context.ClaimTransformEvidence.Malformed
                    ? LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                    : LayeredAuthorizationOutcomeCodes.ClaimTransformDenied,
                context,
                actorSafeIdentifier,
                evaluatedLayers);
        }

        evaluatedLayers.Add(AuthorizationLayer.TenantAccessFreshness);
        string authoritativeTenantId = context.AuthoritativeTenantId.Trim();
        TenantAccessAuthorizationContext tenantAccessContext = new(
            authoritativeTenantId,
            context.PrincipalId,
            RequestedTenantId: authoritativeTenantId);
        TenantAccessAuthorizationResult tenantAccess = context.OperationPolicy.AllowBoundedStaleTenantProjection
            ? await tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(tenantAccessContext, cancellationToken).ConfigureAwait(false)
            : await tenantAccessAuthorizer.AuthorizeMutationAsync(tenantAccessContext, cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return Deny(
                AuthorizationLayer.TenantAccessFreshness,
                MapTenantAccessOutcome(tenantAccess.Outcome),
                context,
                actorSafeIdentifier,
                evaluatedLayers,
                freshnessClass: MapFreshness(tenantAccess.FreshnessStatus),
                freshnessWatermark: tenantAccess.ProjectionWatermark,
                retryable: tenantAccess.Outcome is TenantAccessOutcome.StaleProjection
                    or TenantAccessOutcome.UnavailableProjection);
        }

        if (tenantAccess.TenantId is not null
            && !string.Equals(tenantAccess.TenantId.Trim(), authoritativeTenantId, StringComparison.Ordinal))
        {
            return Deny(
                AuthorizationLayer.TenantAccessFreshness,
                LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
                context,
                actorSafeIdentifier,
                evaluatedLayers,
                freshnessClass: "malformed");
        }

        string managedTenantId = authoritativeTenantId;

        string? folderWatermark = tenantAccess.ProjectionWatermark;
        string folderFreshnessClass = MapFreshness(tenantAccess.FreshnessStatus);
        string? organizationId = null;
        if (includeFolderAcl)
        {
            evaluatedLayers.Add(AuthorizationLayer.FolderAcl);
            if (!EffectivePermissionsActionCatalog.IsSupported(context.ActionToken))
            {
                return Deny(
                    AuthorizationLayer.FolderAcl,
                    LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
                    context,
                    actorSafeIdentifier,
                    evaluatedLayers,
                    freshnessClass: "malformed");
            }

            FolderPermissionEvidenceResult folderEvidence;
            try
            {
                folderEvidence = await folderPermissionEvidenceProvider.GetEvidenceAsync(
                    new FolderPermissionEvidenceRequest(
                        managedTenantId,
                        context.PrincipalId,
                        actorSafeIdentifier,
                        context.ActionToken,
                        context.OperationScope,
                        context.CorrelationId,
                        context.TaskId,
                        context.OperationPolicy.PolicyClass,
                        context.OperationPolicy.AllowBoundedStaleFolderPermission),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                folderEvidence = FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null);
            }

            if (folderEvidence.Status != FolderPermissionEvidenceStatus.Allowed)
            {
                return Deny(
                    AuthorizationLayer.FolderAcl,
                    folderEvidence.OutcomeCode,
                    context,
                    actorSafeIdentifier,
                    evaluatedLayers,
                    folderEvidence.FreshnessClass,
                    folderEvidence.FreshnessWatermark,
                    folderEvidence.Retryable);
            }

            folderWatermark = folderEvidence.FreshnessWatermark ?? tenantAccess.ProjectionWatermark;
            folderFreshnessClass = folderEvidence.FreshnessClass;
            organizationId = folderEvidence.OrganizationId;
        }

        LayeredFolderAuthorizationAllowedContext validatorContext = new(
            managedTenantId,
            actorSafeIdentifier,
            context.ActionToken,
            context.OperationScope,
            context.CorrelationId,
            context.TaskId,
            folderWatermark,
            AuthorizationOrder.LayeredFolderAuthorization)
        {
            PrincipalId = context.PrincipalId,
        };

        evaluatedLayers.Add(AuthorizationLayer.EventStoreValidator);
        EventStoreAuthorizationValidationResult validatorResult;
        try
        {
            validatorResult = await eventStoreAuthorizationValidator.ValidateAsync(
                new EventStoreAuthorizationValidationRequest(validatorContext, context.OperationPolicy.PolicyClassCode),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            validatorResult = EventStoreAuthorizationValidationResult.Unavailable();
        }

        if (validatorResult.Status != EventStoreAuthorizationValidationStatus.Allowed)
        {
            return Deny(
                AuthorizationLayer.EventStoreValidator,
                validatorResult.OutcomeCode,
                context,
                actorSafeIdentifier,
                evaluatedLayers,
                validatorResult.FreshnessClass,
                validatorResult.FreshnessWatermark,
                validatorResult.Retryable);
        }

        string? watermarkAfterValidator = folderWatermark ?? validatorResult.FreshnessWatermark;
        string freshnessClassAfterValidator = validatorResult.FreshnessWatermark is null
            ? folderFreshnessClass
            : MergeFreshnessClass(folderFreshnessClass, validatorResult.FreshnessClass);

        evaluatedLayers.Add(AuthorizationLayer.DaprDenyByDefaultPolicy);
        DaprPolicyEvidenceResult daprEvidence;
        try
        {
            daprEvidence = await daprPolicyEvidenceProvider.GetEvidenceAsync(
                new DaprPolicyEvidenceRequest(
                    context.OperationPolicy.DaprTargetAppId,
                    context.OperationPolicy.ServiceInvocationClass,
                    context.OperationPolicy.RequiresDaprPolicyEvidence,
                    context.CorrelationId,
                    context.TaskId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            daprEvidence = DaprPolicyEvidenceResult.Unavailable("dapr_policy_unavailable");
        }

        if (daprEvidence.Status != DaprPolicyEvidenceStatus.Allowed)
        {
            return Deny(
                AuthorizationLayer.DaprDenyByDefaultPolicy,
                daprEvidence.OutcomeCode,
                context,
                actorSafeIdentifier,
                evaluatedLayers,
                daprEvidence.FreshnessClass,
                daprEvidence.FreshnessWatermark,
                daprEvidence.Retryable);
        }

        string? finalWatermark = watermarkAfterValidator ?? daprEvidence.FreshnessWatermark;
        string finalFreshnessClass = daprEvidence.FreshnessWatermark is null
            ? freshnessClassAfterValidator
            : MergeFreshnessClass(freshnessClassAfterValidator, daprEvidence.FreshnessClass);

        LayeredFolderAuthorizationAllowedContext safeContext = validatorContext with
        {
            FreshnessWatermark = finalWatermark,
            OrganizationId = organizationId,
        };

        LayeredFolderAuthorizationDecisionSnapshot decision = Snapshot(
            AuthorizationLayer.DaprDenyByDefaultPolicy,
            LayeredAuthorizationOutcomeCodes.Allowed,
            retryable: false,
            finalFreshnessClass,
            finalWatermark,
            context,
            actorSafeIdentifier);

        return new LayeredFolderAuthorizationResult(
            IsAllowed: true,
            decision,
            safeContext,
            evaluatedLayers.ToArray());
    }

    /// <summary>Obtains fresh candidate-action evidence immediately before a task mutation effect.</summary>
    public async Task<LayeredFolderAuthorizationResult> ReauthorizeTaskMutationAsync(
        LayeredFolderAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => await ReauthorizeMutationAsync(context, includeFolderAcl: true, cancellationToken).ConfigureAwait(false);

    /// <summary>Obtains fresh candidate-action evidence immediately before a gateway mutation effect.</summary>
    internal async Task<LayeredFolderAuthorizationResult> ReauthorizeMutationAsync(
        LayeredFolderAuthorizationContext context,
        bool includeFolderAcl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        LayeredFolderAuthorizationContext freshContext = includeFolderAcl
            ? context
            : context with { OperationScope = null };
        PreauthorizedRequestState? preauthorized = PreauthorizedRequestContext.Current;
        if (preauthorized is null)
        {
            return await AuthorizeCoreAsync(freshContext, includeFolderAcl, cancellationToken).ConfigureAwait(false);
        }

        if (!PreauthorizedRequestContext.Covers(
                freshContext.AuthoritativeTenantId,
                freshContext.PrincipalId,
                freshContext.OperationScope,
                freshContext.ActionToken))
        {
            return LayeredFolderAuthorizationResult.Denied(
                Snapshot(
                    AuthorizationLayer.EventStoreClaimTransform,
                    LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
                    retryable: false,
                    freshnessClass: "malformed",
                    freshnessWatermark: null,
                    freshContext,
                    SafeActorIdentifier(freshContext)),
                [AuthorizationLayer.JwtValidation, AuthorizationLayer.EventStoreClaimTransform]);
        }

        LayeredFolderAuthorizationContext actorContext = freshContext with
        {
            ActionToken = preauthorized.CandidateActionToken,
            ClaimTransformEvidence = EventStoreClaimTransformEvidence.Allowed(
                preauthorized.TenantId,
                preauthorized.PrincipalId,
                [preauthorized.CandidateActionToken]),
        };

        using IDisposable suppression = PreauthorizedRequestContext.SuppressReuse();
        LayeredFolderAuthorizationResult actor = await AuthorizeCoreAsync(
            actorContext,
            includeFolderAcl,
            cancellationToken).ConfigureAwait(false);
        if (!actor.IsAllowed || actor.AllowedContext is null || preauthorized.DelegatorPrincipalId is null)
        {
            return actor;
        }

        LayeredFolderAuthorizationContext delegatorContext = actorContext with
        {
            PrincipalId = preauthorized.DelegatorPrincipalId,
            ActorSafeIdentifier = preauthorized.PrincipalId,
            ClaimTransformEvidence = EventStoreClaimTransformEvidence.Allowed(
                preauthorized.TenantId,
                preauthorized.DelegatorPrincipalId,
                [preauthorized.CandidateActionToken]),
            ClientControlledPrincipalValues = null,
        };
        LayeredFolderAuthorizationResult delegator = await AuthorizeCoreAsync(
            delegatorContext,
            includeFolderAcl,
            cancellationToken).ConfigureAwait(false);
        return delegator.IsAllowed ? actor : delegator;
    }

    private static string MergeFreshnessClass(string current, string incoming)
    {
        if (string.Equals(current, "fresh", StringComparison.Ordinal))
        {
            return incoming;
        }

        if (string.Equals(incoming, "fresh", StringComparison.Ordinal))
        {
            return current;
        }

        return current;
    }

    private LayeredFolderAuthorizationResult Deny(
        AuthorizationLayer terminalLayer,
        string outcomeCode,
        LayeredFolderAuthorizationContext context,
        string actorSafeIdentifier,
        IReadOnlyList<AuthorizationLayer> evaluatedLayers,
        string freshnessClass = UnknownFreshness,
        string? freshnessWatermark = null,
        bool retryable = false)
        => LayeredFolderAuthorizationResult.Denied(
            Snapshot(
                terminalLayer,
                outcomeCode,
                retryable,
                freshnessClass,
                freshnessWatermark,
                context,
                actorSafeIdentifier),
            evaluatedLayers.ToArray());

    private LayeredFolderAuthorizationDecisionSnapshot Snapshot(
        AuthorizationLayer terminalLayer,
        string outcomeCode,
        bool retryable,
        string freshnessClass,
        string? freshnessWatermark,
        LayeredFolderAuthorizationContext context,
        string actorSafeIdentifier)
        => new(
            terminalLayer,
            outcomeCode,
            retryable,
            freshnessClass,
            freshnessWatermark,
            context.CorrelationId,
            context.TaskId,
            actorSafeIdentifier,
            context.OperationPolicy.PolicyClassCode,
            NotRecordedTimingBucket,
            clock.UtcNow);

    private static bool IsClaimTransformEvidenceValid(LayeredFolderAuthorizationContext context)
    {
        EventStoreClaimTransformEvidence evidence = context.ClaimTransformEvidence;
        return evidence.IsPresent
            && !evidence.Malformed
            && string.Equals(evidence.TenantId?.Trim(), context.AuthoritativeTenantId?.Trim(), StringComparison.Ordinal)
            && string.Equals(evidence.PrincipalId?.Trim(), context.PrincipalId?.Trim(), StringComparison.Ordinal)
            && evidence.HasPermissionFor(context.ActionToken);
    }

    private static bool HasClientControlledMismatch(
        string? authoritativeValue,
        IReadOnlyDictionary<string, string?>? comparisonValues)
    {
        if (comparisonValues is null || comparisonValues.Count == 0)
        {
            return false;
        }

        string authoritative = (authoritativeValue ?? string.Empty).Trim();
        string? firstObserved = null;
        foreach (KeyValuePair<string, string?> entry in comparisonValues)
        {
            if (entry.Value is null)
            {
                continue;
            }

            string value = entry.Value.Trim();
            if (value.Length == 0)
            {
                return true;
            }

            if (!string.Equals(value, authoritative, StringComparison.Ordinal))
            {
                return true;
            }

            if (firstObserved is not null && !string.Equals(value, firstObserved, StringComparison.Ordinal))
            {
                return true;
            }

            firstObserved ??= value;
        }

        return false;
    }

    private static string SafeActorIdentifier(LayeredFolderAuthorizationContext context)
        => string.IsNullOrWhiteSpace(context.ActorSafeIdentifier)
            ? "actor_present"
            : context.ActorSafeIdentifier.Trim();

    private static bool IsReservedTenant(string? tenantId)
        => string.Equals(tenantId?.Trim(), "system", StringComparison.OrdinalIgnoreCase);

    private static string MapTenantAccessOutcome(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Allowed => LayeredAuthorizationOutcomeCodes.Allowed,
            TenantAccessOutcome.StaleProjection => LayeredAuthorizationOutcomeCodes.TenantProjectionStale,
            TenantAccessOutcome.UnavailableProjection => LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable,
            TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed,
            TenantAccessOutcome.UnknownTenant => LayeredAuthorizationOutcomeCodes.SafeNotFound,
            _ => LayeredAuthorizationOutcomeCodes.TenantAccessDenied,
        };

    private static string MapFreshness(TenantProjectionFreshnessStatus freshnessStatus)
        => freshnessStatus switch
        {
            TenantProjectionFreshnessStatus.Fresh => "fresh",
            TenantProjectionFreshnessStatus.Stale => "stale",
            TenantProjectionFreshnessStatus.Unavailable => "unavailable",
            TenantProjectionFreshnessStatus.Future => "malformed",
            _ => UnknownFreshness,
        };
}
