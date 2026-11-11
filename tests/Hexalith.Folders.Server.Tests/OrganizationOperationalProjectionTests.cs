using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Server;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Pins organization ACL intake to real aggregate events and the normal shared SDK journal without inventing Folder views.</summary>
public sealed class OrganizationOperationalProjectionTests
{
    /// <summary>The same-domain organization prefix settles its actual SDK journal while producing no Folder status data.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualOrganizationAclPrefixCompletesSharedJournalWithoutFolderViews(bool explicitVersion)
    {
        ProjectionRequest prefix = ProduceAclPrefix(explicitVersion: explicitVersion);
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        var lease = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId, TestContext.Current.CancellationToken);
        List<SharedProjectionDelivery> deliveries = [];
        foreach (ProjectionEventDto source in prefix.Events)
        {
            ProjectionRequest request = prefix with { Events = [source] };
            long sequence = source.SequenceNumber;
            var delivery = new SharedProjectionDelivery("tenant-a", sequence,
                JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web),
                JsonSerializer.SerializeToUtf8Bytes(sequence), OwnerSequenceNumber: sequence);
            deliveries.Add(delivery);
            (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
        }
        await coordinator.CatchUpAsync(scope, (delivery, generation, token) => handler.FoldCatchUpAsync(delivery,
            generation, coordinator, token), TestContext.Current.CancellationToken);
        var status = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken);
        status.PendingDeliveryCount.ShouldBe(0);
        status.IsDegraded.ShouldBeFalse();
        foreach (var delivery in deliveries)
            (await coordinator.JournalAsync(scope, lease, delivery, TestContext.Current.CancellationToken))
                .ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "tenant-a")).ShouldBeNull();
        store.Snapshot<FolderLifecycleStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Lifecycle("tenant-a", "tenant-a")).ShouldBeNull();
    }
    /// <summary>Direct compute and rebuild planning acknowledge only validated non-Folder ACL input and create no views.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CurrentAclPrefixUsesEmptyBoundedPartitionsAndNoFolderViews(bool includeRevocation, bool explicitVersion)
    {
        var store = new OperationalStatusTestStore();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        var request = ProduceAclPrefix(includeRevocation, explicitVersion);
        Should.Throw<InvalidOperationException>(() => OperationalStatusEventReader.ReadEffective(request.Events[0]));
        (await handler.ProjectAsync(request, "dispatch", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await Should.ThrowAsync<InvalidOperationException>(() => handler.PrepareRebuildAsync(request, "rebuild", TestContext.Current.CancellationToken));
        var identity = new Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildIdentity("tenant-a", "folders",
            OperationalStatusKeys.ProjectionType, "rebuild", "catalog");
        var chunks = await handler.PreparePartitionChunksAsync(identity, request, TestContext.Current.CancellationToken);
        chunks.Count.ShouldBe(1);
        var partitions = new OperationalStatusPartitionCursor(chunks);
        List<ReadModelBatchOperation> operations = [];
        await foreach (var operation in handler.StreamFinalizedOperationsAsync(identity, partitions, TestContext.Current.CancellationToken)) operations.Add(operation);
        operations.ShouldBeEmpty();
        store.Count.ShouldBe(0);
    }

    /// <summary>Foreign scope, changed source metadata, unknown types and mixed namespaces retain source refusal.</summary>
    [Fact]
    public async Task ChangedOrUnrecognizedOrganizationInputsNeverBecomeNoOpCompletion()
    {
        var store = new OperationalStatusTestStore();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        var prefix = ProduceAclPrefix();
        var item = prefix.Events[0];
        ProjectionRequest single = prefix with { Events = [item] };
        foreach (var bad in new[]
        {
            single with { TenantId = "foreign" }, single with { AggregateId = "foreign" }, single with { Domain = "foreign" },
            single with { Events = [item with { CorrelationId = "foreign" }] }, single with { Events = [item with { SequenceNumber = 0 }] },
            single with { Events = [item with { GlobalPosition = 0 }] }, single with { Events = [item with { MetadataVersion = 2 }] },
            single with { Events = [item with { StoredPayloadVersion = 2 }] }, single with { Events = [item with { StoredPayloadVersion = 0 }] },
            single with { Events = [item with { SerializationFormat = "yaml" }] }, single with { Events = [item with { MessageId = "" }] },
            single with { Events = [item with { Timestamp = default }] }, single with { Events = [item with { Payload = "{}"u8.ToArray() }] },
            single with { Events = [item with { EventTypeName = "Unknown.OrganizationEvent" }] },
            single with { Events = [item, item with { SequenceNumber = 2, GlobalPosition = 2 }] },
            prefix with { Events = [item, prefix.Events[1] with { GlobalPosition = item.GlobalPosition }] },
            prefix with { Events = [item, OperationalStatusTestData.Projection([OperationalStatusTestData.Created()]).Events[0] with { SequenceNumber = 2 }] },
        })
        {
            (await handler.ProjectAsync(bad, "dispatch", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
            await Should.ThrowAsync<InvalidOperationException>(() => handler.PrepareRebuildAsync(bad, "rebuild", TestContext.Current.CancellationToken));
        }
        foreach ((string field, JsonNode value) in new (string, JsonNode)[]
        {
            ("PrincipalKind", JsonValue.Create(999)!), ("Action", JsonValue.Create("unknown_action")!),
            ("ManagedTenantId", JsonValue.Create("foreign")!), ("TaskId", JsonValue.Create("")!),
            ("IdempotencyFingerprint", JsonValue.Create("")!),
        })
        {
            var body = JsonNode.Parse(prefix.Events[1].Payload)!.AsObject();
            string property = body.Select(pair => pair.Key).Single(key => key.Equals(field, StringComparison.OrdinalIgnoreCase));
            body[property] = value;
            var invalid = prefix with { Events = [prefix.Events[1] with { Payload = JsonSerializer.SerializeToUtf8Bytes(body) }] };
            (await handler.ProjectAsync(invalid, "dispatch", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        }
        store.Count.ShouldBe(0);
        var delivery = new SharedProjectionDelivery("tenant-a", item.GlobalPosition + 1,
            JsonSerializer.SerializeToUtf8Bytes(single, JsonSerializerOptions.Web), "1"u8.ToArray(), OwnerSequenceNumber: item.SequenceNumber);
        await Should.ThrowAsync<InvalidOperationException>(() => handler.FoldCatchUpAsync(delivery, 0,
            new SharedProjectionEpochCoordinator(store, store), TestContext.Current.CancellationToken));
        delivery = delivery with { EnvelopePosition = item.GlobalPosition, OwnerSequenceNumber = 2 };
        await Should.ThrowAsync<InvalidOperationException>(() => handler.FoldCatchUpAsync(delivery, 0,
            new SharedProjectionEpochCoordinator(store, store), TestContext.Current.CancellationToken));
    }

    /// <summary>Cancellation remains observable before direct ACL completion or empty rebuild planning.</summary>
    [Fact]
    public async Task CanceledAclSourceDoesNotReturnCompletion()
    {
        var store = new OperationalStatusTestStore();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => handler.ProjectAsync(ProduceAclPrefix(), "dispatch", canceled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => handler.PrepareRebuildAsync(ProduceAclPrefix(), "rebuild", canceled.Token));
        store.Count.ShouldBe(0);
    }

    private static ProjectionRequest ProduceAclPrefix(bool includeRevocation = false, bool explicitVersion = false)
    {
        var command = new InitializeOrganizationAclBaseline("tenant-a", "tenant-a",
            [new(OrganizationAclOperationIntent.Grant, OrganizationAclPrincipalKind.User, "actor-a", "create_folder"),
             new(OrganizationAclOperationIntent.Grant, OrganizationAclPrincipalKind.User, "actor-a", "manage_folder_access")],
            "correlation-a", "task-a", "idempotency-a", null);
        var produced = OrganizationAggregate.Handle(OrganizationState.Empty, command);
        produced.Code.ShouldBe(OrganizationAclResultCode.Accepted);
        produced.Events.Count.ShouldBe(3);
        IReadOnlyList<IOrganizationAclEvent> retained = produced.Events;
        if (includeRevocation)
        {
            var revoke = new RevokeOrganizationAclPrincipal("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User, "actor-a", "create_folder",
                "correlation-r", "task-r", "idempotency-r", null);
            var revoked = OrganizationAggregate.Handle(OrganizationState.Empty.Apply(produced.Events), revoke);
            revoked.Code.ShouldBe(OrganizationAclResultCode.Accepted);
            retained = [.. produced.Events, .. revoked.Events];
        }
        int sequence = 0;
        retained.ShouldAllBe(item => EventPayloadVersionResolver.GetDeclaredVersion(item.GetType()) == 1);
        var wire = DomainServiceWireResult.FromDomainResult(DomainResult.Success([.. retained.Cast<IEventPayload>()]));
        wire.Events.ShouldAllBe(item => item.PayloadVersion == null);
        var events = wire.Events.Select(item => new ProjectionEventDto(item.EventTypeName, item.Payload, item.SerializationFormat, ++sequence,
            DateTimeOffset.UtcNow, retained[sequence - 1].CorrelationId, "message-" + sequence, "actor-a", sequence)
        { MetadataVersion = 1, StoredPayloadVersion = explicitVersion ? 1 : item.PayloadVersion }).ToArray();
        return new("tenant-a", "folders", "tenant-a", events);
    }

}
