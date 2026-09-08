using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

/// <summary>
/// Builds metadata-only ProblemDetails envelopes for Folders Server HTTP surfaces.
/// </summary>
internal static class FolderProblemDetailsFactory
{
    /// <summary>
    /// Creates a Domain REST ProblemDetails result, including optional <c>taskId</c> extras.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="category">The canonical reason category.</param>
    /// <param name="code">The canonical reason code.</param>
    /// <param name="retryable">Whether the caller may retry.</param>
    /// <param name="correlationId">The correlation identifier to echo when already validated.</param>
    /// <param name="taskId">The optional task identifier extra.</param>
    /// <param name="message">An optional message override.</param>
    /// <returns>A ProblemDetails <see cref="IResult"/>.</returns>
    public static IResult ForDomain(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        string? taskId,
        string? message = null)
    {
        Dictionary<string, object?> details = CreateDetails(code, category, "http_boundary");
        if (!string.IsNullOrWhiteSpace(taskId) && FolderCanonicalSegmentIdentifier.IsValid(taskId))
        {
            details["taskId"] = taskId;
        }

        if (category is "unknown_provider_outcome" or "reconciliation_required")
        {
            details["finalState"] = category;
        }

        Dictionary<string, object?> extensions = CreateExtensions(
            category,
            code,
            message ?? DomainMessageFor(category),
            correlationId,
            retryable,
            FolderCanonicalErrorMapper.ClientActionFor(category, retryable),
            details);
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            extensions["taskId"] = taskId;
        }

        return Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: DomainTitleFor(statusCode, category),
            statusCode: statusCode,
            extensions: extensions);
    }

    /// <summary>
    /// Creates an Audit REST ProblemDetails result, including <c>evidenceSource</c> and optional <c>todoRef</c>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="category">The canonical reason category.</param>
    /// <param name="code">The canonical reason code.</param>
    /// <param name="retryable">Whether the caller may retry.</param>
    /// <param name="correlationId">The correlation identifier to echo when already validated.</param>
    /// <param name="taskId">The optional task identifier extra.</param>
    /// <param name="message">An optional message override.</param>
    /// <param name="todoRef">An optional TODO reference extra.</param>
    /// <param name="evidenceSource">The audit evidence source extra.</param>
    /// <returns>A ProblemDetails <see cref="IResult"/>.</returns>
    public static IResult ForAudit(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        string? taskId,
        string? message = null,
        string? todoRef = null,
        string evidenceSource = "audit")
    {
        Dictionary<string, object?> details = CreateDetails(code, category, evidenceSource);
        if (!string.IsNullOrWhiteSpace(todoRef))
        {
            details["todoRef"] = todoRef;
        }

        if (!string.IsNullOrWhiteSpace(taskId) && FolderCanonicalSegmentIdentifier.IsValid(taskId))
        {
            details["taskId"] = taskId;
        }

        Dictionary<string, object?> extensions = CreateExtensions(
            category,
            code,
            message ?? AuditMessageFor(category),
            correlationId,
            retryable,
            retryable ? "retry" : "no_action",
            details);
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

    /// <summary>
    /// Creates a Provider readiness ProblemDetails result, including optional <c>retryAfterSeconds</c>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="category">The canonical reason category.</param>
    /// <param name="code">The canonical reason code.</param>
    /// <param name="retryable">Whether the caller may retry.</param>
    /// <param name="correlationId">The correlation identifier; sensitive values are replaced.</param>
    /// <param name="retryAfter">An optional retry-after duration extra.</param>
    /// <returns>A ProblemDetails <see cref="IResult"/>.</returns>
    public static IResult ForProviderReadiness(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId,
        TimeSpan? retryAfter = null)
    {
        Dictionary<string, object?> extensions = CreateExtensions(
            category,
            code,
            ProviderMessageFor(category),
            FolderCanonicalPathIdentifier.SanitizeCorrelationId(correlationId),
            retryable,
            retryable ? "retry" : "no_action",
            CreateDetails(code, category, "provider_readiness"));
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

    /// <summary>
    /// Creates an OpsConsole diagnostics ProblemDetails result.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="category">The canonical reason category.</param>
    /// <param name="code">The canonical reason code.</param>
    /// <param name="retryable">Whether the caller may retry.</param>
    /// <param name="correlationId">The correlation identifier; sensitive values are replaced.</param>
    /// <returns>A ProblemDetails <see cref="IResult"/>.</returns>
    public static IResult ForOpsConsole(
        int statusCode,
        string category,
        string code,
        bool retryable,
        string? correlationId)
    {
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
            extensions: CreateExtensions(
                category,
                code,
                OpsConsoleMessageFor(category),
                FolderCanonicalPathIdentifier.SanitizeCorrelationId(correlationId),
                retryable,
                retryable ? "retry" : "no_action",
                CreateDetails(code, category, "ops_console_diagnostics")));
    }

    private static Dictionary<string, object?> CreateDetails(string code, string category, string evidenceSource)
        => new()
        {
            ["visibility"] = "metadata_only",
            ["retryReasonCode"] = code,
            ["reasonCategory"] = category,
            ["evidenceSource"] = evidenceSource,
        };

    private static Dictionary<string, object?> CreateExtensions(
        string category,
        string code,
        string message,
        string? correlationId,
        bool retryable,
        string clientAction,
        Dictionary<string, object?> details)
        => new()
        {
            ["category"] = category,
            ["code"] = code,
            ["message"] = message,
            ["correlationId"] = correlationId,
            ["retryable"] = retryable,
            ["clientAction"] = clientAction,
            ["details"] = details,
        };

    private static string DomainTitleFor(int statusCode, string category)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Validation failure.",
            StatusCodes.Status401Unauthorized => "Authentication required.",
            StatusCodes.Status404NotFound => "Resource not available.",
            StatusCodes.Status408RequestTimeout => "Query timeout.",
            StatusCodes.Status409Conflict => category == "idempotency_key_expired"
                ? "Idempotency key expired."
                : "Idempotency conflict.",
            StatusCodes.Status413PayloadTooLarge => "Response limit exceeded.",
            StatusCodes.Status416RangeNotSatisfiable => "Range not satisfiable.",
            StatusCodes.Status422UnprocessableEntity => "Validation outcome.",
            StatusCodes.Status503ServiceUnavailable => category == "idempotency_admission_unavailable"
                ? "Idempotency admission unavailable."
                : "Read model unavailable.",
            _ => "Authorization denied.",
        };

    private static string DomainMessageFor(string category)
        => category switch
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
            "idempotency_key_expired" => "The supplied idempotency key is no longer reusable. Refresh state, then submit with a new key.",
            "idempotency_admission_unavailable" => "Idempotency admission is temporarily unavailable. Retry later.",
            "unknown_provider_outcome" => "Provider outcome is unknown and requires safe reconciliation.",
            "reconciliation_required" => "Reconciliation is required before this operation can continue.",
            "provider_unavailable" => "Provider evidence is temporarily unavailable. Retry later.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };

    private static string AuditMessageFor(string category)
        => category switch
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

    private static string ProviderMessageFor(string category)
        => category switch
        {
            "authentication_failure" => "Authentication is required to access this resource.",
            "validation_error" => "Request validation failed.",
            "provider_rate_limited" => "Provider readiness is rate limited. Retry later.",
            "provider_unavailable" or "provider_transient_failure" => "Provider readiness is temporarily unavailable. Retry later.",
            "projection_stale" or "projection_unavailable" => "Authorization evidence is not currently fresh enough for this operation.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };

    private static string OpsConsoleMessageFor(string category)
        => category switch
        {
            "authentication_failure" => "Authentication is required to access this resource.",
            "validation_error" => "Request validation failed.",
            "not_found" => "The requested diagnostic is not available.",
            "projection_stale" => "The backing diagnostic projection is stale beyond the safe threshold.",
            "projection_unavailable" or "read_model_unavailable" => "Diagnostic evidence is temporarily unavailable. Retry later.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };
}
