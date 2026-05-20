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

    private static CommandEnvelope Command(string commandType)
        => new(
            MessageId: "01J00000000000000000000001",
            TenantId: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: commandType,
            Payload: [0x01],
            CorrelationId: "corr-a",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);
}
