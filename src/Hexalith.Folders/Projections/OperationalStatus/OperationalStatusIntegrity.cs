using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Canonical metadata evidence independent of dictionary enumeration and JSON round trips.</summary>
internal static class OperationalStatusIntegrity
{
    /// <summary>Hashes the source-derived checkpoint, excluding its own integrity field.</summary>
    internal static string Checkpoint(OperationalStatusCheckpoint value) => Digest(new
    {
        value.SchemaVersion,
        value.ManagedTenantId,
        value.FolderId,
        value.Sequence,
        value.SourceDigest,
        value.ObservedAt,
        value.State,
        value.EventDigests,
        value.TaskClosedSequences,
        value.MessageIds,
        value.WorkspaceEvidence,
        value.TaskEvidence,
    });

    /// <summary>Hashes all fields of a materialized source-derived snapshot.</summary>
    internal static string Digest<T>(T value)
    {
        using MemoryStream bytes = new();
        using (Utf8JsonWriter writer = new(bytes))
        {
            Write(writer, JsonSerializer.SerializeToElement(value));
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in value.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
