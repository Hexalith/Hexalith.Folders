using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.7 / §3.3 — the assembled, render-ready view-model for the Provider readiness view. A
/// private UI-assembly record (not a Contracts type, not a registered facade / <c>IQueryService</c> —
/// the as-built direct-SDK path, the HIGH 6.5/6.6 lesson): the Provider page composes it from the
/// direct SDK reads enumerated in the story's Data-path note (provider-status diagnostics primary +
/// binding / repository / sync / outcome supplementary).
/// </summary>
/// <remarks>
/// Disposition is the primary visual: it carries the server-computed
/// <see cref="ProviderStatusDiagnostics.Disposition"/> verbatim (never a client-derived value — the
/// client <c>DispositionLabelMapper</c> has no provider-readiness-status overload by design). The
/// readiness reason (<see cref="ReadinessStatus"/> + trust reason codes) is secondary metadata. The
/// credential-reference identifier is a non-secret identifier rendered through <c>SafeCopyId</c> when
/// visible and through <c>RedactedField</c> when redacted; its value is never carried on this record
/// when redacted (defense in depth — <c>redacted</c> ≠ <c>unknown</c> ≠ <c>missing</c>, F-5 / §2.3).
/// </remarks>
public sealed record ProviderReadinessModel
{
    /// <summary>Folder identifier this provider binding belongs to (monospace safe-copy).</summary>
    public required string FolderId { get; init; }

    /// <summary>Correlation id sent on every read for this page load (monospace safe-copy).</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Provider correlation reference echoed by the diagnostics projection, when present.</summary>
    public string? ProviderCorrelationReference { get; init; }

    // --- Provider identity (§3.3) ---

    /// <summary>Whether the provider-binding DTO was loaded (distinguishes unknown vs not-recorded identity).</summary>
    public bool HasBinding { get; init; }

    /// <summary>Provider family reference (non-secret identifier).</summary>
    public string? ProviderFamilyRef { get; init; }

    /// <summary>Provider binding reference (non-secret identifier).</summary>
    public string? ProviderBindingRef { get; init; }

    /// <summary>Capability-profile reference (non-secret identifier).</summary>
    public string? CapabilityProfileRef { get; init; }

    /// <summary>Whether the repository-binding DTO was loaded.</summary>
    public bool HasRepository { get; init; }

    /// <summary>Repository binding identifier (non-secret identifier).</summary>
    public string? RepositoryBindingId { get; init; }

    /// <summary>Repository binding state, when the repository binding was loaded.</summary>
    public RepositoryBindingBindingState? RepositoryBindingState { get; init; }

    /// <summary>Sensitive-metadata tier of the repository binding, when loaded (concern #17).</summary>
    public SensitiveMetadataTier? SensitiveMetadataTier { get; init; }

    // --- Credential-reference identifier (non-secret; redactable) ---

    /// <summary>
    /// Disclosure of the credential-reference identifier. The passive read path never returns a
    /// credential-reference <i>value</i> (<c>nonSecretCredentialReference</c> is a configure-request input
    /// only; <see cref="ProviderBinding.Redaction"/> is a required single-valued marker). So this is only
    /// ever <c>Redacted</c> (provider binding loaded ⇒ hidden by tenant policy) or <c>Unknown</c> (binding
    /// not loaded ⇒ status unknown) — never <c>Visible</c>, and never the provider-binding reference
    /// borrowed and mislabeled as the credential reference (<c>redacted</c> ≠ <c>unknown</c>, F-5 / §2.3).
    /// </summary>
    public FieldDisclosure CredentialReferenceDisclosure { get; init; }

    // --- Readiness (disposition primary, reason secondary — F-4 / AC #4) ---

    /// <summary>Server-computed operator disposition — the PRIMARY readiness visual (fed straight into the badge).</summary>
    public OperatorDispositionLabel Disposition { get; init; }

    /// <summary>Operator-sanitized readiness status text (secondary reason metadata; never raw provider text).</summary>
    public string? ReadinessStatus { get; init; }

    // --- Freshness zone (drive staleness from Freshness.Stale + Trust reason codes; no hardcoded C2/C5 lag — §7) ---

    /// <summary>Projection availability from the diagnostics trust evidence.</summary>
    public ProjectionAvailability Availability { get; init; }

    /// <summary>When the projection observed this evidence.</summary>
    public DateTimeOffset? FreshnessObservedAt { get; init; }

    /// <summary>Projection watermark (opaque ordering token).</summary>
    public string? ProjectionWatermark { get; init; }

    /// <summary>Whether the diagnostics evidence is stale (the boolean signal, not a numeric C2 lag).</summary>
    public bool FreshnessStale { get; init; }

    /// <summary>Stable stale reason code from trust evidence, when present.</summary>
    public string? StaleReasonCode { get; init; }

    /// <summary>Stable unavailable reason code from trust evidence, when present.</summary>
    public string? UnavailableReasonCode { get; init; }

    // --- Optional capability/sync + failure zones ---

    /// <summary>Sync metadata, present only when a workspace context was resolved (workspace-scoped read).</summary>
    public ProviderSyncView? Sync { get; init; }

    /// <summary>Failure metadata + retryability, present only when an operation context was resolved (advisory only).</summary>
    public ProviderOutcomeView? Outcome { get; init; }

    /// <summary>
    /// Assembles the render-ready model from the passive SDK reads. <paramref name="diagnostics"/> is
    /// the primary readiness source; the rest are supplementary and may be <see langword="null"/> when a
    /// supplementary read was unavailable, denied, or out of context (rendered as honest Unknown).
    /// </summary>
    public static ProviderReadinessModel Create(
        string folderId,
        string correlationId,
        ProviderStatusDiagnostics diagnostics,
        ProviderBinding? binding,
        RepositoryBinding? repository,
        SyncStatusDiagnostics? sync,
        ProviderOutcome? outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new ProviderReadinessModel
        {
            FolderId = folderId,
            CorrelationId = correlationId,
            ProviderCorrelationReference = Norm(diagnostics.ProviderCorrelationReference),

            HasBinding = binding is not null,
            ProviderFamilyRef = Norm(binding?.ProviderFamilyRef),
            ProviderBindingRef = Norm(binding?.ProviderBindingRef),
            CapabilityProfileRef = Norm(binding?.CapabilityProfileRef),

            HasRepository = repository is not null,
            RepositoryBindingId = Norm(repository?.RepositoryBindingId),
            RepositoryBindingState = repository?.BindingState,
            SensitiveMetadataTier = repository?.SensitiveMetadataTier,

            CredentialReferenceDisclosure = ResolveCredentialReferenceDisclosure(binding),

            Disposition = diagnostics.Disposition,
            ReadinessStatus = Norm(diagnostics.Status),

            Availability = diagnostics.Trust?.Availability ?? ProjectionAvailability.Unknown,
            FreshnessObservedAt = NormObservedAt(diagnostics.Freshness?.ObservedAt),
            ProjectionWatermark = Norm(diagnostics.Freshness?.ProjectionWatermark),
            FreshnessStale = diagnostics.Freshness?.Stale ?? false,
            StaleReasonCode = Norm(diagnostics.Trust?.StaleReasonCode),
            UnavailableReasonCode = Norm(diagnostics.Trust?.UnavailableReasonCode),

            Sync = sync is null
                ? null
                : new ProviderSyncView
                {
                    ProjectedState = sync.ProjectedState,
                    OutcomeState = sync.ProviderOutcomeState,
                    FreshnessObservedAt = NormObservedAt(sync.Freshness?.ObservedAt),
                    FreshnessStale = sync.Freshness?.Stale ?? false,
                },

            Outcome = outcome is null
                ? null
                : new ProviderOutcomeView
                {
                    State = outcome.State,
                    SanitizedCategory = outcome.SanitizedStatusClass,
                    RetryEligible = outcome.RetryEligibility?.Eligible ?? false,
                    RetryReasonCode = Norm(outcome.RetryEligibility?.ReasonCode),
                    RetryAdvisoryOnly = outcome.RetryEligibility?.AdvisoryOnly ?? true,
                    RetryAfterSeconds = outcome.RetryAfter?.RetryAfterSeconds,
                    ProviderCorrelationReference = Norm(outcome.ProviderCorrelationReference),
                    FreshnessObservedAt = NormObservedAt(outcome.Freshness?.ObservedAt),
                    FreshnessStale = outcome.Freshness?.Stale ?? false,
                },
        };
    }

    private static FieldDisclosure ResolveCredentialReferenceDisclosure(ProviderBinding? binding)
    {
        // The passive read path never surfaces a credential-reference VALUE: `nonSecretCredentialReference`
        // is a configure-request (mutation) input — returned by NO read DTO — and `ProviderBinding.Redaction`
        // is a required, single-valued marker (always `credential_reference_redacted`). So when the provider
        // binding is loaded the credential reference is redacted-by-policy; when it is not loaded the status
        // is honestly Unknown. We deliberately do NOT borrow `diagnostics.ProviderBindingReference` (that is
        // the PROVIDER-BINDING reference — its example value is `opaque_…PBR…`, surfaced separately as
        // "Provider binding") and mislabel it as the credential reference (redacted ≠ unknown ≠ the wrong
        // identifier; F-5 / §2.3 / AC #3). This honest gap mirrors the story's "do not paper over" discipline.
        return binding is { Redaction: ProviderBindingRedaction.Credential_reference_redacted }
            ? FieldDisclosure.Redacted
            : FieldDisclosure.Unknown;
    }

    private static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // A default/min ObservedAt (the read model didn't populate freshness) renders as Unknown, never a
    // fabricated 0001-01-01 timestamp (mirrors the 6.6 Workspace page's ObservedAtOrNull discipline, UX-DR26).
    private static DateTimeOffset? NormObservedAt(DateTimeOffset? observedAt)
        => observedAt is { } value && value != default ? value : null;
}

/// <summary>Story 6.7 — render-ready sync metadata (workspace-scoped; shown only when a workspace context is resolved).</summary>
public sealed record ProviderSyncView
{
    /// <summary>Projected lifecycle state (technical-state secondary metadata).</summary>
    public LifecycleState ProjectedState { get; init; }

    /// <summary>Provider outcome state of the latest sync (badge, awaiting-human treatment for unknown_provider_outcome).</summary>
    public ProviderOutcomeState OutcomeState { get; init; }

    /// <summary>When the sync evidence was observed.</summary>
    public DateTimeOffset? FreshnessObservedAt { get; init; }

    /// <summary>Whether the sync evidence is stale.</summary>
    public bool FreshnessStale { get; init; }
}

/// <summary>
/// Story 6.7 — render-ready provider failure metadata + retryability (advisory DISPLAY only, never an
/// action; shown only when an operation context is resolved). Retryability comes from the passive
/// <c>ProviderOutcome</c> read — the active validations probe is out of scope (AC #5).
/// </summary>
public sealed record ProviderOutcomeView
{
    /// <summary>Provider outcome state (badge).</summary>
    public ProviderOutcomeState State { get; init; }

    /// <summary>Sanitized canonical error category for the failure (operator-facing label, never raw provider text).</summary>
    public CanonicalErrorCategory SanitizedCategory { get; init; }

    /// <summary>Whether a retry is eligible (advisory display only — never a retry button).</summary>
    public bool RetryEligible { get; init; }

    /// <summary>Stable retry reason code, when present.</summary>
    public string? RetryReasonCode { get; init; }

    /// <summary>Whether retryability is advisory only (defaults to <see langword="true"/> — no action affordance).</summary>
    public bool RetryAdvisoryOnly { get; init; }

    /// <summary>Advisory retry-after seconds, when present.</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>Provider correlation reference for this operation, when present.</summary>
    public string? ProviderCorrelationReference { get; init; }

    /// <summary>When the outcome evidence was observed.</summary>
    public DateTimeOffset? FreshnessObservedAt { get; init; }

    /// <summary>Whether the outcome evidence is stale.</summary>
    public bool FreshnessStale { get; init; }
}
