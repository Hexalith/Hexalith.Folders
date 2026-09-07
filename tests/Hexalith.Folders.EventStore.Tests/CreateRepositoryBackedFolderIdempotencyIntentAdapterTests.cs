using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class CreateRepositoryBackedFolderIdempotencyIntentAdapterTests
{
    [Fact]
    public void EquivalentPayloadsShouldProduceTheSameCanonicalIntent()
    {
        CreateRepositoryBackedFolderIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"providerBindingRef":"provider-a","repositoryProfileRef":"profile-a","folderMetadata":{"displayName":"Folder A"},"branchRefPolicy":{"repositoryBindingId":"binding-a","policyRef":"policy-a"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"branchRefPolicy":{"policyRef":"policy-a","repositoryBindingId":"binding-a"},"folderMetadata":{"displayName":"Folder A"},"repositoryProfileRef":"profile-a","providerBindingRef":"provider-a"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(second.SemanticPayload));
        first.CanonicalTarget.ShouldBe("tenant-a/folders/folder-a");
        first.PolicyVersion.ShouldBe(FoldersCanonicalIntentBuilder.PolicyVersion);
    }

    [Fact]
    public void DifferentRepositoryBindingIdShouldChangeCanonicalIntent()
    {
        CreateRepositoryBackedFolderIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"providerBindingRef":"provider-a","repositoryProfileRef":"profile-a","folderMetadata":{"displayName":"Folder A"},"branchRefPolicy":{"repositoryBindingId":"binding-a","policyRef":"policy-a"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"providerBindingRef":"provider-a","repositoryProfileRef":"profile-a","folderMetadata":{"displayName":"Folder A"},"branchRefPolicy":{"repositoryBindingId":"binding-b","policyRef":"policy-a"}}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldNotBe(Encoding.UTF8.GetString(second.SemanticPayload));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new(
            "Hexalith.Folders.Commands.CreateRepositoryBackedFolder",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(payload),
            Extensions: null);
}
