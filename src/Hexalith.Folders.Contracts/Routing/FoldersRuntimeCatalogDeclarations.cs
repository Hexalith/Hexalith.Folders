using System.Reflection;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Contracts.Queries;
using Hexalith.Folders.Routing;
using Hexalith.Folders.Server.Provisioning;
using Hexalith.McpCli.Abstractions;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Folders.Server.FoldersRuntimeCatalogDeclarations))]

namespace Hexalith.Folders.Server;

/// <summary>Declares command, query and projection receivers owned by Folders.</summary>
public static class FoldersRuntimeCatalogDeclarations
{
    /// <summary>Gets the unchanged command intent declarations shared with the gateway adapters.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands => FoldersCommandCatalog.Commands;

    /// <summary>Gets the command, public and owner query, and legacy projection receivers.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } =
    [
        .. FoldersCommandCatalog.Commands.Select(command => command.Route),
        Query<FolderOwnerReadQuery>("authorize-folder-read", FolderOwnerReadQuery.QueryType),
        Query<FolderFileOwnerReadQuery>("authorize-folder-file-read", FolderFileOwnerReadQuery.QueryType),
        Query<FolderFileReferenceReadQuery>("resolve-folder-file-reference", FolderFileReferenceReadQuery.QueryType),
        PublicQuery<GetBranchRefPolicy>(),
        PublicQuery<GetCommitEvidence>(),
        PublicQuery<GetEffectivePermissions>(),
        PublicQuery<GetFolderIndexingStatus>(),
        PublicQuery<GetFolderLifecycleStatus>(),
        PublicQuery<GetProviderBinding>(),
        PublicQuery<GetProviderOutcome>(),
        PublicQuery<GetReconciliationStatus>(),
        PublicQuery<GetRepositoryBinding>(),
        PublicQuery<GetTaskStatus>(),
        PublicQuery<GetWorkspaceCleanupStatus>(),
        PublicQuery<GetWorkspaceLock>(),
        PublicQuery<GetWorkspaceRetryEligibility>(),
        PublicQuery<GetWorkspaceStatus>(),
        PublicQuery<ListFolderAclEntries>(),
        PublicQuery<SearchFolderIndexedFiles>(),
        PublicQuery<ValidateProviderReadiness>(),
        new("folders-legacy-projection", "projection", "folders", null, "folders", "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new("Hexalith.Folders.Server.FolderDomainProjectionHandler", "exact", ["folders"], "domain-service", null,
                ["folder-state", "organization-provider-binding"])),
    ];

    /// <summary>Gets each actual receiver method; the named projection retains its separate declaration.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods => Routes.ToDictionary(route => route.RouteId,
        route => route.Kind switch { "command" => "process", "query" => "query", _ => "project" }, StringComparer.Ordinal);

    /// <summary>Exports public commands and Gateway metadata queries with their wire contracts.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations => Routes.Select(route =>
    {
        Type request = route.Kind == "projection" ? typeof(ProjectionRequest)
            : ResolveRequestType(route.Execution!.HandlerType);
        Type response = route.Kind switch
        {
            "command" => typeof(SubmitCommandResponse),
            "projection" => typeof(ProjectionResponse),
            "query" when route.Execution?.QueryKind == "projection" => typeof(QueryResult),
            _ => route.MessageType == FolderOwnerReadQuery.QueryType ? typeof(FolderOwnerReadResult)
                : route.MessageType == FolderFileOwnerReadQuery.QueryType ? typeof(FolderFileOwnerReadResult)
                : typeof(FolderFileReferenceReadResult),
        };
        return RouteOperationSchema.Create(route, request, response,
            route.Kind == "command" && route.MessageType != FolderProvisioningRequest.CommandType
                || route.Kind == "query" && route.Execution?.QueryKind == "projection" ? "public" : "internal", "agent-eligible", JsonSerializerOptions.Web);
    }).ToArray();

    private static RouteContractDeclaration PublicQuery<T>()
    {
        HexalithQueryAttribute contract = typeof(T).GetCustomAttribute<HexalithQueryAttribute>()
            ?? throw new InvalidOperationException("Public Gateway queries require a decorated contract.");
        // Schema v3 reserves RequiresDelegatedContext for internal owner-to-owner queries.
        // The public Folders handler still requires the SDK's verified authenticated-caller feature.
        return new("folders-" + contract.ProjectionType, "query", "folders", contract.WireType, contract.ProjectionType,
            "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
            new(typeof(T).FullName!, "exact", [contract.WireType!], "domain-service", "projection", []));
    }

    private static Type ResolveRequestType(string handlerType)
    {
        const string processorPayload = "Hexalith.Folders.Server.FolderDomainProcessor+";
        return handlerType.StartsWith(processorPayload, StringComparison.Ordinal)
            ? typeof(FoldersCommandPayloads).GetNestedType(handlerType[processorPayload.Length..],
                System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Unknown Folders command payload '{handlerType}'.")
            : typeof(FoldersRuntimeCatalogDeclarations).Assembly.GetType(handlerType, throwOnError: true)!;
    }

    private static RouteContractDeclaration Query<T>(string route, string discriminator) => new("folders-" + route,
        "query", "folders", discriminator, null, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery,
        null, [], new(typeof(T).FullName!, "exact", [discriminator], "domain-service", "computed", [], RequiresDelegatedContext: true));
}
