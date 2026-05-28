using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// The canonical mixed-surface handoff scenario (Story 5.7) — a single, ordered, immutable sequence of
/// <c>(StepName, OperationId, ExecutingSurface)</c> tuples whose steps are split across REST, SDK, CLI,
/// and MCP against a single in-process host. The scenario asserts the FR51 cross-surface invariants:
/// caller-supplied identity is preserved end-to-end, operation identity pins to its oracle row on every
/// surface, and downstream surfaces see the upstream surfaces' writes.
/// </summary>
/// <remarks>
/// Each <c>OperationId</c> is validated against <see cref="ParityOracle.Rows"/> on first access (mirror
/// of <c>GoldenLifecycle.BuildAndValidate</c>); an unknown operation id throws. Each
/// <c>ExecutingSurface</c> must be in the canonical four-surface vocabulary AND must appear in the oracle
/// row's <c>AdapterExpectations</c>. The handoff scenario stays inside the implemented operation
/// envelope (Story 5.5 Dev Notes "REST surface gap"): mutating →
/// <c>CreateRepositoryBackedFolder</c> / <c>BindRepository</c> / <c>PrepareWorkspace</c> /
/// <c>LockWorkspace</c> / <c>AddFile</c> / <c>ChangeFile</c> / <c>RemoveFile</c> /
/// <c>CommitWorkspace</c> / <c>ArchiveFolder</c>; query → <c>GetWorkspaceStatus</c> /
/// <c>GetFolderLifecycleStatus</c>; context → <c>ListFolderFiles</c>. Reaching domain-terminal states
/// (<c>committed</c>, <c>released</c>) requires worker progression that the in-process host stubs — the
/// scenario asserts transport-terminal classes per step, not domain-terminal reachability.
/// </remarks>
internal static class MixedSurfaceScenario
{
    /// <summary>The canonical four-surface vocabulary the scenario draws from.</summary>
    public static readonly IReadOnlySet<string> SurfaceVocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "rest",
        "sdk",
        "cli",
        "mcp",
    };

    private static readonly Lazy<IReadOnlyList<MixedSurfaceStep>> LazySteps = new(BuildAndValidate);

    /// <summary>Gets the ordered mixed-surface scenario steps.</summary>
    public static IReadOnlyList<MixedSurfaceStep> Steps => LazySteps.Value;

    private static IReadOnlyList<MixedSurfaceStep> BuildAndValidate()
    {
        MixedSurfaceStep[] steps =
        [
            // Mutating step on REST — caller drives the archive through raw HttpClient.
            new("archive_via_rest", "ArchiveFolder", "rest"),

            // Mutating step on SDK — caller drives a second archive (different idempotency key) through
            // the generated IClient. The aggregate treats archive-on-archived idempotently, so the call
            // succeeds and produces a fresh AcceptedCommand response — proving the SDK saw REST's write
            // (the folder is already archived when SDK runs).
            new("archive_via_sdk", "ArchiveFolder", "sdk"),

            // Query step on CLI — caller reads the cumulative archived state through the CLI seam.
            new("status_via_cli", "GetFolderLifecycleStatus", "cli"),

            // Query step on MCP — caller cross-checks the cumulative state through the MCP tool pipeline.
            new("status_via_mcp", "GetFolderLifecycleStatus", "mcp"),
        ];

        HashSet<string> oracleIds = ParityOracle.Rows.Select(row => row.OperationId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> seenStepNames = new(StringComparer.Ordinal);
        foreach (MixedSurfaceStep step in steps)
        {
            if (!seenStepNames.Add(step.StepName))
            {
                throw new InvalidOperationException(
                    $"Mixed-surface scenario contains duplicate step name '{step.StepName}'.");
            }

            if (!oracleIds.Contains(step.OperationId))
            {
                throw new InvalidOperationException(
                    $"Mixed-surface step '{step.StepName}' references unknown oracle operation '{step.OperationId}'.");
            }

            if (!SurfaceVocabulary.Contains(step.ExecutingSurface))
            {
                throw new InvalidOperationException(
                    $"Mixed-surface step '{step.StepName}' executes on unknown surface '{step.ExecutingSurface}'; expected one of [{string.Join(", ", SurfaceVocabulary)}].");
            }

            ParityRow row = ParityOracle.Rows.Single(r => string.Equals(r.OperationId, step.OperationId, StringComparison.Ordinal));
            if (!row.AdapterExpectations.Contains(step.ExecutingSurface, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Mixed-surface step '{step.StepName}' executes on '{step.ExecutingSurface}' but oracle row '{step.OperationId}' does not list that surface in adapter_expectations ([{string.Join(", ", row.AdapterExpectations)}]).");
            }
        }

        return steps;
    }
}

/// <summary>
/// One step in the mixed-surface handoff scenario, pinned to an oracle <c>operation_id</c> and an
/// executing surface in the canonical four-surface set (<c>rest</c> / <c>sdk</c> / <c>cli</c> / <c>mcp</c>).
/// </summary>
/// <param name="StepName">The logical step name (snake_case, unique within the scenario).</param>
/// <param name="OperationId">The canonical oracle operation id this step exercises.</param>
/// <param name="ExecutingSurface">The surface this step is driven through.</param>
internal sealed record MixedSurfaceStep(
    string StepName,
    string OperationId,
    string ExecutingSurface);
