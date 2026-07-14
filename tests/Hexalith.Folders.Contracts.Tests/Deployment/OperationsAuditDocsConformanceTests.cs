using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Static conformance gate for the Story 7.14 operations + audit documentation. Every inventory is
/// re-derived from the authoritative source (the OpenAPI Contract Spine audit family, the audit projection
/// DTOs, <c>FolderAuditOperationKind</c> / <c>FolderAuditResult</c>, <c>DispositionLabelMapper</c>,
/// <c>RedactionVisibility</c> / <c>FieldDisclosure</c>, the production observability manifest, and the C3
/// tenant-deletion runbook) and asserted equal to the published docs with exact cardinality, so the docs
/// cannot silently drift. All assertions route through the same scanners the negative controls exercise.
/// </summary>
public sealed partial class OperationsAuditDocsConformanceTests
{
    private const string SpinePath = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml";
    private const string AuditRecordPath = "src/Hexalith.Folders.Contracts/Projections/Audit/AuditRecord.cs";
    private const string TimelineEntryPath = "src/Hexalith.Folders.Contracts/Projections/Audit/OperationTimelineEntry.cs";
    private const string OperationKindPath = "src/Hexalith.Folders/Observability/FolderAuditOperationKind.cs";
    private const string OperationResultPath = "src/Hexalith.Folders/Observability/FolderAuditResult.cs";
    private const string DispositionMapperPath = "src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs";
    private const string RedactionVisibilityPath = "src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs";
    private const string FieldDisclosurePath = "src/Hexalith.Folders.UI/Services/FieldDisclosure.cs";
    private const string SanitizerPath = "src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs";
    private const string ObservabilityManifestPath = "deploy/observability/production/observability.yaml";
    private const string TenantDeletionRunbookPath = "docs/runbooks/tenant-deletion.md";
    private const string LeakageCorpusPath = "tests/fixtures/audit-leakage-corpus.json";

    private const string ConsoleDocPath = "docs/operations/operations-console.md";
    private const string AuditDocPath = "docs/operations/audit-and-redaction.md";
    private const string IncidentDocPath = "docs/operations/incident-alerting-and-recovery.md";

    private const string GateScriptPath = "tests/tools/run-operations-audit-docs-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string CiWorkflowPath = ".github/workflows/ci.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string ReportPath = "_bmad-output/gates/operations-audit-docs/latest.json";

    private const string ConformanceFqn = "Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests";

    private static readonly string[] RequiredDocs = [ConsoleDocPath, AuditDocPath, IncidentDocPath];

    private static readonly string[] AllowedPlaceholderHostSuffixes =
        [".invalid", ".internal", ".example", ".localhost", ".test"];

    private static readonly string SubmoduleCommand =
        "git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants";

    [Fact]
    public void RequiredOperationsAuditDocsExist()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocExists(doc);
        }
    }

    [Fact]
    public void AuditDocOperationInventoryEqualsSpineAuditFamily()
    {
        HashSet<string> spineAuditOps = ParseAuditFamilyOperations();
        spineAuditOps.Count.ShouldBe(4, "the spine must carry exactly four audit-family operations.");

        HashSet<string> docOps = FirstColumnBacktickTokens(AuditDocPath, "<!-- audit-operation-inventory -->");
        AssertSetEquals(docOps, spineAuditOps, "audit doc operation inventory must equal the spine audit family exactly.");
    }

    [Fact]
    public void AuditDocFieldCatalogEqualsAuditRecordAndTimelineDtos()
    {
        HashSet<string> recordFields = ParseJsonPropertyNames(AuditRecordPath);
        recordFields.Count.ShouldBe(13, "AuditRecord must declare the canonical 13 wire fields.");
        HashSet<string> timelineFields = ParseJsonPropertyNames(TimelineEntryPath);
        timelineFields.Count.ShouldBe(11, "OperationTimelineEntry must declare the canonical 11 wire fields.");

        HashSet<string> docRecordFields = FirstColumnBacktickTokens(AuditDocPath, "<!-- audit-record-fields -->");
        AssertSetEquals(docRecordFields, recordFields, "audit doc AuditRecord field catalog must equal the DTO exactly.");

        HashSet<string> docTimelineFields = FirstColumnBacktickTokens(AuditDocPath, "<!-- operation-timeline-fields -->");
        AssertSetEquals(docTimelineFields, timelineFields, "audit doc OperationTimelineEntry field catalog must equal the DTO exactly.");
    }

    [Fact]
    public void AuditDocOperationKindAndResultTaxonomyEqualsObservationEnums()
    {
        HashSet<string> kinds = ParseEnumMembers(OperationKindPath);
        kinds.Count.ShouldBe(13, "FolderAuditOperationKind must define the canonical 13 members.");
        HashSet<string> results = ParseEnumMembers(OperationResultPath);
        results.Count.ShouldBe(11, "FolderAuditResult must define the canonical 11 members.");

        HashSet<string> docKinds = FirstColumnBacktickTokens(AuditDocPath, "<!-- operation-kind-taxonomy -->");
        AssertSetEquals(docKinds, kinds, "audit doc operation-kind taxonomy must equal FolderAuditOperationKind exactly.");

        HashSet<string> docResults = FirstColumnBacktickTokens(AuditDocPath, "<!-- operation-result-taxonomy -->");
        AssertSetEquals(docResults, results, "audit doc result taxonomy must equal FolderAuditResult exactly.");
    }

    [Fact]
    public void ConsoleDocDispositionVocabularyAndTechnicalStatesEqualMapper()
    {
        Dictionary<string, string> dispositions = ParseDispositionVocabulary();
        dispositions.Count.ShouldBe(5, "the operator-disposition vocabulary must have exactly 5 members.");
        HashSet<string> states = ParseTechnicalStates();
        states.Count.ShouldBe(11, "the C6 technical-state catalog must have exactly 11 states.");

        HashSet<string> docDispositions = FirstColumnBacktickTokens(ConsoleDocPath, "<!-- disposition-vocabulary -->");
        AssertSetEquals(docDispositions, dispositions.Keys, "console doc disposition wire vocabulary must equal the mapper exactly.");

        string console = ReadText(ConsoleDocPath);
        foreach ((_, string label) in dispositions)
        {
            console.ShouldContain(label, Case.Sensitive);
        }

        HashSet<string> docStates = FirstColumnBacktickTokens(ConsoleDocPath, "<!-- technical-state-catalog -->");
        AssertSetEquals(docStates, states, "console doc technical-state catalog must equal the mapper's C6 states exactly.");

        // F-4 rules and the projection-lag conditional must be pinned, not paraphrased away.
        foreach (string required in new[]
        {
            "`ready` maps to **`available` ONLY when there is no projection-lag evidence**",
            "`degraded_but_serving`",
            "disposition is the primary",
            "secondary",
            "non-color-only",
        })
        {
            NormalizeWhitespace(console).ShouldContain(NormalizeWhitespace(required), Case.Sensitive);
        }

        // Seven no-mutation guarantees enumerated in the dedicated block.
        string guarantees = ExtractMarkerBlock(console, "<!-- no-mutation-guarantees -->");
        NumberedListItem().Matches(guarantees).Count.ShouldBe(7, "the console doc must enumerate the seven no-mutation guarantees.");
    }

    [Fact]
    public void RedactionDocVocabulariesEqualWireAndPresentationEnums()
    {
        HashSet<string> wire = ParseRedactionVisibility();
        AssertSetEquals(wire, ["metadata_only", "redacted"], "RedactionVisibility must have exactly the 2 wire members.");
        HashSet<string> presentation = ParseEnumMembers(FieldDisclosurePath);
        AssertSetEquals(presentation, ["Visible", "Redacted", "Unknown", "Missing"], "FieldDisclosure must have exactly the 4 presentation members.");

        HashSet<string> docWire = FirstColumnBacktickTokens(AuditDocPath, "<!-- redaction-wire-vocabulary -->");
        AssertSetEquals(docWire, wire, "audit doc wire redaction vocabulary must equal RedactionVisibility exactly.");

        HashSet<string> docPresentation = FirstColumnBacktickTokens(AuditDocPath, "<!-- redaction-presentation-vocabulary -->");
        AssertSetEquals(docPresentation, presentation, "audit doc presentation redaction vocabulary must equal FieldDisclosure exactly.");

        string audit = ReadText(AuditDocPath);
        foreach (string required in new[]
        {
            "redacted MUST be visibly distinct from unknown and from missing",
            "FolderAuditSanitizer",
            "RedactionDisclosureMapper",
            "FieldDisclosure",
            "do not exist",
            "correlationId` and `taskId`",
        })
        {
            audit.ShouldContain(required, Case.Sensitive);
        }

        // Phantom classes must never be cited (and must not exist in the repository).
        audit.ShouldNotContain("SensitiveMetadataClassifier", Case.Sensitive);
        audit.ShouldNotContain("RedactingFormatter", Case.Sensitive);
        Directory.EnumerateFiles(RepositoryPath("src"), "*.cs", SearchOption.AllDirectories)
            .Any(file => File.ReadAllText(file).Contains("SensitiveMetadataClassifier", StringComparison.Ordinal)
                || File.ReadAllText(file).Contains("RedactingFormatter", StringComparison.Ordinal))
            .ShouldBeFalse("the phantom redaction classes must not exist in source.");
    }

    [Fact]
    public void AlertingDocSignalsEqualObservabilityManifest()
    {
        Dictionary<string, string> signals = ParseObservabilitySignals();
        signals.Count.ShouldBe(5, "the observability manifest must declare exactly 5 operational signals.");

        HashSet<string> docSignals = FirstColumnBacktickTokens(IncidentDocPath, "<!-- alert-signal-inventory -->");
        AssertSetEquals(docSignals, signals.Keys, "incident doc alert-signal inventory must equal the observability manifest exactly.");

        string block = ExtractMarkerBlock(ReadText(IncidentDocPath), "<!-- alert-signal-inventory -->");
        foreach ((string name, string severity) in signals)
        {
            block.ShouldContain($"`{name}` | {severity}", Case.Sensitive);
        }
    }

    [Fact]
    public void RecoveryDocRetentionDispositionsEqualRetentionSources()
    {
        HashSet<string> retention = ParseRetentionDispositions();
        AssertSetEquals(retention, ["deleted", "tombstoned", "retained", "anonymized"],
            "the tenant-deletion runbook must enumerate exactly the 4 retention dispositions.");

        HashSet<string> docRetention = FirstColumnBacktickTokens(IncidentDocPath, "<!-- retention-disposition-inventory -->");
        AssertSetEquals(docRetention, retention, "incident doc retention dispositions must equal the runbook exactly.");

        // The reference-pending retention-class markers must not be presented as resolved.
        string incident = ReadText(IncidentDocPath);
        incident.ShouldContain("RetentionClassToken", Case.Sensitive);
        incident.ShouldContain("reference_pending", Case.Sensitive);
    }

    [Fact]
    public void ConsoleDocPinsRoutesJourneysTrustQuestionsAndPerceivedWait()
    {
        string console = ReadText(ConsoleDocPath);

        foreach (string route in new[]
        {
            "`/`",
            "`/tenants`",
            "`/folders`",
            "`/folders/{folderId}`",
            "`/folders/{folderId}/workspaces/{workspaceId}`",
            "`/folders/{folderId}/audit-trail`",
            "`/folders/{folderId}/operation-timeline`",
            "`/folders/{folderId}/provider`",
            "`/providers/support`",
            "`/_admin/incident-stream`",
            "`/dev/redaction-gallery`",
            "`/dev/state-label-gallery`",
        })
        {
            console.ShouldContain(route, Case.Sensitive);
        }

        foreach (string required in new[]
        {
            "10 operator pages",
            "2 dev-only galleries",
            "non-production",
            "Env.IsDevelopment()",
            "?folder=",
            "Blazor Web App",
            "Interactive Server",
            "never calls `AddHexalithEventStore`",
            "no `/api/v1/commands` endpoint",
            "filter-rejection-only",
            "filter_not_yet_supported",
            // Three journeys + five trust questions.
            "J1",
            "J2",
            "J3",
            "what happened",
            "who or what caused it",
            "from which surface",
            "whether the evidence can be trusted",
            // Perceived-wait + budgets stay release-validation; accessibility now has an automated CI gate
            // (Story 8.4 — the axe / WCAG 2.2 AA `accessibility-gates` job) so the doc must name it.
            "skeleton",
            "400 ms",
            "2 s",
            "< 1.5 s p95",
            "< 3 s p99",
            "≤ 5 s p95",
            "< 500 ms p95",
            "reference_pending",
            "WCAG 2.2 AA",
            "AccessibilityContractSweepTests",
            "accessibility-gates",
        })
        {
            console.ShouldContain(required, Case.Sensitive);
        }

        // Story 8.4 flipped the prior "no new accessibility CI gate" posture: the doc now records the automated
        // axe / WCAG 2.2 AA CI gate, while no new *performance* CI gate is introduced.
        NormalizeWhitespace(console).ShouldContain("axe / WCAG 2.2 AA CI gate", Case.Sensitive);
        NormalizeWhitespace(console).ShouldContain("No new performance CI gate", Case.Sensitive);
    }

    [Fact]
    public void IncidentDocPinsLastResortReadGuardrailsAndReferencePending()
    {
        string incident = ReadText(IncidentDocPath);
        foreach (string required in new[]
        {
            "/_admin/incident-stream?folder=",
            "last-resort read",
            "eventstore:permission=admin",
            "ConsoleErrorPanel",
            // Three F-6 guardrails.
            "persistent degraded-mode banner",
            "`Freshness.ObservedAt`",
            "`unknown`",
            "never `0001-01-01`",
            "beside the raw technical state transition",
            "One-click copy of `correlationId`",
            // As-built honesty.
            "Redaction does not relax in incident mode",
            "IClient.ListOperationTimelineAsync",
            "projection-independent authoritative event-stream read",
            "`reference_pending`",
            // Health endpoints + observe-only + external alerting.
            "/health/live",
            "/health/ready",
            "degraded-but-serving",
            "observe-only",
            "OUTSIDE this repository",
        })
        {
            incident.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void RecoveryDocPinsAuthoritativeRecordsAndNoAutomation()
    {
        string incident = ReadText(IncidentDocPath);
        foreach (string required in new[]
        {
            "Authoritative records",
            "audit metadata",
            "commit idempotency records",
            "rebuildable from the event streams",
            "working copies are disposable cache",
            "acceptably lost on restart",
            "locks, idempotency records, and in-flight reconciliation tasks",
            "ships no backup automation",
            "Point-in-time recovery",
            "deferred",
            // Cleanup-status-without-repair.
            "GetWorkspaceCleanupStatus",
            "`status_only`",
            "no repair, unlock, or discard",
            // Unknown-outcome reconciliation.
            "unknown_provider_outcome` (code `71`)",
            "reconciliation_required` (code `72`)",
            "awaiting_human",
            "no silent retry",
        })
        {
            incident.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void AllPublishedOperationsAuditDocsStayMetadataOnly()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocMetadataOnly(ReadText(doc));
        }

        // Each published ops doc must carry the operator boilerplate (gate command, metadata-only policy,
        // reviewer/rerun note, and the exact root-level submodule command — never the recursive form).
        foreach (string doc in RequiredDocs)
        {
            string text = ReadText(doc);
            foreach (string required in new[]
            {
                "pwsh ./tests/tools/run-operations-audit-docs-gates.ps1",
                ReportPath,
                "metadata-only",
                "reviewer",
                "rerun",
                SubmoduleCommand,
            })
            {
                text.ShouldContain(required, Case.Sensitive);
            }

            AssertNoRecursiveSubmoduleCommand(text);
        }

        // The leakage corpus's forbidden output surfaces must still cover the doc-relevant channels.
        HashSet<string> forbidden = ParseLeakageCorpusForbiddenSurfaces();
        foreach (string channel in new[] { "audit-records", "projections", "console-payloads" })
        {
            forbidden.ShouldContain(channel);
        }
    }

    [Fact]
    public void OperationsAuditDocsGateScriptFailsClosedAndEmitsBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);
        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "diagnostic_policy",
            "metadata-only",
            "Push-Location",
            "Pop-Location",
            "GATE-VACUOUS",
            "xunit",
            $"FullyQualifiedName~{ConformanceFqn}",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        AssertNoRecursiveSubmoduleCommand(script);
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiWireOperationsAuditDocsGate()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-operations-audit-docs-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        AssertNoRecursiveSubmoduleCommand(workflow);

        // Lane separation: the static gate belongs to contract-spine, never to a new ci.yml top-level lane.
        string ci = ReadText(CiWorkflowPath);
        ci.ShouldNotContain("run-operations-audit-docs-gates.ps1", Case.Sensitive);

        string baseline = ReadText(BaselineGatePath);
        baseline.ShouldContain(ConformanceFqn, Case.Sensitive);
    }

    [Fact]
    public void OperationsAuditDocsLatestReportStaysMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("operations-audit-docs");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeOperationsAuditDocsEvidence()
    {
        // 1. Missing doc must fail the existence assertion.
        Should.Throw<ShouldAssertException>(() => AssertDocExists("docs/operations/this-doc-does-not-exist.md"));

        // 2. A wrong inventory count must fail the equality assertion in both directions, using the real source.
        HashSet<string> kinds = ParseEnumMembers(OperationKindPath);
        HashSet<string> missingOne = new(kinds, StringComparer.Ordinal);
        missingOne.Remove(kinds.First());
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(missingOne, kinds, "missing op-kind"));
        HashSet<string> extraOne = new(kinds, StringComparer.Ordinal) { "PhantomKind" };
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(extraOne, kinds, "extra op-kind"));

        // 3. Leaked absolute paths, bearer tokens, and non-placeholder hosts must each fail the SAME scanner
        //    that guards the real docs — not a standalone assertion.
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("evidence at /home/runner/work/leaked.txt"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhY3RvciJ9.signaturesegment"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("dashboard https://grafana.contoso.com/incident"));

        // Approved placeholder forms must pass the same scanner.
        Should.NotThrow(() => AssertDocMetadataOnly(
            "incident console https://folders.localhost.test and dashboard https://localhost:17000"));

        // 4. Malformed JSON must fail to parse rather than pass vacuously.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"operations-audit-docs\", "));

        // 5. The forbidden recursive submodule command is detected; the exact root-level command is not.
        Should.Throw<ShouldAssertException>(() => AssertNoRecursiveSubmoduleCommand("git submodule update --init " + string.Concat("--", "recursive")));
        Should.NotThrow(() => AssertNoRecursiveSubmoduleCommand(SubmoduleCommand));
    }

    private static HashSet<string> ParseAuditFamilyOperations()
    {
        YamlMappingNode root = LoadSingleYamlDocument(SpinePath).ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode paths = Mapping(root, "paths");
        HashSet<string> methods = new(["get", "put", "post", "delete", "patch", "head", "options", "trace"], StringComparer.Ordinal);

        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in paths.Children)
        {
            if (pathEntry.Value is not YamlMappingNode pathItem)
            {
                continue;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                if (methodEntry.Key is not YamlScalarNode methodKey
                    || methodKey.Value is null
                    || !methods.Contains(methodKey.Value)
                    || methodEntry.Value is not YamlMappingNode operation)
                {
                    continue;
                }

                if (!operation.Children.TryGetValue(new YamlScalarNode("tags"), out YamlNode? tagNode)
                    || tagNode is not YamlSequenceNode tags
                    || !tags.Children.OfType<YamlScalarNode>().Any(tag => tag.Value == "audit"))
                {
                    continue;
                }

                if (operation.Children.TryGetValue(new YamlScalarNode("operationId"), out YamlNode? opId)
                    && opId is YamlScalarNode opIdScalar && opIdScalar.Value is not null)
                {
                    operations.Add(opIdScalar.Value);
                }
            }
        }

        return operations;
    }

    private static HashSet<string> ParseJsonPropertyNames(string relativePath)
        => JsonPropertyNamePattern().Matches(ReadText(relativePath))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseEnumMembers(string relativePath)
    {
        string source = ReadText(relativePath);
        int brace = source.IndexOf('{', StringComparison.Ordinal);
        brace.ShouldBeGreaterThanOrEqualTo(0, $"{relativePath} must declare an enum body.");
        int end = source.LastIndexOf('}');
        end.ShouldBeGreaterThan(brace, $"{relativePath} enum body must be closed.");

        return EnumMemberLine().Matches(source[(brace + 1)..end])
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ParseDispositionVocabulary()
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match match in DispositionLabelArm().Matches(ReadText(DispositionMapperPath)))
        {
            result[match.Groups[1].Value.ToLowerInvariant()] = match.Groups[2].Value;
        }

        return result;
    }

    private static HashSet<string> ParseTechnicalStates()
        => LifecycleStateArm().Matches(ReadText(DispositionMapperPath))
            .Select(static match => match.Groups[1].Value.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseRedactionVisibility()
        => EnumMemberValue().Matches(ReadText(RedactionVisibilityPath))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ParseObservabilitySignals()
    {
        YamlMappingNode spec = Mapping(LoadSingleYamlDocument(ObservabilityManifestPath).ShouldBeOfType<YamlMappingNode>(), "spec");
        Dictionary<string, string> signals = new(StringComparer.Ordinal);
        foreach (YamlMappingNode signal in Sequence(spec, "signals").Children.OfType<YamlMappingNode>())
        {
            signals[Scalar(signal, "name")] = Scalar(signal, "severity");
        }

        return signals;
    }

    private static HashSet<string> ParseRetentionDispositions()
    {
        string runbook = ReadText(TenantDeletionRunbookPath);
        int section = runbook.IndexOf("## Disposition matrix", StringComparison.Ordinal);
        section.ShouldBeGreaterThanOrEqualTo(0, "the tenant-deletion runbook must declare a disposition matrix.");
        int next = runbook.IndexOf("\n## ", section + 1, StringComparison.Ordinal);
        if (next < 0)
        {
            next = runbook.Length;
        }

        return DispositionMatrixRow().Matches(runbook[section..next])
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ParseLeakageCorpusForbiddenSurfaces()
    {
        using JsonDocument document = JsonDocument.Parse(ReadText(LeakageCorpusPath));
        return document.RootElement.GetProperty("forbidden_output_surfaces")
            .EnumerateArray()
            .Select(static element => element.GetString().ShouldNotBeNull())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> FirstColumnBacktickTokens(string relativePath, string marker)
    {
        string table = ExtractMarkerTable(ReadText(relativePath), marker);
        HashSet<string> tokens = new(StringComparer.Ordinal);
        foreach (string line in table.Split('\n'))
        {
            Match match = FirstBacktickToken().Match(line);
            if (match.Success)
            {
                tokens.Add(match.Groups[1].Value);
            }
        }

        return tokens;
    }

    private static string ExtractMarkerTable(string doc, string marker)
    {
        int index = doc.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Missing marker '{marker}'.");
        string[] lines = doc[(index + marker.Length)..].Split('\n');
        List<string> table = [];
        bool started = false;
        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith('|'))
            {
                started = true;
                table.Add(line);
            }
            else if (started)
            {
                break;
            }
        }

        table.Count.ShouldBeGreaterThan(0, $"Marker '{marker}' must be followed by a table.");
        return string.Join('\n', table);
    }

    private static string ExtractMarkerBlock(string doc, string marker)
    {
        int index = doc.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Missing marker '{marker}'.");
        int start = index + marker.Length;
        int nextMarker = doc.IndexOf("\n<!--", start, StringComparison.Ordinal);
        int nextHeading = doc.IndexOf("\n## ", start, StringComparison.Ordinal);
        int end = doc.Length;
        if (nextMarker >= 0)
        {
            end = Math.Min(end, nextMarker);
        }

        if (nextHeading >= 0)
        {
            end = Math.Min(end, nextHeading);
        }

        return doc[start..end];
    }

    private static void AssertDocExists(string relativePath)
        => File.Exists(RepositoryPath(relativePath)).ShouldBeTrue(relativePath);

    private static void AssertSetEquals(IEnumerable<string> actual, IEnumerable<string> expected, string because)
        => actual.OrderBy(static value => value, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(static value => value, StringComparer.Ordinal), because);

    private static void AssertDocMetadataOnly(string text)
    {
        HostAbsolutePathPattern().IsMatch(text).ShouldBeFalse($"Ops/audit doc must not contain a host-absolute path: {Excerpt(text)}");
        SecretMaterialPattern().IsMatch(text).ShouldBeFalse($"Ops/audit doc must not contain secret/credential material: {Excerpt(text)}");

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
            allowed.ShouldBeTrue($"Ops/audit doc must use placeholder hosts, not real host '{host}'.");
        }
    }

    private static void AssertNoRecursiveSubmoduleCommand(string text)
        => text.Contains(string.Concat("--", "recursive"), StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse("ops/audit evidence must never request nested submodule initialization.");

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

    private static YamlNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, relativePath);
        return stream.Documents[0].RootNode;
    }

    private static string Scalar(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    private static YamlMappingNode Mapping(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode Sequence(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static string NormalizeWhitespace(string text)
        => WhitespaceRun().Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), " ").Trim();

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

    [GeneratedRegex("JsonPropertyName\\(\"([a-zA-Z]+)\"\\)")]
    private static partial Regex JsonPropertyNamePattern();

    [GeneratedRegex(@"^\s*([A-Za-z][A-Za-z0-9]*)\s*(?:=\s*\d+\s*)?,", RegexOptions.Multiline)]
    private static partial Regex EnumMemberLine();

    [GeneratedRegex(@"OperatorDispositionLabel\.([A-Za-z_]+)\s*=>\s*""([^""]+)""")]
    private static partial Regex DispositionLabelArm();

    [GeneratedRegex(@"LifecycleState\.([A-Za-z_]+)\s*=>")]
    private static partial Regex LifecycleStateArm();

    [GeneratedRegex(@"EnumMember\(Value = ""([^""]+)""\)")]
    private static partial Regex EnumMemberValue();

    [GeneratedRegex(@"^\|[^|]*\|\s*(retained|tombstoned|deleted|anonymized)\s*\|", RegexOptions.Multiline)]
    private static partial Regex DispositionMatrixRow();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex FirstBacktickToken();

    [GeneratedRegex(@"^\s*\d+\.\s+\*\*", RegexOptions.Multiline)]
    private static partial Regex NumberedListItem();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRun();

    // The drive-letter clause requires the letter not to be preceded by another letter, so a URL scheme such
    // as "https:/" is not mistaken for a "C:\" Windows path while a real "C:\Users" path still matches.
    [GeneratedRegex(@"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY|AccountKey=|client_secret\s*[:=]\s*\S|xox[baprs]-|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.", RegexOptions.CultureInvariant)]
    private static partial Regex SecretMaterialPattern();

    [GeneratedRegex(@"https?://([^/\s)""'\]]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPattern();
}
