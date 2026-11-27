using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Queries.Folders;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Shares admission for one typed Folders query and leaves its observation to a dedicated handler.</summary>
/// <typeparam name="T">The decorated v2 request contract.</typeparam>
/// <param name="support">The Gateway read pipeline.</param>
/// <param name="consistency">The operation's fixed read consistency.</param>
/// <param name="action">The exact permission required of the signed caller.</param>
/// <param name="aggregate">The public resource identifier from the request.</param>
/// <param name="historicalAction">The action on the backing in-process handler, if different.</param>
/// <param name="providerScope">Whether the aggregate is a reserved provider binding actor.</param>
internal abstract class GatewayQueryHandler<T>(GatewayQueryExecutionSupport support, string consistency,
    string action, Func<T, string> aggregate, string? historicalAction = null, bool providerScope = false) : IDomainQueryHandler where T : class
{
    private static readonly HexalithQueryAttribute _contract = typeof(T).GetCustomAttribute<HexalithQueryAttribute>()
        ?? throw new InvalidOperationException("Gateway query handlers require a decorated contract.");

    /// <inheritdoc/>
    public string Domain => _contract.Domain!;

    /// <inheritdoc/>
    public string QueryType => _contract.WireType!;

    /// <inheritdoc/>
    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!support.HasVerifiedCaller(query, action))
        {
            return GatewayQueryExecutionSupport.Denied;
        }

        if (support.HasIdempotencyKey())
        {
            return GatewayQueryExecutionSupport.InvalidEnvelope;
        }

        T? payload;
        try
        {
            payload = GatewayQueryExecutionSupport.Deserialize<T>(query.Payload);
        }
        catch (JsonException)
        {
            return GatewayQueryExecutionSupport.InvalidEnvelope;
        }

        if (payload is null || !IsValidCorrelation(query.CorrelationId))
        {
            return GatewayQueryExecutionSupport.InvalidEnvelope;
        }

        QueryResult? invalid = ValidatePaging(query.Paging, payload, out GatewayQueryPage page);
        if (invalid is not null)
        {
            return invalid;
        }

        return await support.ReadAsync(query, aggregate(payload), action, historicalAction ?? action, providerScope,
            TaskScope(payload), WorkspaceScope(payload), SecondaryEntity(payload), RequiresCurrentTask, RequiresCurrentWorkspace,
            () => ReadAsync(query, payload, page, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns an optional task that must be verified before source access.</summary>
    protected virtual string? TaskScope(T payload) => null;

    /// <summary>Returns the workspace addressed by the typed payload, when present.</summary>
    protected virtual string? WorkspaceScope(T payload) => null;

    /// <summary>Returns the exact secondary identifier addressed by the typed payload, when present.</summary>
    protected virtual string? SecondaryEntity(T payload) => null;

    /// <summary>Whether a supplied task must match the current caller-owned workspace task.</summary>
    protected virtual bool RequiresCurrentTask => true;

    /// <summary>Whether workspace scope must be proven before another service can be queried.</summary>
    protected virtual bool RequiresCurrentWorkspace => false;

    /// <summary>Rejects envelope paging for a non-paged query, or merges paging in a paged handler.</summary>
    protected virtual QueryResult? ValidatePaging(QueryPagingOptions? envelope, T payload, out GatewayQueryPage page)
    {
        page = default;
        return envelope is null ? null : GatewayQueryExecutionSupport.InvalidEnvelope;
    }

    /// <summary>Executes the domain observation once caller authority and inputs have been verified.</summary>
    protected abstract Task<QueryResult> ReadAsync(QueryEnvelope query, T payload, GatewayQueryPage page, CancellationToken token);

    /// <summary>Projects a successful domain response to its Gateway wire identity and fixed freshness.</summary>
    protected QueryResult Success<TResponse>(TResponse value, FolderLifecycleFreshness freshness, QueryPagingMetadata? paging = null)
        => GatewayQueryExecutionSupport.Success(value, _contract.ProjectionType!, consistency, freshness, paging);

    private static bool IsValidCorrelation(string value)
        => value.Length is > 0 and <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
}
