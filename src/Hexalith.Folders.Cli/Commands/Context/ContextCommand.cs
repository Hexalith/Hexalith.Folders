using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Context;

/// <summary>
/// The <c>context</c> command group: file-tree, metadata, search, glob, and range-read queries over a
/// prepared workspace. Every operation's signature carries <c>x_Hexalith_Task_Id</c>, so each requires
/// <c>--task-id</c>; none accepts an idempotency key. Range-read results carry authorized content, which the
/// metadata-only renderer drops from all output.
/// </summary>
internal static class ContextCommand
{
    /// <summary>Creates the <c>context</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("context", "Query file context (tree, metadata, search, glob, range).");

        Option<string> listFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> listWorkspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> listFreshness = CommandOptions.Freshness();
        Option<string?> listCursor = CommandOptions.Cursor();
        Option<int?> listLimit = CommandOptions.Limit();
        command.Subcommands.Add(CommandFactory.Query(
            "list",
            "List folder files (query; requires --task-id).",
            pipeline,
            global,
            taskIdRequired: true,
            [listFolderId, listWorkspaceId, listFreshness, listCursor, listLimit],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ListFolderFilesAsync(
                parseResult.GetValue(listFolderId)!,
                parseResult.GetValue(listWorkspaceId)!,
                sourcing.CorrelationId,
                sourcing.TaskId!,
                CommandOptions.ParseFreshness(parseResult.GetValue(listFreshness)),
                parseResult.GetValue(listCursor)!,
                parseResult.GetValue(listLimit),
                ct))));

        command.Subcommands.Add(BodyQuery(
            "metadata",
            "Get folder file metadata (query; requires --task-id).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, body, ct) => CommandFactory.AsObject(
                client.GetFolderFileMetadataAsync(
                    folderId,
                    workspaceId,
                    sourcing.CorrelationId,
                    sourcing.TaskId!,
                    freshness,
                    CommandOptions.ReadBody<FileMetadataRequest>(body),
                    ct))));

        command.Subcommands.Add(BodyQuery(
            "search",
            "Search folder files (query; requires --task-id).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, body, ct) => CommandFactory.AsObject(
                client.SearchFolderFilesAsync(
                    folderId,
                    workspaceId,
                    sourcing.CorrelationId,
                    sourcing.TaskId!,
                    freshness,
                    CommandOptions.ReadBody<FileSearchRequest>(body),
                    ct))));

        command.Subcommands.Add(BodyQuery(
            "glob",
            "Glob folder files (query; requires --task-id).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, body, ct) => CommandFactory.AsObject(
                client.GlobFolderFilesAsync(
                    folderId,
                    workspaceId,
                    sourcing.CorrelationId,
                    sourcing.TaskId!,
                    freshness,
                    CommandOptions.ReadBody<FileGlobRequest>(body),
                    ct))));

        command.Subcommands.Add(BodyQuery(
            "read-range",
            "Read a file byte range (query; requires --task-id). Content is never printed (metadata-only).",
            pipeline,
            global,
            static (parseResult, client, sourcing, folderId, workspaceId, freshness, body, ct) => CommandFactory.AsObject(
                client.ReadFileRangeAsync(
                    folderId,
                    workspaceId,
                    sourcing.CorrelationId,
                    sourcing.TaskId!,
                    freshness,
                    CommandOptions.ReadBody<FileRangeReadRequest>(body),
                    ct))));

        return command;
    }

    private static Command BodyQuery(
        string name,
        string description,
        CommandPipeline pipeline,
        GlobalOptionsBinding global,
        System.Func<System.CommandLine.ParseResult, IClient, QuerySourcing, string, string, ReadConsistencyClass?, string?, System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>> invoke)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> freshness = CommandOptions.Freshness();
        Option<string?> body = CommandOptions.Request();
        return CommandFactory.Query(
            name,
            description,
            pipeline,
            global,
            taskIdRequired: true,
            [folderId, workspaceId, freshness, body],
            (parseResult, client, sourcing, ct) => invoke(
                parseResult,
                client,
                sourcing,
                parseResult.GetValue(folderId)!,
                parseResult.GetValue(workspaceId)!,
                CommandOptions.ParseFreshness(parseResult.GetValue(freshness)),
                parseResult.GetValue(body),
                ct));
    }
}
