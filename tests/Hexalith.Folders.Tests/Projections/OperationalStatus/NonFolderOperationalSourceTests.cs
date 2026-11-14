using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Exercises all legitimate non-Folder owners on the domain-wide production projection.</summary>
public sealed class NonFolderOperationalSourceTests
{
    [Fact]
    public async Task ProviderAndOrganizationPrefixesPassLiveBoundedRebuildAndCatchUpWithoutFolderRowsAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        OperationalStatusClock clock = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store, clock));
        var provider = Provider();
        string actor = ProviderBindingActorId.Create(provider.ProviderBindingRef);
        object[] organization = [Reject("Hexalith.Folders.Commands.GrantOrganizationAclPrincipal"),
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "correlation-a", "task-a", "baseline", "baseline-fingerprint"),
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User, "reader", "read_metadata",
                "correlation-a", "task-a", "grant", "grant-fingerprint"), Reject("Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal")];
        object[] binding = [Reject("Hexalith.Folders.Commands.ConfigureProviderBinding"), provider, Reject("Hexalith.Folders.Commands.ConfigureProviderBinding")];
        var owners = new[] { (Id: "tenant-a", Events: Sources(organization, 100)), (Id: actor, Events: Sources(binding, 200)) };
        foreach (var owner in owners)
        {
            foreach (var item in owner.Events)
                (await handler.ProjectAsync(new("tenant-a", "folders", owner.Id, [item.Source!]), "live", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            var suffix = owner.Events[^1].Source! with { SequenceNumber = owner.Events.Length + 1, GlobalPosition = owner.Events[^1].Source!.GlobalPosition + 1, MessageId = "catchup-" + owner.Id };
            var request = new ProjectionRequest("tenant-a", "folders", owner.Id, [suffix]);
            var delivery = new SharedProjectionDelivery(owner.Id, suffix.GlobalPosition, JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web),
                JsonSerializer.SerializeToUtf8Bytes(suffix.GlobalPosition), OwnerSequenceNumber: suffix.SequenceNumber);
            (await handler.FoldCatchUpAsync(delivery, 0, new(store, store, clock), token)).ShouldBeEmpty();
            await Should.ThrowAsync<InvalidOperationException>(() => handler.FoldCatchUpAsync(delivery with { EnvelopePosition = suffix.GlobalPosition + 1 }, 0, new(store, store, clock), token));
        }
        store.Count.ShouldBe(0);
        var folder = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(OperationalStatusTestData.Now));
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "all-owner-kinds", folder, folder.Length,
            token, clock: clock, additionalOwners: owners.OrderBy(owner => owner.Id, StringComparer.Ordinal)
                .Select(owner => (owner.Id, (IReadOnlyList<OperationalStatusEvent>)owner.Events)).ToArray())).IsSuccess.ShouldBeTrue();
        foreach (var owner in owners)
        {
            store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", owner.Id)).ShouldBeNull();
            store.Snapshot<Hexalith.Folders.Queries.Folders.FolderLifecycleStatusReadModelSnapshot>("statestore", OperationalStatusKeys.Lifecycle("tenant-a", owner.Id)).ShouldBeNull();
        }
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(folder.Length);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("wrong-owner")]
    [InlineData("invalid-version")]
    [InlineData("gap")]
    [InlineData("accepted-rejection")]
    [InlineData("different-binding")]
    [InlineData("acl-before-baseline")]
    public async Task InvalidNonFolderPrefixCannotPublishOrPrepareOperationsAsync(string corruption)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        OperationalStatusTestStore store = new();
        var handler = new OperationalStatusProjectionHandler(new(store, store, store, new OperationalStatusClock()));
        var provider = Provider();
        string actor = ProviderBindingActorId.Create(provider.ProviderBindingRef);
        var events = Sources([provider], 200).Select(item => item.Source!).ToArray();
        var request = new ProjectionRequest("tenant-a", "folders", actor, events);
        request = corruption switch
        {
            "foreign-tenant" => request with { TenantId = "tenant-b" },
            "wrong-owner" => request with { AggregateId = "tenant-a" },
            "invalid-version" => request with { Events = [events[0] with { StoredPayloadVersion = 2 }] },
            "gap" => request with { Events = [events[0] with { SequenceNumber = 2 }] },
            "accepted-rejection" => request with { Events = [events[0] with { EventTypeName = typeof(FolderCommandRejected).FullName!, Payload = JsonSerializer.SerializeToUtf8Bytes(new { Code = "Accepted", CommandType = "Hexalith.Folders.Commands.ConfigureProviderBinding" }) }] },
            "acl-before-baseline" => request with { AggregateId = "tenant-a", Events = Sources([new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User, "reader", "read_metadata", "correlation-a", "task-a", "grant", "fingerprint")], 100).Select(item => item.Source!).ToArray() },
            _ => request with { AggregateId = ProviderBindingActorId.Create("other-binding") },
        };
        if (corruption != "gap")
            (await handler.ProjectAsync(request, "invalid", token)).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        await Should.ThrowAsync<InvalidOperationException>(() => handler.PreparePartitionChunksAsync(new("tenant-a", "folders", OperationalStatusKeys.ProjectionType, "invalid", "catalog"), request, token));
        store.Count.ShouldBe(0);
    }

    private static FolderCommandRejected Reject(string command)
        => FolderCommandRejected.Create("ValidationFailed", command, "tenant-a", "tenant-a", null, "principal-a", "correlation-a", "task-a", "rejection-key");

    private static ProviderBindingConfigured Provider()
    {
        var command = new ConfigureProviderBinding("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User, "principal-a", "binding-a", "forgejo",
            "credential-a", OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a", "provider-key", OperationalStatusTestData.Now, null);
        var validation = OrganizationProviderBindingCommandValidator.Validate(command);
        validation.IsAccepted.ShouldBeTrue();
        return new("tenant-a", "tenant-a", "binding-a", "forgejo", "credential-a", command.NamingPolicy, command.BranchPolicy,
            "correlation-a", "task-a", "provider-key", validation.IdempotencyFingerprint, "configured", OperationalStatusTestData.Now);
    }

    private static OperationalStatusEvent[] Sources(object[] values, long globalOffset)
        => values.Select((value, ordinal) =>
        {
            var source = new ProjectionEventDto(value.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(value, value.GetType()), "json", ordinal + 1,
                OperationalStatusTestData.Now, "correlation-a", "message-" + (globalOffset + ordinal + 1))
            { MetadataVersion = 1, StoredPayloadVersion = 1, GlobalPosition = globalOffset + ordinal + 1 };
            return new OperationalStatusEvent(source.SequenceNumber, source.MessageId!, source.Timestamp, "source-identity-owned-by-sdk", null) { Source = source };
        }).ToArray();
}
