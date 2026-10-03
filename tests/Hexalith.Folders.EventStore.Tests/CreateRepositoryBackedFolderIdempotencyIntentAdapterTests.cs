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
        Encoding.UTF8.GetString(first.SemanticPayload)
            .ShouldContain("branch_ref_policy.repository_binding_id");
        first.SemanticOptions!["semantic_payload_sha256"].ShouldBe(second.SemanticOptions!["semantic_payload_sha256"]);
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

    [Fact]
    public void BranchPolicyBehaviorFieldsParticipateWhilePatternSetOrderDoesNot()
    {
        CreateRepositoryBackedFolderIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"providerBindingRef":"provider-a","repositoryProfileRef":"profile-a","branchRefPolicy":{"policyRef":"policy-a","repositoryBindingId":"binding-a","defaultRef":"main","allowedRefPatterns":["main","feature/*"]}}"""));
        IdempotencyCanonicalIntent reordered = adapter.CreateIntent(Command(
            """{"branchRefPolicy":{"allowedRefPatterns":["feature/*","main"],"repositoryBindingId":"binding-a","defaultRef":"main","policyRef":"policy-a"},"repositoryProfileRef":"profile-a","providerBindingRef":"provider-a"}"""));
        IdempotencyCanonicalIntent changed = adapter.CreateIntent(Command(
            """{"providerBindingRef":"provider-a","repositoryProfileRef":"profile-a","branchRefPolicy":{"policyRef":"policy-a","repositoryBindingId":"binding-a","defaultRef":"develop","allowedRefPatterns":["main","feature/*"]}}"""));

        first.SemanticOptions!["semantic_payload_sha256"].ShouldBe(reordered.SemanticOptions!["semantic_payload_sha256"]);
        first.SemanticOptions["semantic_payload_sha256"].ShouldNotBe(changed.SemanticOptions!["semantic_payload_sha256"]);
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
