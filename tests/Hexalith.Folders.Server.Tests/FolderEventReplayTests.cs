using System.Text.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Server;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderEventReplayTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnstampedVersionOneEventShouldRebuildStateAfterRepositoryRestart()
    {
        FolderCreated created = Created();
        DomainServiceCurrentState persisted = new(null, [Envelope(created)], 0, 1);
        FolderEventReplay replay = Replay();

        ActorFoldedFolderRepository firstProcess = new(replay);
        firstProcess.Begin("tenant-a", "folder-a", persisted);
        firstProcess.Load(FolderStreamName.Create("tenant-a", "folder-a")).DisplayName.ShouldBe("Folder A");

        ActorFoldedFolderRepository restartedProcess = new(replay);
        restartedProcess.Begin("tenant-a", "folder-a",
            JsonSerializer.SerializeToElement(persisted, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        FolderState recovered = restartedProcess.Load(FolderStreamName.Create("tenant-a", "folder-a"));
        recovered.IsCreated.ShouldBeTrue();
        recovered.IdempotencyFingerprints[created.IdempotencyKey].ShouldBe(created.IdempotencyFingerprint);
    }

    [Fact]
    public void JsonFolderSnapshotShouldRebuildStateAndRejectForeignIdentity()
    {
        FolderState snapshot = FolderState.Empty.Apply([Created()],
            FolderStreamName.Create("tenant-a", "folder-a"));
        DomainServiceCurrentState persisted = new(
            JsonSerializer.SerializeToElement(snapshot), [], 1, 1);
        FolderEventReplay replay = Replay();

        FolderState recovered = replay.FoldFolder(
            JsonSerializer.SerializeToElement(persisted, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "tenant-a", "folder-a");
        recovered.DisplayName.ShouldBe("Folder A");
        recovered.IdempotencyFingerprints["idempotency-a"].ShouldBe("fingerprint-a");

        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            persisted, "tenant-b", "folder-a"));
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            persisted, "tenant-a", "folder-b"));
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(JsonSerializer.SerializeToElement(new { isCreated = true }), [], 1, 1),
            "tenant-a", "folder-a"));
    }

    [Fact]
    public void ProductionDecisionShouldNotWriteOutsideActor()
    {
        FolderEventReplay replay = Replay();
        ActorFoldedFolderRepository decision = new(replay);
        decision.Begin("tenant-a", "folder-a", null);
        FolderCreated created = Created();

        decision.AppendIfFingerprintAbsent(FolderStreamName.Create("tenant-a", "folder-a"),
            created.IdempotencyKey, created.IdempotencyFingerprint, [created])
            .ShouldBe(FolderAppendOutcome.Appended);

        ActorFoldedFolderRepository nextProcess = new(replay);
        nextProcess.Begin("tenant-a", "folder-a", null);
        nextProcess.Load(FolderStreamName.Create("tenant-a", "folder-a")).IsCreated.ShouldBeFalse();
    }

    [Fact]
    public void SameKeyShouldReplayEquivalentIntentAndConflictWithDifferentIntent()
    {
        FolderCreated created = Created();
        ActorFoldedFolderRepository repository = new(Replay());
        repository.Begin("tenant-a", "folder-a",
            new DomainServiceCurrentState(null, [Envelope(created)], 0, 1));
        FolderStreamName stream = FolderStreamName.Create("tenant-a", "folder-a");

        repository.AppendIfFingerprintAbsent(stream, created.IdempotencyKey,
            created.IdempotencyFingerprint, [created]).ShouldBe(FolderAppendOutcome.FingerprintMatched);
        repository.AppendIfFingerprintAbsent(stream, created.IdempotencyKey,
            "different-fingerprint", [created]).ShouldBe(FolderAppendOutcome.FingerprintConflict);
        repository.Load(stream).DisplayName.ShouldBe("Folder A");
    }

    [Fact]
    public void ProjectionShouldReadUnstampedVersionOneBytesAndRejectUnknownOrUnsupportedEvents()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(ReplayRegistry())
            .AddSingleton<IDomainProjectionHandler, FolderDomainProjectionHandler>()
            .BuildServiceProvider();

        FolderCreated created = Created();
        ProjectionEventDto stored = new(typeof(FolderCreated).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(created), "json", 1, OccurredAt,
            "correlation-a") { MetadataVersion = 1 };
        ProjectionRequest request = new("tenant-a", "folders", "folder-a", [stored]);

        ProjectionResponse? result = DomainProjectionDispatcher.Project(
            services, request, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.State.GetProperty("isCreated").GetBoolean().ShouldBeTrue();
        result.State.GetProperty("displayName").GetString().ShouldBe("Folder A");

        Should.Throw<InvalidOperationException>(() => DomainProjectionDispatcher.Project(
            services, request with { Events = [stored with { EventTypeName = "Hexalith.Folders.Aggregates.Folder.UnknownEvent" }] },
            TestContext.Current.CancellationToken));
        Should.Throw<EventPayloadEvolutionException>(() => DomainProjectionDispatcher.Project(
            services, request with { Events = [stored with { StoredPayloadVersion = 2 }] },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ForeignTenantEventShouldFailBeforeStateIsReturned()
    {
        FolderCreated foreign = Created() with { ManagedTenantId = "tenant-b" };
        ActorFoldedFolderRepository repository = new(Replay());
        Should.Throw<InvalidOperationException>(() => repository.Begin("tenant-a", "folder-a",
            new DomainServiceCurrentState(null, [Envelope(foreign)], 0, 1)));
    }

    [Fact]
    public void IncompleteOrUnknownActorStreamShouldFailClosed()
    {
        FolderCreated created = Created();
        EventEnvelope stored = Envelope(created);
        FolderEventReplay replay = Replay();

        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null, [stored], 0, 2), "tenant-a", "folder-a"));
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null,
                [Envelope("Hexalith.Folders.Aggregates.Folder.UnknownEvent", stored.Payload, 1)], 0, 1),
            "tenant-a", "folder-a"));
        Should.Throw<EventPayloadEvolutionException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null,
                [Envelope(typeof(FolderCreated).FullName!, stored.Payload, 1, 1025)], 0, 1),
            "tenant-a", "folder-a"));
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null, null!, 0, 0), "tenant-a", "folder-a"));
    }

    [Fact]
    public void FrozenStorageNamesShouldMatchEveryFolderEventType()
    {
        string[] frozen = File.ReadAllLines(Fixture("frozen-event-names-v1.txt"));
        string[] current = typeof(IFolderEvent).Assembly.GetTypes()
            .Where(static type => type.IsClass && !type.IsAbstract
                && (typeof(IFolderEvent).IsAssignableFrom(type)
                    || typeof(IOrganizationEvent).IsAssignableFrom(type)))
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        frozen.ShouldBe(current);
    }

    [Fact]
    public void RetainedUnstampedVersionOneBytesShouldReplayWithoutRewrite()
    {
        byte[] bytes = File.ReadAllBytes(Fixture("folder-created-unstamped-v1.json"));
        byte[] originalDigest = SHA256.HashData(bytes);
        EventEnvelope stored = Envelope(typeof(FolderCreated).FullName!, bytes, 1, null);
        FolderState result = Replay().FoldFolder(new DomainServiceCurrentState(null, [stored], 0, 1),
            "tenant-a", "folder-a");

        result.DisplayName.ShouldBe("Folder A");
        SHA256.HashData(bytes).ShouldBe(originalDigest);
        File.ReadAllBytes(Fixture("folder-created-unstamped-v1.json")).ShouldBe(bytes);
    }

    [Fact]
    public void PersistedRejectionShouldBeSkippedButInvalidStampShouldFailClosed()
    {
        FolderCommandRejected rejection = FolderCommandRejected.Create(
            FolderResultCode.FolderAclDenied.ToString(), "create-folder", "tenant-a", "org-a",
            "folder-a", "user-a", "correlation-a", "task-a", "idempotency-b");
        EventEnvelope first = Envelope(Created());
        byte[] rejectionPayload = JsonSerializer.SerializeToUtf8Bytes(rejection);
        EventEnvelope second = Envelope(typeof(FolderCommandRejected).FullName!, rejectionPayload, 2);
        DomainServiceCurrentState retained = new(null, [first, second], 0, 2);

        Replay().FoldFolder(retained, "tenant-a", "folder-a").IsCreated.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => Replay().FoldFolder(
            retained with { Events = [first, Envelope(typeof(FolderCommandRejected).FullName!, rejectionPayload, 2, 2)] },
            "tenant-a", "folder-a"));
    }

    [Fact]
    public void OrganizationBindingShouldRebuildFromActorEventAfterRestart()
    {
        ProviderBindingConfigured configured = new("tenant-a", "org-a", "binding-a", "forgejo",
            "credential-reference-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a",
            "idempotency-a", "fingerprint-a", "configured", OccurredAt);
        EventPayloadEvolutionRegistry registry = new([typeof(ProviderBindingConfigured)], []);
        FolderEventReplay replay = new(registry);
        DomainServiceCurrentState persisted = new(null,
            [Envelope(typeof(ProviderBindingConfigured).FullName!, JsonSerializer.SerializeToUtf8Bytes(configured),
                1, null, "binding-a")], 0, 1);

        ActorFoldedOrganizationProviderBindingRepository restarted = new(replay);
        restarted.Begin("tenant-a", "binding-a", persisted);
        OrganizationState state = restarted.Load(OrganizationStreamName.Create("tenant-a", "org-a"));
        state.ProviderBindings["binding-a"].CredentialReferenceId.ShouldBe("credential-reference-a");
        state.IdempotencyFingerprints["idempotency-a"].ShouldBe("fingerprint-a");
    }

    [Fact]
    public void JsonOrganizationSnapshotShouldRebuildBindingAndRejectForeignIdentity()
    {
        ProviderBindingConfigured configured = new("tenant-a", "org-a", "binding-a", "forgejo",
            "credential-reference-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a",
            "idempotency-a", "fingerprint-a", "configured", OccurredAt);
        OrganizationState snapshot = OrganizationState.Empty.Apply([configured]);
        DomainServiceCurrentState persisted = new(
            JsonSerializer.SerializeToElement(snapshot), [], 1, 1);
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry([typeof(ProviderBindingConfigured)], []));

        OrganizationState recovered = replay.FoldOrganization(
            JsonSerializer.SerializeToElement(persisted, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "tenant-a", "binding-a");
        recovered.ProviderBindings["binding-a"].CredentialReferenceId.ShouldBe("credential-reference-a");
        recovered.IdempotencyFingerprints["idempotency-a"].ShouldBe("fingerprint-a");

        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            persisted, "tenant-b", "binding-a"));
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            persisted, "tenant-a", "binding-b"));
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(JsonSerializer.SerializeToElement(new { isInitialized = true }), [], 1, 1),
            "tenant-a", "binding-a"));
    }

    [Fact]
    public void OrganizationBindingShouldRejectAnotherOrganizationInTheSameActorStream()
    {
        ProviderBindingConfigured first = new("tenant-a", "org-a", "binding-a", "forgejo",
            "credential-reference-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a",
            "idempotency-a", "fingerprint-a", "configured", OccurredAt);
        ProviderBindingConfigured second = first with
        {
            OrganizationId = "org-b",
            IdempotencyKey = "idempotency-b",
        };
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry([typeof(ProviderBindingConfigured)], []));
        EventEnvelope firstEnvelope = Envelope(typeof(ProviderBindingConfigured).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(first), 1, null, "binding-a");
        EventEnvelope secondEnvelope = Envelope(typeof(ProviderBindingConfigured).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(second), 2, null, "binding-a");

        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(null, [firstEnvelope, secondEnvelope], 0, 2),
            "tenant-a", "binding-a"));

        ActorFoldedOrganizationProviderBindingRepository repository = new(replay);
        repository.Begin("tenant-a", "binding-a",
            new DomainServiceCurrentState(null, [firstEnvelope], 0, 1));
        Should.Throw<InvalidOperationException>(() => repository.Load(
            OrganizationStreamName.Create("tenant-a", "org-b")));
    }

    private static string Fixture(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "event-evolution", fileName);

    private static EventPayloadEvolutionRegistry ReplayRegistry()
        => new([typeof(FolderCreated)], []);

    private static FolderEventReplay Replay() => new(ReplayRegistry());

    private static FolderCreated Created()
        => new("tenant-a", "org-a", "folder-a", "Folder A", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "user-a",
            "correlation-a", "task-a", "idempotency-a", "fingerprint-a", OccurredAt);

    private static EventEnvelope Envelope(FolderCreated value)
        => Envelope(typeof(FolderCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(value), 1);

    private static EventEnvelope Envelope(string name, byte[] payload, long sequence,
        int? payloadVersion = 1, string aggregateId = "folder-a")
        => new(new EventMetadata("message-a", aggregateId, "folder", "tenant-a", "folders", sequence,
                1, OccurredAt, "correlation-a", "causation-a", "user-a", "v1",
                name, 1, "json") { PayloadVersion = payloadVersion }, payload, null);
}
