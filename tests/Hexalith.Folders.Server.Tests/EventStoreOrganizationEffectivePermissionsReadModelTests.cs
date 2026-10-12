using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class EventStoreOrganizationEffectivePermissionsReadModelTests
{
    [Fact]
    public async Task OrganizationActorInvocationUsesItsSuppliedHeadWithoutCallingTheLockedActor()
    {
        using HttpClient client = new(new ThrowOnSendHandler());
        DaprOrganizationStreamSequenceReader reader = new(client);

        using (OrganizationActorSequenceScope.Enter("tenant-a", "tenant-a", 4))
        {
            (await reader.GetCurrentSequenceAsync("tenant-a", "tenant-a",
                TestContext.Current.CancellationToken)).ShouldBe(4);
            OrganizationActorSequenceScope.TryGet("tenant-b", "tenant-b", out _).ShouldBeFalse();
        }

        OrganizationActorSequenceScope.TryGet("tenant-a", "tenant-a", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task SequenceRequestTimeoutIsAnUnavailableAuthorizationSource()
    {
        using HttpClient client = new(new CancelOnSendHandler());
        DaprOrganizationStreamSequenceReader reader = new(client);

        await Should.ThrowAsync<TimeoutException>(() => reader.GetCurrentSequenceAsync(
            "tenant-a", "tenant-a", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectionBeforeInitializationAdvancesTheStreamWithoutCreatingPermission()
    {
        MemoryReadModels store = new();
        TestSequenceReader sequenceReader = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel model = new(store, new TestClock(), sequenceReader);
        EffectivePermissionsReadModelRequest request = new("tenant-a", "organization_baseline",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");

        await model.ApplyNonPermissionEventAsync("tenant-a", "tenant-a", "rejection-1", 1,
            TestContext.Current.CancellationToken);
        (await model.GetAsync(request, TestContext.Current.CancellationToken)).Status
            .ShouldBe(EffectivePermissionsReadModelStatus.NotFound);

        await model.ApplyAsync("tenant-a", "tenant-a", "baseline-2", 2,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr-a", "task-a", "key-a", "fingerprint-a"),
            TestContext.Current.CancellationToken);
        sequenceReader.CurrentSequence = 2;
        (await model.GetAsync(request, TestContext.Current.CancellationToken)).Status
            .ShouldBe(EffectivePermissionsReadModelStatus.Available);
    }

    [Fact]
    public async Task PublishedGrantAndRevocationAreSharedAcrossHostsAndSequenceGapsFailClosed()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader sequenceReader = new();
        EventStoreOrganizationEffectivePermissionsReadModel worker = new(store, clock, sequenceReader);
        EventStoreOrganizationEffectivePermissionsReadModel server = new(store, clock, sequenceReader);
        EffectivePermissionsReadModelRequest request = new("tenant-a", "organization_baseline",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");

        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(EffectivePermissionsReadModelStatus.NotFound);
        await worker.ApplyAsync("tenant-a", "tenant-a", "event-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr-a", "task-a", "key-a", "fingerprint-a"), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(() => worker.ApplyAsync("tenant-a", "tenant-a",
            "event-3", 3, new OrganizationAclPrincipalGranted("tenant-a", "tenant-a",
                OrganizationAclPrincipalKind.User, "user-a", "create_folder", "corr-a", "task-a", "key-a", "fingerprint-a"), TestContext.Current.CancellationToken));
        sequenceReader.CurrentSequence = 1;
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Snapshot!.EvidenceRows.ShouldBeEmpty();

        OrganizationAclPrincipalGranted grant = new("tenant-a", "tenant-a",
            OrganizationAclPrincipalKind.User, "user-a", "create_folder", "corr-a", "task-a", "key-a", "fingerprint-a");
        await worker.ApplyAsync("tenant-a", "tenant-a", "event-2", 2, grant, TestContext.Current.CancellationToken);
        await worker.ApplyAsync("tenant-a", "tenant-a", "event-2", 2, grant, TestContext.Current.CancellationToken);
        sequenceReader.CurrentSequence = 2;
        EffectivePermissionsReadModelResult afterGrant = await server.GetAsync(request, TestContext.Current.CancellationToken);
        afterGrant.Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        afterGrant.Snapshot!.EvidenceRows.ShouldHaveSingleItem().Action.ShouldBe("create_folder");

        await worker.ApplyAsync("tenant-a", "tenant-a", "event-3", 3,
            new OrganizationAclPrincipalRevoked("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "create_folder", "corr-a", "task-a", "key-b", "fingerprint-b"), TestContext.Current.CancellationToken);
        sequenceReader.CurrentSequence = 3;
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Snapshot!.EvidenceRows.ShouldBeEmpty();

        clock.UtcNow += TimeSpan.FromMinutes(3);
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        sequenceReader.CurrentSequence = 4;
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        await worker.ApplyNonPermissionEventAsync("tenant-a", "tenant-a", "event-4", 4, TestContext.Current.CancellationToken);
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        sequenceReader.Unavailable = true;
        (await server.GetAsync(request, TestContext.Current.CancellationToken)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
    }

    private sealed class TestClock : IUtcClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ThrowOnSendHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The sequence reader called the locked actor.");
    }

    private sealed class CancelOnSendHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new TaskCanceledException("The sidecar did not reply.");
    }

    private sealed class TestSequenceReader : IOrganizationStreamSequenceReader
    {
        public long CurrentSequence { get; set; }

        public bool Unavailable { get; set; }

        public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unavailable)
            {
                throw new InvalidOperationException("Actor unavailable.");
            }

            return Task.FromResult(CurrentSequence);
        }
    }

    private sealed class MemoryReadModels : IReadModelStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, (byte[] Json, int Version)> _values = new(StringComparer.Ordinal);

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
            CancellationToken cancellationToken = default) where TValue : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                return Task.FromResult(_values.TryGetValue(key, out var entry)
                    ? new ReadModelEntry<TValue>(JsonSerializer.Deserialize<TValue>(entry.Json), entry.Version.ToString())
                    : new ReadModelEntry<TValue>(null, null));
            }
        }

        public Task SaveAsync<TValue>(string storeName, string key, TValue value,
            CancellationToken cancellationToken = default) where TValue : class
            => throw new NotSupportedException();

        public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
            CancellationToken cancellationToken = default) where TValue : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                int current = _values.TryGetValue(key, out var entry) ? entry.Version : 0;
                if (etag != (current == 0 ? string.Empty : current.ToString()))
                {
                    return Task.FromResult(false);
                }

                _values[key] = (JsonSerializer.SerializeToUtf8Bytes(value), current + 1);
                return Task.FromResult(true);
            }
        }
    }
}
