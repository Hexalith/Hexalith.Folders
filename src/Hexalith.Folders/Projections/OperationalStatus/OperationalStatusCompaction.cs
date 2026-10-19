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
            SchemaVersion = 2,
            State = checkpoint.State with { TaskClosures = FrozenDictionary<string, FolderTaskClosure>.Empty },
            EventDigests = new Dictionary<long, string> { [checkpoint.Sequence] = checkpoint.EventDigests[checkpoint.Sequence] },
            MessageIds = new Dictionary<long, string> { [checkpoint.Sequence] = checkpoint.MessageIds[checkpoint.Sequence] },
            TaskClosedSequences = new Dictionary<string, long>(),
            TaskEvidence = new Dictionary<string, string>(),
            WorkspaceEvidence = new Dictionary<string, string>(),
        };
        return compact with { IntegrityDigest = OperationalStatusIntegrity.Checkpoint(compact) };
    }
}
