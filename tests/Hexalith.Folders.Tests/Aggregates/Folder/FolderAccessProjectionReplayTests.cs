using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessProjectionReplayTests
{
    [Fact]
    public void ProjectionShouldPreserveRevocationWatermarkAndMetadata()
    {
        IReadOnlyList<IFolderEvent> events = GrantThenRevokeEvents("tenant-a", "folder-a");

        FolderAccessProjection projection = FolderAccessProjection.FromEvents("tenant-a", "folder-a", events);

        projection.Watermark.ShouldBe(2);
        FolderAccessOverride access = projection.Overrides.Values.Single();
        access.IsGranted.ShouldBeFalse();
        access.OperationIntent.ShouldBe("revoke");
        access.CorrelationId.ShouldBe("correlation-revoke");
        access.IdempotencyKey.ShouldBe("idempotency-revoke");
        access.RevocationHistory.Count.ShouldBe(1);
        access.RevocationHistory[0].CorrelationId.ShouldBe("correlation-revoke");
        access.RevocationHistory[0].AccessSequence.ShouldBe(2);
    }

    [Fact]
    public void ProjectionShouldIsolateTenantsWithMatchingFolderPrincipalAndAction()
    {
        IReadOnlyList<IFolderEvent> tenantAEvents = GrantThenRevokeEvents("tenant-a", "folder-shared");
        IReadOnlyList<IFolderEvent> tenantBEvents = GrantEvents("tenant-b", "folder-shared");

        FolderAccessProjection tenantA = FolderAccessProjection.FromEvents("tenant-a", "folder-shared", tenantAEvents);
        FolderAccessProjection tenantB = FolderAccessProjection.FromEvents("tenant-b", "folder-shared", tenantBEvents);

        tenantA.Overrides.Keys.Single().ManagedTenantId.ShouldBe("tenant-a");
        tenantA.Overrides.Values.Single().IsGranted.ShouldBeFalse();
        tenantB.Overrides.Keys.Single().ManagedTenantId.ShouldBe("tenant-b");
        tenantB.Overrides.Values.Single().IsGranted.ShouldBeTrue();
    }

    [Fact]
    public void GrantRevokeGrantReplayShouldPreserveHistoricalRevocationMetadata()
    {
        // P2 lock-in: after grant→revoke→grant the latest override is a grant but the
        // historical revocation correlation/sequence/timestamp must still be inspectable
        // for C7 freshness audits and authorization replay.
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-a"));
        FolderState granted = created.Apply(grant.Events, streamName);
        FolderResult revoke = FolderAggregate.Handle(granted, FolderCommandFactory.RevokeAccess(idempotencyKey: "idempotency-revoke", correlationId: "correlation-revoke"));
        FolderState revoked = granted.Apply(revoke.Events, streamName);
        FolderResult grantAgain = FolderAggregate.Handle(revoked, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-b"));

        FolderState final = revoked.Apply(grantAgain.Events, streamName);
        FolderAccessOverride latest = final.AccessOverrides.Values.Single();

        final.AccessSequence.ShouldBe(3);
        latest.IsGranted.ShouldBeTrue();
        latest.OperationIntent.ShouldBe("grant");
        latest.RevocationHistory.Count.ShouldBe(1);
        latest.RevocationHistory[0].AccessSequence.ShouldBe(2);
        latest.RevocationHistory[0].CorrelationId.ShouldBe("correlation-revoke");
        latest.RevocationHistory[0].IdempotencyKey.ShouldBe("idempotency-revoke");
    }

    [Fact]
    public void OccurredAtShouldFlowFromGrantEventToProjectionOverride()
    {
        // P1 lock-in: events carry a wall-clock OccurredAt assigned by the gate's TimeProvider;
        // the override surfaces it so downstream C7 freshness checks can compute time deltas.
        DateTimeOffset grantedAt = new(2026, 5, 19, 9, 30, 15, TimeSpan.Zero);
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult grant = FolderAggregate.Handle(
            created,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant"),
            grantedAt);

        FolderState granted = created.Apply(grant.Events, streamName);
        FolderAccessOverride latest = granted.AccessOverrides.Values.Single();

        latest.OccurredAt.ShouldBe(grantedAt);
        grant.Events.OfType<FolderAccessGranted>().Single().OccurredAt.ShouldBe(grantedAt);
    }

    [Fact]
    public void StaleAccessSequenceShouldNotRegressOverride()
    {
        // P7 lock-in: an out-of-order event with a smaller AccessSequence must not
        // overwrite a fresher override. The state-level watermark stays at the max,
        // but the per-override entry is preserved.
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult firstGrant = FolderAggregate.Handle(
            created,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-a"));
        FolderState afterFirst = created.Apply(firstGrant.Events, streamName);
        FolderAccessGranted freshEvent = firstGrant.Events.OfType<FolderAccessGranted>().Single();

        // Construct a "stale" event with a smaller AccessSequence than what's now recorded.
        FolderAccessGranted staleEvent = freshEvent with
        {
            AccessSequence = freshEvent.AccessSequence - 1,
            CorrelationId = "correlation-stale",
            IdempotencyKey = "idempotency-stale",
            IdempotencyFingerprint = "fingerprint-stale",
        };

        FolderState afterStale = afterFirst.Apply([staleEvent], streamName);

        afterStale.AccessOverrides.Values.Single().CorrelationId.ShouldBe(freshEvent.CorrelationId);
        afterStale.AccessOverrides.Values.Single().AccessSequence.ShouldBe(freshEvent.AccessSequence);
        afterStale.AccessSequence.ShouldBe(freshEvent.AccessSequence);
    }

    [Fact]
    public void ForeignTenantEventInProjectionShouldThrow()
    {
        // P22 guard test: FolderAccessProjection.FromEvents delegates to FolderState.Apply,
        // which rejects events whose (tenant, folder) does not match the expected stream.
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant"));
        FolderAccessGranted granted = grant.Events.OfType<FolderAccessGranted>().Single();

        FolderAccessGranted foreignEvent = granted with { ManagedTenantId = "tenant-b" };

        Should.Throw<InvalidOperationException>(() =>
            FolderAccessProjection.FromEvents("tenant-a", "folder-a", [granted, foreignEvent]));
    }

    [Fact]
    public void PoisonedPrincipalKindEventShouldFailReplayWithStableException()
    {
        // D2 lock-in: replaying an event with an unknown principal kind must fail loud
        // rather than silently bucket it under a tolerant token. The poisoned-stream
        // signal is more useful than partial coherence under unknown enum values.
        FolderState created = CreatedState("tenant-a", "folder-a");
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant"));
        FolderState granted = created.Apply(grant.Events, streamName);

        FolderAccessEntryKey poisoned = granted.AccessOverrides.Keys.Single() with
        {
            PrincipalKind = (FolderAccessPrincipalKind)99,
        };

        Should.Throw<InvalidOperationException>(() => _ = poisoned.PrincipalKindToken);
        Should.Throw<InvalidOperationException>(() => _ = poisoned.CanonicalValue);
    }

    private static IReadOnlyList<IFolderEvent> GrantThenRevokeEvents(string tenantId, string folderId)
    {
        FolderState created = CreatedState(tenantId, folderId);
        FolderStreamName streamName = FolderStreamName.Create(tenantId, folderId);
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            idempotencyKey: "idempotency-grant"));
        FolderState granted = created.Apply(grant.Events, streamName);
        FolderResult revoke = FolderAggregate.Handle(granted, FolderCommandFactory.RevokeAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            correlationId: "correlation-revoke",
            idempotencyKey: "idempotency-revoke"));

        return [.. CreatedEvent(tenantId, folderId), .. grant.Events, .. revoke.Events];
    }

    private static IReadOnlyList<IFolderEvent> GrantEvents(string tenantId, string folderId)
    {
        FolderState created = CreatedState(tenantId, folderId);
        FolderResult grant = FolderAggregate.Handle(created, FolderCommandFactory.GrantAccess(
            managedTenantId: tenantId,
            folderId: folderId,
            idempotencyKey: "idempotency-grant"));
        return [.. CreatedEvent(tenantId, folderId), .. grant.Events];
    }

    private static IReadOnlyList<IFolderEvent> CreatedEvent(string tenantId, string folderId)
        => FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(managedTenantId: tenantId, folderId: folderId)).Events;

    private static FolderState CreatedState(string tenantId, string folderId)
        => FolderState.Empty.Apply(CreatedEvent(tenantId, folderId), FolderStreamName.Create(tenantId, folderId));
}
