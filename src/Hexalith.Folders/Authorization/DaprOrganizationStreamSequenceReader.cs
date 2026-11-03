using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Folders.Authorization;

/// <summary>Reads only validated source-head metadata through the independently enrolled public SDK reader.</summary>
public sealed class DaprOrganizationStreamSequenceReader(INativeSourceMetadataReader source)
    : IOrganizationStreamSequenceReader
{
    public async Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        cancellationToken.ThrowIfCancellationRequested();
        // Organization ACL commands are already inside this same EventStore actor.
        // Calling it through Dapr here would wait for the actor to release itself.
        if (OrganizationActorSequenceScope.TryGet(tenantId, organizationId, out long currentSequence))
        {
            return currentSequence;
        }

        if (FolderActorSequenceScope.TryGet(tenantId, organizationId, out currentSequence))
        {
            return currentSequence;
        }

        var identity = new AggregateIdentity(tenantId, "folders", organizationId);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var observation = await source.ReadAsync(identity, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
            if (observation?.Proof is not { Erased: false } proof
                || !NativeSourceProofCodec.IsValid(proof, identity, proof.HeadSequence, proof.RetainedFloor))
            {
                throw new InvalidOperationException("The independently observed source head is unavailable.");
            }
            return proof.HeadSequence;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The organization actor sequence request timed out.", ex);
        }
    }
}
