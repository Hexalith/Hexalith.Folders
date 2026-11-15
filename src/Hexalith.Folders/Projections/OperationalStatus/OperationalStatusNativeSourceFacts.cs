namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Bounded selected facts; only the SDK scanner can attest their current native observation.</summary>
/// <param name="Sequence">The verified folded head.</param>
/// <param name="SourceDigest">The full original source prefix digest.</param>
/// <param name="CheckpointSourceDigest">The original prefix at the selected predecessor checkpoint.</param>
/// <param name="ValueDigest">The selected view's complete value digest.</param>
/// <param name="ViewSequence">The selected view's original position.</param>
/// <param name="ViewSourceDigest">The selected view's original prefix.</param>
/// <param name="CandidateFound">Whether the exact candidate occupied its asserted position.</param>
/// <param name="MessageReused">Whether its identity also occupied another position.</param>
/// <param name="CandidateConflict">Whether its asserted position contains different original source.</param>
internal sealed record OperationalStatusNativeSourceFacts(long Sequence, string? SourceDigest, string? CheckpointSourceDigest,
    string? ValueDigest, long? ViewSequence, string? ViewSourceDigest, bool CandidateFound, bool MessageReused, bool CandidateConflict);
