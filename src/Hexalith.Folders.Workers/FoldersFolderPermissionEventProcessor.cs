using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Workers;

/// <summary>Advances the durable folder permission view for every accepted folder actor event.</summary>
public sealed class FoldersFolderPermissionEventProcessor(
    EventStoreFolderEffectivePermissionsReadModel readModel)
{
    private static readonly Type[] FolderTypes = typeof(IFolderEvent).Assembly.GetTypes()
        .Where(static type => type.IsClass && !type.IsAbstract
            && typeof(IFolderEvent).IsAssignableFrom(type)).ToArray();
    private static readonly Dictionary<string, Type> KnownTypes = FolderTypes
        .ToDictionary(static type => type.FullName!, StringComparer.Ordinal);
    private static readonly EventPayloadEvolutionRegistry Evolution = new(FolderTypes, []);

    public async Task ProcessAsync(EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Domain != "folders")
        {
            return;
        }

        if (envelope.AggregateId == envelope.TenantId
            || envelope.EventTypeName == typeof(ProviderBindingConfigured).FullName)
        {
            return;
        }

        if (envelope.MetadataVersion is not null and not 1
            || envelope.EventContractType is not null
            || envelope.PayloadVersion is < 1 or > 1024
            || !string.Equals(envelope.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || envelope.Payload is null || envelope.Payload.Length == 0
            || string.IsNullOrWhiteSpace(envelope.MessageId)
            || envelope.SequenceNumber < 1)
        {
            throw new InvalidOperationException("Folder event metadata is invalid.");
        }

        if (ProviderBindingActorId.IsReserved(envelope.AggregateId)
            && envelope.EventTypeName != "Hexalith.Folders.Server.FolderCommandRejected")
        {
            throw new InvalidOperationException("Non-binding event targets a provider-binding actor.");
        }

        string digest = Convert.ToHexString(SHA256.HashData(
            [.. Encoding.UTF8.GetBytes(envelope.EventTypeName + ":" + (envelope.PayloadVersion ?? 1) + ":"),
                .. envelope.Payload]));
        IFolderEvent? folderEvent = null;
        if (KnownTypes.TryGetValue(envelope.EventTypeName, out Type? expected))
        {
            ResolvedEventPayload effective = Evolution.Read(envelope.EventTypeName,
                envelope.PayloadVersion, envelope.Payload, envelope.SequenceNumber);
            if (effective.EventType != expected)
            {
                throw new InvalidOperationException("Folder event type is unsupported.");
            }

            folderEvent = JsonSerializer.Deserialize(effective.Payload, expected,
                EventStorePayloadSerialization.Options) as IFolderEvent
                ?? throw new InvalidOperationException("Folder event payload is unreadable.");
        }
        else if (envelope.EventTypeName is "Hexalith.Folders.Server.FolderCommandRejected"
            or "Hexalith.Folders.Server.DuplicateWorkspaceLockRejected"
            or "Hexalith.Folders.Server.WorkspaceTransitionInvalidRejected")
        {
            if (envelope.PayloadVersion is not null and not 1)
            {
                throw new InvalidOperationException("Folder rejection version is unsupported.");
            }

            using JsonDocument rejection = JsonDocument.Parse(envelope.Payload);
            if (rejection.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Folder rejection payload is unreadable.");
            }

            if (ProviderBindingActorId.IsReserved(envelope.AggregateId))
            {
                if (envelope.EventTypeName != "Hexalith.Folders.Server.FolderCommandRejected"
                    || !HasProviderBindingCommand(envelope.Payload))
                {
                    throw new InvalidOperationException("Non-binding rejection targets a provider-binding actor.");
                }

                return;
            }

            if (envelope.EventTypeName == "Hexalith.Folders.Server.FolderCommandRejected"
                && FoldersRejectedCommandClassifier.IsOrganization(envelope.Payload))
            {
                if (HasProviderBindingCommand(envelope.Payload))
                {
                    throw new InvalidOperationException("Provider-binding rejection is on a folder actor.");
                }

                throw new InvalidOperationException("Organization rejection is on a folder actor.");
            }
        }
        else
        {
            throw new InvalidOperationException("Unknown folder actor event.");
        }

        await readModel.ApplyAsync(envelope.TenantId, envelope.AggregateId, envelope.MessageId,
            envelope.SequenceNumber, digest, folderEvent, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasProviderBindingCommand(byte[] payload)
    {
        using JsonDocument json = JsonDocument.Parse(payload);
        JsonElement root = json.RootElement;
        return (root.TryGetProperty("CommandType", out JsonElement type)
                || root.TryGetProperty("commandType", out type))
            && type.GetString() == "Hexalith.Folders.Commands.ConfigureProviderBinding";
    }
}
