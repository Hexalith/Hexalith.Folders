using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Shared;

/// <summary>Validates bounded metadata shared by the provider adapters.</summary>
internal static class ProviderAdapterGuards
{
    /// <summary>Checks an opaque value for length, canonical Unicode, and unsafe content.</summary>
    /// <param name="value">The metadata value.</param>
    /// <returns>Whether the value is safe opaque metadata.</returns>
    internal static bool IsSafeOpaqueValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 512
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    /// <summary>Checks a lowercase hexadecimal SHA-256 fingerprint.</summary>
    /// <param name="value">The fingerprint.</param>
    /// <returns>Whether the fingerprint has the expected shape.</returns>
    internal static bool IsSafeFingerprint(string? value)
        => value is { Length: 64 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    /// <summary>Checks a bounded ASCII operation reference.</summary>
    /// <param name="value">The operation reference.</param>
    /// <returns>Whether the reference satisfies the shared grammar.</returns>
    internal static bool IsSafeOperationReference(string? value)
        => value is { Length: > 0 and <= 128 }
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    /// <summary>Checks ordinal ancestor conflicts while permitting an exact path match.</summary>
    /// <param name="paths">The existing paths.</param>
    /// <param name="candidate">The candidate path.</param>
    /// <returns>Whether either path is a proper ancestor of the other.</returns>
    internal static bool HasAncestorConflict(HashSet<string> paths, string candidate)
        => paths.Any(path => !string.Equals(path, candidate, StringComparison.Ordinal)
            && (path.StartsWith(candidate + "/", StringComparison.Ordinal)
                || candidate.StartsWith(path + "/", StringComparison.Ordinal)));
}
