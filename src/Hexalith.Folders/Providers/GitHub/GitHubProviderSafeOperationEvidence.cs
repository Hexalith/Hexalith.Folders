using Hexalith.Folders.Providers.Shared;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubProviderSafeOperationEvidence
{
    public static string Create(string domain, params string?[] fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(fields);
        return Compute(domain, writer =>
        {
            foreach (string? field in fields)
            {
                writer.AppendString(field);
            }
        });
    }

    public static string Compute(string domain, Action<GitHubCanonicalEvidenceWriter> append)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(append);
        if (!domain.StartsWith("hxf-github:v1:", StringComparison.Ordinal)
            || !domain.All(static character => character <= 0x7f))
        {
            throw new ArgumentException("The evidence domain is invalid.", nameof(domain));
        }

        return ProviderEvidenceHash.Compute(domain, hash =>
        {
            using GitHubCanonicalEvidenceWriter writer = new(hash);
            append(writer);
        });
    }

    public static bool FixedTimeEquals(string? expected, string? actual)
        => ProviderEvidenceHash.FixedTimeEquals(expected, actual);
}
