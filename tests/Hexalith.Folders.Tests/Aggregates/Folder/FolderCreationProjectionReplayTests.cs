using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationProjectionReplayTests
{
    [Fact]
    public void ProjectionShouldSkipEnvelopeWhenEventTenantDisagreesWithEnvelopeTenant()
    {
        // The projection bucket is keyed by envelope tenant (envelope-wins for scope) but
        // the aggregate-level invariant that envelope and event tenant must agree applies
        // here too: a misrouted event is skipped, not silently filed under either tenant.
        FolderCreated eventWithMismatchedPayload = new(
            "payload-tenant",
            "organization-a",
            "folder-a",
            "Folder A",
            null,
            null,
            [],
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-a",
            "fingerprint-a");

        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [new FolderProjectionEnvelope("envelope-tenant", 1, eventWithMismatchedPayload)]);

        projection.Folders.Count.ShouldBe(0);
        projection.Contains("envelope-tenant", "folder-a").ShouldBeFalse();
        projection.Contains("payload-tenant", "folder-a").ShouldBeFalse();
    }

    [Fact]
    public void ProjectionShouldRecordEnvelopeWhenEventTenantMatches()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [new FolderProjectionEnvelope("tenant-a", 1, Created("tenant-a", "folder-a", "Folder A"))]);

        projection.Contains("tenant-a", "folder-a").ShouldBeTrue();
    }

    [Fact]
    public void ProjectionShouldIsolateSameFolderIdAcrossTenants()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Created("tenant-a", "folder-shared", "Folder A")),
                new FolderProjectionEnvelope("tenant-b", 1, Created("tenant-b", "folder-shared", "Folder B")),
            ]);

        projection.Get("tenant-a", "folder-shared")!.DisplayName.ShouldBe("Folder A");
        projection.Get("tenant-b", "folder-shared")!.DisplayName.ShouldBe("Folder B");
    }

    [Fact]
    public void DuplicateCreationEventsShouldReplayDeterministically()
    {
        FolderCreated created = Created("tenant-a", "folder-a", "Folder A");

        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, created),
                new FolderProjectionEnvelope("tenant-a", 1, created),
            ]);

        projection.Folders.Count.ShouldBe(1);
        projection.Get("tenant-a", "folder-a")!.LifecycleState.ShouldBe(FolderLifecycleState.Active);
    }

    [Fact]
    public void TiedSequencesWithDifferentContentShouldOrderByIdempotencyKey()
    {
        // Sequence tie-breaker is `(IdempotencyKey, IdempotencyFingerprint)`. Earlier
        // implementations relied on enumerator stability — the new tiebreaker makes the
        // last-write-wins choice deterministic regardless of source enumerable order.
        FolderCreated first = Created("tenant-a", "folder-a", "Folder A", idempotencyKey: "idempotency-a");
        FolderCreated second = Created("tenant-a", "folder-a", "Folder B", idempotencyKey: "idempotency-b");

        FolderListProjection forwardOrder = FolderListProjection.Empty.Apply(
            [new FolderProjectionEnvelope("tenant-a", 1, first), new FolderProjectionEnvelope("tenant-a", 1, second)]);
        FolderListProjection reversedOrder = FolderListProjection.Empty.Apply(
            [new FolderProjectionEnvelope("tenant-a", 1, second), new FolderProjectionEnvelope("tenant-a", 1, first)]);

        // Both orderings collapse to the same final state thanks to the deterministic tiebreaker:
        // idempotency-b sorts after idempotency-a, so the second display name wins last-write.
        forwardOrder.Get("tenant-a", "folder-a")!.DisplayName.ShouldBe("Folder B");
        reversedOrder.Get("tenant-a", "folder-a")!.DisplayName.ShouldBe("Folder B");
    }

    [Fact]
    public void NullEnvelopesAndMalformedSegmentsShouldBeSkippedSafely()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                null!,
                new FolderProjectionEnvelope("Tenant-A", 1, Created("Tenant-A", "folder-a", "Folder A")), // invalid segment (uppercase)
                new FolderProjectionEnvelope("tenant-a", 2, Created("tenant-a", "folder-a", "Folder A")),
            ]);

        projection.Folders.Count.ShouldBe(1);
        projection.Get("tenant-a", "folder-a")!.DisplayName.ShouldBe("Folder A");
    }

    private static FolderCreated Created(
        string tenantId,
        string folderId,
        string displayName,
        string idempotencyKey = "idempotency-a")
        => new(
            tenantId,
            "organization-a",
            folderId,
            displayName,
            null,
            null,
            [],
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            "principal-a",
            "correlation-a",
            "task-a",
            idempotencyKey,
            $"fingerprint-{tenantId}-{folderId}-{idempotencyKey}");
}
