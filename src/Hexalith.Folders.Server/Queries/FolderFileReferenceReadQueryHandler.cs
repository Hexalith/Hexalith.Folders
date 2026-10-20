using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.Server.Queries;

/// <summary>Exposes the same identity-only owner boundary for lookup of an explicit Projects reference mapping.</summary>
/// <param name="owner">The shared exact-resource identity read.</param>
public sealed class FolderFileReferenceReadQueryHandler(FolderFileOwnerReadQueryHandler owner) : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "folders";
    /// <inheritdoc/>
    public string QueryType => Hexalith.Folders.Contracts.FolderFileReferenceReadQuery.QueryType;
    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken = default)
        => owner.ExecuteReadAsync(query, true, cancellationToken);
}
