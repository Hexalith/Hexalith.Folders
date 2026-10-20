namespace Hexalith.Folders.Contracts;

/// <summary>Native owner revision observations; absent evidence cannot establish current resolution authority.</summary>
/// <param name="PermissionVersion">The bounded native Folder, Organization and Tenant permission revision tuple.</param>
/// <param name="ResourceVersion">The native resource revision.</param>
/// <param name="ResourceWatermark">The source position covered by the resource view.</param>
/// <param name="LastVerifiedAt">The actual native resource observation instant.</param>
/// <param name="AuthorizedAt">The actual owner policy check instant.</param>
public sealed record FolderOwnerReadEvidence(string PermissionVersion, string ResourceVersion,
    string ResourceWatermark, DateTimeOffset LastVerifiedAt, DateTimeOffset AuthorizedAt);
