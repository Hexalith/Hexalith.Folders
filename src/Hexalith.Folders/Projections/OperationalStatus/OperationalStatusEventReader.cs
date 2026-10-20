using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Validates stored delivery and already-upcast SDK replay at their respective trust boundaries.</summary>
public static class OperationalStatusEventReader
{
    private static readonly Type[] Types = typeof(IFolderEvent).Assembly.GetTypes()
        .Where(static type => type.IsClass && !type.IsAbstract && typeof(IFolderEvent).IsAssignableFrom(type)).ToArray();
    private static readonly IReadOnlyDictionary<string, Type> Known = Types.ToDictionary(static type => type.FullName!, StringComparer.Ordinal);
    private static readonly EventPayloadEvolutionRegistry Evolution = new(Types,
        EventPayloadEvolutionRegistry.DiscoverUpcasters(typeof(IFolderEvent).Assembly));

    /// <summary>Reads the original subscription bytes through the governed event-evolution registry.</summary>
    public static OperationalStatusEvent ReadStored(EventStoreDomainEventEnvelope source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Domain != "folders" || source.EventContractType is not null)
        {
            throw new InvalidOperationException("Unsupported operational source metadata.");
        }
        return Read(source.EventTypeName, source.PayloadVersion, source.MetadataVersion, source.SerializationFormat,
            source.SequenceNumber, source.MessageId, source.Timestamp, source.CorrelationId, source.Payload, true) with
        {
            Source = new ProjectionEventDto(source.EventTypeName, source.Payload, source.SerializationFormat,
                source.SequenceNumber, source.Timestamp, source.CorrelationId, source.MessageId, source.UserId,
                source.GlobalPosition ?? 0)
            { MetadataVersion = source.MetadataVersion, StoredPayloadVersion = source.PayloadVersion },
        };
    }

    /// <summary>Reads original full-prefix bytes supplied to the legacy protected SDK route.</summary>
    public static OperationalStatusEvent ReadStored(ProjectionEventDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Read(source.EventTypeName, source.StoredPayloadVersion, source.MetadataVersion, source.SerializationFormat,
            source.SequenceNumber, source.MessageId, source.Timestamp, source.CorrelationId, source.Payload, true) with
        { Source = source };
    }

    /// <summary>Reads current-semantic payloads that the SDK has already upcast.</summary>
    public static OperationalStatusEvent ReadEffective(ProjectionEventDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Read(source.EventTypeName, source.StoredPayloadVersion, source.MetadataVersion, source.SerializationFormat,
            source.SequenceNumber, source.MessageId, source.Timestamp, source.CorrelationId, source.Payload, false) with
        { Source = source };
    }

    private static OperationalStatusEvent Read(string name, int? version, int? metadata, string format,
        long sequence, string? message, DateTimeOffset timestamp, string correlation, byte[] bytes, bool stored)
    {
        if (metadata is not null and not 1 || version is < 1 or > 1024 || sequence < 1
            || string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(correlation)
            || timestamp == default || bytes is null || bytes.Length == 0
            || !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid operational source metadata.");
        }
        IFolderEvent? value;
        if (Known.TryGetValue(name, out Type? type))
        {
            byte[] effective = stored ? Evolution.Read(name, version, bytes, sequence).Payload : bytes;
            value = JsonSerializer.Deserialize(effective, type, EventStorePayloadSerialization.Options) as IFolderEvent
                ?? throw new InvalidOperationException("Invalid operational event payload.");
            if (string.IsNullOrWhiteSpace(value.TaskId) || string.IsNullOrWhiteSpace(value.CorrelationId)
                || value.CorrelationId != correlation || value.OccurredAt == default)
            {
                throw new InvalidOperationException("Invalid operational event evidence.");
            }
        }
        else if (name is "Hexalith.Folders.Server.FolderCommandRejected"
            or "Hexalith.Folders.Server.DuplicateWorkspaceLockRejected"
            or "Hexalith.Folders.Server.WorkspaceTransitionInvalidRejected")
        {
            if (version is not null and not 1)
            {
                throw new InvalidOperationException("Unsupported operational rejection version.");
            }
            using JsonDocument json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Invalid operational rejection.");
            }
            JsonElement root = json.RootElement;
            if (!(root.TryGetProperty("Code", out JsonElement code) || root.TryGetProperty("code", out code))
                || code.ValueKind != JsonValueKind.String || !Enum.TryParse<FolderResultCode>(code.GetString(), out FolderResultCode resultCode) || !Enum.IsDefined(resultCode)
                || resultCode == FolderResultCode.Accepted
                || !(root.TryGetProperty("CommandType", out JsonElement command) || root.TryGetProperty("commandType", out command))
                || command.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(command.GetString()))
            {
                throw new InvalidOperationException("Malformed operational rejection metadata.");
            }
            value = null;
        }
        else
        {
            throw new InvalidOperationException("Unknown operational source event.");
        }
        string digest = OperationalStatusKeys.Digest(name, (version ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (metadata ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture), format.ToLowerInvariant(),
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), message,
            timestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture), correlation, Convert.ToBase64String(bytes));
        return new(sequence, message, timestamp, digest, value);
    }
}
