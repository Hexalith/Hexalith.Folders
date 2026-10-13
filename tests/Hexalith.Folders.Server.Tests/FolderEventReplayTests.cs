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
    public void RejectionOnlyProviderBindingProjectionShouldRemainOrganizationScoped()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(ReplayRegistry())
            .AddSingleton<IDomainProjectionHandler, FolderDomainProjectionHandler>()
            .BuildServiceProvider();
        FolderCommandRejected rejection = FolderCommandRejected.Create(
            FolderResultCode.ValidationFailed.ToString(),
            FoldersServerModule.ConfigureProviderBindingCommandType,
            "tenant-a", "org-a", null, "user-a", "correlation-a", "task-a", "idempotency-a");
        ProjectionEventDto item = new(typeof(FolderCommandRejected).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(rejection), "json", 1, OccurredAt,
            "correlation-a") { MetadataVersion = 1 };
        ProjectionRequest request = new("tenant-a", "folders", ProviderBindingActorId.Create("binding-a"), [item]);

        ProjectionResponse? projected = DomainProjectionDispatcher.Project(
            services, request, TestContext.Current.CancellationToken);
        projected.ShouldNotBeNull();
        projected.ProjectionType.ShouldBe("organization-provider-binding");

        FolderCommandRejected unknown = FolderCommandRejected.Create(
            FolderResultCode.UnsupportedCommandType.ToString(), "unknown_command_type",
            "tenant-a", "org-a", null, "user-a", "correlation-a", "task-a", "idempotency-a");
        Should.Throw<InvalidOperationException>(() => DomainProjectionDispatcher.Project(
            services, request with { Events = [item with { Payload = JsonSerializer.SerializeToUtf8Bytes(unknown) }] },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OrganizationAclFirstEventMustMatchItsActorIdentity()
    {
        OrganizationAclBaselineInitialized foreign = new("tenant-a", "org-b", "correlation-a",
            "task-a", "idempotency-a", "fingerprint-a");
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry(
            [typeof(OrganizationAclBaselineInitialized)], []));
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(foreign);
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(null,
                [Envelope(typeof(OrganizationAclBaselineInitialized).FullName!, bytes, 1, null, "org-a")],
                0, 1), "tenant-a", "org-a"));
        Should.Throw<InvalidOperationException>(() => FolderEventReplay.FoldOrganization(
            new ProjectionRequest("tenant-a", "folders", "org-a",
                [new ProjectionEventDto(typeof(OrganizationAclBaselineInitialized).FullName!, bytes,
                    "json", 1, OccurredAt, "correlation-a") { MetadataVersion = 1 }])));
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
            .Concat([typeof(FolderCommandRejected), typeof(DuplicateWorkspaceLockRejected),
                typeof(WorkspaceTransitionInvalidRejected)])
            .Select(static type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        frozen.ShouldBe(current);
    }

    [Fact]
    public void RetainedWrongOrganizationBytesShouldFailClosedWithoutRewrite()
    {
        byte[] bytes = File.ReadAllBytes(Fixture("folder-created-unstamped-v1.json"));
        byte[] originalDigest = SHA256.HashData(bytes);
        EventEnvelope stored = Envelope(typeof(FolderCreated).FullName!, bytes, 1, null);
        Should.Throw<InvalidOperationException>(() => Replay().FoldFolder(
            new DomainServiceCurrentState(null, [stored], 0, 1), "tenant-a", "folder-a"));
        SHA256.HashData(bytes).ShouldBe(originalDigest);
        File.ReadAllBytes(Fixture("folder-created-unstamped-v1.json")).ShouldBe(bytes);
    }

    [Fact]
    public void PersistedRejectionShouldBeSkippedButInvalidStampShouldFailClosed()
    {
        FolderCommandRejected rejection = FolderCommandRejected.Create(
            FolderResultCode.FolderAclDenied.ToString(), FoldersServerModule.CreateFolderCommandType, "tenant-a", "org-a",
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
        ProviderBindingConfigured configured = new("tenant-a", "tenant-a", "binding-a", "forgejo",
            "credential-reference-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a",
            "idempotency-a", "fingerprint-a", "configured", OccurredAt);
        EventPayloadEvolutionRegistry registry = new([typeof(ProviderBindingConfigured)], []);
        FolderEventReplay replay = new(registry);
        DomainServiceCurrentState persisted = new(null,
            [Envelope(typeof(ProviderBindingConfigured).FullName!, JsonSerializer.SerializeToUtf8Bytes(configured),
                1, null, ProviderBindingActorId.Create("binding-a"))], 0, 1);

        ActorFoldedOrganizationProviderBindingRepository restarted = new(replay);
        restarted.Begin("tenant-a", ProviderBindingActorId.Create("binding-a"), persisted);
        OrganizationState state = restarted.Load(OrganizationStreamName.Create("tenant-a", "binding-a"));
        state.ProviderBindings["binding-a"].CredentialReferenceId.ShouldBe("credential-reference-a");
        state.IdempotencyFingerprints["idempotency-a"].ShouldBe("fingerprint-a");
    }

    [Fact]
    public void JsonOrganizationSnapshotShouldRebuildBindingAndRejectForeignIdentity()
    {
        ProviderBindingConfigured configured = new("tenant-a", "tenant-a", "binding-a", "forgejo",
            "credential-reference-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a",
            "idempotency-a", "fingerprint-a", "configured", OccurredAt);
        OrganizationState snapshot = OrganizationState.Empty.Apply([configured]);
        DomainServiceCurrentState persisted = new(
            JsonSerializer.SerializeToElement(snapshot), [], 1, 1);
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry([typeof(ProviderBindingConfigured)], []));

        OrganizationState recovered = replay.FoldOrganization(
            JsonSerializer.SerializeToElement(persisted, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "tenant-a", ProviderBindingActorId.Create("binding-a"));
        recovered.ProviderBindings["binding-a"].CredentialReferenceId.ShouldBe("credential-reference-a");
        recovered.IdempotencyFingerprints["idempotency-a"].ShouldBe("fingerprint-a");

        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            persisted, "tenant-b", ProviderBindingActorId.Create("binding-a")));
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            persisted, "tenant-a", ProviderBindingActorId.Create("binding-b")));
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(JsonSerializer.SerializeToElement(new { isInitialized = true }), [], 1, 1),
            "tenant-a", ProviderBindingActorId.Create("binding-a")));
    }

    [Fact]
    public void OrganizationBindingShouldRejectAnotherOrganizationInTheSameActorStream()
    {
        ProviderBindingConfigured first = new("tenant-a", "tenant-a", "binding-a", "forgejo",
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
            JsonSerializer.SerializeToUtf8Bytes(first), 1, null, ProviderBindingActorId.Create("binding-a"));
        EventEnvelope secondEnvelope = Envelope(typeof(ProviderBindingConfigured).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(second), 2, null, ProviderBindingActorId.Create("binding-a"));

        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(null, [firstEnvelope, secondEnvelope], 0, 2),
            "tenant-a", ProviderBindingActorId.Create("binding-a")));

        ActorFoldedOrganizationProviderBindingRepository repository = new(replay);
        repository.Begin("tenant-a", ProviderBindingActorId.Create("binding-a"),
            new DomainServiceCurrentState(null, [firstEnvelope], 0, 1));
        Should.Throw<InvalidOperationException>(() => repository.Load(
            OrganizationStreamName.Create("tenant-a", "org-b")));
    }

    [Fact]
    public void ProviderBindingActorShouldRejectCollidingRetainedFolderStream()
    {
        DomainServiceCurrentState folderHistory = new(null, [Envelope(Created())], 0, 1);
        Should.Throw<InvalidOperationException>(() => Replay().FoldOrganization(
            folderHistory, "tenant-a", "folder-a"));

        FolderCommandRejected folderRejection = FolderCommandRejected.Create(
            FolderResultCode.ValidationFailed.ToString(), FoldersServerModule.CreateFolderCommandType,
            "tenant-a", "org-a", "folder-a", "user-a", "correlation-a", "task-a", "idempotency-b");
        DomainServiceCurrentState rejectionOnly = new(null,
            [Envelope(typeof(FolderCommandRejected).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(folderRejection), 1)], 0, 1);
        Should.Throw<InvalidOperationException>(() => Replay().FoldOrganization(
            rejectionOnly, "tenant-a", "folder-a"));
    }

    [Fact]
    public void SamePublicFolderAndBindingReferenceUseIndependentActorStreams()
    {
        FolderCreated folder = Created();
        ProviderBindingConfigured binding = new("tenant-a", "tenant-a", "folder-a", "github",
            "credential-a", OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a", "idempotency-b",
            "fingerprint-b", "configured", OccurredAt);
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry(
            [typeof(FolderCreated), typeof(ProviderBindingConfigured)], []));

        FolderState folderState = replay.FoldFolder(new DomainServiceCurrentState(null,
            [Envelope(folder)], 0, 1), "tenant-a", "folder-a");
        OrganizationState bindingState = replay.FoldOrganization(new DomainServiceCurrentState(null,
            [Envelope(typeof(ProviderBindingConfigured).FullName!, JsonSerializer.SerializeToUtf8Bytes(binding),
                1, aggregateId: ProviderBindingActorId.Create("folder-a"))], 0, 1),
            "tenant-a", ProviderBindingActorId.Create("folder-a"));

        folderState.IsCreated.ShouldBeTrue();
        bindingState.ProviderBindings["folder-a"].ProviderBindingRef.ShouldBe("folder-a");
        ProviderBindingActorId.Create("folder-a").ShouldNotBe("folder-a");
    }

    [Fact]
    public void RetainedFolderReplayRejectsSecondCreateWrongOrganizationAndInvalidWorkspaceTransition()
    {
        FolderCreated created = Created();
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry(
            [typeof(FolderCreated), typeof(FolderWorkspaceLifecycleEventRecorded)], []));
        EventEnvelope first = Envelope(created);
        EventEnvelope duplicate = Envelope(typeof(FolderCreated).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(created with { DisplayName = "Other" }), 2);
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null, [first, duplicate], 0, 2), "tenant-a", "folder-a"));

        EventEnvelope foreignOrganization = Envelope(created with { OrganizationId = "other-organization" });
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null, [foreignOrganization], 0, 1), "tenant-a", "folder-a"));

        FolderWorkspaceLifecycleEventRecorded invalid = new("tenant-a", "tenant-a", "folder-a",
            "workspace-a", FolderWorkspaceLifecycleEvent.WorkspacePrepared, null, "operation-a",
            "correlation-a", "task-a", "idempotency-b", "fingerprint-b", OccurredAt);
        EventEnvelope second = Envelope(typeof(FolderWorkspaceLifecycleEventRecorded).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(invalid), 2);
        Should.Throw<InvalidOperationException>(() => replay.FoldFolder(
            new DomainServiceCurrentState(null, [first, second], 0, 2), "tenant-a", "folder-a"));
    }

    [Fact]
    public void RetainedOrganizationGrantBeforeBaselineFailsClosed()
    {
        OrganizationAclPrincipalGranted granted = new("tenant-a", "tenant-a",
            OrganizationAclPrincipalKind.User, "user-a", "create_folder", "correlation-a",
            "task-a", "idempotency-a", "fingerprint-a");
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry(
            [typeof(OrganizationAclPrincipalGranted)], []));
        EventEnvelope first = Envelope(typeof(OrganizationAclPrincipalGranted).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(granted), 1, aggregateId: "tenant-a");
        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(null, [first], 0, 1), "tenant-a", "tenant-a"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UninitializedOrganizationSnapshotWithGrantsFailsBeforeBaselineReplay(bool jsonSnapshot)
    {
        OrganizationAclEntryKey grant = new("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
            "user-a", "create_folder");
        OrganizationState snapshot = OrganizationState.Empty with { Grants = new HashSet<OrganizationAclEntryKey> { grant } };
        OrganizationAclBaselineInitialized initialized = new("tenant-a", "tenant-a", "correlation-a",
            "task-a", "idempotency-a", "fingerprint-a");
        EventEnvelope envelope = Envelope(typeof(OrganizationAclBaselineInitialized).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(initialized), 2, aggregateId: "tenant-a");
        object storedSnapshot = jsonSnapshot ? JsonSerializer.SerializeToElement(snapshot) : snapshot;
        FolderEventReplay replay = new(new EventPayloadEvolutionRegistry([typeof(OrganizationAclBaselineInitialized)], []));

        Should.Throw<InvalidOperationException>(() => replay.FoldOrganization(
            new DomainServiceCurrentState(storedSnapshot, [envelope], 1, 2), "tenant-a", "tenant-a"))
            .Message.ShouldBe("Uninitialized organization snapshot contains grants.");
        snapshot.IsInitialized.ShouldBeFalse();
        snapshot.Grants.ShouldHaveSingleItem().ShouldBe(grant);
    }

    [Fact]
    public void UnknownRawSuffixAndMissingSnapshotAnchorsFailClosed()
    {
        byte[] bytes = File.ReadAllBytes(Fixture("folder-created-valid-unstamped-v1.json"));
        foreach (string name in new[] { "Unregistered.Namespace.FolderCreated", "FolderCreated" })
        {
            Should.Throw<InvalidOperationException>(() => Replay().FoldFolder(
                new DomainServiceCurrentState(null, [Envelope(name, bytes, 1, null)], 0, 1),
                "tenant-a", "folder-a"));
        }

        DomainServiceCurrentState missing = new(null, [], 1, 1);
        Should.Throw<InvalidOperationException>(() => Replay().FoldFolder(missing, "tenant-a", "folder-a"));
        Should.Throw<InvalidOperationException>(() => Replay().FoldOrganization(missing, "tenant-a", "tenant-a"));
        Replay().FoldFolder(new DomainServiceCurrentState(null, [], 0, 0), "tenant-a", "folder-a")
            .IsCreated.ShouldBeFalse();
    }

    [Fact]
    public void ImmutableVersionOneBytesAndAllServerRejectionsRebuildActorAndProjectState()
    {
        byte[] created = File.ReadAllBytes(Fixture("folder-created-valid-unstamped-v1.json"));
        string[] names = [typeof(FolderCommandRejected).FullName!,
            typeof(DuplicateWorkspaceLockRejected).FullName!, typeof(WorkspaceTransitionInvalidRejected).FullName!];
        string[] files = ["folder-command-rejected-unstamped-v1.json",
            "duplicate-workspace-lock-rejected-unstamped-v1.json", "workspace-transition-invalid-rejected-unstamped-v1.json"];
        List<EventEnvelope> events = [Envelope(typeof(FolderCreated).FullName!, created, 1, null)];
        List<ProjectionEventDto> projection = [new(typeof(FolderCreated).FullName!, created,
            "json", 1, OccurredAt, "correlation-a") { MetadataVersion = 1 }];
        for (int i = 0; i < files.Length; i++)
        {
            byte[] bytes = File.ReadAllBytes(Fixture(files[i]));
            events.Add(Envelope(names[i], bytes, i + 2, null));
            projection.Add(new(names[i], bytes, "json", i + 2, OccurredAt, "correlation-a") { MetadataVersion = 1 });
        }

        Replay().FoldFolder(new DomainServiceCurrentState(null, events, 0, 4), "tenant-a", "folder-a")
            .DisplayName.ShouldBe("Folder A");
        using ServiceProvider services = new ServiceCollection().AddSingleton(ReplayRegistry())
            .AddSingleton<IDomainProjectionHandler, FolderDomainProjectionHandler>().BuildServiceProvider();
        DomainProjectionDispatcher.Project(services, new ProjectionRequest("tenant-a", "folders", "folder-a", [.. projection]),
            TestContext.Current.CancellationToken)!.State.GetProperty("isCreated").GetBoolean().ShouldBeTrue();
        foreach (string line in File.ReadAllLines(Fixture("retained-fixtures-sha256.txt")))
        {
            string[] entry = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Fixture(entry[1])))).ShouldBe(entry[0]);
        }
    }

    private static string Fixture(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "event-evolution", fileName);

    private static EventPayloadEvolutionRegistry ReplayRegistry()
        => new([typeof(FolderCreated)], []);

    private static FolderEventReplay Replay() => new(ReplayRegistry());

    private static FolderCreated Created()
        => new("tenant-a", "tenant-a", "folder-a", "Folder A", null, null, [],
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
