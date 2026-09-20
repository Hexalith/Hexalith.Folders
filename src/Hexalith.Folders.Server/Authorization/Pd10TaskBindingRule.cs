namespace Hexalith.Folders.Server.Authorization;

/// <summary>Declares whether a candidate operation must prove a task-to-folder parent binding.</summary>
internal enum Pd10TaskBindingRule
{
    /// <summary>No task-to-folder binding is required.</summary>
    None,

    /// <summary>The route task must belong to the already-authorized route folder.</summary>
    RouteTaskBelongsToRouteFolder,
}
