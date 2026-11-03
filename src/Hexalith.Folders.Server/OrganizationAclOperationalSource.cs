using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Server;

/// <summary>Recognizes the finite same-domain organization ACL source contracts that produce no Folder operational views.</summary>
internal static class OrganizationAclOperationalSource
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new[]
    {
        typeof(OrganizationAclBaselineInitialized), typeof(OrganizationAclPrincipalGranted), typeof(OrganizationAclPrincipalRevoked),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    /// <summary>Validates every known ACL event against its real source scope before permitting an empty domain fold.</summary>
    /// <remarks>The SDK bounded V1 producer omits PayloadVersion for declared version one; null and explicit one are the same supported contract here.</remarks>
    internal static bool IsCompletePrefix(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Events is null || !request.Events.Any(item => Types.ContainsKey(item.EventTypeName))) return false;
        if (request.Domain != "folders" || !OrganizationStreamName.TryCreate(request.TenantId, request.AggregateId, out _, out _)
            || request.Events.Length == 0 || request.Events.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() != request.Events.Length)
            throw new InvalidOperationException("Invalid organization ACL operational source scope.");
        long? prior = null;
        long priorPosition = 0;
        foreach (ProjectionEventDto source in request.Events)
        {
            if (!Types.TryGetValue(source.EventTypeName, out Type? type) || EventPayloadVersionResolver.GetDeclaredVersion(type) != 1 || source.MetadataVersion != 1 || source.StoredPayloadVersion is not (null or 1)
                || source.SequenceNumber <= 0 || source.GlobalPosition <= priorPosition || prior is { } sequence && source.SequenceNumber != sequence + 1
                || string.IsNullOrWhiteSpace(source.MessageId) || string.IsNullOrWhiteSpace(source.CorrelationId) || source.Timestamp == default
                || !string.Equals(source.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase) || source.Payload is not { Length: > 0 })
                throw new InvalidOperationException("Invalid organization ACL operational source metadata.");
            var value = JsonSerializer.Deserialize(source.Payload, type, EventStorePayloadSerialization.Options) as IOrganizationAclEvent
                ?? throw new InvalidOperationException("Invalid organization ACL operational source payload.");
            if (value.ManagedTenantId != request.TenantId || value.OrganizationId != request.AggregateId || value.CorrelationId != source.CorrelationId
                || string.IsNullOrWhiteSpace(value.TaskId) || string.IsNullOrWhiteSpace(value.IdempotencyKey) || string.IsNullOrWhiteSpace(value.IdempotencyFingerprint))
                throw new InvalidOperationException("Organization ACL operational source binding differs.");
            IOrganizationAclCommand? command = value switch
            {
                OrganizationAclPrincipalGranted grant => new GrantOrganizationAclPrincipal(grant.ManagedTenantId, grant.OrganizationId,
                    grant.PrincipalKind, grant.PrincipalId, grant.Action, grant.CorrelationId, grant.TaskId, grant.IdempotencyKey, null),
                OrganizationAclPrincipalRevoked revoke => new RevokeOrganizationAclPrincipal(revoke.ManagedTenantId, revoke.OrganizationId,
                    revoke.PrincipalKind, revoke.PrincipalId, revoke.Action, revoke.CorrelationId, revoke.TaskId, revoke.IdempotencyKey, null),
                _ => null,
            };
            if (command is not null && !OrganizationAclCommandValidator.Validate(command).IsAccepted)
                throw new InvalidOperationException("Organization ACL operational source principal or action is invalid.");
            prior = source.SequenceNumber;
            priorPosition = source.GlobalPosition;
        }
        return true;
    }
}
