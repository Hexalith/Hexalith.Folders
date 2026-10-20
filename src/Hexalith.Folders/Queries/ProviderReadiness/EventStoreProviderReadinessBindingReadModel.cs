using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>Reads current provider configuration from its ordered EventStore-derived projection.</summary>
public sealed class EventStoreProviderReadinessBindingReadModel(
    IReadModelStore store, IOrganizationStreamSequenceReader sequenceReader) : IProviderReadinessBindingReader
{
    /// <summary>The shared SDK store used for provider configuration metadata.</summary>
    public const string StoreName = "statestore";

    /// <inheritdoc/>
    public async Task<OrganizationProviderBinding?> GetAsync(ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actorId = ProviderBindingActorId.Create(request.ProviderBindingRef);
        ReadModelEntry<ProviderBindingProjection> entry = await store.GetAsync<ProviderBindingProjection>(
            StoreName, Key(request.ManagedTenantId, actorId), cancellationToken).ConfigureAwait(false);
        long head = await sequenceReader.GetCurrentSequenceAsync(request.ManagedTenantId, actorId,
            cancellationToken).ConfigureAwait(false);
        if (entry.Value is null)
        {
            if (head != 0) throw new InvalidOperationException("Provider configuration projection is unavailable.");
            return null;
        }
        Validate(entry.Value, request.ManagedTenantId, actorId);
        if (head != entry.Value.LastSequence)
            throw new InvalidOperationException("Provider configuration projection is not current.");
        OrganizationProviderBinding? binding = entry.Value.Binding;
        if (binding is not null && binding.ProviderBindingRef != request.ProviderBindingRef)
            throw new InvalidOperationException("Provider configuration projection identity is invalid.");
        return binding;
    }

    /// <summary>Applies a validated configured event or rejection before subscription acknowledgement.</summary>
    public async Task ApplyAsync(string tenantId, string actorId, string messageId, long sequence,
        string digest, ProviderBindingConfigured? configured, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        if (!ProviderBindingActorId.IsReserved(actorId) || sequence < 1
            || configured is not null && (configured.ManagedTenantId != tenantId
                || configured.OrganizationId != tenantId
                || ProviderBindingActorId.Create(configured.ProviderBindingRef) != actorId))
            throw new InvalidOperationException("Provider configuration source identity is invalid.");
        string key = Key(tenantId, actorId);
        ReadModelEntry<ProviderBindingProjection> current = await store.GetAsync<ProviderBindingProjection>(
            StoreName, key, cancellationToken).ConfigureAwait(false);
        ProviderBindingProjection? prior = current.Value;
        if (prior is not null) Validate(prior, tenantId, actorId);
        FolderProjectionMessage? duplicate = prior?.Messages.FirstOrDefault(item => item.Sequence == sequence);
        if (duplicate is not null)
        {
            if (duplicate.MessageId != messageId || duplicate.PayloadDigest != digest)
                throw new InvalidOperationException("Provider configuration replay conflicts.");
            return;
        }
        if (sequence != (prior?.LastSequence ?? 0) + 1
            || prior?.Messages.Any(item => item.MessageId == messageId) == true)
            throw new PermissionProjectionConcurrencyException("Provider configuration sequence conflicts.");
        OrganizationProviderBinding? binding = prior?.Binding;
        if (configured is not null)
            binding = OrganizationState.Empty.Apply([configured]).ProviderBindings[configured.ProviderBindingRef];
        ProviderBindingProjection next = new(tenantId, actorId, sequence, binding,
            [.. prior?.Messages ?? [], new FolderProjectionMessage(sequence, messageId, digest)],
            configured is null ? prior?.Configurations ?? []
                : [.. prior?.Configurations ?? [], ProviderBindingConfigurationEvidence.Create(sequence, digest, configured)]);
        Validate(next, tenantId, actorId);
        if (!await store.TrySaveAsync(StoreName, key, next, current.ETag ?? string.Empty,
            cancellationToken).ConfigureAwait(false))
            throw new PermissionProjectionConcurrencyException("Provider configuration write conflicts.");
    }

    /// <summary>Repairs a provider view from a validated complete source fold without replacing source events.</summary>
    public async Task RebuildAsync(string tenantId, string actorId, OrganizationState state,
        IReadOnlyList<FolderProjectionMessage> messages, IReadOnlyList<ProviderBindingConfigurationEvidence> configurations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(configurations);
        if (state.ProviderBindings.Count > 1 || !ProviderBindingActorId.IsReserved(actorId))
            throw new InvalidOperationException("Provider configuration rebuild identity is invalid.");
        ProviderBindingProjection next = new(tenantId, actorId, messages.Count,
            state.ProviderBindings.Values.SingleOrDefault(), messages, configurations);
        Validate(next, tenantId, actorId);
        if (await sequenceReader.GetCurrentSequenceAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false)
            != next.LastSequence)
            throw new PermissionProjectionConcurrencyException("Provider configuration rebuild is not a complete current prefix.");
        string key = Key(tenantId, actorId);
        ReadModelEntry<ProviderBindingProjection> prior = await store.GetAsync<ProviderBindingProjection>(
            StoreName, key, cancellationToken).ConfigureAwait(false);
        if (prior.Value is { } current)
        {
            // Valid source receipts constrain replacement even when derived configuration is damaged.
            if (ValidSource(current, tenantId, actorId)
                && (current.LastSequence > next.LastSequence || current.Messages.Any(item =>
                next.Messages[(int)item.Sequence - 1] != item))
                )
                throw new PermissionProjectionConcurrencyException("Provider configuration rebuild conflicts with retained source.");
        }
        if (!await store.TrySaveAsync(StoreName, key, next, prior.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false))
            throw new PermissionProjectionConcurrencyException("Provider configuration rebuild conflicted.");
        if (await sequenceReader.GetCurrentSequenceAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false)
            != next.LastSequence)
            throw new PermissionProjectionConcurrencyException("Provider configuration source advanced during rebuild.");
    }

    private static void Validate(ProviderBindingProjection value, string tenantId, string actorId)
    {
        if (!ValidSource(value, tenantId, actorId) || value.Configurations is null
            || value.Configurations.Where((item, index) => item is null || item.Sequence < 1
                || item.Sequence > value.LastSequence || index > 0 && item.Sequence <= value.Configurations[index - 1].Sequence
                || item.SourceDigest != value.Messages[(int)item.Sequence - 1].PayloadDigest
                || item.ValueDigest is not { Length: 64 }).Any()
            || (value.Binding is null) != (value.Configurations.Count == 0)
            || value.Binding is not null && value.Configurations[^1].ValueDigest != OperationalStatusIntegrity.Digest(value.Binding)
            || value.Binding is { } binding && (binding.ManagedTenantId != tenantId
                || binding.OrganizationId != tenantId || ProviderBindingActorId.Create(binding.ProviderBindingRef) != actorId
                || binding.IdempotencyFingerprint != OrganizationProviderBindingCommandValidator.Fingerprint(
                    new ConfigureProviderBinding(binding.ManagedTenantId, binding.OrganizationId,
                        OrganizationAclPrincipalKind.User, "source", binding.ProviderBindingRef, binding.ProviderKind,
                        binding.CredentialReferenceId, binding.NamingPolicy, binding.BranchPolicy, binding.CorrelationId,
                        binding.TaskId, binding.IdempotencyKey, binding.OccurredAt, null))))
            throw new InvalidOperationException("Provider configuration projection is malformed.");
    }

    private static bool ValidSource(ProviderBindingProjection value, string tenantId, string actorId)
        => value.TenantId == tenantId && value.ActorId == actorId && value.LastSequence >= 1
            && value.Messages is not null && value.Messages.Count == value.LastSequence
            && !value.Messages.Where((item, index) => item is null || item.Sequence != index + 1
                || string.IsNullOrWhiteSpace(item.MessageId) || string.IsNullOrWhiteSpace(item.PayloadDigest)).Any()
            && value.Messages.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() == value.Messages.Count;

    /// <summary>Creates a collision-safe tenant and binding-actor key.</summary>
    public static string Key(string tenantId, string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return "projection:folders:provider-binding:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{tenantId.Length}:{tenantId}{actorId.Length}:{actorId}"))).ToLowerInvariant();
    }
}
