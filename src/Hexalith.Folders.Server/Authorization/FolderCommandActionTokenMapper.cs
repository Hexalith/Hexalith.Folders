using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.Folders.Server.Authorization;

public sealed class FolderCommandActionTokenMapper(IReadOnlyDictionary<string, FolderCommandActionMapping>? mappings = null)
    : IFolderCommandActionTokenMapper
{
    private static readonly IReadOnlyDictionary<string, FolderCommandActionMapping> DefaultMappings =
        new Dictionary<string, FolderCommandActionMapping>(StringComparer.Ordinal)
        {
            ["Hexalith.Folders.Commands.CreateFolder"] = new("create_folder", FolderCommandOperationScopeKind.OrganizationBaseline),
            [FoldersServerModule.ArchiveFolderCommandType] = new("archive_folder", FolderCommandOperationScopeKind.FolderAggregate),
            [FoldersServerModule.CreateRepositoryBackedFolderCommandType] = new("create_repository_backed_folder", FolderCommandOperationScopeKind.FolderAggregate),
            [FoldersServerModule.BindRepositoryCommandType] = new("bind_repository", FolderCommandOperationScopeKind.FolderAggregate),
            [FoldersServerModule.ConfigureBranchRefPolicyCommandType] = new("configure_branch_ref_policy", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.GrantFolderAccess"] = new("manage_folder_access", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.RevokeFolderAccess"] = new("manage_folder_access", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.ConfigureProviderBinding"] = new("configure_provider_binding", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.PrepareWorkspace"] = new("prepare_workspace", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.LockWorkspace"] = new("lock_workspace", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.MutateFiles"] = new("mutate_files", FolderCommandOperationScopeKind.FolderAggregate),
            ["Hexalith.Folders.Commands.CommitWorkspace"] = new("commit", FolderCommandOperationScopeKind.FolderAggregate),
        };

    private readonly IReadOnlyDictionary<string, FolderCommandActionMapping> _mappings = mappings ?? DefaultMappings;

    public FolderCommandActionMapping? Map(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mappings.TryGetValue(command.CommandType, out FolderCommandActionMapping? mapping)
            ? mapping
            : null;
    }
}
