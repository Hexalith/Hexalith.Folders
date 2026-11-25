using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates;

/// <summary>Validates stream segments independently of each aggregate's namespace policy.</summary>
internal static partial class StreamNameSegment
{
    /// <summary>The maximum number of characters in a stream segment.</summary>
    internal const int MaxLength = 256;

    /// <summary>The reserved projection scope for organization permissions.</summary>
    internal const string ReservedOrganizationPermissionScope = "organization_baseline";

    /// <summary>Accepts bounded canonical segments and candidate opaque identifiers.</summary>
    /// <param name="value">The segment to validate.</param>
    /// <returns>Whether the segment satisfies the shared grammar.</returns>
    internal static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxLength
            && (CanonicalSegmentPattern().IsMatch(value)
                || IsCandidateOpaqueIdentifier(value));

    /// <summary>
    /// Returns whether a value uses the uppercase-capable v2 opaque-identifier grammar.
    /// The grammar is part of the identifier, including after compatibility seam state ends, so stream names,
    /// command validation, and projection rebuild accept the same value that a candidate request accepted.
    /// </summary>
    internal static bool IsCandidateOpaqueIdentifier(string? value)
        => value is not null
            && value.Length is >= 16 and <= 128
            && char.IsAsciiLetterOrDigit(value[0])
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSegmentPattern();
}
