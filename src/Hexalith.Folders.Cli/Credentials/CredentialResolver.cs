using System;

namespace Hexalith.Folders.Cli.Credentials;

/// <summary>
/// Resolves the bearer token using the Adapter Parity Contract precedence and fails closed when no token is
/// available, before any HTTP call. The resolver is metadata-only: it never returns or logs the file path,
/// the tenant section, or the token text in any diagnostic.
/// </summary>
/// <remarks>
/// Precedence (AC #6): <b>(1)</b> <c>HEXALITH_TOKEN</c> environment variable → <b>(2)</b>
/// <c>~/.hexalith/credentials.json</c> (per-tenant section) → <b>(3)</b> the <c>--token</c> flag. When all
/// three are empty the command must fail with exit code 65 (<c>credential_missing</c>) and make no call.
/// </remarks>
internal sealed class CredentialResolver
{
    private readonly Func<string, string?> _environment;
    private readonly CredentialStore _store;

    /// <summary>Initializes a new instance of the <see cref="CredentialResolver"/> class.</summary>
    /// <param name="environment">An environment-variable reader (injected by tests).</param>
    /// <param name="credentialsFilePath">An explicit credentials-file path (injected by tests); when null the default <c>~/.hexalith/credentials.json</c> is used.</param>
    public CredentialResolver(Func<string, string?>? environment = null, string? credentialsFilePath = null)
    {
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _store = new CredentialStore(credentialsFilePath ?? CredentialStore.DefaultFilePath(_environment));
    }

    /// <summary>
    /// Resolves the token honoring precedence, or returns <see langword="null"/> when none is found.
    /// </summary>
    /// <param name="tokenOption">The value of the <c>--token</c> flag, if supplied.</param>
    /// <returns>The resolved bearer token, or <see langword="null"/> when no source provided one.</returns>
    public string? ResolveToken(string? tokenOption)
    {
        // Layer 1: HEXALITH_TOKEN environment variable.
        string? fromEnvironment = _environment("HEXALITH_TOKEN");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // Layer 2: per-tenant section of the credentials file.
        string tenantSection = _environment("HEXALITH_TENANT") is { } tenant && !string.IsNullOrWhiteSpace(tenant)
            ? tenant
            : "default";
        string? fromFile = _store.TryReadToken(tenantSection);
        if (!string.IsNullOrWhiteSpace(fromFile))
        {
            return fromFile;
        }

        // Layer 3: explicit --token flag.
        return string.IsNullOrWhiteSpace(tokenOption) ? null : tokenOption;
    }
}
