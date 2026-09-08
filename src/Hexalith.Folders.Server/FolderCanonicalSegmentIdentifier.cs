using System.Text.RegularExpressions;

namespace Hexalith.Folders.Server;

/// <summary>
/// Validates Domain and Audit REST segment identifiers against the lowercase 128-character grammar.
/// </summary>
internal static partial class FolderCanonicalSegmentIdentifier
{
    /// <summary>
    /// Returns whether <paramref name="value"/> is a lowercase canonical segment identifier.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value matches <c>^[a-z0-9._-]+$</c> and is at most 128 characters.</returns>
    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= FoldersServerModule.MaxCanonicalIdentifierLength
        && Pattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
