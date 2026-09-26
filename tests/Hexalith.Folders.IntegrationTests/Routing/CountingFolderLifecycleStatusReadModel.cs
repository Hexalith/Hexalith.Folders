using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>Counts lifecycle-status reads so a test can prove whether a handler observed protected state.</summary>
/// <param name="inner">The seeded in-memory read model.</param>
internal sealed class CountingFolderLifecycleStatusReadModel(InMemoryFolderLifecycleStatusReadModel inner)
    : IFolderLifecycleStatusReadModel
{
    private int _reads;

    /// <summary>Gets the number of lifecycle-status reads.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <inheritdoc/>
    public Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _reads);
        return inner.GetAsync(request, cancellationToken);
    }
}
