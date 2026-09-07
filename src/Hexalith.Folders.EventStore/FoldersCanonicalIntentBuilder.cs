using System.Text;
using System.Text.Json;

using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>
/// Builds Contract Spine canonical intent from a trusted command payload.
/// </summary>
internal static class FoldersCanonicalIntentBuilder
{
    public const string AdapterId = "hexalith-folders";

    public const string PolicyVersion = "folders-contract-spine-v1";

    public const int DescriptorVersion = 1;

    public static IdempotencyCanonicalIntent Create(
        IdempotencyIntentCommand command,
        IReadOnlyDictionary<string, string?> semanticFields,
        string? delegatedTaskScope = null,
        string? credentialScope = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(semanticFields);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string?> pair in semanticFields.OrderBy(static field => field.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                if (pair.Value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(pair.Value);
                }
            }

            writer.WriteEndObject();
        }

        return new IdempotencyCanonicalIntent(
            $"{command.Tenant}/{command.Domain}/{command.AggregateId}",
            stream.ToArray(),
            SemanticOptions: null,
            PolicyVersion,
            delegatedTaskScope,
            credentialScope);
    }

    public static JsonDocument ParsePayload(IdempotencyIntentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return JsonDocument.Parse(command.Payload);
    }

    public static string? ReadString(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            ? current.ToString()
            : current.ValueKind == JsonValueKind.Null
                ? null
                : current.GetRawText();
    }

    public static string? ReadTaskScope(IdempotencyIntentCommand command, JsonElement root)
    {
        string? fromPayload = ReadString(root, "taskId");
        if (!string.IsNullOrWhiteSpace(fromPayload))
        {
            return fromPayload;
        }

        if (command.Extensions is not null
            && command.Extensions.TryGetValue("taskId", out string? fromExtension)
            && !string.IsNullOrWhiteSpace(fromExtension))
        {
            return fromExtension;
        }

        return null;
    }

    public static string? ReadCanonicalObject(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            return current.ToString();
        }

        if (current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalValue(writer, current);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string? ReadCanonicalArray(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            return current.ValueKind == JsonValueKind.Null ? null : current.GetRawText();
        }

        List<string> values = [];
        foreach (JsonElement item in current.EnumerateArray())
        {
            values.Add(item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText());
        }

        values.Sort(StringComparer.Ordinal);
        return string.Join('\n', values);
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long integer))
                {
                    writer.WriteNumberValue(integer);
                }
                else
                {
                    writer.WriteNumberValue(element.GetDouble());
                }

                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
