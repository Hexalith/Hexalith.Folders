using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Workers;

/// <summary>Projects accepted organization ACL events from the EventStore folders topic.</summary>
public sealed class FoldersOrganizationAclEventProcessor(
    EventStoreOrganizationEffectivePermissionsReadModel readModel)
{
    public async Task ProcessAsync(EventStoreDomainEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Type? eventType = envelope.EventTypeName switch
        {
            var name when name == typeof(OrganizationAclBaselineInitialized).FullName => typeof(OrganizationAclBaselineInitialized),
            var name when name == typeof(OrganizationAclPrincipalGranted).FullName => typeof(OrganizationAclPrincipalGranted),
            var name when name == typeof(OrganizationAclPrincipalRevoked).FullName => typeof(OrganizationAclPrincipalRevoked),
            _ => null,
        };
        if (eventType is null)
        {
            if (string.Equals(envelope.Domain, "folders", StringComparison.Ordinal)
                && string.Equals(envelope.AggregateId, envelope.TenantId, StringComparison.Ordinal)
                && (envelope.EventTypeName is "Hexalith.Folders.Server.FolderCommandRejected"
                    or "Hexalith.Folders.Aggregates.Organization.ProviderBindingConfigured"))
            {
                await readModel.ApplyNonPermissionEventAsync(envelope.TenantId, envelope.AggregateId,
                    envelope.MessageId, envelope.SequenceNumber, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (!string.Equals(envelope.Domain, "folders", StringComparison.Ordinal)
            || envelope.Payload is null || envelope.Payload.Length == 0)
        {
            throw new InvalidOperationException("Organization ACL event envelope is invalid.");
        }

        IOrganizationAclEvent @event = JsonSerializer.Deserialize(envelope.Payload, eventType)
            as IOrganizationAclEvent
            ?? throw new InvalidOperationException("Organization ACL event payload is invalid.");
        await readModel.ApplyAsync(envelope.TenantId, envelope.AggregateId, envelope.MessageId,
            envelope.SequenceNumber, @event, cancellationToken).ConfigureAwait(false);
    }
}
