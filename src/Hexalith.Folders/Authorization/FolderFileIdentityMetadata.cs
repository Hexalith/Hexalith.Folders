namespace Hexalith.Folders.Authorization;

/// <summary>Content-free projection of one explicit owner File incarnation.</summary>
/// <param name="OwnerFileId">The stable owner File identity.</param>
/// <param name="ProjectFileReferenceId">The separately bound Projects-owned reference.</param>
/// <param name="Active">Whether the owner incarnation remains active.</param>
public sealed record FolderFileIdentityMetadata(string OwnerFileId, string? ProjectFileReferenceId, bool Active);
