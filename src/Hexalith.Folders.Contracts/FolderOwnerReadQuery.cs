namespace Hexalith.Folders.Contracts;

/// <summary>Exact current owner authorization for descriptive Folder metadata; target is the envelope aggregate ID.</summary>
public sealed record FolderOwnerReadQuery
{
    /// <summary>The registered DomainService owner query type.</summary>
    public const string QueryType = "Hexalith.Folders.Queries.AuthorizeFolderRead.v1";
}
