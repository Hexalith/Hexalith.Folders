using System;
using System.IO;

using Hexalith.Folders.Mcp.Configuration;

namespace Hexalith.Folders.Mcp.Credentials;

/// <summary>
/// Resolves the bearer token from server configuration using the Adapter Parity Contract credential
/// precedence and fails closed (returns <see langword="null"/>) when no token is available, before any HTTP
/// call. The resolver is metadata-only: it never returns, logs, or otherwise exposes the token-file path,
/// the auth section, or the token text in any diagnostic.
/// </summary>
/// <remarks>
/// Precedence (AC #7): <b>(1)</b> the <c>HEXALITH_TOKEN</c> environment variable → <b>(2)</b> the inline
/// <c>folders:auth:token</c> configuration value → <b>(3)</b> a token file whose path comes from the
/// <c>HEXALITH_FOLDERS_AUTH_TOKENFILE</c> environment variable or <c>folders:auth:tokenFile</c>. The
/// environment reader and file reader are injectable so hermetic tests never read real environment state or
/// open real files. Unlike the CLI there is no auto-key/auto-token path; a missing token is a hard
/// <c>credential_missing</c> failure.
/// </remarks>
internal sealed class McpCredentialResolver
{
    private const string TokenEnvironmentVariable = "HEXALITH_TOKEN";
    private const string TokenFileEnvironmentVariable = "HEXALITH_FOLDERS_AUTH_TOKENFILE";

    private readonly FoldersMcpAuthOptions _auth;
    private readonly Func<string, string?> _environment;
    private readonly Func<string, string?> _fileReader;

    /// <summary>Initializes a new instance of the <see cref="McpCredentialResolver"/> class.</summary>
    /// <param name="auth">The configured authentication options.</param>
    /// <param name="environment">An environment-variable reader (injected by tests).</param>
    /// <param name="fileReader">A path-to-contents reader returning <see langword="null"/> when the file cannot be read (injected by tests).</param>
    public McpCredentialResolver(
        FoldersMcpAuthOptions auth,
        Func<string, string?>? environment = null,
        Func<string, string?>? fileReader = null)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _fileReader = fileReader ?? ReadFileOrNull;
    }

    /// <summary>
    /// Resolves the bearer token honoring precedence, or returns <see langword="null"/> when none is found.
    /// </summary>
    /// <returns>The resolved, non-blank bearer token, or <see langword="null"/> when no source provided one.</returns>
    public string? ResolveToken()
    {
        // Layer 1: HEXALITH_TOKEN environment variable.
        string? fromEnvironment = _environment(TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        // Layer 2: inline auth.token configuration value.
        if (!string.IsNullOrWhiteSpace(_auth.Token))
        {
            return _auth.Token.Trim();
        }

        // Layer 3: token file (env path wins over the configured path).
        string? tokenFilePath = _environment(TokenFileEnvironmentVariable) is { } envPath && !string.IsNullOrWhiteSpace(envPath)
            ? envPath
            : _auth.TokenFile;
        if (!string.IsNullOrWhiteSpace(tokenFilePath))
        {
            string? contents = _fileReader(tokenFilePath);
            if (!string.IsNullOrWhiteSpace(contents))
            {
                return contents.Trim();
            }
        }

        return null;
    }

    private static string? ReadFileOrNull(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            // Metadata-only: the path and the underlying failure are never surfaced; a missing/unreadable
            // token file is indistinguishable from "no token configured" and yields credential_missing.
            return null;
        }
    }
}
