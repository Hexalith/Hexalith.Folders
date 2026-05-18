using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationIdempotencyTests
{
    [Fact]
    public void EquivalentReplayShouldReturnSameLogicalResultWithoutDuplicateEvents()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);
        CreateFolder command = FolderCommandFactory.Create(idempotencyKey: "idempotency-a");

        FolderResult first = gate.Handle(command, TenantEvidence(), AclEvidence());
        FolderResult second = gate.Handle(command, TenantEvidence(), AclEvidence());

        first.Code.ShouldBe(FolderResultCode.Created);
        second.Code.ShouldBe(FolderResultCode.IdempotentReplay);
        repository.EventsAppended.ShouldBe(1);
    }

    [Fact]
    public void SameKeyWithDifferentMetadataShouldRejectAsConflictBeforeAppend()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);
        CreateFolder original = FolderCommandFactory.Create(idempotencyKey: "idempotency-a", displayName: "Folder A");
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(original);
        repository.RecordIdempotency("tenant-a", "folder-a", "idempotency-a", validation.IdempotencyFingerprint);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(idempotencyKey: "idempotency-a", displayName: "Folder B"),
            TenantEvidence(),
            AclEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyConflict);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void SameOpaqueFolderIdInDifferentTenantsShouldUseDifferentStreamsAndDurableKeys()
    {
        RecordingFolderRepository repository = new();
        FolderCreateTenantGate gate = new(repository);

        FolderResult first = gate.Handle(
            FolderCommandFactory.Create(folderId: "folder-shared", idempotencyKey: "idempotency-a"),
            TenantEvidence("tenant-a"),
            AclEvidence("tenant-a"));
        string? firstKey = repository.LastDurableKey;

        FolderResult second = gate.Handle(
            FolderCommandFactory.Create(folderId: "folder-shared", idempotencyKey: "idempotency-a"),
            TenantEvidence("tenant-b"),
            AclEvidence("tenant-b"));
        string? secondKey = repository.LastDurableKey;

        first.Code.ShouldBe(FolderResultCode.Created);
        second.Code.ShouldBe(FolderResultCode.Created);
        firstKey.ShouldBe("tenant-a:folders:folder-shared|idempotency-a");
        secondKey.ShouldBe("tenant-b:folders:folder-shared|idempotency-a");
    }

    [Fact]
    public void IdempotencyUnavailableShouldFailClosedAfterAuthorizationBeforeAppend()
    {
        RecordingFolderRepository repository = new() { IdempotencyUnavailable = true };
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(FolderCommandFactory.Create(), TenantEvidence(), AclEvidence());

        result.Code.ShouldBe(FolderResultCode.IdempotencyUnavailable);
        repository.StreamNamesConstructed.ShouldBe(0);
        repository.StreamsLoaded.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public void AlreadyExistingFolderWithoutEquivalentReplayShouldReturnDuplicateEvidence()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.Create(idempotencyKey: "idempotency-b"),
            TenantEvidence(),
            AclEvidence());

        result.Code.ShouldBe(FolderResultCode.DuplicateFolder);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AppendConflictShouldReturnStableEvidenceWithoutSecondEvent()
    {
        RecordingFolderRepository repository = new() { SimulateAppendConflict = true };
        FolderCreateTenantGate gate = new(repository);

        FolderResult result = gate.Handle(FolderCommandFactory.Create(), TenantEvidence(), AclEvidence());

        result.Code.ShouldBe(FolderResultCode.AppendConflict);
        repository.EventsAppended.ShouldBe(0);
    }

    private static TenantAccessAuthorizationResult TenantEvidence(string tenantId = "tenant-a")
        => new(
            TenantAccessOutcome.Allowed,
            "allowed",
            tenantId,
            $"{tenantId}:7",
            new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(1),
            TenantProjectionFreshnessStatus.Fresh,
            "local-projection");

    private static FolderCreateAclEvidence AclEvidence(string tenantId = "tenant-a")
        => FolderCreateAclEvidence.Allowed(tenantId, "organization-a", "principal-a");
}
