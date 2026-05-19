using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationCommandValidationTests
{
    [Fact]
    public void CreateFolderShouldEmitMetadataOnlyCreatedEventForEmptyState()
    {
        CreateFolder command = FolderCommandFactory.Create();

        FolderResult result = FolderAggregate.Handle(FolderState.Empty, command);

        result.Code.ShouldBe(FolderResultCode.Created);
        result.Events.Count.ShouldBe(1);
        FolderCreated created = result.Events.OfType<FolderCreated>().Single();
        created.ManagedTenantId.ShouldBe("tenant-a");
        created.FolderId.ShouldBe("folder-a");
        created.DisplayName.ShouldBe("Folder A");
        created.LifecycleState.ShouldBe(FolderLifecycleState.Active);
        created.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Unbound);
    }

    [Fact]
    public void CreatedFolderStateShouldReplayAsActiveLogicalFolder()
    {
        FolderResult result = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());

        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = FolderState.Empty.Apply(result.Events, streamName);

        state.IsCreated.ShouldBeTrue();
        state.LifecycleState.ShouldBe(FolderLifecycleState.Active);
        state.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Unbound);
        state.DisplayName.ShouldBe("Folder A");
    }

    [Theory]
    [InlineData("tenant-a", "folder:a", FolderResultCode.InvalidFolderId)]
    [InlineData("system", "folder-a", FolderResultCode.ReservedTenant)]
    [InlineData("tenant-a", "Folder-A", FolderResultCode.InvalidFolderId)]
    public void InvalidIdentityShouldRejectWithoutEvents(string tenantId, string folderId, FolderResultCode expectedCode)
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(managedTenantId: tenantId, folderId: folderId));

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("repo-secret-name")]
    [InlineData("github_pat_credential_material")]
    [InlineData("branch-main")]
    [InlineData("/tmp/raw-path")]
    [InlineData("diff --git a/file b/file")]
    [InlineData("generated context payload")]
    public void UnsafeMetadataShouldRejectBeforeEvents(string displayName)
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: displayName));

        result.Code.ShouldBe(FolderResultCode.InvalidFolderMetadata);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void DuplicateCreateShouldReturnDuplicateEvidenceWithoutAppendingSecondEvent()
    {
        FolderResult first = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = FolderState.Empty.Apply(first.Events, streamName);

        FolderResult second = FolderAggregate.Handle(
            state,
            FolderCommandFactory.Create(idempotencyKey: "idempotency-b"));

        second.Code.ShouldBe(FolderResultCode.DuplicateFolder);
        second.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AggregateLevelIdempotencyConflictShouldFireWhenSameKeyCarriesDifferentFingerprint()
    {
        // Exercises the FolderAggregate.Handle idempotency-conflict guard directly (the gate
        // catches conflicts via the repository ledger first, but the aggregate-level guard
        // is the last line of defense and was previously unreachable from the test suite).
        CreateFolder original = FolderCommandFactory.Create(idempotencyKey: "idempotency-a", displayName: "Folder A");
        FolderResult firstResult = FolderAggregate.Handle(FolderState.Empty, original);
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = FolderState.Empty.Apply(firstResult.Events, streamName);

        FolderResult conflict = FolderAggregate.Handle(
            state,
            FolderCommandFactory.Create(
                idempotencyKey: "idempotency-a",
                folderId: "folder-b",
                displayName: "Folder B"));

        conflict.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        conflict.Events.ShouldBeEmpty();
    }
}
