using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Tests.Queries.Folders;

internal sealed class ThrowingLifecycleStatusReadModel : IFolderLifecycleStatusReadModel
{
    internal int Requests { get; private set; }

    public Task<FolderLifecycleStatusReadModelResult> GetAsync(
        FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return Task.FromException<FolderLifecycleStatusReadModelResult>(
            new InvalidOperationException("read-model failure"));
    }
}
