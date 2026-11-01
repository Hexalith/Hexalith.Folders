using Hexalith.EventStore.Contracts.Routing;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.Folders.Server.Tests.CallbackFixtureCatalogDeclarations))]

namespace Hexalith.Folders.Server.Tests;

/// <summary>Declares only the actual isolated callback fixture query.</summary>
public static class CallbackFixtureCatalogDeclarations
{
    /// <summary>Gets the compiled query metadata matched to the registered test handler.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes { get; } = [CallbackQueryResultHandler.CatalogDeclaration];
}
