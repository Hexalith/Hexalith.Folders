using System.Text;
using System.Text.Json;

using Hexalith.EventStore.DomainService;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class FolderAccessIdempotencyIntentTests
{
    [Fact]
    public void BatchOutsideTheOneEntryPublicContractFailsClosed()
    {
        GrantFolderAccessIdempotencyIntentAdapter adapter = new();
        IdempotencyIntentCommand command = new(
            "Hexalith.Folders.Commands.GrantFolderAccess",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(
                """{"operations":[{"principalKind":"user","principalId":"one","action":"read_metadata"},{"principalKind":"user","principalId":"two","action":"read_metadata"}]}"""),
            Extensions: null);

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(command));
    }
}
