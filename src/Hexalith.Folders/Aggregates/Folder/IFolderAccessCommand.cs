namespace Hexalith.Folders.Aggregates.Folder;

public interface IFolderAccessCommand : IFolderCommand
{
    IReadOnlyList<FolderAccessOperation> Operations { get; }

    IReadOnlyDictionary<string, string?> ClientControlledTenantIds { get; }

    // Typed authoritative-tenant rebind that returns the same command shape so the gate
    // does not have to runtime-cast `IFolderCommand` back to `IFolderAccessCommand`.
    IFolderAccessCommand WithAuthoritativeTenant(string managedTenantId);
}
