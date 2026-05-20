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

        // ACL mutation against an archived folder must be rejected with a category-neutral
        // state-transition code so ACL callers do not learn that the folder is specifically
        // archived. Direct ArchiveFolder commands still report AlreadyArchived.
        result.Code.ShouldBe(FolderResultCode.StateTransitionInvalid);
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

        // Apply on a stream-name/event-tenant mismatch is a stream-safety violation. Assert
        // exception type rather than message text — the message is for operators, not for
        // tests, and asserting on it couples the test to error-formatting drift.
        Should.Throw<InvalidOperationException>(() =>
            active.Apply([foreign], FolderStreamName.Create("tenant-a", "folder-a")));
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
        // Category-neutral state-transition rejection: callers of active-only mutations
        // (ACL/workspace/lock/etc.) must not learn from the result code that the folder is
        // specifically archived vs. in any other terminal state. ArchiveFolder itself
        // surfaces AlreadyArchived through its own response shape.
        FolderActiveMutationGuard.Evaluate(ArchivedState(), category).ShouldBe(FolderResultCode.StateTransitionInvalid);
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
    public void ActiveOnlyMutationGuardShouldAcceptActiveFolders(FolderActiveMutationCategory category)
    {
        // Symmetric coverage: every active-only mutation category must be Accepted on an
        // Active folder so future stories that wire those commands (Epic 3 repository
        // binding, Epic 4 workspace/lock/file/commit/branch/provider/task) inherit the guard
        // without per-category surprises. AC9 representative-fixture closure.
        FolderActiveMutationGuard.Evaluate(CreatedState(), category).ShouldBe(FolderResultCode.Accepted);
    }

    [Fact]
    public void ActiveOnlyMutationGuardShouldRejectUncreatedFolderAsNotFound()
    {
        // Pre-create state must produce FolderNotFound rather than StateTransitionInvalid so
        // future mutation pipelines short-circuit before constructing folder stream names,
        // projection keys, or audit subjects.
        FolderActiveMutationGuard.Evaluate(FolderState.Empty, FolderActiveMutationCategory.FolderAcl)
            .ShouldBe(FolderResultCode.FolderNotFound);
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
