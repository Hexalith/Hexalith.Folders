using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class MutateFilesIdempotencyIntentAdapterTests
{
    [Fact]
    public void EquivalentPathMetadataShouldProduceTheSameCanonicalIntent()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"fileOperationKind":"add","operationId":"operation-a","workspaceId":"workspace-a","taskId":"task-a","contentHashReference":"hashref-a","pathMetadata":{"normalizedPath":"docs/readme.md","displayName":"readme.md","pathPolicyClass":"tenant_sensitive_document","unicodeNormalization":"NFC"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"contentHashReference":"hashref-a","fileOperationKind":"add","operationId":"operation-a","pathMetadata":{"unicodeNormalization":"NFC","pathPolicyClass":"tenant_sensitive_document","displayName":"readme.md","normalizedPath":"docs/readme.md"},"taskId":"task-a","workspaceId":"workspace-a"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(second.SemanticPayload));
        first.CanonicalTarget.ShouldBe("tenant-a/folders/folder-a");
        first.PolicyVersion.ShouldBe(FoldersCanonicalIntentBuilder.PolicyVersion);
    }

    [Fact]
    public void DifferentFileOperationKindShouldChangeCanonicalIntent()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"fileOperationKind":"add","operationId":"operation-a","workspaceId":"workspace-a","taskId":"task-a","contentHashReference":"hashref-a","pathMetadata":{"normalizedPath":"docs/readme.md","displayName":"readme.md","pathPolicyClass":"tenant_sensitive_document","unicodeNormalization":"NFC"}}"""));
        IdempotencyCanonicalIntent second = adapter.CreateIntent(Command(
            """{"fileOperationKind":"remove","operationId":"operation-a","workspaceId":"workspace-a","taskId":"task-a","contentHashReference":"hashref-a","pathMetadata":{"normalizedPath":"docs/readme.md","displayName":"readme.md","pathPolicyClass":"tenant_sensitive_document","unicodeNormalization":"NFC"}}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldNotBe(Encoding.UTF8.GetString(second.SemanticPayload));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new(
            "Hexalith.Folders.Commands.MutateFiles",
            "tenant-a",
            "folders",
            "folder-a",
            Encoding.UTF8.GetBytes(payload),
            Extensions: null);
}
