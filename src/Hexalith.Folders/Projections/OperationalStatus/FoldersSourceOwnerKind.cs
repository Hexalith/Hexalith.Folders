namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>The existing source-owner namespaces on the Folders domain route.</summary>
public enum FoldersSourceOwnerKind
{
    /// <summary>A folder actor.</summary>
    Folder,
    /// <summary>The tenant's organization ACL actor.</summary>
    Organization,
    /// <summary>An independent provider-binding actor.</summary>
    ProviderBinding,
}
