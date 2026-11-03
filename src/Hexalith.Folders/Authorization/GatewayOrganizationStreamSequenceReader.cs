using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Folders.Authorization;

/// <summary>Reads the current owner head through supported native metadata, preserving in-turn actor sequences.</summary>
/// <param name="metadata">The explicitly enrolled owner metadata reader.</param>
public sealed class GatewayOrganizationStreamSequenceReader(INativeSourceMetadataReader metadata) : IOrganizationStreamSequenceReader
{
    /// <inheritdoc/>
    public async Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (OrganizationActorSequenceScope.TryGet(tenantId, organizationId, out long current)
            || FolderActorSequenceScope.TryGet(tenantId, organizationId, out current)) return current;
        var identity = new AggregateIdentity(tenantId, "folders", organizationId);
        var observation = await metadata.ReadAsync(identity, cancellationToken).ConfigureAwait(false);
        if (observation?.Proof is not { Erased: false } proof
            || !NativeSourceProofCodec.IsValid(proof, identity, proof.HeadSequence, proof.RetainedFloor))
            throw new InvalidOperationException("Current organization owner metadata is unavailable.");
        cancellationToken.ThrowIfCancellationRequested();
        return proof.HeadSequence;
    }
}
