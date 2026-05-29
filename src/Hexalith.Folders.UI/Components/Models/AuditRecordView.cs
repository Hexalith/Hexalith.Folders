using System.Globalization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.8 / §3.4 — the assembled, render-ready view-model for a single audit-trail row. A UI-assembly
/// record (not a <c>Hexalith.Folders.Contracts</c> type, not a registered service — the as-built
/// direct-SDK path, mirroring <see cref="ProviderReadinessModel"/>'s assembler shape): the AuditTrail page
/// composes it from each <see cref="AuditRecord"/> the projection returns.
/// </summary>
/// <remarks>
/// Each redactable field's <see cref="FieldDisclosure"/> is pre-resolved here from the SDK redaction
/// metadata, and the field value is carried ONLY when the disclosure is <see cref="FieldDisclosure.Visible"/>
/// (defense in depth — a redacted/unknown/missing value is never even present on this record, so it can
/// never leak to the DOM; redacted ≠ unknown ≠ missing, F-5 / UX-DR10/22). Blank identifiers normalize to
/// <see langword="null"/> and a default/min evidence timestamp degrades to Unknown — never a fabricated
/// <c>0001-01-01</c> (UX-DR26).
/// </remarks>
public sealed record AuditRecordView
{
    /// <summary>Audit record identifier (non-secret; monospace safe-copy for connected evidence, UX-DR19).</summary>
    public required string? AuditRecordId { get; init; }

    /// <summary>Disclosure of the evidence timestamp (precision-gated: redacted/visible/unknown).</summary>
    public required FieldDisclosure TimestampDisclosure { get; init; }

    /// <summary>Formatted evidence timestamp, present only when <see cref="TimestampDisclosure"/> is Visible.</summary>
    public required string? Timestamp { get; init; }

    /// <summary>Disclosure of the actor reference (redaction-gated).</summary>
    public required FieldDisclosure ActorDisclosure { get; init; }

    /// <summary>Actor reference value, present only when <see cref="ActorDisclosure"/> is Visible.</summary>
    public required string? Actor { get; init; }

    /// <summary>Disclosure of the operation reference (redaction-gated).</summary>
    public required FieldDisclosure OperationDisclosure { get; init; }

    /// <summary>Operation reference value, present only when <see cref="OperationDisclosure"/> is Visible.</summary>
    public required string? Operation { get; init; }

    /// <summary>Task identifier (non-secret; nullable — a missing task renders an honest Missing affordance).</summary>
    public required string? TaskId { get; init; }

    /// <summary>Correlation identifier (non-secret; monospace safe-copy).</summary>
    public required string? CorrelationId { get; init; }

    /// <summary>Canonical result status of the audited operation (operator label).</summary>
    public required CanonicalErrorCategory ResultStatus { get; init; }

    /// <summary>Sanitized error category (operator label; never raw provider text).</summary>
    public required CanonicalErrorCategory SanitizedErrorCategory { get; init; }

    /// <summary>Advisory retryable posture — display only, never an action affordance (F-2).</summary>
    public required bool Retryable { get; init; }

    /// <summary>Operation duration in milliseconds.</summary>
    public required int DurationMilliseconds { get; init; }

    /// <summary>Disclosure of the changed-path evidence (classification-gated).</summary>
    public required FieldDisclosure ChangedPathDisclosure { get; init; }

    /// <summary>Kind of changed-path evidence (digest/reference/redacted/unavailable); null when not recorded.</summary>
    public required ChangedPathEvidenceEvidenceKind? ChangedPathKind { get; init; }

    /// <summary>Metadata-only digest/reference, present only when <see cref="ChangedPathDisclosure"/> is Visible (never a raw path).</summary>
    public required string? ChangedPathValue { get; init; }

    /// <summary>Per-record redaction marker disclosure (metadata-only vs redacted-by-policy).</summary>
    public required FieldDisclosure RecordRedactionDisclosure { get; init; }

    /// <summary>Assembles the render-ready row from a single SDK audit record.</summary>
    public static AuditRecordView Create(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        (FieldDisclosure timestampDisclosure, string? timestamp) = ResolveTimestamp(record.EvidenceTimestamp);

        FieldDisclosure actorDisclosure =
            RedactionDisclosureMapper.FromAuditRedaction(record.ActorReference.Redaction, record.ActorReference.Value);
        FieldDisclosure operationDisclosure =
            RedactionDisclosureMapper.FromAuditRedaction(record.OperationId.Redaction, record.OperationId.Value);

        (FieldDisclosure changedPathDisclosure, ChangedPathEvidenceEvidenceKind? changedPathKind, string? changedPathValue) =
            ResolveChangedPath(record.ChangedPathEvidence);

        return new AuditRecordView
        {
            AuditRecordId = NullIfBlank(record.AuditRecordId),
            TimestampDisclosure = timestampDisclosure,
            Timestamp = timestamp,
            ActorDisclosure = actorDisclosure,
            Actor = actorDisclosure == FieldDisclosure.Visible ? NullIfBlank(record.ActorReference.Value) : null,
            OperationDisclosure = operationDisclosure,
            Operation = operationDisclosure == FieldDisclosure.Visible ? NullIfBlank(record.OperationId.Value) : null,
            TaskId = NullIfBlank(record.TaskId),
            CorrelationId = NullIfBlank(record.CorrelationId),
            ResultStatus = record.ResultStatus,
            SanitizedErrorCategory = record.SanitizedErrorCategory,
            Retryable = record.Retryable,
            DurationMilliseconds = record.DurationMilliseconds,
            ChangedPathDisclosure = changedPathDisclosure,
            ChangedPathKind = changedPathKind,
            ChangedPathValue = changedPathValue,
            RecordRedactionDisclosure = RedactionDisclosureMapper.FromAuditVisibility(record.Redaction.Visibility),
        };
    }

    private static (FieldDisclosure Disclosure, string? Value) ResolveTimestamp(RedactableAuditTimestamp timestamp)
    {
        // Precision drives redaction; an exact/bucketed timestamp with no real value degrades to Unknown
        // rather than rendering a fabricated 0001-01-01 (UX-DR26 / AC #10).
        FieldDisclosure precision = RedactionDisclosureMapper.FromTimestampPrecision(timestamp.Precision);
        if (precision == FieldDisclosure.Redacted)
        {
            return (FieldDisclosure.Redacted, null);
        }

        return timestamp.Value != default
            ? (FieldDisclosure.Visible, timestamp.Value.ToString("u", CultureInfo.InvariantCulture))
            : (FieldDisclosure.Unknown, null);
    }

    private static (FieldDisclosure Disclosure, ChangedPathEvidenceEvidenceKind? Kind, string? Value) ResolveChangedPath(
        ChangedPathEvidence2? evidence)
    {
        if (evidence is null)
        {
            // No changed-path evidence recorded for this audit record (AC #3 — render Missing, never blank).
            return (FieldDisclosure.Missing, null, null);
        }

        // Metadata-only: surface the digest or opaque reference, never a raw path or content (S-6 / #11).
        string? value = NullIfBlank(evidence.Digest) ?? NullIfBlank(evidence.Reference);

        // EvidenceKind is authoritative for the redacted/unavailable cases: the SDK schema guarantees the
        // digest/reference are absent then, so a classification-only disclosure would mislabel the affordance
        // (e.g. an operator_sanitized "redacted" kind would degrade to Missing/"Not recorded" and contradict
        // the "Redacted" kind label, AC #8). Redacted → the policy-hidden lock (F-5); Unavailable → an honest
        // Unknown; the value-bearing digest/reference kinds keep the classification-gated disclosure.
        FieldDisclosure disclosure = evidence.EvidenceKind switch
        {
            ChangedPathEvidenceEvidenceKind.Redacted => FieldDisclosure.Redacted,
            ChangedPathEvidenceEvidenceKind.Unavailable => FieldDisclosure.Unknown,
            _ => RedactionDisclosureMapper.FromDiagnosticClassification(evidence.Classification, value is not null),
        };

        // Defense in depth: a non-Visible disclosure never carries the digest/reference value.
        return (disclosure, evidence.EvidenceKind, disclosure == FieldDisclosure.Visible ? value : null);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
