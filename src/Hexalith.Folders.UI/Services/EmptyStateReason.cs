namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / §3.8 — the four <b>distinct</b> empty-state reasons a diagnostic page renders, none of
/// which may confirm unauthorized resource existence (UX-DR20/21). A presentation-only vocabulary.
/// </summary>
public enum EmptyStateReason
{
    /// <summary>Query valid, zero results.</summary>
    NoMatches,

    /// <summary>Query under-specified; prompt the operator to narrow or scope the request.</summary>
    InsufficientFilterScope,

    /// <summary>The read model / projection is down and cannot answer (§2 <c>unavailable</c>).</summary>
    ReadModelUnavailable,

    /// <summary>Safe denial that does not confirm whether any resource exists (UX-DR21).</summary>
    DeniedAccess,
}
