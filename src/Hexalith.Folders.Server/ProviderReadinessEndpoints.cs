using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

public static partial class ProviderReadinessEndpoints
{
    private const string FreshnessHeaderName = "X-Hexalith-Freshness";
    private const string SnapshotPerTask = "snapshot_per_task";
    private const string EventuallyConsistent = "eventually_consistent";
    private const int DefaultSupportEvidenceLimit = 50;
    private const int MaxSupportEvidenceLimit = 100;
    private const int OpenApiPageLimitCeiling = 1000;

    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapProviderReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/v1/provider-readiness/validations", async (
            HttpContext httpContext,
            ProviderReadinessValidationService service,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await ValidateProviderReadinessAsync(
                httpContext,
                service,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("ValidateProviderReadiness")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/provider-readiness/support-evidence", async (
            HttpContext httpContext,
            ProviderSupportEvidenceQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await GetProviderSupportEvidenceAsync(
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("GetProviderSupportEvidence")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/provider-bindings/{providerBindingRef}", async (
            string providerBindingRef,
            HttpContext httpContext,
            GetProviderBindingQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => await GetProviderBindingAsync(
                providerBindingRef,
                httpContext,
                handler,
                tenantContext,
                claimTransformEvidence,
                cancellationToken).ConfigureAwait(false))
        .WithName("GetProviderBinding")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        return endpoints;
    }

    private static async Task<IResult> GetProviderBindingAsync(
        string providerBindingRef,
        HttpContext httpContext,
        GetProviderBindingQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        if (!TryReadSupportEvidenceCorrelation(httpContext, out string? correlationId))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsafe_correlation_id",
                retryable: false,
                correlationId: null);
        }

        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            // Canonical read-op rejection code per Story 8.1 DD1 / AC3 — must match every other
            // read route (idempotency_key_not_allowed), not the legacy provider-readiness variant.
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_allowed",
                retryable: false,
                correlationId);
        }

        string? freshness = FolderHttpHeaderReader.ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsupported_read_consistency",
                retryable: false,
                correlationId);
        }

        if (!FolderCanonicalPathIdentifier.IsValid(providerBindingRef))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "validation_error",
                retryable: false,
                correlationId);
        }

        GetProviderBindingQueryResult result = await handler.HandleAsync(
            new GetProviderBindingQuery(
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(GetProviderBindingQueryHandler.ReadActionToken),
                providerBindingRef,
                correlationId,
                ClientTenantIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static IResult ToHttpResult(HttpContext httpContext, GetProviderBindingQueryResult result)
    {
        switch (result.Code)
        {
            case GetProviderBindingQueryResultCode.AuthenticationRequired:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status401Unauthorized,
                    "authentication_failure",
                    "authentication_failure",
                    retryable: false,
                    result.CorrelationId);
            case GetProviderBindingQueryResultCode.AuthorizationDenied:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status403Forbidden,
                    "authorization_denied",
                    "denied_safe",
                    retryable: false,
                    result.CorrelationId);
            case GetProviderBindingQueryResultCode.NotFoundSafe:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status404NotFound,
                    "not_found",
                    "not_found",
                    retryable: false,
                    result.CorrelationId);
            case GetProviderBindingQueryResultCode.ProjectionStale:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_stale",
                    "projection_stale",
                    retryable: true,
                    result.CorrelationId);
            case GetProviderBindingQueryResultCode.ProjectionUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_unavailable",
                    "projection_unavailable",
                    retryable: true,
                    result.CorrelationId);
            case GetProviderBindingQueryResultCode.ReadModelUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "read_model_unavailable",
                    "read_model_unavailable",
                    retryable: true,
                    result.CorrelationId);
        }

        AddSuccessHeaders(httpContext, result.CorrelationId, result.Freshness.ReadConsistency);
        return Results.Json(
            new ProviderBindingHttpResponse(
                result.ProviderBindingRef!,
                result.ProviderFamilyRef!,
                result.CapabilityProfileRef!,
                "credential_reference_redacted",
                result.Freshness),
            ResponseJsonOptions);
    }

    private static async Task<IResult> GetProviderSupportEvidenceAsync(
        HttpContext httpContext,
        ProviderSupportEvidenceQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        if (!TryReadSupportEvidenceCorrelation(httpContext, out string? correlationId))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsafe_correlation_id",
                retryable: false,
                correlationId: null);
        }

        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            // Canonical read-op rejection code per Story 8.1 DD1 / AC3 — must match every other
            // read route (idempotency_key_not_allowed), not the legacy provider-readiness variant.
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_allowed",
                retryable: false,
                correlationId);
        }

        string? freshness = FolderHttpHeaderReader.ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsupported_read_consistency",
                retryable: false,
                correlationId);
        }

        if (!TryReadSupportEvidencePagination(httpContext, out string? cursor, out int limit))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "invalid_pagination",
                retryable: false,
                correlationId);
        }

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(
            new ProviderSupportEvidenceQuery(
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                claimTransformEvidence.GetEvidence(ProviderSupportEvidenceQueryHandler.ReadActionToken),
                correlationId,
                cursor,
                limit,
                ClientTenantIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> ValidateProviderReadinessAsync(
        HttpContext httpContext,
        ProviderReadinessValidationService service,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        string? correlationId = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Correlation-Id");
        if (FolderHttpHeaderReader.ReadHeader(httpContext, "Idempotency-Key") is not null)
        {
            // Canonical read-op rejection code per Story 8.1 DD1 / AC3 — must match every other
            // read route (idempotency_key_not_allowed), not the legacy provider-readiness variant.
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_allowed",
                retryable: false,
                correlationId);
        }

        string? freshness = FolderHttpHeaderReader.ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, SnapshotPerTask, StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsupported_read_consistency",
                retryable: false,
                correlationId);
        }

        ProviderReadinessHttpRequest? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<ProviderReadinessHttpRequest>(RequestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "malformed_provider_readiness_request",
                retryable: false,
                correlationId);
        }

        if (body is null
            || string.IsNullOrWhiteSpace(body.ProviderBindingRef)
            || !TryParseCapability(body.RequestedCapability, out ProviderReadinessRequestedCapability requestedCapability))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "malformed_provider_readiness_request",
                retryable: false,
                correlationId);
        }

        ProviderReadinessValidationResult result = await service.ValidateAsync(
            new ProviderReadinessValidationRequest(
                tenantContext.AuthoritativeTenantId,
                tenantContext.PrincipalId,
                body.ProviderBindingRef,
                requestedCapability,
                correlationId,
                claimTransformEvidence.GetEvidence(ProviderReadinessValidationService.ReadActionToken),
                ClientTenantIds(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static IResult ToHttpResult(HttpContext httpContext, ProviderReadinessValidationResult result)
    {
        switch (result.Code)
        {
            case ProviderReadinessResultCode.AuthenticationRequired:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status401Unauthorized,
                    "authentication_failure",
                    "authentication_failure",
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.AuthorizationDenied:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status403Forbidden,
                    "authorization_denied",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ValidationFailed:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status400BadRequest,
                    "validation_error",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ProjectionStale:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_stale",
                    "projection_stale",
                    retryable: true,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ProjectionUnavailable:
            case ProviderReadinessResultCode.ReadModelUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_unavailable",
                    "projection_unavailable",
                    retryable: true,
                    result.CorrelationId);
        }

        if (string.Equals(result.CategoryCode, "provider_rate_limited", StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status429TooManyRequests,
                "provider_rate_limited",
                "provider_rate_limited",
                retryable: true,
                result.CorrelationId,
                result.RetryAfter);
        }

        if (string.Equals(result.CategoryCode, "provider_unavailable", StringComparison.Ordinal)
            || string.Equals(result.CategoryCode, "provider_transient_failure", StringComparison.Ordinal))
        {
            return FolderProblemDetailsFactory.ForProviderReadiness(
                StatusCodes.Status503ServiceUnavailable,
                result.CategoryCode,
                result.CategoryCode,
                retryable: result.Retryable,
                result.CorrelationId,
                result.RetryAfter);
        }

        AddSuccessHeaders(httpContext, result);
        return Results.Json(
            new ProviderReadinessOperatorHttpResponse(
                "authorized_operator",
                result.ProviderBindingRef,
                result.Status,
                result.CapabilityProfileRef,
                result.Evidence,
                result.CategoryCode == "none" ? null : result.CategoryCode,
                result.SafeRemediationCode,
                result.ReasonCode,
                result.Retryable,
                result.RetryAfter is null ? null : (long)Math.Ceiling(result.RetryAfter.Value.TotalSeconds),
                result.RemediationCategory,
                result.ProviderReference,
                result.CorrelationId,
                result.Freshness),
            ResponseJsonOptions);
    }

    private static IResult ToHttpResult(HttpContext httpContext, ProviderSupportEvidenceQueryResult result)
    {
        switch (result.Code)
        {
            case ProviderSupportEvidenceQueryResultCode.AuthenticationRequired:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status401Unauthorized,
                    "authentication_failure",
                    "authentication_failure",
                    retryable: false,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.AuthorizationDenied:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status403Forbidden,
                    "authorization_denied",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProjectionStale:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_stale",
                    "projection_stale",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProjectionUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_unavailable",
                    "projection_unavailable",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProviderUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "provider_unavailable",
                    "provider_unavailable",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable:
                return FolderProblemDetailsFactory.ForProviderReadiness(
                    StatusCodes.Status503ServiceUnavailable,
                    "read_model_unavailable",
                    result.ReasonCode,
                    retryable: true,
                    result.CorrelationId);
        }

        AddSuccessHeaders(httpContext, result.CorrelationId, result.Freshness.ReadConsistency);
        return Results.Json(
            new ProviderSupportEvidenceListHttpResponse(
                result.Items,
                result.Page,
                result.Freshness),
            ResponseJsonOptions);
    }

    private static void AddSuccessHeaders(HttpContext httpContext, ProviderReadinessValidationResult result)
    {
        if (FolderHttpHeaderReader.IsSafeHeaderValue(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
    }

    private static void AddSuccessHeaders(HttpContext httpContext, string correlationId, string freshness)
    {
        if (FolderHttpHeaderReader.IsSafeHeaderValue(correlationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = freshness;
    }

    private static bool TryParseCapability(string? value, out ProviderReadinessRequestedCapability capability)
    {
        capability = value switch
        {
            "repository_creation" => ProviderReadinessRequestedCapability.RepositoryCreation,
            "existing_repository_binding" => ProviderReadinessRequestedCapability.ExistingRepositoryBinding,
            "branch_ref_policy" => ProviderReadinessRequestedCapability.BranchRefPolicy,
            "workspace_preparation" => ProviderReadinessRequestedCapability.WorkspacePreparation,
            "file_operations" => ProviderReadinessRequestedCapability.FileOperations,
            "commit_status" => ProviderReadinessRequestedCapability.CommitStatus,
            "provider_errors" => ProviderReadinessRequestedCapability.ProviderErrors,
            "failure_behavior" => ProviderReadinessRequestedCapability.FailureBehavior,
            _ => ProviderReadinessRequestedCapability.RepositoryCreation,
        };

        return value is "repository_creation"
            or "existing_repository_binding"
            or "branch_ref_policy"
            or "file_operations"
            or "commit_status"
            or "provider_errors"
            or "failure_behavior";
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

    private static bool TryReadSupportEvidencePagination(
        HttpContext httpContext,
        out string? cursor,
        out int limit)
    {
        cursor = null;
        limit = DefaultSupportEvidenceLimit;

        string? rawCursor = FolderHttpHeaderReader.ReadQuery(httpContext, "cursor");
        if (rawCursor is not null)
        {
            if (!CursorPattern().IsMatch(rawCursor))
            {
                return false;
            }

            cursor = rawCursor;
        }

        string? rawLimit = FolderHttpHeaderReader.ReadQuery(httpContext, "limit");
        if (rawLimit is null)
        {
            return true;
        }

        if (!int.TryParse(rawLimit, out int requestedLimit)
            || requestedLimit is < 1 or > OpenApiPageLimitCeiling)
        {
            return false;
        }

        limit = Math.Min(requestedLimit, MaxSupportEvidenceLimit);
        return true;
    }

    private static bool TryReadSupportEvidenceCorrelation(HttpContext httpContext, out string? correlationId)
    {
        correlationId = null;
        if (!httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out StringValues values))
        {
            return true;
        }

        foreach (string? raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string candidate = raw.Trim();
            if (!FolderHttpHeaderReader.IsSafeHeaderValue(candidate)
                || !FolderCanonicalPathIdentifier.IsValid(candidate)
                || FolderSensitiveDiagnosticDetector.IsSensitive(candidate))
            {
                return false;
            }

            correlationId = candidate;
            return true;
        }

        return true;
    }

    private sealed record ProviderReadinessHttpRequest(
        string? ProviderBindingRef,
        string? RequestedCapability);

    private sealed record ProviderReadinessOperatorHttpResponse(
        string Audience,
        string? ProviderBindingRef,
        string Status,
        string? CapabilityProfileRef,
        ProviderReadinessCapabilityEvidence? Evidence,
        string? SanitizedErrorCategory,
        string SafeRemediationCode,
        string SafeReasonCode,
        bool Retryable,
        long? RetryAfterSeconds,
        string RemediationCategory,
        string? ProviderReference,
        string CorrelationId,
        ProviderReadinessFreshness Freshness);

    private sealed record ProviderSupportEvidenceListHttpResponse(
        IReadOnlyList<ProviderSupportEvidenceItem> Items,
        ProviderSupportEvidencePage Page,
        ProviderReadinessFreshness Freshness);

    private sealed record ProviderBindingHttpResponse(
        string ProviderBindingRef,
        string ProviderFamilyRef,
        string CapabilityProfileRef,
        string Redaction,
        ProviderReadinessFreshness Freshness);

    [GeneratedRegex("^cursor_[0-9]{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex CursorPattern();
}
