using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Static conformance gate for the Story 7.17 ADR set and maintenance runbooks. The required ADR/runbook
/// inventory is re-derived from the marker-bounded manifest in the focused gate script, then asserted against
/// the published files, indexes, CI wiring, and metadata-only posture so the handoff docs cannot drift silently.
/// </summary>
public sealed partial class AdrRunbookDocsConformanceTests
{
    private const string AdrDirectory = "docs/adrs";
    private const string RunbookDirectory = "docs/runbooks";
    private const string AdrTemplatePath = "docs/adrs/0000-template.md";
    private const string ExistingAdrPath = "docs/adrs/0001-folder-domain-processor-persistence.md";
    private const string AdrIndexPath = "docs/adrs/index.md";
    private const string RunbookIndexPath = "docs/runbooks/index.md";
    private const string ArchitecturePath = "_bmad-output/planning-artifacts/architecture.md";
    private const string GateScriptPath = "tests/tools/run-adr-runbook-docs-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string CiWorkflowPath = ".github/workflows/ci.yml";
    private const string NightlyDriftWorkflowPath = ".github/workflows/nightly-drift.yml";
    private const string PolicyConformanceWorkflowPath = ".github/workflows/policy-conformance.yml";
    private const string ReleaseWorkflowPath = ".github/workflows/release-packages.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string ReleaseGatePath = "tests/tools/run-release-package-gates.ps1";
    private const string ReportPath = "_bmad-output/gates/adr-runbook-docs/latest.json";
    private const string TestSourcePath = "tests/Hexalith.Folders.Contracts.Tests/Deployment/AdrRunbookDocsConformanceTests.cs";

    private const string ConformanceFqn = "Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests";
    private const string ManifestMarker = "adr-runbook-source-of-truth";
    private const string AdrIndexMarker = "adr-index";
    private const string RunbookIndexMarker = "runbook-index";

    private static readonly string[] AllowedPlaceholderHostSuffixes =
        [".invalid", ".internal", ".example", ".localhost", ".test"];

    private static readonly string[] RequiredAdrSections =
    [
        "Status",
        "Context",
        "Decision",
        "Consequences",
        "Alternatives Considered",
        "Verification",
    ];

    private static readonly string[] RequiredNewRunbookSections =
    [
        "Purpose",
        "Preconditions",
        "Procedure",
        "Verification",
        "Escalation and handoff",
        "Related evidence",
        "Forbidden evidence",
    ];

    private static readonly string[] RequiredTenantDeletionSections =
    [
        "Authorization prerequisites",
        "Disposition matrix",
        "Manual review checklist",
        "Automated validation",
        "Forbidden evidence",
    ];

    private static readonly string[] ExpectedReportSurfaces =
    [
        "adr-set",
        "adr-template-preserved",
        "runbook-set",
        "adr-index",
        "runbook-index",
        "architecture-decision-citations",
        "metadata-only",
        "ci-wiring",
    ];

    private static readonly string SubmoduleCommand =
        "git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants";

    private sealed record AdrManifestRow(string Area, string File, IReadOnlyList<string> DecisionIds);

    private sealed record RunbookManifestRow(string Topic, string File, bool Preserved);

    [Fact]
    public void AdrRunbookManifestIsNonVacuousAndFilesExist()
    {
        (List<AdrManifestRow> adrs, List<RunbookManifestRow> runbooks) = ParseManifest();

        adrs.Select(static r => r.Area).OrderBy(static a => a, StringComparer.Ordinal)
            .ShouldBe(["contract", "deployment", "idempotency", "observability", "provider", "security"]);
        runbooks.Select(static r => r.Topic).OrderBy(static t => t, StringComparer.Ordinal)
            .ShouldBe(["alerts", "incident-mode operations", "provider drift", "reconciliation", "retention", "rollback", "tenant deletion"]);

        adrs.Count.ShouldBe(6);
        runbooks.Count.ShouldBe(7);
        runbooks.Count(static r => r.Preserved).ShouldBe(1, "only tenant-deletion.md is a preserved pre-existing runbook.");

        foreach (AdrManifestRow adr in adrs)
        {
            AssertDocExists(AdrPath(adr.File));
        }

        foreach (RunbookManifestRow runbook in runbooks)
        {
            AssertDocExists(RunbookPath(runbook.File));
        }
    }

    [Fact]
    public void NewAdrsHaveRequiredSectionsAcceptedStatusNoPlaceholderAndRealDecisionIds()
    {
        HashSet<string> architectureIds = ParseArchitectureDecisionIds();
        foreach (AdrManifestRow adr in ParseManifest().Adrs)
        {
            string path = AdrPath(adr.File);
            string doc = ReadText(path);

            doc.ShouldStartWith($"# ADR {adr.File[..4]}: ", Case.Sensitive);
            DateLine().IsMatch(doc).ShouldBeTrue($"{path} must carry a leading Date: YYYY-MM-DD metadata line.");
            foreach (string section in RequiredAdrSections)
            {
                AssertHasSection(doc, section, path);
            }

            ExtractSection(doc, "Status").ShouldContain("Accepted", Case.Sensitive, $"{path} must record an accepted retrospective decision.");
            doc.ShouldNotContain("PLACEHOLDER", Case.Sensitive);

            foreach (string id in adr.DecisionIds)
            {
                architectureIds.ShouldContain(id, $"{path} cites architecture decision ID '{id}' that must exist in architecture.md.");
                doc.ShouldContain($"`{id}`", Case.Sensitive, $"{path} must cite its manifest decision ID '{id}'.");
            }

            doc.ShouldContain("Implementing", Case.Sensitive, $"{path} must cite an implementing story or epic.");
            AssertDocMetadataOnly(doc);
            AssertNoRecursiveSubmoduleCommand(doc);
        }
    }

    [Fact]
    public void AdrTemplateAndExistingAdrArePreserved()
    {
        string template = ReadText(AdrTemplatePath);
        template.ShouldContain("non_policy_placeholder: true", Case.Sensitive);
        template.ShouldContain("PLACEHOLDER", Case.Sensitive);
        template.ShouldNotContain("Decision identifiers:", Case.Sensitive);

        string existing = ReadText(ExistingAdrPath);
        existing.ShouldContain("# ADR 0001: Folder domain processor persistence ownership", Case.Sensitive);
        existing.ShouldContain("Accepted for Story 2.8b implementation", Case.Sensitive);
        existing.ShouldContain("FolderArchiveTenantGate", Case.Sensitive);
    }

    [Fact]
    public void RunbooksHaveExpectedSectionContractsAndGapContent()
    {
        foreach (RunbookManifestRow runbook in ParseManifest().Runbooks)
        {
            string path = RunbookPath(runbook.File);
            string doc = ReadText(path);

            if (runbook.Preserved)
            {
                foreach (string section in RequiredTenantDeletionSections)
                {
                    AssertHasSection(doc, section, path);
                }

                continue;
            }

            foreach (string section in RequiredNewRunbookSections)
            {
                AssertHasSection(doc, section, path);
            }

            doc.ShouldContain("metadata-only", Case.Sensitive, $"{path} must state its metadata-only posture.");
            AssertDocMetadataOnly(doc);
            AssertNoRecursiveSubmoduleCommand(doc);
        }

        ReadText(RunbookPath("rollback.md")).ShouldContain("post-rollback health verification", Case.Sensitive);
        ReadText(RunbookPath("rollback.md")).ShouldContain("reference_pending", Case.Sensitive);
        ReadText(RunbookPath("alerts.md")).ShouldContain("live alert delivery", Case.Insensitive);
        ReadText(RunbookPath("alerts.md")).ShouldContain("reference_pending", Case.Sensitive);
        ReadText(RunbookPath("reconciliation.md")).ShouldContain("unknown_provider_outcome", Case.Sensitive);
        ReadText(RunbookPath("reconciliation.md")).ShouldContain("reconciliation_required", Case.Sensitive);
        ReadText(RunbookPath("reconciliation.md")).ShouldContain("no silent", Case.Insensitive);
        ReadText(RunbookPath("incident-mode.md")).ShouldContain("/_admin/incident-stream", Case.Sensitive);
        ReadText(RunbookPath("incident-mode.md")).ShouldContain("operator-disposition", Case.Sensitive);
    }

    [Fact]
    public void AdrAndRunbookIndexesMatchDirectoryInventories()
    {
        List<string> actualAdrs = Directory.EnumerateFiles(RepositoryPath(AdrDirectory), "*.md")
            .Select(Path.GetFileName)
            .Where(static name => name is not null && name != "0000-template.md" && name != "index.md")
            .Select(static name => name.ShouldNotBeNull())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        List<string> indexedAdrs = FirstColumnBacktickTokens(ExtractMarkerBlock(ReadText(AdrIndexPath), AdrIndexMarker))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        indexedAdrs.ShouldBe(actualAdrs, "ADR index must match accepted numbered ADR files exactly and exclude 0000-template.md.");
        indexedAdrs.ShouldNotContain("0000-template.md");

        List<string> actualRunbooks = Directory.EnumerateFiles(RepositoryPath(RunbookDirectory), "*.md")
            .Select(Path.GetFileName)
            .Where(static name => name is not null && name != "index.md")
            .Select(static name => name.ShouldNotBeNull())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        List<string> indexedRunbooks = FirstColumnBacktickTokens(ExtractMarkerBlock(ReadText(RunbookIndexPath), RunbookIndexMarker))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        indexedRunbooks.ShouldBe(actualRunbooks, "runbook index must match runbook files exactly and exclude index.md.");
    }

    [Fact]
    public void AdrRunbookDocsStayMetadataOnly()
    {
        (List<AdrManifestRow> adrs, List<RunbookManifestRow> runbooks) = ParseManifest();

        foreach (string path in adrs.Select(static a => AdrPath(a.File))
            .Concat(runbooks.Where(static r => !r.Preserved).Select(static r => RunbookPath(r.File)))
            .Concat([AdrIndexPath, RunbookIndexPath]))
        {
            string doc = ReadText(path);
            AssertDocMetadataOnly(doc);
            AssertNoRecursiveSubmoduleCommand(doc);
            doc.ShouldContain("metadata-only", Case.Sensitive, $"{path} must state its metadata-only evidence posture.");
        }
    }

    [Fact]
    public void AdrRunbookGateScriptFailsClosedAndEmitsBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);
        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            "[Alias('NoRestore')]",
            "Split-Path -Parent $MyInvocation.MyCommand.Path",
            "Resolve-Path",
            "Push-Location",
            "Pop-Location",
            "$LASTEXITCODE",
            "utf8NoBOM",
            "GATE-VACUOUS",
            "xunit",
            ReportPath,
            "diagnostic_policy",
            "metadata-only",
            ManifestMarker,
            $"FullyQualifiedName~{ConformanceFqn}",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        AssertNoRecursiveSubmoduleCommand(script);
        ParseManifest().Adrs.Count.ShouldBe(6, "the gate script source-of-truth manifest must be present and non-vacuous.");
        ParseManifest().Runbooks.Count.ShouldBe(7, "the gate script source-of-truth manifest must include all runbooks.");

        HashSet<string> factMethods = ParseTestFactFqns();
        HashSet<string> runnerMethods = RunnerMethodEntry().Matches(script)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        runnerMethods.OrderBy(static m => m, StringComparer.Ordinal)
            .ShouldBe(factMethods.OrderBy(static m => m, StringComparer.Ordinal),
                "the gate script $runnerMethods must equal the AdrRunbookDocsConformanceTests [Fact] set exactly.");
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiWireAdrRunbookGateOnlyInAllowedLanes()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-adr-runbook-docs-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        AssertNoRecursiveSubmoduleCommand(workflow);

        int nfrStep = workflow.IndexOf("run-nfr-traceability-gates.ps1", StringComparison.Ordinal);
        int adrRunbookStep = workflow.IndexOf("run-adr-runbook-docs-gates.ps1", StringComparison.Ordinal);
        nfrStep.ShouldBeGreaterThanOrEqualTo(0, "the NFR traceability step must remain wired.");
        adrRunbookStep.ShouldBeGreaterThan(nfrStep, "the ADR/runbook docs step must come immediately after the NFR traceability step.");

        foreach (string isolatedLane in new[] { CiWorkflowPath, NightlyDriftWorkflowPath, PolicyConformanceWorkflowPath, ReleaseWorkflowPath, ReleaseGatePath })
        {
            ReadText(isolatedLane).ShouldNotContain("run-adr-runbook-docs-gates.ps1", Case.Sensitive,
                $"{isolatedLane} must not run the focused ADR/runbook docs gate.");
        }

        ReadText(BaselineGatePath).ShouldContain(ConformanceFqn, Case.Sensitive);
    }

    [Fact]
    public void AdrRunbookLatestReportStaysMetadataOnlyAndMatchesInventory()
    {
        if (!File.Exists(RepositoryPath(ReportPath)))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;
        RequiredString(root, "gate").ShouldBe("adr-runbook-docs");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        root.GetProperty("adr_total").GetInt32().ShouldBe(6);
        root.GetProperty("runbook_total").GetInt32().ShouldBe(7);
        AssertMetadataOnlyJson(root);

        root.GetProperty("surfaces").EnumerateArray()
            .Select(static e => e.GetString().ShouldNotBeNull())
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(ExpectedReportSurfaces.OrderBy(static s => s, StringComparer.Ordinal),
                "report surfaces[] must be bounded to the intended diagnostic surfaces.");

        string sourceCommit = RequiredString(root, "source_commit");
        (sourceCommit == "NO_VCS" || FortyHex().IsMatch(sourceCommit))
            .ShouldBeTrue("source_commit must be a full SHA or NO_VCS.");
    }

    [Fact]
    public void NegativeControlsRejectAdrRunbookDocDriftAndUnsafeEvidence()
    {
        HashSet<string> architectureIds = ParseArchitectureDecisionIds();

        Should.Throw<ShouldAssertException>(() => AssertDocExists("docs/adrs/9999-missing.md"));
        Should.Throw<ShouldAssertException>(() => AssertCompletedAdr("docs/adrs/0000-template.md", ["C0"], architectureIds));
        Should.Throw<ShouldAssertException>(() => AssertHasSection("# ADR 9999: Missing\n\n## Status\n\nAccepted\n", "Decision", "synthetic"));
        Should.Throw<ShouldAssertException>(() => AssertAcceptedStatus("# ADR 9999: Bad\n\nDate: 2026-05-31\n\n## Status\n\nProposed\n\n## Context\n\nx\n"));
        Should.Throw<ShouldAssertException>(() => architectureIds.ShouldContain("Z-999", "absent decision ID must fail."));

        List<string> realRunbookIndex = FirstColumnBacktickTokens(ExtractMarkerBlock(ReadText(RunbookIndexPath), RunbookIndexMarker));
        Should.Throw<ShouldAssertException>(() => realRunbookIndex.ShouldContain("missing-topic.md"));
        Should.Throw<ShouldAssertException>(() => AssertHasSection("# Broken\n\n## Purpose\n\nx\n", "Verification", "synthetic-runbook"));

        List<string> realAdrIndex = FirstColumnBacktickTokens(ExtractMarkerBlock(ReadText(AdrIndexPath), AdrIndexMarker));
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(realAdrIndex.Where(static f => f != "0007-container-deployment-with-dapr.md"), realAdrIndex, "missing ADR index entry"));
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(realAdrIndex.Append("9999-orphan.md"), realAdrIndex, "orphan ADR index entry"));
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(realRunbookIndex.Where(static f => f != "alerts.md"), realRunbookIndex, "missing runbook index topic"));
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(realRunbookIndex.Append("orphan-runbook.md"), realRunbookIndex, "orphan runbook index topic"));

        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("evidence at /home/runner/work/leaked.txt"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("config at /etc/folders/oidc.json"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("key at /root/.ssh/id_rsa"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhY3RvciJ9.signaturesegment"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("provider token ghp_0123456789abcdefABCDEF0123456789abcd"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("aws key AKIAIOSFODNN7EXAMPLE"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("connection Password=Pr0dPassw0rd!;"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("status dashboard https://folders.example-real.com/api"));
        Should.NotThrow(() => AssertDocMetadataOnly("status dashboard https://folders.localhost.test and https://localhost:17000"));

        Should.Throw<ShouldAssertException>(() => ParsePipeRows("not a markdown table")
            .Count.ShouldBeGreaterThan(0, "malformed markdown table must not pass vacuously."));

        // AC10 enumerates malformed YAML. This gate's published artifacts are Markdown (ADRs/runbooks/indexes)
        // and JSON (the report), so there is no YAML production surface; this control exercises the YAML
        // parser's fail-closed contract directly to keep the AC10/AC8 malformed-YAML obligation honoured.
        Should.Throw<YamlDotNet.Core.YamlException>(() =>
        {
            using StringReader reader = new("- a: 1\n  b: : :\n :\n");
            YamlStream stream = new();
            stream.Load(reader);
        });
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"adr-runbook-docs\", "));
        Should.Throw<ShouldAssertException>(() => AssertNoRecursiveSubmoduleCommand("git submodule update --init " + string.Concat("--", "recursive")));
        Should.NotThrow(() => AssertNoRecursiveSubmoduleCommand(SubmoduleCommand));
    }

    private static (List<AdrManifestRow> Adrs, List<RunbookManifestRow> Runbooks) ParseManifest()
    {
        string block = ExtractMarkerBlock(ReadText(GateScriptPath), ManifestMarker);
        List<AdrManifestRow> adrs = [];
        List<RunbookManifestRow> runbooks = [];

        foreach (string line in block.Split('\n').Select(static l => l.Trim()))
        {
            Match adr = AdrManifestLine().Match(line);
            if (adr.Success)
            {
                adrs.Add(new(
                    adr.Groups[1].Value,
                    adr.Groups[2].Value,
                    adr.Groups[3].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
                continue;
            }

            Match runbook = RunbookManifestLine().Match(line);
            if (runbook.Success)
            {
                runbooks.Add(new(
                    runbook.Groups[1].Value,
                    runbook.Groups[2].Value,
                    runbook.Groups[3].Value.Equals("preserved", StringComparison.Ordinal)));
            }
        }

        adrs.Count.ShouldBe(6, "gate manifest must declare exactly six ADR rows.");
        runbooks.Count.ShouldBe(7, "gate manifest must declare exactly seven runbook rows.");
        adrs.Select(static r => r.File).Distinct(StringComparer.Ordinal).Count().ShouldBe(6);
        runbooks.Select(static r => r.File).Distinct(StringComparer.Ordinal).Count().ShouldBe(7);
        return (adrs, runbooks);
    }

    private static void AssertCompletedAdr(string relativePath, IReadOnlyList<string> decisionIds, HashSet<string> architectureIds)
    {
        string doc = ReadText(relativePath);
        foreach (string section in RequiredAdrSections)
        {
            AssertHasSection(doc, section, relativePath);
        }

        AssertAcceptedStatus(doc);
        doc.ShouldNotContain("PLACEHOLDER", Case.Sensitive);
        decisionIds.Any(architectureIds.Contains).ShouldBeTrue($"{relativePath} must cite at least one known architecture decision ID.");
    }

    private static void AssertAcceptedStatus(string doc)
        => ExtractSection(doc, "Status").ShouldContain("Accepted", Case.Sensitive);

    private static HashSet<string> ParseArchitectureDecisionIds()
        => DecisionId().Matches(ReadText(ArchitecturePath))
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseTestFactFqns()
        => FactMethod().Matches(ReadText(TestSourcePath))
            .Select(match => $"{ConformanceFqn}.{match.Groups[1].Value}")
            .ToHashSet(StringComparer.Ordinal);

    private static string ExtractSection(string doc, string heading)
    {
        string marker = $"## {heading}";
        int start = doc.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing section '{heading}'.");
        string rest = doc[(start + marker.Length)..];
        Match next = LevelTwoHeader().Match(rest);
        return next.Success ? rest[..next.Index] : rest;
    }

    private static void AssertHasSection(string doc, string heading, string path)
        => doc.Contains($"\n## {heading}\n", StringComparison.Ordinal)
            .ShouldBeTrue($"{path} must include section '## {heading}'.");

    private static List<string> FirstColumnBacktickTokens(string table)
        => ParsePipeRows(table)
            .Where(static cells => cells.Length > 0 && !IsSeparator(cells) && cells[0] is not ("ADR" or "Runbook"))
            .Select(static cells => FirstBacktickToken(cells[0]))
            .ToList();

    private static string ExtractMarkerBlock(string doc, string marker)
    {
        string openMarker = $"<!-- {marker} -->";
        int open = doc.IndexOf(openMarker, StringComparison.Ordinal);
        open.ShouldBeGreaterThanOrEqualTo(0, $"Missing open marker '{marker}'.");
        int close = doc.IndexOf($"<!-- /{marker} -->", open, StringComparison.Ordinal);
        close.ShouldBeGreaterThan(open, $"Missing close marker '/{marker}'.");
        return doc[(open + openMarker.Length)..close];
    }

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

            rows.Add(trimmed.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray());
        }

        return rows;
    }

    private static void AssertDocExists(string relativePath)
        => File.Exists(RepositoryPath(relativePath)).ShouldBeTrue(relativePath);

    private static void AssertSetEquals(IEnumerable<string> actual, IEnumerable<string> expected, string because)
        => actual.OrderBy(static s => s, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(static s => s, StringComparer.Ordinal), because);

    private static void AssertDocMetadataOnly(string text)
    {
        HostAbsolutePathPattern().IsMatch(text).ShouldBeFalse($"ADR/runbook evidence must not contain a host-absolute path: {Excerpt(text)}");
        SecretMaterialPattern().IsMatch(text).ShouldBeFalse($"ADR/runbook evidence must not contain secret/credential material: {Excerpt(text)}");

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
            allowed.ShouldBeTrue($"ADR/runbook evidence must use placeholder hosts, not real host '{host}'.");
        }
    }

    private static void AssertNoRecursiveSubmoduleCommand(string text)
        => text.Contains(string.Concat("--", "recursive"), StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse("ADR/runbook evidence must never request nested submodule initialization.");

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

    private static string AdrPath(string fileName) => $"{AdrDirectory}/{fileName}";

    private static string RunbookPath(string fileName) => $"{RunbookDirectory}/{fileName}";

    private static string FirstBacktickToken(string cell)
    {
        Match match = BacktickToken().Match(cell);
        match.Success.ShouldBeTrue($"Expected a backtick token in '{cell}'.");
        return match.Groups[1].Value;
    }

    private static bool IsSeparator(string[] cells)
        => cells.All(static cell => SeparatorCell().IsMatch(cell));

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

    [GeneratedRegex(@"^# ADR\|([^|]+)\|([^|]+)\|([^|]+)$")]
    private static partial Regex AdrManifestLine();

    [GeneratedRegex(@"^# RUNBOOK\|([^|]+)\|([^|]+)\|(new|preserved)$")]
    private static partial Regex RunbookManifestLine();

    [GeneratedRegex(@"(?m)^Date: \d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateLine();

    [GeneratedRegex(@"\n## ")]
    private static partial Regex LevelTwoHeader();

    [GeneratedRegex(@"\b(?:[ADIS]-\d+|C\d+)\b")]
    private static partial Regex DecisionId();

    [GeneratedRegex(@"^-+$")]
    private static partial Regex SeparatorCell();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex BacktickToken();

    [GeneratedRegex(@"\[Fact\]\s+public void (\w+)\s*\(")]
    private static partial Regex FactMethod();

    [GeneratedRegex(@"'(Hexalith\.Folders\.Contracts\.Tests\.Deployment\.AdrRunbookDocsConformanceTests\.\w+)'")]
    private static partial Regex RunnerMethodEntry();

    [GeneratedRegex(@"^[0-9a-fA-F]{40}$")]
    private static partial Regex FortyHex();

    [GeneratedRegex(@"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/(?:home|Users|root|etc|var|opt|srv|mnt)/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY|AccountKey=|client_secret\s*[:=]\s*\S|xox[baprs]-|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.|gh[posru]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|(?i:password)\s*[:=]\s*\S", RegexOptions.CultureInvariant)]
    private static partial Regex SecretMaterialPattern();

    [GeneratedRegex(@"https?://([^/\s)""'\]]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPattern();
}
