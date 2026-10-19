using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Workers;

/// <summary>Durably projects existing provider configuration before acknowledging its ordered delivery.</summary>
public sealed class FoldersProviderBindingEventProcessor(EventStoreProviderReadinessBindingReadModel readModel)
{
    private static readonly EventPayloadEvolutionRegistry Evolution = new([typeof(ProviderBindingConfigured)], []);

    /// <summary>Processes one provider-binding actor event or ignores an unrelated actor.</summary>
    public async Task ProcessAsync(EventStoreDomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Domain != "folders" || !ProviderBindingActorId.IsReserved(envelope.AggregateId)) return;
        if (envelope.MetadataVersion is not null and not 1 || envelope.EventContractType is not null
            || envelope.PayloadVersion is < 1 or > 1024 || envelope.SequenceNumber < 1
            || !string.Equals(envelope.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(envelope.MessageId) || envelope.Payload is not { Length: > 0 })
            throw new InvalidOperationException("Provider configuration source metadata is invalid.");
        ProviderBindingConfigured? configured = null;
        if (envelope.EventTypeName == typeof(ProviderBindingConfigured).FullName)
        {
            ResolvedEventPayload effective = Evolution.Read(envelope.EventTypeName, envelope.PayloadVersion,
                envelope.Payload, envelope.SequenceNumber);
            configured = JsonSerializer.Deserialize<ProviderBindingConfigured>(effective.Payload,
                EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("Provider configuration source is unreadable.");
        }
        else if (envelope.EventTypeName == "Hexalith.Folders.Server.FolderCommandRejected"
            && envelope.PayloadVersion is null or 1)
        {
            using JsonDocument json = JsonDocument.Parse(envelope.Payload);
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("CommandType", out JsonElement type) && !root.TryGetProperty("commandType", out type)
                || type.GetString() != "Hexalith.Folders.Commands.ConfigureProviderBinding"
                || !root.TryGetProperty("Code", out JsonElement code) && !root.TryGetProperty("code", out code)
                || code.ValueKind != JsonValueKind.String
                || !Enum.TryParse(code.GetString(), out FolderResultCode resultCode) || !Enum.IsDefined(resultCode)
                || resultCode == FolderResultCode.Accepted)
                throw new InvalidOperationException("Provider configuration rejection is invalid.");
        }
        else throw new InvalidOperationException("Provider configuration source type is unsupported.");
        string digest = Convert.ToHexString(SHA256.HashData(
            [.. Encoding.UTF8.GetBytes(envelope.EventTypeName + ":" + (envelope.PayloadVersion ?? 1) + ":"), .. envelope.Payload]));
        await readModel.ApplyAsync(envelope.TenantId, envelope.AggregateId, envelope.MessageId,
            envelope.SequenceNumber, digest, configured, cancellationToken).ConfigureAwait(false);
    }
}
