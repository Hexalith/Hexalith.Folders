using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubProviderSafeOperationEvidence
{
    public static string Create(params string?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string? field in fields)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(field ?? string.Empty);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
