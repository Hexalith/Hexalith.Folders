using System.Text;
using System.Text.Json;

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
        first.SemanticOptions!["semantic_payload_sha256"].ShouldBe(second.SemanticOptions!["semantic_payload_sha256"]);
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

    [Theory]
    [InlineData("""{"taskId":"task-a","TaskId":"task-b"}""")]
    [InlineData("""{"taskId":"task-a","pathMetadata":{"normalizedPath":"a","normalizedPath":"b"}}""")]
    [InlineData("""{"taskId":"task-a","pathMetadata":[{"normalizedPath":"a","normalizedPath":"b"}]}""")]
    public void DuplicateJsonPropertiesShouldBeRejectedBeforeCanonicalIntent(string payload)
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(Command(payload)));
    }

    [Fact]
    public void ExtensionOnlyTaskScopeShouldBeRejected()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyIntentCommand command = Command("""{"fileOperationKind":"add","workspaceId":"workspace-a"}""")
            with
        { Extensions = new Dictionary<string, string> { ["taskId"] = "task-a" } };

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(command));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void NonStringTaskScopeShouldBeRejected(string task)
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(Command($$"""{"taskId":{{task}}}""")));
    }

    [Fact]
    public void ConflictingExtensionTaskScopeShouldBeRejected()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyIntentCommand command = Command("""{"taskId":"task-a","fileOperationKind":"add","workspaceId":"workspace-a"}""")
            with
        { Extensions = new Dictionary<string, string> { ["taskId"] = "task-b" } };

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(command));
    }

    [Fact]
    public void NullAndOmissionRemainDistinctWhileTransportChangesDoNot()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent omitted = adapter.CreateIntent(Command(
            """{"taskId":"task-a","workspaceId":"workspace-a","fileOperationKind":"remove","transportOperation":"metadataOnlyRemoval"}"""));
        IdempotencyCanonicalIntent explicitNull = adapter.CreateIntent(Command(
            """{"taskId":"task-a","workspaceId":"workspace-a","fileOperationKind":"remove","transportOperation":"metadataOnlyRemoval","contentHashReference":null}"""));
        IdempotencyCanonicalIntent changedTransport = adapter.CreateIntent(Command(
            """{"taskId":"task-a","workspaceId":"workspace-a","fileOperationKind":"remove","transportOperation":"other"}"""));

        omitted.SemanticPayload.ShouldBe(explicitNull.SemanticPayload);
        omitted.SemanticOptions!["semantic_payload_sha256"].ShouldNotBe(explicitNull.SemanticOptions!["semantic_payload_sha256"]);
        omitted.SemanticOptions["semantic_payload_sha256"].ShouldBe(changedTransport.SemanticOptions!["semantic_payload_sha256"]);
    }

    [Fact]
    public void SameSemanticFileMutationWithDifferentRetryTransportHasEquivalentIntent()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"requestSchemaVersion":"v1","workspaceId":"workspace-a","operationId":"operation-a","fileOperationKind":"add","pathMetadata":{"normalizedPath":"docs/readme.md","displayName":"readme.md","pathPolicyClass":"tenant_sensitive_document","unicodeNormalization":"NFC"},"contentHashReference":"sha256:a","byteLength":12,"mediaType":"text/plain","taskId":"task-a","transportOperation":"PutFileInline","transportEvidenceKind":"inline_decoded","observedByteLength":12}"""));
        IdempotencyCanonicalIntent retried = adapter.CreateIntent(Command(
            """{"taskId":"task-a","mediaType":"text/plain","byteLength":12,"contentHashReference":"sha256:a","pathMetadata":{"unicodeNormalization":"NFC","pathPolicyClass":"tenant_sensitive_document","displayName":"readme.md","normalizedPath":"docs/readme.md"},"fileOperationKind":"add","operationId":"operation-a","workspaceId":"workspace-a","requestSchemaVersion":"v1","transportOperation":"PutFileStream","transportEvidenceKind":"stream_observed","observedByteLength":13,"retryTransportMetadata":"second-attempt"}"""));

        first.SemanticPayload.ShouldBe(retried.SemanticPayload);
        first.SemanticOptions!["semantic_payload_sha256"].ShouldBe(retried.SemanticOptions!["semantic_payload_sha256"]);
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
