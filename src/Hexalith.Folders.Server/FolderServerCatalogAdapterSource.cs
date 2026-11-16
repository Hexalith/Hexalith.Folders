using Hexalith.EventStore.DomainService;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Folders.Server.FolderServerCatalogAdapterSource))]

namespace Hexalith.Folders.Server;

/// <summary>Exposes this actual owner's existing compiled command policies to canonical SDK catalog preparation.</summary>
public static class FolderServerCatalogAdapterSource
{
    /// <summary>Returns the same module adapter implementation used by the existing Folders command gateway.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create()
        => Hexalith.Folders.EventStore.FoldersCatalogAdapters.Create();
}
