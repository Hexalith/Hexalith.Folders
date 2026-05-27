using System.Globalization;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.LoadTests.Scenarios;

public static partial class SafeOrdinalId
{
    public static string Create(string prefix, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        string normalizedPrefix = prefix.Trim();
        if (!SafePrefixPattern().IsMatch(normalizedPrefix))
        {
            throw new ArgumentException("Identifier prefixes must be lowercase ASCII words separated by hyphens.", nameof(prefix));
        }

        if (ordinal < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Ordinal identifiers are one-based.");
        }

        return $"{normalizedPrefix}-{ordinal.ToString("D4", CultureInfo.InvariantCulture)}";
    }

    public static bool IsSafe(string value)
        => SafeIdentifierPattern().IsMatch(value);

    [GeneratedRegex("^[a-z][a-z0-9-]*-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierPattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePrefixPattern();
}
