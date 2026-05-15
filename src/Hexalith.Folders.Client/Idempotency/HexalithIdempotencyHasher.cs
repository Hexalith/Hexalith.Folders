using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hexalith.Folders.Client.Idempotency;

public static class HexalithIdempotencyHasher
{
    public static string Compute(string operationId, IEnumerable<IdempotencyField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(fields);

        List<string> lines = ["operation=" + Escape(operationId)];
        lines.AddRange(fields.Select(field => field.ToCanonicalLine()));
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
            IFormattable formattable => "n:" + formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => "j:" + NormalizeJson(value),
        };
    }

    private static string NormalizeJson(object value)
    {
        JToken token = JToken.FromObject(value, JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
        }));

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
        MemberInfo? member = value.GetType().GetMember(value.ToString()).SingleOrDefault();
        return member?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? value.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal);
}

public readonly record struct IdempotencyField(string Path, bool Present, object? Value)
{
    public string ToCanonicalLine()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Path);
        return "field=" + Path + ";present=" + Present.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() + ";value=" + (Present ? HexalithIdempotencyHasher.Canonicalize(Value) : "omitted");
    }
}
