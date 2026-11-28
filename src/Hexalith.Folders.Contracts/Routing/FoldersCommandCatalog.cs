using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.Routing;

/// <summary>Declares the existing Folders command intent protocol without changing canonical encoding or admission behavior.</summary>
public static class FoldersCommandCatalog
{
    /// <summary>Gets the complete source-owned command, nullable payload, option and scope inventory.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = Array.AsReadOnly<CommandIntentDeclaration>(
    [
        Command("ArchiveFolder", "archive-folder",
            "Hexalith.Folders.Server.FolderDomainProcessor+ArchiveFolderPayload",
            [Payload("archive_reason_code"), Payload("folder_id", nullable: false), Payload("request_schema_version")]),
        Command("AuthorizeWorkspaceContentErasure", "authorize-content-erasure",
            "Hexalith.Folders.Server.Content.WorkspaceContentErasureRequest",
            [Payload("workspace_id"), Payload("receipt_id"), Payload("assigned_task_id"), Payload("expected_generation")]),
        Command("BindRepository", "bind-repository",
            "Hexalith.Folders.Server.FolderDomainProcessor+BindRepositoryPayload",
            [Payload("branch_ref_policy.policy_ref"), Payload("external_repository_ref"), Payload("folder_id", nullable: false), Payload("provider_binding_ref")], credential: true),
        Command("ChangeNativeTask", "change-native-task",
            "Hexalith.Folders.Server.Content.WorkspaceNativeTaskCommand",
            [Payload("folder_id", nullable: false), Payload("workspace_id"), Payload("task_id", nullable: false), Payload("expected_head"), Payload("native_request")], task: true),
        Command("CommitWorkspace", "commit-workspace",
            "Hexalith.Folders.Server.FolderDomainProcessor+CommitWorkspacePayload",
            [Payload("author_metadata_reference"), Payload("branch_ref_target"), Payload("changed_path_metadata_digest"), Payload("commit_message_classification"), Payload("operation_id"), Payload("task_id", nullable: false), Payload("workspace_id")], task: true, commit: true),
        Command("ConfigureBranchRefPolicy", "configure-branch-ref-policy",
            "Hexalith.Folders.Server.FolderDomainProcessor+BranchRefPolicyPayload",
            [Payload("branch_ref_policy.allowed_ref_patterns"), Payload("branch_ref_policy.default_ref"), Payload("branch_ref_policy.policy_ref"), Payload("branch_ref_policy.protected_ref_patterns"), Payload("folder_id", nullable: false), Payload("repository_binding_id")]),
        Command("ConfigureProviderBinding", "configure-provider-binding",
            "Hexalith.Folders.Server.FolderDomainProcessor+ConfigureProviderBindingPayload",
            [Payload("capability_profile_ref"), Payload("non_secret_credential_reference"), Payload("provider_binding_ref"), Payload("provider_family_ref")]),
        Command("CreateFolder", "create-folder",
            "Hexalith.Folders.Server.FolderDomainProcessor+CreateFolderPayload",
            [Payload("folder_metadata.display_name"), Payload("parent_folder_id"), Payload("request_schema_version")]),
        Command("CreateRepositoryBackedFolder", "create-repository-backed-folder",
            "Hexalith.Folders.Server.FolderDomainProcessor+CreateRepositoryBackedFolderPayload",
            [Payload("branch_ref_policy.policy_ref"), Payload("branch_ref_policy.repository_binding_id"), Payload("folder_id", nullable: false), Payload("folder_metadata.display_name"), Payload("provider_binding_ref"), Payload("repository_profile_ref")], credential: true),
        Command("GrantFolderAccess", "grant-folder-access",
            "Hexalith.Folders.Server.FolderDomainProcessor+FolderAccessMutationPayload",
            [Payload("acl_entry_id", nullable: false), Payload("effect", nullable: false), Payload("folder_id", nullable: false), Payload("permission_level"), Payload("subject_ref", nullable: false)]),
        Command("LockWorkspace", "lock-workspace",
            "Hexalith.Folders.Server.FolderDomainProcessor+LockWorkspacePayload",
            [Payload("folder_id", nullable: false), Payload("lock_intent"), Payload("requested_lease_seconds"), Payload("task_id", nullable: false), Payload("workspace_id")], task: true),
        Command("MutateFiles", "mutate-files",
            "Hexalith.Folders.Server.FolderDomainProcessor+FileMutationPayload",
            [Payload("content_hash_reference"), Payload("file_operation_kind"), Payload("operation_id"), Payload("path_metadata"), Payload("path_policy_class"), Payload("task_id", nullable: false), Payload("workspace_id")], task: true),
        Command("PrepareWorkspace", "prepare-workspace",
            "Hexalith.Folders.Server.FolderDomainProcessor+PrepareWorkspacePayload",
            [Payload("branch_ref_policy_ref"), Payload("folder_id", nullable: false), Payload("repository_binding_id"), Payload("task_id", nullable: false), Payload("workspace_id"), Payload("workspace_policy_ref")], task: true),
        Command("ReleaseWorkspaceLock", "release-workspace-lock",
            "Hexalith.Folders.Server.FolderDomainProcessor+ReleaseWorkspaceLockPayload",
            [Payload("folder_id", nullable: false), Payload("lock_id"), Payload("lock_ownership_proof"), Payload("task_id", nullable: false), Payload("workspace_id")], task: true),
        Command("RevokeFolderAccess", "revoke-folder-access",
            "Hexalith.Folders.Server.FolderDomainProcessor+FolderAccessMutationPayload",
            [Payload("acl_entry_id", nullable: false), Payload("effect", nullable: false), Payload("folder_id", nullable: false), Payload("permission_level"), Payload("subject_ref", nullable: false)]),
        Command("InitializeOrganizationAclBaseline", "initialize-organization-acl-baseline",
            "Hexalith.Folders.Server.FolderDomainProcessor+OrganizationAclMutationPayload",
            [Payload("acl_entries", nullable: false), Payload("effect", nullable: false), Payload("organization_id", nullable: false), Payload("request_schema_version")]),
        Command("GrantOrganizationAclPrincipal", "grant-organization-acl-principal",
            "Hexalith.Folders.Server.FolderDomainProcessor+OrganizationAclMutationPayload",
            [Payload("acl_entries", nullable: false), Payload("effect", nullable: false), Payload("organization_id", nullable: false), Payload("request_schema_version")]),
        Command("RevokeOrganizationAclPrincipal", "revoke-organization-acl-principal",
            "Hexalith.Folders.Server.FolderDomainProcessor+OrganizationAclMutationPayload",
            [Payload("acl_entries", nullable: false), Payload("effect", nullable: false), Payload("organization_id", nullable: false), Payload("request_schema_version")])
    ]);

    private static IdempotencyDescriptorField Payload(string name, bool nullable = true)
        => new(name, "string", Nullable: nullable, Source: "payload");

    private static CommandIntentDeclaration Command(string command, string operation, string payloadType,
        IReadOnlyList<IdempotencyDescriptorField> fields, bool task = false, bool credential = false, bool commit = false)
    {
        string discriminator = "Hexalith.Folders.Commands." + command;
        return new(new("folders-" + operation, "command", "folders", discriminator, null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(payloadType, "exact", Array.AsReadOnly(new[] { discriminator }), "domain-service", null, Array.Empty<string>())),
            "hexalith-folders", operation, "folders-contract-spine-v2",
            new("folders-" + operation + "-descriptor", 1, Array.AsReadOnly<IdempotencyDescriptorField>(
            [
                new("target", "string", Source: "target"), .. fields,
                new("semantic_payload_sha256", "string", Source: "semantic-option"),
                .. task ? new[] { new IdempotencyDescriptorField("delegated-task-scope", "string", Source: "delegated-task-scope") } : [],
                .. credential ? new[] { new IdempotencyDescriptorField("credential-scope", "string", Source: "credential-scope") } : [],
            ])), commit ? IdempotencyReplayRetentionTier.Commit : IdempotencyReplayRetentionTier.Mutation, false);
    }
}
