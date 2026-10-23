using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Providers.Shared;

/// <summary>Hashes provider evidence without interpreting provider-specific domains.</summary>
internal static class ProviderEvidenceHash
{
    /// <summary>Hashes a prevalidated ASCII domain followed by the caller's encoded fields.</summary>
    /// <param name="domain">The provider-validated evidence domain.</param>
    /// <param name="append">The field encoder.</param>
    /// <returns>The lowercase SHA-256 fingerprint.</returns>
    internal static string Compute(string domain, Action<IncrementalHash> append)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain));
        append(hash);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>Compares valid SHA-256 hexadecimal values in fixed time after decoding.</summary>
    /// <param name="expected">The expected fingerprint.</param>
    /// <param name="actual">The observed fingerprint.</param>
    /// <returns>Whether both fingerprints decode and match.</returns>
    internal static bool FixedTimeEquals(string? expected, string? actual)
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
