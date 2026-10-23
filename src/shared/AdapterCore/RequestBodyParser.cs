using Newtonsoft.Json;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Deserializes adapter request bodies with the SDK serializer's defaults.</summary>
internal static class RequestBodyParser
{
    /// <summary>Reads JSON, default-constructing omitted or JSON-null bodies.</summary>
    public static T Read<T>(string? json)
        where T : new()
        => string.IsNullOrWhiteSpace(json) ? new T() : JsonConvert.DeserializeObject<T>(json) ?? new T();
}
