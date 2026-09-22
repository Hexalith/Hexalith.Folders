using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderStreamShapeTests
{
    [Fact]
    public void StreamNameShouldUseAuthoritativeTenantAndOpaqueFolderId()
    {
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");

        streamName.Value.ShouldBe("tenant-a:folders:folder-a");
    }

    [Theory]
    [InlineData("system", "folder-a", FolderResultCode.ReservedTenant)]
    [InlineData("System", "folder-a", FolderResultCode.InvalidTenant)] // uppercase fails canonical-segment regex before reserved-name check
    [InlineData(" system ", "folder-a", FolderResultCode.InvalidTenant)] // whitespace fails canonical-segment regex; consistent rejection code with non-reserved-but-whitespaced inputs
    [InlineData("tenant:a", "folder-a", FolderResultCode.InvalidTenant)]
    [InlineData("Tenant-A", "folder-a", FolderResultCode.InvalidTenant)]
    [InlineData("tenant-a", "folder:a", FolderResultCode.InvalidFolderId)]
    [InlineData("tenant-a", "Folder-A", FolderResultCode.InvalidFolderId)]
    public void InvalidSegmentsShouldRejectBeforeStreamNameIsCreated(
        string managedTenantId,
        string folderId,
        FolderResultCode expectedCode)
    {
        bool created = FolderStreamName.TryCreate(managedTenantId, folderId, out FolderStreamName? _, out FolderResultCode code);

        created.ShouldBeFalse();
        code.ShouldBe(expectedCode);
    }

    [Fact]
    public void UppercaseOpaqueIdentifierRemainsValidAfterSeamStateEnds()
    {
        const string folderId = "Folder_0000000001";
        PreauthorizedRequestContext.Begin(new PreauthorizedRequestState(
            "tenant-a",
            "user-a",
            folderId,
            FreshnessWatermark: null,
            OrganizationId: "org-a",
            CandidateActionToken: "manage_folder_access",
            HistoricalActionToken: "create_repository_backed_folder",
            DelegatorPrincipalId: null));
        try
        {
            PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(folderId).ShouldBeTrue();
        }
        finally
        {
            PreauthorizedRequestContext.End();
        }

        PreauthorizedRequestContext.Current.ShouldBeNull();
        FolderStreamName.TryCreate("tenant-a", folderId, out FolderStreamName? streamName, out FolderResultCode code)
            .ShouldBeTrue();
        code.ShouldBe(FolderResultCode.Created);
        streamName!.Value.ShouldBe($"tenant-a:folders:{folderId}");
        FolderCommandValidator.IsValidIdentifier(folderId).ShouldBeTrue();
        FolderStreamName.TryCreate("tenant-a", folderId, out FolderStreamName? rebuilt, out FolderResultCode replayCode)
            .ShouldBeTrue();
        replayCode.ShouldBe(FolderResultCode.Created);
        rebuilt!.Value.ShouldBe(streamName.Value);
    }

    [Fact]
    public void CreateShouldReportFolderIdParamNameWhenFolderIdIsInvalid()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => FolderStreamName.Create("tenant-a", "Folder-A"));

        exception.ParamName.ShouldBe("folderId");
    }

    [Fact]
    public void CreateShouldReportManagedTenantIdParamNameWhenTenantIsInvalid()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => FolderStreamName.Create("Tenant-A", "folder-a"));

        exception.ParamName.ShouldBe("managedTenantId");
    }
}
