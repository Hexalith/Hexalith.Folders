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

    [Theory]
    [InlineData("folder_1")]
    [InlineData("Workspace-A")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAV")]
    public void SafeDiagnosticIdShouldAcceptNonSensitivePathIdentifiers(string value)
        => FolderCanonicalPathIdentifier.IsSafeDiagnosticId(value).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("folder.1")]
    [InlineData("Folder_A\n")]
    public void SafeDiagnosticIdShouldRejectInvalidPathIdentifiers(string? value)
        => FolderCanonicalPathIdentifier.IsSafeDiagnosticId(value).ShouldBeFalse();

    [Fact]
    public void SafeDiagnosticIdShouldAcceptMaximumLength()
        => FolderCanonicalPathIdentifier.IsSafeDiagnosticId(new string('A', 256)).ShouldBeTrue();

    [Fact]
    public void SafeDiagnosticIdShouldRejectValuesLongerThan256()
        => FolderCanonicalPathIdentifier.IsSafeDiagnosticId(new string('A', 257)).ShouldBeFalse();

    [Theory]
    [InlineData("access_token_1")]
    [InlineData("client-Secret")]
    [InlineData("repo_folders")]
    [InlineData("Installation-42")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz")]
    public void SafeDiagnosticIdShouldRejectSecretLookingPathIdentifiers(string value)
    {
        FolderCanonicalPathIdentifier.IsValid(value).ShouldBeTrue();
        FolderCanonicalPathIdentifier.IsSafeDiagnosticId(value).ShouldBeFalse();
    }

    [Theory]
    [InlineData("corr_01HZY7Z6N7J4Q2X8Y9V0A1B2C3")]
    [InlineData("Correlation-A")]
    [InlineData("1")]
    public void SanitizeCorrelationIdShouldEchoSafeValues(string value)
        => FolderCanonicalPathIdentifier.SanitizeCorrelationId(value).ShouldBe(value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" corr_1")]
    [InlineData("corr/1")]
    [InlineData("corr_1\r\nX-Injected: 1")]
    [InlineData("refresh_token_1")]
    [InlineData("Repository-1")]
    public void SanitizeCorrelationIdShouldReplaceUnsafeValuesWithAGeneratedStandIn(string? value)
    {
        string sanitized = FolderCanonicalPathIdentifier.SanitizeCorrelationId(value);

        sanitized.ShouldNotBe(value);
        sanitized.ShouldMatch("^correlation_[0-9a-f]{32}$");
    }

    [Fact]
    public void SanitizeCorrelationIdShouldEchoMaximumLengthValues()
    {
        string value = new('A', 256);

        FolderCanonicalPathIdentifier.SanitizeCorrelationId(value).ShouldBe(value);
    }

    [Fact]
    public void SanitizeCorrelationIdShouldReplaceValuesLongerThan256()
        => FolderCanonicalPathIdentifier.SanitizeCorrelationId(new string('A', 257))
            .ShouldMatch("^correlation_[0-9a-f]{32}$");

    [Fact]
    public void SanitizeCorrelationIdShouldGenerateAFreshEchoableStandInPerCall()
    {
        string first = FolderCanonicalPathIdentifier.SanitizeCorrelationId(null);
        string second = FolderCanonicalPathIdentifier.SanitizeCorrelationId(null);

        first.ShouldNotBe(second);
        FolderCanonicalPathIdentifier.SanitizeCorrelationId(first).ShouldBe(first);
    }
}
