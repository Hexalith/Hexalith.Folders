using System.Text.Json;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Routing;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Folders.Server.FoldersRuntimeCatalogDeclarations))]

namespace Hexalith.Folders.Server;

/// <summary>Declares the actual bespoke processor, owner queries and retained legacy projection without changing their behavior.</summary>
public static class FoldersRuntimeCatalogDeclarations
{
    /// <summary>Gets the unchanged command intent declarations shared with the gateway adapters.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands => FoldersCommandCatalog.Commands;

    /// <summary>Gets all actual command and computed-query receivers plus the legacy dispatcher.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } =
    [
        .. FoldersCommandCatalog.Commands.Select(command => command.Route),
        Query<FolderOwnerReadQuery>("authorize-folder-read", FolderOwnerReadQuery.QueryType),
        Query<FolderFileOwnerReadQuery>("authorize-folder-file-read", FolderFileOwnerReadQuery.QueryType),
        Query<FolderFileReferenceReadQuery>("resolve-folder-file-reference", FolderFileReferenceReadQuery.QueryType),
        new("folders-legacy-projection", "projection", "folders", null, "folders", "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new(typeof(FolderDomainProjectionHandler).FullName!, "exact", ["folders"], "domain-service", null,
                ["folder-state", "organization-provider-binding"])),
    ];

    /// <summary>Gets each actual receiver method; the named projection retains its separate declaration.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Kind switch { "command" => "process", "query" => "query", _ => "project" }, StringComparer.Ordinal);

    /// <summary>Preserves human command ingress while keeping owner queries and projection protocols internal.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route =>
    {
        Type request = route.Kind == "projection" ? typeof(ProjectionRequest)
            : typeof(FolderDomainProcessor).Assembly.GetType(route.Execution!.HandlerType)
                ?? typeof(FolderOwnerReadQuery).Assembly.GetType(route.Execution.HandlerType, throwOnError: true)!;
        Type response = route.Kind switch
        {
            "command" => typeof(SubmitCommandResponse),
            "projection" => typeof(ProjectionResponse),
            _ => route.MessageType == FolderOwnerReadQuery.QueryType ? typeof(FolderOwnerReadResult)
                : route.MessageType == FolderFileOwnerReadQuery.QueryType ? typeof(FolderFileOwnerReadResult)
                : typeof(FolderFileReferenceReadResult),
        };
        return RouteOperationSchema.Create(route, request, response,
            route.Kind == "command" ? "public" : "internal", "agent-eligible", JsonSerializerOptions.Web);
    }).ToArray();

    private static RouteContractDeclaration Query<T>(string route, string discriminator) => new("folders-" + route,
        "query", "folders", discriminator, null, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery,
        null, [], new(typeof(T).FullName!, "exact", [discriminator], "domain-service", "computed", [], RequiresDelegatedContext: true));
}
