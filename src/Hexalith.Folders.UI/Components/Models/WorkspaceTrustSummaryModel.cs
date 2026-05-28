using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.6 / UX-DR5 — the assembled, render-ready view-model for the Workspace Trust Summary. A
/// private UI-assembly record (not a Contracts type, not a registered facade): the Workspace page
/// composes it from the direct SDK reads enumerated in the story's Data-path note (diagnostics primary
/// + status DTOs supplementary). The operator-disposition badge is the primary visual; the technical
/// state is secondary metadata (F-4).
/// </summary>
public sealed record WorkspaceTrustSummaryModel
{
    /// <summary>Authoritative tenant identifier (from authenticated context, monospace safe-copy).</summary>
    public required string Tenant { get; init; }

    /// <summary>Folder identifier (monospace safe-copy).</summary>
    public required string Folder { get; init; }

    /// <summary>Workspace identifier (monospace safe-copy).</summary>
    public required string WorkspaceId { get; init; }

    /// <summary>Current technical lifecycle state (secondary metadata; drives the derived disposition).</summary>
    public LifecycleState CurrentState { get; init; }

    /// <summary>Whether projection-lag evidence is present (the boolean <c>Freshness.Stale</c> signal, not a numeric C2 lag).</summary>
    public bool HasProjectionLagEvidence { get; init; }

    /// <summary>
    /// Optional server-computed disposition from a diagnostics DTO. When present it is the primary visual
    /// directly (AC #7); when <see langword="null"/> the disposition is derived from <see cref="CurrentState"/>.
    /// </summary>
    public OperatorDispositionLabel? ServerDisposition { get; init; }

    /// <summary>Authorization posture for this workspace scope.</summary>
    public TenantAccessState AuthorizationPosture { get; init; } = TenantAccessState.Unknown;

    /// <summary>Lock state, when fetched.</summary>
    public LockState? LockState { get; init; }

    /// <summary>Short dirty-state label (e.g. the dirty diagnostics status); defaults to unknown.</summary>
    public string DirtyState { get; init; } = "Unknown";

    /// <summary>
    /// Server-computed dirty-state disposition (from the dirty diagnostics DTO), when present. Drives the
    /// dirty-state status badge so it carries text + icon/shape + colour + label (UX-DR14) instead of a
    /// bare uncoloured span; <see langword="null"/> when no dirty diagnostic was returned.
    /// </summary>
    public OperatorDispositionLabel? DirtyDisposition { get; init; }

    /// <summary>Task identifier (monospace safe-copy).</summary>
    public string TaskId { get; init; } = "Unknown";

    /// <summary>Correlation identifier echoed for this page load (monospace safe-copy).</summary>
    public string CorrelationId { get; init; } = "Unknown";

    /// <summary>Disclosure for the repository-binding identifier (tenant-sensitive).</summary>
    public FieldDisclosure RepositoryBindingDisclosure { get; init; } = FieldDisclosure.Unknown;

    /// <summary>Repository-binding identifier value (rendered only when the disclosure is visible).</summary>
    public string? RepositoryBinding { get; init; }

    /// <summary>Disclosure for the provider-binding reference (tenant-sensitive).</summary>
    public FieldDisclosure ProviderDisclosure { get; init; } = FieldDisclosure.Unknown;

    /// <summary>Provider-binding reference value (rendered only when the disclosure is visible).</summary>
    public string? Provider { get; init; }

    /// <summary>Disclosure for the commit reference (never a raw hash; classification only).</summary>
    public FieldDisclosure CommitReferenceDisclosure { get; init; } = FieldDisclosure.Unknown;

    /// <summary>Commit-reference classification text (rendered only when the disclosure is visible).</summary>
    public string? CommitReferenceText { get; init; }

    /// <summary>Latest reason category (from <c>WorkspaceStatus.LastFailureCategory</c>); <c>Success</c> means none.</summary>
    public CanonicalErrorCategory LatestReasonCategory { get; init; } = CanonicalErrorCategory.Success;

    /// <summary>Freshness observation timestamp, when present.</summary>
    public DateTimeOffset? FreshnessObservedAt { get; init; }

    /// <summary>Projection watermark, when present.</summary>
    public string? ProjectionWatermark { get; init; }

    /// <summary>Whether the data is stale relative to the freshness target (UX-DR26).</summary>
    public bool FreshnessStale { get; init; }

    /// <summary>Stale/unavailable reason code (only from a diagnostics DTO; <c>FreshnessMetadata</c> has none).</summary>
    public string? FreshnessReasonCode { get; init; }
}
