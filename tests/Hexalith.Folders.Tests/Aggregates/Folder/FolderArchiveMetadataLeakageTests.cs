using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveMetadataLeakageTests
{
    [Theory]
    [InlineData("github_pat_credential_material")]
    [InlineData("principal-token")]
    [InlineData("principal@example.com")]
    public void UnsafeActorEvidenceShouldRejectWithoutEchoingUnsafeIdentifier(string unsafeActor)
    {
        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.Archive(actorPrincipalId: unsafeActor));

        result.Code.ShouldBe(FolderResultCode.MalformedEvidence);
        result.ActorPrincipalId.ShouldBeNull();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AcceptedArchiveEventShouldCarryOnlyMetadataEvidence()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());

        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();
        string serialized = string.Join(
            '|',
            archived.ManagedTenantId,
            archived.OrganizationId,
            archived.FolderId,
            archived.ArchiveReasonCode,
            archived.ActorPrincipalId,
            archived.CorrelationId,
            archived.TaskId,
            archived.IdempotencyKey);

        serialized.ShouldNotContain("token", Case.Insensitive);
        serialized.ShouldNotContain("secret", Case.Insensitive);
        serialized.ShouldNotContain("credential", Case.Insensitive);
        serialized.ShouldNotContain("repository", Case.Insensitive);
        serialized.ShouldNotContain("diff --git", Case.Insensitive);
        serialized.ShouldNotContain("://", Case.Sensitive);
        serialized.ShouldNotContain("\\", Case.Sensitive);
        serialized.ShouldNotContain("/", Case.Sensitive);
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
