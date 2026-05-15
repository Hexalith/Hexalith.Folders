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
            DateTime dateTime => "t:" + dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => "t:" + dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            decimal number => "n:" + number.ToString("G29", CultureInfo.InvariantCulture),
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

        return SortToken(token).ToString(Formatting.None);
    }

    internal static string NormalizeJsonText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
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
        string enumName = value.ToString();
        if (enumName.Contains(',', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Composite enum value '{enumName}' cannot be canonicalized.");
        }

        MemberInfo? member = value.GetType().GetMember(enumName).SingleOrDefault();
        if (member is null)
        {
            throw new InvalidOperationException($"Unknown enum value '{enumName}' cannot be canonicalized.");
        }

        return member?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? value.ToString();
    }

    private static string Escape(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
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
                >= '\u007f' and <= '\u009f' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }

        return builder.ToString();
    }

    private static void EnsureDeclaredOrder(IReadOnlyList<IdempotencyField> fields)
    {
        string? previous = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IdempotencyField field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Path);
            if (previous is not null && string.CompareOrdinal(previous, field.Path) > 0)
            {
                throw new InvalidOperationException("Idempotency fields must be supplied in declared lexicographic order.");
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

        return "n:" + value.ToString("R", CultureInfo.InvariantCulture);
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
                    throw new JsonReaderException($"Duplicate JSON property at {path}.{property.Name}.");
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
        return "field=" + HexalithIdempotencyHasher.Canonicalize(Path)[2..] + ";present=" + Present.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + ";value=" + (Present ? HexalithIdempotencyHasher.Canonicalize(Value) : "omitted");
    }
}
