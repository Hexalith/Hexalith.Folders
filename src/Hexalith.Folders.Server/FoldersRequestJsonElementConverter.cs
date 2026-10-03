using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Server;

/// <summary>Rejects ambiguous properties in untyped request objects before typed validation or gateway admission.</summary>
internal sealed class FoldersRequestJsonElementConverter : JsonConverter<JsonElement>
{
    /// <inheritdoc />
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        ValidateUniqueProperties(document.RootElement);
        return document.RootElement.Clone();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        => value.WriteTo(writer);

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("Request contains duplicate JSON properties.");
                }

                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateUniqueProperties(item);
            }
        }
    }
}
