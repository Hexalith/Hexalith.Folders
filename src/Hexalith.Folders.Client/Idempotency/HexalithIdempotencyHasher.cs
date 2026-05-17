using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hexalith.Folders.Client.Idempotency;

public static class HexalithIdempotencyHasher
{
    public static string Compute(string operationId, IEnumerable<IdempotencyField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        RejectControlCharacters(operationId, nameof(operationId));
        ArgumentNullException.ThrowIfNull(fields);

        IdempotencyField[] orderedFields = fields.ToArray();
        EnsureDeclaredOrder(orderedFields);

        List<string> lines = ["operation=" + Escape(operationId)];
        lines.AddRange(orderedFields.Select(field => field.ToCanonicalLine()));
        string canonical = string.Join('\n', lines);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    internal static string Canonicalize(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value switch
        {
            string text => "s:" + Escape(text),
            bool flag => "b:" + (flag ? "true" : "false"),
            Enum enumValue => "s:" + Escape(GetEnumWireValue(enumValue)),
            Guid guid => "s:" + guid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            DateTime dateTime => CanonicalizeDateTime(dateTime),
            DateTimeOffset dateTimeOffset => dateTimeOffset.Offset == TimeSpan.Zero
                ? CanonicalizeDateTime(dateTimeOffset.UtcDateTime)
                : throw new InvalidOperationException("DateTimeOffset values must have Offset=TimeSpan.Zero before idempotency canonicalization."),
            // Explicit BCL primitive arms below avoid version-dependent NormalizeJson fall-through
            // for types whose Newtonsoft serialization shape can change across runtime updates.
            // Round 4 review finding P10 + Round 4 (external) P7 (Uri).
            DateOnly dateOnly => "t:date:" + dateOnly.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly timeOnly => "t:time:" + timeOnly.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => "t:span:" + timeSpan.ToString("c", CultureInfo.InvariantCulture),
            // Uri canonical form uses AbsoluteUri because Uri.ToString() percent-decodes
            // reserved characters version-inconsistently across .NET runtimes. AbsoluteUri
            // preserves the wire-stable percent-encoded form.
            Uri uri => "u:" + Escape(uri.AbsoluteUri),
            decimal number => CanonicalizeDecimal(number),
            double number => CanonicalizeFloatingPoint(number),
            float number => CanonicalizeFloatingPoint(number),
            byte or sbyte or short or ushort or int or uint or long or ulong => "n:" + Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => "j:" + NormalizeJson(value),
        };
    }

    private static string NormalizeJson(object value)
    {
        JToken token = JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = IncludeNullContractResolver.Instance,
            NullValueHandling = NullValueHandling.Include,
            Culture = CultureInfo.InvariantCulture,
        }));

        RejectDuplicateProperties(token, "$");
        return SortToken(token).ToString(Formatting.None);
    }

    internal static string NormalizeJsonText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 0 && value[0] == '\uFEFF')
        {
            value = value[1..];
        }

        using JsonTextReader reader = new(new StringReader(value))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        JToken token = JToken.ReadFrom(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        });
        return SortToken(token).ToString(Formatting.None);
    }

    private static JToken SortToken(JToken token) => token switch
    {
        JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, SortToken(p.Value)))),
        JArray array => new JArray(array.Select(SortToken)),
        _ => token.DeepClone(),
    };

    private static string GetEnumWireValue(Enum value)
    {
        Type enumType = value.GetType();
        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            throw new InvalidOperationException($"[Flags] enum '{enumType.FullName}' cannot be canonicalized.");
        }

        string enumName = value.ToString();
        if (enumName.Contains(',', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Composite enum value '{enumName}' on '{enumType.FullName}' cannot be canonicalized.");
        }

        FieldInfo? field = enumType.GetField(enumName, BindingFlags.Public | BindingFlags.Static);
        if (field is null)
        {
            throw new InvalidOperationException($"Unknown enum value '{enumName}' on '{enumType.FullName}'.");
        }

        return field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? enumName;
    }

    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new(value.Length);
        int index = 0;
        while (index < value.Length)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new InvalidOperationException($"Lone or invalid UTF-16 high surrogate at index {index}.");
                }

                builder.Append(character);
                builder.Append(value[index + 1]);
                index += 2;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw new InvalidOperationException($"Lone UTF-16 low surrogate at index {index}.");
            }

            builder.Append(character switch
            {
                '\\' => "\\\\",
                '\0' => "\\u0000",
                '\t' => "\\t",
                '\r' => "\\r",
                '\n' => "\\n",
                '\uFEFF' => "\\uFEFF",
                '\u2028' => "\\u2028",
                '\u2029' => "\\u2029",
                ';' => "\\;",
                '=' => "\\=",
                < ' ' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                >= '\u007F' and <= '\u009F' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
            index++;
        }

        return builder.ToString();
    }

    private static void RejectControlCharacters(string value, string context)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    throw new InvalidOperationException(
                        string.Create(CultureInfo.InvariantCulture, $"{context} contains lone or invalid UTF-16 high surrogate at index {i}."));
                }

                i++;
                continue;
            }

            if (char.IsLowSurrogate(ch))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"{context} contains lone UTF-16 low surrogate at index {i}."));
            }

            if (char.IsControl(ch) || ch == '\uFEFF' || ch == '\u2028' || ch == '\u2029')
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"{context} contains forbidden control or line-separator character at index {i} (U+{(int)ch:X4})."));
            }
        }
    }

    private static void EnsureDeclaredOrder(IReadOnlyList<IdempotencyField> fields)
    {
        string? previous = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IdempotencyField field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Path);
            RejectControlCharacters(field.Path, "field path");
            if (previous is not null && string.CompareOrdinal(previous, field.Path) > 0)
            {
                throw new InvalidOperationException(
                    $"Idempotency fields must be in ordinal-ascending order (matches spine declaration): '{previous}' must come after '{field.Path}'.");
            }

            if (!seen.Add(field.Path))
            {
                throw new InvalidOperationException($"Duplicate idempotency field '{field.Path}'.");
            }

            previous = field.Path;
        }
    }

    private static string CanonicalizeFloatingPoint(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException("Non-finite floating point values cannot be canonicalized.");
        }

        // Collapse -0.0 to +0.0 so the canonical encoding agrees with CanonicalizeDecimal's
        // isZeroMagnitude normalization. Round 4 review finding P7.
        long bits = BitConverter.DoubleToInt64Bits(value == 0.0 ? 0.0 : value);
        return "n:double:" + bits.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static string CanonicalizeFloatingPoint(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException("Non-finite floating point values cannot be canonicalized.");
        }

        // Collapse -0.0f to +0.0f for the same reason as the double overload above.
        int bits = BitConverter.SingleToInt32Bits(value == 0.0f ? 0.0f : value);
        return "n:float:" + bits.ToString("x8", CultureInfo.InvariantCulture);
    }

    private static string CanonicalizeDateTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("DateTime values must be UTC before idempotency canonicalization.");
        }

        return "t:" + value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string CanonicalizeDecimal(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        int scale = (bits[3] >> 16) & 0x7F;
        bool isZeroMagnitude = (bits[0] | bits[1] | bits[2]) == 0;
        bool negative = (bits[3] & unchecked((int)0x80000000)) != 0 && !isZeroMagnitude;
        return string.Create(CultureInfo.InvariantCulture, $"n:decimal:{(negative ? "-" : "+")}:{scale}:{bits[2]:x8}:{bits[1]:x8}:{bits[0]:x8}");
    }

    private static void RejectDuplicateProperties(JToken token, string path)
    {
        if (token is JObject obj)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JProperty property in obj.Properties())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidOperationException($"Duplicate JSON property at {path}.{property.Name}.");
                }

                RejectDuplicateProperties(property.Value, path + "." + property.Name);
            }
        }
        else if (token is JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                RejectDuplicateProperties(array[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
            }
        }
    }

    private sealed class IncludeNullContractResolver : DefaultContractResolver
    {
        public static readonly IncludeNullContractResolver Instance = new();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            property.NullValueHandling = NullValueHandling.Include;
            return property;
        }
    }
}

public readonly record struct IdempotencyField(string Path, bool Present, object? Value)
{
    public string ToCanonicalLine()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Path);
        return "field=" + HexalithIdempotencyHasher.Escape(Path) + ";present=" + Present.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + ";value=" + (Present ? HexalithIdempotencyHasher.Canonicalize(Value) : "omitted");
    }
}
