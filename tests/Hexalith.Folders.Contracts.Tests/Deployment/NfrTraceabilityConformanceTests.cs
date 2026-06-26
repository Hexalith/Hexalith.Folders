using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Static conformance gate for the Story 7.16 NFR traceability bridge. Every inventory is re-derived from the
/// authoritative source — the PRD <c>## Non-Functional Requirements</c> bullets, the <c>epics.md</c> numbered
/// <c>NFR1</c>..<c>NFR70</c> list, the <c>C0-C13</c> governance evidence, the cited gate / exit-criteria /
/// release-validation artifacts, and the release-package wiring — and asserted equal to the published
/// <c>docs/exit-criteria/nfr-traceability.md</c> table with exact cardinality, so the bridge cannot silently
/// drift. All assertions route through the same parsers and scanners the negative controls exercise.
/// </summary>
public sealed partial class NfrTraceabilityConformanceTests
{
    private const string PrdPath = "_bmad-output/planning-artifacts/prd.md";
    private const string EpicsPath = "_bmad-output/planning-artifacts/epics.md";
    private const string GovernancePath = "docs/exit-criteria/c0-c13-governance-evidence.yaml";
    private const string DocPath = "docs/exit-criteria/nfr-traceability.md";
    private const string GateScriptPath = "tests/tools/run-nfr-traceability-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string CiWorkflowPath = ".github/workflows/ci.yml";
    private const string NightlyDriftWorkflowPath = ".github/workflows/nightly-drift.yml";
    private const string PolicyConformanceWorkflowPath = ".github/workflows/policy-conformance.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string ReleaseGatePath = "tests/tools/run-release-package-gates.ps1";
    private const string ReleaseWorkflowPath = ".github/workflows/release-packages.yml";
    private const string ReportPath = "_bmad-output/gates/nfr-traceability/latest.json";
    private const string TestSourcePath = "tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs";

    private const string ConformanceFqn = "Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests";

    private const string TableMarker = "nfr-traceability-table";
    private const string CategoryMarker = "nfr-category-rollup";
    private const string BddMarker = "nfr-bdd-evidence-rollup";

    private static readonly string[] AllowedPlaceholderHostSuffixes =
        [".invalid", ".internal", ".example", ".localhost", ".test"];

    private static readonly string[] AllowedStatuses = ["covered", "release-validation", "reference-pending"];

    private static readonly string SubmoduleCommand =
        "git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants";

    private static readonly (int Lo, int Hi, string Name)[] Categories =
    [
        (1, 10, "Security & Tenant Isolation"),
        (11, 20, "Reliability, Idempotency & Failure Visibility"),
        (21, 31, "Performance & Query Bounds"),
        (32, 36, "Scalability & Capacity"),
        (37, 46, "Integration & Contract Compatibility"),
        (47, 55, "Observability, Auditability & Replay"),
        (56, 61, "Data Retention & Cleanup"),
        (62, 66, "Operations-Console Accessibility"),
        (67, 70, "Verification Expectations"),
    ];

    private static readonly string[] BddEvidenceClasses =
    [
        "tenant-isolation-security-gates",
        "audit-completeness",
        "workspace-and-context-query-performance-baselines",
        "cli-mcp-smoke-parity",
        "console-accessibility-responsive-validation",
        "operational-runbook-proof",
    ];

    // The gate report surfaces[] must stay bounded to exactly these diagnostic surfaces (AC11): neither
    // truncated (a dropped surface) nor unbounded (a new surface leaking unintended diagnostics).
    private static readonly string[] ExpectedReportSurfaces =
    [
        "prd-nfr-inventory",
        "epics-nfr-inventory",
        "traceability-table",
        "category-rollup",
        "bdd-evidence-rollup",
        "release-evidence",
        "reference-pending-gaps",
        "ci-wiring",
        "release-wiring",
        "metadata-only",
    ];

    private sealed record TraceRow(
        string Id,
        string Category,
        string Hash,
        string Status,
        string Stories,
        string Gates,
        string Exit,
        string Release,
        string Owner,
        string Note);

    [Fact]
    public void NfrTraceabilityDocExists() => AssertDocExists(DocPath);

    [Fact]
    public void NfrTraceabilityDocNamesItsSourceAuthorities()
    {
        // AC1: the bridge must identify the PRD NFR section, the epics NFR inventory, the C0-C13 governance
        // evidence, the Epic 7 gate reports, and the exit-criteria/release-validation artifacts as the source
        // authorities. A change that replaces those explicit references with vague prose must fail here.
        string doc = ReadText(DocPath);
        foreach (string authority in new[]
        {
            PrdPath,
            EpicsPath,
            "c0-c13-governance-evidence.yaml",
            "_bmad-output/gates/",
            "docs/exit-criteria/",
        })
        {
            doc.ShouldContain(authority, Case.Sensitive,
                $"the NFR bridge must name its source authority '{authority}'.");
        }
    }

    [Fact]
    public void PrdAndEpicsNfrInventoriesAlignOneForOne()
    {
        List<string> prd = ParsePrdNfrBullets();
        prd.Count.ShouldBe(70, "the PRD Non-Functional Requirements section must declare exactly 70 bullets.");

        Dictionary<int, string> epics = ParseEpicsNfrInventory();
        epics.Keys.OrderBy(static n => n).ShouldBe(Enumerable.Range(1, 70), "epics.md must declare NFR1..NFR70.");

        for (int n = 1; n <= 70; n++)
        {
            epics[n].ShouldBe(prd[n - 1], $"PRD bullet {n} and epics.md NFR{n} must be identical (one-for-one).");
        }
    }

    [Fact]
    public void TraceabilityTableHasSeventyRowsMatchingPrdHashes()
    {
        List<TraceRow> rows = ParseTraceRows();
        rows.Count.ShouldBe(70, "the traceability table must contain exactly 70 NFR rows.");

        AssertRowIdsAreNfr1To70(rows.Select(static r => r.Id).ToList());

        List<string> prd = ParsePrdNfrBullets();
        for (int n = 1; n <= 70; n++)
        {
            TraceRow row = rows[n - 1];
            row.Id.ShouldBe($"NFR{n}", "rows must be ordered NFR1..NFR70.");
            TwelveHexHash().IsMatch(row.Hash).ShouldBeTrue($"NFR{n} hash must be 12 lowercase hex chars.");
            row.Hash.ShouldBe(StableHash(prd[n - 1]), $"NFR{n} PRD bullet hash must equal the derived hash from prd.md.");
        }
    }

    [Fact]
    public void TraceabilityTableRowsCarryCategoryStatusAndConcreteEvidence()
    {
        List<TraceRow> rows = ParseTraceRows();
        HashSet<string> knownCriteria = ParseGovernanceCriteria().Keys.ToHashSet(StringComparer.Ordinal);

        for (int n = 1; n <= 70; n++)
        {
            TraceRow row = rows[n - 1];
            row.Category.ShouldBe(CategoryForIndex(n), $"NFR{n} must be in its PRD/architecture category.");
            AllowedStatuses.ShouldContain(row.Status, $"NFR{n} status must be a bounded evidence status.");
            row.Owner.ShouldNotBeNullOrWhiteSpace();
            row.Owner.ShouldNotBe("—", $"NFR{n} must name an owner.");
            row.Note.ShouldNotBeNullOrWhiteSpace();

            AssertEvidenceTokensResolve(row, knownCriteria);
            AssertRowHasConcreteEvidence(row, knownCriteria);

            if (row.Status is "covered" or "release-validation")
            {
                EvidenceTokens(row).Any(token => token.Contains('/', StringComparison.Ordinal) && RepositoryFileExists(token))
                    .ShouldBeTrue($"NFR{n} ({row.Status}) must cite at least one existing evidence file path.");
            }
        }
    }

    [Fact]
    public void ReferencePendingRowsAreOwnedAndSurfaceKnownGaps()
    {
        List<TraceRow> pending = ParseTraceRows().Where(static r => r.Status == "reference-pending").ToList();
        pending.Count.ShouldBeGreaterThan(0, "the bridge must surface at least one owned reference-pending gap.");

        foreach (TraceRow row in pending)
        {
            AssertReferencePendingOwned(row);
        }

        HashSet<string> pendingCriteria = pending
            .SelectMany(static r => BacktickTokens(r.Exit))
            .Where(static t => CriterionId().IsMatch(t))
            .ToHashSet(StringComparer.Ordinal);
        foreach (string criterion in new[] { "C3", "C4", "C7", "C12" })
        {
            pendingCriteria.ShouldContain(criterion, $"{criterion} must stay surfaced as a reference-pending gap.");
        }

        HashSet<string> pendingStories = pending
            .SelectMany(static r => BacktickTokens(r.Stories))
            .ToHashSet(StringComparer.Ordinal);
        pendingStories.ShouldContain("7-17", "Story 7.17 alerting/backup/runbook gaps must stay surfaced.");
    }

    [Fact]
    public void NineCategoryRollupCoversAllSeventyNfrs()
    {
        List<string[]> rollup = ParsePipeRows(ExtractMarkerBlock(ReadText(DocPath), CategoryMarker))
            .Where(static cells => cells.Length == 4 && cells[0] != "Category" && !IsSeparator(cells))
            .ToList();
        rollup.Count.ShouldBe(9, "the rollup must carry exactly nine category rows.");

        Dictionary<string, int> tableCounts = ParseTraceRows()
            .GroupBy(static r => r.Category, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

        rollup.Select(static cells => cells[0]).OrderBy(static c => c, StringComparer.Ordinal)
            .ShouldBe(Categories.Select(static c => c.Name).OrderBy(static c => c, StringComparer.Ordinal),
                "rollup categories must be exactly the nine PRD/architecture categories.");

        int total = 0;
        foreach (string[] cells in rollup)
        {
            int count = int.Parse(cells[2], System.Globalization.CultureInfo.InvariantCulture);
            count.ShouldBe(tableCounts[cells[0]], $"category '{cells[0]}' count must equal its table rows.");
            total += count;
            BacktickTokens(cells[3]).Any(RepositoryFileExists)
                .ShouldBeTrue($"category '{cells[0]}' must cite at least one existing evidence path.");
        }

        total.ShouldBe(70, "the nine category counts must sum to the full 70-NFR inventory.");
    }

    [Fact]
    public void BddRequiredEvidenceClassesArePresent()
    {
        List<string[]> bdd = ParsePipeRows(ExtractMarkerBlock(ReadText(DocPath), BddMarker))
            .Where(static cells => cells.Length == 3 && cells[0] != "Evidence class" && !IsSeparator(cells))
            .ToList();

        bdd.Select(static cells => FirstBacktickToken(cells[0])).OrderBy(static c => c, StringComparer.Ordinal)
            .ShouldBe(BddEvidenceClasses.OrderBy(static c => c, StringComparer.Ordinal),
                "the six BDD-required release-review evidence classes must all be present exactly once.");

        foreach (string[] cells in bdd)
        {
            BacktickTokens(cells[2]).Any(RepositoryFileExists)
                .ShouldBeTrue($"BDD evidence class '{cells[0]}' must cite at least one existing path.");
        }
    }

    [Fact]
    public void NfrTraceabilityDocStaysMetadataOnlyWithOperatorBoilerplate()
    {
        string doc = ReadText(DocPath);
        AssertDocMetadataOnly(doc);
        AssertNoRecursiveSubmoduleCommand(doc);

        foreach (string required in new[]
        {
            "pwsh ./tests/tools/run-nfr-traceability-gates.ps1",
            ReportPath,
            "metadata-only",
            "reviewer",
            "rerun",
            SubmoduleCommand,
        })
        {
            doc.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void GovernanceEvidenceReferencePendingCriteriaStaySurfaced()
    {
        HashSet<string> governancePending = ParseGovernanceCriteria()
            .Where(static kvp => kvp.Value == "reference_pending")
            .Select(static kvp => kvp.Key)
            .ToHashSet(StringComparer.Ordinal);
        governancePending.Count.ShouldBeGreaterThan(0, "governance must declare reference-pending criteria to surface.");

        HashSet<string> docPendingCriteria = ParseTraceRows()
            .Where(static r => r.Status == "reference-pending")
            .SelectMany(static r => BacktickTokens(r.Exit))
            .Where(static t => CriterionId().IsMatch(t))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string criterion in governancePending)
        {
            docPendingCriteria.ShouldContain(criterion,
                $"governance reference-pending {criterion} must stay visible in the traceability bridge.");
        }
    }

    [Fact]
    public void NfrTraceabilityGateScriptFailsClosedAndEmitsBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);
        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            // AC11: repository-root resolution from the script path (mirrors the Story 7.13-7.15 posture),
            // never a hard-coded or host-absolute root.
            "Split-Path -Parent $MyInvocation.MyCommand.Path",
            "Resolve-Path",
            "Join-Path $toolsParent",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "diagnostic_policy",
            "metadata-only",
            "Push-Location",
            "Pop-Location",
            "GATE-VACUOUS",
            "xunit",
            "source_commit",
            "release_blocking_gaps",
            $"FullyQualifiedName~{ConformanceFqn}",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        AssertNoRecursiveSubmoduleCommand(script);

        // $runnerMethods must be exactly the [Fact] set, so the vacuous-test guard tracks the real fact count.
        HashSet<string> factMethods = ParseTestFactFqns();
        HashSet<string> runnerMethods = RunnerMethodEntry().Matches(script)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        runnerMethods.OrderBy(static m => m, StringComparer.Ordinal)
            .ShouldBe(factMethods.OrderBy(static m => m, StringComparer.Ordinal),
                "the gate script $runnerMethods must equal the NfrTraceabilityConformanceTests [Fact] set exactly.");
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiWireNfrTraceabilityGate()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-nfr-traceability-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        AssertNoRecursiveSubmoduleCommand(workflow);

        // The new step must follow the provider-error-docs step, not broaden a new lane.
        int providerStep = workflow.IndexOf("run-provider-error-docs-gates.ps1", StringComparison.Ordinal);
        int nfrStep = workflow.IndexOf("run-nfr-traceability-gates.ps1", StringComparison.Ordinal);
        providerStep.ShouldBeGreaterThanOrEqualTo(0, "the provider-error-docs step must remain wired.");
        nfrStep.ShouldBeGreaterThan(providerStep, "the nfr-traceability step must come after provider-error-docs.");

        // Lane separation (AC12): the static gate belongs to contract-spine, never to the PR ci.yml lane nor
        // to the scheduled / policy workflows. Each must stay free of the focused gate.
        foreach (string isolatedLane in new[] { CiWorkflowPath, NightlyDriftWorkflowPath, PolicyConformanceWorkflowPath })
        {
            ReadText(isolatedLane).ShouldNotContain("run-nfr-traceability-gates.ps1", Case.Sensitive,
                $"{isolatedLane} must not run the focused NFR traceability gate (lane separation).");
        }

        ReadText(BaselineGatePath).ShouldContain(ConformanceFqn, Case.Sensitive);
    }

    [Fact]
    public void ReleasePackageWiringRequiresNfrTraceabilityEvidence()
    {
        // The static gate runs as a release-readiness prerequisite so the committed report is current.
        ReadText(ReleaseWorkflowPath).ShouldContain("./tests/tools/run-nfr-traceability-gates.ps1", Case.Sensitive);

        string releaseGate = ReadText(ReleaseGatePath);
        foreach (string required in new[]
        {
            ReportPath,
            "release_blocking_gaps",
            "nfr-traceability-unowned-release-blocking-gap",
            "stale-nfr-traceability-evidence",
        })
        {
            releaseGate.ShouldContain(required, Case.Sensitive);
        }

        // The evidence path must sit inside the release evidence set so it cannot be dropped silently.
        int evidenceArray = releaseGate.IndexOf("$evidencePaths", StringComparison.Ordinal);
        evidenceArray.ShouldBeGreaterThanOrEqualTo(0, "the release gate must declare an evidence-path set.");
        int pathInArray = releaseGate.IndexOf($"'{ReportPath}'", evidenceArray, StringComparison.Ordinal);
        pathInArray.ShouldBeGreaterThan(evidenceArray, "the nfr-traceability report must be a required release-evidence path.");

        // AC13: live publish must fail-close on stale same-commit NFR evidence. Assert the guarding condition is
        // structurally present inside the NFR-scoped block and precedes the Fail-Gate reason, so the staleness
        // check cannot be neutered while leaving the reason string behind.
        int nfrScopeStart = releaseGate.IndexOf(
            "$relativePath -eq '_bmad-output/gates/nfr-traceability/latest.json'", StringComparison.Ordinal);
        nfrScopeStart.ShouldBeGreaterThan(0, "the release gate must scope NFR-specific checks to the nfr-traceability report.");
        string nfrScope = releaseGate[nfrScopeStart..];
        Match staleGuard = NfrStaleSameCommitGuard().Match(nfrScope);
        staleGuard.Success.ShouldBeTrue(
            "live Publish must guard the stale-evidence failure on Mode=Publish and a same-commit source_commit check.");
        int staleReason = nfrScope.IndexOf("stale-nfr-traceability-evidence", StringComparison.Ordinal);
        staleReason.ShouldBeGreaterThan(staleGuard.Index,
            "the stale-evidence Fail-Gate must be guarded by the same-commit staleness condition.");
    }

    [Fact]
    public void NfrTraceabilityGateRunsOnlyInReleasePrerequisiteJob()
    {
        // AC13: the focused gate is wired as a release *prerequisite*. It must not be silently relocated into the
        // package-conformance or publish jobs, which would let publish proceed without the prerequisite gate.
        YamlMappingNode root = LoadSingleYamlDocument(ReleaseWorkflowPath).ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode jobs = root.Children[new YamlScalarNode("jobs")].ShouldBeOfType<YamlMappingNode>();

        JobRunCommands(jobs, "release-prerequisite-gates")
            .ShouldContain("run-nfr-traceability-gates.ps1", Case.Sensitive,
                "the NFR traceability gate must run in the release-prerequisite-gates job.");
        JobRunCommands(jobs, "release-package-conformance")
            .ShouldNotContain("run-nfr-traceability-gates.ps1", Case.Sensitive,
                "the NFR traceability gate must not be relocated into the release-package-conformance job.");
        JobRunCommands(jobs, "publish-packages")
            .ShouldNotContain("run-nfr-traceability-gates.ps1", Case.Sensitive,
                "the NFR traceability gate must not be relocated into the publish-packages job.");
    }

    [Fact]
    public void NfrTraceabilityLatestReportStaysMetadataOnlyAndMatchesDoc()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("nfr-traceability");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        root.GetProperty("nfr_total").GetInt32().ShouldBe(70);
        root.GetProperty("category_total").GetInt32().ShouldBe(9);
        AssertMetadataOnlyJson(root);

        // AC11: the report surfaces[] must stay bounded to exactly the intended diagnostic surfaces — a dropped
        // surface (truncation) or an extra surface (unbounded diagnostics) must fail closed.
        JsonElement surfaces = root.GetProperty("surfaces");
        surfaces.ValueKind.ShouldBe(JsonValueKind.Array, "the report surfaces must be a bounded array.");
        surfaces.EnumerateArray().Select(static e => e.GetString().ShouldNotBeNull())
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(ExpectedReportSurfaces.OrderBy(static s => s, StringComparer.Ordinal),
                "the gate report surfaces[] must be exactly the bounded surface set.");

        string sourceCommit = RequiredString(root, "source_commit");
        (sourceCommit == "NO_VCS" || FortyHex().IsMatch(sourceCommit))
            .ShouldBeTrue("source_commit must be a full SHA or NO_VCS.");

        // The report's release-blocking gaps must be owned and match the doc's reference-pending rows.
        JsonElement gaps = root.GetProperty("release_blocking_gaps");
        gaps.ValueKind.ShouldBe(JsonValueKind.Array);
        HashSet<string> reportGapNfrs = new(StringComparer.Ordinal);
        foreach (JsonElement gap in gaps.EnumerateArray())
        {
            RequiredString(gap, "owner").ShouldNotBeNullOrWhiteSpace();
            reportGapNfrs.Add(RequiredString(gap, "nfr"));
        }

        HashSet<string> docPendingNfrs = ParseTraceRows()
            .Where(static r => r.Status == "reference-pending")
            .Select(static r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        reportGapNfrs.OrderBy(static n => n, StringComparer.Ordinal)
            .ShouldBe(docPendingNfrs.OrderBy(static n => n, StringComparer.Ordinal),
                "the report release_blocking_gaps must match the doc reference-pending rows.");
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeNfrTraceabilityEvidence()
    {
        HashSet<string> knownCriteria = ParseGovernanceCriteria().Keys.ToHashSet(StringComparer.Ordinal);
        List<string> prd = ParsePrdNfrBullets();

        // 1. Missing doc must fail the existence assertion.
        Should.Throw<ShouldAssertException>(() => AssertDocExists("docs/exit-criteria/this-doc-does-not-exist.md"));

        // 2. A tampered PRD bullet must produce a different stable hash than the published row.
        string realHash = StableHash(prd[0]);
        StableHash(prd[0] + " tampered").ShouldNotBe(realHash);
        Should.Throw<ShouldAssertException>(() => realHash.ShouldBe(StableHash(prd[0] + " tampered")));

        // 2b. A trace row whose published hash field is tampered must fail the SAME production comparison the
        //     table fact uses (row.Hash.ShouldBe(StableHash(prd[n-1]))) — not merely the hash function in isolation.
        TraceRow tamperedHashRow = new("NFR1", "Security & Tenant Isolation", "deadbeefcafe", "covered",
            "`7-6`", "`tests/tools/run-safety-invariant-gates.ps1`", "`C8`", "—", "Safety Invariants", "note");
        Should.Throw<ShouldAssertException>(() => tamperedHashRow.Hash.ShouldBe(StableHash(prd[0])));

        // 3. A missing NFR row and a duplicate row must each fail the inventory check, through the real checker.
        List<string> ids = Enumerable.Range(1, 70).Select(static n => $"NFR{n}").ToList();
        List<string> missingRow = ids.Where(static id => id != "NFR42").ToList();
        Should.Throw<ShouldAssertException>(() => AssertRowIdsAreNfr1To70(missingRow));
        List<string> duplicateRow = new(ids) { "NFR1" };
        Should.Throw<ShouldAssertException>(() => AssertRowIdsAreNfr1To70(duplicateRow));

        // 4. An unmapped row (no concrete evidence) must fail the same concrete-evidence checker.
        TraceRow unmapped = new("NFR1", "Security & Tenant Isolation", "deadbeefdead", "covered",
            "`7-6`", "—", "—", "—", "Owner", "note");
        Should.Throw<ShouldAssertException>(() => AssertRowHasConcreteEvidence(unmapped, knownCriteria));

        // 5. An unowned reference-pending gap must fail the ownership checker.
        TraceRow unowned = new("NFR18", "Reliability, Idempotency & Failure Visibility", "deadbeefdead",
            "reference-pending", "`4-3`", "`tests/tools/run-governance-completeness-gates.ps1`", "`C7`", "—", "—",
            "no release-blocking note");
        Should.Throw<ShouldAssertException>(() => AssertReferencePendingOwned(unowned));

        // 6. Leaked absolute paths, bearer/JWT tokens, and non-placeholder hosts must each fail the same scanner.
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("evidence at /home/runner/work/leaked.txt"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhY3RvciJ9.signaturesegment"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("status dashboard https://folders.example-real.com/api"));
        Should.NotThrow(() => AssertDocMetadataOnly("status dashboard https://folders.localhost.test and https://localhost:17000"));

        // 7. Malformed JSON, malformed YAML, and a markdown table with no rows must fail rather than pass vacuously.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"nfr-traceability\", "));
        Should.Throw<YamlDotNet.Core.YamlException>(() =>
        {
            using StringReader reader = new("- a: 1\n  b: : :\n :\n");
            YamlStream stream = new();
            stream.Load(reader);
        });
        Should.Throw<ShouldAssertException>(() => ParsePipeRows("no pipe table here")
            .Count.ShouldBeGreaterThan(0, "a markdown table with no rows must not pass vacuously."));

        // 7b. A trace-table row with the wrong column count must NOT be accepted as a valid 10-column row through
        //     the same predicate ParseTraceRows uses, so a dropped/extra column surfaces as an inventory-count
        //     failure rather than being silently filtered into a passing table.
        const string wrongColumnTable =
            "| NFR | Category | PRD bullet hash |\n| --- | --- | --- |\n| NFR1 | Security & Tenant Isolation | f33274b9da03 |\n";
        ParsePipeRows(wrongColumnTable)
            .Count(static cells => cells.Length == 10 && NfrId().IsMatch(cells[0]))
            .ShouldBe(0, "a row with the wrong column count must not be accepted as a 10-column trace row.");

        // 8. The forbidden recursive submodule command is detected; the exact root-level command is not.
        Should.Throw<ShouldAssertException>(() => AssertNoRecursiveSubmoduleCommand("git submodule update --init " + string.Concat("--", "recursive")));
        Should.NotThrow(() => AssertNoRecursiveSubmoduleCommand(SubmoduleCommand));
    }

    // ---- parsers -----------------------------------------------------------------------------------------

    private static List<string> ParsePrdNfrBullets()
    {
        string doc = ReadText(PrdPath);
        int start = doc.IndexOf("## Non-Functional Requirements", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, "prd.md must declare a Non-Functional Requirements section.");
        string rest = doc[(start + "## Non-Functional Requirements".Length)..];
        Match next = NextLevelTwoHeader().Match(rest);
        string block = next.Success ? rest[..next.Index] : rest;

        return BulletLine().Matches(block)
            .Select(static match => match.Groups[1].Value.Trim())
            .ToList();
    }

    private static Dictionary<int, string> ParseEpicsNfrInventory()
    {
        string doc = ReadText(EpicsPath);
        int start = doc.IndexOf("### NonFunctional Requirements", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, "epics.md must declare a NonFunctional Requirements section.");
        string rest = doc[(start + "### NonFunctional Requirements".Length)..];
        Match next = NextLevelThreeHeader().Match(rest);
        string block = next.Success ? rest[..next.Index] : rest;

        Dictionary<int, string> items = [];
        foreach (Match match in NumberedNfrLine().Matches(block))
        {
            items[int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]
                = match.Groups[2].Value.Trim();
        }

        return items;
    }

    private static List<TraceRow> ParseTraceRows()
        => ParsePipeRows(ExtractMarkerBlock(ReadText(DocPath), TableMarker))
            .Where(static cells => cells.Length == 10 && NfrId().IsMatch(cells[0]))
            .Select(static cells => new TraceRow(
                cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7], cells[8], cells[9]))
            .ToList();

    private static Dictionary<string, string> ParseGovernanceCriteria()
    {
        YamlMappingNode root = LoadSingleYamlDocument(GovernancePath).ShouldBeOfType<YamlMappingNode>();
        YamlSequenceNode criteria = root.Children[new YamlScalarNode("criteria")].ShouldBeOfType<YamlSequenceNode>();
        Dictionary<string, string> result = [];
        foreach (YamlMappingNode criterion in criteria.Children.OfType<YamlMappingNode>())
        {
            string id = ((YamlScalarNode)criterion.Children[new YamlScalarNode("criterion_id")]).Value.ShouldNotBeNull();
            string status = ((YamlScalarNode)criterion.Children[new YamlScalarNode("status")]).Value.ShouldNotBeNull();
            result[id] = status;
        }

        return result;
    }

    private static HashSet<string> ParseTestFactFqns()
        => FactMethod().Matches(ReadText(TestSourcePath))
            .Select(match => $"{ConformanceFqn}.{match.Groups[1].Value}")
            .ToHashSet(StringComparer.Ordinal);

    private static List<string[]> ParsePipeRows(string block)
    {
        List<string[]> rows = [];
        foreach (string line in block.Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            string[] cells = trimmed.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray();
            rows.Add(cells);
        }

        return rows;
    }

    private static string ExtractMarkerBlock(string doc, string marker)
    {
        string openMarker = $"<!-- {marker} -->";
        int open = doc.IndexOf(openMarker, StringComparison.Ordinal);
        open.ShouldBeGreaterThanOrEqualTo(0, $"Missing open marker '{marker}'.");
        int close = doc.IndexOf($"<!-- /{marker} -->", open, StringComparison.Ordinal);
        close.ShouldBeGreaterThan(open, $"Missing close marker '/{marker}'.");
        return doc[(open + openMarker.Length)..close];
    }

    // ---- evidence + safety assertions --------------------------------------------------------------------

    private static IEnumerable<string> EvidenceTokens(TraceRow row)
        => BacktickTokens(row.Gates).Concat(BacktickTokens(row.Exit)).Concat(BacktickTokens(row.Release));

    private static void AssertEvidenceTokensResolve(TraceRow row, HashSet<string> knownCriteria)
    {
        foreach (string token in EvidenceTokens(row))
        {
            if (CriterionId().IsMatch(token))
            {
                knownCriteria.ShouldContain(token, $"{row.Id} cites unknown criterion '{token}'.");
            }
            else if (token.Contains('/', StringComparison.Ordinal))
            {
                RepositoryFileExists(token).ShouldBeTrue($"{row.Id} cites missing evidence path '{token}'.");
            }
        }
    }

    private static void AssertRowHasConcreteEvidence(TraceRow row, HashSet<string> knownCriteria)
        => EvidenceTokens(row).Any(token => knownCriteria.Contains(token) || RepositoryFileExists(token))
            .ShouldBeTrue($"{row.Id} must map to at least one concrete evidence path or known criterion.");

    private static void AssertReferencePendingOwned(TraceRow row)
    {
        row.Owner.ShouldNotBeNullOrWhiteSpace();
        row.Owner.ShouldNotBe("—", $"{row.Id} reference-pending row must name an owner.");
        BacktickTokens(row.Stories).Any(StoryId().IsMatch)
            .ShouldBeTrue($"{row.Id} reference-pending row must cite a consuming story.");
        row.Note.Contains("Release-blocking", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue($"{row.Id} reference-pending row must carry release-blocking semantics.");
    }

    private static void AssertRowIdsAreNfr1To70(List<string> ids)
    {
        ids.Count.ShouldBe(70, "the table must declare exactly 70 NFR rows.");
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(70, "no NFR row may be duplicated.");
        ids.OrderBy(static id => int.Parse(id[3..], System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(Enumerable.Range(1, 70).Select(static n => $"NFR{n}"), "rows must cover NFR1..NFR70.");
    }

    private static void AssertDocExists(string relativePath)
        => File.Exists(RepositoryPath(relativePath)).ShouldBeTrue(relativePath);

    private static void AssertDocMetadataOnly(string text)
    {
        HostAbsolutePathPattern().IsMatch(text).ShouldBeFalse($"NFR doc must not contain a host-absolute path: {Excerpt(text)}");
        SecretMaterialPattern().IsMatch(text).ShouldBeFalse($"NFR doc must not contain secret/credential material: {Excerpt(text)}");

        foreach (Match match in HttpUrlPattern().Matches(text))
        {
            string host = match.Groups[1].Value;
            int port = host.IndexOf(':');
            if (port >= 0)
            {
                host = host[..port];
            }

            bool allowed = host is "localhost" or "127.0.0.1"
                || AllowedPlaceholderHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal));
            allowed.ShouldBeTrue($"NFR doc must use placeholder hosts, not real host '{host}'.");
        }
    }

    private static void AssertNoRecursiveSubmoduleCommand(string text)
        => text.Contains(string.Concat("--", "recursive"), StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse("NFR traceability evidence must never request nested submodule initialization.");

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }

                break;
            case JsonValueKind.String:
                AssertDocMetadataOnly(element.GetString().ShouldNotBeNull());
                break;
        }
    }

    // ---- primitives --------------------------------------------------------------------------------------

    private static string StableHash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..12];

    private static string CategoryForIndex(int n)
        => Categories.Single(c => n >= c.Lo && n <= c.Hi).Name;

    private static List<string> BacktickTokens(string cell)
        => BacktickToken().Matches(cell).Select(static match => match.Groups[1].Value).ToList();

    private static string FirstBacktickToken(string cell)
    {
        Match match = BacktickToken().Match(cell);
        match.Success.ShouldBeTrue($"Expected a backtick token in '{cell}'.");
        return match.Groups[1].Value;
    }

    private static bool IsSeparator(string[] cells)
        => cells.All(static cell => SeparatorCell().IsMatch(cell));

    private static bool RepositoryFileExists(string relativePath)
    {
        string full = RepositoryPath(relativePath);
        return File.Exists(full) || Directory.Exists(full);
    }

    private static YamlNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, relativePath);
        return stream.Documents[0].RootNode;
    }

    // Concatenate the `run:` commands of every step in a named workflow job, so a fact can assert which job a
    // gate runs in rather than merely that the gate name appears somewhere in the file.
    private static string JobRunCommands(YamlMappingNode jobs, string jobName)
    {
        jobs.Children.ContainsKey(new YamlScalarNode(jobName)).ShouldBeTrue($"workflow must declare job '{jobName}'.");
        YamlMappingNode job = jobs.Children[new YamlScalarNode(jobName)].ShouldBeOfType<YamlMappingNode>();
        YamlSequenceNode steps = job.Children[new YamlScalarNode("steps")].ShouldBeOfType<YamlSequenceNode>();
        YamlScalarNode runKey = new("run");

        StringBuilder commands = new();
        foreach (YamlMappingNode step in steps.Children.OfType<YamlMappingNode>())
        {
            if (step.Children.ContainsKey(runKey) && step.Children[runKey] is YamlScalarNode scalar && scalar.Value is not null)
            {
                commands.Append(scalar.Value).Append('\n');
            }
        }

        return commands.ToString();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static string Excerpt(string value) => value.Length <= 80 ? value : value[..80];

    [GeneratedRegex(@"\n## ")]
    private static partial Regex NextLevelTwoHeader();

    [GeneratedRegex(@"\n### ")]
    private static partial Regex NextLevelThreeHeader();

    [GeneratedRegex(@"^- (.+)$", RegexOptions.Multiline)]
    private static partial Regex BulletLine();

    [GeneratedRegex(@"^- NFR(\d+): (.+)$", RegexOptions.Multiline)]
    private static partial Regex NumberedNfrLine();

    [GeneratedRegex(@"^NFR\d+$")]
    private static partial Regex NfrId();

    [GeneratedRegex(@"^C\d+$")]
    private static partial Regex CriterionId();

    [GeneratedRegex(@"^\d+-\d+$")]
    private static partial Regex StoryId();

    [GeneratedRegex(@"^[0-9a-f]{12}$")]
    private static partial Regex TwelveHexHash();

    [GeneratedRegex(@"^[0-9a-fA-F]{40}$")]
    private static partial Regex FortyHex();

    [GeneratedRegex(@"^-+$")]
    private static partial Regex SeparatorCell();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex BacktickToken();

    [GeneratedRegex(@"\[Fact\]\s+public void (\w+)\s*\(")]
    private static partial Regex FactMethod();

    [GeneratedRegex(@"'(Hexalith\.Folders\.Contracts\.Tests\.Deployment\.NfrTraceabilityConformanceTests\.\w+)'")]
    private static partial Regex RunnerMethodEntry();

    // The release gate must fail live publish on stale NFR evidence: Mode is Publish AND the report source_commit
    // differs from the release SourceRevisionId. Matched inside the NFR-scoped block so it cannot be neutered.
    [GeneratedRegex(@"\$Mode\s*-eq\s*'Publish'\s*-and\s*\$evidence\.source_commit\s*-ne\s*\$SourceRevisionId")]
    private static partial Regex NfrStaleSameCommitGuard();

    // The drive-letter clause requires the letter not to be preceded by another letter, so a URL scheme such
    // as "https:/" is not mistaken for a "C:\" Windows path while a real "C:\Users" path still matches.
    [GeneratedRegex(@"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY|AccountKey=|client_secret\s*[:=]\s*\S|xox[baprs]-|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.", RegexOptions.CultureInvariant)]
    private static partial Regex SecretMaterialPattern();

    [GeneratedRegex(@"https?://([^/\s)""'\]]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPattern();
}
