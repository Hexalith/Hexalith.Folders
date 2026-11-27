using Hexalith.Folders.Queries.ContextSearch;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Returns a scoped and a foreign search hit so the production facade must trim and hydrate them.</summary>
internal sealed class McpCliGatewayQuerySearchSource : IFolderSearchSource
{
    /// <summary>Gets the number of times the production handler invoked the index.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets or sets the file version retained by the real bridge.</summary>
    internal string FileVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the next file version retained by the real bridge.</summary>
    internal string NextFileVersion { get; set; } = string.Empty;

    /// <summary>Whether to return a foreign result after the first visible match.</summary>
    internal bool IncludeForeignTail { get; set; } = true;

    /// <summary>Whether a second indexed file is visible to the caller.</summary>
    internal bool IncludeVisibleTail { get; set; }

    /// <summary>Gets or sets the failure, if any, returned at the continuation offset.</summary>
    internal FolderSearchSourceStatus ContinuationStatus { get; set; } = FolderSearchSourceStatus.Available;

    /// <inheritdoc/>
    public Task<FolderSearchSourceResult> SearchAsync(FolderSearchSourceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (request.Offset > 0 && ContinuationStatus != FolderSearchSourceStatus.Available)
        {
            return Task.FromResult(new FolderSearchSourceResult(ContinuationStatus, [], 0, 0));
        }

        List<FolderSearchSourceHit> hits =
        [
            new(request.ManagedTenantId, request.OrganizationId, request.FolderId, request.WorkspaceId,
                FileVersion, 1),
        ];
        if (IncludeVisibleTail)
        {
            hits.Add(new(request.ManagedTenantId, request.OrganizationId, request.FolderId, request.WorkspaceId, NextFileVersion, 1));
        }
        if (IncludeForeignTail)
        {
            hits.Add(new("foreign-tenant", request.OrganizationId, request.FolderId, request.WorkspaceId, "foreign-private-file", 1));
        }
        return Task.FromResult(new FolderSearchSourceResult(FolderSearchSourceStatus.Available,
            hits.Skip(request.Offset).Take(request.Limit).ToArray(), hits.Count, Math.Clamp(hits.Count - request.Offset, 0, request.Limit)));
    }
}
