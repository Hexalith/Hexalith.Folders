using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveStateTransitionTests
{
    [Fact]
    public void AlreadyArchivedFolderWithDifferentIdempotencyKeyShouldReturnStableStateResult()
    {
        FolderState archived = ArchivedState();

        FolderResult result = FolderAggregate.Handle(
            archived,
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-b"));

        result.Code.ShouldBe(FolderResultCode.AlreadyArchived);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AccessMutationAgainstArchivedFolderShouldRejectBeforeEvents()
    {
        FolderState archived = ArchivedState();

        FolderResult result = FolderAggregate.Handle(
            archived,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-archived"));

        result.Code.ShouldBe(FolderResultCode.AlreadyArchived);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ForeignArchiveEventShouldRejectDuringReplay()
    {
        FolderState active = CreatedState();
        FolderArchived foreign = new(
            "tenant-b",
            "organization-a",
            "folder-a",
            FolderArchiveReasonCode.CallerRequested,
            "principal-a",
            "correlation-a",
            "task-a",
            "idempotency-archive-a",
            "fingerprint-a",
            new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            active.Apply([foreign], FolderStreamName.Create("tenant-a", "folder-a")));

        exception.Message.ShouldContain(FolderResultCode.TenantMismatch.ToString());
    }

    [Theory]
    [InlineData(FolderActiveMutationCategory.FolderMetadata)]
    [InlineData(FolderActiveMutationCategory.FolderAcl)]
    [InlineData(FolderActiveMutationCategory.RepositoryBinding)]
    [InlineData(FolderActiveMutationCategory.Workspace)]
    [InlineData(FolderActiveMutationCategory.Lock)]
    [InlineData(FolderActiveMutationCategory.File)]
    [InlineData(FolderActiveMutationCategory.Commit)]
    [InlineData(FolderActiveMutationCategory.BranchRef)]
    [InlineData(FolderActiveMutationCategory.Provider)]
    [InlineData(FolderActiveMutationCategory.Task)]
    public void ActiveOnlyMutationGuardShouldRejectArchivedFolders(FolderActiveMutationCategory category)
    {
        FolderActiveMutationGuard.Evaluate(ArchivedState(), category).ShouldBe(FolderResultCode.AlreadyArchived);
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static FolderState ArchivedState()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState active = CreatedState();
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive());
        return active.Apply(archived.Events, streamName);
    }
}
