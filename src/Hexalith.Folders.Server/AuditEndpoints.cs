using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

public static partial class AuditEndpoints
{
    public const string FreshnessHeaderName = "X-Hexalith-Freshness";
    public const string EventuallyConsistent = "eventually_consistent";
    public const int OpenApiPageLimitCeiling = 1000;
    public const int MaxEntriesPerPage = 100;

    private const string AuditEvidenceSource = "audit";
    private const string TimelineEvidenceSource = "timeline";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/v1/folders/{folderId}/audit-trail", async (
            string folderId,
            HttpContext httpContext,
            AuditTrailQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await ListAuditTrailAsync(folderId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken).ConfigureAwait(false))
        .WithName(AuditTrailQueryHandler.OperationId)
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/audit-trail/{auditRecordId}", async (
            string folderId,
            string auditRecordId,
            HttpContext httpContext,
            AuditRecordQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await GetAuditRecordAsync(folderId, auditRecordId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken).ConfigureAwait(false))
        .WithName(AuditRecordQueryHandler.OperationId)
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/operation-timeline", async (
            string folderId,
            HttpContext httpContext,
            OperationTimelineQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await ListOperationTimelineAsync(folderId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken).ConfigureAwait(false))
        .WithName(OperationTimelineQueryHandler.OperationId)
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}", async (
            string folderId,
            string timelineEntryId,
            HttpContext httpContext,
            OperationTimelineEntryQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await GetOperationTimelineEntryAsync(folderId, timelineEntryId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken).ConfigureAwait(false))
        .WithName(OperationTimelineEntryQueryHandler.OperationId)
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        return endpoints;
    }

    private static async Task<IResult> ListAuditTrailAsync(
        string folderId,
        HttpContext httpContext,
        AuditTrailQueryHandler handler,
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

        IResult? envelope = ValidateListEnvelope(
            httpContext,
            folderId,
            extraIdentifier: null,
            correlationId,
            taskId,
            AuditEvidenceSource,
            out string? cursor,
            out int? requestedLimit,
            out string? filter);
        if (envelope is not null)
        {
            return envelope;
        }

        AuditTrailQueryResult result = await handler.HandleAsync(
            new AuditTrailQuery(
                folderId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(AuditTrailQueryHandler.ActionToken),
                correlationId,
                taskId,
                cursor,
                requestedLimit,
                filter,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return result.Code switch
        {
            AuditQueryResultCode.Allowed when result.Page is not null => Success(httpContext, result.Page, result.CorrelationId, result.TaskId),
            _ => SafeProblemFor(result.Code, result.CorrelationId, result.TaskId, AuditEvidenceSource),
        };
    }

    private static async Task<IResult> GetAuditRecordAsync(
        string folderId,
        string auditRecordId,
        HttpContext httpContext,
        AuditRecordQueryHandler handler,
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

        IResult? envelope = ValidateSingleEnvelope(httpContext, folderId, auditRecordId, correlationId, taskId, AuditEvidenceSource);
        if (envelope is not null)
        {
            return envelope;
        }

        AuditRecordQueryResult result = await handler.HandleAsync(
            new AuditRecordQuery(
                folderId,
                auditRecordId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(AuditRecordQueryHandler.ActionToken),
                correlationId,
                taskId,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return result.Code switch
        {
            AuditQueryResultCode.Allowed when result.Record is not null => Success(httpContext, result.Record, result.CorrelationId, result.TaskId),
            _ => SafeProblemFor(result.Code, result.CorrelationId, result.TaskId, AuditEvidenceSource),
        };
    }

    private static async Task<IResult> ListOperationTimelineAsync(
        string folderId,
        HttpContext httpContext,
        OperationTimelineQueryHandler handler,
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

        IResult? envelope = ValidateListEnvelope(
            httpContext,
            folderId,
            extraIdentifier: null,
            correlationId,
            taskId,
            TimelineEvidenceSource,
            out string? cursor,
            out int? requestedLimit,
            out string? filter);
        if (envelope is not null)
        {
            return envelope;
        }

        OperationTimelineQueryResult result = await handler.HandleAsync(
            new OperationTimelineQuery(
                folderId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(OperationTimelineQueryHandler.ActionToken),
                correlationId,
                taskId,
                cursor,
                requestedLimit,
                filter,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return result.Code switch
        {
            AuditQueryResultCode.Allowed when result.Page is not null => Success(httpContext, result.Page, result.CorrelationId, result.TaskId),
            _ => SafeProblemFor(result.Code, result.CorrelationId, result.TaskId, TimelineEvidenceSource),
        };
    }

    private static async Task<IResult> GetOperationTimelineEntryAsync(
        string folderId,
        string timelineEntryId,
        HttpContext httpContext,
        OperationTimelineEntryQueryHandler handler,
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

        IResult? envelope = ValidateSingleEnvelope(httpContext, folderId, timelineEntryId, correlationId, taskId, TimelineEvidenceSource);
        if (envelope is not null)
        {
            return envelope;
        }

        OperationTimelineEntryQueryResult result = await handler.HandleAsync(
            new OperationTimelineEntryQuery(
                folderId,
                timelineEntryId,
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(OperationTimelineEntryQueryHandler.ActionToken),
                correlationId,
                taskId,
                ClientTenantIds(httpContext),
                ClientPrincipalIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return result.Code switch
        {
            AuditQueryResultCode.Allowed when result.Entry is not null => Success(httpContext, result.Entry, result.CorrelationId, result.TaskId),
            _ => SafeProblemFor(result.Code, result.CorrelationId, result.TaskId, TimelineEvidenceSource),
        };
    }

    private static IResult? ValidateListEnvelope(
        HttpContext httpContext,
        string folderId,
        string? extraIdentifier,
        string? correlationId,
        string? taskId,
        string evidenceSource,
        out string? cursor,
        out int? requestedLimit,
        out string? filter)
    {
        cursor = null;
        requestedLimit = null;
        filter = null;

        IResult? common = ValidateCommonEnvelope(httpContext, folderId, extraIdentifier, correlationId, taskId, evidenceSource);
        if (common is not null)
        {
            return common;
        }

        string? rawCursor = ReadQuery(httpContext, "cursor");
        if (rawCursor is not null)
        {
            if (rawCursor.Length is < 1 or > 256 || !CursorPattern().IsMatch(rawCursor))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "cursor_tampered",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Pagination cursor is malformed or tampered.",
                    evidenceSource: evidenceSource);
            }

            cursor = rawCursor;
        }

        string? rawLimit = ReadQuery(httpContext, "limit");
        if (rawLimit is not null)
        {
            if (!int.TryParse(rawLimit, out int parsedLimit) || parsedLimit < 1 || parsedLimit > OpenApiPageLimitCeiling)
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "invalid_pagination",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    evidenceSource: evidenceSource);
            }

            requestedLimit = parsedLimit;
        }

        string? rawFilter = ReadQuery(httpContext, "filter");
        if (rawFilter is not null)
        {
            // Validate the spine wire-shape regex first; a malformed filter is validation_error.
            if (rawFilter.Length is < 1 or > 256 || !FilterPattern().IsMatch(rawFilter))
            {
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    category: "validation_error",
                    code: "validation_error",
                    retryable: false,
                    correlationId: correlationId,
                    taskId: taskId,
                    message: "Filter expression does not match the canonical shape.",
                    evidenceSource: evidenceSource);
            }

            // Spine's MetadataFilter is TODO(C4); empty allow-list rejects every well-shaped filter.
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "filter_not_yet_supported",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Filter vocabulary is reference-pending C4.",
                todoRef: "C4",
                evidenceSource: evidenceSource);
        }

        return null;
    }

    private static IResult? ValidateSingleEnvelope(
        HttpContext httpContext,
        string folderId,
        string extraIdentifier,
        string? correlationId,
        string? taskId,
        string evidenceSource)
        => ValidateCommonEnvelope(httpContext, folderId, extraIdentifier, correlationId, taskId, evidenceSource);

    private static IResult? ValidateCommonEnvelope(
        HttpContext httpContext,
        string folderId,
        string? extraIdentifier,
        string? correlationId,
        string? taskId,
        string evidenceSource)
    {
        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "idempotency_key_not_allowed",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Idempotency-Key is not accepted on read operations.",
                evidenceSource: evidenceSource);
        }

        if (!IsCanonicalIdentifier(folderId)
            || (extraIdentifier is not null && !IsCanonicalIdentifier(extraIdentifier))
            || (correlationId is not null && !IsCanonicalIdentifier(correlationId))
            || (taskId is not null && !IsCanonicalIdentifier(taskId)))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: IsCanonicalIdentifier(correlationId) ? correlationId : null,
                taskId: IsCanonicalIdentifier(taskId) ? taskId : null,
                evidenceSource: evidenceSource);
        }

        string? freshness = ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "unsupported_read_consistency",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Operation supports eventually_consistent only.",
                evidenceSource: evidenceSource);
        }

        return null;
    }

    private static IResult SafeProblemFor(
        AuditQueryResultCode code,
        string? correlationId,
        string? taskId,
        string evidenceSource)
        => code switch
        {
            AuditQueryResultCode.AuthenticationRequired => SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "authentication_failure",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.TenantAccessDenied => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "tenant_access_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.FolderAclDenied => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "folder_acl_denied",
                code: "folder_acl_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.AuditAccessDenied => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "audit_access_denied",
                code: "audit_access_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.NotFoundSafe => SafeProblem(
                StatusCodes.Status404NotFound,
                category: "not_found",
                code: "not_found",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ValidationError => SafeProblem(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ProjectionStale => SafeProblem(
                StatusCodes.Status409Conflict,
                category: "projection_stale",
                code: "projection_stale",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ProjectionUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "projection_unavailable",
                code: "projection_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ReadModelUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "read_model_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.Redacted => SafeProblem(
                StatusCodes.Status404NotFound,
                category: "redacted",
                code: "redacted",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            _ => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "internal_error",
                code: "internal_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
        };

    private static IResult Success<T>(HttpContext httpContext, T body, string? correlationId, string? taskId)
    {
        AddSuccessHeaders(httpContext, correlationId, taskId);
        return Results.Json(body, ResponseJsonOptions);
    }

    private static void AddSuccessHeaders(HttpContext httpContext, string? correlationId, string? taskId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId) && IsSafeHeaderValue(correlationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        if (!string.IsNullOrWhiteSpace(taskId) && IsSafeHeaderValue(taskId))
        {
            httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = EventuallyConsistent;
    }

    private static IResult SafeProblem(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        string? taskId,
        string? message = null,
        string? todoRef = null,
        string evidenceSource = AuditEvidenceSource)
    {
        Dictionary<string, object?> details = new()
        {
            ["visibility"] = "metadata_only",
            ["retryReasonCode"] = code,
            ["reasonCategory"] = category,
            ["evidenceSource"] = evidenceSource,
        };

        if (!string.IsNullOrWhiteSpace(todoRef))
        {
            details["todoRef"] = todoRef;
        }

        if (!string.IsNullOrWhiteSpace(taskId) && IsCanonicalIdentifier(taskId))
        {
            details["taskId"] = taskId;
        }

        Dictionary<string, object?> extensions = new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = message ?? MessageFor(category),
            ["correlationId"] = correlationId,
            ["retryable"] = retryable,
            ["clientAction"] = retryable ? "retry" : "no_action",
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
                StatusCodes.Status409Conflict => "Audit evidence is not currently fresh enough for this operation.",
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static string MessageFor(string category) => category switch
    {
        "authentication_failure" => "Authentication is required to access this resource.",
        "tenant_access_denied" => "Access is denied. The caller is not authorized for this operation or resource.",
        "folder_acl_denied" => "Folder access denied.",
        "audit_access_denied" => "Audit access denied.",
        "not_found" => "The requested resource is not available to the caller.",
        "validation_error" => "Request validation failed.",
        "projection_stale" => "The read-model projection is stale. Retry later.",
        "projection_unavailable" => "The read-model projection is unavailable. Retry later.",
        "read_model_unavailable" => "The read model is temporarily unavailable. Retry later.",
        "redacted" => "The requested resource is not available to the caller.",
        _ => "Access is denied. The caller is not authorized for this operation or resource.",
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
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = ReadHeader(httpContext, "X-Forwarded-Principal"),
        };

    private static string? ReadHeader(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Headers.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static string? ReadQuery(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Query.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static string? FirstNonEmpty(StringValues values)
    {
        foreach (string? raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length == 0 || !IsSafeHeaderValue(trimmed))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }

    private static bool IsSafeHeaderValue(string value)
    {
        foreach (char c in value)
        {
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCanonicalIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && CanonicalIdentifierPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("^cursor_[A-Za-z0-9_-]{8,247}$", RegexOptions.CultureInvariant)]
    private static partial Regex CursorPattern();

    [GeneratedRegex(@"^[a-z][A-Za-z0-9_=.,*\- ]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex FilterPattern();
}
