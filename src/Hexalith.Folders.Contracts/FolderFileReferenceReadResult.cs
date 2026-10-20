namespace Hexalith.Folders.Contracts;

/// <summary>Returns only native explicit File mapping and current authority; non-current results omit the mapping.</summary>
/// <param name="Outcome">The owner-read classification.</param>
/// <param name="OwnerFileId">The canonical native owner File identity when current and authorized.</param>
/// <param name="FolderId">The native parent observed in the owner projection.</param>
/// <param name="ProjectFileReferenceId">The exact Projects-owned reference bound by the owner mutation.</param>
/// <param name="Evidence">The native owner evidence.</param>
public sealed record FolderFileReferenceReadResult(FolderOwnerReadOutcome Outcome, string? OwnerFileId,
    string? FolderId, string? ProjectFileReferenceId, FolderOwnerReadEvidence? Evidence);
