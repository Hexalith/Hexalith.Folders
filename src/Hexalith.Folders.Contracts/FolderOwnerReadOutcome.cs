namespace Hexalith.Folders.Contracts;

/// <summary>Closed current owner-read evidence outcomes.</summary>
public enum FolderOwnerReadOutcome
{
    /// <summary>Current tenant, exact read_metadata ACL, and active lifecycle are authorized.</summary>
    Allowed,
    /// <summary>Current required evidence is stale.</summary>
    Stale,
    /// <summary>Current required evidence cannot be verified.</summary>
    Unavailable,
}
