using System.Text.RegularExpressions;

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
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || PemPattern().IsMatch(value);
    }

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
