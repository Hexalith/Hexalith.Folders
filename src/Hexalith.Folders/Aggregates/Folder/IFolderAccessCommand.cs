namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderAccessCommand : IFolderCommand
{
    IReadOnlyList<FolderAccessOperation> Operations { get; }

    IReadOnlyDictionary<string, string?> ClientControlledTenantIds { get; }
}
