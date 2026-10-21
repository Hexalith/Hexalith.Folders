using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.State.TenantDetail;

using TenantDetailProjection = Hexalith.Tenants.Contracts.Queries.TenantDetail;
using TenantDetailSnapshot = Hexalith.Tenants.UI.State.TenantDetail.TenantDetailSnapshot;

namespace Hexalith.Tenants.UI.State.TenantCommands;

public sealed record TenantLifecycleCommandSnapshot(
    TenantCommandLifecycleState State,
    TenantLifecycleCommandRequest? Intent = null,
    TenantStatus LastConfirmedStatus = TenantStatus.Unknown,
    TenantDetailProjection? LastConfirmedProjection = null,
    bool IsPreviewComplete = false,
    string? MessageId = null,
    string? CorrelationId = null,
    string? PreviewProjectionVersion = null,
    string? BaselineProjectionVersion = null,
    bool HasCommandEventEvidence = false,
    string? SafeMessage = null,
    string? SafeMessageKey = null,
    string? RejectionCode = null,
    TenantCommandAuditState AuditState = TenantCommandAuditState.NotStarted,
    TenantCommandFocusTarget FocusTarget = TenantCommandFocusTarget.Submit,
    TenantCommandLiveRegionPoliteness LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
    DateTimeOffset? AttemptStartedAtUtc = null,
    int PendingStatusPollCount = 0,
    string? RecoveryKey = null,
    string? LastObservedProjectionVersion = null,
    long EvidenceRevision = 0)
{
    internal static readonly TimeSpan MaximumRetainedAttemptDuration = TimeSpan.FromMinutes(5);

    public static TenantLifecycleCommandSnapshot Idle(TenantDetailProjection? lastConfirmedProjection = null)
        => new(
            TenantCommandLifecycleState.Idle,
            LastConfirmedStatus: lastConfirmedProjection?.Status ?? TenantStatus.Unknown,
            LastConfirmedProjection: lastConfirmedProjection);

    /// <summary>Gets whether this logical attempt must survive component dismissal or remount.</summary>
    public bool RetainsAttempt
        => State is TenantCommandLifecycleState.RequestSent
            or TenantCommandLifecycleState.Accepted
            or TenantCommandLifecycleState.ProjectionPending;

    /// <summary>Gets whether aggregate ownership may be released for this attempt.</summary>
    public bool HasTerminalOwnership
        => State is TenantCommandLifecycleState.Confirmed
            or TenantCommandLifecycleState.Rejected
            or TenantCommandLifecycleState.AlreadyApplied
            or TenantCommandLifecycleState.DuplicatePrevented
            or TenantCommandLifecycleState.Failed
            or TenantCommandLifecycleState.Degraded
            or TenantCommandLifecycleState.UnableToVerify;

    public TenantLifecycleCommandSnapshot Previewed(
        TenantLifecycleCommandRequest intent,
        TenantDetailProjection lastConfirmedProjection,
        string previewProjectionVersion)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(lastConfirmedProjection);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewProjectionVersion);

        return this with
        {
            State = TenantCommandLifecycleState.Previewed,
            Intent = intent,
            LastConfirmedStatus = lastConfirmedProjection.Status,
            LastConfirmedProjection = lastConfirmedProjection,
            PreviewProjectionVersion = previewProjectionVersion,
            LastObservedProjectionVersion = previewProjectionVersion,
            IsPreviewComplete = true,
            SafeMessage = null,
            SafeMessageKey = null,
            RecoveryKey = null,
            RejectionCode = null,
            AuditState = TenantCommandAuditStates.NotStarted,
            FocusTarget = TenantCommandFocusTarget.Submit,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    public TenantLifecycleCommandSnapshot DuplicatePrevented(string safeMessage)
        => this with
        {
            State = TenantCommandLifecycleState.DuplicatePrevented,
            SafeMessage = safeMessage,
            SafeMessageKey = null,
            AuditState = TenantCommandAuditStates.NotStarted,
            FocusTarget = TenantCommandFocusTarget.Lifecycle,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
            EvidenceRevision = NextEvidenceRevision(),
        };

    /// <summary>
    /// Starts one lifecycle attempt from fresh authoritative detail evidence and a stable message id.
    /// </summary>
    /// <param name="intent">Exact lifecycle intent.</param>
    /// <param name="lastConfirmedProjection">Fresh pre-submit tenant projection.</param>
    /// <param name="baselineProjectionVersion">Ordered pre-submit tenant projection version.</param>
    /// <param name="messageId">Stable message id used by every dispatch or recovery of this attempt.</param>
    /// <returns>The request-sent snapshot.</returns>
    public TenantLifecycleCommandSnapshot RequestSent(
        TenantLifecycleCommandRequest intent,
        TenantDetailProjection lastConfirmedProjection,
        string baselineProjectionVersion,
        string messageId,
        DateTimeOffset? attemptStartedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(lastConfirmedProjection);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineProjectionVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        return this with
        {
            State = TenantCommandLifecycleState.RequestSent,
            Intent = intent,
            LastConfirmedStatus = lastConfirmedProjection.Status,
            LastConfirmedProjection = lastConfirmedProjection,
            MessageId = messageId,
            CorrelationId = null,
            PreviewProjectionVersion = baselineProjectionVersion,
            BaselineProjectionVersion = baselineProjectionVersion,
            LastObservedProjectionVersion = baselineProjectionVersion,
            HasCommandEventEvidence = false,
            SafeMessage = null,
            SafeMessageKey = null,
            RecoveryKey = null,
            RejectionCode = null,
            AuditState = TenantCommandAuditStates.NotStarted,
            FocusTarget = TenantCommandFocusTarget.Lifecycle,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
            AttemptStartedAtUtc = (attemptStartedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            PendingStatusPollCount = 0,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    public TenantLifecycleCommandSnapshot Accepted(TenantCommandSubmissionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (State is not TenantCommandLifecycleState.RequestSent
            || result.State is not TenantCommandLifecycleState.Accepted
            || result.IsAmbiguousFailure
            || string.IsNullOrWhiteSpace(MessageId)
            || !string.Equals(result.MessageId, MessageId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(result.CorrelationId))
        {
            return UnableToVerify("Tenants.Lifecycle.UnableToVerify.TrackingMismatch");
        }

        return this with
        {
            State = TenantCommandLifecycleState.Accepted,
            CorrelationId = result.CorrelationId,
            SafeMessage = null,
            SafeMessageKey = null,
            RecoveryKey = null,
            RejectionCode = null,
            AuditState = TenantCommandAuditStates.NotStarted,
            FocusTarget = TenantCommandFocusTarget.Lifecycle,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
            AttemptStartedAtUtc = AttemptStartedAtUtc ?? DateTimeOffset.UtcNow,
            PendingStatusPollCount = 0,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    /// <summary>Gets whether the bounded dispatch/reconciliation window has elapsed.</summary>
    /// <param name="observedAtUtc">UTC observation time.</param>
    /// <returns><see langword="true"/> when ownership must be released.</returns>
    internal bool IsRetentionExpired(DateTimeOffset observedAtUtc)
    {
        if (AttemptStartedAtUtc is null)
        {
            return true;
        }

        DateTimeOffset normalizedObserved = observedAtUtc.ToUniversalTime();
        DateTimeOffset normalizedStart = AttemptStartedAtUtc.Value.ToUniversalTime();
        return normalizedObserved < normalizedStart
            || normalizedObserved - normalizedStart >= MaximumRetainedAttemptDuration;
    }

    /// <summary>Retains an ambiguous submission for same-message redispatch.</summary>
    /// <param name="safeMessageKey">Localized support-safe ambiguity explanation.</param>
    /// <returns>The retained request-sent snapshot.</returns>
    public TenantLifecycleCommandSnapshot AmbiguousSubmission(string safeMessageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessageKey);
        return this with
        {
            State = TenantCommandLifecycleState.RequestSent,
            SafeMessage = null,
            SafeMessageKey = safeMessageKey,
            RecoveryKey = "Tenants.Lifecycle.Dispatch.Recovery",
            // Dispatch was attempted but its delivery cannot be proven, so the status is unknown.
            AuditState = TenantCommandAuditStates.Unverifiable,
            FocusTarget = TenantCommandFocusTarget.Refresh,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    public TenantLifecycleCommandSnapshot ApplyStatus(
        TenantCommandStatusResult status,
        DateTimeOffset? observedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (!RetainsAttempt)
        {
            return this;
        }

        if ((status.IsPending && status.IsRetryableFailure)
            || (status.Status is not null && (status.IsPending || status.IsRetryableFailure))
            || (status.Status is not null && !status.HasVerifiedCommandIdentity))
        {
            return UnableToVerify("Tenants.Lifecycle.UnableToVerify.TrackingMismatch");
        }

        DateTimeOffset observed = (observedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        bool attemptExpired = IsRetentionExpired(observed);

        if (status.IsPending || status.IsRetryableFailure)
        {
            int nextPendingPollCount = PendingStatusPollCount == int.MaxValue
                ? int.MaxValue
                : PendingStatusPollCount + 1;
            if (attemptExpired
                && !(State is TenantCommandLifecycleState.ProjectionPending && HasCommandEventEvidence))
            {
                return StatusTimedOut() with
                {
                    PendingStatusPollCount = nextPendingPollCount,
                };
            }

            return this with
            {
                SafeMessage = status.SafeMessage,
                SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                    ? status.IsPending
                        ? "Tenants.Lifecycle.StatusEvidence.Pending"
                        : "Tenants.Lifecycle.StatusEvidence.RetryableFailure"
                    : null),
                RecoveryKey = "Tenants.Lifecycle.Retained.Recovery",
                AuditState = TenantCommandAuditStates.FromStatusLookup(status, AuditState),
                FocusTarget = TenantCommandFocusTarget.Refresh,
                LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
                PendingStatusPollCount = nextPendingPollCount,
                EvidenceRevision = NextEvidenceRevision(),
            };
        }

        if (status.Status is null)
        {
            return this with
            {
                State = TenantCommandLifecycleState.UnableToVerify,
                SafeMessage = status.SafeMessage,
                SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                    ? "Tenants.Lifecycle.UnableToVerify.Status"
                    : null),
                RecoveryKey = null,
                AuditState = TenantCommandAuditStates.FromCommandStatus(null),
                FocusTarget = TenantCommandFocusTarget.Refresh,
                LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
                EvidenceRevision = NextEvidenceRevision(),
            };
        }

        if (attemptExpired
            && !(State is TenantCommandLifecycleState.ProjectionPending && HasCommandEventEvidence)
            && status.Status.Value is CommandStatus.Received
                or CommandStatus.Processing)
        {
            return StatusTimedOut();
        }

        return status.Status.Value switch
        {
            CommandStatus.Received or CommandStatus.Processing
                => this with
                {
                    State = State is TenantCommandLifecycleState.ProjectionPending
                        ? TenantCommandLifecycleState.ProjectionPending
                        : TenantCommandLifecycleState.Accepted,
                    SafeMessage = null,
                    SafeMessageKey = null,
                    RecoveryKey = null,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.EventsStored or CommandStatus.EventsPublished
                => this with
                {
                    State = TenantCommandLifecycleState.ProjectionPending,
                    HasCommandEventEvidence = true,
                    SafeMessage = null,
                    SafeMessageKey = null,
                    RecoveryKey = null,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.Completed
                when status.EventCount is not > 0 && !HasCommandEventEvidence
                => UnableToVerify("Tenants.Lifecycle.UnableToVerify.MissingEventEvidence") with
                {
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                },
            CommandStatus.Completed
                => this with
                {
                    State = TenantCommandLifecycleState.ProjectionPending,
                    HasCommandEventEvidence = HasCommandEventEvidence || status.EventCount is > 0,
                    SafeMessage = null,
                    SafeMessageKey = null,
                    RecoveryKey = null,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.Rejected
                when string.Equals(
                    status.RejectionCode,
                    "TenantLifecycleStateAlreadySet",
                    StringComparison.Ordinal)
                => this with
                {
                    State = TenantCommandLifecycleState.AlreadyApplied,
                    SafeMessage = status.SafeMessage,
                    SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                        ? "Tenants.Lifecycle.Message.Rejected.TenantLifecycleStateAlreadySet"
                        : null),
                    RecoveryKey = null,
                    RejectionCode = status.RejectionCode,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    FocusTarget = TenantCommandFocusTarget.Refresh,
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.Rejected
                => this with
                {
                    State = TenantCommandLifecycleState.Rejected,
                    SafeMessage = status.SafeMessage,
                    SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                        ? status.RejectionCode switch
                        {
                            "InsufficientPermissions" => "Tenants.Lifecycle.Message.Rejected.InsufficientPermissions",
                            "TenantDisabled" => "Tenants.Lifecycle.Message.Rejected.TenantDisabled",
                            "TenantNotFound" => "Tenants.Lifecycle.Message.Rejected.TenantNotFound",
                            _ => "Tenants.Lifecycle.Message.Rejected",
                        }
                        : null),
                    RecoveryKey = null,
                    RejectionCode = status.RejectionCode,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    FocusTarget = TenantCommandFocusTarget.Refresh,
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.PublishFailed
                => this with
                {
                    State = TenantCommandLifecycleState.Degraded,
                    SafeMessage = status.SafeMessage,
                    SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                        ? "Tenants.Lifecycle.Message.Degraded"
                        : null),
                    RecoveryKey = null,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    FocusTarget = TenantCommandFocusTarget.Refresh,
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            CommandStatus.TimedOut
                => this with
                {
                    State = TenantCommandLifecycleState.UnableToVerify,
                    SafeMessage = status.SafeMessage,
                    SafeMessageKey = status.SafeMessageKey ?? (string.IsNullOrWhiteSpace(status.SafeMessage)
                        ? "Tenants.Lifecycle.Message.UnableToVerify"
                        : null),
                    RecoveryKey = null,
                    AuditState = TenantCommandAuditStates.FromCommandStatus(status.Status, status.EventCount, HasCommandEventEvidence),
                    FocusTarget = TenantCommandFocusTarget.Refresh,
                    LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
                    PendingStatusPollCount = 0,
                    EvidenceRevision = NextEvidenceRevision(),
                },
            _ => UnableToVerify("Tenants.Lifecycle.UnableToVerify.Status"),
        };
    }

    public TenantLifecycleCommandSnapshot SignalRNudge()
        => !RetainsAttempt || FocusTarget is TenantCommandFocusTarget.Refresh
            ? this
            : this with
            {
                FocusTarget = TenantCommandFocusTarget.Refresh,
                EvidenceRevision = NextEvidenceRevision(),
            };

    /// <summary>
    /// Reconciles the tracked attempt from one authoritative tenant-detail proof read.
    /// </summary>
    /// <param name="proof">Detail, freshness, lifecycle, and ordered version from one unconditional read.</param>
    /// <returns>The reconciled lifecycle snapshot.</returns>
    public TenantLifecycleCommandSnapshot ConfirmProjection(TenantDetailSnapshot? proof)
    {
        if (Intent is null)
        {
            return this;
        }

        if (!Enum.IsDefined(Intent.Operation))
        {
            return UnableToVerify("Tenants.Lifecycle.UnableToVerify.TrackingMismatch");
        }

        TenantDetailProjection? detailEvidence = proof?.Detail;
        bool tenantMatches = string.Equals(detailEvidence?.TenantId, Intent.TenantId, StringComparison.Ordinal);
        bool authoritative = tenantMatches
            && proof!.Kind is TenantDetailSurfaceKind.Ready
            && proof.Freshness is Hexalith.EventStore.Client.Projections.ReadModelFreshnessState.Current
            && proof.Lifecycle is ProjectionLifecycleState.Current
            && !string.IsNullOrWhiteSpace(proof.ProjectionVersion);
        if (!authoritative)
        {
            return this;
        }

        string currentProjectionVersion = proof!.ProjectionVersion!;

        if (TenantLifecycleProjectionVersion.CompareSequences(
                currentProjectionVersion,
                LastObservedProjectionVersion) is TenantLifecycleSequenceRelation.IncomingOlder)
        {
            return this;
        }

        if (!Enum.IsDefined(detailEvidence!.Status)
            || detailEvidence.Status is TenantStatus.Unknown)
        {
            return ProjectionUnverified("Tenants.Lifecycle.UnableToVerify.ProofRead");
        }

        TenantStatus intendedStatus = Intent.Operation is TenantLifecycleOperation.EnableTenant
            ? TenantStatus.Active
            : TenantStatus.Disabled;

        if (detailEvidence.Status != intendedStatus)
        {
            return ObserveProjectionEvidence(detailEvidence, currentProjectionVersion);
        }

        if (State is not TenantCommandLifecycleState.ProjectionPending)
        {
            return ObserveProjectionEvidence(detailEvidence, currentProjectionVersion, moveFocus: false);
        }

        if (string.IsNullOrWhiteSpace(BaselineProjectionVersion))
        {
            return ProjectionUnverified("Tenants.Lifecycle.UnableToVerify.MissingBaseline");
        }

        if (!HasCommandEventEvidence)
        {
            return ObserveProjectionEvidence(
                detailEvidence,
                currentProjectionVersion,
                "Tenants.Lifecycle.ProjectionEvidence.MissingEventEvidence");
        }

        TenantLifecycleProjectionVersionComparison versionComparison
            = TenantLifecycleProjectionVersion.Compare(BaselineProjectionVersion, currentProjectionVersion);
        if (versionComparison is not TenantLifecycleProjectionVersionComparison.Advanced)
        {
            string safeMessageKey = versionComparison switch
            {
                TenantLifecycleProjectionVersionComparison.Invalid
                    => "Tenants.Lifecycle.ProjectionEvidence.InvalidVersion",
                TenantLifecycleProjectionVersionComparison.PrefixMismatch
                    => "Tenants.Lifecycle.ProjectionEvidence.PrefixMismatch",
                _ => "Tenants.Lifecycle.ProjectionEvidence.NotAdvanced",
            };
            return ObserveProjectionEvidence(detailEvidence, currentProjectionVersion, safeMessageKey);
        }

        return this with
        {
            State = TenantCommandLifecycleState.Confirmed,
            LastConfirmedStatus = detailEvidence.Status,
            LastConfirmedProjection = detailEvidence,
            LastObservedProjectionVersion = MergeObservedProjectionVersion(currentProjectionVersion),
            SafeMessage = null,
            SafeMessageKey = null,
            RecoveryKey = null,
            RejectionCode = null,
            // A projection confirmation carries no attempt-specific audit proof, and this flow has no in-panel
            // audit verification: report proof as unsupported rather than pending forever.
            AuditState = TenantCommandAuditStates.ConfirmedWithoutProof,
            FocusTarget = TenantCommandFocusTarget.Lifecycle,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Polite,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    /// <summary>
    /// Records a terminal, support-safe proof failure without discarding last-confirmed projection truth.
    /// </summary>
    /// <param name="safeMessageKey">Whole-string localized failure key.</param>
    /// <returns>The unable-to-verify snapshot.</returns>
    public TenantLifecycleCommandSnapshot UnableToVerify(string safeMessageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessageKey);
        return this with
        {
            State = TenantCommandLifecycleState.UnableToVerify,
            SafeMessage = null,
            SafeMessageKey = safeMessageKey,
            RecoveryKey = null,
            AuditState = TenantCommandAuditStates.Unverifiable,
            FocusTarget = TenantCommandFocusTarget.Refresh,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    /// <summary>
    /// Records a status lookup or dispatch that timed out, or an attempt that outlived its retention window.
    /// The command stays unverified, and the audit dimension is delayed rather than unavailable.
    /// </summary>
    /// <returns>The unable-to-verify snapshot with a delayed audit dimension.</returns>
    public TenantLifecycleCommandSnapshot StatusTimedOut()
        => UnableToVerify("Tenants.Lifecycle.UnableToVerify.StatusTimeout") with
        {
            AuditState = TenantCommandAuditStates.Delayed,
        };

    /// <summary>
    /// Records a projection proof read that failed, or that was read but could not prove the attempt (missing
    /// baseline, unknown status, or no proof reader). The command stays unverified, and the audit dimension keeps
    /// what command status established, because no audit read happened. The unverified outcome is terminal, so
    /// a pending audit record that nothing will advance any more is reported delayed.
    /// </summary>
    /// <param name="safeMessageKey">Whole-string localized failure key.</param>
    /// <returns>The unable-to-verify snapshot with its audit dimension kept or, when pending, delayed.</returns>
    public TenantLifecycleCommandSnapshot ProjectionUnverified(string safeMessageKey)
        => UnableToVerify(safeMessageKey) with
        {
            AuditState = TenantCommandAuditStates.AfterTrackingEnds(AuditState),
        };

    /// <summary>
    /// Surfaces a blocking reason while preserving the tracked attempt and its recovery handle.
    /// </summary>
    /// <param name="safeMessageKey">Whole-string localized blocking reason.</param>
    /// <returns>The same tracked attempt with support-safe copy.</returns>
    public TenantLifecycleCommandSnapshot BlockedWithTracking(
        string safeMessageKey,
        string? recoveryKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessageKey);
        return this with
        {
            SafeMessage = null,
            SafeMessageKey = safeMessageKey,
            RecoveryKey = recoveryKey,
            FocusTarget = TenantCommandFocusTarget.Refresh,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    /// <summary>
    /// Surfaces already-localized blocking copy while preserving the tracked attempt and recovery handle.
    /// </summary>
    /// <param name="safeMessage">Localized support-safe blocking copy.</param>
    /// <returns>The same tracked attempt with support-safe copy.</returns>
    public TenantLifecycleCommandSnapshot BlockedWithTrackingMessage(
        string safeMessage,
        string? recoveryKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return this with
        {
            SafeMessage = safeMessage,
            SafeMessageKey = null,
            RecoveryKey = recoveryKey,
            FocusTarget = TenantCommandFocusTarget.Refresh,
            LiveRegionPoliteness = TenantCommandLiveRegionPoliteness.Assertive,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    /// <summary>Stops retaining an attempt that the operator explicitly chose to abandon.</summary>
    /// <returns>A terminal snapshot that releases tracked ownership.</returns>
    public TenantLifecycleCommandSnapshot Abandon()
        => UnableToVerify("Tenants.Lifecycle.UnableToVerify.Abandoned");

    private TenantLifecycleCommandSnapshot ObserveProjectionEvidence(
        TenantDetailProjection detailEvidence,
        string projectionVersion,
        string? safeMessageKey = null,
        bool moveFocus = true)
    {
        TenantCommandFocusTarget focusTarget = moveFocus
            ? TenantCommandFocusTarget.Refresh
            : FocusTarget;
        string? observedProjectionVersion = MergeObservedProjectionVersion(projectionVersion);
        string? safeMessage = safeMessageKey is null ? SafeMessage : null;
        string? effectiveSafeMessageKey = safeMessageKey ?? SafeMessageKey;
        string? recoveryKey = safeMessageKey is null
            ? RecoveryKey
            : "Tenants.Lifecycle.Retained.Recovery";
        if (LastConfirmedStatus == detailEvidence.Status
            && Equals(LastConfirmedProjection, detailEvidence)
            && string.Equals(LastObservedProjectionVersion, observedProjectionVersion, StringComparison.Ordinal)
            && string.Equals(SafeMessageKey, effectiveSafeMessageKey, StringComparison.Ordinal)
            && string.Equals(RecoveryKey, recoveryKey, StringComparison.Ordinal)
            && FocusTarget == focusTarget)
        {
            return this;
        }

        return this with
        {
            LastConfirmedStatus = detailEvidence.Status,
            LastConfirmedProjection = detailEvidence,
            LastObservedProjectionVersion = observedProjectionVersion,
            SafeMessage = safeMessage,
            SafeMessageKey = effectiveSafeMessageKey,
            RecoveryKey = recoveryKey,
            FocusTarget = focusTarget,
            EvidenceRevision = NextEvidenceRevision(),
        };
    }

    private string? MergeObservedProjectionVersion(string incoming)
    {
        TenantLifecycleSequenceRelation relation = TenantLifecycleProjectionVersion.CompareSequences(
            incoming,
            LastObservedProjectionVersion);
        return relation is TenantLifecycleSequenceRelation.IncomingNewer
            or TenantLifecycleSequenceRelation.Equal
            ? incoming
            : LastObservedProjectionVersion;
    }

    internal long NextEvidenceRevision()
        => EvidenceRevision == long.MaxValue ? long.MaxValue : EvidenceRevision + 1;
}
