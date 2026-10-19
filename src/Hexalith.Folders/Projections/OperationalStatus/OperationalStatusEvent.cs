using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>A validated source position; rejected commands occupy positions without changing domain state.</summary>
/// <param name="Sequence">The source sequence.</param>
/// <param name="MessageId">The retained message identity.</param>
/// <param name="Timestamp">The retained persistence time.</param>
/// <param name="Digest">The source identity and payload digest.</param>
/// <param name="Event">The current semantic event, or null for a known rejection.</param>
public sealed record OperationalStatusEvent(long Sequence, string MessageId, DateTimeOffset Timestamp,
    string Digest, IFolderEvent? Event)
{
    /// <summary>Gets original transport metadata for the SDK-owned durable delivery journal.</summary>
    public Hexalith.EventStore.Contracts.Projections.ProjectionEventDto? Source { get; init; }
}
