namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Bounded full-row source evidence, stored separately from its rebuildable view.</summary>
/// <param name="Sequence">The original view observation ordinal.</param>
/// <param name="SourceDigest">The original source prefix digest.</param>
/// <param name="ValueDigest">Canonical evidence for every field of the view.</param>
public sealed record OperationalStatusViewEvidence(long Sequence, string SourceDigest, string ValueDigest);
