using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Workspace;

/// <summary>
/// The <c>workspace</c> command group: prepare, lock, get-lock, release, and the retry/transition/status
/// queries. Each subcommand wraps the matching <see cref="IClient"/> operation per the Command-to-SDK map.
/// Operations whose signature carries <c>x_Hexalith_Task_Id</c> require <c>--task-id</c> even when reading.
/// </summary>
internal static class WorkspaceCommand
{
    /// <summary>Creates the <c>workspace</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("workspace", "Prepare, lock, release, and inspect workspaces.");

        command.Subcommands.Add(PrepareLikeMutation(
            "prepare",
            "Prepare a workspace (mutating).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, body, ct) => CommandFactory.AsObject(
                client.PrepareWorkspaceAsync(
                    folderId,
                    workspaceId,
                    sourcing.IdempotencyKey,
                    sourcing.CorrelationId,
                    sourcing.TaskId,
                    CommandOptions.ReadBody<PrepareWorkspaceRequest>(body),
                    ct))));

        command.Subcommands.Add(PrepareLikeMutation(
            "lock",
            "Lock a workspace (mutating).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, body, ct) => CommandFactory.AsObject(
                client.LockWorkspaceAsync(
                    folderId,
                    workspaceId,
                    sourcing.IdempotencyKey,
                    sourcing.CorrelationId,
                    sourcing.TaskId,
                    CommandOptions.ReadBody<LockWorkspaceRequest>(body),
                    ct))));

        command.Subcommands.Add(PrepareLikeMutation(
            "release",
            "Release a workspace lock (mutating).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, body, ct) => CommandFactory.AsObject(
                client.ReleaseWorkspaceLockAsync(
                    folderId,
                    workspaceId,
                    sourcing.IdempotencyKey,
                    sourcing.CorrelationId,
                    sourcing.TaskId,
                    CommandOptions.ReadBody<ReleaseWorkspaceLockRequest>(body),
                    ct))));

        command.Subcommands.Add(WorkspaceQuery(
            "get-lock",
            "Get the workspace lock status (query).",
            pipeline,
            global,
            taskIdRequired: false,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, ct) => CommandFactory.AsObject(
                client.GetWorkspaceLockAsync(folderId, workspaceId, sourcing.CorrelationId, freshness, ct))));

        command.Subcommands.Add(WorkspaceQuery(
            "status",
            "Get workspace status (query).",
            pipeline,
            global,
            taskIdRequired: false,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, ct) => CommandFactory.AsObject(
                client.GetWorkspaceStatusAsync(folderId, workspaceId, sourcing.CorrelationId, freshness, ct))));

        command.Subcommands.Add(WorkspaceQuery(
            "retry-eligibility",
            "Get workspace retry eligibility (query; requires --task-id).",
            pipeline,
            global,
            taskIdRequired: true,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, ct) => CommandFactory.AsObject(
                client.GetWorkspaceRetryEligibilityAsync(folderId, workspaceId, sourcing.CorrelationId, sourcing.TaskId!, freshness, ct))));

        command.Subcommands.Add(WorkspaceQuery(
            "transition-evidence",
            "Get workspace transition evidence (query; requires --task-id).",
            pipeline,
            global,
            taskIdRequired: true,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, ct) => CommandFactory.AsObject(
                client.GetWorkspaceTransitionEvidenceAsync(folderId, workspaceId, sourcing.CorrelationId, sourcing.TaskId!, freshness, ct))));

        command.Subcommands.Add(WorkspaceQuery(
            "cleanup-status",
            "Get workspace cleanup status (query; requires --task-id).",
            pipeline,
            global,
            taskIdRequired: true,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, ct) => CommandFactory.AsObject(
                client.GetWorkspaceCleanupStatusAsync(folderId, workspaceId, sourcing.CorrelationId, sourcing.TaskId!, freshness, ct))));

        return command;
    }

    private static Command PrepareLikeMutation(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        System.Func<System.CommandLine.ParseResult, IClient, MutationSourcing, string, string, string?, System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>> invoke)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> body = CommandOptions.Request();
        return CommandFactory.Mutation(
            name,
            description,
            pipeline,
            global,
            [folderId, workspaceId, body],
            (parseResult, client, sourcing, ct) => invoke(
                parseResult,
                client,
                sourcing,
                parseResult.GetValue(folderId)!,
                parseResult.GetValue(workspaceId)!,
                parseResult.GetValue(body),
                ct));
    }

    private static Command WorkspaceQuery(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        bool taskIdRequired,
        System.Func<System.CommandLine.ParseResult, IClient, QuerySourcing, string, string, ReadConsistencyClass?, System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>> invoke)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> freshness = CommandOptions.Freshness();
        return CommandFactory.Query(
            name,
            description,
            pipeline,
            global,
            taskIdRequired,
            [folderId, workspaceId, freshness],
            (parseResult, client, sourcing, ct) => invoke(
                parseResult,
                client,
                sourcing,
                parseResult.GetValue(folderId)!,
                parseResult.GetValue(workspaceId)!,
                CommandOptions.ParseFreshness(parseResult.GetValue(freshness)),
                ct));
    }
}
