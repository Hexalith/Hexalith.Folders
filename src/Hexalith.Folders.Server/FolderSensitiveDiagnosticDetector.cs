using System.Text.RegularExpressions;

using Hexalith.Folders.Observability;

namespace Hexalith.Folders.Server;

/// <summary>
/// Detects secret-looking diagnostic values using the Provider HTTP filter.
/// </summary>
internal static partial class FolderSensitiveDiagnosticDetector
{
    /// <summary>
    /// Returns whether <paramref name="value"/> must be treated as sensitive on HTTP diagnostic surfaces.
    /// </summary>
    /// <param name="value">The candidate diagnostic value.</param>
    /// <returns><see langword="true"/> when the value matches the Provider HTTP secret filter.</returns>
    public static bool IsSensitive(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string canonical = value.Trim().ToLowerInvariant();
        return canonical.Contains("token", StringComparison.Ordinal)
            || canonical.Contains("secret", StringComparison.Ordinal)
            || canonical.Contains("password", StringComparison.Ordinal)
            || canonical.Contains("credential", StringComparison.Ordinal)
            || canonical.Contains("repository", StringComparison.Ordinal)
            || canonical.Contains("repo_", StringComparison.Ordinal)
            || canonical.Contains("repo-", StringComparison.Ordinal)
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || canonical.Contains("diff --git", StringComparison.Ordinal)
            || canonical.Contains("providerpayload", StringComparison.Ordinal)
            || canonical.Contains("privatekey", StringComparison.Ordinal)
            || canonical.Contains("private key", StringComparison.Ordinal)
            || canonical.Contains("installation", StringComparison.Ordinal)
            || FolderAuditSanitizer.ContainsProviderCredential(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);
    }

    /// <summary>Detects credential-shaped values in ordinary query metadata.</summary>
    internal static bool ContainsSecret(string value)
        => FolderAuditSanitizer.ContainsProviderCredential(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value)
            || AuthorizationHeaderPattern().IsMatch(value)
            || ContainsCredentialUrl(value);

    private static bool ContainsCredentialUrl(string value)
    {
        foreach (Match match in UrlPattern().Matches(value))
        {
            if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? url)
                && (url.UserInfo.Length != 0 || HasCredentialParameter(url.Query) || HasCredentialParameter(url.Fragment)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCredentialParameter(string parameters)
    {
        foreach (string parameter in parameters.TrimStart('?', '#').Split('&'))
        {
            int separator = parameter.IndexOf('=');
            if (separator > 0 && separator < parameter.Length - 1
                && CredentialParameterPattern().IsMatch(Uri.UnescapeDataString(parameter[..separator])))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\b(?:proxy-)?authorization[ \t]*:[ \t]*\S|\A[ \t]*(?:bearer|basic)[ \t]+[a-z0-9._~+/-]+=*[ \t]*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderPattern();

    [GeneratedRegex("""\b[a-z][a-z0-9+.\-]*://[^\s<>"']+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\A(?:(?:access|id|refresh)[_-]?token|api[_-]?key|client[_-]?secret|credential|password|secret|signature|sig|token|googleaccessid|x-(?:amz|goog)-(?:credential|security-token|signature))\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialParameterPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
