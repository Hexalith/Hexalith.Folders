using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessIdempotencyTests
{
    [Fact]
    public void EquivalentReplayShouldReturnAlreadyAppliedWithoutDuplicateEvents()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderAccessTenantGate gate = new(repository);
        GrantFolderAccess command = FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-a");

        FolderResult first = gate.Handle(command, TenantEvidence(), AclEvidence());
        FolderResult second = gate.Handle(command, TenantEvidence(), AclEvidence());

        first.Code.ShouldBe(FolderResultCode.Accepted);
        second.Code.ShouldBe(FolderResultCode.AlreadyApplied);
        repository.EventsAppended.ShouldBe(1);
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("principal")]
    [InlineData("action")]
    [InlineData("folder")]
    public void SameIdempotencyKeyWithChangedSemanticPayloadShouldRejectConflict(string changed)
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderAccessTenantGate gate = new(repository);
        GrantFolderAccess original = FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-a");
        gate.Handle(original, TenantEvidence(), AclEvidence());
        if (changed == "folder")
        {
            FolderCommandValidationResult validation = FolderCommandValidator.Validate(original);
            repository.RecordIdempotency("tenant-a", "folder-b", "idempotency-access-a", validation.IdempotencyFingerprint!);
        }

        IFolderAccessCommand changedCommand = changed switch
        {
            "revoke" => FolderCommandFactory.RevokeAccess(idempotencyKey: "idempotency-access-a"),
            "principal" => FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-a", principalId: "target-principal-b"),
            "action" => FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-a", action: "query_status"),
            "folder" => FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-a", folderId: "folder-b"),
            _ => throw new InvalidOperationException("Unsupported test case."),
        };

        FolderResult result = gate.Handle(changedCommand, TenantEvidence(), AclEvidence(folderId: changed == "folder" ? "folder-b" : "folder-a"));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameFolderPrincipalKeyAndActionInDifferentTenantsShouldNotDedupe()
    {
        RecordingFolderRepository repository = new();
        SeedFolder(repository, "tenant-a", "folder-shared");
        SeedFolder(repository, "tenant-b", "folder-shared");
        FolderAccessTenantGate gate = new(repository);

        FolderResult first = gate.Handle(
            FolderCommandFactory.GrantAccess(managedTenantId: "tenant-a", folderId: "folder-shared", idempotencyKey: "same-key"),
            TenantEvidence("tenant-a"),
            AclEvidence("tenant-a", folderId: "folder-shared"));
        string? firstKey = repository.LastDurableKey;

        FolderResult second = gate.Handle(
            FolderCommandFactory.GrantAccess(managedTenantId: "tenant-b", folderId: "folder-shared", idempotencyKey: "same-key"),
            TenantEvidence("tenant-b"),
            AclEvidence("tenant-b", folderId: "folder-shared"));
        string? secondKey = repository.LastDurableKey;

        first.Code.ShouldBe(FolderResultCode.Accepted);
        second.Code.ShouldBe(FolderResultCode.Accepted);
        firstKey.ShouldBe("tenant-a:folders:folder-shared|same-key");
        secondKey.ShouldBe("tenant-b:folders:folder-shared|same-key");
    }

    [Fact]
    public void IdempotencyUnavailableShouldFailClosedBeforeLoadOrAppend()
    {
        RecordingFolderRepository repository = SeededRepository();
        repository.IdempotencyUnavailable = true;
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(FolderCommandFactory.GrantAccess(), TenantEvidence(), AclEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyUnavailable);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void AppendConflictShouldRereadAndReturnAlreadyAppliedWhenConcurrentGrantWon()
    {
        RecordingFolderRepository repository = SeededRepository();
        repository.SimulateAppendConflict = true;
        repository.ConcurrentAppendEvents =
        [
            new FolderAccessGranted(
                "tenant-a",
                "organization-a",
                "folder-a",
                FolderAccessPrincipalKind.User,
                "target-principal-a",
                "read_metadata",
                "principal-a",
                "correlation-other",
                "task-other",
                "idempotency-other",
                "fingerprint-other",
                1,
                new DateTimeOffset(2026, 5, 19, 8, 0, 0, TimeSpan.Zero)),
        ];
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(FolderCommandFactory.GrantAccess(), TenantEvidence(), AclEvidence());

        result.Code.ShouldBe(FolderResultCode.AlreadyApplied);
        // The racing event was applied to state via the test harness (ConcurrentEventsApplied),
        // not by the gate. Asserting EventsAppended==0 proves the gate's re-evaluation made
        // the original grant a no-op rather than silently appending after losing the race.
        repository.EventsAppended.ShouldBe(0);
        repository.ConcurrentEventsApplied.ShouldBe(1);
    }

    [Fact]
    public void AppendConflictShouldReturnAppendConflictWhenRacingEventLeavesWorkToDo()
    {
        // D3 lock-in: the original Grant for principal-X races against a winning Revoke
        // for a DIFFERENT tuple (principal-Y). After the racing revoke is applied,
        // re-evaluation against the refreshed state still wants to emit Grant(X) — the
        // gate must surface `AppendConflict` so the caller can re-prepare and resubmit
        // rather than silently auto-retrying the append.
        RecordingFolderRepository repository = SeededRepository();
        FolderAccessTenantGate gate = new(repository);

        // Pre-grant principal-y so the racing revoke is meaningful (it has something to undo).
        gate.Handle(
            FolderCommandFactory.GrantAccess(
                principalId: "target-principal-y",
                idempotencyKey: "idempotency-priorgrant"),
            TenantEvidence(),
            AclEvidence());

        // Configure the race: the next append will fail with AppendConflict, and the
        // simulated concurrent event is a Revoke for principal-y.
        repository.SimulateAppendConflict = true;
        repository.ConcurrentAppendEvents =
        [
            new FolderAccessRevoked(
                "tenant-a",
                "organization-a",
                "folder-a",
                FolderAccessPrincipalKind.User,
                "target-principal-y",
                "read_metadata",
                "principal-a",
                "correlation-revoke",
                "task-revoke",
                "idempotency-revoke",
                "fingerprint-revoke",
                2,
                new DateTimeOffset(2026, 5, 19, 9, 0, 0, TimeSpan.Zero)),
        ];

        // Submit a grant for a DIFFERENT principal so the racing revoke does not make the
        // grant a no-op. Re-evaluation against post-revoke state still wants to emit
        // Grant(principal-x), so the gate signals AppendConflict.
        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(
                principalId: "target-principal-x",
                idempotencyKey: "idempotency-retrygrant"),
            TenantEvidence(),
            AclEvidence());

        result.Code.ShouldBe(FolderResultCode.AppendConflict);
        repository.ConcurrentEventsApplied.ShouldBe(1);
    }

    [Fact]
    public void IdempotentReplayReportsAlreadyAppliedEvenIfStateLaterRevoked()
    {
        // P20 lock-in: standard "same idempotency key + payload → same response" semantics.
        // The first Grant succeeds. A subsequent Revoke under a different key tears down the
        // tuple. A retried Grant with the original key reports AlreadyApplied because the
        // ledger still maps that key→fingerprint, NOT because the access is currently granted.
        // Callers that need state-sensitive semantics must issue a fresh idempotency key.
        RecordingFolderRepository repository = SeededRepository();
        FolderAccessTenantGate gate = new(repository);

        gate.Handle(
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant"),
            TenantEvidence(),
            AclEvidence());

        gate.Handle(
            FolderCommandFactory.RevokeAccess(idempotencyKey: "idempotency-revoke"),
            TenantEvidence(),
            AclEvidence());

        FolderResult retry = gate.Handle(
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-grant"),
            TenantEvidence(),
            AclEvidence());

        retry.Code.ShouldBe(FolderResultCode.AlreadyApplied);
        retry.Events.ShouldBeEmpty();
    }

    internal static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        SeedFolder(repository, "tenant-a", "folder-a");
        return repository;
    }

    internal static void SeedFolder(RecordingFolderRepository repository, string tenantId, string folderId)
    {
        FolderStreamName streamName = FolderStreamName.Create(tenantId, folderId);
        FolderResult created = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(managedTenantId: tenantId, folderId: folderId));
        repository.Seed(streamName, created.Events);
    }

    internal static TenantAccessAuthorizationResult TenantEvidence(string tenantId = "tenant-a")
        => FolderAccessTenantEvidenceGateTests.Evidence(TenantAccessOutcome.Allowed, tenantId);

    internal static FolderAccessAclEvidence AclEvidence(string tenantId = "tenant-a", string folderId = "folder-a")
        => FolderAccessAclEvidence.Allowed(tenantId, "organization-a", folderId, "principal-a");
}
