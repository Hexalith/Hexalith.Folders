using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace Hexalith.Folders.Cli.Credentials;

/// <summary>
/// Reads tokens from the per-tenant credentials file (<c>~/.hexalith/credentials.json</c>). The store is
/// metadata-only: it never logs, echoes, or otherwise surfaces the file path or any token value.
/// </summary>
/// <remarks>
/// File shape (per-tenant sections; the selected section comes from the <c>HEXALITH_TENANT</c> environment
/// variable, defaulting to <c>default</c>):
/// <code>
/// { "tenants": { "default": { "token": "&lt;jwt&gt;" } } }
/// </code>
/// Tenant <i>authority</i> is carried by the token's claims, not by this selector — the section name only
/// chooses which stored token to present.
/// </remarks>
internal sealed class CredentialStore
{
    private readonly string _filePath;

    /// <summary>Initializes a new instance of the <see cref="CredentialStore"/> class.</summary>
    /// <param name="filePath">The absolute credentials-file path. Injected by tests; production uses <see cref="DefaultFilePath"/>.</param>
    public CredentialStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Computes the default credentials-file path (<c>~/.hexalith/credentials.json</c>) from the
    /// <paramref name="environment"/> reader, falling back to the user profile directory.
    /// </summary>
    /// <param name="environment">An environment-variable reader.</param>
    /// <returns>The resolved default path.</returns>
    public static string DefaultFilePath(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string home = environment("HOME")
            ?? environment("USERPROFILE")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hexalith", "credentials.json");
    }

    /// <summary>
    /// Returns the stored token for <paramref name="tenantSection"/>, or <see langword="null"/> when the
    /// file is absent, unreadable, malformed, or has no token for that section. Failures are swallowed (the
    /// caller falls through to the next precedence layer) and never surface the path or token.
    /// </summary>
    /// <param name="tenantSection">The tenant section name to read.</param>
    /// <returns>The stored token, or <see langword="null"/>.</returns>
    public string? TryReadToken(string tenantSection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantSection);

        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            CredentialFile? file = JsonConvert.DeserializeObject<CredentialFile>(json);
            if (file?.Tenants is null
                || !file.Tenants.TryGetValue(tenantSection, out CredentialEntry? entry)
                || entry is null
                || string.IsNullOrWhiteSpace(entry.Token))
            {
                return null;
            }

            return entry.Token;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable or malformed credentials file: fall through to the next precedence layer.
            return null;
        }
    }

    private sealed class CredentialFile
    {
        [JsonProperty("tenants")]
        public Dictionary<string, CredentialEntry>? Tenants { get; set; }
    }

    private sealed class CredentialEntry
    {
        [JsonProperty("token")]
        public string? Token { get; set; }
    }
}
