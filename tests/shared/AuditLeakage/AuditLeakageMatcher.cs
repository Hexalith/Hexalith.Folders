using System.Text.Json;

namespace Hexalith.Folders.Tests.Shared.AuditLeakage;

/// <summary>Detects literal and JSON-escaped audit leakage in test output.</summary>
internal static class AuditLeakageMatcher
{
    /// <summary>Checks a sample on a channel that its positive scan policy covers.</summary>
    internal static bool LeaksOn(AuditLeakageSample sample, string channel, string payload)
    {
        if (!AuditLeakageCorpus.OutputSurfaces.Contains(channel))
        {
            throw new ArgumentException("The channel must be declared in the audit leakage corpus.", nameof(channel));
        }

        return sample.ParticipatesIn.Contains("positive", StringComparer.Ordinal)
            && sample.ForbiddenOutputSurfaces.Contains(channel, StringComparer.Ordinal)
            && Contains(payload, sample.Value);
    }

    /// <summary>
    /// Scans text and decoded JSON case-insensitively. Repository artifact scans
    /// request token boundaries to avoid matching inside unrelated words.
    /// </summary>
    internal static bool Contains(string payload, string value, bool requireTokenBoundaries = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ReadOnlySpan<char> trimmed = payload.AsSpan().TrimStart();
        if (!trimmed.IsEmpty && trimmed[0] is '{' or '[' or '"')
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                return Contains(document.RootElement, value, requireTokenBoundaries);
            }
            catch (JsonException)
            {
                // Some text surfaces start with JSON punctuation.
            }
        }

        return MatchesText(payload, value, requireTokenBoundaries);
    }

    /// <summary>Scans decoded JSON names, values, and nested elements.</summary>
    internal static bool Contains(JsonElement element, string value, bool requireTokenBoundaries = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return element.EnumerateObject().Any(property =>
                    MatchesText(property.Name, value, requireTokenBoundaries)
                    || Contains(property.Value, value, requireTokenBoundaries));

            case JsonValueKind.Array:
                return element.EnumerateArray().Any(item => Contains(item, value, requireTokenBoundaries));

            case JsonValueKind.String:
                return MatchesText(element.GetString() ?? string.Empty, value, requireTokenBoundaries);

            default:
                return MatchesText(element.GetRawText(), value, requireTokenBoundaries);
        }
    }

    private static bool MatchesText(string text, string value, bool requireTokenBoundaries)
    {
        if (!requireTokenBoundaries)
        {
            return text.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        for (int index = 0; index <= text.Length - value.Length;)
        {
            int found = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            if (HasTokenBoundary(text, found) && HasTokenBoundary(text, found + value.Length))
            {
                return true;
            }

            index = found + 1;
        }

        return false;
    }

    private static bool HasTokenBoundary(string text, int position)
        => position <= 0
            || position >= text.Length
            || !char.IsLetterOrDigit(text[position - 1])
            || !char.IsLetterOrDigit(text[position]);
}
