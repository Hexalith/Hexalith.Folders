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
    [InlineData("")]
    [InlineData(" ")]
    public void SegmentIdentifierShouldRejectCaseCharsetAndBlankValues(string value)
        => FolderCanonicalSegmentIdentifier.IsValid(value).ShouldBeFalse();

    [Fact]
    public void SegmentIdentifierShouldRejectValuesLongerThan128()
        => FolderCanonicalSegmentIdentifier.IsValid(new string('a', 129)).ShouldBeFalse();

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
    [InlineData("")]
    [InlineData(" ")]
    public void PathIdentifierShouldRejectInvalidCharsetAndBlankValues(string value)
        => FolderCanonicalPathIdentifier.IsValid(value).ShouldBeFalse();

    [Fact]
    public void PathIdentifierShouldRejectValuesLongerThan256()
        => FolderCanonicalPathIdentifier.IsValid(new string('A', 257)).ShouldBeFalse();
}
