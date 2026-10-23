using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates;

/// <summary>Validates stream segments independently of each aggregate's namespace policy.</summary>
internal static partial class StreamNameSegment
{
    /// <summary>The maximum number of characters in a stream segment.</summary>
    internal const int MaxLength = 256;

    /// <summary>Accepts bounded canonical segments and candidate opaque identifiers.</summary>
    /// <param name="value">The segment to validate.</param>
    /// <returns>Whether the segment satisfies the shared grammar.</returns>
    internal static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxLength
            && (CanonicalSegmentPattern().IsMatch(value)
                || PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(value));

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSegmentPattern();
}
