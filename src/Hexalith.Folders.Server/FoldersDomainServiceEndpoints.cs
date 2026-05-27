using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
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

    public static IEndpointRouteBuilder MapFoldersDomainServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(FoldersServerModule.ProcessRoute, async (
            DomainServiceRequest request,
            FoldersDomainServiceRequestHandler handler,
            CancellationToken cancellationToken)
            => await handler.ProcessAsync(request, cancellationToken).ConfigureAwait(false));

        endpoints.MapPost(FoldersServerModule.ProjectRoute, (ProjectionRequest _) =>
            Results.Problem(
                type: "https://hexalith.dev/errors/folders/projection-not-implemented",
                title: "Folders projection endpoint is not implemented yet.",
                statusCode: StatusCodes.Status501NotImplemented,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "not_implemented",
                    ["code"] = "projection_not_implemented",
                }));

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
        .WithName("GetEffectivePermissions");

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
        .WithName("ArchiveFolder");

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
        .WithName("CreateRepositoryBackedFolder");

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
        .WithName("BindRepository");

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
        .WithName("ConfigureBranchRefPolicy");

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
        .WithName("PrepareWorkspace");

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
        .WithName("LockWorkspace");

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
        .WithName("GetBranchRefPolicy");

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
        .WithName("GetFolderLifecycleStatus");

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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        // Validate envelope first: idempotency key, correlation/task identifiers.
        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        // folderId: validate canonical segment shape before any downstream use.
        if (string.IsNullOrWhiteSpace(folderId) || !IsCanonicalIdentifier(folderId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
        }

        // Authenticate before parsing the body so unauthenticated callers cannot probe
        // JSON parsing/validation feedback or consume CPU on body deserialization.
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

        // Reserved-tenant rejection at the edge — never let a "system" tenant context
        // reach the command pipeline.
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId)
            || !IsCanonicalIdentifier(folderId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId)
            || !IsCanonicalIdentifier(folderId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId)
            || !IsCanonicalIdentifier(folderId)
            || !IsCanonicalIdentifier(workspaceId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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

        string? idempotencyKey = ReadHeader(httpContext, "Idempotency-Key");
        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = ReadHeader(httpContext, "X-Hexalith-Task-Id");

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(taskId)
            || !IsCanonicalIdentifier(idempotencyKey)
            || !IsCanonicalIdentifier(taskId)
            || !IsCanonicalIdentifier(correlationId)
            || !IsCanonicalIdentifier(folderId)
            || !IsCanonicalIdentifier(workspaceId))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId);
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
                    Tenant: tenantContext.AuthoritativeTenantId,
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
        && value.Length <= 128
        && CanonicalSegmentRegex.IsMatch(value);

    private static bool IsSafeGatewayCorrelationId(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && GatewayCorrelationRegex.IsMatch(value);

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
            "workspace_locked" => "workspace_locked",
            "workspace-locked" => "workspace_locked",
            "workspace-already-locked" => "workspace_locked",
            "workspace-already-locked-rejected" => "workspace_locked",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "unknown-provider-outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            "reconciliation-required" => "reconciliation_required",
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
        Dictionary<string, object?> extensions = new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = message ?? MessageFor(category),
            ["correlationId"] = correlationId,
            ["retryable"] = retryable,
            ["clientAction"] = ClientActionFor(category, retryable),
            ["details"] = new Dictionary<string, object?>
            {
                ["visibility"] = "metadata_only",
            },
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
                StatusCodes.Status409Conflict => "Idempotency conflict.",
                StatusCodes.Status422UnprocessableEntity => "Validation outcome.",
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static string ClientActionFor(string category, bool retryable)
        => category switch
        {
            "unknown_provider_outcome" or "reconciliation_required" => "wait_for_reconciliation",
            "provider_readiness_failed" => "contact_operator",
            "workspace_preparation_failed" or "workspace_transition_invalid" => "revise_request",
            "lock_conflict" or "workspace_locked" => "retry_after_release",
            _ => retryable ? "retry" : "no_action",
        };

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
        "workspace_preparation_failed" => "Workspace preparation could not be accepted.",
        "workspace_transition_invalid" => "Workspace lifecycle transition is not valid for this operation.",
        "lock_conflict" => "Workspace lock is held by another operation.",
        "workspace_locked" => "Workspace is already locked.",
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

    private sealed record AcceptedCommandResponse(
        DateTimeOffset AcceptedAt,
        string CorrelationId,
        string TaskId,
        string Status,
        bool IdempotentReplay);

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

    private sealed record BranchRefPolicyResponse(
        string RequestSchemaVersion,
        string RepositoryBindingId,
        string PolicyRef,
        string DefaultRef,
        IReadOnlyList<string> AllowedRefPatterns,
        IReadOnlyList<string>? ProtectedRefPatterns,
        FreshnessMetadataResponse Freshness);

    private sealed record FreshnessMetadataResponse(
        string ReadConsistency,
        DateTimeOffset ObservedAt,
        string? ProjectionWatermark,
        bool Stale);
}
