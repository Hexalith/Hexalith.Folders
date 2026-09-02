namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Identifies the authorization outcome of a folder lifecycle-status query.
/// </summary>
internal enum FolderLifecycleAuthorizationOutcome
{
    /// <summary>
    /// The query was denied without exposing protected lifecycle data.
    /// </summary>
    DeniedSafe = 0,

    /// <summary>
    /// The query was authorized to return lifecycle data.
    /// </summary>
    Allowed = 1,
}
