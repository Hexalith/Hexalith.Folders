using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class ReleaseWorkspaceLockIdempotencyIntentAdapterTests
{
    [Fact]
    public void ChangedReleaseReasonCannotReplayAsEquivalent()
    {
        ReleaseWorkspaceLockIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent completed = adapter.CreateIntent(Command("caller_completed"));
        IdempotencyCanonicalIntent abandoned = adapter.CreateIntent(Command("caller_abandoned"));

        Encoding.UTF8.GetString(completed.SemanticPayload)
            .ShouldBe(Encoding.UTF8.GetString(abandoned.SemanticPayload));
        completed.SemanticOptions!["semantic_payload_sha256"]
            .ShouldNotBe(abandoned.SemanticOptions!["semantic_payload_sha256"]);
    }

    private static IdempotencyIntentCommand Command(string reason)
        => new(
            "Hexalith.Folders.Commands.ReleaseWorkspaceLock",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(
                $$"""{"requestSchemaVersion":"v1","workspaceId":"workspace-a","taskId":"task-a","lockId":"lock-a","lockOwnershipProof":"proof-a","releaseReasonCode":"{{reason}}"}"""),
            Extensions: null);
}
