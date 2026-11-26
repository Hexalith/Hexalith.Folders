using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Creates the unchanged module command policies for EventStore gateways.</summary>
internal static class FoldersCatalogAdapters
{
    /// <summary>Returns one fresh instance of every existing trusted mutation adapter.</summary>
    internal static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() =>
    [
        new ArchiveFolderIdempotencyIntentAdapter(),
        new AuthorizeContentErasureIdempotencyIntentAdapter(),
        new BindRepositoryIdempotencyIntentAdapter(),
        new ChangeNativeTaskIdempotencyIntentAdapter(),
        new CommitWorkspaceIdempotencyIntentAdapter(),
        new ConfigureBranchRefPolicyIdempotencyIntentAdapter(),
        new ConfigureProviderBindingIdempotencyIntentAdapter(),
        new CreateFolderIdempotencyIntentAdapter(),
        new CreateRepositoryBackedFolderIdempotencyIntentAdapter(),
        new GrantFolderAccessIdempotencyIntentAdapter(),
        new LockWorkspaceIdempotencyIntentAdapter(),
        new MutateFilesIdempotencyIntentAdapter(),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.GrantOrganizationAclPrincipal", "grant-organization-acl-principal"),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.InitializeOrganizationAclBaseline", "initialize-organization-acl-baseline"),
        new OrganizationAclIdempotencyIntentAdapter("Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal", "revoke-organization-acl-principal"),
        new PrepareWorkspaceIdempotencyIntentAdapter(),
        new ProvisionFolderIdempotencyIntentAdapter(),
        new ReleaseWorkspaceLockIdempotencyIntentAdapter(),
        new RevokeFolderAccessIdempotencyIntentAdapter()
    ];
}
