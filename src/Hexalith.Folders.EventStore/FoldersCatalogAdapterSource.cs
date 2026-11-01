using Hexalith.EventStore.DomainService;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.Folders.EventStore.FoldersCatalogAdapterSource))]

namespace Hexalith.Folders.EventStore;

/// <summary>Returns the same installed Folders adapters for independently observed catalog composition.</summary>
public static class FoldersCatalogAdapterSource
{
    /// <summary>Creates the complete existing mutation policy inventory.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() =>
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
        new ReleaseWorkspaceLockIdempotencyIntentAdapter(),
        new RevokeFolderAccessIdempotencyIntentAdapter()
    ];
}
