using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderCanonicalIdentifierTests
{
    [Theory]
    [InlineData("folder_1")]
    [InlineData("a")]
    [InlineData("workspace.1-id")]
    public void SegmentIdentifierShouldAcceptLowercaseSafeValues(string value)
        => FolderCanonicalSegmentIdentifier.IsValid(value).ShouldBeTrue();

    [Fact]
    public void SegmentIdentifierShouldAcceptMaximumLength()
        => FolderCanonicalSegmentIdentifier.IsValid(new string('a', 128)).ShouldBeTrue();

    [Theory]
    [InlineData("Folder_1")]
    [InlineData("folder/1")]
    [InlineData("folder-a\n")]
    [InlineData("folder-a\r\n")]
    [InlineData("folder\na")]
    [InlineData("")]
    [InlineData(" ")]
    public void SegmentIdentifierShouldRejectCaseCharsetAndBlankValues(string value)
        => FolderCanonicalSegmentIdentifier.IsValid(value).ShouldBeFalse();

    [Fact]
    public void SegmentIdentifierShouldRejectValuesLongerThan128()
        => FolderCanonicalSegmentIdentifier.IsValid(new string('a', 129)).ShouldBeFalse();

    [Theory]
    [InlineData("folder_1")]
    [InlineData("a")]
    [InlineData("workspace.1-id")]
    public void LowercaseSegmentShouldAcceptLowercaseSafeValues(string value)
        => FolderCanonicalSegmentIdentifier.IsLowercaseSegment(value).ShouldBeTrue();

    [Fact]
    public void LowercaseSegmentShouldAcceptMaximumLength()
        => FolderCanonicalSegmentIdentifier.IsLowercaseSegment(new string('a', 128)).ShouldBeTrue();

    [Theory]
    [InlineData("Folder_1")]
    [InlineData("folder/1")]
    [InlineData("folder-a\n")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void LowercaseSegmentShouldRejectCaseCharsetBlankAndNullValues(string? value)
        => FolderCanonicalSegmentIdentifier.IsLowercaseSegment(value).ShouldBeFalse();

    [Fact]
    public void LowercaseSegmentShouldRejectValuesLongerThan128()
        => FolderCanonicalSegmentIdentifier.IsLowercaseSegment(new string('a', 129)).ShouldBeFalse();

    [Fact]
    public void LowercaseSegmentShouldNotGrantTheOpaqueIdentifierAllowance()
    {
        const string opaqueUlid = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

        FolderCanonicalSegmentIdentifier.IsValid(opaqueUlid).ShouldBeTrue();
        FolderCanonicalSegmentIdentifier.IsLowercaseSegment(opaqueUlid).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Folder_1")]
    [InlineData("folder-A")]
    [InlineData("A")]
    public void PathIdentifierShouldAcceptMixedCaseWithin256(string value)
        => FolderCanonicalPathIdentifier.IsValid(value).ShouldBeTrue();

    [Fact]
    public void PathIdentifierShouldAcceptMaximumLength()
        => FolderCanonicalPathIdentifier.IsValid(new string('A', 256)).ShouldBeTrue();

    [Theory]
    [InlineData("_invalid")]
    [InlineData("folder.1")]
    [InlineData("Folder_A\n")]
    [InlineData("Folder_A\r\n")]
    [InlineData("Folder\nA")]
    [InlineData("")]
    [InlineData(" ")]
    public void PathIdentifierShouldRejectInvalidCharsetAndBlankValues(string value)
        => FolderCanonicalPathIdentifier.IsValid(value).ShouldBeFalse();

    [Fact]
    public void PathIdentifierShouldRejectValuesLongerThan256()
        => FolderCanonicalPathIdentifier.IsValid(new string('A', 257)).ShouldBeFalse();
}
