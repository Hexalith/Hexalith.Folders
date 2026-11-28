using Hexalith.EventStore.DomainService;

[assembly: EventStoreExtension("folders", 1)]
[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Folders.EventStore.FoldersCatalogAdapterSource))]

namespace Hexalith.Folders.EventStore;

/// <summary>Returns the same installed Folders adapters for independently observed catalog composition.</summary>
public static class FoldersCatalogAdapterSource
{
    /// <summary>Creates the complete existing mutation policy inventory.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => FoldersCatalogAdapters.Create();
}
