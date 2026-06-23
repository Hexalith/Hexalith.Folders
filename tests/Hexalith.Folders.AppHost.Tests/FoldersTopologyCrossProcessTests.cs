using System.Net.Http.Json;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Dapr.Client;

using Hexalith.Folders.Aspire;
using Hexalith.Folders.Workers.SemanticIndexing;

using Shouldly;

using Xunit;

// Alias the Memories contracts so this file does not import the V1 namespace (which also defines a Case type that
// would collide with Shouldly.Case) wholesale.
using ScoredResult = Hexalith.Memories.Contracts.V1.ScoredResult;
using SearchIndexEntryChanged = Hexalith.Memories.Contracts.V1.SearchIndexEntryChanged;
using SearchIndexEntryRemoved = Hexalith.Memories.Contracts.V1.SearchIndexEntryRemoved;
using SearchResult = Hexalith.Memories.Contracts.V1.SearchResult;

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
    /// Story 10.4 AC9 — the live publish → route → index → search → remove round-trip against the real
    /// <c>folders-index</c>. It seeds the index by publishing a real <see cref="SearchIndexEntryChanged"/> through the
    /// worker pub/sub component (option (b): do not depend on the fail-closed content materializer), then proves:
    /// (a) a syntactic search returns exactly one hit whose <see cref="ScoredResult.SourceUri"/> echoes the published
    /// <c>cloudevent.id</c>; (b) after a <see cref="SearchIndexEntryRemoved"/> the search returns zero (no stale
    /// entry); (c) after a <see cref="SearchIndexEntryChanged"/> with <c>folders.status=archived</c> the document
    /// remains, filterable by status. BLOCKED-PENDING the DCP-capable lane: it skips cleanly whenever the opt-in/DCP
    /// capability is absent (the env-wide Aspire CLI/DCP boot mismatch, open Epic 9 action item), so no lane goes red.
    /// </summary>
    [Fact]
    public async Task SeedRemoveAndArchiveRoundTripAgainstFoldersIndex()
    {
        _fixture.SkipIfUnavailable();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DaprClient dapr = CreateWorkerDaprClient();
        using HttpClient memories = _fixture.App.CreateHttpClient(FoldersAspireModule.MemoriesAppId);

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
            $"/api/search?tenantId={FoldersAspireModule.MemoriesIndexTenant}&axis=syntactic&maxResults=10&query={Uri.EscapeDataString(query)}";
        if (statusFilter is not null)
        {
            url += $"&attr:{FoldersSemanticIndexingDefaults.StatusAttributeKey}={Uri.EscapeDataString(statusFilter)}";
        }

        SearchResult? result = await memories
            .GetFromJsonAsync<SearchResult>(url, cancellationToken)
            .ConfigureAwait(true);
        return result.ShouldNotBeNull();
    }

    // Builds a Dapr client bound to the running folders-workers sidecar's HTTP endpoint so the round-trip publishes a
    // real CloudEvent through the same pub/sub component the worker uses. Skips cleanly when the sidecar endpoint
    // cannot be resolved on the current host (the round-trip is BLOCKED-PENDING the DCP lane regardless).
    private DaprClient CreateWorkerDaprClient()
    {
        Uri? endpoint = TryResolveDaprHttpEndpoint(FoldersAspireModule.FoldersWorkersAppId);
        if (endpoint is null)
        {
            Assert.Skip("Could not resolve a folders-workers Dapr sidecar HTTP endpoint for the AC9 round-trip seed on this host.");
        }

        return new DaprClientBuilder()
            .UseHttpEndpoint(endpoint!.ToString())
            .Build();
    }

    private Uri? TryResolveDaprHttpEndpoint(string resourceName)
    {
        try
        {
            return _fixture.App.GetEndpoint(resourceName, "dapr-http");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
