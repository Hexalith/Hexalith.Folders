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
        .WithName("ValidateProviderReadiness");

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
        .WithName("GetProviderSupportEvidence");

        return endpoints;
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
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsafe_correlation_id",
                retryable: false,
                correlationId: null);
        }

        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_accepted",
                retryable: false,
                correlationId);
        }

        string? freshness = ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "unsupported_read_consistency",
                retryable: false,
                correlationId);
        }

        if (!TryReadSupportEvidencePagination(httpContext, out string? cursor, out int limit))
        {
            return SafeProblem(
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

        string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
        if (ReadHeader(httpContext, "Idempotency-Key") is not null)
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_accepted",
                retryable: false,
                correlationId);
        }

        string? freshness = ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, SnapshotPerTask, StringComparison.Ordinal))
        {
            return SafeProblem(
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
            return SafeProblem(
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
            return SafeProblem(
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
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    "authentication_failure",
                    "authentication_failure",
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.AuthorizationDenied:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    "authorization_denied",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ValidationFailed:
                return SafeProblem(
                    StatusCodes.Status400BadRequest,
                    "validation_error",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_stale",
                    "projection_stale",
                    retryable: true,
                    result.CorrelationId);
            case ProviderReadinessResultCode.ProjectionUnavailable:
            case ProviderReadinessResultCode.ReadModelUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_unavailable",
                    "projection_unavailable",
                    retryable: true,
                    result.CorrelationId);
        }

        if (string.Equals(result.CategoryCode, "provider_rate_limited", StringComparison.Ordinal))
        {
            return SafeProblem(
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
            return SafeProblem(
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
                return SafeProblem(
                    StatusCodes.Status401Unauthorized,
                    "authentication_failure",
                    "authentication_failure",
                    retryable: false,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.AuthorizationDenied:
                return SafeProblem(
                    StatusCodes.Status403Forbidden,
                    "authorization_denied",
                    result.ReasonCode,
                    retryable: false,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProjectionStale:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_stale",
                    "projection_stale",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProjectionUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "projection_unavailable",
                    "projection_unavailable",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ProviderUnavailable:
                return SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "provider_unavailable",
                    "provider_unavailable",
                    retryable: true,
                    result.CorrelationId);
            case ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable:
                return SafeProblem(
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

    private static IResult SafeProblem(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        TimeSpan? retryAfter = null)
    {
        string safeCorrelationId = SafeCorrelationId(correlationId);
        Dictionary<string, object?> extensions = new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = MessageFor(category),
            ["correlationId"] = safeCorrelationId,
            ["retryable"] = retryable,
            ["clientAction"] = retryable ? "retry" : "no_action",
            ["details"] = new Dictionary<string, object?>
            {
                ["visibility"] = "metadata_only",
                ["retryReasonCode"] = code,
                ["reasonCategory"] = category,
                ["evidenceSource"] = "provider_readiness",
            },
        };

        if (retryAfter is not null)
        {
            extensions["retryAfterSeconds"] = (long)Math.Ceiling(retryAfter.Value.TotalSeconds);
        }

        return Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: statusCode switch
            {
                StatusCodes.Status400BadRequest => "Validation failure.",
                StatusCodes.Status401Unauthorized => "Authentication required.",
                StatusCodes.Status429TooManyRequests => "Provider rate limited.",
                StatusCodes.Status503ServiceUnavailable => "Provider readiness unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static void AddSuccessHeaders(HttpContext httpContext, ProviderReadinessValidationResult result)
    {
        if (IsSafeHeaderValue(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = result.Freshness.ReadConsistency;
    }

    private static void AddSuccessHeaders(HttpContext httpContext, string correlationId, string freshness)
    {
        if (IsSafeHeaderValue(correlationId))
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
            ["query_tenant_id"] = ReadQuery(httpContext, "tenantId"),
            ["query_managed_tenant_id"] = ReadQuery(httpContext, "managedTenantId"),
            ["header_hexalith_tenant_id"] = ReadHeader(httpContext, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = ReadHeader(httpContext, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = ReadHeader(httpContext, "X-Forwarded-Tenant"),
        };

    private static bool TryReadSupportEvidencePagination(
        HttpContext httpContext,
        out string? cursor,
        out int limit)
    {
        cursor = null;
        limit = DefaultSupportEvidenceLimit;

        string? rawCursor = ReadQuery(httpContext, "cursor");
        if (rawCursor is not null)
        {
            if (!CursorPattern().IsMatch(rawCursor))
            {
                return false;
            }

            cursor = rawCursor;
        }

        string? rawLimit = ReadQuery(httpContext, "limit");
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
            if (!IsSafeHeaderValue(candidate)
                || candidate.Length > 256
                || !CanonicalIdentifierPattern().IsMatch(candidate)
                || IsSensitiveDiagnosticValue(candidate))
            {
                return false;
            }

            correlationId = candidate;
            return true;
        }

        return true;
    }

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
        => !value.Any(static c => c == '\r' || c == '\n' || char.IsControl(c));

    private static string SafeCorrelationId(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && IsSafeHeaderValue(value)
            && CanonicalIdentifierPattern().IsMatch(value)
            && !IsSensitiveDiagnosticValue(value))
        {
            return value.Trim();
        }

        return $"correlation_{Guid.NewGuid():N}";
    }

    private static bool IsSensitiveDiagnosticValue(string value)
    {
        string canonical = value.Trim().ToLowerInvariant();
        return canonical.Contains("token", StringComparison.Ordinal)
            || canonical.Contains("secret", StringComparison.Ordinal)
            || canonical.Contains("password", StringComparison.Ordinal)
            || canonical.Contains("credential", StringComparison.Ordinal)
            || canonical.Contains("repository", StringComparison.Ordinal)
            || canonical.Contains("repo_", StringComparison.Ordinal)
            || canonical.Contains("repo-", StringComparison.Ordinal)
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || canonical.Contains("diff --git", StringComparison.Ordinal)
            || canonical.Contains("providerpayload", StringComparison.Ordinal)
            || canonical.Contains("privatekey", StringComparison.Ordinal)
            || canonical.Contains("private key", StringComparison.Ordinal)
            || canonical.Contains("installation", StringComparison.Ordinal)
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);
    }

    private static string MessageFor(string category)
        => category switch
        {
            "authentication_failure" => "Authentication is required to access this resource.",
            "validation_error" => "Request validation failed.",
            "provider_rate_limited" => "Provider readiness is rate limited. Retry later.",
            "provider_unavailable" or "provider_transient_failure" => "Provider readiness is temporarily unavailable. Retry later.",
            "projection_stale" or "projection_unavailable" => "Authorization evidence is not currently fresh enough for this operation.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };

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

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("^cursor_[0-9]{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex CursorPattern();

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
