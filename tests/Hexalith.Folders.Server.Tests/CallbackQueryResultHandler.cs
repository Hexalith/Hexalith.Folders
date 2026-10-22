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
