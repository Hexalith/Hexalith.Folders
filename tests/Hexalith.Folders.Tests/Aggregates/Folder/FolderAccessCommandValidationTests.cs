using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessCommandValidationTests
{
    [Fact]
    public void GrantFolderAccessShouldEmitMetadataOnlyGrantEvent()
    {
        FolderState state = CreatedState();

        FolderResult result = FolderAggregate.Handle(state, FolderCommandFactory.GrantAccess());

        result.Code.ShouldBe(FolderResultCode.Accepted);
        result.Events.Count.ShouldBe(1);
        FolderAccessGranted granted = result.Events.OfType<FolderAccessGranted>().Single();
        granted.ManagedTenantId.ShouldBe("tenant-a");
        granted.FolderId.ShouldBe("folder-a");
        granted.PrincipalKind.ShouldBe(FolderAccessPrincipalKind.User);
        granted.PrincipalId.ShouldBe("target-principal-a");
        granted.Action.ShouldBe("read_metadata");
        granted.AccessSequence.ShouldBe(1);
    }

    [Fact]
    public void RevokeFolderAccessShouldEmitMetadataOnlyRevokeEventAndReplayAsRevoked()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = CreatedState();
        FolderResult grant = FolderAggregate.Handle(state, FolderCommandFactory.GrantAccess());
        FolderState grantedState = state.Apply(grant.Events, streamName);

        FolderResult revoke = FolderAggregate.Handle(grantedState, FolderCommandFactory.RevokeAccess());
        FolderState revokedState = grantedState.Apply(revoke.Events, streamName);

        revoke.Code.ShouldBe(FolderResultCode.Accepted);
        FolderAccessRevoked revoked = revoke.Events.OfType<FolderAccessRevoked>().Single();
        revoked.AccessSequence.ShouldBe(2);
        revokedState.HasFolderAccess(new FolderAccessEntryKey(
            "tenant-a",
            "folder-a",
            FolderAccessPrincipalKind.User,
            "target-principal-a",
            "read_metadata")).ShouldBeFalse();
        revokedState.AccessOverrides.Values.Single().OperationIntent.ShouldBe("revoke");
    }

    [Theory]
    [InlineData("Create_Folder", FolderResultCode.UnsupportedAction)]
    [InlineData("create_folder", FolderResultCode.UnsupportedAction)]
    [InlineData("read-metadata", FolderResultCode.UnsupportedAction)]
    [InlineData("principal@example.com", FolderResultCode.InvalidPrincipal)]
    [InlineData("delegated service", FolderResultCode.InvalidPrincipal)]
    public void UnsupportedActionOrInvalidPrincipalShouldRejectWithoutEvents(string value, FolderResultCode expectedCode)
    {
        GrantFolderAccess command = expectedCode == FolderResultCode.UnsupportedAction
            ? FolderCommandFactory.GrantAccess(action: value)
            : FolderCommandFactory.GrantAccess(principalId: value);

        FolderResult result = FolderAggregate.Handle(CreatedState(), command);

        result.Code.ShouldBe(expectedCode);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ExactDuplicateGrantOperationsShouldCollapseToOneEvent()
    {
        FolderAccessOperation operation = new(
            FolderAccessOperationIntent.Grant,
            FolderAccessPrincipalKind.Group,
            "group-a",
            "query_status");

        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.GrantAccess(operations: [operation, operation]));

        result.Code.ShouldBe(FolderResultCode.Accepted);
        result.Events.Count.ShouldBe(1);
    }

    [Fact]
    public void SameCommandGrantRevokeConflictShouldRejectBeforeEvents()
    {
        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.GrantAccess(
                operations:
                [
                    new FolderAccessOperation(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "target-principal-a", "read_metadata"),
                    new FolderAccessOperation(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, "target-principal-a", "read_metadata"),
                ]));

        result.Code.ShouldBe(FolderResultCode.ReplayConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AlreadyGrantedAccessShouldReturnAlreadyAppliedWithoutDuplicateEvent()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = CreatedState();
        FolderResult grant = FolderAggregate.Handle(state, FolderCommandFactory.GrantAccess());
        FolderState grantedState = state.Apply(grant.Events, streamName);

        FolderResult duplicate = FolderAggregate.Handle(
            grantedState,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-other"));

        duplicate.Code.ShouldBe(FolderResultCode.AlreadyApplied);
        duplicate.Events.ShouldBeEmpty();
    }

    [Fact]
    public void MissingGrantRevokeShouldReturnMissingEntryWithoutEvent()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.RevokeAccess());

        result.Code.ShouldBe(FolderResultCode.MissingEntry);
        result.Events.ShouldBeEmpty();
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
