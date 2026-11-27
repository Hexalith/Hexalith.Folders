using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Injects a private exception message at each authorized domain observation port.</summary>
internal sealed class McpCliGatewayQueryThrowingReadModels(string privateMessage) : IFolderSearchSource,
    ISemanticIndexingBridgeReadModel, IFolderAclReadModel, IBranchRefPolicyReadModel, IWorkspaceCleanupStatusReadModel,
    IWorkspaceLockStatusReadModel, IWorkspaceStatusReadModel, IProviderReadinessBindingReader
{
    /// <summary>Gets the number of observations that reached a throwing port.</summary>
    internal int Calls { get; private set; }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public Task<FolderSearchSourceResult> SearchAsync(FolderSearchSourceRequest request,
        CancellationToken cancellationToken = default)
        => Fail<FolderSearchSourceResult>();

    /// <inheritdoc/>
    public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default)
        => Fail<SemanticIndexingBridgeEntry?>();

    /// <inheritdoc/>
    public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(string managedTenantId, string folderId, string fileVersionId,
        CancellationToken cancellationToken = default)
        => Fail<SemanticIndexingBridgeEntry?>();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(string managedTenantId, string folderId,
        CancellationToken cancellationToken = default)
        => Fail<IReadOnlyList<SemanticIndexingBridgeEntry>>();

    /// <inheritdoc/>
    public Task<FolderPermissionProjection?> GetCurrentAsync(string tenantId, string folderId,
        CancellationToken cancellationToken = default)
        => Fail<FolderPermissionProjection?>();

    /// <inheritdoc/>
    public Task<BranchRefPolicyReadModelResult> GetAsync(BranchRefPolicyReadModelRequest request,
        CancellationToken cancellationToken = default)
        => Fail<BranchRefPolicyReadModelResult>();

    /// <inheritdoc/>
    public Task<WorkspaceCleanupStatusReadModelResult> GetAsync(WorkspaceCleanupStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
        => Fail<WorkspaceCleanupStatusReadModelResult>();

    /// <inheritdoc/>
    public Task<WorkspaceLockStatusReadModelResult> GetAsync(WorkspaceLockStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
        => Fail<WorkspaceLockStatusReadModelResult>();

    /// <inheritdoc/>
    public Task<WorkspaceStatusReadModelResult> GetAsync(WorkspaceStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
        => Fail<WorkspaceStatusReadModelResult>();

    /// <inheritdoc/>
    public Task<OrganizationProviderBinding?> GetAsync(ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
        => Fail<OrganizationProviderBinding?>();

    private Task<T> Fail<T>()
    {
        Calls++;
        return Task.FromException<T>(new InvalidOperationException(privateMessage));
    }
}
