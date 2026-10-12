using System.Text.Json;
using System.Net;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class EventStoreOrganizationEffectivePermissionsReadModelTests
{
    [Fact]
    public async Task IndependentSequenceReadUsesTheAggregateActorMethodAndParsesItsNumericHead()
    {
        CaptureSequenceHandler handler = new();
        using HttpClient client = new(handler);
        DaprOrganizationStreamSequenceReader reader = new(client);

        (await reader.GetCurrentSequenceAsync("tenant-a", "folder-a",
            TestContext.Current.CancellationToken)).ShouldBe(7);
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Path.ShouldContain("/v1.0/actors/AggregateActor/");
        handler.Path.ShouldContain("tenant-a%3Afolders%3Afolder-a");
        handler.Path.ShouldEndWith("/method/GetCurrentSequenceAsync");
    }

    [Fact]
    public async Task FolderActorInvocationUsesItsSuppliedHeadWithoutCallingTheLockedActor()
    {
        using HttpClient client = new(new ThrowOnSendHandler());
        DaprOrganizationStreamSequenceReader reader = new(client);
        using (FolderActorSequenceScope.Enter("tenant-a", "folder-a", 7))
        {
            (await reader.GetCurrentSequenceAsync("tenant-a", "folder-a",
                TestContext.Current.CancellationToken)).ShouldBe(7);
            FolderActorSequenceScope.TryGet("tenant-a", "folder-b", out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task DurableFolderOverridesInheritOnlyCurrentOrganizationGrants()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await organization.ApplyAsync("tenant-a", "tenant-a", "org-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        await organization.ApplyAsync("tenant-a", "tenant-a", "org-2", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "read_metadata", "corr", "task", "key", "fingerprint"), ct);
        heads.CurrentSequence = 2;
        FolderCreated created = new("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
            "key", "fingerprint", clock.UtcNow);
        await folder.ApplyAsync("tenant-a", "folder-a", "folder-1", 1, "digest-1", created, ct);
        heads.Heads["folder-a"] = 1;
        EffectivePermissionsReadModelRequest request = new("tenant-a", "folder-a",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");
        (await folder.GetAsync(request, ct)).Snapshot!.EvidenceRows.ShouldHaveSingleItem()
            .Action.ShouldBe("read_metadata");

        await folder.ApplyAsync("tenant-a", "folder-a", "folder-2", 2, "digest-2",
            new FolderAccessRevoked("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 1, clock.UtcNow), ct);
        heads.Heads["folder-a"] = 2;
        (await folder.GetAsync(request, ct)).Snapshot!.EvidenceRows.Last().Source
            .ShouldBe(EffectivePermissionEvidenceSource.FolderOverrideRevoke);
        (await folder.GetCurrentAsync("tenant-a", "folder-a", ct))!.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeFalse();

        // The retained stream position advances, but the tuple follows the same AccessSequence
        // precedence as FolderStateApply. A late lower-sequence grant cannot undo this revoke.
        await folder.ApplyAsync("tenant-a", "folder-a", "folder-3", 3, "digest-3",
            new FolderAccessGranted("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 0, clock.UtcNow), ct);
        heads.Heads["folder-a"] = 3;
        (await folder.GetCurrentAsync("tenant-a", "folder-a", ct))!.Overrides.ShouldHaveSingleItem()
            .IsGranted.ShouldBeFalse();
        (await folder.GetAsync(request, ct)).Snapshot!.EvidenceRows.Last().Source
            .ShouldBe(EffectivePermissionEvidenceSource.FolderOverrideRevoke);

        await Should.ThrowAsync<InvalidOperationException>(() => folder.ApplyAsync("tenant-a", "folder-a",
            "folder-2", 2, "different-bytes", null, ct));
        heads.Heads["folder-a"] = 4;
        EffectivePermissionsReadModelResult lagged = await folder.GetAsync(request, ct);
        lagged.Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        lagged.Freshness.Stale.ShouldBeTrue();
        lagged.Snapshot!.Freshness.Stale.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => folder.GetCurrentAsync("tenant-a", "folder-a", ct));
    }

    [Fact]
    public async Task FolderReplayRejectsSecondCreateAndFoldsAclAfterArchive()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new();
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads,
            new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, heads));
        CancellationToken ct = TestContext.Current.CancellationToken;
        FolderCreated created = new("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
            "key", "fingerprint", clock.UtcNow);
        await folder.ApplyAsync("tenant-a", "folder-a", "create-1", 1, "digest-1", created, ct);
        FolderArchived archivedEvent = new("tenant-a", "tenant-a", "folder-a", FolderArchiveReasonCode.CallerRequested,
            "user-a", "corr", "task", "key", "fingerprint", clock.UtcNow);
        await folder.ApplyAsync("tenant-a", "folder-a", "archive-2", 2, "digest-2", archivedEvent, ct);
        await folder.ApplyAsync("tenant-a", "folder-a", "grant-3", 3, "digest-3",
            new FolderAccessGranted("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 1,
                clock.UtcNow), ct);

        FolderPermissionProjection? archived = (await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct)).Value;
        archived.ShouldNotBeNull();
        archived.IsArchived.ShouldBeTrue();
        archived.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => folder.ApplyAsync("tenant-a", "folder-a",
            "create-4", 4, "digest-4", created with { DisplayName = "Duplicate" }, ct));
        await Should.ThrowAsync<InvalidOperationException>(() => folder.ApplyAsync("tenant-a", "folder-a",
            "archive-4", 4, "digest-4", archivedEvent, ct));
        (await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct)).Value!.LastSequence.ShouldBe(3);
    }

    [Fact]
    public async Task OldRedeliveryBeyondBoundedIdentityWindowDoesNotChangeEitherProjection()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await organization.ApplyAsync("tenant-a", "tenant-a", "org-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        FolderCreated created = new("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
            "key", "fingerprint", clock.UtcNow);
        await folder.ApplyAsync("tenant-a", "folder-a", "folder-1", 1, "digest-1", created, ct);
        for (long sequence = 2; sequence <= 258; sequence++)
        {
            await organization.ApplyNonPermissionEventAsync("tenant-a", "tenant-a",
                $"org-{sequence}", sequence, ct);
            await folder.ApplyAsync("tenant-a", "folder-a", $"folder-{sequence}", sequence,
                $"digest-{sequence}", null, ct);
        }

        await organization.ApplyNonPermissionEventAsync("tenant-a", "tenant-a", "old-redelivery", 1,
            ct, "different-digest");
        await folder.ApplyAsync("tenant-a", "folder-a", "old-redelivery", 1,
            "different-digest", created with { DisplayName = "Different" }, ct);
        (await store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:tenant-a", ct)).Value!.LastSequence.ShouldBe(258);
        FolderPermissionProjection? projected = (await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct)).Value;
        projected.ShouldNotBeNull();
        projected.LastSequence.ShouldBe(258);
        projected.IsCreated.ShouldBeTrue();
    }

    [Fact]
    public async Task UninitializedOrganizationProjectionChecksActorHeadBeforeReportingAbsence()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel model = new(store, clock, heads);
        EffectivePermissionsReadModelRequest request = new("tenant-a", "organization_baseline",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");
        CancellationToken ct = TestContext.Current.CancellationToken;

        EffectivePermissionsReadModelResult missing = await model.GetAsync(request, ct);
        missing.Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        missing.Snapshot!.Freshness.Stale.ShouldBeTrue();
        await model.ApplyNonPermissionEventAsync("tenant-a", "tenant-a", "rejection-1", 1, ct);
        heads.CurrentSequence = 2;
        EffectivePermissionsReadModelResult lagged = await model.GetAsync(request, ct);
        lagged.Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        lagged.Snapshot!.Freshness.Stale.ShouldBeTrue();
        heads.CurrentSequence = 1;
        (await model.GetAsync(request, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.NotFound);
    }

    [Fact]
    public async Task MissingFolderRowChecksActorHeadBeforeReportingAbsence()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new();
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads,
            new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, heads));
        EffectivePermissionsReadModelRequest request = new("tenant-a", "folder-a",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");
        CancellationToken ct = TestContext.Current.CancellationToken;

        (await folder.GetAsync(request, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.NotFound);
        (await folder.GetCurrentAsync("tenant-a", "folder-a", ct)).ShouldBeNull();
        heads.Heads["folder-a"] = 1;
        EffectivePermissionsReadModelResult lagged = await folder.GetAsync(request, ct);
        lagged.Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        lagged.Snapshot!.Freshness.Stale.ShouldBeTrue();
        lagged.Snapshot.EvidenceRows.ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => folder.GetCurrentAsync("tenant-a", "folder-a", ct));
    }

    [Fact]
    public async Task PermissionReadersTolerateTwoMinutesOfWorkerClockSkewButRejectLaterTimestamps()
    {
        MemoryReadModels store = new();
        TestClock workerClock = new();
        TestClock serverClock = new() { UtcNow = workerClock.UtcNow - TimeSpan.FromMinutes(1) };
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel workerOrganization = new(store, workerClock, heads);
        EventStoreOrganizationEffectivePermissionsReadModel serverOrganization = new(store, serverClock, heads);
        EventStoreFolderEffectivePermissionsReadModel workerFolder = new(store, workerClock, heads, workerOrganization);
        EventStoreFolderEffectivePermissionsReadModel serverFolder = new(store, serverClock, heads, serverOrganization);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await workerOrganization.ApplyAsync("tenant-a", "tenant-a", "baseline-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        await workerFolder.ApplyAsync("tenant-a", "folder-a", "folder-1", 1, "digest-1",
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
                FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
                "key", "fingerprint", workerClock.UtcNow), ct);
        EffectivePermissionsReadModelRequest organizationRequest = new("tenant-a", "organization_baseline",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");
        EffectivePermissionsReadModelRequest folderRequest = organizationRequest with { FolderId = "folder-a" };

        (await serverOrganization.GetAsync(organizationRequest, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        (await serverFolder.GetAsync(folderRequest, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);

        serverClock.UtcNow = workerClock.UtcNow - TimeSpan.FromMinutes(3);
        (await serverOrganization.GetAsync(organizationRequest, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Malformed);
        (await serverFolder.GetAsync(folderRequest, ct)).Status.ShouldBe(EffectivePermissionsReadModelStatus.Malformed);
    }

    [Fact]
    public async Task ProviderBindingPermissionRequiresCurrentOrganizationGrant()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        EffectivePermissionsFolderPermissionEvidenceProvider evidence = new(folder, clock);
        CancellationToken ct = TestContext.Current.CancellationToken;
        FolderPermissionEvidenceRequest request = new("tenant-a", "user-a", "actor_present",
            "configure_provider_binding", "organization_baseline", "corr", "task",
            FolderOperationPolicyClass.Mutation, AllowBoundedStale: false);

        await organization.ApplyAsync("tenant-a", "tenant-a", "org-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Denied);
        await organization.ApplyAsync("tenant-a", "tenant-a", "org-2", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "configure_provider_binding", "corr", "task", "key", "fingerprint"), ct);
        heads.CurrentSequence = 2;
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
        heads.CurrentSequence = 3;
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
    }
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

    private sealed class CaptureSequenceHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string Path { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsoluteUri ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("7"),
            });
        }
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

        public Dictionary<string, long> Heads { get; } = new(StringComparer.Ordinal);

        public bool Unavailable { get; set; }

        public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unavailable)
            {
                throw new InvalidOperationException("Actor unavailable.");
            }

            return Task.FromResult(Heads.TryGetValue(organizationId, out long head) ? head : CurrentSequence);
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
