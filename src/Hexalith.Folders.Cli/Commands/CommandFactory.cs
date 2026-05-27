using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands;

/// <summary>
/// Builders that assemble the per-command option set and wire the shared <see cref="CommandPipeline"/>, so
/// every subcommand reduces to its resource options plus a single typed <see cref="IClient"/> call. Mutating
/// commands automatically get <c>--task-id</c>, <c>--idempotency-key</c>, and <c>--allow-auto-key</c>; query
/// commands get neither idempotency option (passing one is an unrecognized-argument parse error → exit 64).
/// </summary>
internal static class CommandFactory
{
    /// <summary>Builds a mutating subcommand wrapping one <see cref="IClient"/> mutation.</summary>
    /// <param name="name">The subcommand name.</param>
    /// <param name="description">The subcommand description.</param>
    /// <param name="pipeline">The shared pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <param name="resourceOptions">Resource/body options the command exposes (added before the shared sourcing options).</param>
    /// <param name="invoke">The typed mutation call, given the parse result and resolved sourcing.</param>
    /// <returns>The configured command.</returns>
    public static Command Mutation(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        Option[] resourceOptions,
        Func<ParseResult, IClient, MutationSourcing, CancellationToken, Task<object?>> invoke)
    {
        Option<string?> taskId = CommandOptions.TaskId();
        Option<string?> idempotencyKey = CommandOptions.IdempotencyKey();
        Option<bool> allowAutoKey = CommandOptions.AllowAutoKey();

        Command command = new(name, description);
        foreach (Option option in resourceOptions)
        {
            command.Options.Add(option);
        }

        command.Options.Add(taskId);
        command.Options.Add(idempotencyKey);
        command.Options.Add(allowAutoKey);

        command.SetAction((parseResult, cancellationToken) =>
        {
            GlobalOptions go = global.Resolve(parseResult);
            return pipeline.ExecuteMutationAsync(
                go,
                parseResult.GetValue(taskId),
                parseResult.GetValue(idempotencyKey),
                parseResult.GetValue(allowAutoKey),
                (client, sourcing, ct) => invoke(parseResult, client, sourcing, ct),
                cancellationToken);
        });
        return command;
    }

    /// <summary>Builds a query subcommand wrapping one non-mutating <see cref="IClient"/> operation.</summary>
    /// <param name="name">The subcommand name.</param>
    /// <param name="description">The subcommand description.</param>
    /// <param name="pipeline">The shared pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <param name="taskIdRequired">Whether the operation's signature requires a task ID header.</param>
    /// <param name="resourceOptions">Resource/body/pagination options the command exposes.</param>
    /// <param name="invoke">The typed query call, given the parse result and resolved sourcing.</param>
    /// <returns>The configured command.</returns>
    public static Command Query(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        bool taskIdRequired,
        Option[] resourceOptions,
        Func<ParseResult, IClient, QuerySourcing, CancellationToken, Task<object?>> invoke)
    {
        Command command = new(name, description);
        foreach (Option option in resourceOptions)
        {
            command.Options.Add(option);
        }

        Option<string?>? taskId = null;
        if (taskIdRequired)
        {
            taskId = CommandOptions.TaskId();
            command.Options.Add(taskId);
        }

        command.SetAction((parseResult, cancellationToken) =>
        {
            GlobalOptions go = global.Resolve(parseResult);
            return pipeline.ExecuteQueryAsync(
                go,
                taskId is null ? null : parseResult.GetValue(taskId),
                taskIdRequired,
                (client, sourcing, ct) => invoke(parseResult, client, sourcing, ct),
                cancellationToken);
        });
        return command;
    }

    /// <summary>Adapts a typed <see cref="Task{T}"/> SDK call to the pipeline's <c>Task&lt;object?&gt;</c> shape.</summary>
    /// <typeparam name="T">The SDK result type.</typeparam>
    /// <param name="task">The SDK call.</param>
    /// <returns>The awaited result boxed as <see cref="object"/>.</returns>
    public static async Task<object?> AsObject<T>(Task<T> task) => await task.ConfigureAwait(false);
}
