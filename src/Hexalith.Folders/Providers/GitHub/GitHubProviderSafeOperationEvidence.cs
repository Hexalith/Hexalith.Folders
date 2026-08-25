using System.Security.Cryptography;
using System.Text;

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

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain));
        using GitHubCanonicalEvidenceWriter writer = new(hash);
        append(writer);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string? expected, string? actual)
    {
        if (!TryDecode(expected, out byte[]? expectedBytes)
            || !TryDecode(actual, out byte[]? actualBytes))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static bool TryDecode(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (value is not { Length: 64 })
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
