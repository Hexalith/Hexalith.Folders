using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Workers;

using NSubstitute;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class OrganizationDeliveryRegressionTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("payload-version")]
    [InlineData("unsupported-version")]
    [InlineData("contract-type")]
    [InlineData("format")]
    [InlineData("payload")]
    [InlineData("message")]
    [InlineData("sequence")]
    public async Task OrganizationMetadataFailureDoesNotAdvancePersistedAuthority(string failure)
    {
        WorkerRouteReadModelStore store = new();
        var clock = new FixedUtcClock(DateTimeOffset.UtcNow);
        var heads = Substitute.For<IOrganizationStreamSequenceReader>();
        var processor = new FoldersOrganizationAclEventProcessor(new(store, clock, heads));
        var baseline = new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint");
        var envelope = new EventStoreDomainEventEnvelope("message", "tenant-a", "tenant-a", baseline.GetType().FullName!,
            1, clock.UtcNow, "corr", "json", JsonSerializer.SerializeToUtf8Bytes(baseline))
        { Domain = "folders", MetadataVersion = 1 };
        envelope = failure switch
        {
            "version" => envelope with { MetadataVersion = 2 },
            "payload-version" => envelope with { PayloadVersion = 0 },
            "unsupported-version" => envelope with { PayloadVersion = 2 },
            "contract-type" => envelope with { EventContractType = "unknown" },
            "format" => envelope with { SerializationFormat = "binary" },
            "payload" => envelope with { Payload = [] },
            "message" => envelope with { MessageId = "" },
            _ => envelope with { SequenceNumber = 0 },
        };
        (await Record.ExceptionAsync(() => processor.ProcessAsync(envelope, TestContext.Current.CancellationToken)))
            .ShouldNotBeNull();
        (await store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task OrganizationGapLeavesAuthorityUnchangedAndOrderedReplayRemovesTheRevokedGrant()
    {
        WorkerRouteReadModelStore store = new();
        var clock = new FixedUtcClock(DateTimeOffset.UtcNow);
        var processor = new FoldersOrganizationAclEventProcessor(new(store, clock, Substitute.For<IOrganizationStreamSequenceReader>()));
        EventStoreDomainEventEnvelope Envelope(string message, long sequence, object payload)
            => new(message, "tenant-a", "tenant-a", payload.GetType().FullName!, sequence, clock.UtcNow, "corr", "json",
                JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()))
            { Domain = "folders", MetadataVersion = 1 };
        CancellationToken ct = TestContext.Current.CancellationToken;
        await processor.ProcessAsync(Envelope("baseline", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")), ct);
        await processor.ProcessAsync(Envelope("grant", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "create_folder", "corr", "task", "grant-key", "grant-fingerprint")), ct);
        const string key = "projection:folders:organization-permissions:tenant-a";
        var beforeGap = await store.GetAsync<OrganizationPermissionProjection>("statestore", key, ct);
        EventStoreDomainEventEnvelope rejected = new("rejected", "tenant-a", "tenant-a",
            "Hexalith.Folders.Server.FolderCommandRejected", 4, clock.UtcNow, "corr", "json",
            JsonSerializer.SerializeToUtf8Bytes(new { CommandType = FoldersServerModule.GrantOrganizationAclPrincipalCommandType }))
        { Domain = "folders", MetadataVersion = 1 };
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(rejected, ct));
        var unchanged = await store.GetAsync<OrganizationPermissionProjection>("statestore", key, ct);
        unchanged.ETag.ShouldBe(beforeGap.ETag);
        unchanged.Value!.LastSequence.ShouldBe(2);
        unchanged.Value.Grants.ShouldHaveSingleItem().Action.ShouldBe("create_folder");
        await processor.ProcessAsync(Envelope("revoke", 3,
            new OrganizationAclPrincipalRevoked("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "create_folder", "corr", "task", "revoke-key", "revoke-fingerprint")), ct);
        await processor.ProcessAsync(rejected, ct);
        var recovered = await store.GetAsync<OrganizationPermissionProjection>("statestore", key, ct);
        recovered.Value!.LastSequence.ShouldBe(4);
        recovered.Value.Grants.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrganizationRejectionAdvancesWatermarkAndFolderRejectionFailsWithoutChangingIt()
    {
        WorkerRouteReadModelStore store = new();
        var clock = new FixedUtcClock(DateTimeOffset.UtcNow);
        var model = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, Substitute.For<IOrganizationStreamSequenceReader>());
        var processor = new FoldersOrganizationAclEventProcessor(model);
        var envelope = new EventStoreDomainEventEnvelope("rejection", "tenant-a", "tenant-a",
            "Hexalith.Folders.Server.FolderCommandRejected", 1, clock.UtcNow, "corr", "json",
            JsonSerializer.SerializeToUtf8Bytes(new { CommandType = FoldersServerModule.InitializeOrganizationAclBaselineCommandType }))
        { Domain = "folders", MetadataVersion = 1 };
        await processor.ProcessAsync(envelope, TestContext.Current.CancellationToken);
        var row = await store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken);
        row.Value!.LastSequence.ShouldBe(1);
        row.Value.Grants.ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(envelope with
        {
            MessageId = "folder-rejection",
            SequenceNumber = 2,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { CommandType = FoldersServerModule.CreateFolderCommandType })
        },
            TestContext.Current.CancellationToken));
        (await store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken)).ETag.ShouldBe(row.ETag);
    }

    [Fact]
    public async Task RejectionInventoryAndOrganizationScopeEvidenceMatchPublishedCommandMapper()
    {
        var mapper = new FolderCommandActionTokenMapper();
        var clock = new FixedUtcClock(DateTimeOffset.UtcNow);
        int mapped = 0;
        foreach (FieldInfo field in typeof(FoldersServerModule).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.EndsWith("CommandType", StringComparison.Ordinal)))
        {
            string name = (string)field.GetValue(null)!;
            var command = new CommandEnvelope("message", "tenant-a", "folders", "folder-a", name,
                "{}"u8.ToArray(), "corr", null, "user-a", null);
            var mapping = mapper.Map(command);
            if (mapping is null) continue;
            mapped++;
            bool organization = mapping.ScopeKind == FolderCommandOperationScopeKind.OrganizationBaseline
                && name != FoldersServerModule.CreateFolderCommandType;
            FoldersRejectedCommandClassifier.IsOrganization(JsonSerializer.SerializeToUtf8Bytes(new { CommandType = name }))
                .ShouldBe(organization, name);
            var readModel = new InMemoryEffectivePermissionsReadModel();
            readModel.Save(new("tenant-a", "tenant-a", "organization_baseline", EffectivePermissionsFolderLifecycleState.Active,
                [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User("user-a"),
                    mapping.ActionToken, 1, clock.UtcNow)], new("read_your_writes", clock.UtcNow, "1", false, null), true, null));
            var evidence = await new EffectivePermissionsFolderPermissionEvidenceProvider(readModel, clock).GetEvidenceAsync(
                new("tenant-a", "user-a", "user-a", mapping.ActionToken, "organization_baseline", "corr", null,
                    FolderOperationPolicyClass.Mutation, false), TestContext.Current.CancellationToken);
            evidence.Status.ShouldBe(mapping.ScopeKind == FolderCommandOperationScopeKind.OrganizationBaseline
                ? FolderPermissionEvidenceStatus.Allowed : FolderPermissionEvidenceStatus.NotFoundSafe, name);
        }
        mapped.ShouldBe(16);
    }
}
