using System;
using System.Collections.Generic;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Client.Serialization;

using Newtonsoft.Json;

namespace Hexalith.Folders.Cli.Tests.TestSupport;

/// <summary>Canned SDK shapes and exceptions for hermetic CLI tests.</summary>
internal static class TestData
{
    /// <summary>A canned <c>202 Accepted</c> command JSON body.</summary>
    /// <param name="idempotentReplay">Whether the response marks an idempotent replay.</param>
    /// <returns>The JSON body.</returns>
    public static string AcceptedJson(bool idempotentReplay = false) =>
        $$"""
        {"acceptedAt":"2026-05-27T12:00:00+00:00","correlationId":"corr_01HZY7Z6N7J4Q2X8Y9V0COR001","taskId":"task_01HZY7Z6N7J4Q2X8Y9V0TSK001","status":"accepted","idempotentReplay":{{(idempotentReplay ? "true" : "false")}}}
        """;

    /// <summary>Builds a typed problem exception carrying the supplied canonical category.</summary>
    /// <param name="category">The canonical error category.</param>
    /// <param name="correlationId">The correlation ID on the problem.</param>
    /// <param name="rawResponse">The raw HTTP response body carried by the exception. The CLI must project only typed fields and never echo this raw text.</param>
    /// <returns>The exception the SDK would throw for that category.</returns>
    public static HexalithFoldersApiException<ProblemDetails> ProblemException(
        CanonicalErrorCategory category,
        string correlationId = "correlation_TEST_0001",
        string? rawResponse = null)
    {
        (int status, CanonicalErrorCode code, bool retryable, ProblemDetailsClientAction clientAction) = category switch
        {
            CanonicalErrorCategory.Read_model_unavailable =>
                (503, CanonicalErrorCode.Projection_unavailable, true, ProblemDetailsClientAction.Retry),
            CanonicalErrorCategory.Lock_conflict =>
                (423, CanonicalErrorCode.Workspace_locked, true, ProblemDetailsClientAction.Retry),
            CanonicalErrorCategory.Validation_error =>
                (400, CanonicalErrorCode.Validation_error, false, ProblemDetailsClientAction.Revise_request),
            CanonicalErrorCategory.Unknown_provider_outcome =>
                (503, CanonicalErrorCode.Unknown_provider_outcome, false, ProblemDetailsClientAction.Wait_for_reconciliation),
            CanonicalErrorCategory.Reconciliation_required =>
                (503, CanonicalErrorCode.Reconciliation_required, false, ProblemDetailsClientAction.Wait_for_reconciliation),
            CanonicalErrorCategory.Tenant_access_denied =>
                (404, CanonicalErrorCode.Resource_unavailable, false, ProblemDetailsClientAction.No_action),
            CanonicalErrorCategory.Idempotency_conflict =>
                (409, CanonicalErrorCode.Idempotency_conflict, false, ProblemDetailsClientAction.Revise_request),
            CanonicalErrorCategory.Provider_unavailable =>
                (503, CanonicalErrorCode.Provider_unavailable, true, ProblemDetailsClientAction.Retry),
            CanonicalErrorCategory.State_transition_invalid =>
                (422, CanonicalErrorCode.State_transition_invalid, false, ProblemDetailsClientAction.Revise_request),
            CanonicalErrorCategory.Authentication_failure =>
                (401, CanonicalErrorCode.Authentication_required, false, ProblemDetailsClientAction.Check_credentials),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "The fixture must use a reachable runtime problem tuple."),
        };
        BindOriginatingOperation(category);
        ProblemDetails problem = new()
        {
            Type = "about:blank",
            Title = category.ToString(),
            Status = status,
            Category = category,
            Code = code,
            Message = "Synthetic metadata-only problem.",
            CorrelationId = correlationId,
            Retryable = retryable,
            ClientAction = clientAction,
            Details = new Details
            {
                Visibility = DetailsVisibility.Metadata_only,
                LockStatus = category == CanonicalErrorCategory.Lock_conflict ? "active" : null!,
            },
        };

        return new HexalithFoldersApiException<ProblemDetails>(
            "Synthetic problem.",
            problem.Status,
            response: rawResponse ?? JsonConvert.SerializeObject(problem),
            headers: new Dictionary<string, IEnumerable<string>>(),
            result: problem,
            innerException: null!);
    }

    /// <summary>Builds a typed <see cref="AcceptedCommand"/> acknowledgement for substitute-based tests.</summary>
    /// <param name="idempotentReplay">Whether the acknowledgement marks an idempotent replay.</param>
    /// <returns>A populated accepted-command instance.</returns>
    public static AcceptedCommand Accepted(bool idempotentReplay = false) => new()
    {
        AcceptedAt = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero),
        CorrelationId = "corr_01HZY7Z6N7J4Q2X8Y9V0COR001",
        TaskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001",
        Status = AcceptedCommandStatus.Accepted,
        IdempotentReplay = idempotentReplay,
    };

    /// <summary>
    /// Stamps the operation the real SDK would capture before throwing, so projection
    /// fail-closes on an unbound exception without dropping a declared tuple.
    /// </summary>
    /// <param name="category">The canonical error category whose declared operation is bound.</param>
    private static void BindOriginatingOperation(CanonicalErrorCategory category)
    {
        (string method, string url) = category switch
        {
            CanonicalErrorCategory.Provider_unavailable or CanonicalErrorCategory.Unknown_provider_outcome =>
                ("POST", "api/v2/folders/folder_000000001/workspaces/workspace_000000001/preparation"),
            _ => ("POST", "api/v2/folders/folder_000000001/workspaces/workspace_000000001/files/add"),
        };
        HexalithFoldersOperationContext.Set(method, url);
    }

    /// <summary>Builds a bare (untyped) API exception representing an unexpected/unmapped status.</summary>
    /// <returns>The bare exception.</returns>
    public static HexalithFoldersApiException BareException() => new(
        "Unexpected status.",
        statusCode: 502,
        response: "<html>gateway</html>",
        headers: new Dictionary<string, IEnumerable<string>>(),
        innerException: null!);
}
