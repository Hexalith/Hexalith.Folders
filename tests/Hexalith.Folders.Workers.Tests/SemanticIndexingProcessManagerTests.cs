using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Workers.SemanticIndexing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class SemanticIndexingProcessManagerTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldApplyEventsThenIndexEligibleStaleEntry()
    {
        RecordingBridgeWriter bridge = new();
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "text/plain",
            3,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(
            SemanticIndexingStatus.Accepted,
            "memories_accepted",
            retryable: false,
            publishedEventId: "folders://tenant-a/published-a"));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Indexed);
        results[0].Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");
        bridge.AppliedEnvelopes.ShouldHaveSingleItem().Sequence.ShouldBe(1);
        policy.Evaluated.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        SemanticIndexingContentMaterializationRequest materializationRequest = materializer.Requests.ShouldHaveSingleItem();
        materializationRequest.Identity.ManagedTenantId.ShouldBe("tenant-a");
        materializationRequest.PathPolicyClass.ShouldBe("tenant-sensitive");
        materializationRequest.ExpectedByteLength.ShouldBe(128);
        materializationRequest.ExpectedMediaType.ShouldBe("text/plain");
        materializationRequest.TransportEvidenceKind.ShouldBe("inline_decoded");
        materializationRequest.ObservedByteLength.ShouldBe(128);
        port.Requests.ShouldHaveSingleItem().Source.ToUriString().ShouldStartWith("folders://tenant-a/");
        bridge.RecordedResults.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Indexed);
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldNotReadContentWhenPolicyDenies()
    {
        RecordingBridgeWriter bridge = new();
        DenyingPolicyEvaluator policy = new("tenant_access_denied", retryable: false);
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1],
            "text/plain",
            1,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("tenant_access_denied");
        materializer.Requests.ShouldBeEmpty();
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldSkipTombstonedEntriesWithoutCallingMemories()
    {
        RecordingBridgeWriter bridge = new(Mutation(fileOperationKind: "remove", contentHashReference: null));
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1],
            "text/plain",
            1,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation(fileOperationKind: "remove", contentHashReference: null))],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldBeEmpty();
        policy.Evaluated.ShouldBeEmpty();
        materializer.Requests.ShouldBeEmpty();
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldRecordFailedWhenContentIsUnavailable()
    {
        RecordingBridgeWriter bridge = new();
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Unavailable("content_unavailable", retryable: true));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Failed);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("content_unavailable");
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldSkipOversizedContentBeforeMemories()
    {
        RecordingBridgeWriter bridge = new();
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            new byte[1],
            "text/plain",
            262145,
            "inline",
            "oversized",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("content_too_large");
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldSkipUnsupportedContentTypeBeforeMemories()
    {
        RecordingBridgeWriter bridge = new();
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "application/octet-stream",
            3,
            "inline",
            "small",
            "binary"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("content_type_unsupported");
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldSkipRedactedSensitivityBeforeReadingContent()
    {
        RecordingBridgeWriter bridge = new(Mutation(pathPolicyClass: "credential"));
        FailClosedSemanticIndexingPolicyEvaluator policy = new(new EnabledTenantAccessStore());
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "text/plain",
            3,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation(pathPolicyClass: "credential"))],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("sensitivity_redacted");
        materializer.Requests.ShouldBeEmpty();
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessFolderEventsAsyncShouldFailClosedWhenTenantAccessIsUnavailableBeforeReadingContent()
    {
        RecordingBridgeWriter bridge = new();
        FailClosedSemanticIndexingPolicyEvaluator policy = new(new MissingTenantAccessStore());
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "text/plain",
            3,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(SemanticIndexingStatus.Accepted, "memories_accepted", retryable: false));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);

        IReadOnlyList<SemanticIndexingBridgeEntry> results = await manager.ProcessFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        // Tenant-access authority gate runs first (AC3): an unknown/disabled tenant fails closed and no content is
        // materialized or sent to Memories.
        results.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Failed);
        bridge.RecordedResults.ShouldHaveSingleItem().ReasonCode.ShouldBe("tenant_access_unavailable");
        materializer.Requests.ShouldBeEmpty();
        port.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task EventProcessorShouldNotReindexRedeliveredEventBecauseTheBridgeOwnsIdempotency()
    {
        // Uses the real bridge store (not a recording fake) so the projection's fingerprint-based stale protection
        // is exercised: a redelivered identical event must not be indexed a second time. This replaces the former
        // in-process per-message dedup, which was dead weight on a transient processor.
        InMemorySemanticIndexingBridgeStore bridge = new();
        AllowingPolicyEvaluator policy = new();
        RecordingContentMaterializer materializer = new(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "text/plain",
            3,
            "inline",
            "small",
            "text"));
        RecordingIndexingPort port = new(new SemanticIndexingResult(
            SemanticIndexingStatus.Accepted,
            "memories_accepted",
            retryable: false,
            publishedEventId: "folders://tenant-a/published-a"));
        SemanticIndexingProcessManager manager = new(bridge, policy, materializer, port, TimeProvider.System);
        FoldersSemanticIndexingEventProcessor processor = new(manager);
        EventStoreDomainEventEnvelope envelope = Envelope(Mutation());

        FoldersSemanticIndexingEventProcessingResult first = await processor
            .ProcessAsync(envelope, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        FoldersSemanticIndexingEventProcessingResult second = await processor
            .ProcessAsync(envelope, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        first.ShouldBe(FoldersSemanticIndexingEventProcessingResult.Processed);
        second.ShouldBe(FoldersSemanticIndexingEventProcessingResult.Processed);

        // The redelivered event re-applies to the already-indexed bridge entry (same idempotency fingerprint), so it
        // is never sent to Memories a second time. Idempotency lives in the bridge projection, not an in-process set.
        port.Requests.Count.ShouldBe(1);
    }

    private static WorkspaceFileMutationAccepted Mutation(
        string fileOperationKind = "add",
        string? contentHashReference = "sha256:a",
        string pathPolicyClass = "tenant-sensitive",
        long? byteLength = 128,
        string? mediaType = "text/plain")
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.FileMutated,
            fileOperationKind == "remove" ? "operation-remove-a" : "operation-a",
            fileOperationKind,
            fileOperationKind == "remove" ? "metadataOnlyRemoval" : "PutFileInline",
            pathPolicyClass,
            "path-digest-a",
            contentHashReference,
            fileOperationKind == "remove" ? null : byteLength,
            fileOperationKind == "remove" ? null : mediaType,
            fileOperationKind == "remove" ? null : "inline_decoded",
            fileOperationKind == "remove" ? null : byteLength,
            "principal-a",
            "correlation-a",
            "task-a",
            fileOperationKind == "remove" ? "idempotency-remove-a" : "idempotency-a",
            fileOperationKind == "remove" ? "fingerprint-remove-a" : "fingerprint-a",
            OccurredAt);

    private static EventStoreDomainEventEnvelope Envelope(WorkspaceFileMutationAccepted mutation)
        => new(
            "message-a",
            "folder-a",
            "tenant-a",
            typeof(WorkspaceFileMutationAccepted).FullName!,
            1,
            OccurredAt,
            mutation.CorrelationId,
            "json",
            JsonSerializer.SerializeToUtf8Bytes(mutation));

    private sealed class RecordingBridgeWriter : ISemanticIndexingBridgeWriter
    {
        private readonly WorkspaceFileMutationAccepted _mutation;

        public RecordingBridgeWriter()
            : this(Mutation())
        {
        }

        public RecordingBridgeWriter(WorkspaceFileMutationAccepted mutation)
        {
            _mutation = mutation;
        }

        public List<FolderProjectionEnvelope> AppliedEnvelopes { get; } = [];

        public List<SemanticIndexingResultUpdate> RecordedResults { get; } = [];

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderEventsAsync(
            IReadOnlyCollection<FolderProjectionEnvelope> envelopes,
            CancellationToken cancellationToken = default)
        {
            AppliedEnvelopes.AddRange(envelopes);
            SemanticIndexingBridgeProjection projection = SemanticIndexingBridgeProjection.Empty.Apply(
                [new FolderProjectionEnvelope("tenant-a", 1, _mutation)]);
            return Task.FromResult<IReadOnlyList<SemanticIndexingBridgeEntry>>(projection.Entries.Values.ToArray());
        }

        public Task<SemanticIndexingBridgeEntry?> RecordIndexingResultAsync(
            SemanticIndexingResultUpdate update,
            CancellationToken cancellationToken = default)
        {
            RecordedResults.Add(update);
            SemanticIndexingBridgeEntry current = SemanticIndexingBridgeProjection.Empty.Apply(
                [new FolderProjectionEnvelope("tenant-a", 1, _mutation)]).Entries.Values.ShouldHaveSingleItem();
            return Task.FromResult<SemanticIndexingBridgeEntry?>(SemanticIndexingBridgeProjection.ApplyIndexingResult(current, update));
        }
    }

    private sealed class AllowingPolicyEvaluator : ISemanticIndexingPolicyEvaluator
    {
        public List<SemanticIndexingBridgeEntry> Evaluated { get; } = [];

        public ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
            SemanticIndexingBridgeEntry entry,
            CancellationToken cancellationToken)
        {
            Evaluated.Add(entry);
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Allowed("tenant-sensitive", "allowed"));
        }
    }

    private sealed class DenyingPolicyEvaluator : ISemanticIndexingPolicyEvaluator
    {
        private readonly string _reasonCode;
        private readonly bool _retryable;

        public DenyingPolicyEvaluator(string reasonCode, bool retryable)
        {
            _reasonCode = reasonCode;
            _retryable = retryable;
        }

        public List<SemanticIndexingBridgeEntry> Evaluated { get; } = [];

        public ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
            SemanticIndexingBridgeEntry entry,
            CancellationToken cancellationToken)
        {
            Evaluated.Add(entry);
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Skipped(_reasonCode, _retryable));
        }
    }

    private sealed class RecordingContentMaterializer : ISemanticIndexingContentMaterializer
    {
        private readonly SemanticIndexingContentMaterializationResult _result;

        public RecordingContentMaterializer(SemanticIndexingContentMaterializationResult result)
        {
            _result = result;
        }

        public List<SemanticIndexingContentMaterializationRequest> Requests { get; } = [];

        public ValueTask<SemanticIndexingContentMaterializationResult> MaterializeAsync(
            SemanticIndexingContentMaterializationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class RecordingIndexingPort : ISemanticIndexingPort
    {
        private readonly SemanticIndexingResult _result;

        public RecordingIndexingPort(SemanticIndexingResult result)
        {
            _result = result;
        }

        public List<SemanticIndexingRequest> Requests { get; } = [];

        public ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
            SemanticIndexingRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class EnabledTenantAccessStore : IFolderTenantAccessProjectionStore
    {
        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<FolderTenantAccessProjection?>(new FolderTenantAccessProjection
            {
                TenantId = tenantId,
                Enabled = true,
                Principals = { ["principal-a"] = new FolderTenantPrincipalEvidence("principal-a", "owner") },
            });

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class MissingTenantAccessStore : IFolderTenantAccessProjectionStore
    {
        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<FolderTenantAccessProjection?>(null);

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
