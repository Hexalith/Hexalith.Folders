using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Projections.Audit;
using Hexalith.Folders.Queries.Audit;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        string? correlationId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id");

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
            _ => MapAuditQueryResult(result.Code, result.CorrelationId, result.TaskId, AuditEvidenceSource),
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

        string? correlationId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id");

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
            _ => MapAuditQueryResult(result.Code, result.CorrelationId, result.TaskId, AuditEvidenceSource),
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

        string? correlationId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id");

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
            _ => MapAuditQueryResult(result.Code, result.CorrelationId, result.TaskId, TimelineEvidenceSource),
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

        string? correlationId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");
        string? taskId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id");

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
            _ => MapAuditQueryResult(result.Code, result.CorrelationId, result.TaskId, TimelineEvidenceSource),
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

        string? rawCursor = FolderHttpHeaderReader.ReadQuery(httpContext, "cursor");
        if (rawCursor is not null)
        {
            if (rawCursor.Length is < 1 or > 256 || !CursorPattern().IsMatch(rawCursor))
            {
                return FolderProblemDetailsFactory.ForAudit(
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

        string? rawLimit = FolderHttpHeaderReader.ReadQuery(httpContext, "limit");
        if (rawLimit is not null)
        {
            if (!int.TryParse(rawLimit, out int parsedLimit) || parsedLimit < 1 || parsedLimit > OpenApiPageLimitCeiling)
            {
                return FolderProblemDetailsFactory.ForAudit(
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

        string? rawFilter = FolderHttpHeaderReader.ReadQuery(httpContext, "filter");
        if (rawFilter is not null)
        {
            // Validate the spine wire-shape regex first; a malformed filter is validation_error.
            if (rawFilter.Length is < 1 or > 256 || !FilterPattern().IsMatch(rawFilter))
            {
                return FolderProblemDetailsFactory.ForAudit(
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
            return FolderProblemDetailsFactory.ForAudit(
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
            return FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "idempotency_key_not_allowed",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                message: "Idempotency-Key is not accepted on read operations.",
                evidenceSource: evidenceSource);
        }

        if (!FolderCanonicalSegmentIdentifier.IsValid(folderId)
            || (extraIdentifier is not null && !FolderCanonicalSegmentIdentifier.IsValid(extraIdentifier))
            || (correlationId is not null && !FolderCanonicalSegmentIdentifier.IsValid(correlationId))
            || (taskId is not null && !FolderCanonicalSegmentIdentifier.IsValid(taskId)))
        {
            return FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: FolderCanonicalSegmentIdentifier.IsValid(correlationId) ? correlationId : null,
                taskId: FolderCanonicalSegmentIdentifier.IsValid(taskId) ? taskId : null,
                evidenceSource: evidenceSource);
        }

        string? freshness = FolderHttpHeaderReader.ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForAudit(
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

    private static IResult MapAuditQueryResult(
        AuditQueryResultCode code,
        string? correlationId,
        string? taskId,
        string evidenceSource)
        => code switch
        {
            AuditQueryResultCode.AuthenticationRequired => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "authentication_failure",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.TenantAccessDenied => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "tenant_access_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.FolderAclDenied => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status403Forbidden,
                category: "folder_acl_denied",
                code: "folder_acl_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.AuditAccessDenied => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status403Forbidden,
                category: "audit_access_denied",
                code: "audit_access_denied",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.NotFoundSafe => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status404NotFound,
                category: "not_found",
                code: "not_found",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ValidationError => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status400BadRequest,
                category: "validation_error",
                code: "validation_error",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ProjectionStale => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status409Conflict,
                category: "projection_stale",
                code: "projection_stale",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ProjectionUnavailable => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status503ServiceUnavailable,
                category: "projection_unavailable",
                code: "projection_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.ReadModelUnavailable => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "read_model_unavailable",
                retryable: true,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            AuditQueryResultCode.Redacted => FolderProblemDetailsFactory.ForAudit(
                StatusCodes.Status404NotFound,
                category: "redacted",
                code: "redacted",
                retryable: false,
                correlationId: correlationId,
                taskId: taskId,
                evidenceSource: evidenceSource),
            _ => FolderProblemDetailsFactory.ForAudit(
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
        if (!string.IsNullOrWhiteSpace(correlationId) && FolderHttpHeaderReader.IsSafeHeaderValue(correlationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        if (!string.IsNullOrWhiteSpace(taskId) && FolderHttpHeaderReader.IsSafeHeaderValue(taskId))
        {
            httpContext.Response.Headers["X-Hexalith-Task-Id"] = taskId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = EventuallyConsistent;
    }

    private static IReadOnlyDictionary<string, string?> ClientTenantIds(HttpContext httpContext)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_tenant_id"] = FolderHttpHeaderReader.ReadQuery(httpContext, "tenantId"),
            ["query_managed_tenant_id"] = FolderHttpHeaderReader.ReadQuery(httpContext, "managedTenantId"),
            ["header_hexalith_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Forwarded-Tenant"),
        };

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext httpContext)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Forwarded-Principal"),
        };

    [GeneratedRegex("^cursor_[A-Za-z0-9_-]{8,247}$", RegexOptions.CultureInvariant)]
    private static partial Regex CursorPattern();

    [GeneratedRegex(@"^[a-z][A-Za-z0-9_=.,*\- ]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex FilterPattern();
}
