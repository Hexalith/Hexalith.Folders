using System;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Cli.Rendering;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands;

/// <summary>Resolved sourcing values threaded into a mutating <see cref="IClient"/> call.</summary>
/// <param name="IdempotencyKey">The caller-sourced or opt-in auto-generated idempotency key.</param>
/// <param name="CorrelationId">The per-invocation correlation ID.</param>
/// <param name="TaskId">The caller-provided task ID (never CLI-generated).</param>
internal readonly record struct MutationSourcing(string IdempotencyKey, string CorrelationId, string TaskId);

/// <summary>Resolved sourcing values threaded into a query <see cref="IClient"/> call.</summary>
/// <param name="CorrelationId">The per-invocation correlation ID.</param>
/// <param name="TaskId">The task ID, present only when the operation signature requires it.</param>
internal readonly record struct QuerySourcing(string CorrelationId, string? TaskId);

/// <summary>
/// Centralizes the Adapter Parity Contract behavioral flow shared by every command: per-invocation
/// correlation, fail-closed task-ID sourcing, idempotency-key sourcing, credential resolution, client
/// construction, invocation, and the canonical category→exit-code projection. Commands supply only the
/// typed <see cref="IClient"/> call; all sourcing and error mapping live here so adding commands is
/// mechanical and behaviorally uniform.
/// </summary>
internal sealed class CommandPipeline
{
    private readonly CliDependencies _dependencies;

    /// <summary>Initializes a new instance of the <see cref="CommandPipeline"/> class.</summary>
    /// <param name="dependencies">The injected collaborators.</param>
    public CommandPipeline(CliDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;
    }

    /// <summary>Executes a mutating command (requires idempotency-key sourcing and a task ID).</summary>
    /// <param name="global">The resolved global options.</param>
    /// <param name="taskIdOption">The <c>--task-id</c> value.</param>
    /// <param name="idempotencyKeyOption">The <c>--idempotency-key</c> value.</param>
    /// <param name="allowAutoKey">Whether <c>--allow-auto-key</c> was supplied.</param>
    /// <param name="invoke">The typed SDK call, given the resolved sourcing values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The canonical exit code.</returns>
    public async Task<int> ExecuteMutationAsync(
        GlobalOptions global,
        string? taskIdOption,
        string? idempotencyKeyOption,
        bool allowAutoKey,
        Func<IClient, MutationSourcing, CancellationToken, Task<object?>> invoke,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(invoke);

        // One fresh correlation ID per invocation, always observable to the caller (stderr).
        string correlationId = CorrelationAndTaskId.ResolveCorrelationId(global.CorrelationId);
        EmitCorrelation(correlationId);

        // Task ID: fail-closed and pre-SDK. ResolveTaskId throws when blank; the CLI never generates one.
        string taskId;
        try
        {
            taskId = CorrelationAndTaskId.ResolveTaskId(taskIdOption);
        }
        catch (InvalidOperationException)
        {
            return UsageError(global, correlationId, "A task ID is required for this command; supply --task-id <id>.");
        }

        // Idempotency key: explicit value, else opt-in auto-key (echoed to stderr), else pre-SDK error.
        string idempotencyKey;
        if (!string.IsNullOrWhiteSpace(idempotencyKeyOption))
        {
            idempotencyKey = idempotencyKeyOption;
        }
        else if (allowAutoKey)
        {
            idempotencyKey = _dependencies.IdempotencyKeyGenerator();
            _dependencies.Console.Error.WriteLine($"idempotency-key: {idempotencyKey}");
        }
        else
        {
            return UsageError(
                global,
                correlationId,
                "A mutating command requires --idempotency-key <key> or --allow-auto-key.");
        }

        if (!TryResolveBaseAddress(global, out Uri? baseAddress))
        {
            return UsageError(global, correlationId, "A valid absolute --base-address (or HEXALITH_FOLDERS_BASE_ADDRESS) is required.");
        }

        if (!TryResolveToken(global, correlationId, out string? token, out int credentialFailureCode))
        {
            return credentialFailureCode;
        }

        IClient client = _dependencies.ClientFactory(baseAddress!, token!);
        MutationSourcing sourcing = new(idempotencyKey, correlationId, taskId);
        return await InvokeAndRenderAsync(global, correlationId, () => invoke(client, sourcing, cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>Executes a query command (never accepts idempotency keys).</summary>
    /// <param name="global">The resolved global options.</param>
    /// <param name="taskIdOption">The <c>--task-id</c> value, when the operation requires one.</param>
    /// <param name="taskIdRequired">Whether the operation signature requires a task ID.</param>
    /// <param name="invoke">The typed SDK call, given the resolved sourcing values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The canonical exit code.</returns>
    public async Task<int> ExecuteQueryAsync(
        GlobalOptions global,
        string? taskIdOption,
        bool taskIdRequired,
        Func<IClient, QuerySourcing, CancellationToken, Task<object?>> invoke,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(invoke);

        string correlationId = CorrelationAndTaskId.ResolveCorrelationId(global.CorrelationId);
        EmitCorrelation(correlationId);

        string? taskId = null;
        if (taskIdRequired)
        {
            try
            {
                taskId = CorrelationAndTaskId.ResolveTaskId(taskIdOption);
            }
            catch (InvalidOperationException)
            {
                return UsageError(global, correlationId, "A task ID is required for this query; supply --task-id <id>.");
            }
        }

        if (!TryResolveBaseAddress(global, out Uri? baseAddress))
        {
            return UsageError(global, correlationId, "A valid absolute --base-address (or HEXALITH_FOLDERS_BASE_ADDRESS) is required.");
        }

        if (!TryResolveToken(global, correlationId, out string? token, out int credentialFailureCode))
        {
            return credentialFailureCode;
        }

        IClient client = _dependencies.ClientFactory(baseAddress!, token!);
        QuerySourcing sourcing = new(correlationId, taskId);
        return await InvokeAndRenderAsync(global, correlationId, () => invoke(client, sourcing, cancellationToken))
            .ConfigureAwait(false);
    }

    private static bool TryResolveBaseAddress(GlobalOptions global, out Uri? baseAddress)
    {
        baseAddress = null;
        return !string.IsNullOrWhiteSpace(global.BaseAddress)
            && Uri.TryCreate(global.BaseAddress, UriKind.Absolute, out baseAddress);
    }

    private async Task<int> InvokeAndRenderAsync(GlobalOptions global, string correlationId, Func<Task<object?>> invoke)
    {
        try
        {
            object? result = await invoke().ConfigureAwait(false);
            ResultRenderer.RenderSuccess(_dependencies.Console, global.Output, result);
            return FoldersExitCodes.Success;
        }
        catch (CliUsageException usage)
        {
            // Body-shape validation that runs before the HTTP call: pre-SDK usage error.
            return UsageError(global, correlationId, usage.Message);
        }
        catch (FileUploadStreamingRequiredException)
        {
            // Content exceeds the inline transport boundary. Content-safe and retryable via streamed staging;
            // mapped to the input-limit family (69). The message never discloses limits, paths, or content.
            ResultRenderer.RenderClientError(
                _dependencies.Console,
                global.Output,
                "input_limit_exceeded",
                "File content exceeds the inline upload limit; stage it out of band and retry via the streamed transport.",
                correlationId);
            return FoldersExitCodes.ValidationError;
        }
        catch (HexalithFoldersApiException<ProblemDetails> typed) when (typed.Result is not null)
        {
            // Post-SDK: server returned RFC 9457 + canonical category. Project category → exit code.
            ResultRenderer.RenderProblem(_dependencies.Console, global.Output, typed.Result);
            return ErrorProjection.Project(typed.Result.Category);
        }
        catch (HexalithFoldersApiException)
        {
            // Bare exception (unexpected/unmapped status or null typed body) → internal_error (1).
            ResultRenderer.RenderClientError(
                _dependencies.Console,
                global.Output,
                "internal_error",
                "An unexpected server response was received.",
                correlationId);
            return FoldersExitCodes.InternalError;
        }
    }

    private bool TryResolveToken(GlobalOptions global, string correlationId, out string? token, out int credentialFailureCode)
    {
        token = _dependencies.Credentials.ResolveToken(global.Token);
        if (string.IsNullOrWhiteSpace(token))
        {
            // Pre-SDK: no credential anywhere → 65, before any HTTP call. No path/token leaked.
            ResultRenderer.RenderClientError(
                _dependencies.Console,
                global.Output,
                "credential_missing",
                "No bearer token found (checked HEXALITH_TOKEN, ~/.hexalith/credentials.json, then --token).",
                correlationId);
            credentialFailureCode = FoldersExitCodes.CredentialMissing;
            return false;
        }

        credentialFailureCode = FoldersExitCodes.Success;
        return true;
    }

    private int UsageError(GlobalOptions global, string correlationId, string message)
    {
        ResultRenderer.RenderClientError(_dependencies.Console, global.Output, "client_configuration_error", message, correlationId);
        return FoldersExitCodes.UsageError;
    }

    private void EmitCorrelation(string correlationId)
        => _dependencies.Console.Error.WriteLine($"correlation-id: {correlationId}");
}
