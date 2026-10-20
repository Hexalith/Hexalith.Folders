using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

/// <summary>Records synthetic binding admission for mutation ordering tests; it establishes no production authority.</summary>
internal sealed class RecordingFileReferenceBindingAuthority : IFolderFileReferenceBindingAuthority
{
    public List<FolderFileReferenceBindingRequest> Requests { get; } = [];

    public Task<bool> AuthorizeAsync(FolderFileReferenceBindingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(true);
    }
}
