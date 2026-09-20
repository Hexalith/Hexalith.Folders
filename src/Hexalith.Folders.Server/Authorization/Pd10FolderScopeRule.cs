namespace Hexalith.Folders.Server.Authorization;

/// <summary>Declares how a candidate operation establishes its folder authorization scope.</summary>
internal enum Pd10FolderScopeRule
{
    /// <summary>The operation is tenant-scoped and has no folder authorization scope.</summary>
    None,

    /// <summary>The folder scope is the exact <c>folderId</c> route value.</summary>
    RouteFolder,

    /// <summary>The folder scope is the exact <c>folderId</c> property in the JSON request body.</summary>
    RequestFolder,
}
