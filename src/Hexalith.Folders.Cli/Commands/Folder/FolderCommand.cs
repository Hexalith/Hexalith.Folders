using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Folder;

/// <summary>
/// The <c>folder</c> command group: folder creation (including repository-backed), repository binding,
/// lifecycle status, archive, ACL entries, effective permissions, and branch-ref policy. Each subcommand
/// wraps the matching <see cref="IClient"/> operation per the Command-to-SDK map.
/// </summary>
internal static class FolderCommand
{
    /// <summary>Creates the <c>folder</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("folder", "Create, bind, inspect, and govern folders.");

        Option<string?> createBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Mutation(
            "create",
            "Create a folder (mutating).",
            pipeline,
            global,
            [createBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.CreateFolderAsync(
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<CreateFolderRequest>(parseResult.GetValue(createBody)),
                ct))));

        Option<string?> repoBackedBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Mutation(
            "create-repo-backed",
            "Create a repository-backed folder (mutating).",
            pipeline,
            global,
            [repoBackedBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.CreateRepositoryBackedFolderAsync(
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<CreateRepositoryBackedFolderRequest>(parseResult.GetValue(repoBackedBody)),
                ct))));

        Option<string> bindFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> bindBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Mutation(
            "bind-repo",
            "Bind a repository to a folder (mutating).",
            pipeline,
            global,
            [bindFolderId, bindBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.BindRepositoryAsync(
                parseResult.GetValue(bindFolderId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<BindRepositoryRequest>(parseResult.GetValue(bindBody)),
                ct))));

        Option<string> getBindingFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> getBindingId = CommandOptions.RequiredId("--repository-binding-id", "Opaque repository binding identifier.");
        Option<string?> getBindingFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "get-repo-binding",
            "Get a folder's repository binding (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [getBindingFolderId, getBindingId, getBindingFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetRepositoryBindingAsync(
                parseResult.GetValue(getBindingFolderId)!,
                parseResult.GetValue(getBindingId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(getBindingFreshness)),
                ct))));

        Option<string> statusFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> statusFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "status",
            "Get folder lifecycle status (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [statusFolderId, statusFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetFolderLifecycleStatusAsync(
                parseResult.GetValue(statusFolderId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(statusFreshness)),
                ct))));

        Option<string> archiveFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> archiveBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Mutation(
            "archive",
            "Archive a folder (mutating).",
            pipeline,
            global,
            [archiveFolderId, archiveBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ArchiveFolderAsync(
                parseResult.GetValue(archiveFolderId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<ArchiveFolderRequest>(parseResult.GetValue(archiveBody)),
                ct))));

        Option<string> permsFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> permsFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "effective-permissions",
            "Get effective folder permissions (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [permsFolderId, permsFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetEffectivePermissionsAsync(
                parseResult.GetValue(permsFolderId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(permsFreshness)),
                ct))));

        command.Subcommands.Add(CreateAclCommand(pipeline, global));
        command.Subcommands.Add(CreateBranchPolicyCommand(pipeline, global));
        return command;
    }

    private static Command CreateAclCommand(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command acl = new("acl", "List and update folder ACL entries.");

        Option<string> listFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> listFreshness = CommandOptions.Freshness();
        Option<string?> listCursor = CommandOptions.Cursor();
        Option<int?> listLimit = CommandOptions.Limit();
        Option<string?> listFilter = CommandOptions.Filter();
        acl.Subcommands.Add(CommandFactory.Query(
            "list",
            "List folder ACL entries (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [listFolderId, listFreshness, listCursor, listLimit, listFilter],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ListFolderAclEntriesAsync(
                parseResult.GetValue(listFolderId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(listFreshness)),
                parseResult.GetValue(listCursor)!,
                parseResult.GetValue(listLimit),
                parseResult.GetValue(listFilter)!,
                ct))));

        Option<string> updateFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> updateEntryId = CommandOptions.RequiredId("--acl-entry-id", "Opaque ACL entry identifier.");
        Option<string?> updateBody = CommandOptions.Request();
        acl.Subcommands.Add(CommandFactory.Mutation(
            "update",
            "Update a folder ACL entry (mutating).",
            pipeline,
            global,
            [updateFolderId, updateEntryId, updateBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.UpdateFolderAclEntryAsync(
                parseResult.GetValue(updateFolderId)!,
                parseResult.GetValue(updateEntryId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<UpdateFolderAclEntryRequest>(parseResult.GetValue(updateBody)),
                ct))));

        return acl;
    }

    private static Command CreateBranchPolicyCommand(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command branchPolicy = new("branch-policy", "Configure and read the branch-ref policy.");

        Option<string> setFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> setBody = CommandOptions.Request();
        branchPolicy.Subcommands.Add(CommandFactory.Mutation(
            "set",
            "Configure the branch-ref policy (mutating).",
            pipeline,
            global,
            [setFolderId, setBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ConfigureBranchRefPolicyAsync(
                parseResult.GetValue(setFolderId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<BranchRefPolicyRequest>(parseResult.GetValue(setBody)),
                ct))));

        Option<string> getFolderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string?> getFreshness = CommandOptions.Freshness();
        branchPolicy.Subcommands.Add(CommandFactory.Query(
            "get",
            "Get the branch-ref policy (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [getFolderId, getFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetBranchRefPolicyAsync(
                parseResult.GetValue(getFolderId)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(getFreshness)),
                ct))));

        return branchPolicy;
    }
}
