namespace Hexalith.Folders.Contracts;

/// <summary>Safe current owner authorization evidence; carries no Folder description or provider metadata.</summary>
/// <param name="Outcome">The current owner-read outcome.</param>
public sealed record FolderOwnerReadResult(FolderOwnerReadOutcome Outcome)
{
    /// <summary>Gets optional native evidence. Existing positional v1 consumers retain their outcome semantics.</summary>
    public FolderOwnerReadEvidence? Evidence { get; init; }
}
