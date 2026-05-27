using Hexalith.Folders.Mcp.Configuration;
using Hexalith.Folders.Mcp.Credentials;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies credential sourcing precedence (env token → inline token → token file) and the fail-closed
/// missing case, with injected environment and file readers so the resolver never touches real state. The
/// resolver returns only the token; it never surfaces the file path or the auth section (metadata-only).
/// </summary>
public sealed class CredentialResolverTests
{
    [Fact]
    public void EnvironmentTokenWinsOverInlineAndFile()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { Token = "inline", TokenFile = "/ignored" },
            environment: name => name == "HEXALITH_TOKEN" ? "from-env" : null,
            fileReader: _ => "from-file");

        resolver.ResolveToken().ShouldBe("from-env");
    }

    [Fact]
    public void InlineTokenIsUsedWhenNoEnvironmentToken()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { Token = "  inline-token  " },
            environment: _ => null,
            fileReader: _ => null);

        resolver.ResolveToken().ShouldBe("inline-token");
    }

    [Fact]
    public void TokenFileIsReadWhenNoInlineOrEnvironmentToken()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { TokenFile = "/secrets/token" },
            environment: _ => null,
            fileReader: path => path == "/secrets/token" ? "file-token\n" : null);

        resolver.ResolveToken().ShouldBe("file-token");
    }

    [Fact]
    public void EnvironmentTokenFilePathOverridesConfiguredPath()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { TokenFile = "/configured" },
            environment: name => name == "HEXALITH_FOLDERS_AUTH_TOKENFILE" ? "/env-path" : null,
            fileReader: path => path == "/env-path" ? "env-path-token" : null);

        resolver.ResolveToken().ShouldBe("env-path-token");
    }

    [Fact]
    public void NoTokenAnywhereResolvesToNull()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions(),
            environment: _ => null,
            fileReader: _ => null);

        resolver.ResolveToken().ShouldBeNull();
    }

    [Fact]
    public void UnreadableTokenFileResolvesToNull()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { TokenFile = "/missing" },
            environment: _ => null,
            fileReader: _ => null);

        resolver.ResolveToken().ShouldBeNull();
    }
}
