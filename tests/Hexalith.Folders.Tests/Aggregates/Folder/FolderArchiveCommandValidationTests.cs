using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveCommandValidationTests
{
    [Fact]
    public void ArchiveFolderShouldEmitMetadataOnlyArchivedEventForActiveFolder()
    {
        FolderState state = CreatedState();
        ArchiveFolder command = FolderCommandFactory.Archive();

        FolderResult result = FolderAggregate.Handle(state, command);

        result.Code.ShouldBe(FolderResultCode.Accepted);
        result.Events.Count.ShouldBe(1);
        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();
        archived.ManagedTenantId.ShouldBe("tenant-a");
        archived.OrganizationId.ShouldBe("organization-a");
        archived.FolderId.ShouldBe("folder-a");
        archived.ArchiveReasonCode.ShouldBe(FolderArchiveReasonCode.CallerRequested);
        archived.ActorPrincipalId.ShouldBe("principal-a");
        archived.CorrelationId.ShouldBe("correlation-a");
        archived.TaskId.ShouldBe("task-a");
        archived.IdempotencyKey.ShouldBe("idempotency-archive-a");
    }

    [Fact]
    public void ArchivedFolderStateShouldReplayAsArchivedWithReasonEvidence()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState active = CreatedState();
        FolderResult archived = FolderAggregate.Handle(active, FolderCommandFactory.Archive());

        FolderState state = active.Apply(archived.Events, streamName);

        state.LifecycleState.ShouldBe(FolderLifecycleState.Archived);
        state.ArchiveReasonCode.ShouldBe(FolderArchiveReasonCode.CallerRequested);
    }

    [Theory]
    [InlineData("v2", "caller_requested")]
    [InlineData("v1", "unsupported_reason")]
    [InlineData("v1", " caller_requested")]
    public void UnsupportedArchiveSchemaOrReasonShouldRejectBeforeEvents(
        string requestSchemaVersion,
        string archiveReasonCode)
    {
        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.Archive(
                requestSchemaVersion: requestSchemaVersion,
                archiveReasonCode: archiveReasonCode));

        result.Code.ShouldBe(FolderResultCode.ValidationFailed);
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void ArchiveFingerprintShouldIncludeReasonCodeAndSchema()
    {
        FolderCommandValidationResult callerRequested = FolderCommandValidator.Validate(
            FolderCommandFactory.Archive(archiveReasonCode: "caller_requested"));
        FolderCommandValidationResult operatorReview = FolderCommandValidator.Validate(
            FolderCommandFactory.Archive(archiveReasonCode: "operator_review"));

        callerRequested.IsAccepted.ShouldBeTrue();
        operatorReview.IsAccepted.ShouldBeTrue();
        callerRequested.IdempotencyFingerprint.ShouldNotBe(operatorReview.IdempotencyFingerprint);
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
