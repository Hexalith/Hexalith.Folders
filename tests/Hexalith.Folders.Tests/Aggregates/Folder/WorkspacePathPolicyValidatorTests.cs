using Hexalith.Folders.Aggregates.Folder;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class WorkspacePathPolicyValidatorTests
{
    [Fact]
    public void CanonicalWorkspaceRelativePathShouldBeAcceptedWithDigestEvidence()
    {
        WorkspacePathPolicyResult result = WorkspacePathPolicyValidator.Validate(
            new PathMetadata(
                "docs/readme.md",
                "readme.md",
                "tenant_sensitive_document",
                "NFC"));

        result.IsAccepted.ShouldBeTrue();
        result.Decision.ShouldBe(WorkspacePathPolicyDecision.Accepted);
        result.PathMetadataDigest.ShouldNotBeNullOrWhiteSpace();
        result.PathPolicyClass.ShouldBe("tenant_sensitive_document");
    }

    [Theory]
    [InlineData("../secret.txt", WorkspacePathPolicyDecision.Traversal)]
    [InlineData("/etc/passwd", WorkspacePathPolicyDecision.AbsolutePath)]
    [InlineData("C:/temp/file.txt", WorkspacePathPolicyDecision.AbsolutePath)]
    [InlineData("//server/share/file.txt", WorkspacePathPolicyDecision.AbsolutePath)]
    [InlineData("docs\\readme.md", WorkspacePathPolicyDecision.MixedSeparators)]
    [InlineData("docs//readme.md", WorkspacePathPolicyDecision.EmptySegment)]
    [InlineData("docs/./readme.md", WorkspacePathPolicyDecision.DotSegment)]
    [InlineData("docs/%2e%2e/secret.txt", WorkspacePathPolicyDecision.PercentDotSegmentSmuggling)]
    [InlineData("docs/%2E/secret.txt", WorkspacePathPolicyDecision.PercentDotSegmentSmuggling)]
    [InlineData("docs%2fsecret.txt", WorkspacePathPolicyDecision.WorkspaceRootEscape)]
    [InlineData("docs/con.txt", WorkspacePathPolicyDecision.ReservedPlatformName)]
    [InlineData("docs/NUL.md", WorkspacePathPolicyDecision.ReservedPlatformName)]
    [InlineData("docs/lpt9", WorkspacePathPolicyDecision.ReservedPlatformName)]
    [InlineData("docs/name .txt", WorkspacePathPolicyDecision.TrailingSpaceOrDotAmbiguity)]
    [InlineData("docs/name./file.txt", WorkspacePathPolicyDecision.TrailingSpaceOrDotAmbiguity)]
    [InlineData("docs/", WorkspacePathPolicyDecision.EmptySegment)]
    [InlineData("docs/secret\u0001.txt", WorkspacePathPolicyDecision.ControlCharacter)]
    [InlineData("docs/Ａ-folder.txt", WorkspacePathPolicyDecision.UnicodeNormalizationAmbiguity)]
    public void UnsafeNormalizedPathShouldBeDeniedWithoutPathEcho(
        string normalizedPath,
        WorkspacePathPolicyDecision expectedDecision)
    {
        WorkspacePathPolicyResult result = WorkspacePathPolicyValidator.Validate(
            new PathMetadata(
                normalizedPath,
                "safe-name.txt",
                "tenant_sensitive_document",
                "NFC"));

        result.IsAccepted.ShouldBeFalse();
        result.Decision.ShouldBe(expectedDecision);
        result.UnsafePath.ShouldBeNull();
    }

    [Theory]
    [InlineData("safe/name.txt")]
    [InlineData("safe\\name.txt")]
    [InlineData("safe\u200dname.txt")]
    public void UnsafeDisplayNameShouldBeDeniedWithoutPathEcho(string displayName)
    {
        WorkspacePathPolicyResult result = WorkspacePathPolicyValidator.Validate(
            new PathMetadata(
                "docs/readme.md",
                displayName,
                "tenant_sensitive_document",
                "NFC"));

        result.IsAccepted.ShouldBeFalse();
        result.UnsafePath.ShouldBeNull();
    }

    [Fact]
    public void NonNfcInputShouldBeDeniedAsNormalizationAmbiguity()
    {
        string nonNfc = "docs/cafe\u0301.txt";

        WorkspacePathPolicyResult result = WorkspacePathPolicyValidator.Validate(
            new PathMetadata(
                nonNfc,
                "cafe.txt",
                "tenant_sensitive_document",
                "NFC"));

        result.IsAccepted.ShouldBeFalse();
        result.Decision.ShouldBe(WorkspacePathPolicyDecision.UnicodeNormalizationAmbiguity);
    }

    [Fact]
    public void InvisibleFormatCharactersShouldBeDenied()
    {
        WorkspacePathPolicyResult result = WorkspacePathPolicyValidator.Validate(
            new PathMetadata(
                "docs/readme\u200b.md",
                "readme.md",
                "tenant_sensitive_document",
                "NFC"));

        result.IsAccepted.ShouldBeFalse();
        result.Decision.ShouldBe(WorkspacePathPolicyDecision.InvisibleCharacter);
    }

    [Fact]
    public void OverLengthPathAndDisplayNameShouldBeDenied()
    {
        WorkspacePathPolicyValidator.Validate(
                new PathMetadata(
                    new string('a', 513),
                    "safe-name.txt",
                    "tenant_sensitive_document",
                    "NFC"))
            .Decision
            .ShouldBe(WorkspacePathPolicyDecision.OverLength);

        WorkspacePathPolicyValidator.Validate(
                new PathMetadata(
                    "docs/readme.md",
                    new string('a', 129),
                    "tenant_sensitive_document",
                    "NFC"))
            .Decision
            .ShouldBe(WorkspacePathPolicyDecision.InvalidDisplayName);
    }
}
