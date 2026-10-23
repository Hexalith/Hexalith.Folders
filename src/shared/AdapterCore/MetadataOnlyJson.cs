using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Serializes metadata with the adapters' shared wire formatting and content filter.</summary>
internal static class MetadataOnlyJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new MetadataOnlyContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };

    /// <summary>Serializes a value without content-bearing properties at any depth.</summary>
    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
}
