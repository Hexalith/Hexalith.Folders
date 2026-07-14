using System.Text;

using Dapr.Client;

using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Workers.SemanticIndexing;

using NSubstitute;

using SearchIndexEntryChanged = Hexalith.Memories.Contracts.V1.SearchIndexEntryChanged;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class MetadataDerivedSemanticIndexingContentMaterializerTests
{
    [Fact]
    public async Task MaterializeAsyncShouldReturnAvailableWithFacadeSecurityTrimAttributes()
    {
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult result = await materializer
            .MaterializeAsync(CreateRequest(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingContentMaterializationStatus.Available);
        result.ReasonCode.ShouldBe("metadata-derived");
        result.Retryable.ShouldBeFalse();
        result.ContentType.ShouldBe("text/markdown");
        result.LengthBytes.ShouldBe(1024);
        result.SizeClassification.ShouldBe("small");
        result.TypeClassification.ShouldBe("text");
        result.CuratedText.ShouldNotBeNullOrWhiteSpace();
        Encoding.UTF8.GetString(result.ContentBytes!).ShouldBe(result.CuratedText);

        IReadOnlyDictionary<string, string> attributes = result.CuratedAttributes.ShouldNotBeNull();
        attributes.Count.ShouldBe(9);
        attributes[FoldersSemanticIndexingAttributes.ManagedTenantIdAttribute].ShouldBe("tenant-a");
        attributes[FoldersSemanticIndexingAttributes.OrganizationIdAttribute].ShouldBe("organization-a");
        attributes[FoldersSemanticIndexingAttributes.FolderIdAttribute].ShouldBe("folder-a");
        attributes[FoldersSemanticIndexingAttributes.WorkspaceIdAttribute].ShouldBe("workspace-a");
        attributes[FoldersSemanticIndexingAttributes.FileVersionIdAttribute].ShouldBe("fv-version-a");
        attributes[FoldersSemanticIndexingAttributes.StatusAttribute].ShouldBe(FoldersSemanticIndexingAttributes.StatusActive);
        attributes[FoldersSemanticIndexingAttributes.ContentDescriptorAttribute].ShouldBe("metadata-derived");
        attributes[FoldersSemanticIndexingAttributes.SizeClassificationAttribute].ShouldBe("small");
        attributes[FoldersSemanticIndexingAttributes.TypeClassificationAttribute].ShouldBe("text");
    }

    [Fact]
    public async Task MaterializeAsyncShouldUseObservedLengthAndClassifyDeclaredMediaType()
    {
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult result = await materializer
            .MaterializeAsync(
                CreateRequest(expectedByteLength: 1024, expectedMediaType: "application/json", observedByteLength: 131072),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.ContentType.ShouldBe("application/json");
        result.LengthBytes.ShouldBe(131072);
        result.SizeClassification.ShouldBe("medium");
        result.TypeClassification.ShouldBe("json");
        result.CuratedAttributes![FoldersSemanticIndexingAttributes.SizeClassificationAttribute].ShouldBe("medium");
        result.CuratedAttributes[FoldersSemanticIndexingAttributes.TypeClassificationAttribute].ShouldBe("json");
    }

    [Fact]
    public async Task MaterializeAsyncShouldNotExposeSensitiveMutationContext()
    {
        const string sensitiveContentReference = "C:/repo/secret-token snippet-shaped /etc/passwd file://source";
        SemanticIndexingContentMaterializationRequest request = CreateRequest(
            contentHashReference: sensitiveContentReference,
            pathPolicyClass: "C:/repo/secret-token",
            expectedMediaType: "text/C:/repo/secret-token/snippet-shaped",
            transportEvidenceKind: "file://source/snippet-shaped");
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult result = await materializer
            .MaterializeAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        string[] exposedValues = [result.CuratedText!, .. result.CuratedAttributes!.Values];
        string[] forbiddenValues =
        [
            "C:/",
            "/etc/",
            "file://",
            "secret-token",
            "snippet-shaped",
            request.Identity.SourceUri,
            sensitiveContentReference,
        ];

        foreach (string exposedValue in exposedValues)
        {
            foreach (string forbiddenValue in forbiddenValues)
            {
                exposedValue.ShouldNotContain(forbiddenValue, Case.Sensitive);
            }
        }
    }

    [Fact]
    public async Task MaterializeAsyncShouldReturnReplayStableOrdinalOutput()
    {
        SemanticIndexingContentMaterializationRequest request = CreateRequest();
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult first = await materializer
            .MaterializeAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        SemanticIndexingContentMaterializationResult second = await materializer
            .MaterializeAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        second.CuratedText.ShouldBe(first.CuratedText);
        second.ContentBytes.ShouldBe(first.ContentBytes);
        second.CuratedAttributes!.ToArray().ShouldBe(first.CuratedAttributes!.ToArray());
        IReadOnlyDictionary<string, string> firstAttributes = first.CuratedAttributes.ShouldNotBeNull();
        firstAttributes.Keys.ShouldBe(firstAttributes.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task MaterializeAsyncShouldSkipWhenMediaTypeIsUnavailable()
    {
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult result = await materializer
            .MaterializeAsync(CreateRequest(expectedMediaType: null), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingContentMaterializationStatus.Unavailable);
        result.ReasonCode.ShouldBe("content_descriptor_unavailable");
        result.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task FailClosedFallbackShouldRemainExplicitlyConstructible()
    {
        FailClosedSemanticIndexingContentMaterializer materializer = new();

        SemanticIndexingContentMaterializationResult result = await materializer
            .MaterializeAsync(CreateRequest(), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingContentMaterializationStatus.Unavailable);
        result.ReasonCode.ShouldBe("content_materializer_unavailable");
        result.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task MaterializeAsyncShouldGuardRequestAndCancellation()
    {
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await materializer.MaterializeAsync(null!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ConfigureAwait(true);

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await materializer.MaterializeAsync(CreateRequest(), cancellation.Token).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public async Task RealMaterializerAndPortShouldPublishFacadeDiscoverableMetadataOnlyEntry()
    {
        const string sensitiveContentReference = "C:/repo/secret-token snippet-shaped file://source";
        SemanticIndexingContentMaterializationRequest materializationRequest = CreateRequest(
            contentHashReference: sensitiveContentReference,
            pathPolicyClass: "C:/repo/secret-token",
            transportEvidenceKind: "file://source/snippet-shaped");
        MetadataDerivedSemanticIndexingContentMaterializer materializer = new();
        SemanticIndexingContentMaterializationResult materialized = await materializer
            .MaterializeAsync(materializationRequest, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        DaprClient dapr = Substitute.For<DaprClient>();
        MemoriesSemanticIndexingPort port = new(dapr);
        SemanticIndexingSourceIdentity source = new(
            "folders",
            "tenant-a",
            "organizations/organization-a/folders/folder-a/workspaces/workspace-a/file-versions/fv-version-a");
        SemanticIndexingRequest request = new(
            materializationRequest.Identity.ManagedTenantId,
            materializationRequest.Identity.OrganizationId,
            materializationRequest.Identity.FolderId,
            materializationRequest.Identity.WorkspaceId,
            materializationRequest.Identity.FileVersionId,
            materializationRequest.ContentHashReference,
            source,
            new SemanticIndexingContentDescriptor(
                materialized.ReasonCode,
                materialized.LengthBytes,
                materialized.ContentType!,
                materialized.SizeClassification,
                materialized.TypeClassification,
                materialized.CuratedText!,
                materialized.CuratedAttributes!),
            new SemanticIndexingPolicyOutcome(
                authorizedForIndexing: true,
                materializationRequest.SensitivityClassification,
                materializationRequest.PathPolicyOutcome),
            materializationRequest.CorrelationId,
            materializationRequest.TaskId);

        SemanticIndexingResult result = await port
            .IndexFileVersionAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Accepted);
        object?[] publishedArguments = dapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        SearchIndexEntryChanged entry = publishedArguments[2].ShouldBeOfType<SearchIndexEntryChanged>();
        entry.Attributes[FoldersSemanticIndexingAttributes.ManagedTenantIdAttribute].ShouldBe("tenant-a");
        entry.Attributes[FoldersSemanticIndexingAttributes.OrganizationIdAttribute].ShouldBe("organization-a");
        entry.Attributes[FoldersSemanticIndexingAttributes.FolderIdAttribute].ShouldBe("folder-a");
        entry.Attributes[FoldersSemanticIndexingAttributes.WorkspaceIdAttribute].ShouldBe("workspace-a");
        entry.Attributes[FoldersSemanticIndexingAttributes.FileVersionIdAttribute].ShouldBe("fv-version-a");
        entry.Attributes[FoldersSemanticIndexingAttributes.StatusAttribute].ShouldBe(FoldersSemanticIndexingAttributes.StatusActive);
        entry.Text.ShouldNotContain("C:/", Case.Sensitive);
        entry.Text.ShouldNotContain("secret-token", Case.Sensitive);
        entry.Text.ShouldNotContain("file://", Case.Sensitive);
        entry.Text.ShouldNotContain(materializationRequest.Identity.SourceUri, Case.Sensitive);
    }

    private static SemanticIndexingContentMaterializationRequest CreateRequest(
        string contentHashReference = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        string? pathPolicyClass = "tenant-sensitive",
        long? expectedByteLength = 1024,
        string? expectedMediaType = "text/markdown",
        string? transportEvidenceKind = "inline_decoded",
        long? observedByteLength = null)
    {
        SemanticIndexingFileVersionIdentity identity = new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            "operation-a",
            "sha256:path-metadata-a",
            "fv-version-a",
            contentHashReference,
            "folders://tenant-a/organizations/organization-a/folders/folder-a/workspaces/workspace-a/file-versions/fv-version-a");

        return new SemanticIndexingContentMaterializationRequest(
            identity,
            contentHashReference,
            pathPolicyClass,
            expectedByteLength,
            expectedMediaType,
            transportEvidenceKind,
            observedByteLength,
            "tenant_sensitive",
            "accepted_mutation_authorized",
            "correlation-a",
            "task-a");
    }
}
