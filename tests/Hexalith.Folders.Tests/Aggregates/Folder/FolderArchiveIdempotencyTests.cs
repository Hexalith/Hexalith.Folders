using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveIdempotencyTests
{
    [Fact]
    public void EquivalentReplayShouldReturnIdempotentReplayWithoutDuplicateArchiveEvent()
    {
        RecordingFolderRepository repository = SeededRepository();
        FolderArchiveTenantGate gate = new(repository);
        ArchiveFolder command = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a");

        FolderResult first = gate.Handle(command, TenantEvidence(), AclEvidence(), PolicyEvidence());
        FolderResult second = gate.Handle(command, TenantEvidence(), AclEvidence(), PolicyEvidence());

        first.Code.ShouldBe(FolderResultCode.Accepted);
        second.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.EventsAppended.ShouldBe(1);
    }

    [Fact]
    public void SameKeyWithDifferentArchiveReasonShouldRejectAsConflictBeforeLoad()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder original = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a", archiveReasonCode: "caller_requested");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(original);
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-archive-a", ArchiveDecisionFingerprint(original, validation));
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a", archiveReasonCode: "operator_review"),
            TenantEvidence(),
            AclEvidence(),
            PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void SameKeyWithDifferentCorrelationShouldRejectAsConflictBeforeLoad()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder original = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a", correlationId: "correlation-a");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(original);
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-archive-a", ArchiveDecisionFingerprint(original, validation));
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a", correlationId: "correlation-b"),
            TenantEvidence(),
            AclEvidence(),
            PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void SameKeyWithDifferentPolicyVersionShouldRejectAsConflictBeforeLoad()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder command = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-archive-a", ArchiveDecisionFingerprint(command, validation));
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            command,
            TenantEvidence(),
            AclEvidence(),
            PolicyEvidence(policyVersion: "policy-v2"));

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void SameKeyWithDifferentFreshnessWatermarkShouldRejectAsConflictBeforeLoad()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder command = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-archive-a", ArchiveDecisionFingerprint(command, validation));
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            command,
            TenantEvidence(projectionWatermark: "tenant-a:8"),
            AclEvidence(),
            PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.StreamsLoaded.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void AppendConflictShouldRereadAndReturnIdempotentReplayWhenRacingEquivalentArchiveWon()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder command = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        string decisionFingerprint = ArchiveDecisionFingerprint(command, validation);
        repository.SimulateAppendConflict = true;
        repository.ConcurrentAppendEvents =
        [
            new FolderArchived(
                "tenant-a",
                "organization-a",
                "folder-a",
                validation.ArchiveReasonCode!.Value,
                "principal-a",
                "correlation-a",
                "task-a",
                "idempotency-archive-a",
                decisionFingerprint,
                new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero)),
        ];
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(command, TenantEvidence(), AclEvidence(), PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.EventsAppended.ShouldBe(0);
        repository.ConcurrentEventsApplied.ShouldBe(1);
    }

    [Fact]
    public void AppendConflictShouldReturnConflictWhenNoRacingArchiveIsMaterialized()
    {
        RecordingFolderRepository repository = SeededRepository();
        repository.SimulateAppendConflict = true;
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a"),
            TenantEvidence(),
            AclEvidence(),
            PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.AppendConflict);
        repository.EventsAppended.ShouldBe(0);
        repository.ConcurrentEventsApplied.ShouldBe(0);
    }

    [Fact]
    public void AppendConflictShouldReturnIdempotencyConflictWhenRacingSameKeyCarriesDifferentFingerprint()
    {
        RecordingFolderRepository repository = SeededRepository();
        ArchiveFolder command = FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        repository.SimulateAppendConflict = true;
        repository.ConcurrentAppendEvents =
        [
            new FolderArchived(
                "tenant-a",
                "organization-a",
                "folder-a",
                validation.ArchiveReasonCode!.Value,
                "principal-a",
                "correlation-b",
                "task-a",
                "idempotency-archive-a",
                "different-fingerprint",
                new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero)),
        ];
        FolderArchiveTenantGate gate = new(repository);

        FolderResult result = gate.Handle(command, TenantEvidence(), AclEvidence(), PolicyEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.EventsAppended.ShouldBe(0);
        repository.ConcurrentEventsApplied.ShouldBe(1);
    }

    [Fact]
    public void SameOpaqueFolderIdInDifferentTenantsShouldUseDifferentArchiveLedgerKeys()
    {
        RecordingFolderRepository repository = new();
        Seed(repository, "tenant-a", "folder-shared");
        Seed(repository, "tenant-b", "folder-shared");
        FolderArchiveTenantGate gate = new(repository);

        FolderResult first = gate.Handle(
            FolderCommandFactory.Archive(folderId: "folder-shared", idempotencyKey: "idempotency-archive-a"),
            TenantEvidence("tenant-a"),
            AclEvidence("tenant-a", "folder-shared"),
            PolicyEvidence("tenant-a", "folder-shared"));
        string? firstKey = repository.LastDurableKey;

        FolderResult second = gate.Handle(
            FolderCommandFactory.Archive(managedTenantId: "tenant-b", folderId: "folder-shared", idempotencyKey: "idempotency-archive-a"),
            TenantEvidence("tenant-b"),
            AclEvidence("tenant-b", "folder-shared"),
            PolicyEvidence("tenant-b", "folder-shared"));
        string? secondKey = repository.LastDurableKey;

        first.Code.ShouldBe(FolderResultCode.Accepted);
        second.Code.ShouldBe(FolderResultCode.Accepted);
        firstKey.ShouldBe("tenant-a:folders:folder-shared|idempotency-archive-a");
        secondKey.ShouldBe("tenant-b:folders:folder-shared|idempotency-archive-a");
    }

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        Seed(repository, "tenant-a", "folder-a");
        return repository;
    }

    private static void Seed(RecordingFolderRepository repository, string tenantId, string folderId)
    {
        FolderStreamName streamName = FolderStreamName.Create(tenantId, folderId);
        FolderResult created = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(managedTenantId: tenantId, folderId: folderId));
        repository.Seed(streamName, created.Events);
    }

    private static TenantAccessAuthorizationResult TenantEvidence(string tenantId = "tenant-a", string? projectionWatermark = null)
        => new(
            TenantAccessOutcome.Allowed,
            "allowed",
            tenantId,
            projectionWatermark ?? $"{tenantId}:7",
            new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");

    private static FolderArchiveAclEvidence AclEvidence(string tenantId = "tenant-a", string folderId = "folder-a")
        => FolderArchiveAclEvidence.Allowed(tenantId, "organization-a", folderId, "principal-a");

    private static FolderArchivePolicyEvidence PolicyEvidence(
        string tenantId = "tenant-a",
        string folderId = "folder-a",
        string policyVersion = "policy-v1")
        => FolderArchivePolicyEvidence.Allowed(tenantId, "organization-a", folderId, policyVersion);

    private static string ArchiveDecisionFingerprint(ArchiveFolder command, FolderCommandValidationResult validation)
        => FolderCommandValidator.BindArchiveDecisionFingerprint(
            command,
            validation.IdempotencyFingerprint!,
            "policy-v1",
            "tenant-a:7");
}
