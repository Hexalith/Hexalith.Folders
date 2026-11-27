using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server;

/// <summary>
/// Validates lowercase 128-character segment identifiers for the Domain and Audit REST endpoints and the
/// <c>/process</c> boundary.
/// </summary>
internal static partial class FolderCanonicalSegmentIdentifier
{
    /// <summary>
    /// Returns whether <paramref name="value"/> is a lowercase segment identifier or a candidate opaque identifier.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns>
    /// <see langword="true"/> when the value matches <c>^[a-z0-9._-]+$</c>, or is a mixed-case opaque identifier of
    /// 16 to 128 letters, digits, underscores and hyphens, and is at most 128 characters.
    /// </returns>
    public static bool IsValid(string? value)
        => IsLowercaseSegment(value)
        || (value is not null
            && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
            && PreauthorizedRequestContext.IsCandidateOpaqueIdentifier(value));

    /// <summary>
    /// Returns whether <paramref name="value"/> matches the strict lowercase segment grammar without the
    /// opaque-identifier allowance that <see cref="IsValid"/> grants. The <c>/process</c> boundary and
    /// rejection events use <see cref="IsValid"/>; this stricter form remains for contexts that require
    /// lowercase-only canonical segments.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value matches <c>^[a-z0-9._-]+$</c> and is at most 128 characters.</returns>
    public static bool IsLowercaseSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && Pattern().IsMatch(value);

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
