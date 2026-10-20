namespace Hexalith.Folders.Contracts;

/// <summary>Returns no path, content, provider, workspace or inferred reference identity.</summary>
/// <param name="Outcome">The current owner classification.</param>
/// <param name="Evidence">Native evidence only after both strict authorization passes agree.</param>
public sealed record FolderFileOwnerReadResult(FolderOwnerReadOutcome Outcome, FolderOwnerReadEvidence? Evidence);
