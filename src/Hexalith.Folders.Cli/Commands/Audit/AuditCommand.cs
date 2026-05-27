using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Audit;

/// <summary>
/// The <c>audit</c> command group: audit-trail listing/retrieval and operation-timeline listing/retrieval.
/// All operations are queries (no idempotency key, no task-id header).
/// </summary>
internal static class AuditCommand
{
    /// <summary>Creates the <c>audit</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("audit", "List and inspect audit-trail and operation-timeline records.");

        command.Subcommands.Add(PagedFolderQuery(
            "list",
            "List the audit trail (query).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, freshness, cursor, limit, filter, ct) => CommandFactory.AsObject(
                client.ListAuditTrailAsync(folderId, sourcing.CorrelationId, freshness, cursor, limit, filter, ct))));

        Option<string> getFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> auditRecordId = CommandOptions.RequiredId("--audit-record-id", "Opaque audit record identifier.");
        Option<string?> getFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "get",
            "Get an audit record (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [getFolderId, auditRecordId, getFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetAuditRecordAsync(
                parseResult.GetValue(getFolderId)!,
                parseResult.GetValue(auditRecordId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(getFreshness)),
                ct))));

        command.Subcommands.Add(CreateTimelineCommand(pipeline, global));
        return command;
    }

    private static Command CreateTimelineCommand(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command timeline = new("timeline", "List and inspect operation-timeline entries.");

        timeline.Subcommands.Add(PagedFolderQuery(
            "list",
            "List the operation timeline (query).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, freshness, cursor, limit, filter, ct) => CommandFactory.AsObject(
                client.ListOperationTimelineAsync(folderId, sourcing.CorrelationId, freshness, cursor, limit, filter, ct))));

        Option<string> getFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> entryId = CommandOptions.RequiredId("--timeline-entry-id", "Opaque timeline entry identifier.");
        Option<string?> getFreshness = CommandOptions.Freshness();
        timeline.Subcommands.Add(CommandFactory.Query(
            "get",
            "Get an operation-timeline entry (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [getFolderId, entryId, getFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetOperationTimelineEntryAsync(
                parseResult.GetValue(getFolderId)!,
                parseResult.GetValue(entryId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(getFreshness)),
                ct))));

        return timeline;
    }

    private static Command PagedFolderQuery(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        System.Func<System.CommandLine.ParseResult, IClient, QuerySourcing, string, ReadConsistencyClass?, string, int?, string, System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>> invoke)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> freshness = CommandOptions.Freshness();
        Option<string?> cursor = CommandOptions.Cursor();
        Option<int?> limit = CommandOptions.Limit();
        Option<string?> filter = CommandOptions.Filter();
        return CommandFactory.Query(
            name,
            description,
            pipeline,
            global,
            taskIdRequired: false,
            [folderId, freshness, cursor, limit, filter],
            (parseResult, client, sourcing, ct) => invoke(
                parseResult,
                client,
                sourcing,
                parseResult.GetValue(folderId)!,
                CommandOptions.ParseFreshness(parseResult.GetValue(freshness)),
                parseResult.GetValue(cursor)!,
                parseResult.GetValue(limit),
                parseResult.GetValue(filter)!,
                ct));
    }
}
