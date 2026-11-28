using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.EventStore;

/// <summary>Checks file-content evidence shared by Gateway admission and the Folders callback.</summary>
internal static partial class DecoratedCommandContentEvidence
{
    internal const int InlineByteLimit = 262_144;
    internal const int MaximumFileBytes = 1_048_576;
    internal const long MaximumBatchBytes = 10_485_760;
    internal const int MaximumBatchItems = 100;
    private const string ContentHashPrefix = "hashref_";
    private const int MaximumInlineBase64Length = 349_528;

    /// <summary>Decodes and authenticates the declared inline length and SHA-256.</summary>
    internal static bool TryDecodeInline(string? encoded, long byteLength, string? hash, out byte[]? decoded)
    {
        decoded = null;
        if (encoded is null || encoded.Length > MaximumInlineBase64Length || encoded.Any(char.IsWhiteSpace)
            || byteLength is < 0 or > InlineByteLimit)
        {
            return false;
        }

        byte[] bytes = new byte[byteLength];
        if (!Convert.TryFromBase64String(encoded, bytes, out int written) || written != byteLength
            || !string.Equals(hash, ContentHashPrefix + Convert.ToHexStringLower(SHA256.HashData(bytes)), StringComparison.Ordinal))
        {
            CryptographicOperations.ZeroMemory(bytes);
            return false;
        }

        decoded = bytes;
        return true;
    }

    /// <summary>Checks the bounded transport media-type token grammar.</summary>
    internal static bool IsValidMediaType(string? value)
        => value is { Length: <= 128 } && MediaTypePattern().IsMatch(value);

    /// <summary>Checks the content hash reference grammar for staged evidence.</summary>
    internal static bool IsValidHashReference(string? value)
        => value is not null && ContentHashReferencePattern().IsMatch(value);

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex MediaTypePattern();

    [GeneratedRegex("\\Ahashref_[A-Za-z0-9]{32,96}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ContentHashReferencePattern();
}
