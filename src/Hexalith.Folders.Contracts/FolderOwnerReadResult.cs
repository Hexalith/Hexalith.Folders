namespace Hexalith.Folders.Contracts;

/// <summary>Safe current owner authorization evidence; carries no Folder description or provider metadata.</summary>
/// <param name="Outcome">The current owner-read outcome.</param>
public sealed record FolderOwnerReadResult(FolderOwnerReadOutcome Outcome);
