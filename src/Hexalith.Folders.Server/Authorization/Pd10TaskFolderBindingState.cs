namespace Hexalith.Folders.Server.Authorization;

/// <summary>Describes whether fresh task evidence proves the requested task belongs to the authorized folder.</summary>
internal enum Pd10TaskFolderBindingState
{
    /// <summary>The binding is proven by fresh, well-formed evidence.</summary>
    Bound,

    /// <summary>The task is absent or belongs to a different authorized parent.</summary>
    NotBound,

    /// <summary>The binding evidence is stale, malformed, conflicting, or unavailable.</summary>
    Unavailable,
}
