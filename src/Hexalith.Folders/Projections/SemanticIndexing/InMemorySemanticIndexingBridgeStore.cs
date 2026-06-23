using Hexalith.Folders.Projections.FolderList;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed class InMemorySemanticIndexingBridgeStore : ISemanticIndexingBridgeReadModel, ISemanticIndexingBridgeWriter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, SemanticIndexingBridgeEntry> _entries = new(StringComparer.Ordinal);

    public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _entries.TryGetValue(identity.ReadModelKey, out SemanticIndexingBridgeEntry? entry);
            return Task.FromResult(entry);
        }
    }

    public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderEventsAsync(
        IReadOnlyCollection<FolderProjectionEnvelope> envelopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            SemanticIndexingBridgeProjection projected = SemanticIndexingBridgeProjection
                .FromEntries(_entries.Values)
                .Apply(envelopes);
            foreach (SemanticIndexingBridgeEntry entry in projected.Entries.Values)
            {
                _entries[entry.Identity.ReadModelKey] = entry;
            }

            return Task.FromResult<IReadOnlyList<SemanticIndexingBridgeEntry>>(
                projected.Entries.Values.OrderBy(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal).ToArray());
        }
    }

    public Task<SemanticIndexingBridgeEntry?> RecordIndexingResultAsync(
        SemanticIndexingResultUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_entries.TryGetValue(update.Identity.ReadModelKey, out SemanticIndexingBridgeEntry? current))
            {
                return Task.FromResult<SemanticIndexingBridgeEntry?>(null);
            }

            SemanticIndexingBridgeEntry next = SemanticIndexingBridgeProjection.ApplyIndexingResult(current, update);
            _entries[update.Identity.ReadModelKey] = next;
            return Task.FromResult<SemanticIndexingBridgeEntry?>(next);
        }
    }

    public Task<SemanticIndexingBridgeEntry?> RecordRemovalEvidenceAsync(
        SemanticIndexingRemovalEvidenceUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_entries.TryGetValue(update.Identity.ReadModelKey, out SemanticIndexingBridgeEntry? current))
            {
                return Task.FromResult<SemanticIndexingBridgeEntry?>(null);
            }

            SemanticIndexingBridgeEntry next = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(current, update);
            _entries[update.Identity.ReadModelKey] = next;
            return Task.FromResult<SemanticIndexingBridgeEntry?>(next);
        }
    }
}
