using Bunit;

using Hexalith.Folders.Client.Generated;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using NSubstitute;

// The IClient stub helpers below configure the CancellationToken overloads of the reads (the overloads the
// pages call after Story 6.10) with an Arg.Any<CancellationToken>() matcher. These are substitute
// configuration, not cancellable test operations, so xUnit1051 (pass TestContext.Current.CancellationToken)
// does not apply — mirror the per-page test files that suppress it for the same reason.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.11 — shared happy-path stubs and metadata-only synthetic DTO builders for the consolidated
/// no-mutation (<see cref="NoMutationConsoleSweepTests"/>) and WCAG-2.2-AA structural
/// (<see cref="AccessibilityContractSweepTests"/>) verification sweeps. Every identifier is synthetic
/// (<c>folder-1</c>, <c>workspace-1</c>, …); no real tenant/folder/credential/path/audit data appears.
/// The DTO shapes mirror the established per-page test builders verbatim so the sweeps render the same
/// fully-populated surfaces the per-page tests assert against.
/// </summary>
internal static class ConsoleSweepFixtures
{
    public const string FolderId = "folder-1";
    public const string WorkspaceId = "workspace-1";

    /// <summary>The bare-route incident-stream URL with the folder query the F-6 read path requires.</summary>
    public const string IncidentStreamRoute = "/_admin/incident-stream?folder=folder-1";

    /// <summary>
    /// <see cref="Pages.Home"/> injects an <see cref="IHostEnvironment"/> and gates its dev-gallery links on
    /// <c>IsDevelopment()</c>; <see cref="BadgeRenderingFixture"/> registers none, so the sweep supplies a
    /// Development one (renders the richest read-only surface — all four nav anchors).
    /// </summary>
    public static void AddDevelopmentHostEnvironment(BunitContext ctx)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        ctx.Services.Replace(ServiceDescriptor.Singleton(env));
    }

    // ---------------------------------------------------------------------------------------------------
    // Happy-path IClient stubs — one per SDK-backed page. Each returns the populated primary read so the
    // page renders its full evidence surface (identity, tables, badges, copy affordances, cross-links).
    // ---------------------------------------------------------------------------------------------------

    public static void StubFolderDetail(IClient client)
        => client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());

    public static void StubWorkspace(IClient client)
        => client.GetWorkspaceStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(WorkspaceStatusValue());

    public static void StubAuditTrail(IClient client)
        => client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuditPage(VisibleAuditRecord()));

    public static void StubOperationTimeline(IClient client)
        => client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TimelinePage(VisibleTimelineEntry()));

    public static void StubProvider(IClient client)
    {
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderDiagnostics());
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());
        client.GetProviderBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderBindingValue());
        client.GetRepositoryBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(RepositoryBindingValue());
    }

    public static void StubProviderSupport(IClient client)
        => client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(SupportEvidence(
                SupportRow(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported),
                SupportRow(ProviderCapabilityName.File_operations, ProviderCapabilityState.Unsupported)));

    public static void StubIncidentStream(IClient client)
        => client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TimelinePage(VisibleTimelineEntry()));

    // ---------------------------------------------------------------------------------------------------
    // Redaction-leakage stubs — a redacted sentinel injected through the view models. The redacted value
    // must never reach the rendered DOM (F-5 / UX-DR11 / UX-DR12 defence-in-depth).
    // ---------------------------------------------------------------------------------------------------

    public static void StubAuditTrailWithRedactedActor(IClient client, string sentinel)
    {
        AuditRecord record = VisibleAuditRecord();
        record.ActorReference = Actor(sentinel, RedactionMetadataVisibility.Redacted);
        client.ListAuditTrailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuditPage(record));
    }

    public static void StubOperationTimelineWithRedactedWorkspace(IClient client, string sentinel)
    {
        OperationTimelineEntry entry = VisibleTimelineEntry();
        entry.WorkspaceReference = Workspace(sentinel, RedactionMetadataVisibility.Redacted);
        client.ListOperationTimelineAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(),
                Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TimelinePage(entry));
    }

    public static void StubProviderWithRedactedCredentialReference(IClient client, string sentinel)
    {
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderDiagnostics(new RedactableDiagnosticIdentifier
            {
                Value = sentinel,
                Classification = DiagnosticFieldClassification.Operator_sanitized,
            }));
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle());
        client.GetProviderBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(ProviderBindingValue());
        client.GetRepositoryBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(RepositoryBindingValue());
    }

    // ---------------------------------------------------------------------------------------------------
    // Metadata-only synthetic DTO builders (mirrored from the per-page test files).
    // ---------------------------------------------------------------------------------------------------

    public static FreshnessMetadata Fresh()
        => new()
        {
            Stale = false,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };

    private static FolderLifecycleStatus Lifecycle()
        => new()
        {
            FolderId = FolderId,
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            RepositoryBindingId = "rb-1",
            ProviderBindingRef = "pbr-1",
            Freshness = Fresh(),
        };

    private static WorkspaceStatus WorkspaceStatusValue()
        => new()
        {
            FolderId = FolderId,
            WorkspaceId = WorkspaceId,
            CurrentState = LifecycleState.Ready,
            AcceptedCommandState = new AcceptedCommandState
            {
                TaskId = "task-1",
                OperationId = "op-1",
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

    private static AuditRecord VisibleAuditRecord()
        => new()
        {
            AuditRecordId = "audit-1",
            ActorReference = Actor("actor-1"),
            TaskId = "task-1",
            OperationId = Operation("op-1"),
            CorrelationId = "corr-1",
            ResultStatus = CanonicalErrorCategory.Success,
            SanitizedErrorCategory = CanonicalErrorCategory.Success,
            Retryable = false,
            DurationMilliseconds = 42,
            EvidenceTimestamp = Timestamp(DateTimeOffset.UnixEpoch, RedactableAuditTimestampPrecision.Exact),
            Redaction = Marker(RedactionMetadataVisibility.Metadata_only),
            ChangedPathEvidence = ChangedPath(ChangedPathEvidenceEvidenceKind.Digest, "sha256:abc", DiagnosticFieldClassification.Operator_sanitized),
            Freshness = Fresh(),
        };

    private static RedactableAuditActorReference Actor(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(visibility),
        };

    private static RedactableAuditOperationReference Operation(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = Marker(visibility),
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

    private static OperationTimelineEntry VisibleTimelineEntry()
        => new()
        {
            TimelineEntryId = "entry-1",
            OperationId = "op-1",
            TaskId = "task-1",
            CorrelationId = "corr-1",
            WorkspaceReference = Workspace("workspace-1"),
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

    private static RedactableDiagnosticIdentifier Workspace(string? value, RedactionMetadataVisibility visibility = RedactionMetadataVisibility.Metadata_only)
        => new()
        {
            Value = value,
            Classification = DiagnosticFieldClassification.Operator_sanitized,
            Redaction = new RedactionMetadata { Visibility = visibility, ReasonCode = "none" },
        };

    private static ProviderStatusDiagnostics ProviderDiagnostics(RedactableDiagnosticIdentifier? bindingReference = null)
        => new()
        {
            Status = "ready",
            Disposition = OperatorDispositionLabel.Available,
            Trust = new DiagnosticTrustEvidence { Availability = ProjectionAvailability.Available },
            Freshness = Fresh(),
            ProviderBindingReference = bindingReference ?? new RedactableDiagnosticIdentifier
            {
                Value = "diag-ref-1",
                Classification = DiagnosticFieldClassification.Operator_sanitized,
            },
            ProviderCorrelationReference = "prov-corr-1",
        };

    private static ProviderBinding ProviderBindingValue()
        => new()
        {
            ProviderBindingRef = "pbr-1",
            ProviderFamilyRef = "github",
            CapabilityProfileRef = "cap-1",
            Redaction = ProviderBindingRedaction.Credential_reference_redacted,
            Freshness = Fresh(),
        };

    private static RepositoryBinding RepositoryBindingValue()
        => new()
        {
            RepositoryBindingId = "rb-1",
            FolderId = FolderId,
            ProviderBindingRef = "pbr-1",
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
