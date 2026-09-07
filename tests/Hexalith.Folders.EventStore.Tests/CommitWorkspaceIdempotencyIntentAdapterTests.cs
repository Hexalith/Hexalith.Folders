using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class CommitWorkspaceIdempotencyIntentAdapterTests
{
    [Fact]
    public void EquivalentPayloadsShouldProduceTheSameCanonicalIntent()
    {
        CommitWorkspaceIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"authorMetadataReference":"authorref_service","branchRefTarget":"branchref_primary","changedPathMetadataDigest":"digest_a","commitMessageClassification":"generated_summary","operationId":"operation-a","taskId":"task-a","workspaceId":"workspace-a"}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"workspaceId":"workspace-a","taskId":"task-a","operationId":"operation-a","commitMessageClassification":"generated_summary","changedPathMetadataDigest":"digest_a","branchRefTarget":"branchref_primary","authorMetadataReference":"authorref_service"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(second.SemanticPayload));
        first.CanonicalTarget.ShouldBe("tenant-a/folders/folder-a");
        first.PolicyVersion.ShouldBe(FoldersCanonicalIntentBuilder.PolicyVersion);
    }

    [Fact]
    public void DifferentBranchRefTargetShouldChangeCanonicalIntent()
    {
        CommitWorkspaceIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"authorMetadataReference":"authorref_service","branchRefTarget":"branchref_primary","changedPathMetadataDigest":"digest_a","commitMessageClassification":"generated_summary","operationId":"operation-a","taskId":"task-a","workspaceId":"workspace-a"}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"authorMetadataReference":"authorref_service","branchRefTarget":"branchref_feature","changedPathMetadataDigest":"digest_a","commitMessageClassification":"generated_summary","operationId":"operation-a","taskId":"task-a","workspaceId":"workspace-a"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldNotBe(Encoding.UTF8.GetString(second.SemanticPayload));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new(
            "Hexalith.Folders.Commands.CommitWorkspace",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(payload),
            Extensions: null);
}
