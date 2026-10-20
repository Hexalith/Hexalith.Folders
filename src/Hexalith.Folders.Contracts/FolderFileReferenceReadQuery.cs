namespace Hexalith.Folders.Contracts;

/// <summary>Looks up an explicit Projects-owned binding under a natively verified parent; caller Folder metadata never creates that binding.</summary>
/// <param name="ProjectFileReferenceId">The canonical Projects-owned File reference.</param>
public sealed record FolderFileReferenceReadQuery(string ProjectFileReferenceId)
{
    /// <summary>Gets the additive identity mapping query discriminator.</summary>
    public const string QueryType = "Hexalith.Folders.Queries.ResolveFolderFileReference.v1";
}
