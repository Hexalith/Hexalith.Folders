using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Owns the narrow native task completion lifecycle independently of provider commits.</summary>
public static class WorkspaceNativeTaskService
{
    /// <summary>Issues a task- and fence-bound ownership proof from an owner-allocated lock identity.</summary>
    public static string OwnershipProof(string tenant, string folder, string workspace, string task, string lockId, long fence)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", tenant, folder, workspace, task, lockId,
            fence.ToString(System.Globalization.CultureInfo.InvariantCulture)))));

    /// <summary>Folds only owner-admitted lease events; completion preserves all body/publication evidence.</summary>
    public static FolderState Apply(FolderState state, WorkspaceNativeTaskChanged change)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(change);
        if (state.WorkspaceId != change.WorkspaceId || change.LockFence <= 0 || state.LifecycleState != FolderLifecycleState.Active)
            throw new InvalidOperationException("Invalid native task binding.");
        state = state with
        {
            WorkspaceCorrelationId = change.CorrelationId,
            WorkspaceActorPrincipalId = change.ActorPrincipalId,
            WorkspaceTaskId = change.TaskId,
            WorkspaceOperationId = change.PublicationCommandId ?? change.LockId,
            WorkspaceLifecycleUpdatedAt = change.OccurredAt
        };
        if (change.Operation == "acquire")
        {
            if (state.WorkspaceLifecycleState != FolderWorkspaceLifecycleState.Ready || state.WorkspaceLockHolderTaskId is not null
                || change.LockFence != checked(state.WorkspaceLockFence + 1) || state.IsTaskClosed(change.WorkspaceId, change.TaskId))
                throw new InvalidOperationException("Invalid native lease acquisition.");
            return state with
            {
                WorkspaceLifecycleState = FolderWorkspaceLifecycleState.Locked,
                WorkspaceOperatorDisposition = FolderOperatorDisposition.DegradedButServing,
                WorkspaceLockId = change.LockId,
                WorkspaceLockFence = change.LockFence,
                WorkspaceLockIntent = "exclusive_write",
                WorkspaceLifecycleEvent = FolderWorkspaceLifecycleEvent.WorkspaceLocked,
                WorkspaceLockHolderTaskId = change.TaskId,
                WorkspaceLockAcquiredAt = change.EffectiveAt,
                WorkspaceLockEffectiveAt = change.EffectiveAt,
                WorkspaceLockExpiresAt = change.ExpiresAt,
                WorkspaceLockRequestedLeaseSeconds = change.LeaseSeconds,
                WorkspaceLockRetryEligibilityBasis = "lease_until_expiry",
                WorkspaceTaskId = change.TaskId,
                WorkspaceNativeLease = true,
                WorkspaceNativeProviderWork = false,
                WorkspaceLifecycleUpdatedAt = change.OccurredAt
            };
        }
        if (!state.WorkspaceNativeLease || state.WorkspaceLockId != change.LockId || state.WorkspaceLockFence != change.LockFence
            || state.WorkspaceLockHolderTaskId != change.TaskId) throw new InvalidOperationException("Native lease is not owned.");
        if (change.Operation == "renew")
        {
            if (state.WorkspaceLockExpiresAt <= change.OccurredAt || change.EffectiveAt < state.WorkspaceLockEffectiveAt
                || change.ExpiresAt <= change.EffectiveAt) throw new InvalidOperationException("Invalid native lease renewal.");
            return state with { WorkspaceLockEffectiveAt = change.EffectiveAt, WorkspaceLockExpiresAt = change.ExpiresAt };
        }
        if (state.WorkspaceNativeProviderWork) throw new InvalidOperationException("Native task contains provider work.");
        if (change.Operation is not ("complete" or "release")) throw new InvalidOperationException("Unknown native decision.");
        var closures = new Dictionary<string, FolderTaskClosure>(state.TaskClosures, StringComparer.Ordinal);
        var released = new Dictionary<string, long>(state.NativeReleasedLeaseFences, StringComparer.Ordinal);
        if (change.Operation == "complete") closures[FolderState.TaskBindingKey(change.WorkspaceId, change.TaskId)] = new(change.WorkspaceId,
            change.TaskId, "native_completed", change.PublicationCommandId!, change.CorrelationId, change.OccurredAt);
        else released[FolderState.TaskBindingKey(change.TaskId, change.LockId)] = change.LockFence;
        return state with
        {
            WorkspaceLifecycleState = FolderWorkspaceLifecycleState.Ready,
            WorkspaceOperatorDisposition = FolderOperatorDisposition.Available,
            WorkspaceLockId = null,
            WorkspaceLockHolderTaskId = null,
            WorkspaceLockAcquiredAt = null,
            WorkspaceLockEffectiveAt = null,
            WorkspaceLockExpiresAt = null,
            WorkspaceLockRetryEligibilityBasis = null,
            WorkspaceNativeLease = false,
            NativeReleasedLeaseFences = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(released, StringComparer.Ordinal),
            TaskClosures = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(closures, StringComparer.Ordinal),
            WorkspaceLifecycleUpdatedAt = change.OccurredAt
        };
    }
}
