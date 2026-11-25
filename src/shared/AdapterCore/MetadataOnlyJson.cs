using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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
        using JTokenWriter writer = new();
        JsonSerializer.Create(Settings).Serialize(writer, value);
        JToken output = writer.Token!;

        // Dictionary entries and JSON tokens bypass the reflected property resolver.
        if (output is JContainer container)
        {
            foreach (JProperty property in container.Descendants().OfType<JProperty>()
                .Where(property => MetadataOnlyContractResolver.IsContentProperty(property.Name)).ToArray())
            {
                property.Remove();
            }
        }

        return output.ToString(Formatting.Indented);
    }
}
