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
    [InlineData(" read_metadata", FolderResultCode.UnsupportedAction)]
    [InlineData("read_metadata ", FolderResultCode.UnsupportedAction)]
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

        // ValidateOperation catches the cross-intent op first and reports it as ReplayConflict
        // (intent != requiredIntent for a Grant command).
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

    [Fact]
    public void RevokeBatchShouldSkipAbsentTuplesAndEmitEventsForPresentOnes()
    {
        // D1 lock-in: Revoke is per-op symmetric with Grant — present tuples emit revoke
        // events, absent tuples are silently skipped, and only an empty event list returns
        // MissingEntry. Callers see the actual delta via the emitted events.
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = CreatedState();

        // Pre-grant principal-a so the revoke batch has one present + one absent tuple.
        FolderResult grant = FolderAggregate.Handle(
            state,
            FolderCommandFactory.GrantAccess(
                principalId: "target-principal-a",
                idempotencyKey: "idempotency-grant-a"));
        FolderState granted = state.Apply(grant.Events, streamName);

        FolderResult revoke = FolderAggregate.Handle(
            granted,
            FolderCommandFactory.RevokeAccess(
                idempotencyKey: "idempotency-revoke",
                operations:
                [
                    new FolderAccessOperation(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, "target-principal-a", "read_metadata"),
                    new FolderAccessOperation(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, "target-principal-absent", "read_metadata"),
                ]));

        revoke.Code.ShouldBe(FolderResultCode.Accepted);
        revoke.Events.Count.ShouldBe(1);
        revoke.Events.OfType<FolderAccessRevoked>().Single().PrincipalId.ShouldBe("target-principal-a");
    }

    [Fact]
    public void RevokeBatchWithAllAbsentTuplesShouldReturnMissingEntry()
    {
        // D1 lock-in: when no requested tuple was present, the whole-command outcome is
        // MissingEntry, mirroring Grant's AlreadyApplied when every tuple was already
        // granted. Symmetry across the two operations.
        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.RevokeAccess(
                operations:
                [
                    new FolderAccessOperation(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, "target-principal-a", "read_metadata"),
                    new FolderAccessOperation(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.User, "target-principal-b", "read_metadata"),
                ]));

        result.Code.ShouldBe(FolderResultCode.MissingEntry);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void GrantBatchShouldSkipAlreadyGrantedTuplesAndEmitEventsForNewOnes()
    {
        // D1 lock-in mirror: Grant skips already-granted tuples and emits events for new
        // ones; both sides of the asymmetry are now explicitly covered.
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState state = CreatedState();
        FolderResult firstGrant = FolderAggregate.Handle(
            state,
            FolderCommandFactory.GrantAccess(principalId: "target-principal-a", idempotencyKey: "idempotency-grant-a"));
        FolderState afterFirst = state.Apply(firstGrant.Events, streamName);

        FolderResult mixed = FolderAggregate.Handle(
            afterFirst,
            FolderCommandFactory.GrantAccess(
                idempotencyKey: "idempotency-mixed",
                operations:
                [
                    new FolderAccessOperation(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "target-principal-a", "read_metadata"),
                    new FolderAccessOperation(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "target-principal-b", "read_metadata"),
                ]));

        mixed.Code.ShouldBe(FolderResultCode.Accepted);
        mixed.Events.Count.ShouldBe(1);
        mixed.Events.OfType<FolderAccessGranted>().Single().PrincipalId.ShouldBe("target-principal-b");
    }

    [Fact]
    public void ConcurrentGrantsOffStaleStateShouldEmitOverlappingAccessSequenceLocally()
    {
        // P21 lock-in: the aggregate computes nextSequence from in-memory state. Two
        // writers handling commands against the SAME starting state will independently
        // emit events with overlapping sequences. The optimistic-concurrency layer in the
        // repository is responsible for rejecting the second write — the aggregate does
        // not enforce sequence uniqueness across stale views.
        FolderState created = CreatedState();
        FolderResult firstGrant = FolderAggregate.Handle(
            created,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant-a"));
        FolderResult secondGrant = FolderAggregate.Handle(
            created,
            FolderCommandFactory.GrantAccess(
                principalId: "target-principal-b",
                idempotencyKey: "idempotency-grant-b"));

        firstGrant.Events.OfType<FolderAccessGranted>().Single().AccessSequence.ShouldBe(1);
        secondGrant.Events.OfType<FolderAccessGranted>().Single().AccessSequence.ShouldBe(1);
    }

    [Fact]
    public void FingerprintShouldEqualAcrossExactDuplicateOperationDedup()
    {
        // P23 lock-in: fingerprint is computed against the post-dedup canonical operations.
        // A command with exact duplicate operations produces the same fingerprint as a
        // command with a single copy. Future dedup-rule changes must keep this invariant
        // so existing idempotency-ledger entries continue to equate.
        FolderAccessOperation operation = new(
            FolderAccessOperationIntent.Grant,
            FolderAccessPrincipalKind.Group,
            "group-a",
            "query_status");

        FolderCommandValidationResult single = FolderCommandValidator.Validate(
            FolderCommandFactory.GrantAccess(operations: [operation]));
        FolderCommandValidationResult duplicate = FolderCommandValidator.Validate(
            FolderCommandFactory.GrantAccess(operations: [operation, operation]));

        single.IdempotencyFingerprint.ShouldBe(duplicate.IdempotencyFingerprint);
    }

    [Fact]
    public void IsSupportedShouldRejectLeadingAndTrailingWhitespace()
    {
        // P24 lock-in: the action vocabulary is strict; surrounding whitespace is not
        // canonicalized into a supported token. Callers that submit `" read_metadata"`
        // must see UnsupportedAction rather than have the value silently trimmed.
        FolderAccessAction.IsSupported(" read_metadata").ShouldBeFalse();
        FolderAccessAction.IsSupported("read_metadata ").ShouldBeFalse();
        FolderAccessAction.IsSupported("\tread_metadata").ShouldBeFalse();
        FolderAccessAction.IsSupported("read_metadata").ShouldBeTrue();
    }

    [Fact]
    public void IsSafeEvidenceIdentifierShouldAcceptIdentifiersContainingFalsePositiveSubstrings()
    {
        // P3 lock-in: substring matching of identifier-shaped terms (`auth`, `display`,
        // `branch`, `email`) silently nullified valid tenant IDs. The anchored-term
        // matcher must let through identifiers that merely contain those substrings.
        FolderCommandValidator.IsSafeEvidenceIdentifier("tenant-authority").ShouldBeTrue();
        FolderCommandValidator.IsSafeEvidenceIdentifier("acme-displayservice").ShouldBeTrue();
        FolderCommandValidator.IsSafeEvidenceIdentifier("tenant-branchless").ShouldBeTrue();
        FolderCommandValidator.IsSafeEvidenceIdentifier("tenant-emailing").ShouldBeTrue();

        // But the whole-word matches still block.
        FolderCommandValidator.IsSafeEvidenceIdentifier("tenant-auth").ShouldBeFalse();
        FolderCommandValidator.IsSafeEvidenceIdentifier("display-prod").ShouldBeFalse();
        FolderCommandValidator.IsSafeEvidenceIdentifier("acme.email.svc").ShouldBeFalse();
        FolderCommandValidator.IsSafeEvidenceIdentifier("system_branch_a").ShouldBeFalse();
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
