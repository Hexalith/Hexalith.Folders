using Hexalith.Folders.Projections.FolderList;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public interface ISemanticIndexingBridgeWriter
{
    Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderEventsAsync(
        IReadOnlyCollection<FolderProjectionEnvelope> envelopes,
        CancellationToken cancellationToken = default);

    Task<SemanticIndexingBridgeEntry?> RecordIndexingResultAsync(
        SemanticIndexingResultUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the outcome of a removal (hard delete) or archive (soft delete) egress against a <c>Tombstoned</c>
    /// entry, updating only evidence/outcome fields and never the status (Story 10.4 AC6). Returns the updated entry,
    /// or <see langword="null"/> when the entry is absent.
    /// </summary>
    Task<SemanticIndexingBridgeEntry?> RecordRemovalEvidenceAsync(
        SemanticIndexingRemovalEvidenceUpdate update,
        CancellationToken cancellationToken = default);
}
