using System.Text.RegularExpressions;

namespace Hexalith.Folders.Contracts.Projections.Audit;

/// <summary>Identifies provider credential shapes that must stay out of diagnostic identifiers.</summary>
internal static partial class ProviderCredentialShape
{
    /// <summary>Returns whether the value contains a supported provider credential shape.</summary>
    internal static bool IsPresent(string value) => Pattern().IsMatch(value);

    [GeneratedRegex("(?:gh[pousr]_|github_pat_)[a-zA-Z0-9_]{20,}|glpat-[a-zA-Z0-9_-]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
