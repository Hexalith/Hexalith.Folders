using System.Text.RegularExpressions;

namespace Hexalith.Folders.Projections.SemanticIndexing;

internal static partial class SemanticIndexingBridgeValidation
{
    public static string RequireSegment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!SegmentPattern().IsMatch(value))
        {
            throw new ArgumentException("Identifier must be metadata-safe and segment-shaped.", paramName);
        }

        return value;
    }

    public static string? RequireOptionalValue(string? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    public static string RequireMetadataReference(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (LooksLikeUnsafeLocalPath(value) || value.Contains("://", StringComparison.Ordinal))
        {
            throw new ArgumentException("Metadata references must not contain raw paths or URI payloads.", paramName);
        }

        return value;
    }

    public static string RequireSourceUri(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!value.StartsWith("folders://", StringComparison.Ordinal)
            || value.StartsWith("file://", StringComparison.Ordinal)
            || LooksLikeUnsafeLocalPath(value))
        {
            throw new ArgumentException("Source URI must use the folders:// metadata-safe shape.", paramName);
        }

        return value;
    }

    private static bool LooksLikeUnsafeLocalPath(string value)
        => value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || DrivePathPattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z]:/", RegexOptions.CultureInvariant)]
    private static partial Regex DrivePathPattern();

    [GeneratedRegex("^[a-zA-Z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
