using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Contracts.Commands;

namespace Hexalith.Folders.EventStore;

/// <summary>Creates the module command policies for EventStore gateways.</summary>
internal static class FoldersCatalogAdapters
{
    /// <summary>Returns one fresh instance of every existing trusted mutation adapter.</summary>
    internal static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() =>
    [
        new GatewayCommandIntentAdapter<ArchiveFolder>(new ArchiveFolderIdempotencyIntentAdapter()),
        new AuthorizeContentErasureIdempotencyIntentAdapter(),
        new GatewayCommandIntentAdapter<BindRepository>(new BindRepositoryIdempotencyIntentAdapter()),
        new ChangeNativeTaskIdempotencyIntentAdapter(),
        new GatewayCommandIntentAdapter<CommitWorkspace>(new CommitWorkspaceIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<ConfigureBranchRefPolicy>(new ConfigureBranchRefPolicyIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<ConfigureProviderBinding>(new ConfigureProviderBindingIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<CreateFolder>(new CreateFolderIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<CreateRepositoryBackedFolder>(new CreateRepositoryBackedFolderIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<GrantFolderAccess>(new GrantFolderAccessIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<LockWorkspace>(new LockWorkspaceIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<MutateFiles>(new MutateFilesIdempotencyIntentAdapter()),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.GrantOrganizationAclPrincipal", "grant-organization-acl-principal"),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.InitializeOrganizationAclBaseline", "initialize-organization-acl-baseline"),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal", "revoke-organization-acl-principal"),
        new GatewayCommandIntentAdapter<PrepareWorkspace>(new PrepareWorkspaceIdempotencyIntentAdapter()),
        new ProvisionFolderIdempotencyIntentAdapter(),
        new GatewayCommandIntentAdapter<ReleaseWorkspaceLock>(new ReleaseWorkspaceLockIdempotencyIntentAdapter()),
        new GatewayCommandIntentAdapter<RevokeFolderAccess>(new RevokeFolderAccessIdempotencyIntentAdapter())
    ];
}
