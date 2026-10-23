using Hexalith.Folders.Providers.Shared;

namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoProviderSafeOperationEvidence
{
    public static string Create(string domain, params string?[] fields)
        => Compute(domain, writer =>
        {
            foreach (string? field in fields)
            {
                writer.AppendString(field);
            }
        });

    public static string Compute(string domain, Action<ForgejoCanonicalEvidenceWriter> append)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(append);
        if (!domain.StartsWith("hxf-forgejo:v1:", StringComparison.Ordinal)
            || !domain.All(static character => character <= 0x7f))
        {
            throw new ArgumentException("The evidence domain is invalid.", nameof(domain));
        }

        return ProviderEvidenceHash.Compute(domain, hash =>
        {
            using ForgejoCanonicalEvidenceWriter writer = new(hash);
            append(writer);
        });
    }

    public static bool FixedTimeEquals(string? expected, string? actual)
        => ProviderEvidenceHash.FixedTimeEquals(expected, actual);
}
