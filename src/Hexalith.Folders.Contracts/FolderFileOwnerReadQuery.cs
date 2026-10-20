namespace Hexalith.Folders.Contracts;

/// <summary>Reads identity-only evidence under the exact owning Folder envelope target.</summary>
/// <param name="OwnerFileId">The canonical owner File identity from an explicit owner mapping.</param>
public sealed record FolderFileOwnerReadQuery(string OwnerFileId)
{
    /// <summary>Gets the additive SDK query discriminator; deployment enrollment remains separate.</summary>
    public const string QueryType = "Hexalith.Folders.Queries.AuthorizeFolderFileRead.v1";
}
