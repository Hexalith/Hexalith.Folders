using System.Globalization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.8 / §3.4 — the assembled, render-ready view-model for a single operation-timeline entry (the
/// architecture-sanctioned "Diagnostic Timeline", UX-DR8). A UI-assembly record (not a Contracts type, not
/// a registered service — mirrors <see cref="ProviderReadinessModel"/>'s assembler shape) composed from
/// each <see cref="OperationTimelineEntry"/> the projection returns.
/// </summary>
/// <remarks>
/// The state transition's operator <see cref="Disposition"/> is the PRIMARY visual (F-4) and is the
/// server-computed value passed straight through — never re-derived from <see cref="ToState"/>. The only
/// redactable field is the workspace reference, whose <see cref="FieldDisclosure"/> is pre-resolved and
/// whose value is carried only when Visible (redacted ≠ unknown ≠ missing, F-5). The timeline evidence
/// timestamp is a plain <see cref="DateTimeOffset"/> formatted directly; a default/min value renders
/// "unknown", never a fabricated <c>0001-01-01</c> (UX-DR26).
/// </remarks>
public sealed record OperationTimelineEntryView
{
    /// <summary>Timeline entry identifier (non-secret; monospace safe-copy for connected evidence).</summary>
    public required string? TimelineEntryId { get; init; }

    /// <summary>Formatted evidence timestamp ("unknown" when the projection recorded no real time).</summary>
    public required string Timestamp { get; init; }

    /// <summary>Operation identifier (non-secret; monospace safe-copy).</summary>
    public required string? OperationId { get; init; }

    /// <summary>Task identifier (non-secret; monospace safe-copy).</summary>
    public required string? TaskId { get; init; }

    /// <summary>Correlation identifier (non-secret; monospace safe-copy).</summary>
    public required string? CorrelationId { get; init; }

    /// <summary>Disclosure of the workspace reference (redaction-gated).</summary>
    public required FieldDisclosure WorkspaceDisclosure { get; init; }

    /// <summary>Workspace reference value, present only when <see cref="WorkspaceDisclosure"/> is Visible.</summary>
    public required string? Workspace { get; init; }

    /// <summary>Lifecycle state transitioned FROM (technical metadata, secondary).</summary>
    public required LifecycleState FromState { get; init; }

    /// <summary>Lifecycle state transitioned TO (technical metadata, secondary).</summary>
    public required LifecycleState ToState { get; init; }

    /// <summary>Server-computed operator disposition — the PRIMARY status visual (F-4), passed through verbatim.</summary>
    public required OperatorDispositionLabel Disposition { get; init; }

    /// <summary>Sanitized canonical result of the operation (operator label; never raw provider text).</summary>
    public required CanonicalErrorCategory SanitizedResult { get; init; }

    /// <summary>Advisory retryable posture — display only, never an action affordance (F-2).</summary>
    public required bool Retryable { get; init; }

    /// <summary>Operation duration in milliseconds.</summary>
    public required int DurationMilliseconds { get; init; }

    /// <summary>Assembles the render-ready entry from a single SDK timeline entry.</summary>
    public static OperationTimelineEntryView Create(OperationTimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        FieldDisclosure workspaceDisclosure =
            RedactionDisclosureMapper.FromAuditRedaction(entry.WorkspaceReference.Redaction, entry.WorkspaceReference.Value);

        return new OperationTimelineEntryView
        {
            TimelineEntryId = NullIfBlank(entry.TimelineEntryId),
            Timestamp = FormatTimestamp(entry.EvidenceTimestamp),
            OperationId = NullIfBlank(entry.OperationId),
            TaskId = NullIfBlank(entry.TaskId),
            CorrelationId = NullIfBlank(entry.CorrelationId),
            WorkspaceDisclosure = workspaceDisclosure,
            Workspace = workspaceDisclosure == FieldDisclosure.Visible ? NullIfBlank(entry.WorkspaceReference.Value) : null,
            FromState = entry.StateTransition.FromState,
            ToState = entry.StateTransition.ToState,
            Disposition = entry.StateTransition.Disposition,
            SanitizedResult = entry.SanitizedResult,
            Retryable = entry.Retryable,
            DurationMilliseconds = entry.DurationMilliseconds,
        };
    }

    private static string FormatTimestamp(DateTimeOffset value)
        => value != default ? value.ToString("u", CultureInfo.InvariantCulture) : "unknown";

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
