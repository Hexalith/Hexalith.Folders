// <copyright file="ProjectAuthorizationGate.cs" company="Hexalith">
// Copyright (c) Hexalith. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Projects.Server;

using Hexalith.Projects.Authorization;
using Hexalith.Projects.Contracts.Ui;
using Hexalith.Projects.Projections.ProjectDetail;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

/// <summary>Runs the ordered host-side Projects authorization chain and short-circuits on denial.</summary>
public sealed class ProjectAuthorizationGate(
    TenantAccessAuthorizer tenantAccessAuthorizer,
    IProjectEventStoreAuthorizationValidator eventStoreAuthorizationValidator,
    IProjectDaprPolicyEvidenceProvider daprPolicyEvidenceProvider,
    IProjectDetailReadModel projectDetailReadModel)
{
    /// <summary>The action token required to create a project.</summary>
    public const string CreateProjectAction = "projects:create";

    /// <summary>The action token required to read a project detail.</summary>
    public const string ReadProjectAction = "projects:read";

    /// <summary>The action token required to list project rows.</summary>
    public const string ListProjectsAction = "projects:list";

    /// <summary>Authorizes a supported read scope using current Tenant evidence before input validation.</summary>
    public Task<ProjectAuthorizationResult> AuthorizeSupportedScopeAsync(
        bool list,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string correlationId,
        CancellationToken cancellationToken = default)
        => AuthorizeAsync(tenantContext, httpContext, list ? ListProjectsAction : ReadProjectAction,
            projectId: null, correlationId, taskId: null, allowBoundedStaleTenantProjection: false,
            requireProjectDetail: false, cancellationToken);

    /// <summary>The action token required to update project setup.</summary>
    public const string UpdateProjectSetupAction = "projects:update_setup";

    /// <summary>The action token required to archive a project.</summary>
    public const string ArchiveProjectAction = "projects:archive";

    /// <summary>The action token required to restore an archived project.</summary>
    public const string RestoreProjectAction = "projects:restore";

    /// <summary>The action token required to link a conversation to a project.</summary>
    public const string LinkConversationAction = "projects:link_conversation";

    /// <summary>The action token required to move a conversation between projects.</summary>
    public const string MoveConversationAction = "projects:move_conversation";

    /// <summary>The action token required to unlink a conversation from a project.</summary>
    public const string UnlinkConversationAction = "projects:unlink_conversation";

    /// <summary>The action token required to confirm an ambiguous Project resolution.</summary>
    public const string ConfirmProjectResolutionAction = "projects:confirm_resolution";

    /// <summary>The action token required to set or replace a Project Folder reference.</summary>
    public const string SetProjectFolderAction = "projects:set_folder";

    /// <summary>The action token required to link an optional File Reference to a project.</summary>
    public const string LinkFileReferenceAction = "projects:link_file_reference";

    /// <summary>The action token required to unlink an optional File Reference from a project.</summary>
    public const string UnlinkFileReferenceAction = "projects:unlink_file_reference";

    /// <summary>The action token required to link a Memory Reference to a project.</summary>
    public const string LinkMemoryAction = "projects:link_memory";

    /// <summary>The action token required to unlink a Memory Reference from a project.</summary>
    public const string UnlinkMemoryAction = "projects:unlink_memory";

    /// <summary>Authorizes project creation.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeCreateAsync(
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            CreateProjectAction,
            projectId: null,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: false,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes a project detail read.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeReadAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            ReadProjectAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: true,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes a supported Project read with a caller-supplied persisted-detail loader.</summary>
    /// <param name="projectId">The target Project identifier.</param>
    /// <param name="tenantContext">The authenticated tenant and principal context.</param>
    /// <param name="httpContext">The authenticated callback HTTP context.</param>
    /// <param name="correlationId">The request correlation identifier.</param>
    /// <param name="taskId">The optional task identifier.</param>
    /// <param name="detailLoader">The supported EventStore read-model loader.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete ordered authorization result and authorized detail.</returns>
    public async Task<ProjectAuthorizationResult> AuthorizeSupportedReadAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        Func<string, string, CancellationToken, Task<ProjectDetailItem?>> detailLoader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detailLoader);
        return await AuthorizeAsync(
            tenantContext,
            httpContext,
            ReadProjectAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: true,
            requireProjectDetail: true,
            cancellationToken,
            detailLoader).ConfigureAwait(false);
    }

    /// <summary>Authorizes a project list read.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeListAsync(
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            ListProjectsAction,
            projectId: null,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: true,
            requireProjectDetail: false,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes a project setup update.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeUpdateSetupAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            UpdateProjectSetupAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes a project archive mutation.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeArchiveAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            ArchiveProjectAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes a project restore mutation.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeRestoreAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            RestoreProjectAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes linking a conversation to a target project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeLinkConversationAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            LinkConversationAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes moving a conversation into or out of a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeMoveConversationAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            MoveConversationAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes unlinking a conversation from a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeUnlinkConversationAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            UnlinkConversationAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes confirming an ambiguous Project resolution for a target or source project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeConfirmProjectResolutionAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            ConfirmProjectResolutionAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes setting or replacing a Project Folder reference.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeSetProjectFolderAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            SetProjectFolderAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes linking an optional File Reference to a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeLinkFileReferenceAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            LinkFileReferenceAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes unlinking an optional File Reference from a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeUnlinkFileReferenceAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            UnlinkFileReferenceAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes linking a Memory Reference to a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeLinkMemoryAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            LinkMemoryAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Authorizes unlinking a Memory Reference from a project.</summary>
    public async Task<ProjectAuthorizationResult> AuthorizeUnlinkMemoryAsync(
        string projectId,
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        CancellationToken cancellationToken = default)
        => await AuthorizeAsync(
            tenantContext,
            httpContext,
            UnlinkMemoryAction,
            projectId,
            correlationId,
            taskId,
            allowBoundedStaleTenantProjection: false,
            requireProjectDetail: true,
            cancellationToken).ConfigureAwait(false);

    private async Task<ProjectAuthorizationResult> AuthorizeAsync(
        IProjectTenantContextAccessor tenantContext,
        HttpContext httpContext,
        string actionToken,
        string? projectId,
        string? correlationId,
        string? taskId,
        bool allowBoundedStaleTenantProjection,
        bool requireProjectDetail,
        CancellationToken cancellationToken,
        Func<string, string, CancellationToken, Task<ProjectDetailItem?>>? supportedDetailLoader = null)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(httpContext);

        List<AuthorizationLayer> evaluatedLayers = [];

        evaluatedLayers.Add(AuthorizationLayer.JwtValidation);
        string? authoritativeTenantId = tenantContext.AuthoritativeTenantId?.Trim();
        string? principalId = tenantContext.PrincipalId?.Trim();
        if (string.IsNullOrWhiteSpace(authoritativeTenantId)
            || string.IsNullOrWhiteSpace(principalId)
            || string.Equals(authoritativeTenantId, ProjectsServerModule.ReservedSystemTenant, StringComparison.Ordinal))
        {
            return Deny(AuthorizationLayer.JwtValidation, ReferenceState.Unauthorized, "authentication_denied", retryable: false, evaluatedLayers);
        }

        evaluatedLayers.Add(AuthorizationLayer.EventStoreClaimTransform);
        EventStoreClaimTransformEvidence claimTransform = tenantContext.GetClaimTransformEvidence(actionToken);
        if (HasClientControlledMismatch(authoritativeTenantId, ClientControlledTenantValues(httpContext)))
        {
            return Deny(
                AuthorizationLayer.EventStoreClaimTransform,
                ReferenceState.TenantMismatch,
                "tenant_mismatch",
                retryable: false,
                evaluatedLayers);
        }

        if (HasClientControlledMismatch(principalId, ClientControlledPrincipalValues(httpContext))
            || !IsClaimTransformEvidenceValid(claimTransform, authoritativeTenantId, principalId, actionToken))
        {
            return Deny(
                AuthorizationLayer.EventStoreClaimTransform,
                claimTransform.Malformed ? ReferenceState.InvalidReference : ReferenceState.Unauthorized,
                claimTransform.Malformed ? "authorization_evidence_malformed" : "claim_transform_denied",
                retryable: false,
                evaluatedLayers);
        }

        evaluatedLayers.Add(AuthorizationLayer.TenantAccessFreshness);
        TenantAccessAuthorizationContext tenantAccessContext = new(authoritativeTenantId, principalId, RequestedTenantId: authoritativeTenantId);
        TenantAccessAuthorizationResult tenantAccess = allowBoundedStaleTenantProjection
            ? await tenantAccessAuthorizer.AuthorizeDiagnosticReadAsync(tenantAccessContext, cancellationToken).ConfigureAwait(false)
            : await tenantAccessAuthorizer.AuthorizeMutationAsync(tenantAccessContext, cancellationToken).ConfigureAwait(false);

        if (!tenantAccess.IsAllowed)
        {
            return Deny(
                AuthorizationLayer.TenantAccessFreshness,
                TenantAccessOutcomeReferenceStateMapper.ToReferenceState(tenantAccess.Outcome),
                tenantAccess.Code,
                retryable: tenantAccess.Outcome is TenantAccessOutcome.StaleProjection or TenantAccessOutcome.UnavailableProjection,
                evaluatedLayers,
                tenantAccess);
        }

        if (!string.Equals(tenantAccess.TenantId?.Trim(), authoritativeTenantId, StringComparison.Ordinal))
        {
            return Deny(
                AuthorizationLayer.TenantAccessFreshness,
                ReferenceState.InvalidReference,
                "authorization_evidence_malformed",
                retryable: false,
                evaluatedLayers,
                tenantAccess);
        }

        evaluatedLayers.Add(AuthorizationLayer.ProjectAcl);
        ProjectDetailItem? detail = null;
        if (requireProjectDetail)
        {
            try
            {
                detail = supportedDetailLoader is null
                    ? await projectDetailReadModel.GetAsync(authoritativeTenantId, projectId ?? string.Empty, cancellationToken).ConfigureAwait(false)
                    : await supportedDetailLoader(authoritativeTenantId, projectId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Deny(AuthorizationLayer.ProjectAcl, ReferenceState.Unavailable, "projection_unavailable", retryable: true, evaluatedLayers, tenantAccess);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Deny(AuthorizationLayer.ProjectAcl, ReferenceState.Unavailable, "projection_unavailable", retryable: true, evaluatedLayers, tenantAccess);
            }

            detail = ProjectQueryTenantFilter.Filter(authoritativeTenantId, detail);
            if (detail is null)
            {
                return Deny(AuthorizationLayer.ProjectAcl, ReferenceState.Unauthorized, "project_acl_denied", retryable: false, evaluatedLayers, tenantAccess);
            }
        }

        evaluatedLayers.Add(AuthorizationLayer.EventStoreValidator);
        EventStoreAuthorizationValidationResult validatorResult;
        try
        {
            validatorResult = await eventStoreAuthorizationValidator.ValidateAsync(
                new EventStoreAuthorizationValidationRequest(
                    authoritativeTenantId,
                    principalId,
                    actionToken,
                    projectId,
                    correlationId,
                    taskId,
                    evaluatedLayers.ToArray()),
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
                MapValidatorReason(validatorResult.Status),
                validatorResult.OutcomeCode,
                validatorResult.Retryable,
                evaluatedLayers,
                tenantAccess);
        }

        evaluatedLayers.Add(AuthorizationLayer.DaprDenyByDefaultPolicy);
        ProjectDaprPolicyEvidenceResult daprEvidence;
        try
        {
            daprEvidence = await daprPolicyEvidenceProvider
                .GetEvidenceAsync(actionToken, correlationId, taskId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            daprEvidence = ProjectDaprPolicyEvidenceResult.Unavailable();
        }

        if (daprEvidence.Status != ProjectDaprPolicyEvidenceStatus.Allowed)
        {
            return Deny(
                AuthorizationLayer.DaprDenyByDefaultPolicy,
                daprEvidence.Status == ProjectDaprPolicyEvidenceStatus.Unavailable ? ReferenceState.Unavailable : ReferenceState.Unauthorized,
                daprEvidence.OutcomeCode,
                daprEvidence.Retryable,
                evaluatedLayers,
                tenantAccess);
        }

        return ProjectAuthorizationResult.Allowed(evaluatedLayers.ToArray(), detail, tenantAccess) with
        {
            EventStoreValidationResult = validatorResult,
        };
    }

    private static ProjectAuthorizationResult Deny(
        AuthorizationLayer layer,
        ReferenceState reason,
        string code,
        bool retryable,
        IReadOnlyList<AuthorizationLayer> evaluatedLayers,
        TenantAccessAuthorizationResult? tenantAccessResult = null)
        => ProjectAuthorizationResult.Denied(layer, reason, code, retryable, evaluatedLayers.ToArray(), tenantAccessResult);

    private static bool IsClaimTransformEvidenceValid(
        EventStoreClaimTransformEvidence evidence,
        string authoritativeTenantId,
        string principalId,
        string actionToken)
        => evidence.IsPresent
            && !evidence.Malformed
            && string.Equals(evidence.TenantId?.Trim(), authoritativeTenantId, StringComparison.Ordinal)
            && string.Equals(evidence.PrincipalId?.Trim(), principalId, StringComparison.Ordinal)
            && evidence.HasPermissionFor(actionToken);

    private static ReferenceState MapValidatorReason(EventStoreAuthorizationValidationStatus status)
        => status switch
        {
            EventStoreAuthorizationValidationStatus.Malformed => ReferenceState.InvalidReference,
            EventStoreAuthorizationValidationStatus.Unavailable => ReferenceState.Unavailable,
            _ => ReferenceState.Unauthorized,
        };

    private static IReadOnlyDictionary<string, string?> ClientControlledTenantValues(HttpContext httpContext)
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        AddHeader(values, httpContext, "X-Tenant-Id");
        AddHeader(values, httpContext, "X-Hexalith-Tenant-Id");
        AddQuery(values, httpContext, "tenantId");
        AddQuery(values, httpContext, "tenant");
        return values;
    }

    private static IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues(HttpContext httpContext)
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        AddHeader(values, httpContext, "X-Principal-Id");
        AddHeader(values, httpContext, "X-User-Id");
        AddQuery(values, httpContext, "principalId");
        AddQuery(values, httpContext, "userId");
        return values;
    }

    private static void AddHeader(Dictionary<string, string?> values, HttpContext httpContext, string name)
    {
        if (httpContext.Request.Headers.TryGetValue(name, out StringValues headerValues))
        {
            values[name] = headerValues.FirstOrDefault();
        }
    }

    private static void AddQuery(Dictionary<string, string?> values, HttpContext httpContext, string name)
    {
        if (httpContext.Request.Query.TryGetValue(name, out StringValues queryValues))
        {
            values[name] = queryValues.FirstOrDefault();
        }
    }

    private static bool HasClientControlledMismatch(
        string? authoritativeValue,
        IReadOnlyDictionary<string, string?> comparisonValues)
    {
        if (comparisonValues.Count == 0)
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
            if (value.Length == 0 || !string.Equals(value, authoritative, StringComparison.Ordinal))
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
}
