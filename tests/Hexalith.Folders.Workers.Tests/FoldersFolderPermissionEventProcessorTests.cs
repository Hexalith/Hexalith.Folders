using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Workers;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class FoldersFolderPermissionEventProcessorTests
{
    [Fact]
    public async Task ProviderBindingRejectionDoesNotCreateFolderProjection()
    {
        WorkerRouteReadModelStore store = new();
        FixedUtcClock clock = new(DateTimeOffset.UtcNow);
        CurrentHead heads = new();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        FoldersFolderPermissionEventProcessor processor = new(
            new EventStoreFolderEffectivePermissionsReadModel(store, clock, heads, organization));
        EventStoreDomainEventEnvelope rejected = new("message-a", ProviderBindingActorId.Create("binding-a"), "tenant-a",
            "Hexalith.Folders.Server.FolderCommandRejected", 1, clock.UtcNow, "corr", "json",
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                CommandType = "Hexalith.Folders.Commands.ConfigureProviderBinding",
            }))
        { Domain = "folders", MetadataVersion = 1 };

        await processor.ProcessAsync(rejected, TestContext.Current.CancellationToken);
        (await store.GetAsync<FolderPermissionProjection>("statestore",
            $"projection:folders:folder-permissions:tenant-a:{rejected.AggregateId}",
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(rejected with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { CommandType = "unknown_command_type" }),
        }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(rejected with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { CommandType = "Hexalith.Folders.Commands.CreateFolder" }),
        }, TestContext.Current.CancellationToken));
        (await store.GetAsync<FolderPermissionProjection>("statestore",
            $"projection:folders:folder-permissions:tenant-a:{rejected.AggregateId}",
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(rejected with
        {
            AggregateId = "folder-a",
        }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RetainedFolderEventsAdvanceDurableAclAndConflictingReplayFailsClosed()
    {
        WorkerRouteReadModelStore store = new();
        FixedUtcClock clock = new(DateTimeOffset.UtcNow);
        CurrentHead heads = new();
        EventStoreOrganizationEffectivePermissionsReadModel organization = new(store, clock, heads);
        EventStoreFolderEffectivePermissionsReadModel view = new(store, clock, heads, organization);
        FoldersFolderPermissionEventProcessor processor = new(view);
        CancellationToken ct = TestContext.Current.CancellationToken;

        FolderCreated created = new("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a", "corr", "task",
            "key", "fingerprint", clock.UtcNow);
        EventStoreDomainEventEnvelope first = Envelope("message-1", 1, created);
        FolderCreated? direct = JsonSerializer.Deserialize<FolderCreated>(first.Payload,
            EventStorePayloadSerialization.Options);
        direct.ShouldNotBeNull();
        direct.ManagedTenantId.ShouldBe("tenant-a");
        direct.OrganizationId.ShouldBe("tenant-a");
        direct.FolderId.ShouldBe("folder-a");
        await processor.ProcessAsync(first, ct);
        await processor.ProcessAsync(first, ct);

        FolderAccessGranted grant = new("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
            "user-a", "read_metadata", "user-a", "corr", "task", "key", "fingerprint", 1, clock.UtcNow);
        await processor.ProcessAsync(Envelope("message-2", 2, grant), ct);
        FolderPermissionProjection? projection = await view.GetCurrentAsync("tenant-a", "folder-a", ct);
        projection.ShouldNotBeNull();
        projection.LastSequence.ShouldBe(2);
        projection.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeTrue();

        var beforeGap = await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct);
        EventStoreDomainEventEnvelope gap = Envelope("message-gap", 4, grant);
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(gap, ct));
        var rejected = await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct);
        rejected.ETag.ShouldBe(beforeGap.ETag);
        rejected.Value!.LastSequence.ShouldBe(2);
        rejected.Value.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeTrue();
        FolderAccessRevoked revoke = new("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
            "user-a", "read_metadata", "user-a", "corr", "task", "revoke-key", "revoke-fingerprint", 2, clock.UtcNow);
        await processor.ProcessAsync(Envelope("message-3", 3, revoke), ct);
        await processor.ProcessAsync(gap, ct); // Retry the identical rejected envelope after the missing range.
        var recovered = await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", ct);
        recovered.Value!.LastSequence.ShouldBe(4);
        recovered.Value.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeFalse();

        EventStoreDomainEventEnvelope conflict = first with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(created with { DisplayName = "Changed" },
                EventStorePayloadSerialization.Options),
        };
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(conflict, ct));
        await Should.ThrowAsync<EventPayloadEvolutionException>(() => processor.ProcessAsync(
            Envelope("message-3", 3, grant) with { PayloadVersion = 2 }, ct));
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(
            Envelope("message-3", 3, grant with { FolderId = "foreign" }), ct));
    }

    private static EventStoreDomainEventEnvelope Envelope<T>(string messageId, long sequence, T payload)
        => new(messageId, "folder-a", "tenant-a", typeof(T).FullName!, sequence,
            DateTimeOffset.UtcNow, "corr", "json", JsonSerializer.SerializeToUtf8Bytes(payload,
                EventStorePayloadSerialization.Options))
        {
            Domain = "folders",
            MetadataVersion = 1,
        };

    private sealed class CurrentHead : IOrganizationStreamSequenceReader
    {
        public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
            CancellationToken cancellationToken = default) => Task.FromResult(2L);
    }
}
