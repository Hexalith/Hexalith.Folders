using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Folders.Tests.Authorization;

/// <summary>Provides explicit native metadata evidence for sequence adapter controls.</summary>
internal sealed class OwnerMetadataReaderStub : INativeSourceMetadataReader
{
    /// <summary>Gets or sets the calls control.</summary>
    internal int Calls { get; private set; }
    /// <summary>Gets or sets the missing control.</summary>
    internal bool Missing { get; set; }
    /// <summary>Gets or sets the foreign control.</summary>
    internal bool Foreign { get; set; }
    /// <inheritdoc/>
    public Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Missing) return Task.FromResult<NativeSourceObservation?>(null);
        if (Foreign) identity = new AggregateIdentity("foreign", identity.Domain, identity.AggregateId);
        var proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        return Task.FromResult<NativeSourceObservation?>(new(proof, DateTimeOffset.UtcNow));
    }
}
