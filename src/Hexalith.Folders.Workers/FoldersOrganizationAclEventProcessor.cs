using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Workers;

/// <summary>Projects accepted organization ACL events from the EventStore folders topic.</summary>
public sealed class FoldersOrganizationAclEventProcessor(
    EventStoreOrganizationEffectivePermissionsReadModel readModel)
{
    private static readonly EventPayloadEvolutionRegistry Evolution = new(
        [typeof(OrganizationAclBaselineInitialized), typeof(OrganizationAclPrincipalGranted),
            typeof(OrganizationAclPrincipalRevoked), typeof(ProviderBindingConfigured)], []);

    public async Task ProcessAsync(EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!string.Equals(envelope.Domain, "folders", StringComparison.Ordinal))
        {
            return;
        }

        bool bindingDelivery = envelope.EventTypeName == typeof(ProviderBindingConfigured).FullName;
        if (!bindingDelivery
            && !string.Equals(envelope.AggregateId, envelope.TenantId, StringComparison.Ordinal))
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
            throw new InvalidOperationException("Organization ACL event metadata is invalid.");
        }

        string digest = Convert.ToHexString(SHA256.HashData(
            [.. Encoding.UTF8.GetBytes(envelope.EventTypeName + ":" + (envelope.PayloadVersion ?? 1) + ":"),
                .. envelope.Payload]));

        Type? eventType = envelope.EventTypeName switch
        {
            var name when name == typeof(OrganizationAclBaselineInitialized).FullName => typeof(OrganizationAclBaselineInitialized),
            var name when name == typeof(OrganizationAclPrincipalGranted).FullName => typeof(OrganizationAclPrincipalGranted),
            var name when name == typeof(OrganizationAclPrincipalRevoked).FullName => typeof(OrganizationAclPrincipalRevoked),
            var name when name == typeof(ProviderBindingConfigured).FullName => typeof(ProviderBindingConfigured),
            _ => null,
        };
        if (eventType is null)
        {
            if (envelope.EventTypeName == "Hexalith.Folders.Server.FolderCommandRejected")
            {
                if (envelope.PayloadVersion is not null and not 1)
                {
                    throw new InvalidOperationException("Organization rejection event version is unsupported.");
                }

                using JsonDocument rejection = JsonDocument.Parse(envelope.Payload);
                if (rejection.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("Organization rejection event is unreadable.");
                }

                if (!FoldersRejectedCommandClassifier.IsOrganization(envelope.Payload))
                {
                    throw new InvalidOperationException("Folder rejection is on the organization actor.");
                }

                await readModel.ApplyNonPermissionEventAsync(envelope.TenantId, envelope.AggregateId,
                    envelope.MessageId, envelope.SequenceNumber, cancellationToken, digest).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException("Unknown organization actor event.");
        }

        ResolvedEventPayload effective = Evolution.Read(envelope.EventTypeName,
            envelope.PayloadVersion, envelope.Payload, envelope.SequenceNumber);
        if (effective.EventType != eventType)
        {
            throw new InvalidOperationException("Organization event type is unsupported.");
        }

        if (eventType == typeof(ProviderBindingConfigured))
        {
            ProviderBindingConfigured binding = (ProviderBindingConfigured)(JsonSerializer.Deserialize(
                effective.Payload, eventType, EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("Provider binding event is unreadable."));
            if (!string.Equals(binding.ManagedTenantId, envelope.TenantId, StringComparison.Ordinal)
                || !string.Equals(binding.OrganizationId, envelope.TenantId, StringComparison.Ordinal)
                || !string.Equals(ProviderBindingActorId.Create(binding.ProviderBindingRef), envelope.AggregateId, StringComparison.Ordinal)
                || string.Equals(envelope.AggregateId, envelope.TenantId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Provider binding event identity is invalid.");
            }
            // Binding actors are independent of the organization baseline and have no
            // folder permission projection. Validation precedes acknowledgement.
            return;
        }

        IOrganizationAclEvent @event = JsonSerializer.Deserialize(effective.Payload, eventType,
            EventStorePayloadSerialization.Options) as IOrganizationAclEvent
            ?? throw new InvalidOperationException("Organization ACL event payload is invalid.");
        if (@event is OrganizationAclPrincipalGranted grant && !Enum.IsDefined(grant.PrincipalKind)
            || @event is OrganizationAclPrincipalRevoked revoke && !Enum.IsDefined(revoke.PrincipalKind))
        {
            throw new InvalidOperationException("Organization ACL event principal kind is invalid.");
        }
        await readModel.ApplyAsync(envelope.TenantId, envelope.AggregateId, envelope.MessageId,
            envelope.SequenceNumber, @event, cancellationToken, digest).ConfigureAwait(false);
    }
}
