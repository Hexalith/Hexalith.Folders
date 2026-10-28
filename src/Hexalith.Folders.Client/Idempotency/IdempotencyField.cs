using System.Globalization;

namespace Hexalith.Folders.Client.Idempotency;

/// <summary>Describes a field in a canonical idempotency descriptor.</summary>
/// <param name="Path">The canonical, case-sensitive field path.</param>
/// <param name="Present">Whether the field was supplied.</param>
/// <param name="Value">The supplied value, including explicit null.</param>
public readonly record struct IdempotencyField(string Path, bool Present, object? Value)
{
    /// <summary>Validates the field path and returns its canonical representation.</summary>
    /// <returns>The field's canonical line.</returns>
    public string ToCanonicalLine()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Path);
        HexalithIdempotencyHasher.RejectControlCharacters(Path, "field path");
        return "field=" + HexalithIdempotencyHasher.Escape(Path) + ";present=" + Present.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + ";value=" + (Present ? HexalithIdempotencyHasher.Canonicalize(Value) : "omitted");
    }
}
