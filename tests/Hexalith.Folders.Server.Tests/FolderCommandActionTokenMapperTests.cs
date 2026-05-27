using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Folders.Server.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderCommandActionTokenMapperTests
{
    [Fact]
    public void ArchiveFolderCommandShouldMapToArchiveAdminAction()
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command("Hexalith.Folders.Commands.ArchiveFolder"));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("archive_folder");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    [Fact]
    public void CreateRepositoryBackedFolderCommandShouldMapToRepositoryCreationAction()
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command(FoldersServerModule.CreateRepositoryBackedFolderCommandType));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("create_repository_backed_folder");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    [Fact]
    public void BindRepositoryCommandShouldMapToRepositoryBindingAction()
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command(FoldersServerModule.BindRepositoryCommandType));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("bind_repository");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    [Fact]
    public void LockWorkspaceCommandShouldMapToWorkspaceLockAction()
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command(FoldersServerModule.LockWorkspaceCommandType));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("lock_workspace");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    [Fact]
    public void ReleaseWorkspaceLockCommandShouldMapToWorkspaceLockAction()
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command(FoldersServerModule.ReleaseWorkspaceLockCommandType));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("lock_workspace");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    [Theory]
    [InlineData("Hexalith.Folders.Commands.GrantFolderAccess")]
    [InlineData("Hexalith.Folders.Commands.RevokeFolderAccess")]
    public void FolderAclMutationCommandsShouldMapToAdministerAccessAction(string commandType)
    {
        FolderCommandActionTokenMapper mapper = new();

        FolderCommandActionMapping? mapping = mapper.Map(Command(commandType));

        mapping.ShouldNotBeNull();
        mapping.ActionToken.ShouldBe("manage_folder_access");
        mapping.ScopeKind.ShouldBe(FolderCommandOperationScopeKind.FolderAggregate);
    }

    private static CommandEnvelope Command(string commandType)
        => new(
            MessageId: "01J00000000000000000000001",
            TenantId: "tenant-a",
            Domain: FoldersServerModule.DomainName,
            AggregateId: "folder-a",
            CommandType: commandType,
            Payload: [0x01],
            CorrelationId: "corr-a",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);
}
