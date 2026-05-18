using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationProjectionReplayTests
{
    [Fact]
    public void ProjectionShouldDeriveTenantScopeFromEnvelopeInsteadOfEventPayload()
    {
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

        projection.Contains("envelope-tenant", "folder-a").ShouldBeTrue();
        projection.Contains("payload-tenant", "folder-a").ShouldBeFalse();
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

    private static FolderCreated Created(string tenantId, string folderId, string displayName)
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
            "idempotency-a",
            $"fingerprint-{tenantId}-{folderId}");
}
