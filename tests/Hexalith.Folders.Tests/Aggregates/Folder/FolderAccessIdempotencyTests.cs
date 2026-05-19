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
            FolderCommandFactory.GrantAccess(folderId: "folder-shared", idempotencyKey: "same-key"),
            TenantEvidence("tenant-a"),
            AclEvidence("tenant-a", folderId: "folder-shared"));
        string? firstKey = repository.LastDurableKey;

        FolderResult second = gate.Handle(
            FolderCommandFactory.GrantAccess(folderId: "folder-shared", idempotencyKey: "same-key"),
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
                1),
        ];
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(FolderCommandFactory.GrantAccess(), TenantEvidence(), AclEvidence());

        result.Code.ShouldBe(FolderResultCode.AlreadyApplied);
        repository.EventsAppended.ShouldBe(1);
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
