using System;
using System.Collections.Generic;

using Hexalith.Folders.Client.Generated;

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
        string correlationId = "corr_TEST",
        string rawResponse = "{}")
    {
        ProblemDetails problem = new()
        {
            Type = "about:blank",
            Title = category.ToString(),
            Status = 409,
            Category = category,
            Code = "test_code",
            Message = "Synthetic metadata-only problem.",
            CorrelationId = correlationId,
            Retryable = false,
            ClientAction = ProblemDetailsClientAction.No_action,
        };

        return new HexalithFoldersApiException<ProblemDetails>(
            "Synthetic problem.",
            problem.Status,
            response: rawResponse,
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

    /// <summary>Builds a bare (untyped) API exception representing an unexpected/unmapped status.</summary>
    /// <returns>The bare exception.</returns>
    public static HexalithFoldersApiException BareException() => new(
        "Unexpected status.",
        statusCode: 502,
        response: "<html>gateway</html>",
        headers: new Dictionary<string, IEnumerable<string>>(),
        innerException: null!);
}
