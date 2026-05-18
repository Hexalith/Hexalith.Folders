using System.Text.Json;
using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationMetadataLeakageTests
{
    [Theory]
    [InlineData("github_pat_credential_material")]
    [InlineData("provider-token-secret")]
    [InlineData("repo-internal-name")]
    [InlineData("branch-main")]
    [InlineData("C:/tenant/private/path.txt")]
    [InlineData("raw file content sentinel")]
    [InlineData("diff --git a/secret b/secret")]
    [InlineData("generated context payload")]
    [InlineData("person@example.test")]
    [InlineData("group display name")]
    [InlineData("unauthorized-resource-name")]
    public void UnsafeMetadataShouldNotAppearInStructuredResultOrEvents(string sentinel)
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: sentinel, description: sentinel, tags: [sentinel]));

        result.Code.ShouldBe(FolderResultCode.InvalidFolderMetadata);
        result.Events.ShouldBeEmpty();
        JsonSerializer.Serialize(result).ShouldNotContain(sentinel);
    }

    [Fact]
    public void AcceptedEventShouldContainOnlySafeMetadata()
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Operations", description: "review notes", tags: ["ops", "audit"]));

        string json = JsonSerializer.Serialize(result.Events);

        json.ShouldContain("Operations");
        json.ShouldContain("tenant-a");
        json.ShouldContain("folder-a");
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("repo", Case.Insensitive);
        json.ShouldNotContain("branch", Case.Insensitive);
        json.ShouldNotContain("diff", Case.Insensitive);
    }
}
