using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

public static class FoldersDomainServiceEndpoints
{
    private const string FreshnessHeaderName = "X-Hexalith-Freshness";
    private const string EventuallyConsistent = "eventually_consistent";
    private const string ReadYourWrites = "read_your_writes";
    private const string SnapshotPerTask = "snapshot_per_task";
    private const int InlineContentByteLimit = 262144;
    private const int StreamContentMinimumBytes = 262145;
    private const int MaxBranchRefPatternCount = 16;

    // Outbound: ignore-when-null is fine because we serialize records.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Inbound: reject unknown fields so a client cannot smuggle additional properties into
    // the archive payload that would later be forwarded to the gateway/aggregate.
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // Gateway payload: outbound JSON forwarded to the EventStore gateway. Kept separate from
    // ResponseJsonOptions so HTTP-response tweaks (e.g. WriteIndented) cannot accidentally
    // change wire shape of the command payload.
    private static readonly JsonSerializerOptions GatewayPayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ReservedSystemTenant = "system";

    private static readonly System.Text.RegularExpressions.Regex CanonicalSegmentRegex =
        new("^[a-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Gateway-returned correlation IDs may carry uppercase hex (ULIDs, UUIDs) coming from
    // upstream systems. Caller-supplied identifiers stay strictly lowercase canonical to
    // preserve safe-comparison invariants; the gateway-corrected echo is validated against
    // this broader shape so traces remain joinable across hops.
    private static readonly System.Text.RegularExpressions.Regex GatewayCorrelationRegex =
        new("^[A-Za-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex BranchRefPolicyRegex =
        new("^branch_ref_[a-z0-9_]{3,80}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex CommitAuthorMetadataReferenceRegex =
        new("^authorref_[A-Za-z0-9_-]{6,118}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex CommitBranchRefTargetRegex =
        new("^branchref_[A-Za-z0-9_-]{6,118}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex CommitChangedPathMetadataDigestRegex =
        new("^digest_[A-Za-z0-9_-]{9,121}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex CommitMessageClassificationRegex =
        new("^[a-z][a-z0-9_]{0,79}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapFoldersDomainServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(FoldersServerModule.ProcessRoute, async (
            DomainServiceRequest request,
            FoldersDomainServiceRequestHandler handler,
            CancellationToken cancellationToken)
            => await handler.ProcessAsync(request, cancellationToken).ConfigureAwait(false))
        .WithName("ProcessFolderDomainCommand")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost(FoldersServerModule.ProjectRoute, (ProjectionRequest _) =>
            Results.Problem(
                type: "https://hexalith.dev/errors/folders/projection-not-implemented",
                title: "Folders projection endpoint is not implemented yet.",
                statusCode: StatusCodes.Status501NotImplemented,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "not_implemented",
                    ["code"] = "projection_not_implemented",
                }))
        .WithName("ProjectFolderDomainReadModel")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/effective-permissions", async (
            string folderId,
            HttpContext httpContext,
            EffectivePermissionsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            EffectivePermissionsQueryResult result = await handler.HandleAsync(
                new EffectivePermissionsQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId ?? string.Empty,
                    correlationId,
                    TaskContextId: ReadHeader(httpContext, "X-Hexalith-Task-Id"),
                    WorkspaceContextId: ReadHeader(httpContext, "X-Hexalith-Workspace-Id"),
                    ClientControlledTenantIds: ClientTenantIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(result, correlationId);
        })
        .WithName("GetEffectivePermissions")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/archive", async (
            string folderId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await ArchiveFolderAsync(
                folderId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("ArchiveFolder")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/repository-backed", async (
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await CreateRepositoryBackedFolderAsync(
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("CreateRepositoryBackedFolder")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/repository-bindings", async (
            string folderId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await BindRepositoryAsync(
                folderId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("BindRepository")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPut("/api/v1/folders/{folderId}/branch-ref-policy", async (
            string folderId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await ConfigureBranchRefPolicyAsync(
                folderId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("ConfigureBranchRefPolicy")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/preparation", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await PrepareWorkspaceAsync(
                folderId,
                workspaceId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("PrepareWorkspace")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await LockWorkspaceAsync(
                folderId,
                workspaceId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("LockWorkspace")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceLockStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
            if (idempotencyKey is not null)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "idempotency_key_not_allowed",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Idempotency-Key is not accepted on read operations.");
            }

            if (!IsCanonicalIdentifier(folderId)
                || !IsCanonicalIdentifier(workspaceId)
                || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
                || (taskId is not null && !IsCanonicalIdentifier(taskId)))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                    taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
            }

            string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
            if (requestedFreshness is not null
                && !string.Equals(requestedFreshness, ReadYourWrites, StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "unsupported_read_consistency",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Operation supports read_your_writes only.");
            }

            WorkspaceLockStatusQueryResult result = await handler.HandleAsync(
                new WorkspaceLockStatusQuery(
                    folderId,
                    workspaceId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence(WorkspaceLockStatusQueryHandler.ActionToken),
                    correlationId,
                    taskId,
                    ClientTenantIds(httpContext),
                    ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId, taskId);
        })
        .WithName("GetWorkspaceLock")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/status", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
            if (idempotencyKey is not null)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "idempotency_key_not_allowed",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Idempotency-Key is not accepted on read operations.");
            }

            if (!IsCanonicalIdentifier(folderId)
                || !IsCanonicalIdentifier(workspaceId)
                || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
                || (taskId is not null && !IsCanonicalIdentifier(taskId)))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                    taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
            }

            string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
            if (requestedFreshness is not null
                && !string.Equals(requestedFreshness, ReadYourWrites, StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "unsupported_read_consistency",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Operation supports read_your_writes only.");
            }

            WorkspaceStatusQueryResult result = await handler.HandleAsync(
                new WorkspaceStatusQuery(
                    folderId,
                    workspaceId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence(WorkspaceStatusQueryHandler.ActionToken),
                    correlationId,
                    taskId,
                    ClientTenantIds(httpContext),
                    ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId, taskId);
        })
        .WithName("GetWorkspaceStatus")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/tasks/{taskId}/status", async (
            string taskId,
            HttpContext httpContext,
            TaskStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            IResult? envelopeFailure = ValidateEvidenceQueryEnvelope(
                httpContext,
                correlationId,
                null,
                [taskId],
                requireEventuallyConsistent: true);
            if (envelopeFailure is not null)
            {
                return envelopeFailure;
            }

            TaskStatusQueryResult result = await handler.HandleAsync(
                new TaskStatusQuery(
                    taskId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence(TaskStatusQueryHandler.ActionToken),
                    correlationId,
                    ClientTenantIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId);
        })
        .WithName("GetTaskStatus")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence", async (
            string folderId,
            string workspaceId,
            string operationId,
            HttpContext httpContext,
            WorkspaceStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await WorkspaceEvidenceQueryAsync(
                WorkspaceEvidenceKind.CommitEvidence,
                folderId,
                workspaceId,
                operationId,
                null,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("GetCommitEvidence")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome", async (
            string folderId,
            string workspaceId,
            string operationId,
            HttpContext httpContext,
            WorkspaceStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await WorkspaceEvidenceQueryAsync(
                WorkspaceEvidenceKind.ProviderOutcome,
                folderId,
                workspaceId,
                operationId,
                null,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("GetProviderOutcome")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status", async (
            string folderId,
            string workspaceId,
            string reconciliationId,
            HttpContext httpContext,
            WorkspaceStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await WorkspaceEvidenceQueryAsync(
                WorkspaceEvidenceKind.ReconciliationStatus,
                folderId,
                workspaceId,
                null,
                reconciliationId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("GetReconciliationStatus")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/cleanup/status", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceCleanupStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
            if (idempotencyKey is not null)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "idempotency_key_not_allowed",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Idempotency-Key is not accepted on read operations.");
            }

            if (!IsCanonicalIdentifier(folderId)
                || !IsCanonicalIdentifier(workspaceId)
                || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
                || (taskId is not null && !IsCanonicalIdentifier(taskId)))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                    taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
            }

            string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
            if (requestedFreshness is not null
                && !string.Equals(requestedFreshness, ReadYourWrites, StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "unsupported_read_consistency",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Operation supports read_your_writes only.");
            }

            WorkspaceCleanupStatusQueryResult result = await handler.HandleAsync(
                new WorkspaceCleanupStatusQuery(
                    folderId,
                    workspaceId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence(WorkspaceCleanupStatusQueryHandler.ActionToken),
                    correlationId,
                    taskId,
                    ClientTenantIds(httpContext),
                    ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId, taskId);
        })
        .WithName("GetWorkspaceCleanupStatus")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await ReleaseWorkspaceLockAsync(
                folderId,
                workspaceId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("ReleaseWorkspaceLock")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await FileMutationAsync(
                folderId,
                workspaceId,
                expectedFileOperationKind: "add",
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("AddWorkspaceFile")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPut("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await FileMutationAsync(
                folderId,
                workspaceId,
                expectedFileOperationKind: "change",
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("ChangeWorkspaceFile")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await FileMutationAsync(
                folderId,
                workspaceId,
                expectedFileOperationKind: "remove",
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("RemoveWorkspaceFile")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            IEventStoreGatewayClient gateway,
            ITenantContextAccessor tenantContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
            => await CommitWorkspaceAsync(
                folderId,
                workspaceId,
                httpContext,
                gateway,
                tenantContext,
                timeProvider,
                cancellationToken).ConfigureAwait(false))
        .WithName("CommitWorkspace")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/tree", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceFileContextQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await FileContextQueryAsync(
                WorkspaceFileContextQueryKind.Tree,
                folderId,
                workspaceId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                paths: null,
                queryText: null,
                globPattern: null,
                startOffset: null,
                endOffset: null,
                cancellationToken).ConfigureAwait(false))
        .WithName("ListFolderFiles")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/metadata", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceFileContextQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            IResult? envelopeFailure = ValidateContextQueryEnvelope(folderId, workspaceId, httpContext, correlationId, taskId);
            if (envelopeFailure is not null)
            {
                return envelopeFailure;
            }

            FileMetadataContextHttpRequest? body = await ReadContextBodyAsync<FileMetadataContextHttpRequest>(httpContext, cancellationToken).ConfigureAwait(false);
            if (body is null || !IsSchemaVersionV1(body.RequestSchemaVersion))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: ReadHeader(httpContext, "X-Correlation-Id"),
                    taskId: ReadHeader(httpContext, "X-Hexalith-Task-Id"));
            }

            return await FileContextQueryAsync(
                WorkspaceFileContextQueryKind.Metadata,
                folderId,
                workspaceId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                body.Paths,
                queryText: null,
                globPattern: null,
                startOffset: null,
                endOffset: null,
                cancellationToken).ConfigureAwait(false);
        })
        .WithName("GetFolderFileMetadata")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/search", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceFileContextQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            IResult? envelopeFailure = ValidateContextQueryEnvelope(folderId, workspaceId, httpContext, correlationId, taskId);
            if (envelopeFailure is not null)
            {
                return envelopeFailure;
            }

            FileSearchContextHttpRequest? body = await ReadContextBodyAsync<FileSearchContextHttpRequest>(httpContext, cancellationToken).ConfigureAwait(false);
            if (body is null
                || !IsSchemaVersionV1(body.RequestSchemaVersion)
                || !string.Equals(body.QueryFamily, "search", StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: ReadHeader(httpContext, "X-Correlation-Id"),
                    taskId: ReadHeader(httpContext, "X-Hexalith-Task-Id"));
            }

            return await FileContextQueryAsync(
                WorkspaceFileContextQueryKind.Search,
                folderId,
                workspaceId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                body.RequestedPaths,
                body.QueryText,
                globPattern: null,
                startOffset: null,
                endOffset: null,
                cancellationToken,
                requestLimit: body.Limit,
                requestCursor: body.Cursor).ConfigureAwait(false);
        })
        .WithName("SearchFolderFiles")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/glob", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceFileContextQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            IResult? envelopeFailure = ValidateContextQueryEnvelope(folderId, workspaceId, httpContext, correlationId, taskId);
            if (envelopeFailure is not null)
            {
                return envelopeFailure;
            }

            FileGlobContextHttpRequest? body = await ReadContextBodyAsync<FileGlobContextHttpRequest>(httpContext, cancellationToken).ConfigureAwait(false);
            if (body is null
                || !IsSchemaVersionV1(body.RequestSchemaVersion)
                || !string.Equals(body.QueryFamily, "glob", StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: ReadHeader(httpContext, "X-Correlation-Id"),
                    taskId: ReadHeader(httpContext, "X-Hexalith-Task-Id"));
            }

            return await FileContextQueryAsync(
                WorkspaceFileContextQueryKind.Glob,
                folderId,
                workspaceId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                body.RequestedPaths,
                queryText: null,
                body.GlobPattern,
                startOffset: null,
                endOffset: null,
                cancellationToken,
                requestLimit: body.Limit,
                requestCursor: body.Cursor).ConfigureAwait(false);
        })
        .WithName("GlobFolderFiles")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/range-read", async (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            WorkspaceFileContextQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            IResult? envelopeFailure = ValidateContextQueryEnvelope(folderId, workspaceId, httpContext, correlationId, taskId);
            if (envelopeFailure is not null)
            {
                return envelopeFailure;
            }

            FileRangeReadContextHttpRequest? body = await ReadContextBodyAsync<FileRangeReadContextHttpRequest>(httpContext, cancellationToken).ConfigureAwait(false);
            if (body is null || !IsSchemaVersionV1(body.RequestSchemaVersion) || body.Path is null)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: ReadHeader(httpContext, "X-Correlation-Id"),
                    taskId: ReadHeader(httpContext, "X-Hexalith-Task-Id"));
            }

            return await FileContextQueryAsync(
                WorkspaceFileContextQueryKind.Range,
                folderId,
                workspaceId,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                [body.Path],
                queryText: null,
                globPattern: null,
                body.StartOffset,
                body.EndOffset,
                cancellationToken).ConfigureAwait(false);
        })
        .WithName("ReadFileRange")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/branch-ref-policy", async (
            string folderId,
            HttpContext httpContext,
            BranchRefPolicyQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
            string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
            if (idempotencyKey is not null)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "idempotency_key_not_allowed",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Idempotency-Key is not accepted on read operations.");
            }

            string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
            if (requestedFreshness is not null
                && !string.Equals(requestedFreshness, EventuallyConsistent, StringComparison.Ordinal))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "unsupported_read_consistency",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Operation supports eventually_consistent only.");
            }

            BranchRefPolicyQueryResult result = await handler.HandleAsync(
                new BranchRefPolicyQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence("read_branch_ref_policy"),
                    correlationId,
                    taskId,
                    ClientTenantIds(httpContext),
                    ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId, taskId);
        })
        .WithName("GetBranchRefPolicy")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/lifecycle-status", async (
            string folderId,
            HttpContext httpContext,
            FolderLifecycleStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

            // The operation declares x-hexalith-read-consistency.class: eventually_consistent.
            // A caller-supplied stricter class is silently invalid; surface as 400 instead of
            // silently downgrading to eventually_consistent.
            string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
            if (requestedFreshness is not null
                && !string.Equals(requestedFreshness, EventuallyConsistent, StringComparison.Ordinal))
            {
                return Results.Problem(
                    type: "https://hexalith.dev/errors/folders/validation_error",
                    title: "Unsupported read-consistency class.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?>
                    {
                        ["category"] = "validation_error",
                        ["code"] = "unsupported_read_consistency",
                        ["message"] = "Operation supports eventually_consistent only.",
                        ["correlationId"] = correlationId,
                        ["taskId"] = taskId,
                        ["retryable"] = false,
                        ["clientAction"] = "no_action",
                        ["details"] = new Dictionary<string, object?>
                        {
                            ["visibility"] = "metadata_only",
                        },
                    });
            }

            FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
                new FolderLifecycleStatusQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence("read_metadata"),
                    correlationId,
                    TaskId: taskId,
                    ClientControlledTenantValues: ClientTenantIds(httpContext),
                    ClientControlledPrincipalValues: ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(httpContext, result, correlationId, taskId);
        })
        .WithName("GetFolderLifecycleStatus")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        return endpoints;
    }

    private static async Task<IResult> ArchiveFolderAsync(
        string folderId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId: null,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        ArchiveFolderHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<ArchiveFolderHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!FolderArchiveReasonCodes.IsSupported(body.ArchiveReasonCode))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_archive_reason_code",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.ArchiveFolderCommandType,
                    Payload: JsonSerializer.SerializeToElement(body, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Gateway transport / serialization / Dapr failures must surface as a safe
            // retryable evidence-unavailable result, not a 500 with internal stack.
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        // Re-validate the gateway-returned correlation id before reflecting it into a
        // response header to avoid log/header injection via the gateway hop. Gateway-corrected
        // IDs may carry uppercase characters (UUIDs, ULIDs from upstream systems), so use the
        // broader gateway-correlation shape rather than the strict caller-side canonical shape.
        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> CreateRepositoryBackedFolderAsync(
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId: null,
            workspaceId: null,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        CreateRepositoryBackedFolderHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<CreateRepositoryBackedFolderHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidRepositoryBackedRequest(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        CreateRepositoryBackedFolderGatewayPayload gatewayPayload = new(
            body.RequestSchemaVersion,
            body.FolderId,
            body.BranchRefPolicy!.RepositoryBindingId,
            body.ProviderBindingRef,
            body.RepositoryProfileRef,
            body.FolderMetadata,
            body.BranchRefPolicy,
            CredentialScopeClass: "provider_binding");

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: body.FolderId!,
                    CommandType: FoldersServerModule.CreateRepositoryBackedFolderCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> BindRepositoryAsync(
        string folderId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId: null,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        BindRepositoryHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<BindRepositoryHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidBindRepositoryRequest(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.BindRepositoryCommandType,
                    Payload: JsonSerializer.SerializeToElement(body, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ConfigureBranchRefPolicyAsync(
        string folderId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId: null,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        BranchRefPolicyHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<BranchRefPolicyHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidBranchRefPolicy(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.ConfigureBranchRefPolicyCommandType,
                    Payload: JsonSerializer.SerializeToElement(body, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> PrepareWorkspaceAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        PrepareWorkspaceHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<PrepareWorkspaceHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidPrepareWorkspaceRequest(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        PrepareWorkspaceGatewayPayload gatewayPayload = new(
            body.RequestSchemaVersion,
            workspaceId,
            body.RepositoryBindingId,
            body.BranchRefPolicyRef,
            body.WorkspacePolicyRef);

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.PrepareWorkspaceCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> LockWorkspaceAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        LockWorkspaceHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<LockWorkspaceHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidLockWorkspaceRequest(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        LockWorkspaceGatewayPayload gatewayPayload = new(
            body.RequestSchemaVersion,
            workspaceId,
            body.LockIntent,
            body.RequestedLeaseSeconds);

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.LockWorkspaceCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ReleaseWorkspaceLockAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        ReleaseWorkspaceLockHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<ReleaseWorkspaceLockHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_request_schema_version",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "requestSchemaVersion must be exactly v1.");
        }

        if (!IsValidReleaseWorkspaceLockRequest(body))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        ReleaseWorkspaceLockGatewayPayload gatewayPayload = new(
            body.RequestSchemaVersion,
            workspaceId,
            body.LockId,
            body.LockOwnershipProof,
            body.ReleaseReasonCode);

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.ReleaseWorkspaceLockCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> FileMutationAsync(
        string folderId,
        string workspaceId,
        string expectedFileOperationKind,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        FileMutationHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<FileMutationHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (body is null
            || !string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal)
            || !string.Equals(body.FileOperationKind, expectedFileOperationKind, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        FileMutationTransportValidation validation = ValidateFileMutationRequest(body);
        if (!validation.IsAccepted)
        {
            string category = validation.Result switch
            {
                FileMutationRequestValidationResult.PathValidationFailed => "path_validation_failed",
                FileMutationRequestValidationResult.InputLimitExceeded => "input_limit_exceeded",
                _ => "validation_error",
            };
            int statusCode = validation.Result == FileMutationRequestValidationResult.InputLimitExceeded
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status400BadRequest;
            if (validation.Result == FileMutationRequestValidationResult.InputLimitExceeded)
            {
                httpContext.Response.Headers["X-Hexalith-Retry-Transport"] = "stream";
            }

            return SafeProblem(
                statusCode,
                category: category,
                code: category,
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        FileMutationGatewayPayload gatewayPayload = new(
            body.RequestSchemaVersion,
            workspaceId,
            body.OperationId,
            body.FileOperationKind,
            body.TransportOperation,
            body.PathMetadata,
            body.ContentHashReference,
            body.ByteLength,
            validation.MediaType,
            validation.TransportEvidenceKind,
            validation.ObservedByteLength);

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.MutateFilesCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> CommitWorkspaceAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        IEventStoreGatewayClient gateway,
        ITenantContextAccessor tenantContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IResult? envelopeFailure = ValidateMutationEnvelope(
            httpContext,
            tenantContext,
            folderId,
            workspaceId,
            out MutationCommandEnvelope envelope);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        string idempotencyKey = envelope.IdempotencyKey;
        string correlationId = envelope.CorrelationId;
        string taskId = envelope.TaskId;

        CommitWorkspaceHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<CommitWorkspaceHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (!IsValidCommitWorkspaceRequest(body, taskId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        CommitWorkspaceGatewayPayload gatewayPayload = new(
            body!.RequestSchemaVersion,
            workspaceId,
            body.OperationId,
            body.TaskId,
            body.BranchRefTarget,
            body.ChangedPathMetadataDigest,
            body.AuthorMetadataReference,
            body.CommitMessageClassification,
            body.AuditMetadataKeys);

        SubmitCommandResponse submitted;
        try
        {
            submitted = await gateway.SubmitCommandAsync(
                new SubmitCommandRequest(
                    MessageId: idempotencyKey,
                    Tenant: envelope.TenantId,
                    Domain: FoldersServerModule.DomainName,
                    AggregateId: folderId,
                    CommandType: FoldersServerModule.CommitWorkspaceCommandType,
                    Payload: JsonSerializer.SerializeToElement(gatewayPayload, GatewayPayloadJsonOptions),
                    CorrelationId: correlationId,
                    Extensions: new Dictionary<string, string>
                    {
                        ["taskId"] = taskId,
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (EventStoreGatewayException ex)
        {
            return ToArchiveGatewayProblem(ex, correlationId, taskId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId);
        }

        string acceptedCorrelationId = !string.IsNullOrWhiteSpace(submitted.CorrelationId)
            && IsSafeGatewayCorrelationId(submitted.CorrelationId)
            ? submitted.CorrelationId
            : correlationId;

        httpContext.Response.Headers["X-Correlation-Id"] = acceptedCorrelationId;
        httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;

        return Results.Json(
            new AcceptedCommandResponse(
                timeProvider.GetUtcNow(),
                acceptedCorrelationId,
                taskId,
                "accepted",
                IdempotentReplay: IsIdempotentReplay(submitted.ResultPayload)),
            ResponseJsonOptions,
            statusCode: StatusCodes.Status202Accepted);
    }

    private static bool IsValidRepositoryBackedRequest(CreateRepositoryBackedFolderHttpRequest? body)
        => body is not null
        && IsCanonicalIdentifier(body.FolderId)
        && IsCanonicalIdentifier(body.ProviderBindingRef)
        && IsCanonicalIdentifier(body.RepositoryProfileRef)
        && IsValidFolderMetadata(body.FolderMetadata)
        && IsValidBranchRefPolicy(body.BranchRefPolicy);

    private static bool IsValidBindRepositoryRequest(BindRepositoryHttpRequest? body)
        => body is not null
        && IsCanonicalIdentifier(body.ProviderBindingRef)
        && IsCanonicalIdentifier(body.ExternalRepositoryRef)
        && IsValidBranchRefPolicy(body.BranchRefPolicy);

    private static bool IsValidPrepareWorkspaceRequest(PrepareWorkspaceHttpRequest? body)
        => body is not null
        && IsCanonicalIdentifier(body.RepositoryBindingId)
        && IsCanonicalIdentifier(body.BranchRefPolicyRef)
        && IsCanonicalIdentifier(body.WorkspacePolicyRef);

    private static bool IsValidLockWorkspaceRequest(LockWorkspaceHttpRequest? body)
        => body is not null
        && string.Equals(body.LockIntent, "exclusive_write", StringComparison.Ordinal)
        && body.RequestedLeaseSeconds is >= 1 and <= 86400;

    private static bool IsValidReleaseWorkspaceLockRequest(ReleaseWorkspaceLockHttpRequest? body)
        => body is not null
        && IsCanonicalIdentifier(body.LockId)
        && IsCanonicalIdentifier(body.LockOwnershipProof)
        && body.ReleaseReasonCode is "caller_completed"
            or "caller_abandoned"
            or "operator_requested"
            or "authorization_revoked"
            or "task_cancelled"
            or "lock_revoked";

    private static bool IsValidCommitWorkspaceRequest(CommitWorkspaceHttpRequest? body, string taskId)
        => body is not null
        && string.Equals(body.RequestSchemaVersion, "v1", StringComparison.Ordinal)
        && IsCanonicalIdentifier(body.OperationId)
        && string.Equals(body.TaskId, taskId, StringComparison.Ordinal)
        && IsCommitAuthorMetadataReference(body.AuthorMetadataReference)
        && IsCommitBranchRefTarget(body.BranchRefTarget)
        && IsCommitChangedPathMetadataDigest(body.ChangedPathMetadataDigest)
        && IsCommitMessageClassification(body.CommitMessageClassification)
        && body.AuditMetadataKeys is not null
        && body.AuditMetadataKeys.Count is >= 1 and <= 24
        && body.AuditMetadataKeys.Distinct(StringComparer.Ordinal).Count() == body.AuditMetadataKeys.Count
        && body.AuditMetadataKeys.All(IsCommitMessageClassification);

    private static bool IsCommitAuthorMetadataReference(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CommitAuthorMetadataReferenceRegex.IsMatch(value);

    private static bool IsCommitBranchRefTarget(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CommitBranchRefTargetRegex.IsMatch(value);

    private static bool IsCommitChangedPathMetadataDigest(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CommitChangedPathMetadataDigestRegex.IsMatch(value);

    private static bool IsCommitMessageClassification(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && CommitMessageClassificationRegex.IsMatch(value);

    private static FileMutationTransportValidation ValidateFileMutationRequest(FileMutationHttpRequest? body)
    {
        if (body is null
            || !IsCanonicalIdentifier(body.OperationId)
            || body.PathMetadata is null)
        {
            return FileMutationTransportValidation.Rejected(body?.PathMetadata is null
                ? FileMutationRequestValidationResult.PathValidationFailed
                : FileMutationRequestValidationResult.ValidationFailed);
        }

        WorkspacePathPolicyResult pathPolicy = WorkspacePathPolicyValidator.Validate(new PathMetadata(
            body.PathMetadata.NormalizedPath ?? string.Empty,
            body.PathMetadata.DisplayName ?? string.Empty,
            body.PathMetadata.PathPolicyClass ?? string.Empty,
            body.PathMetadata.UnicodeNormalization ?? string.Empty));
        if (!pathPolicy.IsAccepted)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.PathValidationFailed);
        }

        return body.FileOperationKind switch
        {
            "add" or "change" => ValidateAddOrChangeFileMutationRequest(body),
            "remove" => string.Equals(body.TransportOperation, "metadataOnlyRemoval", StringComparison.Ordinal)
                && body.ContentHashReference is null
                && body.ByteLength is null
                && body.InlineContent is null
                && body.StreamDescriptor is null
                    ? FileMutationTransportValidation.Accepted(null, null, null)
                    : FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed),
            _ => FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed),
        };
    }

    private static FileMutationTransportValidation ValidateAddOrChangeFileMutationRequest(FileMutationHttpRequest body)
    {
        if (!IsCanonicalIdentifier(body.ContentHashReference) || body.ByteLength is null)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        return body.TransportOperation switch
        {
            "PutFileInline" => ValidateInlineContent(body),
            "PutFileStream" => ValidateStreamContent(body),
            _ => FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed),
        };
    }

    private static FileMutationTransportValidation ValidateInlineContent(FileMutationHttpRequest body)
    {
        if (body.InlineContent is null || body.StreamDescriptor is not null)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        JsonElement inline = body.InlineContent.Value;
        if (inline.ValueKind != JsonValueKind.Object
            || !TryGetStringProperty(inline, "mediaType", out string? mediaType)
            || !IsValidMediaType(mediaType)
            || !TryGetStringProperty(inline, "contentBytes", out string? contentBytes)
            || contentBytes is null
            || string.IsNullOrWhiteSpace(contentBytes) && body.ByteLength != 0
            || HasWhitespace(contentBytes))
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(contentBytes);
        }
        catch (FormatException)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        if (decoded.LongLength != body.ByteLength)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        return decoded.LongLength > InlineContentByteLimit
            ? FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.InputLimitExceeded)
            : FileMutationTransportValidation.Accepted(mediaType, "inline_decoded", decoded.LongLength);
    }

    private static FileMutationTransportValidation ValidateStreamContent(FileMutationHttpRequest body)
    {
        if (body.StreamDescriptor is null || body.InlineContent is not null)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        JsonElement stream = body.StreamDescriptor.Value;
        if (stream.ValueKind != JsonValueKind.Object
            || !TryGetStringProperty(stream, "mediaType", out string? mediaType)
            || !IsValidMediaType(mediaType)
            || !TryGetInt64Property(stream, "declaredLength", out long declaredLength)
            || !TryGetInt64Property(stream, "observedLength", out long observedLength)
            || !TryGetStringProperty(stream, "stagingReference", out string? stagingReference)
            || !IsCanonicalIdentifier(stagingReference)
            || !TryGetStringProperty(stream, "observedContentHashReference", out string? observedContentHashReference)
            || !string.Equals(observedContentHashReference, body.ContentHashReference, StringComparison.Ordinal)
            || !TryGetStringProperty(stream, "uploadMode", out string? uploadMode)
            || !string.Equals(uploadMode, "request_body_stream", StringComparison.Ordinal)
            || declaredLength < StreamContentMinimumBytes
            || declaredLength != body.ByteLength
            || observedLength != body.ByteLength)
        {
            return FileMutationTransportValidation.Rejected(FileMutationRequestValidationResult.ValidationFailed);
        }

        return FileMutationTransportValidation.Accepted(mediaType, "stream_observed", observedLength);
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return value is not null;
    }

    private static bool TryGetInt64Property(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value);
    }

    private static bool IsValidMediaType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        int slash = value.IndexOf('/');
        return slash > 0
            && slash < value.Length - 1
            && IsMediaToken(value[..slash])
            && IsMediaToken(value[(slash + 1)..]);
    }

    private static bool IsMediaToken(string value)
    {
        foreach (char c in value)
        {
            bool accepted = char.IsAsciiLetterOrDigit(c)
                || c is '!' or '#' or '$' or '&' or '^' or '_' or '.' or '+' or '-';
            if (!accepted)
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool HasWhitespace(string value)
    {
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidFolderMetadata(FolderMetadataHttpRequest? metadata)
        => metadata is not null
        && !string.IsNullOrWhiteSpace(metadata.DisplayName)
        && metadata.DisplayName.Length <= 160
        && string.Equals(metadata.MetadataClass, "tenant_sensitive", StringComparison.Ordinal);

    private static bool IsValidBranchRefPolicy(BranchRefPolicyHttpRequest? policy)
        => policy is not null
        && string.Equals(policy.RequestSchemaVersion, "v1", StringComparison.Ordinal)
        && IsCanonicalIdentifier(policy.RepositoryBindingId)
        && IsCanonicalIdentifier(policy.PolicyRef)
        && IsBranchRefPolicyIdentifier(policy.DefaultRef)
        && AreRequiredBranchRefPatterns(policy.AllowedRefPatterns)
        && AreOptionalBranchRefPatterns(policy.ProtectedRefPatterns)
        && !HasDuplicateBranchRefPatterns(policy.AllowedRefPatterns)
        && !HasDuplicateBranchRefPatterns(policy.ProtectedRefPatterns)
        && !HasDuplicateBranchRefPatterns(policy.AllowedRefPatterns, policy.ProtectedRefPatterns);

    private static bool IsBranchRefPolicyIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 14 and <= 91
        && BranchRefPolicyRegex.IsMatch(value);

    private static bool AreRequiredBranchRefPatterns(IReadOnlyList<string>? values)
        => values is not null
        && values.Count is >= 1 and <= MaxBranchRefPatternCount
        && values.All(static value => IsBranchRefPolicyIdentifier(value));

    private static bool AreOptionalBranchRefPatterns(IReadOnlyList<string>? values)
        => values is null
        || (values.Count <= MaxBranchRefPatternCount
            && values.All(static value => IsBranchRefPolicyIdentifier(value)));

    private static bool HasDuplicateBranchRefPatterns(IReadOnlyList<string>? values)
        => values is not null
        && values.Distinct(StringComparer.Ordinal).Count() != values.Count;

    private static bool HasDuplicateBranchRefPatterns(IReadOnlyList<string>? allowed, IReadOnlyList<string>? protectedPatterns)
        => allowed is not null
        && protectedPatterns is not null
        && allowed.Intersect(protectedPatterns, StringComparer.Ordinal).Any();

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CanonicalSegmentRegex.IsMatch(value);

    private static bool IsSafeGatewayCorrelationId(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && GatewayCorrelationRegex.IsMatch(value);

    private static IResult? ValidateMutationEnvelope(
        HttpContext httpContext,
        ITenantContextAccessor tenantContext,
        string? folderId,
        string? workspaceId,
        out MutationCommandEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(tenantContext);

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
        envelope = new MutationCommandEnvelope(string.Empty, string.Empty, string.Empty, string.Empty);

        if (!IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(correlationId)
            || !IsCanonicalIdentifier(taskId)
            || (folderId is not null && !IsCanonicalIdentifier(folderId))
            || (workspaceId is not null && !IsCanonicalIdentifier(workspaceId)))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
        }

        if (string.IsNullOrWhiteSpace(tenantContext.AuthoritativeTenantId))
        {
            return SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "authentication_failure",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        if (string.Equals(tenantContext.AuthoritativeTenantId, ReservedSystemTenant, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        envelope = new MutationCommandEnvelope(
            idempotencyKey!,
            correlationId!,
            taskId!,
            tenantContext.AuthoritativeTenantId!);
        return null;
    }

    private static bool IsIdempotentReplay(JsonElement? resultPayload)
    {
        if (resultPayload is null || resultPayload.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement root = resultPayload.Value;
        if (root.TryGetProperty("idempotentReplay", out JsonElement replay)
            && replay.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return replay.GetBoolean();
        }

        if (TryReadString(root, "code", out string? code)
            || TryReadString(root, "status", out code)
            || TryReadString(root, "result", out code))
        {
            return string.Equals(code, "idempotent_replay", StringComparison.Ordinal)
                || string.Equals(code, "IdempotentReplay", StringComparison.Ordinal);
        }

        return false;
    }

    private static IResult ToArchiveGatewayProblem(EventStoreGatewayException exception, string correlationId, string taskId)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string safeCorrelationId = IsSafeGatewayCorrelationId(exception.CorrelationId)
            ? exception.CorrelationId!
            : correlationId;
        string? reasonCode = SafeGatewayReasonCode(exception.ReasonCode);

        if (exception.StatusCode == StatusCodes.Status409Conflict && reasonCode == "duplicate_binding")
        {
            return SafeProblem(
                StatusCodes.Status409Conflict,
                category: "duplicate_binding",
                code: "duplicate_binding",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status429TooManyRequests && reasonCode == "provider_rate_limited")
        {
            return SafeProblem(
                StatusCodes.Status429TooManyRequests,
                category: "provider_rate_limited",
                code: "provider_rate_limited",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status503ServiceUnavailable && reasonCode == "provider_unavailable")
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "provider_unavailable",
                code: "provider_unavailable",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity && reasonCode == "provider_readiness_failed")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: "provider_readiness_failed",
                code: "provider_readiness_failed",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity
            && reasonCode is "workspace_preparation_failed" or "workspace_transition_invalid")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: reasonCode,
                code: reasonCode,
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status503ServiceUnavailable && reasonCode == "unknown_provider_outcome")
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "unknown_provider_outcome",
                code: "unknown_provider_outcome",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status409Conflict && reasonCode == "reconciliation_required")
        {
            return SafeProblem(
                StatusCodes.Status409Conflict,
                category: "reconciliation_required",
                code: "reconciliation_required",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity && reasonCode == "file_operation_failed")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: "file_operation_failed",
                code: "file_operation_failed",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity
            && reasonCode is "commit_failed" or "provider_failure_known")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: reasonCode,
                code: reasonCode,
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status409Conflict && reasonCode == "idempotency_conflict")
        {
            return SafeProblem(
                StatusCodes.Status409Conflict,
                category: "idempotency_conflict",
                code: "idempotency_conflict",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status503ServiceUnavailable
            && reasonCode is "read_model_unavailable" or "projection_stale" or "projection_unavailable")
        {
            return SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: reasonCode,
                code: reasonCode,
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity && reasonCode == "unsupported_provider_capability")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: "unsupported_provider_capability",
                code: "unsupported_provider_capability",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status409Conflict
            && reasonCode is "lock_conflict" or "workspace_locked")
        {
            return SafeProblem(
                StatusCodes.Status409Conflict,
                category: reasonCode,
                code: reasonCode,
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status409Conflict && reasonCode == "lock_not_owned")
        {
            return SafeProblem(
                StatusCodes.Status409Conflict,
                category: "lock_not_owned",
                code: "lock_not_owned",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status410Gone && reasonCode == "lock_expired")
        {
            return SafeProblem(
                StatusCodes.Status410Gone,
                category: "lock_expired",
                code: "lock_expired",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        if (exception.StatusCode == StatusCodes.Status422UnprocessableEntity
            && reasonCode is "path_policy_denied" or "path_validation_failed")
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: reasonCode,
                code: reasonCode,
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId);
        }

        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId),
            StatusCodes.Status401Unauthorized => SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "authentication_failure",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId),
            StatusCodes.Status404NotFound => SafeProblem(
                StatusCodes.Status404NotFound,
                category: "not_found",
                code: "not_found",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId),
            StatusCodes.Status409Conflict => SafeProblem(
                StatusCodes.Status409Conflict,
                category: "idempotency_conflict",
                code: "idempotency_conflict",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId),
            StatusCodes.Status429TooManyRequests => SafeProblem(
                StatusCodes.Status429TooManyRequests,
                category: "provider_rate_limited",
                code: "provider_rate_limited",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId),
            StatusCodes.Status503ServiceUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId),
            // Any 5xx from the gateway (500, 502, 504) is an upstream failure, not an
            // authorization decision. Surfacing it as 403 denied_safe would be active
            // misinformation to operators chasing a backend incident. Map to safe 503.
            >= 500 and < 600 => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "evidence_unavailable",
                retryable: true,
                correlationId: safeCorrelationId,
                taskId: taskId),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: safeCorrelationId,
                taskId: taskId),
        };
    }

    private static string? SafeGatewayReasonCode(string? reasonCode)
        => reasonCode switch
        {
            "duplicate_binding" => "duplicate_binding",
            "ProviderRateLimited" => "provider_rate_limited",
            "provider_rate_limited" => "provider_rate_limited",
            "provider_readiness_failed" => "provider_readiness_failed",
            "workspace_preparation_failed" => "workspace_preparation_failed",
            "workspace-preparation-failed" => "workspace_preparation_failed",
            "workspace_transition_invalid" => "workspace_transition_invalid",
            "workspace-transition-invalid" => "workspace_transition_invalid",
            "workspace-transition-invalid-rejected" => "workspace_transition_invalid",
            "lock_conflict" => "lock_conflict",
            "lock-conflict" => "lock_conflict",
            "duplicate-workspace-lock" => "lock_conflict",
            "duplicate-workspace-lock-rejected" => "lock_conflict",
            "lock_not_owned" => "lock_not_owned",
            "lock-not-owned" => "lock_not_owned",
            "LockNotOwned" => "lock_not_owned",
            "lock_expired" => "lock_expired",
            "lock-expired" => "lock_expired",
            "LockExpired" => "lock_expired",
            "path_policy_denied" => "path_policy_denied",
            "path-policy-denied" => "path_policy_denied",
            "PathPolicyDenied" => "path_policy_denied",
            "path_validation_failed" => "path_validation_failed",
            "path-validation-failed" => "path_validation_failed",
            "PathValidationFailed" => "path_validation_failed",
            "workspace_locked" => "workspace_locked",
            "workspace-locked" => "workspace_locked",
            "workspace-already-locked" => "workspace_locked",
            "workspace-already-locked-rejected" => "workspace_locked",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "unknown-provider-outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            "reconciliation-required" => "reconciliation_required",
            "file_operation_failed" => "file_operation_failed",
            "file-operation-failed" => "file_operation_failed",
            "FileOperationFailed" => "file_operation_failed",
            "commit_failed" => "commit_failed",
            "commit-failed" => "commit_failed",
            "CommitFailed" => "commit_failed",
            "provider_failure_known" => "provider_failure_known",
            "provider-failure-known" => "provider_failure_known",
            "ProviderFailureKnown" => "provider_failure_known",
            "idempotency_conflict" => "idempotency_conflict",
            "idempotency-conflict" => "idempotency_conflict",
            "IdempotencyConflict" => "idempotency_conflict",
            "read_model_unavailable" => "read_model_unavailable",
            "read-model-unavailable" => "read_model_unavailable",
            "projection_stale" => "projection_stale",
            "projection-stale" => "projection_stale",
            "projection_unavailable" => "projection_unavailable",
            "projection-unavailable" => "projection_unavailable",
            "unsupported_provider_capability" => "unsupported_provider_capability",
            "unsupported-provider-capability" => "unsupported_provider_capability",
            "UnsupportedProviderCapability" => "unsupported_provider_capability",
            "provider_unavailable" => "provider_unavailable",
            "provider-unavailable" => "provider_unavailable",
            _ => null,
        };

    private static IResult ToHttpResult(HttpContext httpContext, FolderLifecycleStatusQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case FolderLifecycleStatusResultCode.Allowed:
                // Defensive: an Allowed result with null FolderId or LifecycleState is a
                // handler invariant break. Fail closed rather than synthesizing a Contract
                // Spine value the caller is told to trust.
                if (string.IsNullOrWhiteSpace(result.FolderId) || string.IsNullOrWhiteSpace(result.LifecycleState))
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: taskId);
                }

                AddLifecycleSuccessHeaders(httpContext, result);
                return Results.Json(
                    new FolderLifecycleStatusResponse(
                        result.FolderId,
                        result.LifecycleState,
                        result.Archived,
                        result.RepositoryBindingId,
                        result.ProviderBindingRef,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale)),
                    ResponseJsonOptions);

            case FolderLifecycleStatusResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.ArchiveStateUnsupported:
                // Permanent until Story 2.8 lands — non-retryable.
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "internal_error",
                    code: "archive_state_unsupported",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case FolderLifecycleStatusResultCode.AuthorizationDenied:
            default:
                // AuthorizationDenied without AuthorizationDenial details is a handler
                // invariant break — fail closed to read_model_unavailable.
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "internal_error",
                    code: "read_model_unavailable",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static async Task<IResult> FileContextQueryAsync(
        WorkspaceFileContextQueryKind kind,
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        WorkspaceFileContextQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        IReadOnlyList<PathMetadata>? paths,
        string? queryText,
        string? globPattern,
        long? startOffset,
        long? endOffset,
        CancellationToken cancellationToken,
        int? requestLimit = null,
        string? requestCursor = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        IResult? envelopeFailure = ValidateContextQueryEnvelope(
            folderId,
            workspaceId,
            httpContext,
            correlationId,
            taskId);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        int? limit = requestLimit ?? ReadOptionalIntQuery(httpContext, "limit");
        string? cursor = requestCursor ?? ReadQuery(httpContext, "cursor");
        string actionToken = kind == WorkspaceFileContextQueryKind.Range
            ? WorkspaceFileContextQueryHandler.ContentActionToken
            : WorkspaceFileContextQueryHandler.MetadataActionToken;

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            new WorkspaceFileContextQuery(
                kind,
                folderId,
                workspaceId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(actionToken),
                correlationId,
                taskId,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext),
                paths,
                queryText,
                globPattern,
                limit,
                cursor,
                startOffset,
                endOffset),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result, correlationId, taskId);
    }

    private static IResult? ValidateContextQueryEnvelope(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        string? correlationId,
        string? taskId)
    {
        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        if (idempotencyKey is not null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "idempotency_key_not_allowed",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Idempotency-Key is not accepted on read operations.");
        }

        if (!IsCanonicalIdentifier(folderId)
            || !IsCanonicalIdentifier(workspaceId)
            || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
            || (taskId is not null && !IsCanonicalIdentifier(taskId)))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
        }

        string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
        if (requestedFreshness is not null
            && !string.Equals(requestedFreshness, SnapshotPerTask, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_read_consistency",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Operation supports snapshot_per_task only.");
        }

        string? limit = ReadQuery(httpContext, "limit");
        if (limit is not null && ReadOptionalIntQuery(httpContext, "limit") is null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        string? cursor = ReadQuery(httpContext, "cursor");
        if (cursor is { Length: > 256 })
        {
            return SafeProblem(
                StatusCodes.Status422UnprocessableEntity,
                category: "input_limit_exceeded",
                code: "input_limit_exceeded",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        return null;
    }

    private static async Task<T?> ReadContextBodyAsync<T>(HttpContext httpContext, CancellationToken cancellationToken)
    {
        try
        {
            return await httpContext.Request
                .ReadFromJsonAsync<T>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static bool IsSchemaVersionV1(string? requestSchemaVersion)
        => string.Equals(requestSchemaVersion, "v1", StringComparison.Ordinal);

    private static int? ReadOptionalIntQuery(HttpContext httpContext, string name)
    {
        string? raw = ReadQuery(httpContext, name);
        return raw is null
            ? null
            : int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value)
                ? value
                : null;
    }

    private static IResult ToHttpResult(HttpContext httpContext, WorkspaceFileContextQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case WorkspaceFileContextResultCode.Allowed:
                AddFileContextSuccessHeaders(httpContext, result);
                if (result.Kind == WorkspaceFileContextQueryKind.Range)
                {
                    if (result.RangePath is null || result.Range is null || result.ContentBytes is null)
                    {
                        return SafeProblem(
                            StatusCodes.Status503ServiceUnavailable,
                            category: "read_model_unavailable",
                            code: "read_model_unavailable",
                            retryable: true,
                            correlationId: correlationId,
                            taskId: taskId);
                    }

                    return Results.Json(
                        new FileRangeReadResultResponse(
                            result.RangePath,
                            result.Range,
                            result.ContentBytes,
                            result.Limits,
                            ToFreshnessResponse(result.Freshness)),
                        options: ResponseJsonOptions,
                        statusCode: result.Range.Partial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK);
                }

                if (result.Kind == WorkspaceFileContextQueryKind.Metadata)
                {
                    return Results.Json(
                        new FileMetadataResultResponse(
                            result.Items,
                            result.Limits,
                            ToFreshnessResponse(result.Freshness)),
                        ResponseJsonOptions);
                }

                return Results.Json(
                    new FileTreeResultResponse(
                        result.Items,
                        result.Page ?? new WorkspaceFileContextPage(null, result.Limits.ConfiguredLimit, false, null),
                        result.Limits,
                        ToFreshnessResponse(result.Freshness)),
                    ResponseJsonOptions);

            case WorkspaceFileContextResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.ValidationFailed:
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.PathValidationFailed:
                return SafeProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    category: "path_validation_failed",
                    code: "path_validation_failed",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.InputLimitExceeded:
                return SafeProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    category: "input_limit_exceeded",
                    code: "input_limit_exceeded",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.ResponseLimitExceeded:
                return SafeProblem(
                    StatusCodes.Status413PayloadTooLarge,
                    category: "response_limit_exceeded",
                    code: "response_limit_exceeded",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.QueryTimeout:
                return SafeProblem(
                    StatusCodes.Status408RequestTimeout,
                    category: "query_timeout",
                    code: "query_timeout",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.Redacted:
                return SafeProblem(
                    result.Kind == WorkspaceFileContextQueryKind.Range
                        ? StatusCodes.Status416RangeNotSatisfiable
                        : StatusCodes.Status404NotFound,
                    category: "redacted",
                    code: "redacted",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.RangeUnsatisfiable:
                return SafeProblem(
                    StatusCodes.Status416RangeNotSatisfiable,
                    category: "range_unsatisfiable",
                    code: "range_unsatisfiable",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceFileContextResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static FreshnessMetadataResponse ToFreshnessResponse(FolderLifecycleFreshness freshness)
        => new(
            freshness.ReadConsistency,
            freshness.ObservedAt,
            freshness.ProjectionWatermark,
            freshness.Stale);

    private static IResult ToHttpResult(HttpContext httpContext, BranchRefPolicyQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case BranchRefPolicyQueryResultCode.Allowed:
                if (string.IsNullOrWhiteSpace(result.FolderId)
                    || string.IsNullOrWhiteSpace(result.RepositoryBindingId)
                    || string.IsNullOrWhiteSpace(result.PolicyRef)
                    || string.IsNullOrWhiteSpace(result.DefaultRef)
                    || result.AllowedRefPatterns.Count == 0)
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: taskId);
                }

                AddBranchRefPolicySuccessHeaders(httpContext, result);
                return Results.Json(
                    new BranchRefPolicyResponse(
                        "v1",
                        result.RepositoryBindingId,
                        result.PolicyRef,
                        result.DefaultRef,
                        result.AllowedRefPatterns,
                        result.ProtectedRefPatterns.Count == 0 ? null : result.ProtectedRefPatterns,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale)),
                    ResponseJsonOptions);

            case BranchRefPolicyQueryResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case BranchRefPolicyQueryResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case BranchRefPolicyQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case BranchRefPolicyQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case BranchRefPolicyQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case BranchRefPolicyQueryResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static IResult ToHttpResult(HttpContext httpContext, WorkspaceLockStatusQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case WorkspaceLockStatusQueryResultCode.Allowed:
                if (string.IsNullOrWhiteSpace(result.WorkspaceId))
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: taskId);
                }

                AddWorkspaceLockSuccessHeaders(httpContext, result);
                return Results.Json(
                    new WorkspaceLockStatusResponse(
                        new RedactableIdentifierResponse(
                            result.WorkspaceId,
                            "operator_sanitized",
                            new RedactionMetadataResponse("metadata_only", "not_redacted")),
                        result.LockState,
                        result.Lease is null
                            ? null
                            : new LockLeaseMetadataResponse(
                                result.Lease.LockId,
                                result.Lease.LeaseStatus,
                                result.Lease.AcquiredAt,
                                result.Lease.EffectiveAt,
                                result.Lease.ExpiresAt,
                                result.Lease.HolderRef),
                        new WorkspaceRetryEligibilityResponse(
                            result.RetryEligibility.Retryable,
                            result.RetryEligibility.RetryAfterSeconds,
                            result.RetryEligibility.ReasonCode,
                            result.RetryEligibility.CorrelationId,
                            result.RetryEligibility.TaskId,
                            result.RetryEligibility.CurrentState,
                            new FreshnessMetadataResponse(
                                result.RetryEligibility.Freshness.ReadConsistency,
                                result.RetryEligibility.Freshness.ObservedAt,
                                result.RetryEligibility.Freshness.ProjectionWatermark,
                                result.RetryEligibility.Freshness.Stale)),
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale)),
                    ResponseJsonOptions);

            case WorkspaceLockStatusQueryResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceLockStatusQueryResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceLockStatusQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceLockStatusQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceLockStatusQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceLockStatusQueryResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static IResult ToHttpResult(HttpContext httpContext, WorkspaceStatusQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case WorkspaceStatusQueryResultCode.Allowed:
                if (string.IsNullOrWhiteSpace(result.FolderId)
                    || string.IsNullOrWhiteSpace(result.WorkspaceId)
                    || result.ProjectedState is null
                    || result.ProviderOutcome is null)
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: taskId);
                }

                AddWorkspaceStatusSuccessHeaders(httpContext, result);
                return Results.Json(
                    new WorkspaceStatusResponse(
                        result.FolderId,
                        result.WorkspaceId,
                        result.CurrentState,
                        result.AcceptedCommandState,
                        result.ProjectedState,
                        new WorkspaceProviderOutcomeResponse(
                            result.ProviderOutcome.OperationId,
                            result.ProviderOutcome.State,
                            result.ProviderOutcome.SanitizedStatusClass,
                            result.ProviderOutcome.ProviderCorrelationReference,
                            result.ProviderOutcome.RetryEligibility,
                            result.ProviderOutcome.RetryAfter,
                            new FreshnessMetadataResponse(
                                result.ProviderOutcome.Freshness.ReadConsistency,
                                result.ProviderOutcome.Freshness.ObservedAt,
                                result.ProviderOutcome.Freshness.ProjectionWatermark,
                                result.ProviderOutcome.Freshness.Stale)),
                        result.RetryEligibility,
                        result.RetryAfter,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale),
                        result.ProjectionLag,
                        result.LastFailureCategory),
                    ResponseJsonOptions);

            case WorkspaceStatusQueryResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceStatusQueryResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceStatusQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceStatusQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceStatusQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceStatusQueryResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static async Task<IResult> WorkspaceEvidenceQueryAsync(
        WorkspaceEvidenceKind kind,
        string folderId,
        string workspaceId,
        string? operationId,
        string? secondaryId,
        HttpContext httpContext,
        WorkspaceStatusQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");
        string[] identifiers = operationId is null
            ? [folderId, workspaceId, secondaryId ?? string.Empty]
            : [folderId, workspaceId, operationId];
        IResult? envelopeFailure = ValidateEvidenceQueryEnvelope(
            httpContext,
            correlationId,
            taskId,
            identifiers,
            requireEventuallyConsistent: true);
        if (envelopeFailure is not null)
        {
            return envelopeFailure;
        }

        WorkspaceStatusQueryResult result = await handler.HandleAsync(
            new WorkspaceStatusQuery(
                folderId,
                workspaceId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(WorkspaceStatusQueryHandler.ActionToken),
                correlationId,
                taskId,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        if (result.Code != WorkspaceStatusQueryResultCode.Allowed)
        {
            return ToHttpResult(httpContext, result, correlationId, taskId);
        }

        if (string.IsNullOrWhiteSpace(result.FolderId)
            || string.IsNullOrWhiteSpace(result.WorkspaceId)
            || result.ProviderOutcome is null
            || (operationId is not null && !string.Equals(result.ProviderOutcome.OperationId, operationId, StringComparison.Ordinal))
            || (kind == WorkspaceEvidenceKind.ReconciliationStatus && !string.Equals(result.ProviderOutcome.ReconciliationReference, secondaryId, StringComparison.Ordinal)))
        {
            return SafeProblem(
                StatusCodes.Status404NotFound,
                category: "not_found",
                code: "not_found",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        AddEvidenceSuccessHeaders(httpContext, correlationId);
        return kind switch
        {
            WorkspaceEvidenceKind.CommitEvidence => Results.Json(
                new CommitEvidenceResponse(
                    result.ProviderOutcome.OperationId,
                    result.CurrentState,
                    result.ProviderOutcome.CommitReferenceClassification ?? CommitReferenceClassification(result.CurrentState),
                    result.ProviderOutcome.ChangedPathMetadataDigest ?? "digest_unavailable",
                    result.ProviderOutcome.ProviderCorrelationReference,
                    new RedactionMetadataResponse("metadata_only", "not_redacted"),
                    [
                        "folder_id",
                        "workspace_id",
                        "operation_id",
                        "changed_path_metadata_digest",
                        "provider_correlation_reference",
                        "sensitive_metadata_tier",
                        "correlation_id",
                    ],
                    ToEventuallyConsistentFreshness(result.Freshness)),
                ResponseJsonOptions),
            WorkspaceEvidenceKind.ProviderOutcome => Results.Json(
                new WorkspaceProviderOutcomeResponse(
                    result.ProviderOutcome.OperationId,
                    result.ProviderOutcome.State,
                    result.ProviderOutcome.SanitizedStatusClass,
                    result.ProviderOutcome.ProviderCorrelationReference,
                    result.ProviderOutcome.RetryEligibility,
                    result.ProviderOutcome.RetryAfter,
                    ToEventuallyConsistentFreshness(result.ProviderOutcome.Freshness)),
                ResponseJsonOptions),
            WorkspaceEvidenceKind.ReconciliationStatus => Results.Json(
                new ReconciliationStatusResponse(
                    secondaryId ?? "reconciliation_status",
                    result.ProviderOutcome.OperationId,
                    ReconciliationStateFor(result.CurrentState),
                    FinalStateEvidenceFor(result.CurrentState),
                    EscalationRequiredFor(result.CurrentState),
                    result.RetryEligibility,
                    result.RetryAfter,
                    ToEventuallyConsistentFreshness(result.Freshness)),
                ResponseJsonOptions),
            _ => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "internal_error",
                code: "read_model_unavailable",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId),
        };
    }

    private static IResult ToHttpResult(HttpContext httpContext, TaskStatusQueryResult result, string? correlationId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return TenantAccessDenialToProblem(result.AuthorizationDenial, correlationId);
        }

        switch (result.Code)
        {
            case TaskStatusQueryResultCode.Allowed:
                if (string.IsNullOrWhiteSpace(result.TaskId))
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: null);
                }

                AddEvidenceSuccessHeaders(httpContext, result.CorrelationId);
                return Results.Json(
                    new TaskStatusResponse(
                        result.TaskId,
                        result.CurrentState,
                        result.TerminalState,
                        result.LastOperationId,
                        result.LastFailureCategory,
                        result.RetryEligibility,
                        result.RetryAfter,
                        ToEventuallyConsistentFreshness(result.Freshness)),
                    ResponseJsonOptions);

            case TaskStatusQueryResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: null);

            case TaskStatusQueryResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: null);

            case TaskStatusQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: null);

            case TaskStatusQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: null);

            case TaskStatusQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: null);

            case TaskStatusQueryResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: null);
        }
    }

    private static IResult ToHttpResult(HttpContext httpContext, WorkspaceCleanupStatusQueryResult result, string? correlationId, string? taskId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        switch (result.Code)
        {
            case WorkspaceCleanupStatusQueryResultCode.Allowed:
                if (string.IsNullOrWhiteSpace(result.FolderId)
                    || string.IsNullOrWhiteSpace(result.WorkspaceId))
                {
                    return SafeProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        category: "read_model_unavailable",
                        code: "read_model_unavailable",
                        retryable: true,
                        correlationId: correlationId,
                        taskId: taskId);
                }

                AddWorkspaceCleanupStatusSuccessHeaders(httpContext, result);
                return Results.Json(
                    new WorkspaceCleanupStatusResponse(
                        result.FolderId,
                        result.WorkspaceId,
                        result.TaskId,
                        result.Status,
                        result.ReasonCode,
                        result.RetryEligibility,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale),
                        result.CorrelationId,
                        result.ObservedAt,
                        result.LastAttemptedAt),
                    ResponseJsonOptions);

            case WorkspaceCleanupStatusQueryResultCode.AuthenticationRequired:
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    category: "authentication_failure",
                    code: "authentication_failure",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceCleanupStatusQueryResultCode.NotFoundSafe:
                return SafeProblem(
                    StatusCodes.Status404NotFound,
                    category: "not_found",
                    code: "not_found",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceCleanupStatusQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_stale",
                    code: "projection_stale",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceCleanupStatusQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "projection_unavailable",
                    code: "projection_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceCleanupStatusQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId,
                    taskId: taskId);

            case WorkspaceCleanupStatusQueryResultCode.AuthorizationDenied:
            default:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    category: "tenant_access_denied",
                    code: "denied_safe",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId);
        }
    }

    private static IResult ToHttpResult(EffectivePermissionsQueryResult result, string? correlationId)
        => result.Code switch
        {
            EffectivePermissionsResultCode.Allowed
                or EffectivePermissionsResultCode.DeniedSafe
                or EffectivePermissionsResultCode.NotFoundSafe
                or EffectivePermissionsResultCode.ProjectionStale => Results.Json(
                    new EffectivePermissionsResponse(
                        result.FolderId ?? string.Empty,
                        result.Permissions.Select(PermissionToken).ToArray(),
                        result.AuthorizationOutcome,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale)),
                    ResponseJsonOptions),
            EffectivePermissionsResultCode.AuthenticationRequired => SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId,
                taskId: null),
            EffectivePermissionsResultCode.ReadModelUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "read_model_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: null),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId,
                taskId: null),
        };

    private static IResult SafeProblem(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        string? taskId,
        string? message = null)
    {
        Dictionary<string, object?> details = new()
        {
            ["visibility"] = "metadata_only",
            ["retryReasonCode"] = code,
            ["reasonCategory"] = category,
            ["evidenceSource"] = "http_boundary",
        };

        if (!string.IsNullOrWhiteSpace(taskId) && IsCanonicalIdentifier(taskId))
        {
            details["taskId"] = taskId;
        }

        if (category is "unknown_provider_outcome" or "reconciliation_required")
        {
            details["finalState"] = category;
        }

        Dictionary<string, object?> extensions = new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = message ?? MessageFor(category),
            ["correlationId"] = correlationId,
            ["retryable"] = retryable,
            ["clientAction"] = ClientActionFor(category, retryable),
            ["details"] = details,
        };

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            extensions["taskId"] = taskId;
        }

        return Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: statusCode switch
            {
            StatusCodes.Status400BadRequest => "Validation failure.",
            StatusCodes.Status401Unauthorized => "Authentication required.",
            StatusCodes.Status404NotFound => "Resource not available.",
            StatusCodes.Status408RequestTimeout => "Query timeout.",
            StatusCodes.Status409Conflict => "Idempotency conflict.",
            StatusCodes.Status413PayloadTooLarge => "Response limit exceeded.",
            StatusCodes.Status416RangeNotSatisfiable => "Range not satisfiable.",
            StatusCodes.Status422UnprocessableEntity => "Validation outcome.",
            StatusCodes.Status503ServiceUnavailable => "Read model unavailable.",
            _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static IResult? ValidateEvidenceQueryEnvelope(
        HttpContext httpContext,
        string? correlationId,
        string? taskId,
        IReadOnlyList<string> identifiers,
        bool requireEventuallyConsistent)
    {
        if (ReadHeader(httpContext, "Idempotency-Key") is not null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "idempotency_key_not_allowed",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Idempotency-Key is not accepted on read operations.");
        }

        if (identifiers.Any(static identifier => !IsCanonicalIdentifier(identifier))
            || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
            || (taskId is not null && !IsCanonicalIdentifier(taskId)))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                taskId: IsCanonicalIdentifier(taskId) ? taskId : null);
        }

        string? requestedFreshness = ReadHeader(httpContext, FreshnessHeaderName);
        string expectedFreshness = requireEventuallyConsistent ? EventuallyConsistent : ReadYourWrites;
        if (requestedFreshness is not null
            && !string.Equals(requestedFreshness, expectedFreshness, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_read_consistency",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: $"Operation supports {expectedFreshness} only.");
        }

        return null;
    }

    private static IResult TenantAccessDenialToProblem(TenantAccessAuthorizationResult denial, string? correlationId)
        => denial.Outcome switch
        {
            TenantAccessOutcome.MissingAuthoritativeTenant => SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "authentication_failure",
                retryable: false,
                correlationId: correlationId,
                taskId: null),
            TenantAccessOutcome.StaleProjection => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "projection_stale",
                code: "projection_stale",
                retryable: true,
                correlationId: correlationId,
                taskId: null),
            TenantAccessOutcome.UnavailableProjection or TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "read_model_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: null),
            TenantAccessOutcome.UnknownTenant or TenantAccessOutcome.DisabledTenant or TenantAccessOutcome.Denied => SafeProblem(
                StatusCodes.Status404NotFound,
                category: "not_found",
                code: "not_found",
                retryable: false,
                correlationId: correlationId,
                taskId: null),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId,
                taskId: null),
        };

    private static FreshnessMetadataResponse ToEventuallyConsistentFreshness(FolderLifecycleFreshness freshness)
        => new(
            EventuallyConsistent,
            freshness.ObservedAt,
            freshness.ProjectionWatermark,
            freshness.Stale);

    private static void AddEvidenceSuccessHeaders(
        HttpContext httpContext,
        string? correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId) && !ContainsControlChars(correlationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = EventuallyConsistent;
    }

    private static string CommitReferenceClassification(string currentState)
        => currentState == "committed" ? "opaque_reference" : "unavailable";

    private static string ReconciliationStateFor(string currentState)
        => currentState switch
        {
            "reconciliation_required" => "required",
            "unknown_provider_outcome" => "in_progress",
            "committed" => "completed_clean",
            "dirty" => "completed_dirty",
            "failed" or "inaccessible" => "failed",
            _ => "not_required",
        };

    private static string FinalStateEvidenceFor(string currentState)
        => currentState switch
        {
            "committed" => "committed",
            "dirty" => "dirty",
            "failed" or "inaccessible" => "failed",
            "reconciliation_required" or "unknown_provider_outcome" => "pending",
            _ => "pending",
        };

    private static bool EscalationRequiredFor(string currentState)
        => currentState is "reconciliation_required" or "failed" or "inaccessible";

    private static string ClientActionFor(string category, bool retryable)
        => FolderCanonicalErrorMapper.ClientActionFor(category, retryable);

    private static string MessageFor(string category) => category switch
    {
        "authentication_failure" => "Authentication is required to access this resource.",
        "read_model_unavailable" => "The read model is temporarily unavailable. Retry later.",
        "projection_stale" => "The read-model projection is stale. Retry later.",
        "projection_unavailable" => "The read-model projection is unavailable. Retry later.",
        "not_found" => "The requested resource is not available to the caller.",
        "validation_error" => "Request validation failed.",
        "internal_error" => "The operation cannot be completed in this configuration.",
        "provider_readiness_failed" => "Provider readiness could not be established for this operation.",
        "unsupported_provider_capability" => "Provider capability is not available for this operation.",
        "workspace_preparation_failed" => "Workspace preparation could not be accepted.",
        "workspace_transition_invalid" => "Workspace lifecycle transition is not valid for this operation.",
        "lock_conflict" => "Workspace lock is held by another operation.",
        "workspace_locked" => "Workspace is already locked.",
        "lock_not_owned" => "Workspace lock is not owned by this task scope.",
        "lock_expired" => "The workspace lock lease is no longer active.",
        "path_policy_denied" => "Path policy denied the requested file operation.",
        "path_validation_failed" => "Path validation failed for the requested operation.",
        "input_limit_exceeded" => "The request exceeds configured input limits.",
        "response_limit_exceeded" => "The query exceeds configured response limits.",
        "query_timeout" => "The context query timed out. Retry later.",
        "redacted" => "The requested context is not available to the caller.",
        "range_unsatisfiable" => "The requested byte range cannot be satisfied.",
        "commit_failed" => "Commit failed with a known final outcome.",
        "provider_failure_known" => "Provider failure was observed with a known final outcome.",
        "idempotency_conflict" => "Idempotency key conflicts with a prior operation.",
        "unknown_provider_outcome" => "Provider outcome is unknown and requires safe reconciliation.",
        "reconciliation_required" => "Reconciliation is required before this operation can continue.",
        "provider_unavailable" => "Provider evidence is temporarily unavailable. Retry later.",
        _ => "Access is denied. The caller is not authorized for this operation or resource.",
    };

    private static string PermissionToken(EffectivePermissionLevel permission)
        => permission switch
        {
            EffectivePermissionLevel.Read => "read",
            EffectivePermissionLevel.Write => "write",
            EffectivePermissionLevel.Administer => "administer",
            _ => "read",
        };

    private static IReadOnlyDictionary<string, string?> ClientTenantIds(HttpContext httpContext)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_tenant_id"] = ReadQuery(httpContext, "tenantId"),
            ["query_managed_tenant_id"] = ReadQuery(httpContext, "managedTenantId"),
            ["header_hexalith_tenant_id"] = ReadHeader(httpContext, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = ReadHeader(httpContext, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = ReadHeader(httpContext, "X-Forwarded-Tenant"),
        };

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext httpContext)
        // Principal identity is header-only — query-string sources are not accepted to
        // reduce attack surface.
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = ReadHeader(httpContext, "X-Forwarded-Principal"),
        };

    private static void AddLifecycleSuccessHeaders(HttpContext httpContext, FolderLifecycleStatusQueryResult result)
    {
        // Only invoked on the Allowed branch — OpenAPI declares these headers only on the
        // 200 response. Writing them on denial paths would leak that the lifecycle handler
        // was reached vs other handlers.
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static void AddBranchRefPolicySuccessHeaders(HttpContext httpContext, BranchRefPolicyQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static void AddWorkspaceLockSuccessHeaders(HttpContext httpContext, WorkspaceLockStatusQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static void AddWorkspaceStatusSuccessHeaders(HttpContext httpContext, WorkspaceStatusQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static void AddWorkspaceCleanupStatusSuccessHeaders(HttpContext httpContext, WorkspaceCleanupStatusQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static void AddFileContextSuccessHeaders(HttpContext httpContext, WorkspaceFileContextQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId)
            && !ContainsControlChars(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.Freshness.ReadConsistency)
            && !ContainsControlChars(result.Freshness.ReadConsistency))
        {
            httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
        }
    }

    private static string? ReadHeader(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Headers.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static string? ReadQuery(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Query.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static bool TryReadString(JsonElement root, string propertyName, out string? value)
    {
        value = root.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? FirstNonEmpty(StringValues values)
    {
        foreach (string? raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            // Reject header/query values containing CR or LF to prevent response-splitting
            // when echoed back into response headers.
            if (ContainsControlChars(trimmed))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }

    private static bool ContainsControlChars(string value)
    {
        foreach (char c in value)
        {
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    private enum FileMutationRequestValidationResult
    {
        Accepted,
        ValidationFailed,
        PathValidationFailed,
        InputLimitExceeded,
    }

    private sealed record FileMutationTransportValidation(
        FileMutationRequestValidationResult Result,
        string? MediaType,
        string? TransportEvidenceKind,
        long? ObservedByteLength)
    {
        public bool IsAccepted => Result == FileMutationRequestValidationResult.Accepted;

        public static FileMutationTransportValidation Accepted(
            string? mediaType,
            string? transportEvidenceKind,
            long? observedByteLength)
            => new(
                FileMutationRequestValidationResult.Accepted,
                mediaType,
                transportEvidenceKind,
                observedByteLength);

        public static FileMutationTransportValidation Rejected(FileMutationRequestValidationResult result)
            => new(result, null, null, null);
    }

    private sealed record ArchiveFolderHttpRequest(
        string? RequestSchemaVersion,
        string? ArchiveReasonCode);

    private sealed record CreateRepositoryBackedFolderHttpRequest(
        string? RequestSchemaVersion,
        string? FolderId,
        string? ProviderBindingRef,
        string? RepositoryProfileRef,
        FolderMetadataHttpRequest? FolderMetadata,
        BranchRefPolicyHttpRequest? BranchRefPolicy);

    private sealed record BindRepositoryHttpRequest(
        string? RequestSchemaVersion,
        string? ProviderBindingRef,
        string? ExternalRepositoryRef,
        BranchRefPolicyHttpRequest? BranchRefPolicy);

    private sealed record PrepareWorkspaceHttpRequest(
        string? RequestSchemaVersion,
        string? RepositoryBindingId,
        string? BranchRefPolicyRef,
        string? WorkspacePolicyRef);

    private sealed record LockWorkspaceHttpRequest(
        string? RequestSchemaVersion,
        string? LockIntent,
        int? RequestedLeaseSeconds);

    private sealed record ReleaseWorkspaceLockHttpRequest(
        string? RequestSchemaVersion,
        string? LockId,
        string? LockOwnershipProof,
        string? ReleaseReasonCode);

    private sealed record PathMetadataHttpRequest(
        string? NormalizedPath,
        string? DisplayName,
        string? PathPolicyClass,
        string? UnicodeNormalization);

    private sealed record FileMutationHttpRequest(
        string? RequestSchemaVersion,
        string? OperationId,
        string? FileOperationKind,
        string? TransportOperation,
        PathMetadataHttpRequest? PathMetadata,
        string? ContentHashReference,
        long? ByteLength,
        JsonElement? InlineContent,
        JsonElement? StreamDescriptor);

    private sealed record CommitWorkspaceHttpRequest(
        string? RequestSchemaVersion,
        string? OperationId,
        string? TaskId,
        string? BranchRefTarget,
        string? ChangedPathMetadataDigest,
        string? AuthorMetadataReference,
        string? CommitMessageClassification,
        IReadOnlyList<string>? AuditMetadataKeys);

    private sealed record CreateRepositoryBackedFolderGatewayPayload(
        string? RequestSchemaVersion,
        string? FolderId,
        string? RepositoryBindingId,
        string? ProviderBindingRef,
        string? RepositoryProfileRef,
        FolderMetadataHttpRequest? FolderMetadata,
        BranchRefPolicyHttpRequest? BranchRefPolicy,
        string? CredentialScopeClass);

    private sealed record PrepareWorkspaceGatewayPayload(
        string? RequestSchemaVersion,
        string WorkspaceId,
        string? RepositoryBindingId,
        string? BranchRefPolicyRef,
        string? WorkspacePolicyRef);

    private sealed record LockWorkspaceGatewayPayload(
        string? RequestSchemaVersion,
        string WorkspaceId,
        string? LockIntent,
        int? RequestedLeaseSeconds);

    private sealed record ReleaseWorkspaceLockGatewayPayload(
        string? RequestSchemaVersion,
        string WorkspaceId,
        string? LockId,
        string? LockOwnershipProof,
        string? ReleaseReasonCode);

    private sealed record FileMutationGatewayPayload(
        string? RequestSchemaVersion,
        string WorkspaceId,
        string? OperationId,
        string? FileOperationKind,
        string? TransportOperation,
        PathMetadataHttpRequest? PathMetadata,
        string? ContentHashReference,
        long? ByteLength,
        string? MediaType,
        string? TransportEvidenceKind,
        long? ObservedByteLength);

    private sealed record CommitWorkspaceGatewayPayload(
        string? RequestSchemaVersion,
        string WorkspaceId,
        string? OperationId,
        string? TaskId,
        string? BranchRefTarget,
        string? ChangedPathMetadataDigest,
        string? AuthorMetadataReference,
        string? CommitMessageClassification,
        IReadOnlyList<string>? AuditMetadataKeys);

    private sealed record FolderMetadataHttpRequest(
        string? DisplayName,
        string? MetadataClass);

    private sealed record BranchRefPolicyHttpRequest(
        string? RequestSchemaVersion,
        string? RepositoryBindingId,
        string? PolicyRef,
        string? DefaultRef,
        IReadOnlyList<string>? AllowedRefPatterns,
        IReadOnlyList<string>? ProtectedRefPatterns);

    private sealed record FileMetadataContextHttpRequest(
        string? RequestSchemaVersion,
        IReadOnlyList<PathMetadata>? Paths);

    private sealed record FileSearchContextHttpRequest(
        string? RequestSchemaVersion,
        string? QueryFamily,
        string? QueryText,
        IReadOnlyList<PathMetadata>? RequestedPaths,
        int? Limit,
        string? Cursor);

    private sealed record FileGlobContextHttpRequest(
        string? RequestSchemaVersion,
        string? QueryFamily,
        string? GlobPattern,
        IReadOnlyList<PathMetadata>? RequestedPaths,
        int? Limit,
        string? Cursor);

    private sealed record FileRangeReadContextHttpRequest(
        string? RequestSchemaVersion,
        PathMetadata? Path,
        long? StartOffset,
        long? EndOffset);

    private sealed record AcceptedCommandResponse(
        DateTimeOffset AcceptedAt,
        string CorrelationId,
        string TaskId,
        string Status,
        bool IdempotentReplay);

    private sealed record CommitWorkspaceAcceptedResponse(
        DateTimeOffset AcceptedAt,
        string CorrelationId,
        string TaskId,
        string Status,
        bool IdempotentReplay,
        string OperationId,
        string AcceptedCommandState,
        string ProviderOutcomeState,
        RetryEligibilityResponse RetryEligibility);

    private sealed record RetryEligibilityResponse(
        bool Eligible,
        string ReasonCode,
        bool AdvisoryOnly);

    private readonly record struct MutationCommandEnvelope(
        string IdempotencyKey,
        string CorrelationId,
        string TaskId,
        string TenantId);

    private sealed record EffectivePermissionsResponse(
        string FolderId,
        IReadOnlyList<string> Permissions,
        string AuthorizationOutcome,
        FreshnessMetadataResponse Freshness);

    private sealed record FolderLifecycleStatusResponse(
        string FolderId,
        string LifecycleState,
        bool Archived,
        string? RepositoryBindingId,
        string? ProviderBindingRef,
        FreshnessMetadataResponse Freshness);

    private sealed record WorkspaceLockStatusResponse(
        RedactableIdentifierResponse WorkspaceReference,
        string LockState,
        LockLeaseMetadataResponse? Lease,
        WorkspaceRetryEligibilityResponse RetryEligibility,
        FreshnessMetadataResponse Freshness);

    private sealed record RedactableIdentifierResponse(
        string Value,
        string Classification,
        RedactionMetadataResponse Redaction);

    private sealed record RedactionMetadataResponse(
        string Visibility,
        string ReasonCode);

    private sealed record LockLeaseMetadataResponse(
        string LockId,
        string LeaseStatus,
        DateTimeOffset AcquiredAt,
        DateTimeOffset EffectiveAt,
        DateTimeOffset ExpiresAt,
        string? HolderRef);

    private sealed record WorkspaceRetryEligibilityResponse(
        bool Retryable,
        int? RetryAfterSeconds,
        string ReasonCode,
        string? CorrelationId,
        string? TaskId,
        string CurrentState,
        FreshnessMetadataResponse Freshness);

    private sealed record WorkspaceStatusResponse(
        string FolderId,
        string WorkspaceId,
        string CurrentState,
        WorkspaceAcceptedCommandState? AcceptedCommandState,
        WorkspaceProjectedState ProjectedState,
        WorkspaceProviderOutcomeResponse ProviderOutcome,
        WorkspaceStatusRetryEligibility RetryEligibility,
        WorkspaceStatusRetryAfter? RetryAfter,
        FreshnessMetadataResponse Freshness,
        WorkspaceProjectionLag ProjectionLag,
        string? LastFailureCategory);

    private sealed record WorkspaceProviderOutcomeResponse(
        string OperationId,
        string State,
        string SanitizedStatusClass,
        string ProviderCorrelationReference,
        WorkspaceStatusRetryEligibility RetryEligibility,
        WorkspaceStatusRetryAfter? RetryAfter,
        FreshnessMetadataResponse Freshness);

    private sealed record TaskStatusResponse(
        string TaskId,
        string CurrentState,
        string? TerminalState,
        string? LastOperationId,
        string? LastFailureCategory,
        WorkspaceStatusRetryEligibility RetryEligibility,
        WorkspaceStatusRetryAfter? RetryAfter,
        FreshnessMetadataResponse Freshness);

    private sealed record CommitEvidenceResponse(
        string OperationId,
        string CommitResultStatus,
        string CommitReferenceClassification,
        string ChangedPathMetadataDigest,
        string ProviderCorrelationReference,
        RedactionMetadataResponse Redaction,
        IReadOnlyList<string> AuditMetadataKeys,
        FreshnessMetadataResponse Freshness);

    private sealed record ReconciliationStatusResponse(
        string ReconciliationId,
        string OperationId,
        string State,
        string FinalStateEvidence,
        bool EscalationRequired,
        WorkspaceStatusRetryEligibility RetryEligibility,
        WorkspaceStatusRetryAfter? RetryAfter,
        FreshnessMetadataResponse Freshness);

    private sealed record WorkspaceCleanupStatusResponse(
        string FolderId,
        string WorkspaceId,
        string? TaskId,
        string Status,
        string ReasonCode,
        WorkspaceStatusRetryEligibility RetryEligibility,
        FreshnessMetadataResponse Freshness,
        string? CorrelationId,
        DateTimeOffset? ObservedAt,
        DateTimeOffset? LastAttemptedAt);

    private sealed record BranchRefPolicyResponse(
        string RequestSchemaVersion,
        string RepositoryBindingId,
        string PolicyRef,
        string DefaultRef,
        IReadOnlyList<string> AllowedRefPatterns,
        IReadOnlyList<string>? ProtectedRefPatterns,
        FreshnessMetadataResponse Freshness);

    private sealed record FileTreeResultResponse(
        IReadOnlyList<WorkspaceFileContextItem> Items,
        WorkspaceFileContextPage Page,
        WorkspaceFileContextLimits Limits,
        FreshnessMetadataResponse Freshness);

    private sealed record FileMetadataResultResponse(
        IReadOnlyList<WorkspaceFileContextItem> Items,
        WorkspaceFileContextLimits Limits,
        FreshnessMetadataResponse Freshness);

    private sealed record FileRangeReadResultResponse(
        PathMetadata Path,
        WorkspaceFileContextRange Range,
        string ContentBytes,
        WorkspaceFileContextLimits Limits,
        FreshnessMetadataResponse Freshness);

    private sealed record FreshnessMetadataResponse(
        string ReadConsistency,
        DateTimeOffset ObservedAt,
        string? ProjectionWatermark,
        bool Stale);

    private enum WorkspaceEvidenceKind
    {
        CommitEvidence,
        ProviderOutcome,
        ReconciliationStatus,
    }
}
