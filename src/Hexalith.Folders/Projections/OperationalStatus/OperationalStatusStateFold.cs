using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Folds operational metadata independently of primary content and File history.</summary>
internal static class OperationalStatusStateFold
{
    /// <summary>Preserves source transitions without retaining publication versions or file incarnations.</summary>
    internal static FolderState Apply(FolderState state, IFolderEvent value, FolderStreamName stream)
    {
        FolderState next;
        if (value is WorkspaceFileMutationAccepted file)
        {
            if (!state.IsCreated || FolderStreamName.Create(value.ManagedTenantId, value.FolderId) != stream
                || string.IsNullOrWhiteSpace(file.PathMetadataDigest)
                || (file.OwnerFileId is not null || file.ProjectFileReferenceId is not null)
                    && !FolderFileIdentityState.IsCanonical(file.OwnerFileId)
                || file.ProjectFileReferenceId is not null && !FolderFileIdentityState.IsProjectReference(file.ProjectFileReferenceId))
                throw new InvalidOperationException("Invalid operational file evidence.");
            ValidateContentBinding(state, file);
            FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(state.WorkspaceLifecycleState, file.WorkspaceLifecycleEvent);
            if (!transition.IsAccepted) throw new InvalidOperationException("Invalid retained operational transition.");
            // Publication/version/erasure and File authority remain in the primary source fold.
            // No operational concern reads those collections, including subsequent native completion.
            next = state with
            {
                WorkspaceLifecycleState = transition.NextState,
                WorkspaceOperatorDisposition = transition.OperatorDisposition,
                WorkspaceLifecycleEvent = transition.AttemptedEvent,
                WorkspaceOperationId = file.OperationId,
                WorkspaceCorrelationId = file.CorrelationId,
                WorkspaceTaskId = file.TaskId,
                WorkspaceActorPrincipalId = file.ActorPrincipalId,
                WorkspaceLifecycleUpdatedAt = file.OccurredAt,
                WorkspaceNativeProviderWork = state.WorkspaceNativeProviderWork || state.WorkspaceNativeLease && file.ContentBinding?.AcceptedSdkKey is null,
            };
        }
        else next = state.Apply([value], stream);
        return OperationalStatusCompaction.CurrentState(next);
    }

    private static void ValidateContentBinding(FolderState state, WorkspaceFileMutationAccepted file)
    {
        if (file.ContentBinding is not { } binding) return;
        if (binding.ManagedTenantId != file.ManagedTenantId || binding.FolderId != file.FolderId
            || binding.WorkspaceId != file.WorkspaceId || binding.TaskId != file.TaskId
            || string.IsNullOrWhiteSpace(binding.CommandId)
            || (binding.CommandId != file.IdempotencyKey
                && file.IdempotencyKey != WorkspaceContentBinding.BatchItemKey(binding.CommandId, binding.OperationId))
            || binding.FileId != file.OwnerFileId || binding.OperationId != file.OperationId
            || binding.FileOperationKind != file.FileOperationKind || binding.PathMetadataDigest != file.PathMetadataDigest
            || binding.LockFence != state.WorkspaceLockFence || binding.Version <= 0 || binding.ExpectedVersion < 0
            || binding.ContentHashReference != file.ContentHashReference || binding.ByteLength != file.ByteLength
            || binding.MediaType != file.MediaType || binding.PolicyVersion != "1.1.0"
            || string.IsNullOrWhiteSpace(binding.PolicyDigest)
            || (binding.FileOperationKind == "remove") != (binding.ReceiptId is null))
            throw new InvalidOperationException("Invalid operational publication evidence.");
    }
}
