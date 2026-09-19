namespace Hexalith.Folders.Server.Authorization;

/// <summary>Freshness and usability states for authority evidence evaluated before protected observation.</summary>
internal enum V2AuthorityEvidenceState
{
    Fresh,
    Stale,
    Unavailable,
    Conflicting,
    Incomplete,
}
