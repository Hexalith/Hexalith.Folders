using System;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Credentials;
using Hexalith.Folders.Mcp.Errors;
using Hexalith.Folders.Mcp.Infrastructure;

namespace Hexalith.Folders.Mcp.Tooling;

/// <summary>Resolved sourcing values threaded into a mutating <see cref="IClient"/> call.</summary>
/// <param name="IdempotencyKey">The caller-sourced idempotency key (never MCP-generated).</param>
/// <param name="CorrelationId">The per-call correlation ID.</param>
/// <param name="TaskId">The caller-provided task ID (never MCP-generated).</param>
internal readonly record struct MutationSourcing(string IdempotencyKey, string CorrelationId, string TaskId);

/// <summary>Resolved sourcing values threaded into a query <see cref="IClient"/> call.</summary>
/// <param name="CorrelationId">The per-call correlation ID.</param>
/// <param name="TaskId">The task ID, present only when the operation signature requires it.</param>
internal readonly record struct QuerySourcing(string CorrelationId, string? TaskId);

/// <summary>
/// Centralizes the Adapter Parity Contract behavioral flow shared by every tool and resource:
/// per-call correlation (always echoed in the result), fail-closed task-ID sourcing, idempotency-key
/// sourcing, credential resolution, the typed <see cref="IClient"/> invocation, and the canonical
/// category→failure-kind projection — so each tool body is a few lines and behaviorally uniform.
/// </summary>
/// <remarks>
/// Mirrors the Story 5.2 CLI <c>CommandPipeline</c>. Every pre-SDK guard (blank task ID, blank idempotency
/// key, missing credential, malformed body) short-circuits <b>before</b> the <see cref="IClient"/> is
/// touched, so a hermetic test can assert no HTTP call was made. The MCP server never auto-generates an
/// idempotency key (no auto-key path) and never generates a task ID; only the correlation ID may be
/// synthesized when omitted.
/// </remarks>
internal sealed class ToolPipeline
{
    private readonly IClient _client;
    private readonly McpCredentialResolver _credentials;

    /// <summary>Initializes a new instance of the <see cref="ToolPipeline"/> class.</summary>
    /// <param name="client">The typed Folders SDK client.</param>
    /// <param name="credentials">The credential resolver.</param>
    public ToolPipeline(IClient client, McpCredentialResolver credentials)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(credentials);
        _client = client;
        _credentials = credentials;
    }

    /// <summary>Executes a mutating tool (requires idempotency-key and task-ID sourcing).</summary>
    /// <param name="idempotencyKey">The caller-supplied idempotency key.</param>
    /// <param name="taskId">The caller-supplied task ID.</param>
    /// <param name="correlationId">The optional caller-supplied correlation ID.</param>
    /// <param name="invoke">The typed SDK call, given the resolved sourcing values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The metadata-only JSON tool result (success envelope or failure).</returns>
    public async Task<string> ExecuteMutationAsync(
        string? idempotencyKey,
        string? taskId,
        string? correlationId,
        Func<IClient, MutationSourcing, CancellationToken, Task<object?>> invoke,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        string correlation = CorrelationAndTaskId.ResolveCorrelationId(correlationId);

        // Task ID: fail-closed and pre-SDK. ResolveTaskId throws when blank; MCP never generates one.
        string resolvedTaskId;
        try
        {
            resolvedTaskId = CorrelationAndTaskId.ResolveTaskId(taskId);
        }
        catch (InvalidOperationException)
        {
            return Failure(McpFailure.UsageError(correlation, "A task ID is required for this tool; supply taskId."));
        }

        // Idempotency key: caller-sourced only. No auto-key path exists for MCP.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Failure(McpFailure.UsageError(correlation, "A mutating tool requires a caller-supplied idempotencyKey."));
        }

        if (!TryResolveCredential(correlation, out string failure))
        {
            return failure;
        }

        MutationSourcing sourcing = new(idempotencyKey, correlation, resolvedTaskId);
        return await InvokeAsync(correlation, () => invoke(_client, sourcing, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Executes a query tool (never accepts an idempotency key).</summary>
    /// <param name="taskId">The caller-supplied task ID, when the operation requires one.</param>
    /// <param name="taskIdRequired">Whether the operation signature requires a task ID.</param>
    /// <param name="correlationId">The optional caller-supplied correlation ID.</param>
    /// <param name="invoke">The typed SDK call, given the resolved sourcing values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The metadata-only JSON tool result (success envelope or failure).</returns>
    public async Task<string> ExecuteQueryAsync(
        string? taskId,
        bool taskIdRequired,
        string? correlationId,
        Func<IClient, QuerySourcing, CancellationToken, Task<object?>> invoke,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        string correlation = CorrelationAndTaskId.ResolveCorrelationId(correlationId);

        string? resolvedTaskId = null;
        if (taskIdRequired)
        {
            try
            {
                resolvedTaskId = CorrelationAndTaskId.ResolveTaskId(taskId);
            }
            catch (InvalidOperationException)
            {
                return Failure(McpFailure.UsageError(correlation, "A task ID is required for this query tool; supply taskId."));
            }
        }

        if (!TryResolveCredential(correlation, out string failure))
        {
            return failure;
        }

        QuerySourcing sourcing = new(correlation, resolvedTaskId);
        return await InvokeAsync(correlation, () => invoke(_client, sourcing, cancellationToken)).ConfigureAwait(false);
    }

    private bool TryResolveCredential(string correlation, out string failure)
    {
        if (string.IsNullOrWhiteSpace(_credentials.ResolveToken()))
        {
            // Pre-SDK: no credential resolved anywhere → credential_missing, before any HTTP call.
            failure = Failure(McpFailure.Credentials(correlation));
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static async Task<string> InvokeAsync(string correlation, Func<Task<object?>> invoke)
    {
        try
        {
            object? result = await invoke().ConfigureAwait(false);

            // Success envelope: the correlation ID used is always echoed alongside the metadata-only result.
            return MetadataOnlyJson.Serialize(new { correlationId = correlation, result });
        }
        catch (McpUsageException usage)
        {
            // Body-shape validation that ran before the HTTP call: pre-SDK usage error.
            return Failure(McpFailure.UsageError(correlation, usage.Message));
        }
        catch (FileUploadStreamingRequiredException)
        {
            // Inline content exceeded the upload boundary; content-safe, mapped to input_limit_exceeded.
            return Failure(McpFailure.InputLimitExceeded(correlation));
        }
        catch (HexalithFoldersApiException<ProblemDetails> typed) when (typed.Result is not null)
        {
            // Post-SDK: server returned RFC 9457 + canonical category. Project category → kind.
            return Failure(McpFailure.FromProblem(typed.Result, correlation));
        }
        catch (HexalithFoldersApiException)
        {
            // Bare exception (unexpected/unmapped status or null typed body) → internal_error.
            return Failure(McpFailure.Internal(correlation));
        }
    }

    private static string Failure(McpFailure failure) => MetadataOnlyJson.Serialize(failure);

    /// <summary>Adapts a typed <see cref="Task{T}"/> SDK call to the pipeline's <c>Task&lt;object?&gt;</c> shape.</summary>
    /// <typeparam name="T">The SDK result type.</typeparam>
    /// <param name="task">The SDK call.</param>
    /// <returns>The awaited result boxed as <see cref="object"/>.</returns>
    public static async Task<object?> AsObject<T>(Task<T> task) => await task.ConfigureAwait(false);
}
