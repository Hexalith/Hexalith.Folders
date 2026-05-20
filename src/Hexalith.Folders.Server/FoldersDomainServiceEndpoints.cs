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
            ["clientAction"] = retryable ? "retry" : "no_action",
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
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static string MessageFor(string category) => category switch
    {
        "authentication_failure" => "Authentication is required to access this resource.",
        "read_model_unavailable" => "The read model is temporarily unavailable. Retry later.",
        "projection_stale" => "The read-model projection is stale. Retry later.",
        "projection_unavailable" => "The read-model projection is unavailable. Retry later.",
        "not_found" => "The requested resource is not available to the caller.",
        "validation_error" => "Request validation failed.",
        "internal_error" => "The operation cannot be completed in this configuration.",
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

    private sealed record FreshnessMetadataResponse(
        string ReadConsistency,
        DateTimeOffset ObservedAt,
        string? ProjectionWatermark,
        bool Stale);
}
