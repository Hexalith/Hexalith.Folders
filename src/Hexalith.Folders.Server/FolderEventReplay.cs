using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Server;

/// <summary>Reads actor-owned Folders streams through the EventStore event-evolution registry.</summary>
public sealed class FolderEventReplay(EventPayloadEvolutionRegistry evolution)
{
    private static readonly IReadOnlyDictionary<string, Type> KnownEvents = typeof(IFolderEvent).Assembly
        .GetTypes()
        .Where(static type => type.IsClass && !type.IsAbstract
            && (typeof(IFolderEvent).IsAssignableFrom(type)
                || typeof(IOrganizationEvent).IsAssignableFrom(type)))
        .ToDictionary(static type => type.FullName!, StringComparer.Ordinal);

    private readonly EventPayloadEvolutionRegistry _evolution = evolution;

    /// <summary>Rejects unregistered raw storage identifiers before SDK name canonicalization.</summary>
    internal static void RequireStoredEventName(string eventTypeName)
    {
        if (!KnownEvents.ContainsKey(eventTypeName) && !IsKnownRejection(eventTypeName))
        {
            throw new InvalidOperationException("Unknown Folders stored event identifier.");
        }
    }

    /// <summary>Identifies the frozen rejection names which EventStore records in the same stream.</summary>
    internal static bool IsKnownRejection(string eventTypeName)
        => eventTypeName == typeof(FolderCommandRejected).FullName
            || eventTypeName == typeof(DuplicateWorkspaceLockRejected).FullName
            || eventTypeName == typeof(WorkspaceTransitionInvalidRejected).FullName;

    /// <summary>Classifies a retained projection event using its registered event or rejection contract.</summary>
    internal static bool IsOrganizationProjectionEvent(ProjectionEventDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (KnownEvents.TryGetValue(item.EventTypeName, out Type? type))
        {
            return typeof(IOrganizationEvent).IsAssignableFrom(type);
        }

        if (item.EventTypeName == typeof(DuplicateWorkspaceLockRejected).FullName
            || item.EventTypeName == typeof(WorkspaceTransitionInvalidRejected).FullName)
        {
            return false;
        }

        if (item.EventTypeName != typeof(FolderCommandRejected).FullName)
        {
            throw new InvalidOperationException("Unknown Folders projection event.");
        }

        ValidateRejection(item.EventTypeName, item.StoredPayloadVersion,
            item.MetadataVersion, item.SerializationFormat, item.Payload);
        return IsOrganizationRejection(item.EventTypeName, item.Payload);
    }

    private static bool IsOrganizationRejection(string eventTypeName, byte[] bytes)
    {
        if (eventTypeName == typeof(DuplicateWorkspaceLockRejected).FullName
            || eventTypeName == typeof(WorkspaceTransitionInvalidRejected).FullName)
        {
            return false;
        }

        using JsonDocument payload = JsonDocument.Parse(bytes);
        if (!payload.RootElement.TryGetProperty(nameof(FolderCommandRejected.CommandType), out JsonElement commandType)
            && !payload.RootElement.TryGetProperty("commandType", out commandType)
            || commandType.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Unclassifiable Folders rejection event.");
        }

        return commandType.GetString() switch
        {
            FoldersServerModule.InitializeOrganizationAclBaselineCommandType
                or FoldersServerModule.GrantOrganizationAclPrincipalCommandType
                or FoldersServerModule.RevokeOrganizationAclPrincipalCommandType
                or FoldersServerModule.ConfigureProviderBindingCommandType => true,
            FoldersServerModule.CreateFolderCommandType
                or FoldersServerModule.GrantFolderAccessCommandType
                or FoldersServerModule.RevokeFolderAccessCommandType
                or FoldersServerModule.ArchiveFolderCommandType
                or FoldersServerModule.CreateRepositoryBackedFolderCommandType
                or FoldersServerModule.BindRepositoryCommandType
                or FoldersServerModule.ConfigureBranchRefPolicyCommandType
                or FoldersServerModule.PrepareWorkspaceCommandType
                or FoldersServerModule.LockWorkspaceCommandType
                or FoldersServerModule.ReleaseWorkspaceLockCommandType
                or FoldersServerModule.MutateFilesCommandType
                or FoldersServerModule.CommitWorkspaceCommandType => false,
            _ => throw new InvalidOperationException("Unclassifiable Folders rejection event."),
        };
    }

    /// <summary>Folds the command's actor-owned folder stream without querying another store.</summary>
    internal FolderState FoldFolder(object? currentState, string tenantId, string aggregateId)
    {
        DomainServiceCurrentState? source = ReadCurrentState(currentState);
        FolderState state = ReadFolderSnapshot(source?.SnapshotState, tenantId, aggregateId);
        long sequence = source?.LastSnapshotSequence ?? 0;
        List<IFolderEvent> events = [];
        foreach (EventEnvelope item in source?.Events ?? [])
        {
            RequireSequence(++sequence, item.Metadata.SequenceNumber);
            RequireIdentity(item.Metadata.TenantId, item.Metadata.AggregateId, item.Metadata.Domain, tenantId, aggregateId);
            if (IsKnownRejection(item.Metadata.EventTypeName))
            {
                ValidateRejection(item.Metadata.EventTypeName, item.Metadata.PayloadVersion,
                    item.Metadata.MetadataVersion, item.Metadata.SerializationFormat, item.Payload);
                if (IsOrganizationRejection(item.Metadata.EventTypeName, item.Payload))
                {
                    throw new InvalidOperationException("Organization rejection in folder actor stream.");
                }

                continue;
            }
            IFolderEvent value = Read<IFolderEvent>(item.Metadata.EventTypeName, item.Metadata.PayloadVersion,
                item.Metadata.MetadataVersion, item.Metadata.SerializationFormat, item.Payload, sequence);
            RequireFolderOrganization(value.OrganizationId, tenantId);
            events.Add(value);
        }

        RequireSequence(source?.CurrentSequence ?? 0, sequence);
        return state.Apply(events, FolderStreamName.Create(tenantId, aggregateId));
    }

    /// <summary>Folds the command's actor-owned organization binding stream.</summary>
    internal OrganizationState FoldOrganization(object? currentState, string tenantId, string aggregateId)
    {
        DomainServiceCurrentState? source = ReadCurrentState(currentState);
        OrganizationState state = ReadOrganizationSnapshot(source?.SnapshotState, tenantId, aggregateId);
        long sequence = source?.LastSnapshotSequence ?? 0;
        string? organizationId = state.ProviderBindings.Values.FirstOrDefault()?.OrganizationId;
        foreach (EventEnvelope item in source?.Events ?? [])
        {
            RequireSequence(++sequence, item.Metadata.SequenceNumber);
            RequireIdentity(item.Metadata.TenantId, item.Metadata.AggregateId, item.Metadata.Domain, tenantId, aggregateId);
            if (IsKnownRejection(item.Metadata.EventTypeName))
            {
                ValidateRejection(item.Metadata.EventTypeName, item.Metadata.PayloadVersion,
                    item.Metadata.MetadataVersion, item.Metadata.SerializationFormat, item.Payload);
                if (!IsOrganizationRejection(item.Metadata.EventTypeName, item.Payload))
                {
                    throw new InvalidOperationException("Folder rejection in organization actor stream.");
                }

                continue;
            }
            IOrganizationEvent value = Read<IOrganizationEvent>(item.Metadata.EventTypeName,
                item.Metadata.PayloadVersion, item.Metadata.MetadataVersion, item.Metadata.SerializationFormat,
                item.Payload, sequence);
            organizationId ??= value.OrganizationId;
            if (!string.Equals(value.ManagedTenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(value.OrganizationId, organizationId, StringComparison.Ordinal)
                || value is IOrganizationAclEvent
                    && !string.Equals(value.OrganizationId, aggregateId, StringComparison.Ordinal)
                || value is ProviderBindingConfigured binding
                    && (!string.Equals(binding.OrganizationId, tenantId, StringComparison.Ordinal)
                        || !string.Equals(ProviderBindingActorId.Create(binding.ProviderBindingRef), aggregateId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Foreign organization event in actor stream.");
            }

            state = state.Apply([value]);
        }

        RequireSequence(source?.CurrentSequence ?? 0, sequence);
        return state;
    }

    /// <summary>Folds a complete projection prefix into current folder state.</summary>
    internal static FolderState FoldFolder(ProjectionRequest request)
    {
        FolderState state = FolderState.Empty;
        long sequence = 0;
        FolderStreamName stream = FolderStreamName.Create(request.TenantId, request.AggregateId);
        List<IFolderEvent> events = [];
        foreach (ProjectionEventDto item in request.Events)
        {
            RequireSequence(++sequence, item.SequenceNumber);
            if (IsKnownRejection(item.EventTypeName))
            {
                ValidateRejection(item.EventTypeName, item.StoredPayloadVersion,
                    item.MetadataVersion, item.SerializationFormat, item.Payload);
                continue;
            }
            IFolderEvent value = ReadEffective<IFolderEvent>(item.EventTypeName,
                item.MetadataVersion, item.SerializationFormat, item.Payload);
            RequireFolderOrganization(value.OrganizationId, request.TenantId);
            events.Add(value);
        }

        return state.Apply(events, stream);
    }

    /// <summary>Folds a complete projection prefix into current organization state.</summary>
    internal static OrganizationState FoldOrganization(ProjectionRequest request)
    {
        OrganizationState state = OrganizationState.Empty;
        long sequence = 0;
        string? organizationId = null;
        foreach (ProjectionEventDto item in request.Events)
        {
            RequireSequence(++sequence, item.SequenceNumber);
            if (IsKnownRejection(item.EventTypeName))
            {
                ValidateRejection(item.EventTypeName, item.StoredPayloadVersion,
                    item.MetadataVersion, item.SerializationFormat, item.Payload);
                continue;
            }
            IOrganizationEvent value = ReadEffective<IOrganizationEvent>(item.EventTypeName,
                item.MetadataVersion, item.SerializationFormat, item.Payload);
            organizationId ??= value.OrganizationId;
            if (!string.Equals(value.ManagedTenantId, request.TenantId, StringComparison.Ordinal)
                || !string.Equals(value.OrganizationId, organizationId, StringComparison.Ordinal)
                || value is IOrganizationAclEvent
                    && !string.Equals(value.OrganizationId, request.AggregateId, StringComparison.Ordinal)
                || value is ProviderBindingConfigured binding
                    && (!string.Equals(binding.OrganizationId, request.TenantId, StringComparison.Ordinal)
                        || !string.Equals(ProviderBindingActorId.Create(binding.ProviderBindingRef), request.AggregateId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Foreign organization event in projection stream.");
            }

            state = state.Apply([value]);
        }

        return state;
    }

    private static DomainServiceCurrentState? ReadCurrentState(object? source)
    {
        if (source is null)
        {
            return null;
        }

        DomainServiceCurrentState state = DomainStateReplay.SnapshotAware(source)
            ?? throw new InvalidOperationException("Unsupported actor state shape.");
        if (state.LastSnapshotSequence < 0 || state.CurrentSequence < state.LastSnapshotSequence
            || state.LastSnapshotSequence > 0 && state.SnapshotState is null)
        {
            throw new InvalidOperationException("Missing or invalid actor snapshot anchor.");
        }
        return state.Events is null
            ? throw new InvalidOperationException("Missing actor event stream.")
            : state;
    }

    private static FolderState ReadFolderSnapshot(object? snapshot, string tenantId, string aggregateId)
    {
        FolderState state = snapshot switch
        {
            null => FolderState.Empty,
            FolderState typed => typed,
            JsonElement { ValueKind: JsonValueKind.Object } json =>
                json.Deserialize<FolderState>(EventStorePayloadSerialization.Options)
                    ?? throw new InvalidOperationException("Unreadable folder snapshot."),
            _ => throw new InvalidOperationException("Unsupported folder snapshot shape."),
        };

        if (state.Tags is null
            || state.AccessOverrides is null
            || state.IdempotencyFingerprints is null
            || state.IsCreated
                && (state.ManagedTenantId is null || state.FolderId is null)
            || state.OrganizationId is not null
                && !string.Equals(state.OrganizationId, tenantId, StringComparison.Ordinal)
            || state.ManagedTenantId is not null
                && !string.Equals(state.ManagedTenantId, tenantId, StringComparison.Ordinal)
            || state.FolderId is not null
                && !string.Equals(state.FolderId, aggregateId, StringComparison.Ordinal)
            || state.AccessOverrides.Keys.Any(key =>
                !string.Equals(key.ManagedTenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(key.FolderId, aggregateId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Foreign folder snapshot in actor stream.");
        }

        return state;
    }

    private static OrganizationState ReadOrganizationSnapshot(object? snapshot, string tenantId, string aggregateId)
    {
        OrganizationState state = snapshot switch
        {
            null => OrganizationState.Empty,
            OrganizationState typed => typed,
            JsonElement { ValueKind: JsonValueKind.Object } json =>
                ReadOrganizationJsonSnapshot(json),
            _ => throw new InvalidOperationException("Unsupported organization snapshot shape."),
        };

        if (state.Grants is null || state.IdempotencyFingerprints is null || state.ProviderBindings is null)
        {
            throw new InvalidOperationException("Incomplete organization snapshot.");
        }

        if (!state.IsInitialized && state.Grants.Count > 0)
        {
            throw new InvalidOperationException("Uninitialized organization snapshot contains grants.");
        }

        string[] organizationIds = state.Grants.Select(static grant => grant.OrganizationId)
            .Concat(state.ProviderBindings.Values.Select(static binding => binding.OrganizationId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (organizationIds.Length > 1
            || state.Grants.Any(grant =>
                !string.Equals(grant.ManagedTenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(grant.OrganizationId, aggregateId, StringComparison.Ordinal))
            || state.ProviderBindings.Any(binding =>
                !string.Equals(binding.Key, binding.Value.ProviderBindingRef, StringComparison.Ordinal)
                || !string.Equals(binding.Value.ManagedTenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(ProviderBindingActorId.Create(binding.Value.ProviderBindingRef), aggregateId, StringComparison.Ordinal)
                || !string.Equals(binding.Value.OrganizationId, tenantId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Foreign organization snapshot in actor stream.");
        }

        return state;
    }

    private static OrganizationState ReadOrganizationJsonSnapshot(JsonElement json)
    {
        JsonElement Property(string name)
        {
            JsonElement? value = null;
            foreach (JsonProperty property in json.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (value is not null)
                    {
                        throw new InvalidOperationException("Duplicate organization snapshot property.");
                    }

                    value = property.Value;
                }
            }

            return value ?? throw new InvalidOperationException("Incomplete organization snapshot.");
        }

        return new OrganizationState(
            Property(nameof(OrganizationState.IsInitialized)).GetBoolean(),
            Property(nameof(OrganizationState.Grants))
                .Deserialize<HashSet<OrganizationAclEntryKey>>(EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("Unreadable organization snapshot grants."),
            Property(nameof(OrganizationState.IdempotencyFingerprints))
                .Deserialize<Dictionary<string, string>>(EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("Unreadable organization snapshot fingerprints."),
            Property(nameof(OrganizationState.ProviderBindings))
                .Deserialize<Dictionary<string, OrganizationProviderBinding>>(EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException("Unreadable organization snapshot bindings."));
    }

    private T Read<T>(string typeName, int? payloadVersion, int? metadataVersion,
        string serializationFormat, byte[] payload, long sequence) where T : class, IEventPayload
    {
        RequireStoredEventName(typeName);
        if (metadataVersion is not null and not 1 || !string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported Folders event metadata.");
        }

        ResolvedEventPayload effective = _evolution.Read(typeName, payloadVersion, payload, sequence);
        if (effective.EventType is null || !typeof(T).IsAssignableFrom(effective.EventType))
        {
            throw new InvalidOperationException("Unknown Folders stream event.");
        }

        return (T)(JsonSerializer.Deserialize(effective.Payload, effective.EventType,
            EventStorePayloadSerialization.Options)
            ?? throw new InvalidOperationException("Unreadable Folders stream event."));
    }

    private static void ValidateRejection(string typeName, int? payloadVersion, int? metadataVersion,
        string serializationFormat, byte[] payload)
    {
        if (!IsKnownRejection(typeName) || payloadVersion is not null and not 1
            || metadataVersion is not null and not 1
            || !string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported Folders rejection event.");
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Unreadable Folders rejection event.");
        }
    }

    private static T ReadEffective<T>(string typeName, int? metadataVersion,
        string serializationFormat, byte[] payload) where T : class, IEventPayload
    {
        if (metadataVersion is not null and not 1
            || !string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || !KnownEvents.TryGetValue(typeName, out Type? type)
            || !typeof(T).IsAssignableFrom(type))
        {
            throw new InvalidOperationException("Unknown or unsupported Folders projection event.");
        }

        return (T)(JsonSerializer.Deserialize(payload, type, EventStorePayloadSerialization.Options)
            ?? throw new InvalidOperationException("Unreadable Folders projection event."));
    }

    private static void RequireSequence(long expected, long actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException("Incomplete or unordered Folders event stream.");
        }
    }

    private static void RequireFolderOrganization(string organizationId, string tenantId)
    {
        if (!string.Equals(organizationId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Foreign folder organization in actor stream.");
        }
    }

    private static void RequireIdentity(string eventTenantId, string eventAggregateId, string eventDomain,
        string tenantId, string aggregateId)
    {
        if (!string.Equals(eventTenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(eventAggregateId, aggregateId, StringComparison.Ordinal)
            || !string.Equals(eventDomain, FoldersServerModule.DomainName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Foreign Folders event in actor stream.");
        }
    }
}
