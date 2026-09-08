using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderSensitiveDiagnosticDetectorTests
{
    [Theory]
    [InlineData("repo_aaaaaaaaaaaaaaaaaaaa")]
    [InlineData("owner-repository-name")]
    [InlineData("diff --git a/file b/file")]
    [InlineData("installation-123")]
    [InlineData("providerpayload")]
    [InlineData("repo-secret-name")]
    [InlineData("bearer-token")]
    [InlineData("client-secret")]
    [InlineData("password-reset")]
    [InlineData("credential-ref")]
    [InlineData("https://example.test/path")]
    [InlineData("user@example.test")]
    [InlineData("privatekey")]
    [InlineData("private key")]
    public void DetectorShouldMarkProviderHttpSensitiveTokens(string value)
        => FolderSensitiveDiagnosticDetector.IsSensitive(value).ShouldBeTrue();

    [Theory]
    [InlineData("gho_abcdefghijklmnopqrst")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.aaaaa.bbbbb")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    public void DetectorShouldMarkProviderTokenJwtAndPemShapes(string value)
        => FolderSensitiveDiagnosticDetector.IsSensitive(value).ShouldBeTrue();

    [Theory]
    [InlineData("corr-a")]
    [InlineData("folder-a")]
    [InlineData("workspace_1")]
    public void DetectorShouldAllowMetadataOnlyIdentifiers(string value)
        => FolderSensitiveDiagnosticDetector.IsSensitive(value).ShouldBeFalse();
}
