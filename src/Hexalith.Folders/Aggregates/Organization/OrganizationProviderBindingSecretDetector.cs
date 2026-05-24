using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Organization;

public static partial class OrganizationProviderBindingSecretDetector
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "secret",
        "token",
        "clientsecret",
        "privatekey",
        "credential",
        "connectionstring",
    ];

    public static bool ContainsForbiddenValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return BearerPattern().IsMatch(value)
            || PemPattern().IsMatch(value)
            || JwtPattern().IsMatch(value)
            || CredentialUrlPattern().IsMatch(value)
            || ConnectionStringPattern().IsMatch(value)
            || DiffPattern().IsMatch(value)
            || ProviderPayloadPattern().IsMatch(value)
            || GeneratedContextPattern().IsMatch(value);
    }

    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        return SensitiveKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"bearer\s+[a-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();

    [GeneratedRegex(@"[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialUrlPattern();

    [GeneratedRegex(@"(?:AccountKey|SharedAccessKey|Password|Pwd|ClientSecret)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"diff\s+--git\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiffPattern();

    [GeneratedRegex(@"""(?:password|secret|token|clientSecret)""\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProviderPayloadPattern();

    [GeneratedRegex(@"generated[-_ ]context[-_ ]payload", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedContextPattern();
}
