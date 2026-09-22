using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Claims;
using System.Net.Http.Headers;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

/// <summary>Provides the isolated, opt-in PD10 v2 candidate compatibility pipeline.</summary>
public static class Pd10V2CandidateCompatibilitySeam
{
    private const string CandidatePrefix = "/api/v2";
    private const long MaximumRequestBodyBytes = 1_048_576;
    private const string DelegatorClaimType = "eventstore:delegator";
    private const string DelegatorPermissionClaimType = "eventstore:delegator-permission";
    private const string AccessStateClaimType = "eventstore:access-state";
    private const string AuthorizedTenantItem = "pd10.authorized-tenant";
    private const string AuthorizedPrincipalItem = "pd10.authorized-principal";
    private const string AuthorizedFolderItem = "pd10.authorized-folder";
    private const string AuthorizedWatermarkItem = "pd10.authorized-watermark";
    private const string AuthorizedOrganizationItem = "pd10.authorized-organization";
    private const string AuthorizedDelegatorItem = "pd10.authorized-delegator";
    private const string TaskSnapshotItem = "pd10.task-snapshot";
    private static readonly HashSet<string> TaskLifecycleStates = new(StringComparer.Ordinal)
    {
        "requested", "preparing", "ready", "locked", "changes_staged", "dirty", "committed", "failed",
        "inaccessible", "unknown_provider_outcome", "reconciliation_required",
    };
    private static readonly HashSet<string> TaskErrorCategories = new(StringComparer.Ordinal)
    {
        "success", "authentication_failure", "client_configuration_error", "credential_missing",
        "credential_reference_invalid", "tenant_access_denied", "validation_error", "concurrency_conflict",
        "idempotency_conflict", "idempotency_key_expired", "idempotency_admission_unavailable", "provider_readiness_failed",
        "provider_permission_insufficient", "provider_unavailable", "provider_rate_limited",
        "repository_binding_unavailable", "branch_ref_policy_invalid", "workspace_not_ready",
        "workspace_preparation_failed", "workspace_locked", "lock_conflict", "lock_expired", "lock_not_owned",
        "stale_workspace", "authorization_revocation_detected", "repository_conflict", "duplicate_binding",
        "unsupported_provider_capability", "path_validation_failed", "file_operation_failed", "dirty_workspace",
        "commit_failed", "provider_failure_known", "unknown_provider_outcome", "reconciliation_required",
        "state_transition_invalid", "input_limit_exceeded", "response_limit_exceeded", "query_timeout",
        "read_model_unavailable", "projection_stale", "projection_unavailable", "range_unsatisfiable",
        "file_policy_unavailable", "failed_operation", "redacted", "internal_error",
    };
    private static readonly HashSet<string> RootSchemaVersionOperations = new(StringComparer.Ordinal)
    {
        "CreateFolder", "ArchiveFolder", "UpdateFolderAclEntry", "ConfigureProviderBinding",
        "CreateRepositoryBackedFolder", "BindRepository",
        "ConfigureBranchRefPolicy", "PrepareWorkspace", "LockWorkspace", "ReleaseWorkspaceLock",
        "AddFile", "ChangeFile", "RemoveFile", "GetFolderFileMetadata", "SearchFolderFiles",
        "SearchFolderIndexedFiles", "GlobFolderFiles", "ReadFileRange", "CommitWorkspace",
    };
    private static readonly HashSet<string> NestedBranchPolicySchemaVersionOperations = new(StringComparer.Ordinal)
    {
        "CreateRepositoryBackedFolder", "BindRepository",
    };
    private static readonly HashSet<string> RequiredBodyOperations = new(StringComparer.Ordinal)
    {
        "CreateFolder", "ArchiveFolder", "UpdateFolderAclEntry", "ConfigureProviderBinding",
        "ValidateProviderReadiness", "CreateRepositoryBackedFolder", "BindRepository",
        "ConfigureBranchRefPolicy", "PrepareWorkspace", "LockWorkspace", "ReleaseWorkspaceLock",
        "AddFile", "ChangeFile", "RemoveFile", "GetFolderFileMetadata", "SearchFolderFiles",
        "SearchFolderIndexedFiles", "GlobFolderFiles", "ReadFileRange", "CommitWorkspace",
    };
    private static readonly IReadOnlyDictionary<string, string> OperationFreshness =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GetFolderLifecycleStatus"] = "eventually_consistent",
            ["ListFolderAclEntries"] = "eventually_consistent",
            ["GetEffectivePermissions"] = "read_your_writes",
            ["GetProviderBinding"] = "eventually_consistent",
            ["ValidateProviderReadiness"] = "snapshot_per_task",
            ["GetProviderSupportEvidence"] = "eventually_consistent",
            ["GetRepositoryBinding"] = "eventually_consistent",
            ["GetBranchRefPolicy"] = "eventually_consistent",
            ["GetWorkspaceLock"] = "read_your_writes",
            ["GetWorkspaceRetryEligibility"] = "eventually_consistent",
            ["GetWorkspaceTransitionEvidence"] = "snapshot_per_task",
            ["ListFolderFiles"] = "snapshot_per_task",
            ["GetFolderFileMetadata"] = "snapshot_per_task",
            ["SearchFolderFiles"] = "snapshot_per_task",
            ["SearchFolderIndexedFiles"] = "eventually_consistent",
            ["GetFolderIndexingStatus"] = "eventually_consistent",
            ["GlobFolderFiles"] = "snapshot_per_task",
            ["ReadFileRange"] = "snapshot_per_task",
            ["GetWorkspaceStatus"] = "read_your_writes",
            ["GetWorkspaceCleanupStatus"] = "read_your_writes",
            ["GetTaskStatus"] = "eventually_consistent",
            ["GetCommitEvidence"] = "eventually_consistent",
            ["GetProviderOutcome"] = "eventually_consistent",
            ["GetReconciliationStatus"] = "eventually_consistent",
            ["ListAuditTrail"] = "eventually_consistent",
            ["GetAuditRecord"] = "eventually_consistent",
            ["ListOperationTimeline"] = "eventually_consistent",
            ["GetOperationTimelineEntry"] = "eventually_consistent",
            ["GetReadinessDiagnostics"] = "eventually_consistent",
            ["GetLockDiagnostics"] = "eventually_consistent",
            ["GetDirtyStateDiagnostics"] = "eventually_consistent",
            ["GetFailedOperationDiagnostics"] = "eventually_consistent",
            ["GetProviderStatusDiagnostics"] = "eventually_consistent",
            ["GetSyncStatusDiagnostics"] = "eventually_consistent",
            ["GetProjectionFreshness"] = "eventually_consistent",
        };

    /// <summary>Adds the candidate-only authorization and historical transport seam.</summary>
    public static IApplicationBuilder UsePd10V2CandidateCompatibilitySeam(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (!IsCandidatePath(path))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!Pd10ProtectedOperationCatalog.TryResolve(
                    context.Request.Method,
                    path,
                    out Pd10ProtectedOperationDescriptor? descriptor,
                    out IReadOnlyDictionary<string, string> routeValues)
                || descriptor is null)
            {
                await AuditAsync(
                    context,
                    operation: "unknown",
                    operationFamily: "unknown",
                    result: "deny").ConfigureAwait(false);
                await WriteProblemAsync(context, Pd10AuthorizationOutcome.SafeDenial, null).ConfigureAwait(false);
                return;
            }

            ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
            if (string.IsNullOrWhiteSpace(tenant.AuthoritativeTenantId)
                || string.IsNullOrWhiteSpace(tenant.PrincipalId))
            {
                await AuditAsync(
                    context,
                    descriptor.OperationId,
                    ToKebabCase(descriptor.OperationFamily.ToString()),
                    result: "deny").ConfigureAwait(false);
                await WriteProblemAsync(context, Pd10AuthorizationOutcome.AuthenticationRequired, null).ConfigureAwait(false);
                return;
            }

            if (RequiresRequestBody(descriptor)
                && !await BufferBoundedRequestBodyAsync(context.Request, context.RequestAborted).ConfigureAwait(false))
            {
                await WriteInputLimitProblemAsync(context).ConfigureAwait(false);
                return;
            }

            Pd10AuthorizationContext authorization;
            try
            {
                authorization = await AuthorizeAsync(context, descriptor, routeValues).ConfigureAwait(false);
            }
            catch (Pd10RequestValidationException)
            {
                await WriteValidationProblemAsync(context).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                authorization = Unusable(descriptor);
            }

            Func<CancellationToken, ValueTask<Pd10TaskFolderBindingState>>? binding =
                descriptor.TaskBinding == Pd10TaskBindingRule.RouteTaskBelongsToRouteFolder
                    ? token => VerifyTaskBindingAsync(context, routeValues, token)
                    : null;
            Pd10ProtectedOperationResult<bool> result = await Pd10ProtectedOperationExecutor.ExecuteAsync(
                    authorization,
                    (outcome, _) => AuditAsync(
                        context,
                        descriptor.OperationId,
                        ToKebabCase(descriptor.OperationFamily.ToString()),
                        outcome.IsAllowed ? "allow" : "deny"),
                    token => ValidateCandidateEnvelopeAsync(context, descriptor, routeValues, token),
                    binding,
                    async token =>
                    {
                        if (descriptor.OperationId == "GetTaskStatus"
                            && context.Items.TryGetValue(TaskSnapshotItem, out object? snapshotValue)
                            && snapshotValue is TaskStatusReadModelSnapshot snapshot)
                        {
                            await WriteTaskStatusAsync(context, snapshot, token).ConfigureAwait(false);
                            return true;
                        }

                        PathString originalPath = context.Request.Path;
                        PreauthorizedRequestState preauthorized = new(
                            (string)context.Items[AuthorizedTenantItem]!,
                            (string)context.Items[AuthorizedPrincipalItem]!,
                            context.Items.TryGetValue(AuthorizedFolderItem, out object? folder) ? folder as string : null,
                            context.Items.TryGetValue(AuthorizedWatermarkItem, out object? watermark) ? watermark as string : null,
                            context.Items.TryGetValue(AuthorizedOrganizationItem, out object? organization) ? organization as string : null,
                            descriptor.ActionToken,
                            descriptor.HistoricalActionToken,
                            context.Items.TryGetValue(AuthorizedDelegatorItem, out object? delegator) ? delegator as string : null);
                        try
                        {
                            PreauthorizedRequestContext.Begin(preauthorized);
                            context.Request.Path = Pd10ProtectedOperationCatalog.HistoricalPath(descriptor, routeValues);
                            await RewriteRequestAsync(context.Request, descriptor, token).ConfigureAwait(false);
                            await InvokeHistoricalAsync(context, next, descriptor).ConfigureAwait(false);
                            return true;
                        }
                        finally
                        {
                            PreauthorizedRequestContext.End();
                            context.Request.Path = originalPath;
                        }
                    },
                    context.RequestAborted).ConfigureAwait(false);
            if (!result.Outcome.IsAllowed)
            {
                await WriteProblemAsync(context, result.Outcome, null).ConfigureAwait(false);
            }
        });
    }

    private static async ValueTask<Pd10AuthorizationContext> AuthorizeAsync(
        HttpContext context,
        Pd10ProtectedOperationDescriptor descriptor,
        IReadOnlyDictionary<string, string> routeValues)
    {
        ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        string? tenantId = tenant.AuthoritativeTenantId;
        string? principalId = tenant.PrincipalId;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId))
        {
            return new(
                false,
                V2AccessState.TenantMember,
                Pd10AuthorityEvidenceState.Unavailable,
                false,
                false,
                false,
                false,
                false,
                RequiresBinding(descriptor));
        }

        (bool isDelegated, bool isUsable, string? delegatorId, EventStoreClaimTransformEvidence? delegatorClaim) =
            DelegationEvidence(context.User, descriptor.ActionToken, tenantId, principalId);
        V2AccessState accessState = CanonicalAccessState(context.User, isDelegated);
        if (!isUsable)
        {
            return Unusable(descriptor, Pd10AuthorityEvidenceState.Incomplete, V2AccessState.DelegatedServiceAgent);
        }

        if (delegatorId is not null)
        {
            context.Items[AuthorizedDelegatorItem] = delegatorId;
        }

        if (isDelegated && !IsDelegable(descriptor.OperationFamily))
        {
            return Denied(descriptor, accessState);
        }

        EventStoreClaimTransformEvidence claim = context.RequestServices
            .GetRequiredService<IEventStoreClaimTransformEvidenceAccessor>()
            .GetEvidence(descriptor.ActionToken);
        if (!claim.IsPresent || claim.Malformed)
        {
            return Unusable(descriptor, accessState: accessState);
        }

        if (!string.Equals(claim.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(claim.PrincipalId, principalId, StringComparison.Ordinal)
            || !claim.HasPermissionFor(descriptor.ActionToken))
        {
            return Denied(descriptor, accessState);
        }

        string? folderId = descriptor.FolderScope switch
        {
            Pd10FolderScopeRule.None => null,
            Pd10FolderScopeRule.RouteFolder => Value(routeValues, "folderId"),
            Pd10FolderScopeRule.RequestFolder => await ReadRequestFolderAsync(context.Request, context.RequestAborted).ConfigureAwait(false),
            _ => null,
        };
        if (descriptor.FolderScope != Pd10FolderScopeRule.None && string.IsNullOrWhiteSpace(folderId))
        {
            return Unusable(descriptor, accessState: accessState);
        }

        context.Items[AuthorizedTenantItem] = tenantId;
        context.Items[AuthorizedPrincipalItem] = principalId;
        if (folderId is not null)
        {
            context.Items[AuthorizedFolderItem] = folderId;
        }

        if (folderId is null)
        {
            if (HasClientControlledMismatch(tenantId, ClientTenantIds(context))
                || HasClientControlledMismatch(principalId, ClientPrincipalIds(context)))
            {
                return Denied(descriptor, accessState);
            }

            TenantAccessAuthorizer authorizer = context.RequestServices.GetRequiredService<TenantAccessAuthorizer>();
            TenantAccessOutcome outcome = (await authorizer
                .AuthorizeMutationAsync(
                    new TenantAccessAuthorizationContext(tenantId, principalId, tenantId),
                    context.RequestAborted)
                .ConfigureAwait(false)).Outcome;
            if (outcome == TenantAccessOutcome.Allowed && isDelegated)
            {
                outcome = (await authorizer.AuthorizeMutationAsync(
                    new TenantAccessAuthorizationContext(tenantId, delegatorId!, tenantId),
                    context.RequestAborted).ConfigureAwait(false)).Outcome;
            }

            return outcome switch
            {
                TenantAccessOutcome.Allowed => Allowed(descriptor, accessState),
                TenantAccessOutcome.StaleProjection => Unusable(descriptor, Pd10AuthorityEvidenceState.Stale, accessState),
                TenantAccessOutcome.UnavailableProjection => Unusable(descriptor, Pd10AuthorityEvidenceState.Unavailable, accessState),
                TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict =>
                    Unusable(descriptor, Pd10AuthorityEvidenceState.Conflicting, accessState),
                _ => Denied(descriptor, accessState),
            };
        }

        LayeredFolderOperationPolicy policy = descriptor.PolicyClass == FolderOperationPolicyClass.StrictRead
            ? LayeredFolderOperationPolicy.StrictRead()
            : LayeredFolderOperationPolicy.Mutation();
        LayeredFolderAuthorizationService layered = context.RequestServices
            .GetRequiredService<LayeredFolderAuthorizationService>();
        LayeredFolderAuthorizationResult result = await layered
            .AuthorizeAsync(
                new LayeredFolderAuthorizationContext(
                    tenantId,
                    principalId,
                    principalId,
                    descriptor.ActionToken,
                    policy,
                    claim,
                    folderId,
                    CorrelationId(context),
                    Header(context, "X-Hexalith-Task-Id") ?? Value(routeValues, "taskId"),
                    ClientTenantIds(context),
                    ClientPrincipalIds(context)),
                context.RequestAborted)
            .ConfigureAwait(false);
        if (result.IsAllowed && isDelegated)
        {
            result = await layered.AuthorizeAsync(
                new LayeredFolderAuthorizationContext(
                    tenantId,
                    delegatorId!,
                    principalId,
                    descriptor.ActionToken,
                    policy,
                    delegatorClaim!,
                    folderId,
                    CorrelationId(context),
                    Header(context, "X-Hexalith-Task-Id") ?? Value(routeValues, "taskId"),
                    ClientTenantIds(context),
                    ClientControlledPrincipalValues: null),
                context.RequestAborted).ConfigureAwait(false);
        }

        if (result.IsAllowed)
        {
            if (result.AllowedContext?.FreshnessWatermark is { } watermark)
            {
                context.Items[AuthorizedWatermarkItem] = watermark;
            }

            if (result.AllowedContext?.OrganizationId is { } organizationId)
            {
                context.Items[AuthorizedOrganizationItem] = organizationId;
            }

            return Allowed(descriptor, accessState);
        }

        if (result.Decision.OutcomeCode == LayeredAuthorizationOutcomeCodes.AuthenticationDenied)
        {
            return new(
                false,
                accessState,
                Pd10AuthorityEvidenceState.Unavailable,
                false,
                false,
                false,
                false,
                !isDelegated,
                RequiresBinding(descriptor));
        }

        Pd10AuthorityEvidenceState state = result.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale or LayeredAuthorizationOutcomeCodes.FolderAclStale =>
                Pd10AuthorityEvidenceState.Stale,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable =>
                Pd10AuthorityEvidenceState.Unavailable,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => Pd10AuthorityEvidenceState.Incomplete,
            _ when result.Decision.Retryable => Pd10AuthorityEvidenceState.Unavailable,
            _ => Pd10AuthorityEvidenceState.Fresh,
        };
        return state == Pd10AuthorityEvidenceState.Fresh
            ? Denied(descriptor, accessState)
            : Unusable(descriptor, state, accessState);
    }

    private static async ValueTask<Pd10TaskFolderBindingState> VerifyTaskBindingAsync(
        HttpContext context,
        IReadOnlyDictionary<string, string> routeValues,
        CancellationToken cancellationToken)
    {
        string? folderId = Value(routeValues, "folderId");
        string? taskId = Value(routeValues, "taskId");
        ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        if (string.IsNullOrWhiteSpace(folderId) || string.IsNullOrWhiteSpace(taskId)
            || string.IsNullOrWhiteSpace(tenant.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(tenant.PrincipalId))
        {
            return Pd10TaskFolderBindingState.NotBound;
        }

        TaskStatusReadModelResult result;
        try
        {
            result = await context.RequestServices.GetRequiredService<ITaskStatusReadModel>().GetAsync(
                new TaskStatusReadModelRequest(
                    tenant.AuthoritativeTenantId,
                    taskId,
                    tenant.PrincipalId,
                    TaskStatusQueryHandler.ActionToken,
                    CorrelationId(context),
                    "eventually_consistent"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        if (result.Freshness is null || result.Freshness.Stale)
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        if (result.Status == TaskStatusReadModelStatus.NotFound)
        {
            return Pd10TaskFolderBindingState.NotBound;
        }

        if (result.Status != TaskStatusReadModelStatus.Available
            || result.Snapshot is null
            || string.IsNullOrWhiteSpace(result.Snapshot.FolderId)
            || result.Snapshot.Freshness is null
            || result.Snapshot.EvidenceScope is null
            || result.Snapshot.RetryEligibility is null
            || result.Snapshot.Freshness.Stale)
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        if (!string.Equals(
                result.Freshness.ProjectionWatermark,
                result.Snapshot.Freshness.ProjectionWatermark,
                StringComparison.Ordinal))
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        bool isBound = result.Snapshot.ManagedTenantId == tenant.AuthoritativeTenantId
            && result.Snapshot.FolderId == folderId
            && result.Snapshot.TaskId == taskId;
        if (!isBound)
        {
            return Pd10TaskFolderBindingState.NotBound;
        }

        bool hasCompatibleEvidence = result.Snapshot.EvidenceScope.ManagedTenantId == tenant.AuthoritativeTenantId
            && result.Snapshot.EvidenceScope.PrincipalId == tenant.PrincipalId
            && result.Snapshot.EvidenceScope.ActionToken == TaskStatusQueryHandler.ActionToken
            && result.Snapshot.EvidenceScope.TaskId == taskId;
        if (!hasCompatibleEvidence || !IsTaskStatusContractShaped(result.Snapshot))
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        context.Items[TaskSnapshotItem] = result.Snapshot;
        return Pd10TaskFolderBindingState.Bound;
    }

    private static async ValueTask<bool> ValidateCandidateEnvelopeAsync(
        HttpContext context,
        Pd10ProtectedOperationDescriptor descriptor,
        IReadOnlyDictionary<string, string> routeValues,
        CancellationToken cancellationToken)
    {
        if (descriptor.OperationId == "GetTaskStatus" && HeaderValues(context, "Idempotency-Key").Count > 0)
        {
            await WriteValidationProblemAsync(
                context,
                "idempotency_key_not_allowed",
                "Idempotency-Key is not accepted on read operations.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!HeaderValuesAreExactIdentifiers(context, "X-Correlation-Id")
            || !HeaderValuesAreExactIdentifiers(context, "X-Hexalith-Task-Id")
            || !HeaderValuesAreExactIdentifiers(context, "Idempotency-Key")
            || !HeaderValuesAreExactIdentifiers(context, "X-Hexalith-Tenant-Id")
            || !HeaderValuesAreExactIdentifiers(context, "X-Tenant-Id")
            || !HeaderValuesAreExactIdentifiers(context, "X-Forwarded-Tenant")
            || !HeaderValuesAreExactIdentifiers(context, "X-Principal-Id")
            || !HeaderValuesAreExactIdentifiers(context, "X-Forwarded-Principal")
            || !QueryValuesAreExactIdentifiers(context, "tenantId")
            || !QueryValuesAreExactIdentifiers(context, "managedTenantId")
            || !QueryValuesAreExactIdentifiers(context, "principalId")
            || !AllHeaderValuesAgree(context, "X-Correlation-Id")
            || !AllHeaderValuesAgree(context, "X-Hexalith-Task-Id")
            || !AllHeaderValuesAgree(context, "Idempotency-Key"))
        {
            await WriteValidationProblemAsync(
                context,
                "validation_error",
                "Request validation failed.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        IReadOnlyList<string> freshnessValues = HeaderValues(context, "X-Hexalith-Freshness");
        if (OperationFreshness.TryGetValue(descriptor.OperationId, out string? acceptedFreshness))
        {
            if (freshnessValues.Any(value => !string.Equals(value, acceptedFreshness, StringComparison.Ordinal)))
            {
                await WriteValidationProblemAsync(
                    context,
                    "unsupported_read_consistency",
                    $"Operation supports {acceptedFreshness} only.",
                    cancellationToken).ConfigureAwait(false);
                return false;
            }
        }
        else if (freshnessValues.Count > 0)
        {
            await WriteValidationProblemAsync(
                context,
                "unsupported_read_consistency",
                "Operation does not accept a freshness value.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (descriptor.OperationId == "GetTaskStatus")
        {
            string? taskId = Value(routeValues, "taskId");
            if (HeaderValues(context, "X-Hexalith-Task-Id")
                .Any(value => !string.Equals(value, taskId, StringComparison.Ordinal)))
            {
                await WriteValidationProblemAsync(
                    context,
                    "validation_error",
                    "Request validation failed.",
                    cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        bool hasBody = HasRequestBody(context.Request);
        if (RequiresRequestBody(descriptor) != hasBody)
        {
            await WriteValidationProblemAsync(
                context,
                "validation_error",
                RequiresRequestBody(descriptor)
                    ? "The request body is required for this operation."
                    : "The operation does not accept a request body.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!await HasExactV2SchemaDiscriminatorsAsync(
                context.Request,
                descriptor,
                cancellationToken).ConfigureAwait(false))
        {
            await WriteValidationProblemAsync(
                context,
                "unsupported_request_schema_version",
                "The request schema version is not supported.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private static async Task InvokeHistoricalAsync(
        HttpContext context,
        RequestDelegate next,
        Pd10ProtectedOperationDescriptor descriptor)
    {
        Stream destination = context.Response.Body;
        using MemoryStream captured = new();
        context.Response.Body = captured;
        try
        {
            await next(context).ConfigureAwait(false);
            captured.Position = 0;
            Pd10AuthorizationOutcome? canonical = context.Response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => Pd10AuthorizationOutcome.AuthenticationRequired,
                StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound => Pd10AuthorizationOutcome.SafeDenial,
                StatusCodes.Status503ServiceUnavailable when IsAuthorityUnavailable(captured) => Pd10AuthorizationOutcome.AuthorityUnavailable,
                _ => null,
            };
            captured.Position = 0;
            if (canonical is not null)
            {
                string? correlationId = ValidateCorrelationId(ReadCorrelationId(captured));
                context.Response.Body = destination;
                await WriteProblemAsync(context, canonical, correlationId).ConfigureAwait(false);
                return;
            }

            if (!Pd10V2RuntimeResponseCatalog.AllowsStatus(descriptor.OperationId, context.Response.StatusCode))
            {
                context.Response.Body = destination;
                await WriteProblemAsync(
                    context,
                    Pd10AuthorizationOutcome.AuthorityUnavailable,
                    ValidateCorrelationId(ReadCorrelationId(captured))).ConfigureAwait(false);
                return;
            }

            if (descriptor.OperationId == "GetBranchRefPolicy"
                && context.Response.StatusCode is >= 200 and < 300)
            {
                TranslateBranchRefPolicySuccess(captured, context.Response.ContentType);
            }
            else
            {
                bool normalized = NormalizeHistoricalProblem(captured, context.Response.ContentType);
                if (context.Response.StatusCode >= 400
                    && (!normalized || !IsDeclaredProblem(captured, descriptor.OperationId, context.Response.StatusCode)))
                {
                    context.Response.Body = destination;
                    await WriteProblemAsync(
                        context,
                        Pd10AuthorizationOutcome.AuthorityUnavailable,
                        ValidateCorrelationId(ReadCorrelationId(captured))).ConfigureAwait(false);
                    return;
                }
            }
            context.Response.Headers.Remove("X-Hexalith-Read-Consistency");
            if (context.Response.StatusCode is >= 200 and < 300
                && OperationFreshness.TryGetValue(descriptor.OperationId, out string? responseFreshness))
            {
                context.Response.Headers["X-Hexalith-Freshness"] = responseFreshness;
            }
            context.Response.ContentLength = captured.Length;
            await captured.CopyToAsync(destination, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = destination;
        }
    }

    private static void TranslateBranchRefPolicySuccess(MemoryStream body, string? contentType)
    {
        if (!IsExactJsonMediaType(contentType))
        {
            body.Position = 0;
            return;
        }

        body.Position = 0;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            body.Position = 0;
            return;
        }

        if (parsed is JsonObject response
            && RewriteSuccessSchemaDiscriminator(response, "GetBranchRefPolicy"))
        {
            body.SetLength(0);
            JsonSerializer.Serialize(body, response);
        }

        body.Position = 0;
    }

    /// <summary>Translates only the one schema-owned successful response discriminator changed by v2.</summary>
    internal static bool RewriteSuccessSchemaDiscriminator(JsonObject response, string operationId)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!string.Equals(operationId, "GetBranchRefPolicy", StringComparison.Ordinal)
            || response["requestSchemaVersion"] is not JsonValue version
            || !version.TryGetValue(out string? value)
            || !string.Equals(value, "v1", StringComparison.Ordinal))
        {
            return false;
        }

        response["requestSchemaVersion"] = "v2";
        return true;
    }

    private static bool NormalizeHistoricalProblem(MemoryStream body, string? contentType)
    {
        if (!IsExactProblemJsonMediaType(contentType))
        {
            body.Position = 0;
            return false;
        }

        body.Position = 0;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            body.Position = 0;
            return false;
        }

        if (parsed is not JsonObject problem
            || problem["category"] is not JsonValue categoryValue
            || !categoryValue.TryGetValue(out string? category)
            || problem["code"] is not JsonValue codeValue
            || !codeValue.TryGetValue(out string? code))
        {
            body.Position = 0;
            return false;
        }

        // Historical handlers expose task, retry, and RFC 9457 extension metadata at the top level.
        // The v2 candidate owns a closed Problem Details shape, so retain only schema-owned fields.
        problem.Remove("taskId");
        problem.Remove("retryAfterSeconds");
        problem.Remove("detail");
        problem.Remove("instance");
        if (problem["details"] is JsonObject details)
        {
            details.Remove("retryReasonCode");
            details.Remove("reasonCategory");
            details.Remove("evidenceSource");
            details.Remove("taskId");
            foreach ((string key, JsonNode? value) in details.ToArray())
            {
                if (value is JsonValue scalar && scalar.TryGetValue(out string? _))
                {
                    continue;
                }

                details[key] = value switch
                {
                    null => "null",
                    JsonValue boolean when boolean.TryGetValue(out bool boolValue) => boolValue ? "true" : "false",
                    _ => value.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
                };
            }
        }

        if (category == "lock_conflict")
        {
            problem["code"] = "workspace_locked";
            problem["retryable"] = true;
            if (problem["details"] is JsonObject lockDetails)
            {
                lockDetails["lockStatus"] = "active";
            }
        }
        else if (category is "projection_stale" or "projection_unavailable" or "provider_unavailable"
            or "file_policy_unavailable" or "read_model_unavailable" or "lock_expired")
        {
            problem["retryable"] = true;
            if (category == "read_model_unavailable")
            {
                problem["code"] = "projection_unavailable";
            }
        }

        problem["clientAction"] = (category, code) switch
        {
            ("authentication_failure", _) => "check_credentials",
            ("validation_error", "tampered_cursor_or_changed_filter") => "restart_query",
            ("validation_error", _) or ("duplicate_binding", _) or ("idempotency_conflict", _)
                or ("repository_conflict", _) or ("input_limit_exceeded", _)
                or ("response_limit_exceeded", _) or ("range_unsatisfiable", _)
                or ("state_transition_invalid", _) or ("workspace_preparation_failed", _)
                or ("query_timeout", _) => "revise_request",
            ("idempotency_key_expired", _) => "refresh_state_then_submit_with_new_key",
            ("authorization_revocation_detected", _) or ("dirty_workspace", _)
                or ("commit_failed", _) or ("provider_readiness_failed", _) => "contact_operator",
            ("unknown_provider_outcome", _) or ("reconciliation_required", _) => "wait_for_reconciliation",
            ("provider_failure_known", _) => "do_not_retry",
            ("idempotency_admission_unavailable", _) => "retry",
            ("lock_conflict", _) or ("lock_expired", _) or ("projection_stale", _)
                or ("projection_unavailable", _) or ("provider_unavailable", _)
                or ("file_policy_unavailable", _) or ("read_model_unavailable", _) => "retry",
            _ => problem["clientAction"]?.DeepClone(),
        };

        body.SetLength(0);
        JsonSerializer.Serialize(body, problem);
        body.Position = 0;
        return true;
    }

    private static bool IsDeclaredProblem(Stream body, string operationId, int httpStatus)
    {
        body.Position = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string[] requiredNames =
            [
                "type", "title", "status", "category", "code", "message", "correlationId",
                "retryable", "clientAction", "details",
            ];
            HashSet<string> allowedNames = new(requiredNames, StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object
                || HasDuplicateProperties(root)
                || root.EnumerateObject().Any(property => !allowedNames.Contains(property.Name))
                || requiredNames.Any(name => !root.TryGetProperty(name, out _))
                || !root.TryGetProperty("status", out JsonElement status)
                || status.ValueKind != JsonValueKind.Number
                || !status.TryGetInt32(out int statusValue)
                || statusValue != httpStatus
                || StringProperty(root, "type") is null
                || StringProperty(root, "title") is null
                || StringProperty(root, "category") is not { } category
                || StringProperty(root, "code") is not { } code
                || StringProperty(root, "message") is null
                || !Pd10OpaqueIdentifier.IsValid(StringProperty(root, "correlationId"))
                || !root.TryGetProperty("retryable", out JsonElement retryable)
                || retryable.ValueKind is not JsonValueKind.True and not JsonValueKind.False
                || StringProperty(root, "clientAction") is not { } clientAction
                || !root.TryGetProperty("details", out JsonElement details)
                || details.ValueKind != JsonValueKind.Object
                || !details.TryGetProperty("visibility", out JsonElement visibility)
                || visibility.ValueKind != JsonValueKind.String
                || details.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String))
            {
                return false;
            }

            string tuple = string.Join(
                '|',
                statusValue,
                category,
                code,
                retryable.GetBoolean() ? "true" : "false",
                clientAction,
                string.Join(',', details.EnumerateObject()
                    .Select(static property => property.Name)
                    .Order(StringComparer.Ordinal)));
            return Pd10V2RuntimeResponseCatalog.AllowsProblem(operationId, tuple);
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            body.Position = 0;
        }
    }

    private static bool IsAuthorityUnavailable(Stream body)
    {
        body.Position = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            string? category = StringProperty(root, "category");
            string? code = StringProperty(root, "code");
            string? evidence = root.TryGetProperty("details", out JsonElement details)
                && details.ValueKind == JsonValueKind.Object ? StringProperty(details, "evidenceSource") : null;
            return evidence == "authorization_decision"
                || category is "policy_evidence_unavailable" or "authorization_evidence_unavailable"
                || code is "policy_evidence_unavailable" or "authorization_evidence_unavailable"
                    or "tenant_projection_stale" or "tenant_projection_unavailable"
                    or "folder_acl_stale" or "folder_acl_unavailable";
        }
        catch (JsonException)
        {
            return true;
        }
        finally
        {
            body.Position = 0;
        }
    }

    private static string? ReadCorrelationId(Stream body)
    {
        body.Position = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? StringProperty(document.RootElement, "correlationId") : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            body.Position = 0;
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Pd10AuthorizationOutcome outcome, string? correlationId)
    {
        (string title, string message) = outcome.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ("Authentication required", "Authentication is required."),
            StatusCodes.Status404NotFound => ("Resource not available", "The requested resource is unavailable."),
            _ => ("Authorization evidence unavailable", "Authorization evidence is temporarily unavailable."),
        };
        context.Response.Headers.Clear();
        context.Response.StatusCode = outcome.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            type = "about:blank",
            title,
            status = outcome.StatusCode,
            category = outcome.Category,
            code = outcome.Code,
            message,
            correlationId = ValidateCorrelationId(correlationId)
                ?? CorrelationId(context)
                ?? "correlation_absent",
            retryable = outcome.Retryable,
            clientAction = outcome.ClientAction,
            details = new { visibility = outcome.Visibility },
        }, cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<string?> ReadRequestFolderAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || !IsExactJsonMediaType(request.ContentType))
        {
            throw new Pd10RequestValidationException();
        }

        request.Body.Position = 0;
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (HasDuplicateProperties(document.RootElement))
            {
                throw new Pd10RequestValidationException();
            }

            string? folderId = document.RootElement.ValueKind == JsonValueKind.Object
                ? StringProperty(document.RootElement, "folderId") : null;
            return !Pd10OpaqueIdentifier.IsValid(folderId)
                ? throw new Pd10RequestValidationException()
                : folderId;
        }
        catch (JsonException)
        {
            throw new Pd10RequestValidationException();
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task RewriteRequestAsync(
        HttpRequest request,
        Pd10ProtectedOperationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || !IsExactJsonMediaType(request.ContentType))
        {
            return;
        }

        using StreamReader reader = new(request.Body, Encoding.UTF8, false, leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        JsonNode? root = JsonNode.Parse(json);
        if (root is JsonObject jsonObject)
        {
            RewriteSchemaDiscriminators(jsonObject, descriptor);
        }

        byte[] rewritten = Encoding.UTF8.GetBytes(root?.ToJsonString() ?? json);
        request.Body = new MemoryStream(rewritten, writable: false);
        request.ContentLength = rewritten.Length;
    }

    /// <summary>Translates only the request-schema discriminator positions owned by the selected operation.</summary>
    internal static void RewriteSchemaDiscriminators(
        JsonObject jsonObject,
        Pd10ProtectedOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(jsonObject);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!RootSchemaVersionOperations.Contains(descriptor.OperationId))
        {
            return;
        }

        RewriteSchemaVersion(jsonObject);
        if (NestedBranchPolicySchemaVersionOperations.Contains(descriptor.OperationId)
            && jsonObject["branchRefPolicy"] is JsonObject branchRefPolicy)
        {
            RewriteSchemaVersion(branchRefPolicy);
        }
    }

    private static void RewriteSchemaVersion(JsonObject jsonObject)
    {
        if (jsonObject.TryGetPropertyValue("requestSchemaVersion", out JsonNode? version)
            && version?.GetValueKind() == JsonValueKind.String
            && string.Equals(version.GetValue<string>(), "v2", StringComparison.Ordinal))
        {
            jsonObject["requestSchemaVersion"] = "v1";
        }
    }

    private static Pd10AuthorizationContext Allowed(
        Pd10ProtectedOperationDescriptor descriptor,
        V2AccessState accessState)
        => new(
            true,
            accessState,
            Pd10AuthorityEvidenceState.Fresh,
            true,
            true,
            true,
            true,
            true,
            RequiresBinding(descriptor));

    private static Pd10AuthorizationContext Denied(
        Pd10ProtectedOperationDescriptor descriptor,
        V2AccessState accessState)
        => new(
            true,
            accessState,
            Pd10AuthorityEvidenceState.Fresh,
            false,
            false,
            false,
            false,
            accessState != V2AccessState.DelegatedServiceAgent,
            RequiresBinding(descriptor));

    private static Pd10AuthorizationContext Unusable(
        Pd10ProtectedOperationDescriptor descriptor,
        Pd10AuthorityEvidenceState state = Pd10AuthorityEvidenceState.Incomplete,
        V2AccessState accessState = V2AccessState.TenantMember)
        => new(
            true,
            accessState,
            state,
            false,
            false,
            false,
            false,
            accessState != V2AccessState.DelegatedServiceAgent,
            RequiresBinding(descriptor));

    private static async Task<bool> BufferBoundedRequestBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBodyBytes)
        {
            return false;
        }

        if (request.Body == Stream.Null)
        {
            return true;
        }

        try
        {
            request.EnableBuffering(64 * 1024, MaximumRequestBodyBytes);
            request.Body.Position = 0;
            byte[] buffer = new byte[8192];
            long observed = 0;
            int read;
            while ((read = await request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                observed += read;
                if (observed > MaximumRequestBodyBytes)
                {
                    return false;
                }
            }

            request.Body.Position = 0;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task WriteInputLimitProblemAsync(HttpContext context)
    {
        context.Response.Headers.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            type = "about:blank",
            title = "Request body too large",
            status = StatusCodes.Status413PayloadTooLarge,
            category = "input_limit_exceeded",
            code = "c4_input_limit_exceeded",
            message = "The request body exceeds the supported limit.",
            correlationId = CorrelationId(context) ?? "correlation_absent",
            retryable = false,
            clientAction = "revise_request",
            details = new { visibility = "redacted" },
        }, cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }

    private static Task WriteValidationProblemAsync(HttpContext context)
        => WriteValidationProblemAsync(
            context,
            "validation_error",
            "Request validation failed.",
            context.RequestAborted);

    private static async Task WriteValidationProblemAsync(
        HttpContext context,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.Clear();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            type = "about:blank",
            title = "Validation failure",
            status = StatusCodes.Status400BadRequest,
            category = "validation_error",
            code,
            message,
            correlationId = CorrelationId(context) ?? "correlation_absent",
            retryable = false,
            clientAction = "revise_request",
            details = new { visibility = "metadata_only" },
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask AuditAsync(
        HttpContext context,
        string operation,
        string operationFamily,
        string result)
    {
        ITenantContextAccessor? tenant = context.RequestServices.GetService<ITenantContextAccessor>();
        IPd10AuthorizationAuditSink sink = context.RequestServices.GetRequiredService<IPd10AuthorizationAuditSink>();
        await sink.WriteAsync(
            new Pd10AuthorizationAuditRecord(
                tenant?.PrincipalId ?? "actor_absent",
                tenant?.AuthoritativeTenantId ?? "tenant_absent",
                operation,
                operationFamily,
                result,
                CorrelationId(context) ?? "correlation_absent"),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WriteTaskStatusAsync(
        HttpContext context,
        TaskStatusReadModelSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        string? correlationId = CorrelationId(context);
        if (correlationId is not null)
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        context.Response.Headers["X-Hexalith-Freshness"] = "eventually_consistent";
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        Dictionary<string, object?> response = new(StringComparer.Ordinal)
        {
            ["taskId"] = snapshot.TaskId,
            ["currentState"] = snapshot.CurrentState,
            ["retryEligibility"] = new
            {
                eligible = snapshot.RetryEligibility.Eligible,
                reasonCode = snapshot.RetryEligibility.ReasonCode,
                advisoryOnly = snapshot.RetryEligibility.AdvisoryOnly,
            },
            ["freshness"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["readConsistency"] = "eventually_consistent",
                ["observedAt"] = snapshot.Freshness.ObservedAt,
                ["stale"] = snapshot.Freshness.Stale,
            },
        };
        AddWhenPresent(response, "terminalState", snapshot.TerminalState);
        AddWhenPresent(response, "lastOperationId", snapshot.LastOperationId);
        AddWhenPresent(response, "lastFailureCategory", snapshot.LastFailureCategory);
        if (snapshot.RetryAfter is not null)
        {
            response["retryAfter"] = new
            {
                retryAfterSeconds = snapshot.RetryAfter.RetryAfterSeconds,
                advisoryOnly = snapshot.RetryAfter.AdvisoryOnly,
            };
        }

        if (snapshot.Freshness.ProjectionWatermark is not null
            && response["freshness"] is Dictionary<string, object?> freshness)
        {
            freshness["projectionWatermark"] = snapshot.Freshness.ProjectionWatermark;
        }

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void AddWhenPresent(IDictionary<string, object?> values, string name, string? value)
    {
        if (value is not null)
        {
            values[name] = value;
        }
    }

    private static (bool IsDelegated, bool IsUsable, string? DelegatorId, EventStoreClaimTransformEvidence? Evidence)
        DelegationEvidence(
            ClaimsPrincipal principal,
            string actionToken,
            string tenantId,
            string actorId)
    {
        string[] rawDelegatorIds = principal.FindAll(DelegatorClaimType)
            .Select(static claim => claim.Value)
            .ToArray();
        string[] delegatorIds = rawDelegatorIds
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string? delegatorId = delegatorIds.Length == 1 ? delegatorIds[0] : null;
        string[] permissions = principal.FindAll(DelegatorPermissionClaimType)
            .Select(static claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        bool isDelegated = rawDelegatorIds.Length > 0 || permissions.Length > 0;
        if (!isDelegated)
        {
            return (false, true, null, null);
        }

        bool usable = rawDelegatorIds.Length > 0
            && rawDelegatorIds.All(static value => !string.IsNullOrWhiteSpace(value))
            && delegatorIds.Length == 1
            && !string.IsNullOrWhiteSpace(delegatorId)
            && !string.Equals(delegatorId, actorId, StringComparison.Ordinal)
            && permissions.Contains(actionToken, StringComparer.Ordinal);
        return usable
            ? (true, true, delegatorId, EventStoreClaimTransformEvidence.Allowed(tenantId, delegatorId, permissions))
            : (true, false, delegatorId, null);
    }

    private static V2AccessState CanonicalAccessState(ClaimsPrincipal principal, bool isDelegated)
    {
        string[] values = principal.FindAll(AccessStateClaimType)
            .Select(static claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        V2AccessState[] states = values.Select(static value => value switch
        {
            "tenant-administrator" => V2AccessState.TenantAdministrator,
            "tenant-member" => V2AccessState.TenantMember,
            "tenant-scoped-operator" => V2AccessState.TenantScopedOperator,
            "audit-reviewer" => V2AccessState.AuditReviewer,
            "incident-administrator" => V2AccessState.IncidentAdministrator,
            "wrong-tenant" => V2AccessState.WrongTenant,
            "revoked" => V2AccessState.Revoked,
            "stale" => V2AccessState.Stale,
            "disabled" => V2AccessState.Disabled,
            "unknown" => V2AccessState.Unknown,
            "hidden-resource" => V2AccessState.HiddenResource,
            "absent-resource" => V2AccessState.AbsentResource,
            "insufficient-scope" => V2AccessState.InsufficientScope,
            _ => (V2AccessState)(-1),
        }).ToArray();
        if (states.Any(static state => (int)state < 0))
        {
            return (V2AccessState)(-1);
        }

        if (states.Distinct().Count() > 1)
        {
            return (V2AccessState)(-1);
        }

        V2AccessState[] negativeStates = states.Where(IsNegativeAccessState).ToArray();
        if (negativeStates.Length > 0)
        {
            return negativeStates[0];
        }

        V2AccessState claimedState = values.SingleOrDefault() switch
        {
            "tenant-administrator" => V2AccessState.TenantAdministrator,
            "tenant-member" => V2AccessState.TenantMember,
            "tenant-scoped-operator" => V2AccessState.TenantScopedOperator,
            "audit-reviewer" => V2AccessState.AuditReviewer,
            "incident-administrator" => V2AccessState.IncidentAdministrator,
            "wrong-tenant" => V2AccessState.WrongTenant,
            "revoked" => V2AccessState.Revoked,
            "stale" => V2AccessState.Stale,
            "disabled" => V2AccessState.Disabled,
            "unknown" => V2AccessState.Unknown,
            "hidden-resource" => V2AccessState.HiddenResource,
            "absent-resource" => V2AccessState.AbsentResource,
            "insufficient-scope" => V2AccessState.InsufficientScope,
            null or "" => V2AccessState.TenantMember,
            _ => (V2AccessState)(-1),
        };
        return isDelegated && !IsNegativeAccessState(claimedState)
            ? V2AccessState.DelegatedServiceAgent
            : claimedState;
    }

    private static bool IsNegativeAccessState(V2AccessState accessState)
        => accessState is V2AccessState.WrongTenant
            or V2AccessState.Revoked
            or V2AccessState.Stale
            or V2AccessState.Disabled
            or V2AccessState.Unknown
            or V2AccessState.HiddenResource
            or V2AccessState.AbsentResource
            or V2AccessState.InsufficientScope;

    internal static bool IsDelegable(V2ProtectedOperationFamily family)
        => family is V2ProtectedOperationFamily.FolderAdministration
            or V2ProtectedOperationFamily.TaskMutation
            or V2ProtectedOperationFamily.ContextRead
            or V2ProtectedOperationFamily.StatusPermissionAndLockInspection
            or V2ProtectedOperationFamily.IndexSearch;

    private static bool IsTaskStatusContractShaped(TaskStatusReadModelSnapshot snapshot)
    {
        return snapshot.RetryEligibility is not null
            && snapshot.Freshness is not null
            && snapshot.EvidenceScope is not null
            && TaskLifecycleStates.Contains(snapshot.CurrentState)
            && (snapshot.TerminalState is null || TaskLifecycleStates.Contains(snapshot.TerminalState))
            && Pd10OpaqueIdentifier.IsValid(snapshot.TaskId)
            && (snapshot.LastOperationId is null || Pd10OpaqueIdentifier.IsValid(snapshot.LastOperationId))
            && (snapshot.LastFailureCategory is null
                || TaskErrorCategories.Contains(snapshot.LastFailureCategory))
            && snapshot.RetryEligibility.AdvisoryOnly
            && IsReasonCode(snapshot.RetryEligibility.ReasonCode)
            && (snapshot.RetryAfter is null
                || snapshot.RetryAfter.AdvisoryOnly
                && snapshot.RetryAfter.RetryAfterSeconds is >= 1 and <= 3600)
            && string.Equals(snapshot.Freshness.ReadConsistency, "eventually_consistent", StringComparison.Ordinal)
            && !snapshot.Freshness.Stale
            && (snapshot.Freshness.ProjectionWatermark is null
                || Pd10OpaqueIdentifier.IsValid(snapshot.Freshness.ProjectionWatermark));
    }

    private static bool IsReasonCode(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 80
            && value[0] is >= 'a' and <= 'z'
            && value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static string? CorrelationId(HttpContext context)
        => ValidateCorrelationId(Header(context, "X-Correlation-Id"));

    private static string? ValidateCorrelationId(string? value)
        => Pd10OpaqueIdentifier.IsValid(value) ? value : null;

    private static string ToKebabCase(string value)
    {
        StringBuilder builder = new(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index > 0 && char.IsUpper(character))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static bool RequiresBinding(Pd10ProtectedOperationDescriptor descriptor)
        => descriptor.TaskBinding != Pd10TaskBindingRule.None;

    private static bool RequiresRequestBody(Pd10ProtectedOperationDescriptor descriptor)
        => RequiredBodyOperations.Contains(descriptor.OperationId);

    private static bool HasRequestBody(HttpRequest request)
        => request.ContentLength is > 0
            || request.Headers.ContainsKey("Transfer-Encoding");

    private static bool IsCandidatePath(string path)
        => path == CandidatePrefix || path.StartsWith(CandidatePrefix + "/", StringComparison.Ordinal);

    private static string? Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) ? value : null;

    private static string? Header(HttpContext context, string name) => HeaderValues(context, name).FirstOrDefault();

    private static IReadOnlyList<string> HeaderValues(HttpContext context, string name)
        => context.Request.Headers[name]
            .Select(static value => value ?? string.Empty)
            .ToArray();

    private static bool HeaderValuesAreExactIdentifiers(HttpContext context, string name)
    {
        IReadOnlyList<string> values = HeaderValues(context, name);
        return values.Count == 0 || values.All(Pd10OpaqueIdentifier.IsValid);
    }

    private static bool AllHeaderValuesAgree(HttpContext context, string name)
    {
        IReadOnlyList<string> values = HeaderValues(context, name);
        return values.Count < 2 || values.All(value => string.Equals(value, values[0], StringComparison.Ordinal));
    }

    private static bool QueryValuesAreExactIdentifiers(HttpContext context, string name)
    {
        string[] values = context.Request.Query[name]
            .Select(static value => value ?? string.Empty)
            .ToArray();
        return values.Length == 0 || values.All(Pd10OpaqueIdentifier.IsValid);
    }

    private static async ValueTask<bool> HasExactV2SchemaDiscriminatorsAsync(
        HttpRequest request,
        Pd10ProtectedOperationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        bool requiresSchemaVersion = RootSchemaVersionOperations.Contains(descriptor.OperationId);
        bool requiresBodyValidation = requiresSchemaVersion
            || string.Equals(descriptor.OperationId, "ValidateProviderReadiness", StringComparison.Ordinal);
        if (!requiresBodyValidation)
        {
            return true;
        }

        if (request.Body == Stream.Null || !IsExactJsonMediaType(request.ContentType))
        {
            return false;
        }

        request.Body.Position = 0;
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (HasDuplicateProperties(document.RootElement)
                || !HasValidSchemaOwnedIdentifiers(document.RootElement))
            {
                return false;
            }

            if (!requiresSchemaVersion)
            {
                return true;
            }

            if (!HasExactV2SchemaVersion(document.RootElement))
            {
                return false;
            }

            if (!NestedBranchPolicySchemaVersionOperations.Contains(descriptor.OperationId))
            {
                return true;
            }

            return document.RootElement.TryGetProperty("branchRefPolicy", out JsonElement branchRefPolicy)
                && branchRefPolicy.ValueKind == JsonValueKind.Object
                && HasExactV2SchemaVersion(branchRefPolicy);
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static bool HasExactV2SchemaVersion(JsonElement element)
        => element.TryGetProperty("requestSchemaVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.String
            && string.Equals(version.GetString(), "v2", StringComparison.Ordinal);

    private static bool IsExactJsonMediaType(string? contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? parsed)
            && string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);

    private static bool IsExactProblemJsonMediaType(string? contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? parsed)
            && string.Equals(parsed.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase);

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(HasDuplicateProperties);
        }

        return false;
    }

    private static bool HasValidSchemaOwnedIdentifiers(JsonElement element)
    {
        HashSet<string> identifierProperties = new(StringComparer.Ordinal)
        {
            "parentFolderId", "folderId", "subjectRef", "providerFamilyRef", "capabilityProfileRef",
            "nonSecretCredentialReference", "providerBindingRef", "repositoryProfileRef", "externalRepositoryRef",
            "repositoryBindingId", "policyRef", "branchRefPolicyRef", "workspacePolicyRef", "lockId",
            "lockOwnershipProof", "operationId", "stagingReference", "taskId",
        };

        return ValidateIdentifiers(element, identifierProperties);
    }

    private static bool ValidateIdentifiers(JsonElement element, IReadOnlySet<string> identifierProperties)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (identifierProperties.Contains(property.Name)
                    && (property.Value.ValueKind != JsonValueKind.String
                        || !Pd10OpaqueIdentifier.IsValid(property.Value.GetString())))
                {
                    return false;
                }

                if (!ValidateIdentifiers(property.Value, identifierProperties))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().All(item => ValidateIdentifiers(item, identifierProperties));
        }

        return true;
    }

    private static bool HasClientControlledMismatch(
        string authoritativeValue,
        IReadOnlyDictionary<string, string?> comparisonValues)
        => comparisonValues.Values.Any(value =>
            string.IsNullOrWhiteSpace(value)
            || !string.Equals(value.Trim(), authoritativeValue, StringComparison.Ordinal));

    private static string? StringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static IReadOnlyDictionary<string, string?> ClientTenantIds(HttpContext context)
        => ClientControlledValues(
            context,
            ("query_tenant_id", false, "tenantId"),
            ("query_managed_tenant_id", false, "managedTenantId"),
            ("header_hexalith_tenant_id", true, "X-Hexalith-Tenant-Id"),
            ("header_tenant_id", true, "X-Tenant-Id"),
            ("forwarded_tenant_id", true, "X-Forwarded-Tenant"));

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext context)
        => ClientControlledValues(
            context,
            ("query_principal_id", false, "principalId"),
            ("header_principal_id", true, "X-Principal-Id"),
            ("forwarded_principal_id", true, "X-Forwarded-Principal"));

    private static IReadOnlyDictionary<string, string?> ClientControlledValues(
        HttpContext context,
        params (string Source, bool IsHeader, string Name)[] sources)
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        foreach ((string source, bool isHeader, string name) in sources)
        {
            IEnumerable<string?> supplied = isHeader
                ? context.Request.Headers[name]
                : context.Request.Query[name];
            int index = 0;
            foreach (string? value in supplied)
            {
                values[$"{source}[{index}]"] = value;
                index++;
            }
        }

        return values;
    }

    private sealed class Pd10RequestValidationException : Exception;
}
