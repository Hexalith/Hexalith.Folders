namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Describes whether the authority evidence required by a protected v2 operation is usable.
/// </summary>
internal enum Pd10AuthorityEvidenceState
{
    /// <summary>Evidence is fresh, complete, and conflict-free.</summary>
    Fresh,

    /// <summary>Evidence is older than the authority freshness limit.</summary>
    Stale,

    /// <summary>Evidence cannot currently be obtained.</summary>
    Unavailable,

    /// <summary>Evidence sources disagree.</summary>
    Conflicting,

    /// <summary>One or more required authority dimensions are missing.</summary>
    Incomplete,
}
