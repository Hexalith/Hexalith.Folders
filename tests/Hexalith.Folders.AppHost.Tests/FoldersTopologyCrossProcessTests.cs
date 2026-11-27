using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Aspire;
using Hexalith.Folders.Workers.SemanticIndexing;

using Shouldly;

using Xunit;

// Alias the Memories contracts so this file does not import the V1 namespace (which also defines a Case type that
// would collide with Shouldly.Case) wholesale.
using MemoriesRoutes = Hexalith.Memories.Contracts.V1.MemoriesRoutes;
using ScoredResult = Hexalith.Memories.Contracts.V1.ScoredResult;
using SearchIndexEntryChanged = Hexalith.Memories.Contracts.V1.SearchIndexEntryChanged;
using SearchIndexEntryRemoved = Hexalith.Memories.Contracts.V1.SearchIndexEntryRemoved;
using SearchResult = Hexalith.Memories.Contracts.V1.SearchResult;
using TenantInfo = Hexalith.Memories.Contracts.V1.TenantInfo;
using TenantStatus = Hexalith.Memories.Contracts.V1.TenantStatus;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Tier-3 cross-process proof that the full Folders topology — the <c>eventstore</c> folder-events publisher and
/// the <c>folders-workers</c> / <c>memories</c> subscribers — boots together with the Story 10.3 D1 wiring
/// (the <c>folders.events</c> EventStore publish-topic override plus the production pub/sub scopes), activating
/// the Epic 9 routing that is dormant in-process. Runs only on a DCP-capable host opted in via
/// <see cref="AspireFoldersAppHostFixture.OptInEnvironmentVariable"/>; otherwise every test skips.
/// </summary>
public sealed class FoldersTopologyCrossProcessTests(AspireFoldersAppHostFixture fixture)
    : IClassFixture<AspireFoldersAppHostFixture>
{
    private const string StateStore = "statestore";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IndexReadinessTimeout = TimeSpan.FromMinutes(3);

    private readonly AspireFoldersAppHostFixture _fixture = fixture;

    [Fact]
    public void FullFoldersTopologyBootsRunningAcrossProcesses()
    {
        _fixture.SkipIfUnavailable();

        foreach (string resourceName in AspireFoldersAppHostFixture.TopologyResourceNames)
        {
            _fixture.App.ResourceNotifications.TryGetCurrentState(resourceName, out ResourceEvent? resourceEvent)
                .ShouldBeTrue($"Resource '{resourceName}' is missing from the running topology.");
            resourceEvent.ShouldNotBeNull();
            resourceEvent.Snapshot.State?.Text.ShouldBe(
                KnownResourceStates.Running,
                $"Resource '{resourceName}' did not boot to Running.");
        }
    }

    [Fact]
    public void EventStorePublisherAndFolderWorkerSubscriberAreLive()
    {
        _fixture.SkipIfUnavailable();

        // The publish/subscribe participants of the semantic-indexing path are each running across the process
        // boundary: eventstore publishes folder.events, folders-workers subscribes folders.events + memories-events,
        // and memories subscribes memories-events. The deny-by-default Dapr scopes that authorize this are pinned
        // separately by DaprPolicyConformanceTests; here we prove the processes actually come up.
        foreach (string participant in new[] { "eventstore", "folders-workers", "memories" })
        {
            _fixture.App.ResourceNotifications.TryGetCurrentState(participant, out ResourceEvent? resourceEvent)
                .ShouldBeTrue($"Publish/subscribe participant '{participant}' is missing from the running topology.");
            resourceEvent!.Snapshot.State?.Text.ShouldBe(
                KnownResourceStates.Running,
                $"Publish/subscribe participant '{participant}' did not boot to Running.");
        }

        // The folders REST gateway (the entry point a real folder mutation hits to emit a folder domain event)
        // published its HTTP endpoint — Aspire resolves it through service discovery.
        using HttpClient foldersClient = _fixture.App.CreateHttpClient(FoldersAspireModule.FoldersAppId);
        foldersClient.BaseAddress.ShouldNotBeNull();
    }

    /// <summary>
    /// Publishes a synthetic organization event through the EventStore sidecar and verifies the
    /// worker's durable receipt for that delivery. This diagnostic covers the Story 10.3 D1
    /// pub/sub route. FR58 acceptance requires a public mutation followed by a public search.
    /// </summary>
    [Fact]
    public async Task EventStoreSidecarShouldPublishFolderEnvelopeToWorkerSubscriberTopic()
    {
        _fixture.SkipIfUnavailable();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DaprClient publisher = await CreateDaprClientAsync(
            FoldersAspireModule.EventStoreAppId, cancellationToken).ConfigureAwait(true);
        using DaprClient workerSidecar = await CreateDaprClientAsync(
            FoldersAspireModule.FoldersWorkersAppId, cancellationToken).ConfigureAwait(true);
        DaprReadModelStore store = new(workerSidecar);

        // The organization actor gives the probe one durable receipt row to observe and delete.
        string tenantId = NewId();
        string messageId = NewId();
        string projectionKey = "projection:folders:organization-permissions:" + tenantId;
        OrganizationAclBaselineInitialized initialized = new(tenantId, tenantId, NewId(), NewId(), NewId(), NewId());
        EventStoreDomainEventEnvelope envelope = new(
            messageId,
            tenantId,
            tenantId,
            typeof(OrganizationAclBaselineInitialized).FullName!,
            1,
            DateTimeOffset.UtcNow,
            initialized.CorrelationId,
            "json",
            JsonSerializer.SerializeToUtf8Bytes(initialized, EventStorePayloadSerialization.Options))
        {
            Domain = "folders",
        };

        bool receiptVerified = false;
        try
        {
            using CancellationTokenSource delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            delivery.CancelAfter(ProbeTimeout);
            await publisher.PublishEventAsync(
                FoldersSemanticIndexingDefaults.PubSubName,
                FoldersAspireModule.FolderDomainEventsTopic,
                envelope,
                delivery.Token).ConfigureAwait(true);

            OrganizationPermissionProjection receipt = await WaitForReceiptAsync(
                store, projectionKey, delivery.Token).ConfigureAwait(true);
            receipt.TenantId.ShouldBe(tenantId);
            receipt.OrganizationId.ShouldBe(tenantId);
            receipt.IsInitialized.ShouldBeTrue();
            receipt.LastSequence.ShouldBe(envelope.SequenceNumber);
            FolderProjectionMessage message = receipt.RecentMessages.ShouldNotBeNull().ShouldHaveSingleItem();
            message.MessageId.ShouldBe(messageId);
            message.Sequence.ShouldBe(envelope.SequenceNumber);
            receiptVerified = true;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The Workers did not persist the published folder-domain receipt within the probe budget.", exception);
        }
        finally
        {
            try
            {
                await EraseReceiptAsync(store, projectionKey, messageId).ConfigureAwait(true);
            }
            catch (Exception exception) when (!receiptVerified)
            {
                TestContext.Current.AddWarning($"Synthetic receipt cleanup also failed ({exception.GetType().Name}).");
            }
        }
    }

    /// <summary>
    /// Diagnostic (not Story 10.8 / FR58 acceptance): Story 10.4 AC9 live publish → route → index → search →
    /// remove round-trip against the real <c>folders-index</c>. It seeds the index by publishing a real
    /// <see cref="SearchIndexEntryChanged"/> through the worker pub/sub component, then proves:
    /// (a) a syntactic search returns exactly one hit whose <see cref="ScoredResult.SourceUri"/> echoes the published
    /// <c>cloudevent.id</c>; (b) after a <see cref="SearchIndexEntryRemoved"/> the search returns zero (no stale
    /// entry); (c) after a <see cref="SearchIndexEntryChanged"/> with <c>folders.status=archived</c> the document
    /// remains, filterable by status. Direct Memories query and index seeding cannot satisfy FR58.
    /// </summary>
    [Fact]
    public async Task SeedRemoveAndArchiveRoundTripAgainstFoldersIndex()
    {
        _fixture.SkipIfUnavailable();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DaprClient dapr = await CreateDaprClientAsync(
            FoldersAspireModule.FoldersWorkersAppId, cancellationToken).ConfigureAwait(true);
        using HttpClient memories = _fixture.CreateMemoriesDiagnosticClient();
        await WaitForIndexTenantAsync(memories, cancellationToken).ConfigureAwait(true);

        const string query = "hexalithfoldersroundtripseed";
        const string aggregateId = "tenant-a/organization-a/folder-a/version-roundtrip";
        const string cloudEventId =
            "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-roundtrip";

        // (1) Seed: a real SearchIndexEntryChanged through the worker pub/sub component populates folders-index.
        await PublishChangedAsync(dapr, aggregateId, cloudEventId, $"{query} version-roundtrip", FoldersSemanticIndexingDefaults.StatusActive, cancellationToken).ConfigureAwait(true);
        SearchResult seeded = await WaitForHitCountAsync(memories, query, statusFilter: null, expected: 1, cancellationToken).ConfigureAwait(true);
        seeded.Results.ShouldHaveSingleItem().SourceUri.ShouldBe(cloudEventId);

        // (2) Hard delete: SearchIndexEntryRemoved drops the document — search returns zero, no stale entry.
        await PublishRemovedAsync(dapr, aggregateId, cloudEventId, cancellationToken).ConfigureAwait(true);
        SearchResult removed = await WaitForHitCountAsync(memories, query, statusFilter: null, expected: 0, cancellationToken).ConfigureAwait(true);
        removed.Results.ShouldBeEmpty();

        // (3) Archive soft delete: the document remains but is now filterable as folders.status=archived (and no longer
        // matches the active filter).
        await PublishChangedAsync(dapr, aggregateId, cloudEventId, $"{query} version-roundtrip", FoldersSemanticIndexingDefaults.StatusArchived, cancellationToken).ConfigureAwait(true);
        SearchResult archived = await WaitForHitCountAsync(memories, query, FoldersSemanticIndexingDefaults.StatusArchived, expected: 1, cancellationToken).ConfigureAwait(true);
        archived.Results.ShouldHaveSingleItem().SourceUri.ShouldBe(cloudEventId);
        SearchResult stillActive = await WaitForHitCountAsync(memories, query, FoldersSemanticIndexingDefaults.StatusActive, expected: 0, cancellationToken).ConfigureAwait(true);
        stillActive.Results.ShouldBeEmpty();
    }

    private static Task PublishChangedAsync(
        DaprClient dapr,
        string aggregateId,
        string cloudEventId,
        string text,
        string status,
        CancellationToken cancellationToken)
    {
        SearchIndexEntryChanged changed = new()
        {
            TenantId = FoldersAspireModule.MemoriesIndexTenant,
            AggregateId = aggregateId,
            Text = text,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["folders.fileVersionId"] = "version-roundtrip",
                [FoldersSemanticIndexingDefaults.StatusAttributeKey] = status,
            },
        };

        return dapr.PublishEventAsync(
            FoldersSemanticIndexingDefaults.PubSubName,
            FoldersSemanticIndexingDefaults.EventsTopicName,
            changed,
            Metadata(cloudEventId, nameof(SearchIndexEntryChanged)),
            cancellationToken);
    }

    private static Task PublishRemovedAsync(
        DaprClient dapr,
        string aggregateId,
        string cloudEventId,
        CancellationToken cancellationToken)
    {
        SearchIndexEntryRemoved removed = new()
        {
            TenantId = FoldersAspireModule.MemoriesIndexTenant,
            AggregateId = aggregateId,
        };

        return dapr.PublishEventAsync(
            FoldersSemanticIndexingDefaults.PubSubName,
            FoldersSemanticIndexingDefaults.EventsTopicName,
            removed,
            Metadata(cloudEventId, nameof(SearchIndexEntryRemoved)),
            cancellationToken);
    }

    private static Dictionary<string, string> Metadata(string cloudEventId, string cloudEventType)
        => new(StringComparer.Ordinal)
        {
            ["cloudevent.id"] = cloudEventId,
            ["cloudevent.type"] = cloudEventType,
            ["cloudevent.source"] = FoldersSemanticIndexingDefaults.CloudEventsSource,
        };

    private static async Task WaitForIndexTenantAsync(HttpClient memories, CancellationToken cancellationToken)
    {
        using CancellationTokenSource readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readiness.CancelAfter(IndexReadinessTimeout);
        string path = MemoriesRoutes.TenantPath(FoldersAspireModule.MemoriesIndexTenant);
        try
        {
            while (true)
            {
                using HttpResponseMessage response = await memories.GetAsync(path, readiness.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                    TenantInfo tenant = (await response.Content.ReadFromJsonAsync<TenantInfo>(readiness.Token).ConfigureAwait(false))
                        .ShouldNotBeNull();
                    tenant.Id.ShouldBe(FoldersAspireModule.MemoriesIndexTenant);
                    if (tenant.Status == TenantStatus.Active)
                    {
                        return;
                    }

                    if (tenant.Status is TenantStatus.Failed or TenantStatus.CompensationFailed)
                    {
                        throw new InvalidOperationException("Provisioning the routed Memories index tenant failed.");
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), readiness.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The routed Memories index tenant did not become active within the probe budget.", exception);
        }
    }

    // Search is eventually consistent (Dapr at-least-once delivery + RediSearch indexing), so poll until the expected
    // hit count is observed or the budget is exhausted; the final result is asserted by the caller.
    private static async Task<SearchResult> WaitForHitCountAsync(
        HttpClient memories,
        string query,
        string? statusFilter,
        int expected,
        CancellationToken cancellationToken)
    {
        SearchResult result = await SearchAsync(memories, query, statusFilter, cancellationToken).ConfigureAwait(true);
        for (int attempt = 0; attempt < 20 && result.Results.Count != expected; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(true);
            result = await SearchAsync(memories, query, statusFilter, cancellationToken).ConfigureAwait(true);
        }

        return result;
    }

    private static async Task<SearchResult> SearchAsync(
        HttpClient memories,
        string query,
        string? statusFilter,
        CancellationToken cancellationToken)
    {
        string url =
            $"{MemoriesRoutes.Search}?tenantId={FoldersAspireModule.MemoriesIndexTenant}&axis=syntactic&maxResults=10&query={Uri.EscapeDataString(query)}";
        if (statusFilter is not null)
        {
            // The Memories search endpoint binds attribute filters ONLY from query keys prefixed "attribute."
            // (ReadAttributeFilters strips that prefix to derive the attribute name). The remaining key must equal the
            // worker's upsert attribute key (folders.status), since index-time and query-time both run it through
            // SyntacticSearchService.BuildAttributeTag(key, value). An "attr:" prefix is silently dropped (no filtering).
            url += $"&attribute.{FoldersSemanticIndexingDefaults.StatusAttributeKey}={Uri.EscapeDataString(statusFilter)}";
        }

        SearchResult? result = await memories
            .GetFromJsonAsync<SearchResult>(url, cancellationToken)
            .ConfigureAwait(true);
        return result.ShouldNotBeNull();
    }

    private static string NewId() => UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();

    private static async Task<OrganizationPermissionProjection> WaitForReceiptAsync(
        DaprReadModelStore store,
        string projectionKey,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadModelEntry<OrganizationPermissionProjection> entry =
                await store.GetAsync<OrganizationPermissionProjection>(StateStore, projectionKey, cancellationToken).ConfigureAwait(false);
            if (entry.Value is not null)
            {
                return entry.Value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EraseReceiptAsync(DaprReadModelStore store, string projectionKey, string messageId)
    {
        using CancellationTokenSource cleanup = new(ProbeTimeout);
        ReadModelEntry<OrganizationPermissionProjection> entry =
            await store.GetAsync<OrganizationPermissionProjection>(StateStore, projectionKey, cleanup.Token).ConfigureAwait(false);
        if (entry.Value?.RecentMessages?.Any(message => message.MessageId == messageId) == true)
        {
            string etag = entry.ETag.ShouldNotBeNull();
            (await store.TryEraseAsync(StateStore, projectionKey, etag, cleanup.Token).ConfigureAwait(false))
                .ShouldBeTrue("The topology probe must remove its synthetic receipt.");
        }
    }

    private async Task<DaprClient> CreateDaprClientAsync(string resourceName, CancellationToken cancellationToken)
    {
        string sidecar = _fixture.DaprCliResourceName(resourceName);
        using CancellationTokenSource ready = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ready.CancelAfter(ProbeTimeout);

        await _fixture.App.ResourceNotifications
            .WaitForResourceAsync(sidecar, KnownResourceStates.Running, ready.Token).ConfigureAwait(false);
        DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(_fixture.App.GetEndpoint(sidecar, "http").ToString())
            .UseGrpcEndpoint(_fixture.App.GetEndpoint(sidecar, "grpc").ToString())
            .Build();
        try
        {
            await client.WaitForSidecarAsync(ready.Token).ConfigureAwait(false);
            while (!await client.CheckHealthAsync(ready.Token).ConfigureAwait(false))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), ready.Token).ConfigureAwait(false);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
