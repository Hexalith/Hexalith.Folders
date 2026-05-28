using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// The canonical golden-lifecycle step list (Story 5.5 AC #7) — a single, ordered, immutable sequence of
/// <c>(StepName, operation_id)</c> pairs covering provider readiness → repository binding → prepare → lock
/// → file change → commit → context query → status → audit inspection. The dual-surface end-to-end test
/// (REST + SDK) consumes this list as its authoritative scenario, so a step name change here propagates to
/// both surface runs.
/// </summary>
/// <remarks>
/// Each <c>OperationId</c> is validated against <see cref="ParityOracle.Rows"/> on first access; an unknown
/// operation id throws — drift in the shared step list fails loudly before any surface run.
/// </remarks>
internal static class GoldenLifecycle
{
    private static readonly Lazy<IReadOnlyList<GoldenLifecycleStep>> LazySteps = new(BuildAndValidate);

    /// <summary>Gets the ordered golden-lifecycle steps.</summary>
    public static IReadOnlyList<GoldenLifecycleStep> Steps => LazySteps.Value;

    private static IReadOnlyList<GoldenLifecycleStep> BuildAndValidate()
    {
        GoldenLifecycleStep[] steps =
        [
            new("provider_readiness", "ValidateProviderReadiness"),
            new("repository_binding", "CreateRepositoryBackedFolder"),
            new("prepare_workspace", "PrepareWorkspace"),
            new("lock_workspace", "LockWorkspace"),
            new("add_file", "AddFile"),
            new("commit_workspace", "CommitWorkspace"),
            new("context_query", "ListFolderFiles"),
            new("workspace_status", "GetWorkspaceStatus"),

            // AC #7 audit inspection step. The REST server does not yet implement an audit-family
            // endpoint (ListAuditTrail/GetAuditRecord/ListOperationTimeline/GetOperationTimelineEntry
            // have no /api/v1 route in the current build — see Story 5.5 Dev Notes "REST surface gap").
            // Per the drift-aware reconciliation, BOTH surfaces use GetFolderLifecycleStatus as their
            // in-process inspection step so the dual-surface run produces equivalent transport outcomes
            // against the same host. A future audit endpoint can replace this with ListAuditTrail (and
            // both surfaces will exercise the audit family end-to-end without further test changes).
            new("audit_inspection", OperationId: "GetFolderLifecycleStatus"),
        ];

        HashSet<string> oracleIds = ParityOracle.Rows.Select(row => row.OperationId).ToHashSet(StringComparer.Ordinal);
        foreach (GoldenLifecycleStep step in steps)
        {
            if (!oracleIds.Contains(step.OperationId))
            {
                throw new InvalidOperationException(
                    $"Golden-lifecycle step '{step.StepName}' references unknown oracle operation '{step.OperationId}'.");
            }

            if (step.RestInspectionOperationId is not null && !oracleIds.Contains(step.RestInspectionOperationId))
            {
                throw new InvalidOperationException(
                    $"Golden-lifecycle step '{step.StepName}' references unknown REST inspection operation '{step.RestInspectionOperationId}'.");
            }
        }

        return steps;
    }
}

/// <summary>
/// One step in the golden lifecycle, pinned to an oracle <c>operation_id</c>. The optional
/// <see cref="RestInspectionOperationId"/> lets a step nominate a substitute operation for the REST run
/// where the audit-family operation is not yet implemented as a <c>/api/v1</c> endpoint (see Story 5.5
/// Dev Notes).
/// </summary>
/// <param name="StepName">The logical step name (snake_case, stable across surfaces).</param>
/// <param name="OperationId">The canonical oracle operation id used by the SDK run (and by REST when no substitute is set).</param>
/// <param name="RestInspectionOperationId">When set, the REST run uses this operation id instead of <see cref="OperationId"/>.</param>
internal sealed record GoldenLifecycleStep(
    string StepName,
    string OperationId,
    string? RestInspectionOperationId = null)
{
    /// <summary>Gets the operation id the REST run should execute (the substitute when provided).</summary>
    public string RestOperationId => RestInspectionOperationId ?? OperationId;

    /// <summary>Gets the operation id the SDK run should execute (always the canonical <see cref="OperationId"/>).</summary>
    public string SdkOperationId => OperationId;
}
