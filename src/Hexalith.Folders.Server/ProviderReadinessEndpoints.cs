using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

public static class ProviderReadinessEndpoints
{
    private const string FreshnessHeaderName = "X-Hexalith-Freshness";
    private const string SnapshotPerTask = "snapshot_per_task";

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

        return endpoints;
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

    private static IResult SafeProblem(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        TimeSpan? retryAfter = null)
    {
        Dictionary<string, object?> extensions = new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = MessageFor(category),
            ["correlationId"] = correlationId,
            ["retryable"] = retryable,
            ["clientAction"] = retryable ? "retry" : "no_action",
            ["details"] = new Dictionary<string, object?>
            {
                ["visibility"] = "metadata_only",
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

    private static bool TryParseCapability(string? value, out ProviderReadinessRequestedCapability capability)
    {
        capability = value switch
        {
            "repository_creation" => ProviderReadinessRequestedCapability.RepositoryCreation,
            "existing_repository_binding" => ProviderReadinessRequestedCapability.ExistingRepositoryBinding,
            "branch_ref_policy" => ProviderReadinessRequestedCapability.BranchRefPolicy,
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
}
