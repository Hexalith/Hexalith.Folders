using System.Text;
using System.Text.Json;

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

    [Theory]
    [InlineData("\"another_scope\"")]
    [InlineData("null")]
    [InlineData("42")]
    public void CallerSelectedCredentialScopeShouldBeRejected(string scope)
    {
        CreateRepositoryBackedFolderIdempotencyIntentAdapter adapter = new();
        BindRepositoryIdempotencyIntentAdapter binding = new();
        string payload = $$"""{"credentialScopeClass":{{scope}}}""";

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(Command(payload)));
        _ = Should.Throw<JsonException>(() => binding.CreateIntent(Command(payload) with { CommandType = binding.CommandType }));
    }

    [Fact]
    public void CredentialScopeShouldComeFromTheFixedServerPolicy()
    {
        CreateRepositoryBackedFolderIdempotencyIntentAdapter adapter = new();
        BindRepositoryIdempotencyIntentAdapter binding = new();
        IdempotencyIntentCommand command = Command("{}") with
        {
            Extensions = new Dictionary<string, string> { ["credentialScopeClass"] = "another_scope" },
        };

        adapter.CreateIntent(command).CredentialScope.ShouldBe("provider_binding");
        binding.CreateIntent(command with { CommandType = binding.CommandType }).CredentialScope.ShouldBe("provider_binding");
        IdempotencyCanonicalIntent implicitScope = adapter.CreateIntent(command);
        IdempotencyCanonicalIntent explicitScope = adapter.CreateIntent(Command("""{"credentialScopeClass":"provider_binding"}"""));
        implicitScope.SemanticPayload.ShouldBe(explicitScope.SemanticPayload);
        implicitScope.SemanticOptions!["semantic_payload_sha256"].ShouldBe(explicitScope.SemanticOptions!["semantic_payload_sha256"]);
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
