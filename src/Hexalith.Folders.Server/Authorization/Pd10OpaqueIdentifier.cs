namespace Hexalith.Folders.Server.Authorization;

/// <summary>Validates the exact PD10 v2 <c>OpaqueIdentifier</c> wire grammar.</summary>
internal static class Pd10OpaqueIdentifier
{
    /// <summary>Determines whether a value matches the closed v2 opaque-identifier shape.</summary>
    /// <param name="value">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value matches the contract.</returns>
    internal static bool IsValid(string? value)
        => value is not null
            && value.Length is >= 16 and <= 128
            && char.IsAsciiLetterOrDigit(value[0])
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
