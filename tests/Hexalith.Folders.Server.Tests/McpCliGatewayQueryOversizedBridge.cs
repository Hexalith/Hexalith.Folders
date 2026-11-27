using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Hydrates a file reference that exceeds the context search response budget.</summary>
internal sealed class McpCliGatewayQueryOversizedBridge : ISemanticIndexingBridgeReadModel
{
    /// <summary>Gets the synthetic file reference returned by the search and bridge ports.</summary>
    internal string FileVersion { get; } = new('a', 1_100_000);

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
        string managedTenantId, string folderId, string fileVersionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fileVersionId != FileVersion)
        {
            return Task.FromResult<SemanticIndexingBridgeEntry?>(null);
        }

        string workspace = McpCliGatewayQueryFixture.Workspace;
        SemanticIndexingFileVersionIdentity identity = new(
            managedTenantId, managedTenantId, folderId, workspace, "opaque_FileOperationGateway",
            "digest_oversized_fixture", fileVersionId, null,
            $"folders://{managedTenantId}/organizations/{managedTenantId}/folders/{folderId}/workspaces/{workspace}/file-versions/{fileVersionId}");
        return Task.FromResult<SemanticIndexingBridgeEntry?>(new(
            identity, SemanticIndexingBridgeStatus.Indexed, "memories_accepted", false,
            McpCliGatewayQueryFixture.Correlation, McpCliGatewayQueryFixture.TaskId, DateTimeOffset.UtcNow,
            new SemanticIndexingEvidence()));
    }

    /// <inheritdoc/>
    public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
        string managedTenantId, string folderId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
