using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderState(
    bool IsCreated,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? DisplayName,
    string? Description,
    string? PathLabel,
    IReadOnlyList<string> Tags,
    FolderLifecycleState? LifecycleState,
    FolderRepositoryBindingState? RepositoryBindingState,
    FolderWorkspaceLifecycleState? WorkspaceLifecycleState,
    FolderOperatorDisposition? WorkspaceOperatorDisposition,
    string? WorkspaceId,
    string? WorkspacePolicyRef,
    FolderWorkspaceLifecycleEvent? WorkspaceLifecycleEvent,
    string? WorkspaceOperationId,
    string? WorkspaceCorrelationId,
    string? WorkspaceTaskId,
    string? WorkspaceActorPrincipalId,
    DateTimeOffset? WorkspaceLifecycleUpdatedAt,
    string? WorkspaceLockId,
    string? WorkspaceLockIntent,
    int? WorkspaceLockRequestedLeaseSeconds,
    string? WorkspaceLockHolderTaskId,
    DateTimeOffset? WorkspaceLockAcquiredAt,
    DateTimeOffset? WorkspaceLockEffectiveAt,
    DateTimeOffset? WorkspaceLockExpiresAt,
    string? WorkspaceLockRetryEligibilityBasis,
    string? RepositoryBindingId,
    string? ProviderBindingRef,
    string? RepositoryProfileRef,
    string? ExternalRepositoryRefFingerprint,
    string? BranchRefPolicyRef,
    BranchRefPolicyMetadata? BranchRefPolicy,
    string? RepositoryBindingFailureCategory,
    string? RepositoryBindingOutcomeCategory,
    DateTimeOffset? RepositoryBindingUpdatedAt,
    string? RepositoryBindingActorPrincipalId,
    string? RepositoryBindingCorrelationId,
    string? RepositoryBindingTaskId,
    string? RepositoryBindingIdempotencyKey,
    string? RepositoryBindingIdempotencyFingerprint,
    string? WorkspaceCommitReference,
    string? WorkspaceCommitFailureCategory,
    string? WorkspaceCommitOutcomeCategory,
    string? WorkspaceCommitAuthorMetadataReference,
    string? WorkspaceCommitBranchRefTarget,
    string? WorkspaceCommitMessageClassification,
    string? WorkspaceCommitChangedPathMetadataDigest,
    string? WorkspaceCommitReconciliationReference,
    bool WorkspaceCommitReconciliationRequired,
    FolderArchiveReasonCode? ArchiveReasonCode,
    string? ArchiveActorPrincipalId,
    string? ArchiveCorrelationId,
    string? ArchiveTaskId,
    string? ArchiveIdempotencyKey,
    DateTimeOffset? ArchivedAt,
    IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> AccessOverrides,
    long AccessSequence,
    IReadOnlyDictionary<string, string> IdempotencyFingerprints)
{
    /// <summary>Gets the source operation for the most recent retained commit outcome.</summary>
    public string? WorkspaceCommitOperationId { get; init; }

    /// <summary>Gets terminal task results retained independently of later workspace transitions.</summary>
    public IReadOnlyDictionary<string, FolderTaskClosure> TaskClosures { get; init; }
        = FrozenDictionary<string, FolderTaskClosure>.Empty;

    /// <summary>Tests source-folded terminal authority for a task and workspace binding.</summary>
    public bool IsTaskClosed(string workspaceId, string taskId)
        => TaskClosures.ContainsKey(TaskBindingKey(workspaceId, taskId));

    /// <summary>Encodes component boundaries without delimiter collisions.</summary>
    public static string TaskBindingKey(string workspaceId, string taskId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(taskId);
        return $"{workspaceId.Length}:{workspaceId}{taskId.Length}:{taskId}";
    }

    public static FolderState Empty { get; } = new(
        IsCreated: false,
        ManagedTenantId: null,
        OrganizationId: null,
        FolderId: null,
        DisplayName: null,
        Description: null,
        PathLabel: null,
        Tags: [],
        LifecycleState: null,
        RepositoryBindingState: null,
        WorkspaceLifecycleState: null,
        WorkspaceOperatorDisposition: null,
        WorkspaceId: null,
        WorkspacePolicyRef: null,
        WorkspaceLifecycleEvent: null,
        WorkspaceOperationId: null,
        WorkspaceCorrelationId: null,
        WorkspaceTaskId: null,
        WorkspaceActorPrincipalId: null,
        WorkspaceLifecycleUpdatedAt: null,
        WorkspaceLockId: null,
        WorkspaceLockIntent: null,
        WorkspaceLockRequestedLeaseSeconds: null,
        WorkspaceLockHolderTaskId: null,
        WorkspaceLockAcquiredAt: null,
        WorkspaceLockEffectiveAt: null,
        WorkspaceLockExpiresAt: null,
        WorkspaceLockRetryEligibilityBasis: null,
        RepositoryBindingId: null,
        ProviderBindingRef: null,
        RepositoryProfileRef: null,
        ExternalRepositoryRefFingerprint: null,
        BranchRefPolicyRef: null,
        BranchRefPolicy: null,
        RepositoryBindingFailureCategory: null,
        RepositoryBindingOutcomeCategory: null,
        RepositoryBindingUpdatedAt: null,
        RepositoryBindingActorPrincipalId: null,
        RepositoryBindingCorrelationId: null,
        RepositoryBindingTaskId: null,
        RepositoryBindingIdempotencyKey: null,
        RepositoryBindingIdempotencyFingerprint: null,
        WorkspaceCommitReference: null,
        WorkspaceCommitFailureCategory: null,
        WorkspaceCommitOutcomeCategory: null,
        WorkspaceCommitAuthorMetadataReference: null,
        WorkspaceCommitBranchRefTarget: null,
        WorkspaceCommitMessageClassification: null,
        WorkspaceCommitChangedPathMetadataDigest: null,
        WorkspaceCommitReconciliationReference: null,
        WorkspaceCommitReconciliationRequired: false,
        ArchiveReasonCode: null,
        ArchiveActorPrincipalId: null,
        ArchiveCorrelationId: null,
        ArchiveTaskId: null,
        ArchiveIdempotencyKey: null,
        ArchivedAt: null,
        AccessOverrides: FrozenDictionary<FolderAccessEntryKey, FolderAccessOverride>.Empty,
        AccessSequence: 0,
        IdempotencyFingerprints: FrozenDictionary<string, string>.Empty);

    public FolderState Apply(IEnumerable<IFolderEvent> events, FolderStreamName expectedStreamName)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(expectedStreamName);

        FolderState state = this;
        foreach (IFolderEvent folderEvent in events)
        {
            // Fold every retained position. Value equality cannot distinguish a repeated
            // delivery from a second invalid transition in the authoritative stream.
            state = FolderStateApply.Apply(state, folderEvent, expectedStreamName);
        }

        return state;
    }

    public bool HasFolderAccess(FolderAccessEntryKey key)
        => AccessOverrides.TryGetValue(key, out FolderAccessOverride? access)
            && access.IsGranted;
}
