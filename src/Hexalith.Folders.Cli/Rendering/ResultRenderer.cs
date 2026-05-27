using System.Globalization;

using Hexalith.Folders.Cli.Infrastructure;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Rendering;

/// <summary>
/// Renders command results and projected problem envelopes to the console. All rendering is metadata-only
/// in both <see cref="OutputMode.Human"/> and <see cref="OutputMode.Json"/> modes: it serializes only typed
/// SDK shapes through <see cref="MetadataOnlyJson"/> (which drops content-bearing fields) and never touches
/// raw HTTP response text.
/// </summary>
internal static class ResultRenderer
{
    /// <summary>Renders a successful command result to stdout.</summary>
    /// <param name="console">The console.</param>
    /// <param name="output">The selected output mode.</param>
    /// <param name="result">The typed SDK result, or <see langword="null"/> for a result-less success.</param>
    public static void RenderSuccess(ICliConsole console, OutputMode output, object? result)
    {
        if (result is null)
        {
            return;
        }

        if (output == OutputMode.Json)
        {
            console.Out.WriteLine(MetadataOnlyJson.Serialize(result));
            return;
        }

        switch (result)
        {
            case AcceptedCommand accepted:
                // Surface acceptance fields truthfully, including idempotent-replay (never hidden).
                console.Out.WriteLine($"status: {accepted.Status}");
                console.Out.WriteLine($"correlationId: {accepted.CorrelationId}");
                console.Out.WriteLine($"taskId: {accepted.TaskId}");
                console.Out.WriteLine($"idempotentReplay: {accepted.IdempotentReplay.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
                console.Out.WriteLine($"acceptedAt: {accepted.AcceptedAt.ToString("O", CultureInfo.InvariantCulture)}");
                break;

            default:
                // Query results are metadata-only by contract; render them as readable JSON.
                console.Out.WriteLine(MetadataOnlyJson.Serialize(result));
                break;
        }
    }

    /// <summary>Renders a server problem envelope to stderr, metadata-only, projected from the typed shape.</summary>
    /// <param name="console">The console.</param>
    /// <param name="output">The selected output mode.</param>
    /// <param name="problem">The typed problem details.</param>
    public static void RenderProblem(ICliConsole console, OutputMode output, ProblemDetails problem)
    {
        // Project from typed fields only — never echo problem.Response raw body, which could carry content.
        ProjectedProblem projected = new(
            problem.Category.ToString(),
            problem.Code,
            problem.Message,
            problem.CorrelationId,
            problem.Retryable,
            problem.ClientAction.ToString(),
            problem.Details);

        if (output == OutputMode.Json)
        {
            console.Error.WriteLine(MetadataOnlyJson.Serialize(projected));
            return;
        }

        console.Error.WriteLine($"error: {projected.Category}");
        console.Error.WriteLine($"code: {projected.Code}");
        console.Error.WriteLine($"message: {projected.Message}");
        console.Error.WriteLine($"correlationId: {projected.CorrelationId}");
        console.Error.WriteLine($"retryable: {projected.Retryable.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}");
        console.Error.WriteLine($"clientAction: {projected.ClientAction}");
    }

    /// <summary>
    /// Renders a client-side or transport-level failure (pre-SDK usage/credential errors, the
    /// content-safe streaming-required outcome, and unmapped <c>internal_error</c> responses) to stderr,
    /// always emitting the correlation ID. The message must be metadata-only.
    /// </summary>
    /// <param name="console">The console.</param>
    /// <param name="output">The selected output mode.</param>
    /// <param name="category">The canonical category label for the failure.</param>
    /// <param name="message">A metadata-only message describing the failure class.</param>
    /// <param name="correlationId">The correlation ID used for the invocation.</param>
    public static void RenderClientError(ICliConsole console, OutputMode output, string category, string message, string correlationId)
    {
        if (output == OutputMode.Json)
        {
            console.Error.WriteLine(MetadataOnlyJson.Serialize(new
            {
                category,
                message,
                correlationId,
            }));
            return;
        }

        console.Error.WriteLine($"error: {category}");
        console.Error.WriteLine($"message: {message}");
        console.Error.WriteLine($"correlationId: {correlationId}");
    }

    private sealed record ProjectedProblem(
        string Category,
        string Code,
        string Message,
        string CorrelationId,
        bool Retryable,
        string ClientAction,
        System.Collections.Generic.Dictionary<string, string> Details);
}
