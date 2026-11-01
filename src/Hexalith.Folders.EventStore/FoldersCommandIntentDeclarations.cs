using System.Collections.Frozen;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Immutable owner metadata for the existing schema-two command admission policy.</summary>
internal static class FoldersCommandIntentDeclarations
{
    // These labels describe complete existing encoder components. They are not semantic JSON
    // keys: dotted/underscore legacy payload names, nulls and ordering stay inside semantic-json.
    private static readonly IReadOnlyList<IdempotencyDescriptorField> Fields = Array.AsReadOnly(
        new IdempotencyDescriptorField[]
        {
            new("target", "string"),
            new("semantic-json", "string"),
            new("semantic-payload-sha256", "string"),
            new("delegated-task-scope", "string"),
            new("credential-scope", "string"),
        });

    /// <summary>Gets the complete policy, independent of runtime adapter registrations.</summary>
    internal static IReadOnlyDictionary<string, CommandIntentDeclaration> All { get; } =
        new CommandIntentDeclaration[]
        {
            Create("Hexalith.Folders.Commands.CreateFolder", "create-folder", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.ArchiveFolder", "archive-folder", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.GrantFolderAccess", "grant-folder-access", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.RevokeFolderAccess", "revoke-folder-access", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.InitializeOrganizationAclBaseline", "initialize-organization-acl-baseline", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.GrantOrganizationAclPrincipal", "grant-organization-acl-principal", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal", "revoke-organization-acl-principal", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.ConfigureProviderBinding", "configure-provider-binding", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.CreateRepositoryBackedFolder", "create-repository-backed-folder", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.BindRepository", "bind-repository", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.ConfigureBranchRefPolicy", "configure-branch-ref-policy", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.PrepareWorkspace", "prepare-workspace", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.LockWorkspace", "lock-workspace", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.ChangeNativeTask", "change-native-task", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.ReleaseWorkspaceLock", "release-workspace-lock", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.MutateFiles", "mutate-files", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.AuthorizeWorkspaceContentErasure", "authorize-content-erasure", IdempotencyReplayRetentionTier.Mutation),
            Create("Hexalith.Folders.Commands.CommitWorkspace", "commit-workspace", IdempotencyReplayRetentionTier.Commit),
        }.ToFrozenDictionary(static declaration => declaration.Route.MessageType!, StringComparer.Ordinal);

    /// <summary>Returns the frozen declaration for an existing command discriminator.</summary>
    internal static CommandIntentDeclaration For(string commandType) => All[commandType];

    private static CommandIntentDeclaration Create(string commandType, string operationId, IdempotencyReplayRetentionTier tier)
        => new(
            new RouteContractDeclaration(
                $"folders-{operationId}", "command", "folders", commandType, null, "v1", 1,
                "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, Array.Empty<string>(),
                // All existing discriminators are dispatched exactly by this real handler. They
                // are not CLR command/payload identities and grant no schema-three eligibility.
                new RouteExecutionDeclaration("Hexalith.Folders.Server.FolderDomainProcessor", "exact",
                    Array.AsReadOnly(new[] { commandType }), "domain-service", null, Array.Empty<string>())),
            FoldersCanonicalIntentBuilder.AdapterId,
            operationId,
            FoldersCanonicalIntentBuilder.PolicyVersion,
            new IdempotencyDescriptorSchema($"folders-{operationId}-intent", FoldersCanonicalIntentBuilder.DescriptorVersion, Fields),
            tier,
            RequiresAuthenticatedSubject: false);
}
