using System.Collections.Frozen;

using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Separates bounded current fold state from per-position and per-view historical evidence.</summary>
internal static class OperationalStatusCompaction
{
    /// <summary>Retains only the current source boundary; historical authority lives in SDK logical records.</summary>
    internal static OperationalStatusCheckpoint Compact(OperationalStatusCheckpoint checkpoint)
    {
        OperationalStatusCheckpoint compact = checkpoint with
        {
            SchemaVersion = 3,
            State = CurrentState(checkpoint.State) with { TaskClosures = FrozenDictionary<string, FolderTaskClosure>.Empty },
            EventDigests = new Dictionary<long, string> { [checkpoint.Sequence] = checkpoint.EventDigests[checkpoint.Sequence] },
            MessageIds = new Dictionary<long, string> { [checkpoint.Sequence] = checkpoint.MessageIds[checkpoint.Sequence] },
            TaskClosedSequences = new Dictionary<string, long>(),
            TaskEvidence = new Dictionary<string, string>(),
            WorkspaceEvidence = new Dictionary<string, string>(),
        };
        return compact with { IntegrityDigest = OperationalStatusIntegrity.Checkpoint(compact) };
    }

    /// <summary>Removes primary content and replay ledgers that no operational transition consumes.</summary>
    internal static FolderState CurrentState(FolderState state) => state with
    {
        ContentErasureGrants = FrozenDictionary<string, long>.Empty,
        CommittedContentReferences = FrozenDictionary<string, string>.Empty,
        NativeReleasedLeaseFences = FrozenDictionary<string, long>.Empty,
        ContentPublications = FrozenDictionary<string, WorkspaceContentBinding>.Empty,
        FileIdentities = FrozenDictionary<string, FolderFileIdentity>.Empty,
        IdempotencyFingerprints = FrozenDictionary<string, string>.Empty,
        AccessOverrides = FrozenDictionary<FolderAccessEntryKey, FolderAccessOverride>.Empty,
    };
}
