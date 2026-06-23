using Hexalith.Folders.Client.Generated;

using NSubstitute;

// The IClient stub helpers below configure the CancellationToken overloads of the reads with an
// Arg.Any<CancellationToken>() matcher. These are substitute configuration, not cancellable test
// operations, so xUnit1051 (pass TestContext.Current.CancellationToken) does not apply — mirror the
// bUnit ConsoleSweepFixtures that suppress it for the same reason.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

/// <summary>
/// Story 8.4 — synthetic, metadata-only stub <see cref="IClient"/> builders for the populated-backend
/// accessibility host. The journey pages inject <see cref="IClient"/> (a typed HttpClient registered
/// Transient by <c>AddFoldersClient</c>); the host replaces that single descriptor with one of these
/// substitutes so every console journey renders its fully-populated read-only surface (tables, trust
/// summaries, timelines, metadata trees) for the axe / WCAG 2.2 AA scan, instead of degrading to the
/// §3.8 read-model-unavailable state the dead-loopback hermetic host produces.
/// <para>
/// These DTO shapes mirror the bUnit <c>Hexalith.Folders.UI.Tests.ConsoleSweepFixtures</c> verbatim (that
/// type is <c>internal</c> to the UI.Tests assembly, so the minimal set is replicated here per the story's
/// permitted fallback — one synthetic source of truth would otherwise require an <c>InternalsVisibleTo</c>
/// or a shared compile-link). Every identifier is synthetic (<c>folder-1</c>, <c>workspace-1</c>, …); no
/// real tenant / folder / credential / path / audit data appears. The <see cref="Density.Dense"/> variant
/// seeds long folder IDs / long paths / dense identifiers into the UX-DR31 surfaces for the zoom /
/// no-clipping invariant (Task 5); it stays synthetic and metadata-only.
/// </para>
/// </summary>
public static class ConsoleStubFixtures
{
    /// <summary>The synthetic folder identifier the happy-path journeys navigate against.</summary>
    public const string FolderId = "folder-1";

    /// <summary>The synthetic workspace identifier the happy-path journeys navigate against.</summary>
    public const string WorkspaceId = "workspace-1";

    /// <summary>The bare-route incident-stream URL with the folder query the F-6 read path requires.</summary>
    public const string IncidentStreamRoute = "/_admin/incident-stream?folder=folder-1";

    // Long synthetic identifiers / paths for the UX-DR31 dense-identifier stress (Task 5). Plausible
    // operational shapes (region-scoped platform paths, full sha digests) — never real tenant data.
    private const string DenseFolderId = "folder-archive-eu-west-1-platform-infrastructure-service-mesh-region-0007";
    private const string DenseCorrelationId = "corr-7f3c9a21-8b4e-4d6a-9f1c-2e5d8a0b3c6f-incident-window-2026-06-23";
    private const string DenseTaskId = "task-provisioning-reconciliation-eu-west-1-0007-attempt-3-of-5-backoff";
    private const string DenseOperationId = "op-workspace-preparation-eu-west-1-platform-infrastructure-0007-step-4";
    private const string DenseRecordId = "audit-7f3c9a21-8b4e-4d6a-9f1c-2e5d8a0b3c6f-changed-path-evidence-digest-0007";
    private const string DenseEntryId = "timeline-entry-7f3c9a21-8b4e-4d6a-9f1c-2e5d8a0b3c6f-state-transition-0007";
    private const string DenseActor = "actor-service-principal-eu-west-1-platform-infrastructure-reconciler-0007";
    private const string DenseDigest = "sha256:3a7bd3e2360a3d29eea436fcfb7e44c735d117c42d1c1835420b6b9942dd4f1b";
    private const string DenseRepoBindingId = "rb-eu-west-1-platform-infrastructure-service-mesh-region-0007-binding-3";
    private const string DenseProviderBindingRef = "pbr-eu-west-1-platform-infrastructure-service-mesh-region-0007-cred-9";

    /// <summary>Selects between the happy-path and the UX-DR31 dense-identifier dataset.</summary>
    public enum Density
    {
        /// <summary>Short, conventional synthetic identifiers (the per-page bUnit shapes).</summary>
        HappyPath,

        /// <summary>Long folder IDs / long paths / dense identifiers for the zoom / no-clipping invariant.</summary>
        Dense,
    }

    /// <summary>
    /// Builds a single substitute <see cref="IClient"/> configured for every journey page so one DI
    /// registration serves all read-only console routes. Methods that several pages share (e.g.
    /// <c>GetFolderLifecycleStatusAsync</c>, <c>ListOperationTimelineAsync</c>) resolve to the same
    /// populated value, so configuring them together does not conflict.
    /// </summary>
    public static IClient CreateClient(Density density)
    {
        IClient client = Substitute.For<IClient>();

        // J1 find-and-inspect-trust-state: folder detail + workspace trust summary/matrix.
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle(density));
        client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(WorkspaceStatusValue(density));

        // J2 prove-tenant-isolation: provider readiness + tenant-scoped provider support.
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderDiagnostics(density));
        client.GetProviderBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderBindingValue(density));
        client.GetRepositoryBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(RepositoryBindingValue(density));
        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(SupportEvidence(
                SupportRow(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported),
                SupportRow(ProviderCapabilityName.File_operations, ProviderCapabilityState.Unsupported)));

        // J3 diagnose-failure-from-evidence: audit trail + operation timeline + incident stream.
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuditPage(VisibleAuditRecord(density)));
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TimelinePage(VisibleTimelineEntry(density)));

        return client;
    }

    private static string Folder(Density d) => d == Density.Dense ? DenseFolderId : FolderId;

    private static string Correlation(Density d) => d == Density.Dense ? DenseCorrelationId : "corr-1";

    private static string Task(Density d) => d == Density.Dense ? DenseTaskId : "task-1";

    private static string Operation(Density d) => d == Density.Dense ? DenseOperationId : "op-1";

    // ---------------------------------------------------------------------------------------------------
    // Metadata-only synthetic DTO builders (mirrored from the bUnit ConsoleSweepFixtures).
    // ---------------------------------------------------------------------------------------------------

    private static FreshnessMetadata Fresh()
        => new()
        {
            Stale = false,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };

    private static FolderLifecycleStatus Lifecycle(Density d)
        => new()
        {
            FolderId = Folder(d),
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            RepositoryBindingId = d == Density.Dense ? DenseRepoBindingId : "rb-1",
            ProviderBindingRef = d == Density.Dense ? DenseProviderBindingRef : "pbr-1",
            Freshness = Fresh(),
        };

    private static WorkspaceStatus WorkspaceStatusValue(Density d)
        => new()
        {
            FolderId = Folder(d),
            WorkspaceId = d == Density.Dense ? "workspace-eu-west-1-platform-infrastructure-region-0007-active" : WorkspaceId,
            CurrentState = LifecycleState.Ready,
            AcceptedCommandState = new AcceptedCommandState
            {
                TaskId = Task(d),
                OperationId = Operation(d),
                AcceptedAt = DateTimeOffset.UnixEpoch,
            },
            LastFailureCategory = CanonicalErrorCategory.Success,
            Freshness = Fresh(),
        };

    private static AuditTrailPage AuditPage(params AuditRecord[] records)
        => new()
        {
            Entries = [.. records],
            Page = new PaginationMetadata { Cursor = string.Empty, Limit = 50, IsTruncated = false },
            RetentionClass = "TODO(reference-pending):default",
            Freshness = Fresh(),
        };

    private static AuditRecord VisibleAuditRecord(Density d)
        => new()
        {
            AuditRecordId = d == Density.Dense ? DenseRecordId : "audit-1",
            ActorReference = Actor(d == Density.Dense ? DenseActor : "actor-1"),
            TaskId = Task(d),
            OperationId = OperationRef(Operation(d)),
            CorrelationId = Correlation(d),
            ResultStatus = CanonicalErrorCategory.Success,
            SanitizedErrorCategory = CanonicalErrorCategory.Success,
            Retryable = false,
            DurationMilliseconds = 42,
            EvidenceTimestamp = Timestamp(DateTimeOffset.UnixEpoch, RedactableAuditTimestampPrecision.Exact),
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
            ChangedPathEvidence = ChangedPath(
                ChangedPathEvidenceEvidenceKind.Digest,
                d == Density.Dense ? DenseDigest : "sha256:abc",
                DiagnosticFieldClassification.Operator_sanitized),
            Freshness = Fresh(),
        };

    private static RedactableAuditActorReference Actor(string? value)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
        };

    private static RedactableAuditOperationReference OperationRef(string? value)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
        };

    private static RedactableAuditTimestamp Timestamp(DateTimeOffset value, RedactableAuditTimestampPrecision precision)
        => new()
        {
            Value = value,
            Precision = precision,
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
        };

    private static ChangedPathEvidence2 ChangedPath(ChangedPathEvidenceEvidenceKind kind, string? digest, DiagnosticFieldClassification classification)
        => new()
        {
            EvidenceKind = kind,
            Digest = digest,
            Reference = null,
            Classification = classification,
        };

    private static RedactionMetadata Marker(RedactionMetadataVisibility visibility)
        => new() { Visibility = visibility, ReasonCode = "none" };

    private static OperationTimelinePage TimelinePage(params OperationTimelineEntry[] entries)
        => new()
        {
            Entries = [.. entries],
            Page = new PaginationMetadata { Cursor = string.Empty, Limit = 50, IsTruncated = false },
            RetentionClass = "TODO(reference-pending):default",
            Freshness = Fresh(),
        };

    private static OperationTimelineEntry VisibleTimelineEntry(Density d)
        => new()
        {
            TimelineEntryId = d == Density.Dense ? DenseEntryId : "entry-1",
            OperationId = Operation(d),
            TaskId = Task(d),
            CorrelationId = Correlation(d),
            WorkspaceReference = WorkspaceRef(d == Density.Dense ? "workspace-eu-west-1-platform-infrastructure-region-0007-active" : "workspace-1"),
            StateTransition = new DiagnosticStateTransition
            {
                FromState = LifecycleState.Preparing,
                ToState = LifecycleState.Ready,
                Disposition = OperatorDispositionLabel.Available,
            },
            SanitizedResult = CanonicalErrorCategory.Success,
            Retryable = false,
            DurationMilliseconds = 99,
            EvidenceTimestamp = DateTimeOffset.UnixEpoch,
            Freshness = Fresh(),
        };

    private static RedactableDiagnosticIdentifier WorkspaceRef(string? value)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = new RedactionMetadata { Visibility = RedactionMetadataVisibility.Metadata_only, ReasonCode = "none" },
        };

    private static ProviderStatusDiagnostics ProviderDiagnostics(Density d)
        => new()
        {
            Status = "ready",
            Disposition = OperatorDispositionLabel.Available,
            Trust = new DiagnosticTrustEvidence { Availability = ProjectionAvailability.Available },
            Freshness = Fresh(),
            ProviderBindingReference = new RedactableDiagnosticIdentifier
            {
                Value = d == Density.Dense ? DenseProviderBindingRef : "diag-ref-1",
                Classification = DiagnosticFieldClassification.Operator_sanitized,
            },
            ProviderCorrelationReference = Correlation(d),
        };

    private static ProviderBinding ProviderBindingValue(Density d)
        => new()
        {
            ProviderBindingRef = d == Density.Dense ? DenseProviderBindingRef : "pbr-1",
            ProviderFamilyRef = "github",
            CapabilityProfileRef = "cap-1",
            Redaction = ProviderBindingRedaction.Credential_reference_redacted,
            Freshness = Fresh(),
        };

    private static RepositoryBinding RepositoryBindingValue(Density d)
        => new()
        {
            RepositoryBindingId = d == Density.Dense ? DenseRepoBindingId : "rb-1",
            FolderId = Folder(d),
            ProviderBindingRef = d == Density.Dense ? DenseProviderBindingRef : "pbr-1",
            BindingState = RepositoryBindingBindingState.Bound,
            SensitiveMetadataTier = SensitiveMetadataTier.Tenant_sensitive,
            Freshness = Fresh(),
        };

    private static ProviderSupportEvidenceList SupportEvidence(params ProviderSupportEvidence[] items)
        => new()
        {
            Items = [.. items],
            Page = new PaginationMetadata { Cursor = string.Empty, Limit = 50, IsTruncated = false },
            Freshness = Fresh(),
        };

    private static ProviderSupportEvidence SupportRow(ProviderCapabilityName capability, ProviderCapabilityState state)
        => new()
        {
            CapabilityProfileRef = "cap-profile-1",
            Capability = capability,
            SupportState = state,
        };
}
