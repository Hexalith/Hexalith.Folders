using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies controlled dispatcher results through the production callback route.</summary>
/// <param name="result">The result returned by the registered test query.</param>
internal sealed class CallbackQueryResultHandler(QueryResult result) : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "folders";

    /// <inheritdoc/>
    public string QueryType => "callback-boundary-test";

    /// <summary>Gets the isolated handler's exact owner declaration, without any command admission policy.</summary>
    internal static RouteContractDeclaration CatalogDeclaration { get; } = new("folders-callback-fixture", "query", "folders", "callback-boundary-test", null,
        "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(CallbackQueryResultHandler).FullName!, "exact", ["callback-boundary-test"], "domain-service", "computed", []));

    /// <summary>Gets the number of dispatched queries.</summary>
    internal int Calls { get; private set; }

    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(result);
    }
}
