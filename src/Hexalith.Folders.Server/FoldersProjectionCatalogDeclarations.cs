using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Projections.OperationalStatus;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Folders.Server.FoldersProjectionCatalogDeclarations))]

namespace Hexalith.Folders.Server;

/// <summary>Declares the operational projection implemented by this host; command admission is independently owned.</summary>
public static class FoldersProjectionCatalogDeclarations
{
    /// <summary>Gets no command policies; this source owns only the named projection.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands => [];

    /// <summary>Gets the actual named receiver method.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => new Dictionary<string, string>
    {
        ["folders-operational-status"] = "project/v2",
    };

    /// <summary>Gets the internal SDK named-dispatch schemas.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route => RouteOperationSchema.Create(route,
        typeof(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchRequest), typeof(Hexalith.EventStore.Contracts.Projections.ProjectionDispatchResponse),
        "internal", "agent-eligible", System.Text.Json.JsonSerializerOptions.Web)).ToArray();

    /// <summary>Gets the callable named projection and its complete logical output.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } =
    [
        new("folders-operational-status", "projection", "folders", null, OperationalStatusKeys.ProjectionType, "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new(typeof(OperationalStatusProjectionHandler).FullName!, "exact", [OperationalStatusKeys.ProjectionType],
                "domain-service", null, [OperationalStatusKeys.ProjectionType])),
    ];
}
