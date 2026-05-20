using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.FolderList;

public sealed class FolderArchiveProjectionReplayTests
{
    [Fact]
    public void ProjectionShouldPreserveMetadataAndMarkFolderArchived()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Created("tenant-a", "folder-a")),
                new FolderProjectionEnvelope("tenant-a", 2, Archived("tenant-a", "folder-a")),
            ]);

        FolderListItem item = projection.Get("tenant-a", "folder-a").ShouldNotBeNull();
        item.DisplayName.ShouldBe("Folder A");
        item.LifecycleState.ShouldBe(FolderLifecycleState.Archived);
        item.ArchiveReasonCode.ShouldBe(FolderArchiveReasonCode.CallerRequested);
        item.ArchiveCorrelationId.ShouldBe("correlation-archive-a");
        item.ArchiveTaskId.ShouldBe("task-archive-a");
        item.Sequence.ShouldBe(2);
    }

    [Fact]
    public void ProjectionShouldSkipArchiveEventWhenEnvelopeTenantDisagreesWithEventTenant()
    {
        FolderListProjection projection = FolderListProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Created("tenant-a", "folder-a")),
                new FolderProjectionEnvelope("tenant-a", 2, Archived("tenant-b", "folder-a")),
            ]);

        FolderListItem item = projection.Get("tenant-a", "folder-a").ShouldNotBeNull();
        item.LifecycleState.ShouldBe(FolderLifecycleState.Active);
        item.ArchiveReasonCode.ShouldBeNull();
    }

    private static FolderCreated Created(string tenantId, string folderId)
        => new(
            tenantId,
            "organization-a",
            folderId,
            "Folder A",
            "safe description",
            "folder-a",
            ["alpha"],
            FolderLifecycleState.Active,
            FolderRepositoryBindingState.Unbound,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-a",
            $"fingerprint-{tenantId}-{folderId}",
            new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero));

    private static FolderArchived Archived(string tenantId, string folderId)
        => new(
            tenantId,
            "organization-a",
            folderId,
            FolderArchiveReasonCode.CallerRequested,
            "principal-a",
            "correlation-archive-a",
            "task-archive-a",
            "idempotency-archive-a",
            $"fingerprint-archive-{tenantId}-{folderId}",
            new DateTimeOffset(2026, 5, 20, 8, 30, 0, TimeSpan.Zero));
}
