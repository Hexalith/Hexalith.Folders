using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Workers;
using Hexalith.Folders.Workers.SemanticIndexing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class SemanticIndexingEndpointE2ETests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersTenantEventWorkerEndpointsShouldExposeSeparateSemanticIndexingSubscription()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersTenantEventWorkers();
        WebApplication app = builder.Build();

        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersTenantEventWorkerEndpoints();

        RouteEndpoint[] endpoints = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        RouteEndpoint semanticEndpoint = endpoints.Single(static endpoint =>
            string.Equals(endpoint.RoutePattern.RawText, FoldersSemanticIndexingDefaults.DomainEventsRoute, StringComparison.Ordinal));
        Dapr.ITopicMetadata? topicMetadata = semanticEndpoint.Metadata.GetMetadata<Dapr.ITopicMetadata>();
        topicMetadata.ShouldNotBeNull();
        topicMetadata.PubsubName.ShouldBe(FoldersSemanticIndexingDefaults.PubSubName);
        topicMetadata.Name.ShouldBe(FoldersSemanticIndexingDefaults.DomainEventsTopicName);

        RouteEndpoint tenantEndpoint = endpoints.Single(static endpoint =>
            string.Equals(endpoint.RoutePattern.RawText, FoldersWorkersModule.TenantEventsRoute, StringComparison.Ordinal));
        Dapr.ITopicMetadata? tenantMetadata = tenantEndpoint.Metadata.GetMetadata<Dapr.ITopicMetadata>();
        tenantMetadata.ShouldNotBeNull();
        tenantMetadata.PubsubName.ShouldBe(FoldersWorkersModule.TenantEventsPubSubName);
        tenantMetadata.Name.ShouldBe(FoldersWorkersModule.TenantEventsTopicName);
    }

    [Fact]
    public async Task SemanticIndexingSubscriptionEndpointShouldIndexAuthorizedFileMutationEndToEnd()
    {
        WorkerSemanticIndexingHost host = await StartSemanticIndexingHostAsync().ConfigureAwait(true);
        try
        {
            WorkspaceFileMutationAccepted mutation = Mutation();

            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(
                    FoldersSemanticIndexingDefaults.DomainEventsRoute,
                    Envelope("message-index-a", 1, mutation),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            host.Policy.Evaluated.ShouldHaveSingleItem().Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
            host.Materializer.Requests.ShouldHaveSingleItem().Identity.ManagedTenantId.ShouldBe("tenant-a");
            SemanticIndexingRequest request = host.Port.Requests.ShouldHaveSingleItem();
            request.Source.ToUriString().ShouldStartWith("folders://tenant-a/organizations/organization-a/folders/folder-a/");
            request.Source.ToUriString().ShouldNotContain("C:/", Case.Sensitive);

            SemanticIndexingBridgeEntry indexed = (await host.Bridge.GetFileVersionAsync(
                SemanticIndexingFileVersionIdentity.From(mutation),
                TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
            indexed.Status.ShouldBe(SemanticIndexingBridgeStatus.Indexed);
            indexed.ReasonCode.ShouldBe("memories_accepted");
            indexed.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");
            indexed.Identity.SourceUri.ShouldNotContain("C:/", Case.Sensitive);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SemanticIndexingSubscriptionEndpointShouldNotReadContentWhenPolicyDenies()
    {
        RecordingPolicyEvaluator policy = new(SemanticIndexingPolicyEvaluationResult.Skipped("path_policy_denied", retryable: false));
        WorkerSemanticIndexingHost host = await StartSemanticIndexingHostAsync(policy).ConfigureAwait(true);
        try
        {
            WorkspaceFileMutationAccepted mutation = Mutation();

            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(
                    FoldersSemanticIndexingDefaults.DomainEventsRoute,
                    Envelope("message-denied-a", 1, mutation),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            host.Policy.Evaluated.ShouldHaveSingleItem();
            host.Materializer.Requests.ShouldBeEmpty();
            host.Port.Requests.ShouldBeEmpty();

            SemanticIndexingBridgeEntry denied = (await host.Bridge.GetFileVersionAsync(
                SemanticIndexingFileVersionIdentity.From(mutation),
                TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
            denied.Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
            denied.ReasonCode.ShouldBe("path_policy_denied");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SemanticIndexingSubscriptionEndpointShouldRejectInvalidPayloadWithoutEchoingContent()
    {
        WorkerSemanticIndexingHost host = await StartSemanticIndexingHostAsync().ConfigureAwait(true);
        try
        {
            EventStoreDomainEventEnvelope envelope = new(
                "message-invalid-a",
                "folder-a",
                "tenant-a",
                typeof(WorkspaceFileMutationAccepted).FullName!,
                1,
                OccurredAt,
                "correlation-invalid-a",
                "json",
                Encoding.UTF8.GetBytes("{\"managedTenantId\":\"tenant-a\",\"pathMetadataDigest\":\"sentinel-secret-value\""));

            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(
                    FoldersSemanticIndexingDefaults.DomainEventsRoute,
                    envelope,
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            body.ShouldNotContain("sentinel-secret-value", Case.Sensitive);
            host.Policy.Evaluated.ShouldBeEmpty();
            host.Materializer.Requests.ShouldBeEmpty();
            host.Port.Requests.ShouldBeEmpty();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SemanticIndexingSubscriptionEndpointShouldAcknowledgeUnknownEventTypesWithoutSideEffects()
    {
        WorkerSemanticIndexingHost host = await StartSemanticIndexingHostAsync().ConfigureAwait(true);
        try
        {
            HttpResponseMessage response = await host.Client
                .PostAsJsonAsync(
                    FoldersSemanticIndexingDefaults.DomainEventsRoute,
                    new EventStoreDomainEventEnvelope(
                        "message-unknown-a",
                        "folder-a",
                        "tenant-a",
                        "Hexalith.Folders.Aggregates.Folder.UnknownEvent",
                        1,
                        OccurredAt,
                        "correlation-unknown-a",
                        "json",
                        "{}"u8.ToArray()),
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            host.Policy.Evaluated.ShouldBeEmpty();
            host.Materializer.Requests.ShouldBeEmpty();
            host.Port.Requests.ShouldBeEmpty();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<WorkerSemanticIndexingHost> StartSemanticIndexingHostAsync(
        RecordingPolicyEvaluator? policy = null,
        RecordingContentMaterializer? materializer = null,
        RecordingIndexingPort? port = null)
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridge = new(readModelStore);
        policy ??= new RecordingPolicyEvaluator(SemanticIndexingPolicyEvaluationResult.Allowed("tenant-sensitive", "accepted_mutation_authorized"));
        materializer ??= new RecordingContentMaterializer(SemanticIndexingContentMaterializationResult.Available(
            [1, 2, 3],
            "text/plain",
            3,
            "inline",
            "small",
            "text"));
        port ??= new RecordingIndexingPort(new SemanticIndexingResult(
            SemanticIndexingStatus.Accepted,
            "memories_accepted",
            retryable: false,
            publishedEventId: "folders://tenant-a/published-a"));

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.RemoveAll<EventStoreSemanticIndexingBridgeStore>();
        builder.Services.RemoveAll<ISemanticIndexingBridgeReadModel>();
        builder.Services.RemoveAll<ISemanticIndexingBridgeWriter>();
        builder.Services.RemoveAll<ISemanticIndexingPolicyEvaluator>();
        builder.Services.RemoveAll<ISemanticIndexingContentMaterializer>();
        builder.Services.RemoveAll<ISemanticIndexingPort>();
        builder.Services.AddSingleton<IReadModelStore>(readModelStore);
        builder.Services.AddSingleton(bridge);
        builder.Services.AddSingleton<ISemanticIndexingBridgeReadModel>(bridge);
        builder.Services.AddSingleton<ISemanticIndexingBridgeWriter>(bridge);
        builder.Services.AddSingleton<ISemanticIndexingPolicyEvaluator>(policy);
        builder.Services.AddSingleton<ISemanticIndexingContentMaterializer>(materializer);
        builder.Services.AddSingleton<ISemanticIndexingPort>(port);

        WebApplication app = builder.Build();
        app.UseCloudEvents();
        app.MapSubscribeHandler();
        app.MapFoldersTenantEventWorkerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        return new WorkerSemanticIndexingHost(
            app,
            new HttpClient { BaseAddress = new Uri(app.Urls.First()) },
            bridge,
            policy,
            materializer,
            port);
    }

    private static EventStoreDomainEventEnvelope Envelope<TEvent>(string messageId, long sequenceNumber, TEvent @event)
        => new(
            messageId,
            "folder-a",
            "tenant-a",
            typeof(TEvent).FullName!,
            sequenceNumber,
            OccurredAt,
            $"correlation-{sequenceNumber}",
            "json",
            JsonSerializer.SerializeToUtf8Bytes(@event));

    private static WorkspaceFileMutationAccepted Mutation()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.FileMutated,
            "operation-a",
            "add",
            "PutFileInline",
            "tenant-sensitive",
            "path-digest-a",
            "sha256:a",
            128,
            "text/plain",
            "inline_decoded",
            128,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-a",
            "fingerprint-a",
            OccurredAt);

    private sealed record WorkerSemanticIndexingHost(
        WebApplication App,
        HttpClient Client,
        EventStoreSemanticIndexingBridgeStore Bridge,
        RecordingPolicyEvaluator Policy,
        RecordingContentMaterializer Materializer,
        RecordingIndexingPort Port) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class RecordingPolicyEvaluator : ISemanticIndexingPolicyEvaluator
    {
        private readonly SemanticIndexingPolicyEvaluationResult _result;

        public RecordingPolicyEvaluator(SemanticIndexingPolicyEvaluationResult result)
        {
            _result = result;
        }

        public List<SemanticIndexingBridgeEntry> Evaluated { get; } = [];

        public ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
            SemanticIndexingBridgeEntry entry,
            CancellationToken cancellationToken)
        {
            Evaluated.Add(entry);
            return ValueTask.FromResult(_result);
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

    private sealed class InMemoryReadModelStoreDouble : IReadModelStore
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private long _etagSequence;

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();

            return _entries.TryGetValue(Compose(storeName, key), out Entry? entry)
                ? Task.FromResult(new ReadModelEntry<TValue>((TValue)entry.Value, entry.ETag))
                : Task.FromResult(new ReadModelEntry<TValue>(null, null));
        }

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            cancellationToken.ThrowIfCancellationRequested();

            _entries[Compose(storeName, key)] = new Entry(value, NextETag());
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(etag);
            cancellationToken.ThrowIfCancellationRequested();

            string composite = Compose(storeName, key);
            bool exists = _entries.TryGetValue(composite, out Entry? current);
            bool matches = exists
                ? string.Equals(current!.ETag, etag, StringComparison.Ordinal)
                : etag.Length == 0;
            if (!matches)
            {
                return Task.FromResult(false);
            }

            _entries[composite] = new Entry(value, NextETag());
            return Task.FromResult(true);
        }

        private static string Compose(string storeName, string key) => $"{storeName}:{key}";

        private string NextETag() => Interlocked.Increment(ref _etagSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private sealed record Entry(object Value, string ETag);
    }
}
