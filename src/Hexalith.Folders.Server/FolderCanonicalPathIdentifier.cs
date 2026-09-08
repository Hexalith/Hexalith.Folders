using System.Text.RegularExpressions;

namespace Hexalith.Folders.Server;

/// <summary>
/// Validates Provider and OpsConsole path identifiers against the mixed-case 256-character grammar.
/// </summary>
internal static partial class FolderCanonicalPathIdentifier
{
    /// <summary>
    /// Returns whether <paramref name="value"/> matches the path-identifier grammar.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value matches <c>^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$</c> and is at most 256 characters.</returns>
    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256
        && Pattern().IsMatch(value);

    /// <summary>
    /// Returns whether <paramref name="value"/> is a path identifier that is also safe to echo on diagnostic surfaces.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value is a valid path identifier and is not secret-looking.</returns>
    public static bool IsSafeDiagnosticId(string? value)
        => value is not null && IsValid(value) && !FolderSensitiveDiagnosticDetector.IsSensitive(value);

    /// <summary>
    /// Returns a correlation identifier that is safe to echo, or a generated <c>correlation_{guid}</c> stand-in.
    /// </summary>
    /// <param name="value">The candidate correlation identifier.</param>
    /// <returns>The trimmed original value when it is a non-sensitive path identifier; otherwise a generated stand-in.</returns>
    public static string SanitizeCorrelationId(string? value)
    {
        if (value is not null
            && IsValid(value)
            && FolderHttpHeaderReader.IsSafeHeaderValue(value)
            && !FolderSensitiveDiagnosticDetector.IsSensitive(value))
        {
            return value.Trim();
        }

        return $"correlation_{Guid.NewGuid():N}";
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
