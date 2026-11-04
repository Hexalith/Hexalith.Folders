using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Folders.Tests.Queries.Folders;

/// <summary>Records native metadata requests whose source cannot provide a supported proof.</summary>
internal sealed class UnsupportedTaskScopeMetadataReader : INativeSourceMetadataReader
{
    /// <summary>Gets exact observed source identities without fabricating empty-stream authority.</summary>
    internal List<AggregateIdentity> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(identity);
        return Task.FromResult<NativeSourceObservation?>(null);
    }
}
