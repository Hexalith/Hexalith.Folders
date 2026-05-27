using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Commit;

/// <summary>
/// The <c>commit</c> command group: commit a workspace and inspect commit evidence, provider outcome,
/// reconciliation status, and task status. Unknown/reconciliation outcomes are surfaced truthfully by the
/// pipeline's category→exit projection (71/72), never hidden or retry-looped.
/// </summary>
internal static class CommitCommand
{
    /// <summary>Creates the <c>commit</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("commit", "Commit a workspace and inspect commit/provider outcomes.");

        Option<string> commitFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> commitWorkspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> commitBody = CommandOptions.Request();
        // Subcommand is named "create", NOT "commit": a "commit" child under the "commit" group collides in
        // the System.CommandLine 2.0.8 token table (group name == child name) and crashes the parser for
        // every `commit <subcommand>` invocation. "commit create" maps to CommitWorkspaceAsync.
        command.Subcommands.Add(CommandFactory.Mutation(
            "create",
            "Commit a workspace (mutating).",
            pipeline,
            global,
            [commitFolderId, commitWorkspaceId, commitBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.CommitWorkspaceAsync(
                parseResult.GetValue(commitFolderId)!,
                parseResult.GetValue(commitWorkspaceId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<CommitWorkspaceRequest>(parseResult.GetValue(commitBody)),
                ct))));

        command.Subcommands.Add(OperationQuery(
            "evidence",
            "Get commit evidence (query).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, operationId, freshness, ct) => CommandFactory.AsObject(
                client.GetCommitEvidenceAsync(folderId, workspaceId, operationId, sourcing.CorrelationId, freshness, ct))));

        command.Subcommands.Add(OperationQuery(
            "provider-outcome",
            "Get provider outcome (query).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, operationId, freshness, ct) => CommandFactory.AsObject(
                client.GetProviderOutcomeAsync(folderId, workspaceId, operationId, sourcing.CorrelationId, freshness, ct))));

        Option<string> reconFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> reconWorkspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string> reconId = CommandOptions.RequiredId("--reconciliation-id", "Opaque reconciliation identifier.");
        Option<string?> reconFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "reconciliation-status",
            "Get reconciliation status (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [reconFolderId, reconWorkspaceId, reconId, reconFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetReconciliationStatusAsync(
                parseResult.GetValue(reconFolderId)!,
                parseResult.GetValue(reconWorkspaceId)!,
                parseResult.GetValue(reconId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(reconFreshness)),
                ct))));

        // task-status takes the task identifier as a PATH/resource parameter (not the header task-id);
        // it carries no idempotency key and no header task-id.
        Option<string> taskResourceId = CommandOptions.RequiredId("--task-id", "Opaque task identifier to query.");
        Option<string?> taskFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "task-status",
            "Get task status (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [taskResourceId, taskFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetTaskStatusAsync(
                parseResult.GetValue(taskResourceId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(taskFreshness)),
                ct))));

        return command;
    }

    private static Command OperationQuery(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        System.Func<System.CommandLine.ParseResult, IClient, QuerySourcing, string, string, string, ReadConsistencyClass?, System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>> invoke)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string> operationId = CommandOptions.RequiredId("--operation-id", "Opaque per-file operation identifier.");
        Option<string?> freshness = CommandOptions.Freshness();
        return CommandFactory.Query(
            name,
            description,
            pipeline,
            global,
            taskIdRequired: false,
            [folderId, workspaceId, operationId, freshness],
            (parseResult, client, sourcing, ct) => invoke(
                parseResult,
                client,
                sourcing,
                parseResult.GetValue(folderId)!,
                parseResult.GetValue(workspaceId)!,
                parseResult.GetValue(operationId)!,
                CommandOptions.ParseFreshness(parseResult.GetValue(freshness)),
                ct));
    }
}
