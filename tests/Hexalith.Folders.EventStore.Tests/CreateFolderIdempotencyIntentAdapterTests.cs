using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class CreateFolderIdempotencyIntentAdapterTests
{
    [Fact]
    public void EquivalentPayloadsShouldProduceTheSameCanonicalIntent()
    {
        CreateFolderIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command("""{"requestSchemaVersion":"v1","parentFolderId":"parent-a","folderMetadata":{"displayName":"Folder A"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command("""{"folderMetadata":{"displayName":"Folder A"},"parentFolderId":"parent-a","requestSchemaVersion":"v1"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(second.SemanticPayload));
        first.CanonicalTarget.ShouldBe("tenant-a/folders/folder-a");
        first.PolicyVersion.ShouldBe(FoldersCanonicalIntentBuilder.PolicyVersion);
    }

    [Fact]
    public void DifferentDisplayNameShouldChangeCanonicalIntent()
    {
        CreateFolderIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command("""{"requestSchemaVersion":"v1","parentFolderId":"parent-a","folderMetadata":{"displayName":"Folder A"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command("""{"requestSchemaVersion":"v1","parentFolderId":"parent-a","folderMetadata":{"displayName":"Folder B"}}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldNotBe(Encoding.UTF8.GetString(second.SemanticPayload));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new(
            "Hexalith.Folders.Commands.CreateFolder",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(payload),
            Extensions: null);
}
