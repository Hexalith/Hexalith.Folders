using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal sealed class SequencedFolderPermissionEvidenceProvider(
    params FolderPermissionEvidenceResult[] results) : IFolderPermissionEvidenceProvider
{
    private readonly Queue<FolderPermissionEvidenceResult> _results = new(results);

    public int Calls { get; private set; }

    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
        FolderPermissionEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(_results.Count > 1 ? _results.Dequeue() : _results.Peek());
    }
}
