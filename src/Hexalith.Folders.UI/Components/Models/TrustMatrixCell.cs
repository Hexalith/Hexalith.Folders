using Hexalith.Folders.UI.Services;

namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.6 / UX-DR9 — one Trust Matrix cell: a trust dimension plus its state, reason summary,
/// last-updated time, and a link to supporting evidence (the connected-evidence requirement, UX-DR19).
/// A UI-assembly view-model record assembled by the Workspace page.
/// </summary>
/// <param name="Dimension">The trust dimension name (e.g. "Tenant boundary").</param>
/// <param name="State">The dimension's resolved state.</param>
/// <param name="ReasonSummary">A short, metadata-only reason summary.</param>
/// <param name="LastUpdated">When the supporting evidence was last observed, if known.</param>
/// <param name="EvidenceHref">Route to the supporting evidence page (UX-DR19), if any.</param>
/// <param name="EvidenceLabel">Accessible label for the evidence link.</param>
public sealed record TrustMatrixCell(
    string Dimension,
    TrustDimensionState State,
    string ReasonSummary,
    DateTimeOffset? LastUpdated,
    string? EvidenceHref,
    string? EvidenceLabel);
