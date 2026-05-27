using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

internal static class FolderLifecycleReplayFixture
{
    public const string ManagedTenantId = "tenant-a";
    public const string OrganizationId = "organization-a";
    public const string FolderId = "folder-a";
    public const string WorkspaceId = "workspace-a";
    public const string RepositoryBindingId = "repository-binding-a";
    public const string ProviderBindingRef = "provider-binding-a";
    public const string BranchRefPolicyRef = "branch-ref-policy-a";
    public const string ActorPrincipalId = "principal-a";
    public const string CorrelationId = "correlation-a";
    public const string TaskId = "task-a";

    public static readonly DateTimeOffset OccurredAt = new(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);
    public static readonly FolderStreamName StreamName = FolderStreamName.Create(ManagedTenantId, FolderId);

    public static IReadOnlyList<IFolderEvent> SuccessfulLifecycle()
    {
        List<IFolderEvent> events = [.. CreatedEvents()];
        FolderState state = FolderState.Empty.Apply(events, StreamName);

        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.GrantAccess(idempotencyKey: "idempotency-access-grant-a"),
            OccurredAt.AddMinutes(1)));
        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.RevokeAccess(idempotencyKey: "idempotency-access-revoke-a"),
            OccurredAt.AddMinutes(2)));
        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.CreateRepositoryBackedFolder(idempotencyKey: "idempotency-binding-request-a"),
            OccurredAt.AddMinutes(3)));

        AppendDirect(events, ref state, RepositoryBound("idempotency-repository-bound-a", OccurredAt.AddMinutes(4)));
        AppendDirect(events, ref state, BranchRefPolicyConfigured("idempotency-branch-policy-a", OccurredAt.AddMinutes(5)));
        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.PrepareWorkspace(idempotencyKey: "idempotency-workspace-request-a"),
            OccurredAt.AddMinutes(6)));
        AppendDirect(events, ref state, LifecycleEvent(
            FolderWorkspaceLifecycleEvent.WorkspacePrepared,
            "idempotency-workspace-prepared-a",
            OccurredAt.AddMinutes(7)));
        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.LockWorkspace(idempotencyKey: "idempotency-lock-a"),
            OccurredAt.AddMinutes(8)));
        AppendDirect(events, ref state, FileMutationAccepted("idempotency-file-mutation-a", OccurredAt.AddMinutes(9)));
        AppendDirect(events, ref state, CommitSucceeded("idempotency-commit-success-a", OccurredAt.AddMinutes(10)));
        AppendDirect(events, ref state, LockReleased("idempotency-lock-release-a", OccurredAt.AddMinutes(11)));

        return events;
    }

    public static IReadOnlyList<IFolderEvent> ExistingRepositoryBindingLifecycle()
        =>
        [
            .. CreatedEvents(),
            ExistingRepositoryBindingRequested("idempotency-existing-binding-request-a", OccurredAt.AddMinutes(1)),
            RepositoryBound("idempotency-existing-repository-bound-a", OccurredAt.AddMinutes(2)),
        ];

    public static IReadOnlyList<IFolderEvent> RepositoryFailureLifecycle()
        =>
        [
            .. CreatedEvents(),
            RepositoryBindingRequested("idempotency-binding-request-a", OccurredAt.AddMinutes(1)),
            RepositoryBindingFailed("idempotency-binding-failed-a", OccurredAt.AddMinutes(2)),
        ];

    public static IReadOnlyList<IFolderEvent> RepositoryUnknownOutcomeLifecycle(bool reconciliationRequired)
        =>
        [
            .. CreatedEvents(),
            RepositoryBindingRequested("idempotency-binding-request-a", OccurredAt.AddMinutes(1)),
            ProviderOutcomeUnknown(
                reconciliationRequired,
                reconciliationRequired ? "idempotency-reconciliation-required-a" : "idempotency-provider-unknown-a",
                OccurredAt.AddMinutes(2)),
        ];

    public static IReadOnlyList<IFolderEvent> WorkspaceFailureLifecycle()
        =>
        [
            .. WorkspacePreparationRequestedPrefix(),
            LifecycleEvent(
                FolderWorkspaceLifecycleEvent.WorkspacePreparationFailed,
                "idempotency-workspace-preparation-failed-a",
                OccurredAt.AddMinutes(8)),
        ];

    public static IReadOnlyList<IFolderEvent> DirtyLockExpiredLifecycle()
        =>
        [
            .. ReadyWorkspacePrefix(),
            WorkspaceLockAcquired("idempotency-lock-a", OccurredAt.AddMinutes(8)),
            LifecycleEvent(
                FolderWorkspaceLifecycleEvent.LockLeaseExpired,
                "idempotency-lock-expired-a",
                OccurredAt.AddMinutes(9)),
        ];

    public static IReadOnlyList<IFolderEvent> CommitFailureLifecycle()
        =>
        [
            .. ChangesStagedPrefix(),
            CommitFailed("idempotency-commit-failed-a", OccurredAt.AddMinutes(10)),
        ];

    public static IReadOnlyList<IFolderEvent> CommittedLifecycle()
        =>
        [
            .. ChangesStagedPrefix(),
            CommitSucceeded("idempotency-commit-success-a", OccurredAt.AddMinutes(10)),
        ];

    public static IReadOnlyList<IFolderEvent> CommitUnknownOutcomeLifecycle(bool reconciliationRequired)
        =>
        [
            .. ChangesStagedPrefix(),
            CommitOutcomeUnknown(
                reconciliationRequired,
                reconciliationRequired ? "idempotency-commit-reconciliation-required-a" : "idempotency-commit-unknown-a",
                OccurredAt.AddMinutes(10)),
        ];

    public static IReadOnlyList<IFolderEvent> ArchivedLifecycle()
    {
        List<IFolderEvent> events = [.. CreatedEvents()];
        FolderState state = FolderState.Empty.Apply(events, StreamName);
        AppendAccepted(events, ref state, FolderAggregate.Handle(
            state,
            FolderCommandFactory.Archive(idempotencyKey: "idempotency-archive-a"),
            OccurredAt.AddMinutes(1)));
        return events;
    }

    public static IReadOnlyList<IFolderEvent> DuplicateAccessDeliveryLifecycle()
    {
        IReadOnlyList<IFolderEvent> created = CreatedEvents();
        FolderAccessGranted grant = new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            FolderAccessPrincipalKind.User,
            "target-principal-a",
            "read_metadata",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            "idempotency-duplicate-grant-a",
            "fingerprint-duplicate-grant-a",
            1,
            OccurredAt.AddMinutes(1));

        return [.. created, grant, grant];
    }

    public static IReadOnlyList<IFolderEvent> FolderListProjectionEvents()
        =>
        [
            .. CreatedEvents(),
            RepositoryBindingRequested("idempotency-binding-request-a", OccurredAt.AddMinutes(1)),
            RepositoryBound("idempotency-repository-bound-a", OccurredAt.AddMinutes(2)),
        ];

    public static IReadOnlyList<FolderProjectionEnvelope> Envelopes(IReadOnlyList<IFolderEvent> events)
        => events
            .Select(static (folderEvent, index) => new FolderProjectionEnvelope(folderEvent.ManagedTenantId, index + 1, folderEvent))
            .ToArray();

    public static IReadOnlyList<IFolderEvent> CreatedEvents()
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(idempotencyKey: "idempotency-create-a"),
            OccurredAt);
        if (result.Code != FolderResultCode.Created)
        {
            throw new InvalidOperationException($"Fixture create failed with result code {result.Code}.");
        }

        return result.Events;
    }

    public static IReadOnlyList<IFolderEvent> LockedLifecycleForTenant(
        string managedTenantId,
        string folderId = FolderId,
        string workspaceId = WorkspaceId,
        string taskId = TaskId)
        =>
        [
            new FolderCreated(
                managedTenantId,
                OrganizationId,
                folderId,
                "Folder A",
                "safe description",
                folderId,
                ["alpha", "beta"],
                FolderLifecycleState.Active,
                FolderRepositoryBindingState.Unbound,
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-create-a",
                "fingerprint-idempotency-create-a",
                OccurredAt),
            new RepositoryBindingRequested(
                managedTenantId,
                OrganizationId,
                folderId,
                RepositoryBindingId,
                ProviderBindingRef,
                "repository-profile-a",
                BranchRefPolicyRef,
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-binding-request-a",
                "fingerprint-idempotency-binding-request-a",
                OccurredAt.AddMinutes(1)),
            new RepositoryBound(
                managedTenantId,
                OrganizationId,
                folderId,
                RepositoryBindingId,
                ProviderBindingRef,
                CorrelationId,
                taskId,
                "idempotency-repository-bound-a",
                "fingerprint-idempotency-repository-bound-a",
                OccurredAt.AddMinutes(2)),
            new BranchRefPolicyConfigured(
                managedTenantId,
                OrganizationId,
                folderId,
                RepositoryBindingId,
                BranchRefPolicyRef,
                "branch_ref_primary",
                ["branch_ref_feature"],
                ["branch_ref_release"],
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-policy-a",
                "fingerprint-idempotency-policy-a",
                OccurredAt.AddMinutes(3)),
            new WorkspacePreparationRequested(
                managedTenantId,
                OrganizationId,
                folderId,
                workspaceId,
                RepositoryBindingId,
                BranchRefPolicyRef,
                "workspace-policy-a",
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-workspace-request-a",
                "fingerprint-idempotency-workspace-request-a",
                OccurredAt.AddMinutes(4)),
            new FolderWorkspaceLifecycleEventRecorded(
                managedTenantId,
                OrganizationId,
                folderId,
                workspaceId,
                FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                DirtyResolution: null,
                OperationId: workspaceId,
                CorrelationId,
                taskId,
                "idempotency-workspace-prepared-a",
                "fingerprint-idempotency-workspace-prepared-a",
                OccurredAt.AddMinutes(5)),
            new WorkspaceLockAcquired(
                managedTenantId,
                OrganizationId,
                folderId,
                workspaceId,
                FolderWorkspaceLifecycleEvent.WorkspaceLocked,
                "lock-id-a",
                "exclusive_write",
                3600,
                taskId,
                OccurredAt.AddMinutes(6),
                OccurredAt.AddMinutes(6),
                OccurredAt.AddMinutes(66),
                "lease_until_expiry",
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-lock-a",
                "fingerprint-idempotency-lock-a",
                OccurredAt.AddMinutes(6)),
        ];

    public static IReadOnlyList<IFolderEvent> ReadyLifecycleForTenant(
        string managedTenantId,
        string folderId = FolderId,
        string workspaceId = WorkspaceId,
        string taskId = TaskId)
        => LockedLifecycleForTenant(managedTenantId, folderId, workspaceId, taskId)
            .SkipLast(1)
            .ToArray();

    public static IReadOnlyList<IFolderEvent> ChangesStagedLifecycleForTenant(
        string managedTenantId,
        string folderId = FolderId,
        string workspaceId = WorkspaceId,
        string taskId = TaskId)
        =>
        [
            .. LockedLifecycleForTenant(managedTenantId, folderId, workspaceId, taskId),
            new WorkspaceFileMutationAccepted(
                managedTenantId,
                OrganizationId,
                folderId,
                workspaceId,
                FolderWorkspaceLifecycleEvent.FileMutated,
                "operation-a",
                "add",
                "PutFileInline",
                "tenant_sensitive_document",
                "pathmeta_boundary_a",
                "hashref-a",
                12,
                "text/plain",
                "inline_decoded",
                12,
                ActorPrincipalId,
                CorrelationId,
                taskId,
                "idempotency-file-a",
                "fingerprint-idempotency-file-a",
                OccurredAt.AddMinutes(7)),
        ];

    public static FolderWorkspaceLifecycleEventRecorded LifecycleEvent(
        FolderWorkspaceLifecycleEvent workspaceLifecycleEvent,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        FolderWorkspaceDirtyResolution? dirtyResolution = null)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            workspaceLifecycleEvent,
            dirtyResolution,
            $"operation-{idempotencyKey}",
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static IReadOnlyList<IFolderEvent> ReadyWorkspacePrefix()
        =>
        [
            .. WorkspacePreparationRequestedPrefix(),
            LifecycleEvent(
                FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                "idempotency-workspace-prepared-a",
                OccurredAt.AddMinutes(5)),
        ];

    private static IReadOnlyList<IFolderEvent> WorkspacePreparationRequestedPrefix()
        =>
        [
            .. CreatedEvents(),
            RepositoryBindingRequested("idempotency-binding-request-a", OccurredAt.AddMinutes(1)),
            RepositoryBound("idempotency-repository-bound-a", OccurredAt.AddMinutes(2)),
            BranchRefPolicyConfigured("idempotency-branch-policy-a", OccurredAt.AddMinutes(3)),
            WorkspacePreparationRequested("idempotency-workspace-request-a", OccurredAt.AddMinutes(4)),
        ];

    private static IReadOnlyList<IFolderEvent> ChangesStagedPrefix()
        =>
        [
            .. ReadyWorkspacePrefix(),
            WorkspaceLockAcquired("idempotency-lock-a", OccurredAt.AddMinutes(8)),
            FileMutationAccepted("idempotency-file-mutation-a", OccurredAt.AddMinutes(9)),
        ];

    private static void AppendAccepted(List<IFolderEvent> events, ref FolderState state, FolderResult result)
    {
        if (result.Code is not (FolderResultCode.Accepted or FolderResultCode.Created))
        {
            throw new InvalidOperationException($"Fixture command failed with result code {result.Code}.");
        }

        events.AddRange(result.Events);
        state = state.Apply(result.Events, StreamName);
    }

    private static void AppendDirect(List<IFolderEvent> events, ref FolderState state, IFolderEvent folderEvent)
    {
        events.Add(folderEvent);
        state = state.Apply([folderEvent], StreamName);
    }

    private static RepositoryBindingRequested RepositoryBindingRequested(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            ProviderBindingRef,
            "repository-profile-a",
            BranchRefPolicyRef,
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static ExistingRepositoryBindingRequested ExistingRepositoryBindingRequested(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            ProviderBindingRef,
            "external-repository-fingerprint-a",
            BranchRefPolicyRef,
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static RepositoryBound RepositoryBound(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            ProviderBindingRef,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static BranchRefPolicyConfigured BranchRefPolicyConfigured(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            BranchRefPolicyRef,
            "ref-default-safe",
            ["ref-allowed-safe"],
            ["ref-protected-safe"],
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static RepositoryBindingFailed RepositoryBindingFailed(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            ProviderBindingRef,
            "provider_unavailable",
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static ProviderOutcomeUnknown ProviderOutcomeUnknown(
        bool reconciliationRequired,
        string idempotencyKey,
        DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            RepositoryBindingId,
            ProviderBindingRef,
            reconciliationRequired,
            reconciliationRequired ? "reconciliation_required" : "unknown_provider_outcome",
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspacePreparationRequested WorkspacePreparationRequested(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            RepositoryBindingId,
            BranchRefPolicyRef,
            "workspace-policy-a",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceLockAcquired WorkspaceLockAcquired(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.WorkspaceLocked,
            "lock-id-a",
            "exclusive_write",
            3600,
            TaskId,
            occurredAt,
            occurredAt,
            occurredAt.AddHours(1),
            "lease_until_expiry",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceLockReleased LockReleased(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.WorkspaceLockReleased,
            "lock-id-a",
            TaskId,
            "caller_completed",
            "active",
            OccurredAt.AddMinutes(8),
            OccurredAt.AddMinutes(8),
            OccurredAt.AddMinutes(68),
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceFileMutationAccepted FileMutationAccepted(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.FileMutated,
            "operation-file-mutation-a",
            "upsert_file",
            "inline_metadata_only",
            "metadata_only_path_policy",
            "path_digest_a",
            "content_hash_ref_a",
            64,
            "application/octet-stream",
            "inline_metadata_only",
            64,
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceCommitSucceeded CommitSucceeded(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.CommitSucceeded,
            "operation-commit-a",
            "commit-reference-a",
            "success",
            "author-metadata-ref-a",
            "ref-target-safe",
            "metadata_only_commit_message",
            "changed_path_digest_a",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceCommitFailed CommitFailed(string idempotencyKey, DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.CommitFailed,
            "operation-commit-a",
            "provider_unavailable",
            "known_failure",
            "author-metadata-ref-a",
            "ref-target-safe",
            "metadata_only_commit_message",
            "changed_path_digest_a",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);

    private static WorkspaceCommitOutcomeUnknown CommitOutcomeUnknown(
        bool reconciliationRequired,
        string idempotencyKey,
        DateTimeOffset occurredAt)
        => new(
            ManagedTenantId,
            OrganizationId,
            FolderId,
            WorkspaceId,
            FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown,
            "operation-commit-a",
            reconciliationRequired ? "reconciliation_required" : "unknown_provider_outcome",
            "commit-reconciliation-ref-a",
            reconciliationRequired,
            "author-metadata-ref-a",
            "ref-target-safe",
            "metadata_only_commit_message",
            "changed_path_digest_a",
            ActorPrincipalId,
            CorrelationId,
            TaskId,
            idempotencyKey,
            $"fingerprint-{idempotencyKey}",
            occurredAt);
}
