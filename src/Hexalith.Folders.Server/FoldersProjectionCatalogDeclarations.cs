using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Projections.OperationalStatus;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Folders.Server.FoldersProjectionCatalogDeclarations))]

namespace Hexalith.Folders.Server;

/// <summary>Declares the operational projection implemented by this host; command admission is independently owned.</summary>
public static class FoldersProjectionCatalogDeclarations
{
    /// <summary>Gets the callable named projection and its complete logical output.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } =
    [
        new("folders-operational-status", "projection", "folders", null, OperationalStatusKeys.ProjectionType, "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new(typeof(OperationalStatusProjectionHandler).FullName!, "exact", [OperationalStatusKeys.ProjectionType],
                "domain-service", null, [OperationalStatusKeys.ProjectionType])),
    ];
}
