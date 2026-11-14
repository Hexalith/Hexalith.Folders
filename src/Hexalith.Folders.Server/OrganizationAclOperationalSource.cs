using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Server;

/// <summary>Validates finite organization/provider source contracts which produce no Folder operational views.</summary>
internal static class OrganizationAclOperationalSource
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new[]
    {
        typeof(OrganizationAclBaselineInitialized), typeof(OrganizationAclPrincipalGranted),
        typeof(OrganizationAclPrincipalRevoked), typeof(ProviderBindingConfigured),
    }.ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    /// <summary>Validates every non-Folder source position before permitting an empty domain fold.</summary>
    /// <remarks>Live delivery can contain a suffix; a captured rebuild partition must start at position one.</remarks>
    internal static bool IsCompletePrefix(ProjectionRequest request, bool requireCompletePrefix = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool organization = request.AggregateId == request.TenantId;
        bool provider = ProviderBindingActorId.IsReserved(request.AggregateId);
        if (!organization && !provider && request.Events?.Any(item => Types.ContainsKey(item.EventTypeName)) != true) return false;
        if (request.Domain != "folders" || !OrganizationStreamName.TryCreate(request.TenantId, request.TenantId, out _, out _)
            || !organization && !provider || request.Events is not { Length: > 0 }
            || request.Events.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() != request.Events.Length
            || requireCompletePrefix && request.Events[0].SequenceNumber != 1)
            throw new InvalidOperationException("Invalid non-Folder operational source scope.");
        List<IOrganizationEvent> accepted = [];
        long? prior = null;
        long priorPosition = 0;
        foreach (ProjectionEventDto source in request.Events)
        {
            if (source.MetadataVersion is not (null or 1) || source.StoredPayloadVersion is not (null or 1)
                || source.SequenceNumber <= 0 || source.GlobalPosition <= priorPosition || prior is { } sequence && source.SequenceNumber != sequence + 1
                || string.IsNullOrWhiteSpace(source.MessageId) || string.IsNullOrWhiteSpace(source.CorrelationId) || source.Timestamp == default
                || !string.Equals(source.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase) || source.Payload is not { Length: > 0 })
                throw new InvalidOperationException("Invalid non-Folder operational source metadata.");
            if (source.EventTypeName == typeof(FolderCommandRejected).FullName)
            {
                FoldersSourceOwnerKind kind = FoldersRejectionSource.ReadOwner(source.Payload, request.TenantId,
                    request.AggregateId, source.CorrelationId);
                if (kind != (provider ? FoldersSourceOwnerKind.ProviderBinding : FoldersSourceOwnerKind.Organization))
                    throw new InvalidOperationException("Non-Folder rejection owner differs from its source.");
            }
            else
            {
                if (!Types.TryGetValue(source.EventTypeName, out Type? type))
                    throw new InvalidOperationException("Unknown non-Folder operational source contract.");
                var value = JsonSerializer.Deserialize(source.Payload, type, EventStorePayloadSerialization.Options) as IOrganizationEvent
                    ?? throw new InvalidOperationException("Invalid non-Folder operational source payload.");
                if (value.ManagedTenantId != request.TenantId || value.OrganizationId != request.TenantId || value.CorrelationId != source.CorrelationId
                    || string.IsNullOrWhiteSpace(value.TaskId) || string.IsNullOrWhiteSpace(value.IdempotencyKey) || string.IsNullOrWhiteSpace(value.IdempotencyFingerprint))
                    throw new InvalidOperationException("Non-Folder operational source binding differs.");
                accepted.Add(value);
                if (value is ProviderBindingConfigured binding)
                {
                    if (!provider || ProviderBindingActorId.Create(binding.ProviderBindingRef) != request.AggregateId
                        || binding.OccurredAt == default || binding.ConfiguredStatus != "configured")
                        throw new InvalidOperationException("Provider operational source owner or status differs.");
                    var command = new ConfigureProviderBinding(binding.ManagedTenantId, binding.OrganizationId,
                        OrganizationAclPrincipalKind.User, "projection", binding.ProviderBindingRef, binding.ProviderKind,
                        binding.CredentialReferenceId, binding.NamingPolicy, binding.BranchPolicy, binding.CorrelationId,
                        binding.TaskId, binding.IdempotencyKey, binding.OccurredAt, null);
                    var validation = OrganizationProviderBindingCommandValidator.Validate(command);
                    if (!validation.IsAccepted || validation.IdempotencyFingerprint != binding.IdempotencyFingerprint)
                        throw new InvalidOperationException("Provider operational source configuration is invalid.");
                }
                else
                {
                    if (!organization) throw new InvalidOperationException("ACL operational source owner differs.");
                    IOrganizationAclCommand? command = value switch
                    {
                        OrganizationAclPrincipalGranted grant => new GrantOrganizationAclPrincipal(grant.ManagedTenantId, grant.OrganizationId,
                            grant.PrincipalKind, grant.PrincipalId, grant.Action, grant.CorrelationId, grant.TaskId, grant.IdempotencyKey, null),
                        OrganizationAclPrincipalRevoked revoke => new RevokeOrganizationAclPrincipal(revoke.ManagedTenantId, revoke.OrganizationId,
                            revoke.PrincipalKind, revoke.PrincipalId, revoke.Action, revoke.CorrelationId, revoke.TaskId, revoke.IdempotencyKey, null),
                        _ => null,
                    };
                    if (command is not null && !OrganizationAclCommandValidator.Validate(command).IsAccepted)
                        throw new InvalidOperationException("ACL operational source principal or action is invalid.");
                }
            }
            prior = source.SequenceNumber;
            priorPosition = source.GlobalPosition;
        }
        if (requireCompletePrefix || request.Events[0].SequenceNumber == 1)
            _ = OrganizationState.Empty.Apply(accepted);
        return true;
    }
}
