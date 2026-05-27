using System;
using System.Collections.Generic;
using System.IO;

using Hexalith.Folders.Cli.Credentials;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Validates the Adapter Parity Contract token precedence (HEXALITH_TOKEN → credentials file → --token) and
/// the fail-closed missing-token behavior. The credentials-file path and environment are injected so the
/// tests never read <c>~/.hexalith</c> or process environment.
/// </summary>
public sealed class CredentialResolverTests : IDisposable
{
    private readonly string _credentialsPath = Path.Combine(Path.GetTempPath(), $"hexalith-creds-{Guid.NewGuid():N}.json");

    private static Func<string, string?> Env(params (string Name, string Value)[] entries)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string name, string value) in entries)
        {
            map[name] = value;
        }

        return name => map.TryGetValue(name, out string? value) ? value : null;
    }

    [Fact]
    public void EnvironmentTokenWinsOverFileAndFlag()
    {
        WriteCredentials("default", "file-token");
        CredentialResolver resolver = new(Env(("HEXALITH_TOKEN", "env-token")), _credentialsPath);

        resolver.ResolveToken("flag-token").ShouldBe("env-token");
    }

    [Fact]
    public void FileTokenWinsOverFlagWhenNoEnvironment()
    {
        WriteCredentials("default", "file-token");
        CredentialResolver resolver = new(Env(), _credentialsPath);

        resolver.ResolveToken("flag-token").ShouldBe("file-token");
    }

    [Fact]
    public void FlagTokenUsedWhenNoEnvironmentOrFile()
    {
        CredentialResolver resolver = new(Env(), _credentialsPath);

        resolver.ResolveToken("flag-token").ShouldBe("flag-token");
    }

    [Fact]
    public void ReturnsNullWhenNoSourceProvidesToken()
    {
        CredentialResolver resolver = new(Env(), _credentialsPath);

        resolver.ResolveToken(tokenOption: null).ShouldBeNull();
    }

    [Fact]
    public void HexalithTenantSelectsTheCredentialsSection()
    {
        WriteCredentials("acme", "acme-token");
        CredentialResolver resolver = new(Env(("HEXALITH_TENANT", "acme")), _credentialsPath);

        resolver.ResolveToken(tokenOption: null).ShouldBe("acme-token");
    }

    [Fact]
    public void MalformedCredentialsFileFallsThroughToFlag()
    {
        System.IO.File.WriteAllText(_credentialsPath, "{ not json");
        CredentialResolver resolver = new(Env(), _credentialsPath);

        resolver.ResolveToken("flag-token").ShouldBe("flag-token");
    }

    private void WriteCredentials(string section, string token)
        => System.IO.File.WriteAllText(_credentialsPath, "{\"tenants\":{\"" + section + "\":{\"token\":\"" + token + "\"}}}");

    public void Dispose()
    {
        if (System.IO.File.Exists(_credentialsPath))
        {
            System.IO.File.Delete(_credentialsPath);
        }
    }
}
