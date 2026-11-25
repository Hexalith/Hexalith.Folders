using System.Text.Json;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Serializes metadata with the adapters' shared wire formatting and content filter.</summary>
internal static class MetadataOnlyJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new MetadataOnlyContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };

    /// <summary>Serializes a value without content-bearing properties at any depth.</summary>
    public static string Serialize(object value)
    {
        JsonSerializer serializer = JsonSerializer.CreateDefault(Settings);
        using JTokenWriter writer = new();
        serializer.Serialize(writer, value);
        JToken token = writer.Token is JRaw raw ? ParseRawValue(raw) : writer.Token!;
        RemoveContentProperties(token);

        if (token is JContainer container)
        {
            foreach (JRaw nested in container.Descendants().OfType<JRaw>().ToArray())
            {
                JToken parsed = ParseRawValue(nested);
                RemoveContentProperties(parsed);
                nested.Replace(parsed);
            }
        }

        using StringWriter output = new(serializer.Culture);
        using JsonTextWriter json = new(output)
        {
            Culture = serializer.Culture,
            Formatting = Formatting.Indented,
            DateFormatHandling = serializer.DateFormatHandling,
            DateFormatString = serializer.DateFormatString,
            DateTimeZoneHandling = serializer.DateTimeZoneHandling,
            FloatFormatHandling = serializer.FloatFormatHandling,
            StringEscapeHandling = serializer.StringEscapeHandling,
        };
        token.WriteTo(json);
        return output.ToString();
    }

    private static void RemoveContentProperties(JToken token)
    {
        // Dictionary entries and JSON tokens bypass the reflected property resolver.
        if (token is JContainer container)
        {
            foreach (JProperty property in container.Descendants().OfType<JProperty>()
                .Where(property => MetadataOnlyContractResolver.IsContentProperty(property.Name)).ToArray())
            {
                property.Remove();
            }
        }
    }

    private static JToken ParseRawValue(JRaw raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw.ToString());
        return ConvertRawValue(document.RootElement);
    }

    private static JToken ConvertRawValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => new JObject(element.EnumerateObject()
            .Select(property => new JProperty(property.Name, ConvertRawValue(property.Value)))),
        JsonValueKind.Array => new JArray(element.EnumerateArray().Select(ConvertRawValue)),
        // The parser has validated each leaf. Retain its spelling, including numeric precision.
        _ => new JRaw(element.GetRawText()),
    };
}
