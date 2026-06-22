using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Folders.Queries.OpsConsole;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Hexalith.Folders.Server;

/// <summary>
/// REST routes for the seven read-only ops-console diagnostics (Story 8.2, Bucket B). Each is a metadata-only,
/// projection-backed query honoring authorization-before-observation, the read-op transport guardrails
/// (reject <c>Idempotency-Key</c>, validate <c>X-Hexalith-Freshness</c>), and safe denial. Per the contract
/// spine, <c>projection_stale</c> maps to HTTP 409 (not 503); <c>read_model_unavailable</c> /
/// <c>projection_unavailable</c> map to 503. Self-contained (own helpers) like <see cref="ProviderReadinessEndpoints"/>.
/// </summary>
public static partial class OpsConsoleDiagnosticsEndpoints
{
    private const string FreshnessHeaderName = "X-Hexalith-Freshness";
    private const string TaskHeaderName = "X-Hexalith-Task-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string EventuallyConsistent = "eventually_consistent";
    private const string EvidenceSource = "ops_console_diagnostics";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Maps the seven ops-console diagnostics GET routes.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapOpsConsoleDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/v1/ops-console/readiness-diagnostics", static (
            HttpContext httpContext,
            TenantScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetReadinessDiagnosticsAsync(httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetReadinessDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics", static (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            FolderScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetLockDiagnosticsAsync(folderId, workspaceId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetLockDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics", static (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            FolderScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetDirtyStateDiagnosticsAsync(folderId, workspaceId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetDirtyStateDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics", static (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            FolderScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetFailedOperationDiagnosticsAsync(folderId, workspaceId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetFailedOperationDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics", static (
            string folderId,
            HttpContext httpContext,
            FolderScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetProviderStatusDiagnosticsAsync(folderId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetProviderStatusDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics", static (
            string folderId,
            string workspaceId,
            HttpContext httpContext,
            FolderScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetSyncStatusDiagnosticsAsync(folderId, workspaceId, httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetSyncStatusDiagnostics")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        endpoints.MapGet("/api/v1/ops-console/projection-freshness", static (
            HttpContext httpContext,
            TenantScopedDiagnosticsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            => GetProjectionFreshnessAsync(httpContext, handler, tenantContext, claimTransformEvidence, cancellationToken))
        .WithName("GetProjectionFreshness")
        .AddEndpointFilter<FolderAuditEndpointFilter>();

        return endpoints;
    }

    // -------------------------------------------------------------------------------------------------
    // Tenant-scoped diagnostics (no folder/workspace selector).
    // -------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetReadinessDiagnosticsAsync(
        HttpContext httpContext,
        TenantScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        if (!TryReadOpPreflight(httpContext, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<ReadinessDiagnosticsView> result = await handler.GetReadinessAsync(
            tenantContext.AuthoritativeTenantId,
            tenantContext.PrincipalId,
            claimTransformEvidence.GetEvidence(TenantScopedDiagnosticsQueryHandler.ReadActionToken),
            correlationId,
            ClientTenantIds(httpContext),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> GetProjectionFreshnessAsync(
        HttpContext httpContext,
        TenantScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(claimTransformEvidence);

        if (!TryReadOpPreflight(httpContext, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<ProjectionFreshnessDiagnosticsView> result = await handler.GetProjectionFreshnessAsync(
            tenantContext.AuthoritativeTenantId,
            tenantContext.PrincipalId,
            claimTransformEvidence.GetEvidence(TenantScopedDiagnosticsQueryHandler.ReadActionToken),
            correlationId,
            ClientTenantIds(httpContext),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    // -------------------------------------------------------------------------------------------------
    // Folder/workspace-scoped diagnostics.
    // -------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetLockDiagnosticsAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        FolderScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorkspacePreflight(httpContext, folderId, workspaceId, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<LockDiagnosticsView> result = await handler.GetLockAsync(
            BuildRequest(tenantContext, claimTransformEvidence, correlationId, httpContext, folderId),
            workspaceId,
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> GetDirtyStateDiagnosticsAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        FolderScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorkspacePreflight(httpContext, folderId, workspaceId, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<DirtyStateDiagnosticsView> result = await handler.GetDirtyStateAsync(
            BuildRequest(tenantContext, claimTransformEvidence, correlationId, httpContext, folderId),
            workspaceId,
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> GetFailedOperationDiagnosticsAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        FolderScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorkspacePreflight(httpContext, folderId, workspaceId, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<FailedOperationDiagnosticsView> result = await handler.GetFailedOperationAsync(
            BuildRequest(tenantContext, claimTransformEvidence, correlationId, httpContext, folderId),
            workspaceId,
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> GetSyncStatusDiagnosticsAsync(
        string folderId,
        string workspaceId,
        HttpContext httpContext,
        FolderScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorkspacePreflight(httpContext, folderId, workspaceId, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<SyncStatusDiagnosticsView> result = await handler.GetSyncStatusAsync(
            BuildRequest(tenantContext, claimTransformEvidence, correlationId, httpContext, folderId),
            workspaceId,
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    private static async Task<IResult> GetProviderStatusDiagnosticsAsync(
        string folderId,
        HttpContext httpContext,
        FolderScopedDiagnosticsQueryHandler handler,
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        CancellationToken cancellationToken)
    {
        if (!TryReadFolderPreflight(httpContext, folderId, out string? correlationId, out IResult? problem))
        {
            return problem!;
        }

        OpsConsoleDiagnosticReadResult<ProviderStatusDiagnosticsView> result = await handler.GetProviderStatusAsync(
            BuildRequest(tenantContext, claimTransformEvidence, correlationId, httpContext, folderId),
            cancellationToken).ConfigureAwait(false);

        return ToHttpResult(httpContext, result);
    }

    // -------------------------------------------------------------------------------------------------
    // Preflight / request building.
    // -------------------------------------------------------------------------------------------------

    private static bool TryReadWorkspacePreflight(
        HttpContext httpContext,
        string folderId,
        string workspaceId,
        out string? correlationId,
        out IResult? problem)
    {
        if (!TryReadOpPreflight(httpContext, out correlationId, out problem))
        {
            return false;
        }

        if (!IsCanonicalPathId(folderId) || !IsCanonicalPathId(workspaceId))
        {
            problem = SafeProblem(StatusCodes.Status400BadRequest, "validation_error", "validation_error", retryable: false, correlationId);
            return false;
        }

        return true;
    }

    private static bool TryReadFolderPreflight(
        HttpContext httpContext,
        string folderId,
        out string? correlationId,
        out IResult? problem)
    {
        if (!TryReadOpPreflight(httpContext, out correlationId, out problem))
        {
            return false;
        }

        if (!IsCanonicalPathId(folderId))
        {
            problem = SafeProblem(StatusCodes.Status400BadRequest, "validation_error", "validation_error", retryable: false, correlationId);
            return false;
        }

        return true;
    }

    private static bool TryReadOpPreflight(HttpContext httpContext, out string? correlationId, out IResult? problem)
    {
        problem = null;
        if (!TryReadCorrelation(httpContext, out correlationId))
        {
            problem = SafeProblem(StatusCodes.Status400BadRequest, "validation_error", "unsafe_correlation_id", retryable: false, correlationId: null);
            return false;
        }

        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            problem = SafeProblem(StatusCodes.Status400BadRequest, "validation_error", "idempotency_key_not_allowed", retryable: false, correlationId);
            return false;
        }

        string? freshness = ReadHeader(httpContext, FreshnessHeaderName);
        if (freshness is not null && !string.Equals(freshness, EventuallyConsistent, StringComparison.Ordinal))
        {
            problem = SafeProblem(StatusCodes.Status400BadRequest, "validation_error", "unsupported_read_consistency", retryable: false, correlationId);
            return false;
        }

        return true;
    }

    private static DiagnosticReadRequest BuildRequest(
        ITenantContextAccessor tenantContext,
        IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
        string? correlationId,
        HttpContext httpContext,
        string folderId)
        => new(
            tenantContext.AuthoritativeTenantId,
            tenantContext.PrincipalId,
            claimTransformEvidence.GetEvidence(FolderScopedDiagnosticsQueryHandler.ActionToken),
            folderId,
            correlationId,
            ReadHeader(httpContext, TaskHeaderName),
            ClientTenantIds(httpContext),
            ClientPrincipalIds(httpContext));

    // -------------------------------------------------------------------------------------------------
    // Result mapping.
    // -------------------------------------------------------------------------------------------------

    private static IResult ToHttpResult<TView>(HttpContext httpContext, OpsConsoleDiagnosticReadResult<TView> result)
        where TView : class
    {
        switch (result.Code)
        {
            case DiagnosticReadResultCode.AuthenticationRequired:
                return SafeProblem(StatusCodes.Status401Unauthorized, "authentication_failure", "authentication_failure", retryable: false, result.CorrelationId);
            case DiagnosticReadResultCode.AuthorizationDenied:
                return SafeProblem(StatusCodes.Status403Forbidden, "authorization_denied", "denied_safe", retryable: false, result.CorrelationId);
            case DiagnosticReadResultCode.NotFoundSafe:
                return SafeProblem(StatusCodes.Status404NotFound, "not_found", "not_found", retryable: false, result.CorrelationId);
            case DiagnosticReadResultCode.ProjectionStale:
                // Diagnostics-specific: stale projection surfaces as 409 (not 503) per the spine.
                return SafeProblem(StatusCodes.Status409Conflict, "projection_stale", "projection_stale", retryable: true, result.CorrelationId);
            case DiagnosticReadResultCode.ProjectionUnavailable:
                return SafeProblem(StatusCodes.Status503ServiceUnavailable, "projection_unavailable", "projection_unavailable", retryable: true, result.CorrelationId);
            case DiagnosticReadResultCode.ReadModelUnavailable:
                return SafeProblem(StatusCodes.Status503ServiceUnavailable, "read_model_unavailable", "read_model_unavailable", retryable: true, result.CorrelationId);
        }

        AddSuccessHeaders(httpContext, result.CorrelationId, EventuallyConsistent);
        return Results.Json(result.Payload, ResponseJsonOptions);
    }

    // -------------------------------------------------------------------------------------------------
    // Shared metadata-only helpers (mirrors ProviderReadinessEndpoints).
    // -------------------------------------------------------------------------------------------------

    private static IResult SafeProblem(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId)
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
                ["evidenceSource"] = EvidenceSource,
            },
        };

        return Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: statusCode switch
            {
                StatusCodes.Status400BadRequest => "Validation failure.",
                StatusCodes.Status401Unauthorized => "Authentication required.",
                StatusCodes.Status404NotFound => "Not found.",
                StatusCodes.Status409Conflict => "Diagnostic projection is stale.",
                StatusCodes.Status503ServiceUnavailable => "Diagnostic evidence unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: extensions);
    }

    private static void AddSuccessHeaders(HttpContext httpContext, string correlationId, string freshness)
    {
        if (IsSafeHeaderValue(correlationId))
        {
            httpContext.Response.Headers[CorrelationHeaderName] = correlationId;
        }

        httpContext.Response.Headers[FreshnessHeaderName] = freshness;
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

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext httpContext)
        // Principal identity is header-only — query-string sources are not accepted to reduce attack surface.
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = ReadHeader(httpContext, "X-Forwarded-Principal"),
        };

    private static bool TryReadCorrelation(HttpContext httpContext, out string? correlationId)
    {
        correlationId = null;
        if (!httpContext.Request.Headers.TryGetValue(CorrelationHeaderName, out StringValues values))
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

    private static bool IsCanonicalPathId(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 256
            && CanonicalIdentifierPattern().IsMatch(value)
            && !IsSensitiveDiagnosticValue(value);

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
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);
    }

    private static string MessageFor(string category)
        => category switch
        {
            "authentication_failure" => "Authentication is required to access this resource.",
            "validation_error" => "Request validation failed.",
            "not_found" => "The requested diagnostic is not available.",
            "projection_stale" => "The backing diagnostic projection is stale beyond the safe threshold.",
            "projection_unavailable" or "read_model_unavailable" => "Diagnostic evidence is temporarily unavailable. Retry later.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
