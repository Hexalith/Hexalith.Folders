using System.Text.Json;
using System.Net;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Configuration;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class EventStoreOrganizationEffectivePermissionsReadModelTests
{
    [Theory]
    [InlineData(nameof(FolderPermissionProjection.IsCreated))]
    [InlineData(nameof(FolderPermissionProjection.IsArchived))]
    public async Task SerializedFolderRowWithoutLifecycleEvidenceCannotAuthorize(string missingProperty)
    {
        TestClock clock = new();
        FolderPermissionProjection archived = new("tenant-a", "tenant-a", "folder-a", 3,
            true, true, clock.UtcNow,
            [new(FolderAccessPrincipalKind.User, "user-a", "read_metadata", true, 1, clock.UtcNow)], []);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(archived);
        FolderPermissionProjection valid = JsonSerializer.Deserialize<FolderPermissionProjection>(bytes)!;
        valid.IsArchived.ShouldBeTrue();
        JsonSerializer.SerializeToUtf8Bytes(valid).ShouldBe(bytes);

        Dictionary<string, JsonElement> incomplete = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bytes)!;
        incomplete.Remove(missingProperty).ShouldBeTrue();
        byte[] incompleteBytes = JsonSerializer.SerializeToUtf8Bytes(incomplete);
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<FolderPermissionProjection>(incompleteBytes));

        MemoryReadModels store = new();
        await store.TrySaveAsync("statestore", "projection:folders:folder-permissions:tenant-a:folder-a",
            incomplete, string.Empty, TestContext.Current.CancellationToken);
        TestSequenceReader heads = new() { CurrentSequence = 3 };
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads,
            new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, heads));
        EffectivePermissionsReadModelResult result = await folder.GetAsync(new("tenant-a", "folder-a",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes"),
            TestContext.Current.CancellationToken);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
        result.Snapshot.ShouldBeNull();
        (await new EffectivePermissionsFolderPermissionEvidenceProvider(folder, clock).GetEvidenceAsync(
            new("tenant-a", "user-a", "user-a", "read_metadata", "folder-a", "corr", null,
                FolderOperationPolicyClass.StrictRead, false), TestContext.Current.CancellationToken))
            .Status.ShouldBe(FolderPermissionEvidenceStatus.Unavailable);
    }

    [Theory]
    [InlineData("configured-sidecar-token")]
    [InlineData("")]
    public async Task IndependentSequenceReadUsesOnlyTheConfiguredDaprApiToken(string token)
    {
        CaptureSequenceHandler handler = new();
        using HttpClient client = new(handler);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DAPR_API_TOKEN"] = token }).Build();
        DaprOrganizationStreamSequenceReader reader = new(client, configuration);
        (await reader.GetCurrentSequenceAsync("tenant-a", "folder-a", TestContext.Current.CancellationToken))
            .ShouldBe(7);
        handler.ApiToken.ShouldBe(string.IsNullOrEmpty(token) ? null : token);
    }

    [Theory]
    [InlineData("create_repository_backed_folder", false)]
    [InlineData("bind_repository", false)]
    [InlineData("configure_branch_ref_policy", false)]
    [InlineData("create_repository_backed_folder", true)]
    [InlineData("bind_repository", true)]
    [InlineData("configure_branch_ref_policy", true)]
    public async Task AcceptedPersistedAclEventsSupplyPublishedExactActionAtItsScope(string action, bool folderScope)
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        EffectivePermissionsFolderPermissionEvidenceProvider evidence = new(folder, clock);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await organization.ApplyAsync("tenant-a", "tenant-a", "baseline", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        FolderPermissionEvidenceRequest request = new("tenant-a", "user-a", "user-a", action,
            "folder-a", "corr", "task", FolderOperationPolicyClass.Mutation, false);
        FolderCreated created = new("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task", "key", "fingerprint", clock.UtcNow);
        await folder.ApplyAsync("tenant-a", "folder-a", "created", 1, "digest-create", created, ct);
        heads.Heads["folder-a"] = 1;
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Denied);
        if (folderScope)
        {
            GrantFolderAccess command = new("tenant-a", "tenant-a", "folder-a",
                [new(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "user-a", action)],
                "user-a", "corr", "task", "key-grant", null);
            FolderResult accepted = FolderAggregate.Handle(FolderState.Empty.Apply([created],
                FolderStreamName.Create("tenant-a", "folder-a")), command);
            accepted.Code.ShouldBe(FolderResultCode.Accepted);
            FolderAccessGranted grant = accepted.Events.ShouldHaveSingleItem().ShouldBeOfType<FolderAccessGranted>();
            await folder.ApplyAsync("tenant-a", "folder-a", "grant", 2, "digest-grant", grant, ct);
            heads.Heads["folder-a"] = 2;
            (await store.GetAsync<FolderPermissionProjection>("statestore",
                "projection:folders:folder-permissions:tenant-a:folder-a", ct)).Value!.Overrides
                .ShouldHaveSingleItem().Action.ShouldBe(action);
        }
        else
        {
            GrantOrganizationAclPrincipal command = new("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", action, "corr", "task", "key-grant", null);
            OrganizationAclResult accepted = OrganizationAggregate.Handle(OrganizationState.Empty.Apply([
                new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")]), command);
            accepted.Code.ShouldBe(OrganizationAclResultCode.Accepted);
            OrganizationAclPrincipalGranted grant = accepted.Events.ShouldHaveSingleItem().ShouldBeOfType<OrganizationAclPrincipalGranted>();
            await organization.ApplyAsync("tenant-a", "tenant-a", "grant", 2, grant, ct);
            heads.CurrentSequence = 2;
            (await store.GetAsync<OrganizationPermissionProjection>("statestore",
                "projection:folders:organization-permissions:tenant-a", ct)).Value!.Grants
                .ShouldHaveSingleItem().Action.ShouldBe(action);
        }
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
        (await evidence.GetEvidenceAsync(request with { OperationScope = "organization_baseline" }, ct))
            .Status.ShouldBe(FolderPermissionEvidenceStatus.NotFoundSafe);
        heads.Heads["folder-missing"] = 0;
        (await evidence.GetEvidenceAsync(request with { OperationScope = "folder-missing" }, ct))
            .Status.ShouldBe(FolderPermissionEvidenceStatus.NotFoundSafe);
        heads.Heads["folder-missing"] = 1;
        (await evidence.GetEvidenceAsync(request with { OperationScope = "folder-missing" }, ct))
            .Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
        heads.Heads["folder-a"] = 3;
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
        heads.Heads["folder-a"] = folderScope ? 2 : 1;
        heads.Heads["tenant-b"] = 0;
        heads.Heads["folder-b"] = 0;
        (await evidence.GetEvidenceAsync(request with { ManagedTenantId = "tenant-b", OperationScope = "folder-b" }, ct))
            .Status.ShouldBe(FolderPermissionEvidenceStatus.NotFoundSafe);

        if (folderScope)
        {
            await folder.ApplyAsync("tenant-a", "folder-a", "revoke", 3, "digest-revoke",
                new FolderAccessRevoked("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                    "user-a", action, "user-a", "corr", "task", "key-revoke", "fingerprint-revoke", 2, clock.UtcNow), ct);
            heads.Heads["folder-a"] = 3;
            (await store.GetAsync<FolderPermissionProjection>("statestore",
                "projection:folders:folder-permissions:tenant-a:folder-a", ct)).Value!.Overrides
                .ShouldHaveSingleItem().IsGranted.ShouldBeFalse();
        }
        else
        {
            await organization.ApplyAsync("tenant-a", "tenant-a", "revoke", 3,
                new OrganizationAclPrincipalRevoked("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                    "user-a", action, "corr", "task", "key-revoke", "fingerprint-revoke"), ct);
            heads.CurrentSequence = 3;
            (await store.GetAsync<OrganizationPermissionProjection>("statestore",
                "projection:folders:organization-permissions:tenant-a", ct)).Value!.Grants.ShouldBeEmpty();
        }
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Denied);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentFolderOverrideDoesNotRequireAnInitializedOrganizationBaseline(bool hasRejection)
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        CancellationToken ct = TestContext.Current.CancellationToken;
        if (hasRejection)
        {
            await organization.ApplyNonPermissionEventAsync("tenant-a", "tenant-a", "rejection-1", 1, ct);
            heads.CurrentSequence = 1;
        }

        await folder.ApplyAsync("tenant-a", "folder-a", "create-1", 1, "digest-1",
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
                FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
                "key", "fingerprint", clock.UtcNow), ct);
        await folder.ApplyAsync("tenant-a", "folder-a", "grant-2", 2, "digest-2",
            new FolderAccessGranted("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 1, clock.UtcNow), ct);
        heads.Heads["folder-a"] = 2;
        EffectivePermissionsReadModelRequest request = new("tenant-a", "folder-a",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes");

        EffectivePermissionsReadModelResult result = await folder.GetAsync(request, ct);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Available);
        result.Snapshot!.EvidenceRows.ShouldHaveSingleItem().Source
            .ShouldBe(EffectivePermissionEvidenceSource.FolderOverrideGrant);
        result.Snapshot.RevocationFreshnessEstablished.ShouldBeTrue();
        EffectivePermissionsFolderPermissionEvidenceProvider evidence = new(folder, clock);
        (await evidence.GetEvidenceAsync(new FolderPermissionEvidenceRequest("tenant-a", "user-a", "actor_present",
            "read_metadata", "folder-a", "corr", "task", FolderOperationPolicyClass.StrictRead,
            AllowBoundedStale: false), ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Allowed);
    }

    [Fact]
    public async Task LaggedBaselinePreservesFolderEvidenceForBoundedReadsAndStrictReadsRemainStale()
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel folder = new(store, clock, heads, organization);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await organization.ApplyAsync("tenant-a", "tenant-a", "baseline-1", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint"), ct);
        await organization.ApplyAsync("tenant-a", "tenant-a", "grant-2", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "read_metadata", "corr", "task", "key", "fingerprint"), ct);
        heads.CurrentSequence = 3;
        await folder.ApplyAsync("tenant-a", "folder-a", "create-1", 1, "digest-1",
            new FolderCreated("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
                FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
                "key", "fingerprint", clock.UtcNow), ct);
        heads.Heads["folder-a"] = 1;
        EffectivePermissionsReadModelResult result = await folder.GetAsync(new("tenant-a", "folder-a",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes"), ct);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Stale);
        result.Snapshot!.LifecycleState.ShouldBe(EffectivePermissionsFolderLifecycleState.Active);
        result.Snapshot.EvidenceRows.ShouldHaveSingleItem().Action.ShouldBe("read_metadata");
        result.Snapshot.Freshness.Stale.ShouldBeTrue();
        result.Snapshot.Freshness.ReasonCode.ShouldBe("organization_projection_lagging");

        EffectivePermissionsFolderPermissionEvidenceProvider evidence = new(folder, clock);
        FolderPermissionEvidenceRequest request = new("tenant-a", "user-a", "actor_present", "read_metadata",
            "folder-a", "corr", "task", FolderOperationPolicyClass.StrictRead, AllowBoundedStale: false);
        (await evidence.GetEvidenceAsync(request, ct)).Status.ShouldBe(FolderPermissionEvidenceStatus.Stale);
        (await evidence.GetEvidenceAsync(request with { AllowBoundedStale = true }, ct)).FreshnessClass
            .ShouldBe("bounded_stale");

        await folder.ApplyAsync("tenant-a", "folder-a", "revoke-2", 2, "digest-2",
            new FolderAccessRevoked("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 1, clock.UtcNow), ct);
        heads.Heads["folder-a"] = 2;
        (await evidence.GetEvidenceAsync(request with { AllowBoundedStale = true }, ct)).Status
            .ShouldBe(FolderPermissionEvidenceStatus.Denied);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NullOrganizationMessageMapFailsClosedWithoutAdvancingTheStoredProjection(long sequence)
    {
        MemoryReadModels store = new();
        TestClock clock = new();
        TestSequenceReader heads = new() { CurrentSequence = 1 };
        OrganizationPermissionProjection corrupted = new("tenant-a", "tenant-a", 1, true, clock.UtcNow,
            [], null!, [new FolderProjectionMessage(1, "event-1", "digest-1")]);
        const string key = "projection:folders:organization-permissions:tenant-a";
        CancellationToken ct = TestContext.Current.CancellationToken;
        (await store.TrySaveAsync("statestore", key, corrupted, string.Empty, ct)).ShouldBeTrue();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);

        (await organization.GetAsync(new("tenant-a", "organization_baseline",
            [EffectivePermissionPrincipal.User("user-a")], null, null, "read_your_writes"), ct)).Status
            .ShouldBe(EffectivePermissionsReadModelStatus.Malformed);
        await Should.ThrowAsync<InvalidOperationException>(() => organization.ApplyNonPermissionEventAsync(
            "tenant-a", "tenant-a", $"event-{sequence}", sequence, ct, $"digest-{sequence}"));
        ReadModelEntry<OrganizationPermissionProjection> persisted = await store.GetAsync<OrganizationPermissionProjection>(
            "statestore", key, ct);
        persisted.ETag.ShouldBe("1");
        persisted.Value!.LastSequence.ShouldBe(1);
        persisted.Value.ProcessedMessages.ShouldBeNull();
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

        public string? ApiToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsoluteUri ?? string.Empty;
            ApiToken = request.Headers.TryGetValues("dapr-api-token", out IEnumerable<string>? values)
                ? values.Single() : null;
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
