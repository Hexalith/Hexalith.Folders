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
}
