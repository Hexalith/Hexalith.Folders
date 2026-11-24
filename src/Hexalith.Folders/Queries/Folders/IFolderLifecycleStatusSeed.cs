namespace Hexalith.Folders.Queries.Folders;

/// <summary>Publishes lifecycle snapshots in the in-memory projection.</summary>
internal interface IFolderLifecycleStatusSeed
{
    /// <summary>Saves the latest snapshot for its tenant and folder.</summary>
    /// <param name="snapshot">The projected folder lifecycle status.</param>
    void Save(FolderLifecycleStatusReadModelSnapshot snapshot);
}
