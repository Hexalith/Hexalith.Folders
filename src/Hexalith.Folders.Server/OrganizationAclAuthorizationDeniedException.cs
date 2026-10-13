using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server;

/// <summary>Stops an ACL decision before any appendable event crosses the transport boundary.</summary>
internal sealed class OrganizationAclAuthorizationDeniedException(LayeredFolderAuthorizationResult authorization)
    : Exception("Current organization ACL authority is unavailable.")
{
    /// <summary>Gets the metadata-only authorization denial.</summary>
    internal LayeredFolderAuthorizationResult Authorization { get; } = authorization;
}
