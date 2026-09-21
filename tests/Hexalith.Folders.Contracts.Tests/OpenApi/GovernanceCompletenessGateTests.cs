using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class GovernanceCompletenessGateTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string EvidencePath = Path.Combine(RepositoryRoot, "docs", "exit-criteria", "c0-c13-governance-evidence.yaml");
    private static readonly string C7DecisionPath = Path.Combine(RepositoryRoot, "docs", "exit-criteria", "c7-lock-authorization-timing.md");
    private static readonly string Oq8DesignPath = Path.Combine(RepositoryRoot, "docs", "exit-criteria", "oq8-idempotency-design.md");
    private static readonly string Oq8EvidencePath = Path.Combine(RepositoryRoot, "docs", "exit-criteria", "oq8-idempotency-evidence.yaml");
    private static readonly string Oq2PolicyPath = Path.Combine(RepositoryRoot, "docs", "contract", "file-context-contract-groups.md");
    private static readonly string Oq2EvidencePath = Path.Combine(RepositoryRoot, "docs", "contract", "oq2-file-policy-evidence.yaml");
    private static readonly string Oq3MatrixPath = Path.Combine(RepositoryRoot, "docs", "contract", "authorization-matrix.md");
    private static readonly string Oq3EvidencePath = Path.Combine(RepositoryRoot, "docs", "contract", "oq3-authorization-evidence.yaml");
    private static readonly string Oq4CatalogPath = Path.Combine(RepositoryRoot, "docs", "contract", "provider-compatibility-catalog.md");
    private static readonly string Oq4EvidencePath = Path.Combine(RepositoryRoot, "docs", "contract", "oq4-provider-compatibility-evidence.yaml");
    private static readonly string CorpusPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus.json");
    private static readonly string CorpusSchemaPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus.schema.json");
    private static readonly string CorpusConsumptionPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus-consumption.yaml");
    private static readonly string PatternManifestPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "pattern-example-manifest.yaml");
    private static readonly string CacheKeyExceptionsPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "cache-key-exceptions.yaml");
    private static readonly string ParityContractPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml");
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");
    private static readonly string V2ConformanceSetPath = Path.Combine(RepositoryRoot, "_bmad-output", "planning-artifacts", "generated-v2-conformance-set-2026-09-17.yaml");
    private static readonly string ApprovalRegisterPath = Path.Combine(RepositoryRoot, "_bmad-output", "planning-artifacts", "planning-authority-relock-approval-register.yaml");
    private static readonly string WorkflowPath = Path.Combine(RepositoryRoot, ".github", "workflows", "contract-spine.yml");
    private static readonly string GateScriptPath = Path.Combine(RepositoryRoot, "tests", "tools", "run-governance-completeness-gates.ps1");
    private static readonly string GateDocumentationPath = Path.Combine(RepositoryRoot, "docs", "contract", "governance-and-completeness-ci-gates.md");
    private static readonly string GateReportPath = Path.Combine(RepositoryRoot, "_bmad-output", "gates", "governance-completeness", "latest.json");
    private static readonly string SolutionPath = Path.Combine(RepositoryRoot, "Hexalith.Folders.slnx");

    private static readonly Regex MarkerPattern = new(
        @"^<!-- hexalith-example: [a-z][a-z0-9-]* -->$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AbsoluteWindowsDrivePathPattern = new(
        @"[A-Za-z]:\\",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Lazy<CorpusSchemaConstraints> CorpusConstraints = new(LoadCorpusSchemaConstraints);

    private const string ApprovedC7Version = "1.0.0";
    private const string ApprovedC7Sha256 = "47da9d95b5d809a08b0c6097fc9ad97e8bd22e156ee1789f98870345c6be4403";
    private const string ApprovedOq2Version = "1.1.0";
    private const string ApprovedOq2Sha256 = "b7123536cc243c52e853980313ff8ebee5abb9936b331e71f30ad6d554975713";
    private const string ApprovedOq2Date = "2026-09-14";
    private const string ApprovedOq2ReopenPolicy = "Any change to the canonical policy content, policy version, SHA-256 digest, required authority set, approver identity, or approval date reopens PM, Architecture, and Security approval.";
    private const string ApprovedOq3Version = "1.0.0";
    private const string ApprovedOq3Sha256 = "5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7";
    private const string ApprovedOq3Date = "2026-09-14";
    private const string ApprovedOq3ReopenPolicy = "Any change to the canonical matrix content, matrix version, SHA-256 digest, required authority set, approver identity, or approval date reopens Security and PM approval.";
    private const string ApprovedOq4Version = "1.0.0";
    private const string ApprovedOq4Sha256 = "5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a";
    private const string ApprovedOq4Date = "2026-09-15";
    private const string ApprovedOq4ReopenPolicy = "Any change to the canonical catalog content, catalog version, SHA-256 digest, required authority set, approver identity, or approval date reopens Provider, Architecture, and PM approval.";
    private const string ApprovedOq4C12EvidenceStandard = "C12 is closed on hermetic-PR-gate provider contract evidence plus scheduled containerized and fixture drift evidence; credentialed live provider runs against GitHub and Forgejo are reported as explicitly not run and remain residual provider-ready debt.";

    // Stories the OQ3 design approval explicitly does not complete. Shortening this list is a false
    // completion claim, not a cleanup.
    private static readonly string[] ApprovedOq3IncompleteStories =
    [
        "12.1",
        "4.19",
        "4.20",
        "4.21",
        "6.14",
        "10.8",
    ];

    // Conformance gaps the approved matrix records rather than disguises.
    private static readonly string[] ApprovedOq3GapIds =
    [
        "G1",
        "G2",
        "G3",
        "G4",
        "G5",
        "G6",
        "G7",
        "G8",
        "G9",
        "G10",
        "G11",
    ];

    // Planning artifacts that restate the approved OQ3 digest by hand. Without this pin a future matrix
    // revision would leave all three silently stale.
    private static readonly string[] Oq3DigestBoundPlanningArtifacts =
    [
        "_bmad-output/planning-artifacts/prd.md",
        "_bmad-output/planning-artifacts/.memlog.md",
    ];

    // Providers the approved OQ4 catalog governs. Adding a provider is catalog work, not a gate edit.
    private static readonly string[] ApprovedOq4GovernedProviders = ["github", "forgejo"];

    // Call ceilings the approved catalog publishes. Every one of them either cites an enforcing constant
    // or is recorded as a numbered gap; ProviderCompatibilityCatalogContractTests proves that property.
    private static readonly string[] ApprovedOq4CeilingIds =
    [
        "CC1",
        "CC2",
        "CC3",
        "CC4",
        "CC5",
        "CC6",
        "CC7",
        "CC8",
        "CC9",
        "CC10",
        "CC11",
        "CC12",
    ];

    // Unenforced ceilings the approved catalog records rather than disguises.
    private static readonly string[] ApprovedOq4GapIds = ["PG1", "PG2", "PG3"];

    // Stories the OQ4 catalog approval explicitly does not complete. Shortening this list is a false
    // completion claim, not a cleanup.
    private static readonly string[] ApprovedOq4IncompleteStories =
    [
        "3.3",
        "3.14",
        "12.1",
        "12.3",
    ];

    // Artifacts whose exact bytes the OQ4 approval digest depends on, or that the drift lane reads verbatim.
    // Each must stay LF-pinned in `.gitattributes` or the digest stops being reproducible.
    private static readonly string[] Oq4LineEndingPinnedPaths =
    [
        "docs/contract/provider-compatibility-catalog.md",
        "docs/contract/oq4-provider-compatibility-evidence.yaml",
        "tests/contracts/github/pinned-profile.json",
    ];

    // Planning artifacts that restate the approved OQ4 digest by hand.
    private static readonly string[] Oq4DigestBoundPlanningArtifacts =
    [
        "_bmad-output/planning-artifacts/prd.md",
        "_bmad-output/planning-artifacts/.memlog.md",
    ];

    private static readonly string[] Criteria =
    [
        "C0",
        "C1",
        "C2",
        "C3",
        "C4",
        "C5",
        "C6",
        "C7",
        "C8",
        "C9",
        "C10",
        "C11",
        "C12",
        "C13",
    ];

    // Criteria whose `approved` status rests on a human governance sign-off (not a machine-validated
    // gate). Each MUST carry a well-formed `approval` block; the block cannot be silently dropped to
    // dodge the freshness/exact-record checks. Extend this set whenever a new criterion becomes
    // approval-backed, in lockstep with adding its `approval` block to the evidence YAML.
    private static readonly string[] ApprovalBackedCriteria =
    [
        "C3",
        "C4",
        "C7",
        "C12",
    ];

    [Fact]
    public void WorkflowAndScriptExposeOneOfflineGovernanceCompletenessCommand()
    {
        string workflow = File.ReadAllText(WorkflowPath);
        string script = File.ReadAllText(GateScriptPath);
        string documentation = File.ReadAllText(GateDocumentationPath);
        const string c7DecisionPath = "docs/exit-criteria/c7-lock-authorization-timing.md";

        workflow.ShouldContain("./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild");
        workflow.ShouldContain("actions/checkout@v6");
        workflow.ShouldContain("submodules: false");
        workflow.ShouldContain("actions/setup-dotnet@v5");
        workflow.ShouldContain("global-json-file: global.json");
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll");
        script.ShouldContain("dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true");
        script.ShouldContain("dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror");
        script.ShouldContain("'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests'", Case.Sensitive);
        script.ShouldContain("'Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests'", Case.Sensitive);
        script.ShouldContain("docs/contract/authorization-matrix.md", Case.Sensitive);
        script.ShouldContain("docs/contract/oq3-authorization-evidence.yaml", Case.Sensitive);
        script.ShouldContain("'Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests'", Case.Sensitive);
        script.ShouldContain("foreach ($testClass in $governanceClasses)", Case.Sensitive);
        script.ShouldContain("-class $testClass", Case.Sensitive);
        script.ShouldContain("class selection executed zero tests", Case.Sensitive);
        script.ShouldContain("docs/contract/provider-compatibility-catalog.md", Case.Sensitive);
        script.ShouldContain("docs/contract/oq4-provider-compatibility-evidence.yaml", Case.Sensitive);
        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/bin/Release");
        script.ShouldContain("tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj");
        script.ShouldContain("_bmad-output/gates/governance-completeness/latest.json");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("utf8NoBOM");
        script.ShouldContain(c7DecisionPath, Case.Sensitive);
        script.ShouldNotContain("--filter", Case.Sensitive);
        script.ShouldNotContain("--recursive", Case.Insensitive);

        documentation.ShouldContain(".\\tests\\tools\\run-governance-completeness-gates.ps1");
        documentation.ShouldContain("prerequisite_drift");
        documentation.ShouldContain("idempotency_sample_unmapped");
        documentation.ShouldContain("cache_key_unscoped");
        documentation.ShouldContain("parity_completeness_mismatch");
        documentation.ShouldContain("approval_record_missing");
        documentation.ShouldContain("approval_authority_unsatisfied");
        documentation.ShouldContain("approval_approver_generic");
        documentation.ShouldContain("approval_date_invalid");
        documentation.ShouldContain("approval_date_future");
        documentation.ShouldContain("approval_stale");
        documentation.ShouldContain("approval_evidence_version_mismatch");
        documentation.ShouldContain("approval_evidence_digest_missing");
        documentation.ShouldContain("approval_evidence_digest_mismatch");
        documentation.ShouldContain("c7_timing_profile_invalid");
        documentation.ShouldContain("c7_approval_identity_mismatch");
        documentation.ShouldContain("c7_approval_date_mismatch");
        documentation.ShouldContain("oq2_evidence_missing");
        documentation.ShouldContain("oq2_evidence_mismatch");
        documentation.ShouldContain("oq2_approval_incomplete");
        documentation.ShouldContain("oq2_approval_extra");
        documentation.ShouldContain("oq2_approval_identity_mismatch");
        documentation.ShouldContain("oq2_approval_date_mismatch");
        documentation.ShouldContain("oq3_evidence_missing");
        documentation.ShouldContain("oq3_evidence_mismatch");
        documentation.ShouldContain("oq3_approval_incomplete");
        documentation.ShouldContain("oq3_approval_extra");
        documentation.ShouldContain("oq3_approval_identity_mismatch");
        documentation.ShouldContain("oq3_approval_date_mismatch");
        documentation.ShouldContain("oq3_matrix_mismatch");
        documentation.ShouldContain("oq3_operation_unmapped");
        documentation.ShouldContain("oq3_operation_unknown");
        documentation.ShouldContain("oq3_operation_duplicate");
        documentation.ShouldContain("oq3_family_uncovered");
        documentation.ShouldContain("oq3_actor_uncovered");
        documentation.ShouldContain("oq3_scope_incomplete");
        documentation.ShouldContain("oq3_denial_shape_mismatch");
        documentation.ShouldContain("oq3_gap_unrecorded");
        documentation.ShouldContain("oq4_evidence_missing");
        documentation.ShouldContain("oq4_evidence_mismatch");
        documentation.ShouldContain("oq4_approval_incomplete");
        documentation.ShouldContain("oq4_approval_extra");
        documentation.ShouldContain("oq4_approval_identity_mismatch");
        documentation.ShouldContain("oq4_approval_date_mismatch");
        documentation.ShouldContain("oq4_catalog_section_missing");
        documentation.ShouldContain("oq4_catalog_section_duplicate");
        documentation.ShouldContain("oq4_ceiling_missing");
        documentation.ShouldContain("oq4_ceiling_duplicate");
        documentation.ShouldContain("oq4_ceiling_unpinned");
        documentation.ShouldContain("oq4_ceiling_provider_unknown");
        documentation.ShouldContain("oq4_gap_missing");
        documentation.ShouldContain("oq4_gap_duplicate");
        documentation.ShouldContain("oq4_readiness_row_missing");
        documentation.ShouldContain("oq4_readiness_row_duplicate");
        documentation.ShouldContain("docs/contract/file-context-contract-groups.md", Case.Sensitive);
        documentation.ShouldContain("docs/contract/oq2-file-policy-evidence.yaml", Case.Sensitive);
        documentation.ShouldContain("docs/contract/authorization-matrix.md", Case.Sensitive);
        documentation.ShouldContain("docs/contract/oq3-authorization-evidence.yaml", Case.Sensitive);
        documentation.ShouldContain("docs/contract/provider-compatibility-catalog.md", Case.Sensitive);
        documentation.ShouldContain("docs/contract/oq4-provider-compatibility-evidence.yaml", Case.Sensitive);
        documentation.ShouldContain(c7DecisionPath, Case.Sensitive);
        AssertMetadataOnly(documentation);

        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(GateReportPath));
        report.RootElement.GetProperty("canonical_inputs").EnumerateArray()
            .Select(item => item.GetString().ShouldNotBeNull()).ShouldContain(c7DecisionPath);
        string[] reportInputs = report.RootElement.GetProperty("canonical_inputs").EnumerateArray()
            .Select(item => item.GetString().ShouldNotBeNull()).ToArray();
        reportInputs.ShouldContain("docs/contract/file-context-contract-groups.md");
        reportInputs.ShouldContain("docs/contract/oq2-file-policy-evidence.yaml");
        reportInputs.ShouldContain("docs/contract/authorization-matrix.md");
        reportInputs.ShouldContain("docs/contract/oq3-authorization-evidence.yaml");
        reportInputs.ShouldContain("docs/contract/provider-compatibility-catalog.md");
        reportInputs.ShouldContain("docs/contract/oq4-provider-compatibility-evidence.yaml");
        reportInputs.ShouldContain("src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml");
    }

    [Fact]
    public void ExitCriteriaEvidenceMapsEveryC0ThroughC13WithBoundedReferencePendingRows()
    {
        YamlMappingNode root = LoadYamlMapping(EvidencePath);
        YamlMappingNode[] rows = RequiredSequence(root, "criteria").Children.Cast<YamlMappingNode>().ToArray();
        GateDiagnostic[] diagnostics = EvaluateExitCriteriaRows(rows);

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
        rows.Select(row => RequiredScalar(row, "criterion_id")).ToArray().ShouldBe(Criteria, ignoreOrder: true);

        foreach (YamlMappingNode row in rows)
        {
            string criterion = RequiredScalar(row, "criterion_id");
            string status = RequiredScalar(row, "status");
            string artifact = RequiredScalar(row, "artifact_path");
            string command = RequiredScalar(row, "verification_command");
            string summary = RequiredScalar(row, "result_summary");

            status.ShouldBeOneOf("approved", "reference_pending");
            PathExists(artifact).ShouldBeTrue(artifact);
            command.ShouldNotBeNullOrWhiteSpace();
            summary.ShouldNotBeNullOrWhiteSpace();
            AssertMetadataOnly(command);
            AssertMetadataOnly(summary);

            if (status == "reference_pending")
            {
                YamlMappingNode[] placeholders = RequiredSequence(row, "open_policy_placeholders")
                    .Children.Cast<YamlMappingNode>()
                    .ToArray();
                placeholders.Length.ShouldBeGreaterThan(0, criterion);

                foreach (YamlMappingNode placeholder in placeholders)
                {
                    RequiredScalar(placeholder, "id").ShouldNotBeNullOrWhiteSpace();
                    RequiredScalar(placeholder, "owner").ShouldNotBeNullOrWhiteSpace();
                    RequiredScalar(placeholder, "reason").ShouldNotBeNullOrWhiteSpace();
                    RequiredScalar(placeholder, "verification_gap").ShouldNotBeNullOrWhiteSpace();
                    RequiredScalar(placeholder, "consuming_story").ShouldNotBeNullOrWhiteSpace();
                    AssertMetadataOnly(RequiredScalar(placeholder, "verification_gap"));
                }
            }
        }
    }

    [Fact]
    public void ExitCriteriaNegativeControlsFailClosedWithBoundedDiagnostics()
    {
        YamlMappingNode[] rows = LoadCriteriaRows(EvidencePath);
        YamlMappingNode[] missing = rows.Where(row => RequiredScalar(row, "criterion_id") != "C13").ToArray();
        string clonedCriterionId = RequiredScalar(rows[0], "criterion_id");
        YamlMappingNode[] duplicate = rows.Concat([CloneRow(rows[0])]).ToArray();
        YamlMappingNode invalidPlaceholder = CloneRow(rows[0]);
        SetScalar(invalidPlaceholder, "owner", "PLACEHOLDER");
        YamlMappingNode invalidPath = CloneRow(rows[1]);
        SetScalar(invalidPath, "artifact_path", "D:/not/repository/local.md");

        EvaluateExitCriteriaRows(missing).ShouldContain(d => d.Category == "exit_criteria_missing" && d.Identifier == "C13");
        EvaluateExitCriteriaRows(duplicate).ShouldContain(d => d.Category == "exit_criteria_duplicate" && d.Identifier == clonedCriterionId);
        EvaluateExitCriteriaRows([invalidPlaceholder]).ShouldContain(d => d.Category == "exit_criteria_malformed");
        EvaluateExitCriteriaRows([invalidPath]).ShouldContain(d => d.Category == "artifact_path_invalid");

        foreach (GateDiagnostic diagnostic in EvaluateExitCriteriaRows([invalidPlaceholder, invalidPath]))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void ApprovalBackedCriteriaCarryFreshExactApprovalRecords()
    {
        YamlMappingNode root = LoadYamlMapping(EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(root);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        YamlMappingNode[] rows = RequiredSequence(root, "criteria").Children.Cast<YamlMappingNode>().ToArray();

        // The mandatory global freshness window must be a positive number of days.
        policy.MaxAgeDays.ShouldBeGreaterThan(0);
        policy.GenericApproverTokens.ShouldNotBeEmpty();

        // Every pinned approval-backed criterion must be `approved` and must carry an `approval` block —
        // the block cannot be dropped to escape the checks.
        foreach (string criterion in ApprovalBackedCriteria)
        {
            YamlMappingNode row = rows.Single(r => RequiredScalar(r, "criterion_id") == criterion);
            RequiredScalar(row, "status").ShouldBe("approved", criterion);
            HasApprovalBlock(row).ShouldBeTrue($"{criterion} must carry a structured approval block");
        }

        // Validate every row that declares an approval block (pinned or future); C7 uses the stricter bounded
        // exact-value evaluator below so unexpected authority, signer, date, and count values cannot be echoed.
        GateDiagnostic[] diagnostics = rows
            .Where(row => HasApprovalBlock(row) && RequiredScalar(row, "criterion_id") != "C7")
            .SelectMany(row => EvaluateApprovalRecords(row, policy, today))
            .ToArray();

        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void ApprovalRecordNegativeControlsFailClosedWithBoundedDiagnostics()
    {
        ApprovalPolicy policy = SyntheticApprovalPolicy();
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        string fresh = today.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string future = today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string stale = today.AddDays(-(policy.MaxAgeDays + 30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // A criterion with no approval block at all.
        GateDiagnostic[] missingBlock = EvaluateApprovalRecords(RowWithoutApproval("C-MISSING"), policy, today);
        missingBlock.ShouldContain(d => d.Category == "approval_record_missing" && d.Identifier == "C-MISSING");

        // A generic approver ("Legal") standing in for a named signer.
        GateDiagnostic[] generic = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-GENERIC", ["Legal"], [("Legal", "Legal", fresh)]), policy, today);
        generic.ShouldContain(d => d.Category == "approval_approver_generic" && d.Identifier == "C-GENERIC:Legal");

        // A future-dated approval.
        GateDiagnostic[] futureDated = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-FUTURE", ["PM"], [("PM", "Jerome", future)]), policy, today);
        futureDated.ShouldContain(d => d.Category == "approval_date_future" && d.Identifier == "C-FUTURE:PM");

        // A malformed approval date.
        GateDiagnostic[] malformed = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-MALFORMED", ["PM"], [("PM", "Jerome", "2026-13-40")]), policy, today);
        malformed.ShouldContain(d => d.Category == "approval_date_invalid" && d.Identifier == "C-MALFORMED:PM");

        // A required authority (Legal) with no record.
        GateDiagnostic[] unsatisfied = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-UNSAT", ["PM", "Legal"], [("PM", "Jerome", fresh)]), policy, today);
        unsatisfied.ShouldContain(d => d.Category == "approval_authority_unsatisfied" && d.Identifier == "C-UNSAT:Legal");

        // A stale approval older than the mandatory global max-age window.
        GateDiagnostic[] staleDiagnostics = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-STALE", ["PM"], [("PM", "Jerome", stale)]), policy, today);
        staleDiagnostics.ShouldContain(d => d.Category == "approval_stale" && d.Identifier == "C-STALE:PM");

        // A per-criterion review_by date that has already passed.
        GateDiagnostic[] reviewExpired = EvaluateApprovalRecords(
            SyntheticApprovalRow("C-REVIEW", ["PM"], [("PM", "Jerome", fresh)], reviewBy: stale), policy, today);
        reviewExpired.ShouldContain(d => d.Category == "approval_stale" && d.Identifier == "C-REVIEW");

        // A fully valid, fresh, exactly-recorded approval produces no diagnostics.
        EvaluateApprovalRecords(
            SyntheticApprovalRow("C-OK", ["PM", "Legal"], [("PM", "Jerome", fresh), ("Legal", "Jérôme Piquot", fresh)]),
            policy, today).ShouldBeEmpty();

        foreach (GateDiagnostic diagnostic in missingBlock
            .Concat(generic).Concat(futureDated).Concat(malformed)
            .Concat(unsatisfied).Concat(staleDiagnostics).Concat(reviewExpired))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void C7DecisionPackageBindsProfileVersionDigestAndExactApprovals()
    {
        File.Exists(C7DecisionPath).ShouldBeTrue("C7 requires a canonical timing decision artifact.");

        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(C7DecisionPath)));
        actualDigest.ShouldBe(ApprovedC7Sha256, "C7 artifact changes require a new version, digest, and fresh approvals.");

        YamlMappingNode root = LoadYamlMapping(EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(root);
        YamlMappingNode c7 = RequiredSequence(root, "criteria").Children.Cast<YamlMappingNode>()
            .Single(row => RequiredScalar(row, "criterion_id") == "C7");

        GateDiagnostic[] diagnostics = EvaluateC7DecisionEvidence(
            c7,
            actualDigest,
            policy,
            DateOnly.FromDateTime(DateTime.UtcNow));

        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
        RequiredScalar(c7, "status").ShouldBe("approved");
        RequiredScalar(c7, "artifact_path").ShouldBe("docs/exit-criteria/c7-lock-authorization-timing.md");
        RequiredScalar(c7, "evidence_version").ShouldBe(ApprovedC7Version);
        RequiredScalar(c7, "evidence_sha256").ShouldBe(ApprovedC7Sha256);

    }

    [Fact]
    public void C7DecisionNegativeControlsFailClosedWithBoundedDiagnostics()
    {
        YamlMappingNode root = LoadYamlMapping(EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(root);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        YamlMappingNode c7 = RequiredSequence(root, "criteria").Children.Cast<YamlMappingNode>()
            .Single(row => RequiredScalar(row, "criterion_id") == "C7");

        YamlMappingNode missingDigest = CloneRow(c7);
        missingDigest.Children.Remove(new YamlScalarNode("evidence_sha256"));
        GateDiagnostic[] missingDigestDiagnostics = EvaluateC7DecisionEvidence(
            missingDigest, ApprovedC7Sha256, policy, today);
        missingDigestDiagnostics.ShouldContain(d =>
            d.Category == "approval_evidence_digest_missing" && d.Identifier == "C7");

        YamlMappingNode mismatchedDigest = CloneRow(c7);
        SetScalar(mismatchedDigest, "evidence_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedDigestDiagnostics = EvaluateC7DecisionEvidence(
            mismatchedDigest, ApprovedC7Sha256, policy, today);
        mismatchedDigestDiagnostics.ShouldContain(d =>
            d.Category == "approval_evidence_digest_mismatch" && d.Identifier == "C7");

        YamlMappingNode incompleteAuthority = CloneRow(c7);
        YamlSequenceNode approvalRecords = RequiredSequence(RequiredMapping(incompleteAuthority, "approval"), "records");
        YamlNode securityRecord = approvalRecords.Children.Cast<YamlMappingNode>()
            .Single(record => RequiredScalar(record, "authority") == "Security");
        approvalRecords.Children.Remove(securityRecord);
        GateDiagnostic[] incompleteAuthorityDiagnostics = EvaluateC7DecisionEvidence(
            incompleteAuthority, ApprovedC7Sha256, policy, today);
        incompleteAuthorityDiagnostics.ShouldContain(d =>
            d.Category == "approval_authority_unsatisfied" && d.Identifier == "C7:Security");

        YamlMappingNode invalidTiming = CloneRow(c7);
        SetScalar(RequiredMapping(invalidTiming, "timing_profile"), "authorization_revalidation_interval_seconds", "61");
        GateDiagnostic[] invalidTimingDiagnostics = EvaluateC7DecisionEvidence(
            invalidTiming, ApprovedC7Sha256, policy, today);
        invalidTimingDiagnostics.ShouldContain(d =>
            d.Category == "c7_timing_profile_invalid" && d.Identifier == "C7");

        const string unexpectedValue = "tenant-secret-unexpected-value";
        YamlMappingNode unexpectedTimingKey = CloneRow(c7);
        RequiredMapping(unexpectedTimingKey, "timing_profile").Add(
            new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("7"));
        GateDiagnostic[] unexpectedTimingDiagnostics = EvaluateC7DecisionEvidence(
            unexpectedTimingKey, ApprovedC7Sha256, policy, today);
        unexpectedTimingDiagnostics.ShouldContain(d =>
            d.Category == "c7_timing_profile_invalid" && d.Identifier == "C7");

        YamlMappingNode unexpectedAuthority = CloneRow(c7);
        YamlMappingNode unexpectedApproval = RequiredMapping(unexpectedAuthority, "approval");
        RequiredSequence(unexpectedApproval, "required_authorities").Add(new YamlScalarNode(unexpectedValue));
        RequiredSequence(unexpectedApproval, "records").Add(new YamlMappingNode(
            new YamlScalarNode("authority"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approver"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approved_on"), new YamlScalarNode("2026-09-12"),
            new YamlScalarNode("evidence_version"), new YamlScalarNode(ApprovedC7Version),
            new YamlScalarNode("evidence_sha256"), new YamlScalarNode(ApprovedC7Sha256)));
        GateDiagnostic[] unexpectedAuthorityDiagnostics = EvaluateC7DecisionEvidence(
            unexpectedAuthority, ApprovedC7Sha256, policy, today);
        unexpectedAuthorityDiagnostics.ShouldContain(d =>
            d.Category == "approval_authority_unsatisfied" && d.Identifier == "C7:required-authorities");
        unexpectedAuthorityDiagnostics.ShouldContain(d =>
            d.Category == "approval_authority_unsatisfied" && d.Identifier == "C7:record-count");

        YamlMappingNode unexpectedSigner = CloneRow(c7);
        YamlMappingNode signerRecord = RequiredSequence(RequiredMapping(unexpectedSigner, "approval"), "records")
            .Children.Cast<YamlMappingNode>().Single(record => RequiredScalar(record, "authority") == "Architecture");
        SetScalar(signerRecord, "approver", unexpectedValue);
        GateDiagnostic[] unexpectedSignerDiagnostics = EvaluateC7DecisionEvidence(
            unexpectedSigner, ApprovedC7Sha256, policy, today);
        unexpectedSignerDiagnostics.ShouldContain(d =>
            d.Category == "c7_approval_identity_mismatch" && d.Identifier == "C7:Architecture");

        YamlMappingNode unexpectedDate = CloneRow(c7);
        YamlMappingNode dateRecord = RequiredSequence(RequiredMapping(unexpectedDate, "approval"), "records")
            .Children.Cast<YamlMappingNode>().Single(record => RequiredScalar(record, "authority") == "Security");
        SetScalar(dateRecord, "approved_on", "2099-12-31");
        GateDiagnostic[] unexpectedDateDiagnostics = EvaluateC7DecisionEvidence(
            unexpectedDate, ApprovedC7Sha256, policy, today);
        unexpectedDateDiagnostics.ShouldContain(d =>
            d.Category == "c7_approval_date_mismatch" && d.Identifier == "C7:Security");

        YamlMappingNode unexpectedRecordCount = CloneRow(c7);
        YamlSequenceNode records = RequiredSequence(RequiredMapping(unexpectedRecordCount, "approval"), "records");
        records.Add(CloneRow(records.Children.Cast<YamlMappingNode>().First()));
        GateDiagnostic[] unexpectedRecordCountDiagnostics = EvaluateC7DecisionEvidence(
            unexpectedRecordCount, ApprovedC7Sha256, policy, today);
        unexpectedRecordCountDiagnostics.ShouldContain(d =>
            d.Category == "approval_authority_unsatisfied" && d.Identifier == "C7:record-count");

        GateDiagnostic[] staleApprovalDiagnostics = EvaluateC7DecisionEvidence(
            c7, ApprovedC7Sha256, policy, today.AddDays(policy.MaxAgeDays + 1));
        staleApprovalDiagnostics.ShouldContain(d =>
            d.Category == "approval_stale" && d.Identifier == "C7:Architecture");
        staleApprovalDiagnostics.ShouldContain(d =>
            d.Category == "approval_stale" && d.Identifier == "C7:Security");

        foreach (GateDiagnostic diagnostic in missingDigestDiagnostics
            .Concat(mismatchedDigestDiagnostics)
            .Concat(incompleteAuthorityDiagnostics)
            .Concat(invalidTimingDiagnostics)
            .Concat(unexpectedTimingDiagnostics)
            .Concat(unexpectedAuthorityDiagnostics)
            .Concat(unexpectedSignerDiagnostics)
            .Concat(unexpectedDateDiagnostics)
            .Concat(unexpectedRecordCountDiagnostics)
            .Concat(staleApprovalDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
            diagnostic.ToString().ShouldNotContain(unexpectedValue, Case.Sensitive);
        }
    }

    [Fact]
    public void Oq8DesignPackageBindsApprovedDecisionsAndExactDigest()
    {
        File.Exists(Oq8DesignPath).ShouldBeTrue("OQ8 requires a canonical design decision document.");
        File.Exists(Oq8EvidencePath).ShouldBeTrue("OQ8 requires a versioned governance evidence manifest.");

        string design = File.ReadAllText(Oq8DesignPath);
        string[] requiredDesignStatements =
        [
            "managed tenant plus HMAC-SHA-256 key digest",
            "reserved -> pending -> terminal",
            "recoverable",
            "unknown_provider_outcome",
            "now >= expiresAt",
            "mutation replay result: PT24H",
            "commit replay result: P7Y",
            "managed-tenant lifetime plus 400 days",
            "HTTP 409",
            "CLI exit 76",
            "MCP kind `idempotency_key_expired`",
            "refresh_state_then_submit_with_new_key",
            "authorization and canonical validation before admission",
            "deterministic post-admission failures consume the key",
            "EventStore-owned admission actor",
        ];

        foreach (string statement in requiredDesignStatements)
        {
            design.ShouldContain(statement, Case.Sensitive);
        }

        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Oq8DesignPath)));
        YamlMappingNode evidence = LoadYamlMapping(Oq8EvidencePath);
        RequiredScalar(evidence, "schema_version").ShouldBe("1.0.0");
        RequiredScalar(evidence, "evidence_id").ShouldBe("OQ8");
        RequiredScalar(evidence, "status").ShouldBe("design-approved");
        RequiredScalar(evidence, "design_version").ShouldBe("1.0.0");
        RequiredScalar(evidence, "design_path").ShouldBe("docs/exit-criteria/oq8-idempotency-design.md");
        RequiredScalar(evidence, "design_sha256").ShouldBe(actualDigest);

        YamlMappingNode approval = RequiredMapping(evidence, "approval");
        RequiredSequence(approval, "required_authorities")
            .Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["Architecture", "Security", "Test"], ignoreOrder: true);

        YamlMappingNode[] records = RequiredSequence(approval, "records")
            .Children.Cast<YamlMappingNode>().ToArray();
        records.Length.ShouldBe(3);

        foreach (string authority in new[] { "Architecture", "Security", "Test" })
        {
            YamlMappingNode record = records.Single(row => RequiredScalar(row, "authority") == authority);
            RequiredScalar(record, "approver").ShouldBe("Administrator");
            RequiredScalar(record, "approved_on").ShouldBe("2026-07-19");
            RequiredScalar(record, "evidence_version").ShouldBe("1.0.0");
            RequiredScalar(record, "evidence_sha256").ShouldBe(actualDigest);
        }
    }

    [Fact]
    public void Oq2FilePolicyPackageBindsVersionDigestApprovalsAndRuntimePosture()
    {
        File.Exists(Oq2PolicyPath).ShouldBeTrue("OQ2 requires the canonical file-policy artifact.");
        File.Exists(Oq2EvidencePath).ShouldBeTrue("OQ2 requires a versioned governance evidence manifest.");

        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Oq2PolicyPath)));
        actualDigest.ShouldBe(ApprovedOq2Sha256, "OQ2 policy changes require a new version, digest, and fresh PM, Architecture, and Security approvals.");

        YamlMappingNode evidence = LoadYamlMapping(Oq2EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        GateDiagnostic[] diagnostics = EvaluateOq2Evidence(
            evidence,
            actualDigest,
            policy,
            DateOnly.FromDateTime(DateTime.UtcNow));

        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        string[] canonicalSurfaces = RequiredSequence(evidence, "canonical_surfaces").Children
            .Select(node => RequiredScalar(node, "canonical_surface"))
            .ToArray();
        canonicalSurfaces.ShouldBe(
        [
            "docs/contract/file-context-contract-groups.md",
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml",
            "src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        ]);

        YamlMappingNode runtime = RequiredMapping(evidence, "runtime_posture");
        RequiredScalar(runtime, "status").ShouldBe("incomplete");
        RequiredSequence(runtime, "incomplete_stories").Children
            .Select(node => RequiredScalar(node, "incomplete_story"))
            .ToArray().ShouldBe(["12.1", "12.3", "4.20"]);

        YamlMappingNode requirements = RequiredMapping(runtime, "functional_requirements");
        requirements.Children.Keys.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["FR32", "FR33", "FR34", "FR35"]);
        requirements.Children.Values.Select(node => RequiredScalar(node, "runtime_status")).ToArray()
            .ShouldAllBe(status => status == "incomplete");
        RequiredScalar(runtime, "evidence_claim").ShouldContain("no runtime implementation or production evidence is claimed", Case.Sensitive);
    }

    [Fact]
    public void Oq2EvidenceNegativeControlsFailClosedForMissingMismatchedStaleExtraAndIncompleteEvidence()
    {
        YamlMappingNode evidence = LoadYamlMapping(Oq2EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        (string key, string expected)[] packageIdentity =
        [
            ("schema_version", "1.0.0"),
            ("evidence_id", "OQ2"),
            ("status", "design-approved"),
            ("release_gate_status", "in-progress"),
            ("policy_version", ApprovedOq2Version),
            ("policy_path", "docs/contract/file-context-contract-groups.md"),
            ("approved_on", ApprovedOq2Date),
        ];
        foreach ((string key, string expected) in packageIdentity)
        {
            YamlMappingNode missingIdentity = CloneRow(evidence);
            missingIdentity.Children.Remove(new YamlScalarNode(key));
            EvaluateOq2Evidence(missingIdentity, ApprovedOq2Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq2_evidence_missing" && diagnostic.Identifier == $"OQ2:{key}");

            YamlMappingNode mismatchedIdentity = CloneRow(evidence);
            SetScalar(mismatchedIdentity, key, expected + "-unexpected");
            EvaluateOq2Evidence(mismatchedIdentity, ApprovedOq2Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq2_evidence_mismatch" && diagnostic.Identifier == $"OQ2:{key}");
        }

        YamlMappingNode missingDigest = CloneRow(evidence);
        missingDigest.Children.Remove(new YamlScalarNode("policy_sha256"));
        GateDiagnostic[] missingDiagnostics = EvaluateOq2Evidence(missingDigest, ApprovedOq2Sha256, policy, today);
        missingDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_evidence_missing" && diagnostic.Identifier == "OQ2:policy_sha256");

        YamlMappingNode mismatchedDigest = CloneRow(evidence);
        SetScalar(mismatchedDigest, "policy_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedDiagnostics = EvaluateOq2Evidence(mismatchedDigest, ApprovedOq2Sha256, policy, today);
        mismatchedDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ2");

        YamlMappingNode incomplete = CloneRow(evidence);
        YamlSequenceNode incompleteRecords = RequiredSequence(RequiredMapping(incomplete, "approval"), "records");
        YamlNode securityRecord = incompleteRecords.Children.Cast<YamlMappingNode>()
            .Single(record => RequiredScalar(record, "authority") == "Security");
        incompleteRecords.Children.Remove(securityRecord);
        GateDiagnostic[] incompleteDiagnostics = EvaluateOq2Evidence(incomplete, ApprovedOq2Sha256, policy, today);
        incompleteDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_approval_incomplete" && diagnostic.Identifier == "OQ2:Security");

        YamlMappingNode missingAuthorities = CloneRow(evidence);
        RequiredMapping(missingAuthorities, "approval").Children.Remove(new YamlScalarNode("required_authorities"));
        GateDiagnostic[] missingAuthorityDiagnostics = EvaluateOq2Evidence(missingAuthorities, ApprovedOq2Sha256, policy, today);
        missingAuthorityDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_evidence_missing" && diagnostic.Identifier == "OQ2:required-authorities");

        YamlMappingNode emptyAuthorities = CloneRow(evidence);
        RequiredMapping(emptyAuthorities, "approval").Children[new YamlScalarNode("required_authorities")] = new YamlSequenceNode();
        GateDiagnostic[] emptyAuthorityDiagnostics = EvaluateOq2Evidence(emptyAuthorities, ApprovedOq2Sha256, policy, today);
        emptyAuthorityDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_evidence_missing" && diagnostic.Identifier == "OQ2:required-authorities");

        const string unexpectedValue = "tenant-secret-unexpected-value";
        YamlMappingNode extra = CloneRow(evidence);
        YamlMappingNode extraApproval = RequiredMapping(extra, "approval");
        RequiredSequence(extraApproval, "required_authorities").Add(new YamlScalarNode(unexpectedValue));
        RequiredSequence(extraApproval, "records").Add(new YamlMappingNode(
            new YamlScalarNode("authority"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approver"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approved_on"), new YamlScalarNode(ApprovedOq2Date),
            new YamlScalarNode("evidence_version"), new YamlScalarNode(ApprovedOq2Version),
            new YamlScalarNode("evidence_sha256"), new YamlScalarNode(ApprovedOq2Sha256)));
        GateDiagnostic[] extraDiagnostics = EvaluateOq2Evidence(extra, ApprovedOq2Sha256, policy, today);
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_approval_extra" && diagnostic.Identifier == "OQ2:required-authorities");
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_approval_extra" && diagnostic.Identifier == "OQ2:record-count");

        YamlMappingNode malformedExtraRecord = CloneRow(evidence);
        RequiredSequence(RequiredMapping(malformedExtraRecord, "approval"), "records")
            .Add(new YamlScalarNode("malformed-extra-record"));
        GateDiagnostic[] malformedExtraRecordDiagnostics = EvaluateOq2Evidence(
            malformedExtraRecord,
            ApprovedOq2Sha256,
            policy,
            today);
        malformedExtraRecordDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq2_approval_extra" && diagnostic.Identifier == "OQ2:record-count");

        YamlMappingNode missingReopenPolicy = CloneRow(evidence);
        missingReopenPolicy.Children.Remove(new YamlScalarNode("reopen_policy"));
        GateDiagnostic[] missingReopenDiagnostics = EvaluateOq2Evidence(missingReopenPolicy, ApprovedOq2Sha256, policy, today);
        missingReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq2_evidence_missing" && diagnostic.Identifier == "OQ2:reopen_policy");

        YamlMappingNode mismatchedReopenPolicy = CloneRow(evidence);
        SetScalar(mismatchedReopenPolicy, "reopen_policy", "policy content and digest only");
        GateDiagnostic[] mismatchedReopenDiagnostics = EvaluateOq2Evidence(mismatchedReopenPolicy, ApprovedOq2Sha256, policy, today);
        mismatchedReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq2_evidence_mismatch" && diagnostic.Identifier == "OQ2:reopen_policy");

        YamlMappingNode mismatchedRecord = CloneRow(evidence);
        YamlMappingNode architectureRecord = RequiredSequence(RequiredMapping(mismatchedRecord, "approval"), "records")
            .Children.Cast<YamlMappingNode>().Single(record => RequiredScalar(record, "authority") == "Architecture");
        SetScalar(architectureRecord, "approver", unexpectedValue);
        SetScalar(architectureRecord, "approved_on", "2099-12-31");
        SetScalar(architectureRecord, "evidence_version", "9.9.9");
        SetScalar(architectureRecord, "evidence_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedRecordDiagnostics = EvaluateOq2Evidence(mismatchedRecord, ApprovedOq2Sha256, policy, today);
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_approval_identity_mismatch" && diagnostic.Identifier == "OQ2:Architecture");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq2_approval_date_mismatch" && diagnostic.Identifier == "OQ2:Architecture");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_version_mismatch" && diagnostic.Identifier == "OQ2:Architecture");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ2:Architecture");

        GateDiagnostic[] staleDiagnostics = EvaluateOq2Evidence(
            evidence,
            ApprovedOq2Sha256,
            policy,
            today.AddDays(policy.MaxAgeDays + 1));
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ2:PM");
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ2:Architecture");
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ2:Security");

        foreach (GateDiagnostic diagnostic in missingDiagnostics
            .Concat(mismatchedDiagnostics)
            .Concat(incompleteDiagnostics)
            .Concat(missingAuthorityDiagnostics)
            .Concat(emptyAuthorityDiagnostics)
            .Concat(extraDiagnostics)
            .Concat(malformedExtraRecordDiagnostics)
            .Concat(missingReopenDiagnostics)
            .Concat(mismatchedReopenDiagnostics)
            .Concat(mismatchedRecordDiagnostics)
            .Concat(staleDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
            diagnostic.ToString().ShouldNotContain(unexpectedValue, Case.Sensitive);
        }
    }

    [Fact]
    public void Oq3AuthorizationMatrixPackageBindsVersionDigestApprovalsAndRuntimePosture()
    {
        File.Exists(Oq3MatrixPath).ShouldBeTrue("OQ3 requires the canonical authorization-matrix artifact.");
        File.Exists(Oq3EvidencePath).ShouldBeTrue("OQ3 requires a versioned governance evidence manifest.");
        File.Exists(V2ConformanceSetPath).ShouldBeTrue("A6b review requires the generated v2 conformance set.");

        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Oq3MatrixPath)));
        actualDigest.ShouldNotBe(ApprovedOq3Sha256, "The 2.0.0 candidate must not reuse the historical v1 approval digest.");
        File.ReadAllText(Oq3MatrixPath).ShouldContain("Matrix version: `2.0.0`", Case.Sensitive);
        AssertA6bRegisterStateIsCoherent(actualDigest);

        YamlMappingNode evidence = LoadYamlMapping(Oq3EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        GateDiagnostic[] diagnostics = EvaluateOq3Evidence(
            evidence,
            ApprovedOq3Sha256,
            policy,
            DateOnly.FromDateTime(DateTime.UtcNow));

        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        string[] canonicalSurfaces = RequiredSequence(evidence, "canonical_surfaces").Children
            .Select(node => RequiredScalar(node, "canonical_surface"))
            .ToArray();
        canonicalSurfaces.ShouldBe(
        [
            "docs/contract/authorization-matrix.md",
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        ]);

        YamlMappingNode denominator = RequiredMapping(evidence, "denominator");
        RequiredScalar(denominator, "access_states").ShouldBe("12");
        RequiredScalar(denominator, "canonical_actors").ShouldBe("6");
        RequiredScalar(denominator, "negative_access_states").ShouldBe("6");
        RequiredScalar(denominator, "denial_routing_states").ShouldBe("8");
        RequiredScalar(denominator, "operation_families").ShouldBe("11");
        RequiredScalar(denominator, "spine_operations").ShouldBe("49");
        RequiredScalar(denominator, "scope_dimensions").ShouldBe("8");

        YamlMappingNode runtime = RequiredMapping(evidence, "runtime_posture");
        RequiredScalar(runtime, "status").ShouldBe("incomplete");
        RequiredSequence(runtime, "incomplete_stories").Children
            .Select(node => RequiredScalar(node, "incomplete_story"))
            .ToArray().ShouldBe(ApprovedOq3IncompleteStories);

        YamlMappingNode requirements = RequiredMapping(runtime, "functional_requirements");
        requirements.Children.Keys.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["FR8", "FR9", "FR10"]);
        requirements.Children.Values.Select(node => RequiredScalar(node, "runtime_status")).ToArray()
            .ShouldAllBe(status => status == "incomplete");
        RequiredScalar(runtime, "evidence_claim").ShouldContain("no runtime authorization", Case.Sensitive);

        foreach (string planningPath in Oq3DigestBoundPlanningArtifacts)
        {
            File.ReadAllText(Path.Combine(RepositoryRoot, NormalizeForFileSystem(planningPath)))
                .Contains(ApprovedOq3Sha256, StringComparison.Ordinal)
                .ShouldBeTrue(planningPath);
        }
    }

    [Fact]
    public void Oq3EvidenceNegativeControlsFailClosedForMissingMismatchedStaleExtraAndIncompleteEvidence()
    {
        YamlMappingNode evidence = LoadYamlMapping(Oq3EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        (string key, string expected)[] packageIdentity =
        [
            ("schema_version", "1.0.0"),
            ("evidence_id", "OQ3"),
            ("status", "design-approved"),
            ("release_gate_status", "in-progress"),
            ("matrix_version", ApprovedOq3Version),
            ("matrix_path", "docs/contract/authorization-matrix.md"),
            ("approved_on", ApprovedOq3Date),
        ];
        foreach ((string key, string expected) in packageIdentity)
        {
            YamlMappingNode missingIdentity = CloneRow(evidence);
            missingIdentity.Children.Remove(new YamlScalarNode(key));
            EvaluateOq3Evidence(missingIdentity, ApprovedOq3Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_missing" && diagnostic.Identifier == $"OQ3:{key}");

            YamlMappingNode mismatchedIdentity = CloneRow(evidence);
            SetScalar(mismatchedIdentity, key, expected + "-unexpected");
            EvaluateOq3Evidence(mismatchedIdentity, ApprovedOq3Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_mismatch" && diagnostic.Identifier == $"OQ3:{key}");
        }

        YamlMappingNode missingDigest = CloneRow(evidence);
        missingDigest.Children.Remove(new YamlScalarNode("matrix_sha256"));
        GateDiagnostic[] missingDiagnostics = EvaluateOq3Evidence(missingDigest, ApprovedOq3Sha256, policy, today);
        missingDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_missing" && diagnostic.Identifier == "OQ3:matrix_sha256");

        YamlMappingNode mismatchedDigest = CloneRow(evidence);
        SetScalar(mismatchedDigest, "matrix_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedDiagnostics = EvaluateOq3Evidence(mismatchedDigest, ApprovedOq3Sha256, policy, today);
        mismatchedDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ3");

        YamlMappingNode driftedDenominator = CloneRow(evidence);
        SetScalar(RequiredMapping(driftedDenominator, "denominator"), "spine_operations", "48");
        GateDiagnostic[] denominatorDiagnostics = EvaluateOq3Evidence(driftedDenominator, ApprovedOq3Sha256, policy, today);
        denominatorDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_mismatch" && diagnostic.Identifier == "OQ3:denominator");

        YamlMappingNode droppedGap = CloneRow(evidence);
        YamlSequenceNode gapIds = RequiredSequence(droppedGap, "recorded_gap_ids");
        gapIds.Children.Remove(gapIds.Children.Last());
        GateDiagnostic[] gapDiagnostics = EvaluateOq3Evidence(droppedGap, ApprovedOq3Sha256, policy, today);
        gapDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_mismatch" && diagnostic.Identifier == "OQ3:recorded-gap-ids");

        YamlMappingNode incomplete = CloneRow(evidence);
        YamlSequenceNode incompleteRecords = RequiredSequence(RequiredMapping(incomplete, "approval"), "records");
        YamlNode securityRecord = incompleteRecords.Children.Cast<YamlMappingNode>()
            .Single(record => RequiredScalar(record, "authority") == "Security");
        incompleteRecords.Children.Remove(securityRecord);
        GateDiagnostic[] incompleteDiagnostics = EvaluateOq3Evidence(incomplete, ApprovedOq3Sha256, policy, today);
        incompleteDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_approval_incomplete" && diagnostic.Identifier == "OQ3:Security");

        YamlMappingNode missingAuthorities = CloneRow(evidence);
        RequiredMapping(missingAuthorities, "approval").Children.Remove(new YamlScalarNode("required_authorities"));
        GateDiagnostic[] missingAuthorityDiagnostics = EvaluateOq3Evidence(missingAuthorities, ApprovedOq3Sha256, policy, today);
        missingAuthorityDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_evidence_missing" && diagnostic.Identifier == "OQ3:required-authorities");

        const string unexpectedValue = "tenant-secret-unexpected-value";
        YamlMappingNode extra = CloneRow(evidence);
        YamlMappingNode extraApproval = RequiredMapping(extra, "approval");
        RequiredSequence(extraApproval, "required_authorities").Add(new YamlScalarNode(unexpectedValue));
        RequiredSequence(extraApproval, "records").Add(new YamlMappingNode(
            new YamlScalarNode("authority"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approver"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approved_on"), new YamlScalarNode(ApprovedOq3Date),
            new YamlScalarNode("evidence_version"), new YamlScalarNode(ApprovedOq3Version),
            new YamlScalarNode("evidence_sha256"), new YamlScalarNode(ApprovedOq3Sha256)));
        GateDiagnostic[] extraDiagnostics = EvaluateOq3Evidence(extra, ApprovedOq3Sha256, policy, today);
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_approval_extra" && diagnostic.Identifier == "OQ3:required-authorities");
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_approval_extra" && diagnostic.Identifier == "OQ3:record-count");

        YamlMappingNode missingReopenPolicy = CloneRow(evidence);
        missingReopenPolicy.Children.Remove(new YamlScalarNode("reopen_policy"));
        GateDiagnostic[] missingReopenDiagnostics = EvaluateOq3Evidence(missingReopenPolicy, ApprovedOq3Sha256, policy, today);
        missingReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_evidence_missing" && diagnostic.Identifier == "OQ3:reopen_policy");

        YamlMappingNode mismatchedReopenPolicy = CloneRow(evidence);
        SetScalar(mismatchedReopenPolicy, "reopen_policy", "matrix content and digest only");
        GateDiagnostic[] mismatchedReopenDiagnostics = EvaluateOq3Evidence(mismatchedReopenPolicy, ApprovedOq3Sha256, policy, today);
        mismatchedReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_evidence_mismatch" && diagnostic.Identifier == "OQ3:reopen_policy");

        YamlMappingNode mismatchedRecord = CloneRow(evidence);
        YamlMappingNode pmRecord = RequiredSequence(RequiredMapping(mismatchedRecord, "approval"), "records")
            .Children.Cast<YamlMappingNode>().Single(record => RequiredScalar(record, "authority") == "PM");
        SetScalar(pmRecord, "approver", unexpectedValue);
        SetScalar(pmRecord, "approved_on", "2099-12-31");
        SetScalar(pmRecord, "evidence_version", "9.9.9");
        SetScalar(pmRecord, "evidence_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedRecordDiagnostics = EvaluateOq3Evidence(mismatchedRecord, ApprovedOq3Sha256, policy, today);
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_approval_identity_mismatch" && diagnostic.Identifier == "OQ3:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq3_approval_date_mismatch" && diagnostic.Identifier == "OQ3:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_version_mismatch" && diagnostic.Identifier == "OQ3:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ3:PM");

        YamlMappingNode completedRuntime = CloneRow(evidence);
        SetScalar(RequiredMapping(completedRuntime, "runtime_posture"), "status", "complete");
        GateDiagnostic[] runtimeDiagnostics = EvaluateOq3Evidence(completedRuntime, ApprovedOq3Sha256, policy, today);
        runtimeDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_evidence_mismatch" && diagnostic.Identifier == "OQ3:runtime-posture");

        GateDiagnostic[] staleDiagnostics = EvaluateOq3Evidence(
            evidence,
            ApprovedOq3Sha256,
            policy,
            today.AddDays(policy.MaxAgeDays + 1));
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ3:Security");
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ3:PM");

        foreach (GateDiagnostic diagnostic in missingDiagnostics
            .Concat(mismatchedDiagnostics)
            .Concat(denominatorDiagnostics)
            .Concat(gapDiagnostics)
            .Concat(incompleteDiagnostics)
            .Concat(missingAuthorityDiagnostics)
            .Concat(extraDiagnostics)
            .Concat(missingReopenDiagnostics)
            .Concat(mismatchedReopenDiagnostics)
            .Concat(mismatchedRecordDiagnostics)
            .Concat(runtimeDiagnostics)
            .Concat(staleDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
            diagnostic.ToString().ShouldNotContain(unexpectedValue, Case.Sensitive);
        }
    }

    [Fact]
    public void Oq4ProviderCompatibilityPackageBindsVersionDigestApprovalsAndRuntimePosture()
    {
        File.Exists(Oq4CatalogPath).ShouldBeTrue("OQ4 requires the canonical provider compatibility catalog.");
        File.Exists(Oq4EvidencePath).ShouldBeTrue("OQ4 requires a versioned governance evidence manifest.");

        string actualDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Oq4CatalogPath)));
        actualDigest.ShouldBe(ApprovedOq4Sha256, "OQ4 catalog changes require a new version, digest, and fresh Provider, Architecture, and PM approvals.");

        YamlMappingNode evidence = LoadYamlMapping(Oq4EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        GateDiagnostic[] diagnostics = EvaluateOq4Evidence(
            evidence,
            actualDigest,
            policy,
            DateOnly.FromDateTime(DateTime.UtcNow));

        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        RequiredSequence(evidence, "canonical_surfaces").Children
            .Select(node => RequiredScalar(node, "canonical_surface"))
            .ToArray()
            .ShouldBe(
            [
                "docs/contract/provider-compatibility-catalog.md",
                "tests/contracts/github/pinned-profile.json",
                "tests/Hexalith.Folders.Contracts.Tests/OpenApi/ProviderCompatibilityCatalogContractTests.cs",
                "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
            ]);

        RequiredSequence(evidence, "governed_providers").Children
            .Select(node => RequiredScalar(node, "governed_provider"))
            .ToArray().ShouldBe(ApprovedOq4GovernedProviders);
        RequiredSequence(evidence, "published_ceiling_ids").Children
            .Select(node => RequiredScalar(node, "published_ceiling_id"))
            .ToArray().ShouldBe(ApprovedOq4CeilingIds);
        RequiredSequence(evidence, "recorded_gap_ids").Children
            .Select(node => RequiredScalar(node, "recorded_gap_id"))
            .ToArray().ShouldBe(ApprovedOq4GapIds);

        YamlMappingNode runtime = RequiredMapping(evidence, "runtime_posture");
        RequiredScalar(runtime, "status").ShouldBe("incomplete");
        RequiredScalar(runtime, "credentialed_live_provider_evidence").ShouldBe("not-run");
        RequiredScalar(runtime, "c12_evidence_standard").ShouldBe(ApprovedOq4C12EvidenceStandard);
        RequiredSequence(runtime, "incomplete_stories").Children
            .Select(node => RequiredScalar(node, "incomplete_story"))
            .ToArray().ShouldBe(ApprovedOq4IncompleteStories);

        YamlMappingNode requirements = RequiredMapping(runtime, "functional_requirements");
        requirements.Children.Keys.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["FR16", "FR17", "FR22", "FR23"]);
        requirements.Children.Values.Select(node => RequiredScalar(node, "runtime_status")).ToArray()
            .ShouldAllBe(status => status == "incomplete");
        RequiredScalar(runtime, "evidence_claim").ShouldContain("no credentialed live provider run", Case.Sensitive);

        // The narrowed C12 evidence standard must also be stated on the criterion row itself, and the
        // criterion must stay separate from the still-open NFR49 runtime gap.
        YamlMappingNode c12 = RequiredSequence(LoadYamlMapping(EvidencePath), "criteria").Children
            .Cast<YamlMappingNode>()
            .Single(row => RequiredScalar(row, "criterion_id") == "C12");
        RequiredScalar(c12, "status").ShouldBe("approved");
        RequiredScalar(c12, "evidence_sha256").ShouldBe(ApprovedOq4Sha256);
        string summary = RequiredScalar(c12, "result_summary");
        summary.ShouldContain("hermetic-PR-gate", Case.Sensitive);
        summary.ShouldContain("explicitly not run", Case.Sensitive);
        summary.ShouldContain("residual provider-ready debt", Case.Sensitive);
        summary.ShouldContain("NFR49", Case.Sensitive);
        RequiredSequence(c12, "open_policy_placeholders").Children.Count.ShouldBe(0);

        foreach (string planningPath in Oq4DigestBoundPlanningArtifacts)
        {
            File.ReadAllText(Path.Combine(RepositoryRoot, NormalizeForFileSystem(planningPath)))
                .Contains(ApprovedOq4Sha256, StringComparison.Ordinal)
                .ShouldBeTrue(planningPath);
        }

        // The planning manifest binds the OQ4 evidence manifest rather than duplicating the catalog digest.
        // Validate that transitive chain explicitly so the approved planning artifact remains immutable.
        const string planningManifestPath = "_bmad-output/planning-artifacts/planning-story-manifest.yaml";
        YamlMappingNode planningManifest = LoadYamlMapping(
            Path.Combine(RepositoryRoot, NormalizeForFileSystem(planningManifestPath)));
        YamlMappingNode oq4Decision = RequiredMapping(
            RequiredMapping(planningManifest, "accepted_terminal_references"),
            "OQ4");
        RequiredScalar(oq4Decision, "evidence_path").ShouldBe("docs/contract/oq4-provider-compatibility-evidence.yaml");
        RequiredScalar(oq4Decision, "evidence_sha256").ShouldBe(
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Oq4EvidencePath))));

        // The LF pins are what make the approval-bound digest reproducible across checkouts. Without them a
        // Windows checkout produces CRLF bytes and the approval fails as an unexplained digest mismatch.
        string attributes = File.ReadAllText(Path.Combine(RepositoryRoot, ".gitattributes"));
        foreach (string lfPinnedPath in Oq4LineEndingPinnedPaths)
        {
            attributes.ShouldContain($"{lfPinnedPath} text eol=lf", Case.Sensitive);
        }
    }

    [Fact]
    public void Oq4EvidenceNegativeControlsFailClosedForMissingMismatchedStaleExtraAndIncompleteEvidence()
    {
        YamlMappingNode evidence = LoadYamlMapping(Oq4EvidencePath);
        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        (string key, string expected)[] packageIdentity =
        [
            ("schema_version", "1.0.0"),
            ("evidence_id", "OQ4"),
            ("status", "design-approved"),
            ("release_gate_status", "in-progress"),
            ("catalog_version", ApprovedOq4Version),
            ("catalog_path", "docs/contract/provider-compatibility-catalog.md"),
            ("approved_on", ApprovedOq4Date),
        ];
        foreach ((string key, string expected) in packageIdentity)
        {
            YamlMappingNode missingIdentity = CloneRow(evidence);
            missingIdentity.Children.Remove(new YamlScalarNode(key));
            EvaluateOq4Evidence(missingIdentity, ApprovedOq4Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_missing" && diagnostic.Identifier == $"OQ4:{key}");

            YamlMappingNode mismatchedIdentity = CloneRow(evidence);
            SetScalar(mismatchedIdentity, key, expected + "-unexpected");
            EvaluateOq4Evidence(mismatchedIdentity, ApprovedOq4Sha256, policy, today)
                .ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == $"OQ4:{key}");
        }

        YamlMappingNode missingDigest = CloneRow(evidence);
        missingDigest.Children.Remove(new YamlScalarNode("catalog_sha256"));
        GateDiagnostic[] missingDiagnostics = EvaluateOq4Evidence(missingDigest, ApprovedOq4Sha256, policy, today);
        missingDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_missing" && diagnostic.Identifier == "OQ4:catalog_sha256");

        YamlMappingNode mismatchedDigest = CloneRow(evidence);
        SetScalar(mismatchedDigest, "catalog_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedDiagnostics = EvaluateOq4Evidence(mismatchedDigest, ApprovedOq4Sha256, policy, today);
        mismatchedDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ4");

        YamlMappingNode droppedCeiling = CloneRow(evidence);
        YamlSequenceNode ceilingIds = RequiredSequence(droppedCeiling, "published_ceiling_ids");
        ceilingIds.Children.Remove(ceilingIds.Children.Last());
        GateDiagnostic[] ceilingDiagnostics = EvaluateOq4Evidence(droppedCeiling, ApprovedOq4Sha256, policy, today);
        ceilingDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:published-ceiling-ids");

        YamlMappingNode droppedGap = CloneRow(evidence);
        YamlSequenceNode gapIds = RequiredSequence(droppedGap, "recorded_gap_ids");
        gapIds.Children.Remove(gapIds.Children.Last());
        GateDiagnostic[] gapDiagnostics = EvaluateOq4Evidence(droppedGap, ApprovedOq4Sha256, policy, today);
        gapDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:recorded-gap-ids");

        YamlMappingNode droppedProvider = CloneRow(evidence);
        YamlSequenceNode providers = RequiredSequence(droppedProvider, "governed_providers");
        providers.Children.Remove(providers.Children.Last());
        GateDiagnostic[] providerDiagnostics = EvaluateOq4Evidence(droppedProvider, ApprovedOq4Sha256, policy, today);
        providerDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:governed-providers");

        YamlMappingNode incomplete = CloneRow(evidence);
        YamlSequenceNode incompleteRecords = RequiredSequence(RequiredMapping(incomplete, "approval"), "records");
        YamlNode providerRecord = incompleteRecords.Children.Cast<YamlMappingNode>()
            .Single(record => RequiredScalar(record, "authority") == "Provider");
        incompleteRecords.Children.Remove(providerRecord);
        GateDiagnostic[] incompleteDiagnostics = EvaluateOq4Evidence(incomplete, ApprovedOq4Sha256, policy, today);
        incompleteDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_approval_incomplete" && diagnostic.Identifier == "OQ4:Provider");

        YamlMappingNode missingAuthorities = CloneRow(evidence);
        RequiredMapping(missingAuthorities, "approval").Children.Remove(new YamlScalarNode("required_authorities"));
        GateDiagnostic[] missingAuthorityDiagnostics = EvaluateOq4Evidence(missingAuthorities, ApprovedOq4Sha256, policy, today);
        missingAuthorityDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_evidence_missing" && diagnostic.Identifier == "OQ4:required-authorities");

        const string unexpectedValue = "tenant-secret-unexpected-value";
        YamlMappingNode extra = CloneRow(evidence);
        YamlMappingNode extraApproval = RequiredMapping(extra, "approval");
        RequiredSequence(extraApproval, "required_authorities").Add(new YamlScalarNode(unexpectedValue));
        RequiredSequence(extraApproval, "records").Add(new YamlMappingNode(
            new YamlScalarNode("authority"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approver"), new YamlScalarNode(unexpectedValue),
            new YamlScalarNode("approved_on"), new YamlScalarNode(ApprovedOq4Date),
            new YamlScalarNode("evidence_version"), new YamlScalarNode(ApprovedOq4Version),
            new YamlScalarNode("evidence_sha256"), new YamlScalarNode(ApprovedOq4Sha256)));
        GateDiagnostic[] extraDiagnostics = EvaluateOq4Evidence(extra, ApprovedOq4Sha256, policy, today);
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_approval_extra" && diagnostic.Identifier == "OQ4:required-authorities");
        extraDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_approval_extra" && diagnostic.Identifier == "OQ4:record-count");

        YamlMappingNode missingReopenPolicy = CloneRow(evidence);
        missingReopenPolicy.Children.Remove(new YamlScalarNode("reopen_policy"));
        GateDiagnostic[] missingReopenDiagnostics = EvaluateOq4Evidence(missingReopenPolicy, ApprovedOq4Sha256, policy, today);
        missingReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_evidence_missing" && diagnostic.Identifier == "OQ4:reopen_policy");

        YamlMappingNode mismatchedReopenPolicy = CloneRow(evidence);
        SetScalar(mismatchedReopenPolicy, "reopen_policy", "catalog content and digest only");
        GateDiagnostic[] mismatchedReopenDiagnostics = EvaluateOq4Evidence(mismatchedReopenPolicy, ApprovedOq4Sha256, policy, today);
        mismatchedReopenDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:reopen_policy");

        YamlMappingNode mismatchedRecord = CloneRow(evidence);
        YamlMappingNode pmRecord = RequiredSequence(RequiredMapping(mismatchedRecord, "approval"), "records")
            .Children.Cast<YamlMappingNode>().Single(record => RequiredScalar(record, "authority") == "PM");
        SetScalar(pmRecord, "approver", unexpectedValue);
        SetScalar(pmRecord, "approved_on", "2099-12-31");
        SetScalar(pmRecord, "evidence_version", "9.9.9");
        SetScalar(pmRecord, "evidence_sha256", new string('0', 64));
        GateDiagnostic[] mismatchedRecordDiagnostics = EvaluateOq4Evidence(mismatchedRecord, ApprovedOq4Sha256, policy, today);
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_approval_identity_mismatch" && diagnostic.Identifier == "OQ4:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "oq4_approval_date_mismatch" && diagnostic.Identifier == "OQ4:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_version_mismatch" && diagnostic.Identifier == "OQ4:PM");
        mismatchedRecordDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_evidence_digest_mismatch" && diagnostic.Identifier == "OQ4:PM");

        // A completed runtime posture, a dropped incomplete story, or a claimed credentialed live run all
        // fail closed: OQ4 approval never converts into runtime or live-provider completion.
        YamlMappingNode completedRuntime = CloneRow(evidence);
        SetScalar(RequiredMapping(completedRuntime, "runtime_posture"), "status", "complete");
        GateDiagnostic[] runtimeDiagnostics = EvaluateOq4Evidence(completedRuntime, ApprovedOq4Sha256, policy, today);
        runtimeDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:runtime-posture");

        YamlMappingNode claimedLiveRun = CloneRow(evidence);
        SetScalar(RequiredMapping(claimedLiveRun, "runtime_posture"), "credentialed_live_provider_evidence", "completed");
        GateDiagnostic[] liveRunDiagnostics = EvaluateOq4Evidence(claimedLiveRun, ApprovedOq4Sha256, policy, today);
        liveRunDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:runtime-posture");

        YamlMappingNode droppedStory = CloneRow(evidence);
        YamlSequenceNode stories = RequiredSequence(RequiredMapping(droppedStory, "runtime_posture"), "incomplete_stories");
        stories.Children.Remove(stories.Children.Last());
        GateDiagnostic[] storyDiagnostics = EvaluateOq4Evidence(droppedStory, ApprovedOq4Sha256, policy, today);
        storyDiagnostics.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_evidence_mismatch" && diagnostic.Identifier == "OQ4:runtime-posture");

        GateDiagnostic[] staleDiagnostics = EvaluateOq4Evidence(
            evidence,
            ApprovedOq4Sha256,
            policy,
            today.AddDays(policy.MaxAgeDays + 1));
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ4:Provider");
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ4:Architecture");
        staleDiagnostics.ShouldContain(diagnostic => diagnostic.Category == "approval_stale" && diagnostic.Identifier == "OQ4:PM");

        foreach (GateDiagnostic diagnostic in missingDiagnostics
            .Concat(mismatchedDiagnostics)
            .Concat(ceilingDiagnostics)
            .Concat(gapDiagnostics)
            .Concat(providerDiagnostics)
            .Concat(incompleteDiagnostics)
            .Concat(missingAuthorityDiagnostics)
            .Concat(extraDiagnostics)
            .Concat(missingReopenDiagnostics)
            .Concat(mismatchedReopenDiagnostics)
            .Concat(mismatchedRecordDiagnostics)
            .Concat(runtimeDiagnostics)
            .Concat(liveRunDiagnostics)
            .Concat(storyDiagnostics)
            .Concat(staleDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
            diagnostic.ToString().ShouldNotContain(unexpectedValue, Case.Sensitive);
        }
    }

    [Fact]
    public void IdempotencyCorpusSchemaAndStableConsumptionMapCoverEverySample()
    {
        using JsonDocument corpus = JsonDocument.Parse(File.ReadAllText(CorpusPath));
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(CorpusSchemaPath));

        GateDiagnostic[] schemaDiagnostics = ValidateCorpusAgainstSchema(corpus, schema);
        schemaDiagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, schemaDiagnostics.Select(d => d.ToString())));

        Dictionary<string, string> classifications = corpus.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(
            item => RequiredString(item, "id"),
            item => RequiredString(item, "equivalence_classification"),
            StringComparer.Ordinal);

        YamlMappingNode[] consumption = RequiredSequence(LoadYamlMapping(CorpusConsumptionPath), "samples").Children.Cast<YamlMappingNode>().ToArray();
        string[] corpusSampleIds = classifications.Keys.Order(StringComparer.Ordinal).ToArray();

        GateDiagnostic[] consumptionDiagnostics = EvaluateSampleConsumption(consumption, corpusSampleIds);
        consumptionDiagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, consumptionDiagnostics.Select(d => d.ToString())));

        foreach (YamlMappingNode row in consumption)
        {
            string sampleId = RequiredScalar(row, "sample_id");
            RequiredScalar(row, "equivalence_classification").ShouldBe(classifications[sampleId], sampleId);
            RequiredScalar(row, "coverage_kind").ShouldBeOneOf("generated-helper-contract-test", "parser-policy-test", "prerequisite-drift-test");
            PathExists(RequiredScalar(row, "consumer_path")).ShouldBeTrue(sampleId);
        }
    }

    [Fact]
    public void IdempotencyConsumptionNegativeControlsCatchMissingDuplicateAndStaleMappings()
    {
        YamlMappingNode[] rows = RequiredSequence(LoadYamlMapping(CorpusConsumptionPath), "samples").Children.Cast<YamlMappingNode>().ToArray();
        string[] sampleIds = ReadCorpusSampleIds();

        GateDiagnostic[] missingDiagnostics = EvaluateSampleConsumption(rows.Where(row => RequiredScalar(row, "sample_id") != sampleIds[0]).ToArray(), sampleIds);
        missingDiagnostics.ShouldContain(d => d.Category == "idempotency_sample_unmapped");

        GateDiagnostic[] duplicateDiagnostics = EvaluateSampleConsumption(rows.Concat([CloneRow(rows[0])]).ToArray(), sampleIds);
        duplicateDiagnostics.ShouldContain(d => d.Category == "idempotency_sample_duplicate");

        YamlMappingNode stale = CloneRow(rows[0]);
        SetScalar(stale, "sample_id", "deleted-synthetic-sample");
        GateDiagnostic[] staleDiagnostics = EvaluateSampleConsumption([stale], sampleIds);
        staleDiagnostics.ShouldContain(d => d.Category == "idempotency_sample_stale");

        GateDiagnostic[] staleAndDuplicateDiagnostics = EvaluateSampleConsumption([stale, CloneRow(stale)], sampleIds);
        staleAndDuplicateDiagnostics.ShouldContain(d => d.Category == "idempotency_sample_stale" && d.Identifier == "deleted-synthetic-sample");
        staleAndDuplicateDiagnostics.ShouldContain(d => d.Category == "idempotency_sample_duplicate" && d.Identifier == "deleted-synthetic-sample");

        foreach (GateDiagnostic diagnostic in missingDiagnostics.Concat(duplicateDiagnostics).Concat(staleDiagnostics).Concat(staleAndDuplicateDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void PatternExampleManifestIsOptInAndCompilableProjectIsInSolution()
    {
        YamlMappingNode manifest = LoadYamlMapping(PatternManifestPath);
        string project = RequiredScalar(manifest, "compilable_examples_project");
        string solution = File.ReadAllText(SolutionPath);

        project.ShouldBe("tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj");
        PathExists(project).ShouldBeTrue(project);
        solution.ShouldContain(project);
        RequiredScalar(manifest, "target_framework").ShouldBe(ReadRootTargetFramework());

        YamlMappingNode[] examples = RequiredSequence(manifest, "examples").Children.Cast<YamlMappingNode>().ToArray();
        examples.ShouldContain(row => RequiredScalar(row, "classification") == "compilable-csharp");
        examples.ShouldContain(row => RequiredScalar(row, "classification") == "documentation-only");

        foreach (YamlMappingNode example in examples)
        {
            string marker = RequiredScalar(example, "marker");
            MarkerPattern.IsMatch(marker).ShouldBeTrue(marker);
            ParseRequiredBoolean(example, "synthetic_data_only").ShouldBeTrue(RequiredScalar(example, "example_id"));

            string sourcePath = RequiredScalar(example, "source_path");
            PathExists(sourcePath).ShouldBeTrue(RequiredScalar(example, "example_id"));

            if (RequiredScalar(example, "classification") == "documentation-only")
            {
                string sourceText = File.ReadAllText(Path.Combine(RepositoryRoot, NormalizeForFileSystem(sourcePath)));
                sourceText.Contains(marker, StringComparison.Ordinal).ShouldBeTrue($"{RequiredScalar(example, "example_id")} marker must appear in source doc");
            }
        }
    }

    [Fact]
    public void CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope()
    {
        YamlMappingNode[] exceptions = RequiredSequence(LoadYamlMapping(CacheKeyExceptionsPath), "exceptions").Children.Cast<YamlMappingNode>().ToArray();
        exceptions.ShouldNotBeEmpty();

        DateOnly latestAllowedReviewDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        DateOnly currentUtcDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (YamlMappingNode exception in exceptions)
        {
            string ruleId = RequiredScalar(exception, "rule_id");
            ruleId.ShouldStartWith("CACHE-");
            RequiredScalar(exception, "owner").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(exception, "reason").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(exception, "scope").ShouldNotBeNullOrWhiteSpace();

            string reviewStatus = RequiredScalar(exception, "review_status");
            reviewStatus.ShouldBe("approved", ruleId);

            DateOnly lastReviewedOn = ParseRequiredDate(exception, "last_reviewed_on");
            lastReviewedOn.ShouldBeLessThan(latestAllowedReviewDate, ruleId);

            if (exception.Children.TryGetValue(new YamlScalarNode("expiry_date"), out YamlNode? expiryNode))
            {
                if (expiryNode is not YamlScalarNode { Value: { Length: > 0 } } expiryScalar)
                {
                    throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: expiry-date-empty: {ruleId}");
                }

                DateOnly expiry = ParseDate(expiryScalar.Value!, "expiry_date");
                expiry.ShouldBeGreaterThan(lastReviewedOn, ruleId);
                expiry.ShouldBeGreaterThan(currentUtcDate, ruleId);
            }

            PathExists(RequiredScalar(exception, "evidence_link")).ShouldBeTrue();
        }

        EvaluateCacheKeyExceptionApprovalStates(exceptions).ShouldBeEmpty();

        GateDiagnostic[] diagnostics = ScanRepositoryForTenantCacheKeyCandidates();
        foreach (GateDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void CacheKeyExceptionApprovalStateFailsClosedForExpiredOrUnknownStatus()
    {
        YamlMappingNode expired = SyntheticCacheKeyException("CACHE-SYNTHETIC-EXPIRED", "expired");
        YamlMappingNode pending = SyntheticCacheKeyException("CACHE-SYNTHETIC-PENDING", "pending-review");
        YamlMappingNode approved = SyntheticCacheKeyException("CACHE-SYNTHETIC-APPROVED", "approved");

        GateDiagnostic[] expiredDiagnostics = EvaluateCacheKeyExceptionApprovalStates([expired]);
        expiredDiagnostics.ShouldContain(d => d.Category == "cache_key_exception_not_approved" && d.Identifier == "CACHE-SYNTHETIC-EXPIRED");

        GateDiagnostic[] pendingDiagnostics = EvaluateCacheKeyExceptionApprovalStates([pending]);
        pendingDiagnostics.ShouldContain(d => d.Category == "cache_key_exception_not_approved" && d.Identifier == "CACHE-SYNTHETIC-PENDING");

        EvaluateCacheKeyExceptionApprovalStates([approved]).ShouldBeEmpty();

        foreach (GateDiagnostic diagnostic in expiredDiagnostics.Concat(pendingDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void CacheKeyLintNegativeControlsClassifyTenantScopeAndExceptionsWithoutEchoingKeyValues()
    {
        CacheKeyCandidate scoped = new("src/Synthetic.cs", 10, "tenant-data", HasTenantScope: true, ExceptionRuleId: null);
        CacheKeyCandidate unscoped = new("src/Synthetic.cs", 11, "tenant-data", HasTenantScope: false, ExceptionRuleId: null);
        CacheKeyCandidate exception = new("tests/Synthetic.cs", 12, "tool-cache", HasTenantScope: false, ExceptionRuleId: "CACHE-NON-TENANT-NUGET");

        EvaluateCacheKeyCandidate(scoped).ShouldBeNull();
        EvaluateCacheKeyCandidate(unscoped)!.Category.ShouldBe("cache_key_unscoped");
        EvaluateCacheKeyCandidate(exception).ShouldBeNull();
        AssertMetadataOnly(EvaluateCacheKeyCandidate(unscoped)!.ToString());
    }

    [Fact]
    public void ParityCompletenessComparesStructuredOpenApiOperationsToGeneratedRows()
    {
        string[] operations = LoadOpenApiOperationIds(OpenApiPath);
        YamlMappingNode[] rows = LoadParityRows(ParityContractPath);
        GateDiagnostic[] diagnostics = EvaluateParityCompleteness(operations, rows);

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
        rows.Select(row => RequiredScalar(row, "operation_id")).Order(StringComparer.Ordinal).ToArray().ShouldBe(operations);

        foreach (YamlMappingNode row in rows)
        {
            RequiredScalar(row, "operation_family").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(row, "read_consistency_class").ShouldNotBeNullOrWhiteSpace();
            RequiredSequence(row, "adapter_expectations").Children.Count.ShouldBeGreaterThan(0);
            RequiredMapping(row, "transport_parity").Children.ContainsKey(new YamlScalarNode("idempotency_key_rule")).ShouldBeTrue();
            RequiredMapping(row, "transport_parity").Children.ContainsKey(new YamlScalarNode("error_code_set")).ShouldBeTrue();
            RequiredMapping(row, "behavioral_parity").Children.ContainsKey(new YamlScalarNode("mcp_failure_kind")).ShouldBeTrue();
        }
    }

    [Fact]
    public void ParityCompletenessNegativeControlsSeparateMissingStaleAndDuplicateRows()
    {
        string[] operations = ["CreateFolder", "GetWorkspaceStatus"];
        YamlMappingNode createA = SyntheticParityRow("CreateFolder");
        YamlMappingNode createB = SyntheticParityRow("CreateFolder");
        YamlMappingNode staleA = SyntheticParityRow("RemovedOperation");
        YamlMappingNode staleB = SyntheticParityRow("RemovedOperation");

        GateDiagnostic[] missingDiagnostics = EvaluateParityCompleteness(operations, [createA]);
        missingDiagnostics.ShouldContain(d => d.Category == "parity_missing_row" && d.Identifier == "GetWorkspaceStatus");

        GateDiagnostic[] staleDiagnostics = EvaluateParityCompleteness(operations, [createA, staleA]);
        staleDiagnostics.ShouldContain(d => d.Category == "parity_stale_row" && d.Identifier == "RemovedOperation");

        GateDiagnostic[] duplicateDiagnostics = EvaluateParityCompleteness(operations, [createA, createB]);
        duplicateDiagnostics.ShouldContain(d => d.Category == "parity_duplicate_row" && d.Identifier == "CreateFolder");

        GateDiagnostic[] staleAndDuplicateDiagnostics = EvaluateParityCompleteness(operations, [createA, staleA, staleB]);
        staleAndDuplicateDiagnostics.ShouldContain(d => d.Category == "parity_stale_row" && d.Identifier == "RemovedOperation");
        staleAndDuplicateDiagnostics.ShouldContain(d => d.Category == "parity_duplicate_row" && d.Identifier == "RemovedOperation");

        EvaluateDuplicateOpenApiOperationIds(["CreateFolder", "CreateFolder"]).ShouldContain(d => d.Category == "openapi_duplicate_operation_id");

        foreach (GateDiagnostic diagnostic in missingDiagnostics.Concat(staleDiagnostics).Concat(duplicateDiagnostics).Concat(staleAndDuplicateDiagnostics))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    private static bool HasApprovalBlock(YamlMappingNode row) =>
        row.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? node) && node is YamlMappingNode;

    private static ApprovalPolicy LoadApprovalPolicy(YamlMappingNode root)
    {
        YamlMappingNode policy = RequiredMapping(root, "approval_policy");
        int maxAgeDays = int.Parse(RequiredScalar(policy, "max_age_days"), CultureInfo.InvariantCulture);
        HashSet<string> tokens = RequiredSequence(policy, "generic_approver_tokens")
            .Children
            .Select(node => RequiredScalar(node, "generic_approver_token").ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
        return new ApprovalPolicy(maxAgeDays, tokens);
    }

    private static ApprovalPolicy SyntheticApprovalPolicy() =>
        new(365, new HashSet<string>(StringComparer.Ordinal) { "approved", "legal", "pm", "signed", "pending", "placeholder", "none" });

    private static GateDiagnostic[] EvaluateApprovalRecords(YamlMappingNode row, ApprovalPolicy policy, DateOnly today)
    {
        const string path = "docs/exit-criteria/c0-c13-governance-evidence.yaml";
        List<GateDiagnostic> diagnostics = [];
        string criterion = RequiredScalar(row, "criterion_id");

        if (!row.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? approvalNode)
            || approvalNode is not YamlMappingNode approval)
        {
            diagnostics.Add(new("exit-criteria", "approval_record_missing", criterion, path));
            return diagnostics.ToArray();
        }

        string[] requiredAuthorities = approval.Children.TryGetValue(new YamlScalarNode("required_authorities"), out YamlNode? authoritiesNode)
            && authoritiesNode is YamlSequenceNode authoritiesSeq
                ? authoritiesSeq.Children.Select(node => RequiredScalar(node, "authority")).ToArray()
                : [];

        YamlMappingNode[] records = approval.Children.TryGetValue(new YamlScalarNode("records"), out YamlNode? recordsNode)
            && recordsNode is YamlSequenceNode recordsSeq
                ? recordsSeq.Children.OfType<YamlMappingNode>().ToArray()
                : [];

        if (requiredAuthorities.Length == 0 || records.Length == 0)
        {
            diagnostics.Add(new("exit-criteria", "approval_record_missing", criterion, path));
            return diagnostics.ToArray();
        }

        foreach (string authority in requiredAuthorities)
        {
            YamlMappingNode[] matching = records
                .Where(record => string.Equals(TryScalar(record, "authority"), authority, StringComparison.Ordinal))
                .ToArray();

            if (matching.Length != 1)
            {
                diagnostics.Add(new("exit-criteria", "approval_authority_unsatisfied", $"{criterion}:{authority}", path));
                continue;
            }

            YamlMappingNode record = matching[0];
            string approver = (TryScalar(record, "approver") ?? string.Empty).Trim();
            if (approver.Length < 2
                || policy.GenericApproverTokens.Contains(approver.ToLowerInvariant())
                || string.Equals(approver, authority, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new("exit-criteria", "approval_approver_generic", $"{criterion}:{authority}", path));
            }

            string approvedOnRaw = TryScalar(record, "approved_on") ?? string.Empty;
            if (!DateOnly.TryParseExact(approvedOnRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly approvedOn))
            {
                diagnostics.Add(new("exit-criteria", "approval_date_invalid", $"{criterion}:{authority}", path));
                continue;
            }

            if (approvedOn > today)
            {
                diagnostics.Add(new("exit-criteria", "approval_date_future", $"{criterion}:{authority}", path));
            }
            else if (today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
            {
                diagnostics.Add(new("exit-criteria", "approval_stale", $"{criterion}:{authority}", path));
            }
        }

        // Optional per-criterion review-by / expiry date: if present it must be a valid date strictly
        // in the future, so an approval cannot sit indefinitely past its own declared re-review date.
        if (approval.Children.TryGetValue(new YamlScalarNode("review_by"), out YamlNode? reviewByNode)
            && reviewByNode is YamlScalarNode { Value: { Length: > 0 } } reviewByScalar)
        {
            if (!DateOnly.TryParseExact(reviewByScalar.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly reviewBy))
            {
                diagnostics.Add(new("exit-criteria", "approval_date_invalid", criterion, path));
            }
            else if (reviewBy <= today)
            {
                diagnostics.Add(new("exit-criteria", "approval_stale", criterion, path));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateC7DecisionEvidence(
        YamlMappingNode row,
        string actualDigest,
        ApprovalPolicy policy,
        DateOnly today)
    {
        const string path = "docs/exit-criteria/c0-c13-governance-evidence.yaml";
        List<GateDiagnostic> diagnostics = [];

        if (!string.Equals(TryScalar(row, "evidence_version"), ApprovedC7Version, StringComparison.Ordinal))
        {
            diagnostics.Add(new("exit-criteria", "approval_evidence_version_mismatch", "C7", path));
        }

        string? evidenceDigest = TryScalar(row, "evidence_sha256");
        if (evidenceDigest is null)
        {
            diagnostics.Add(new("exit-criteria", "approval_evidence_digest_missing", "C7", path));
        }
        else if (!string.Equals(evidenceDigest, actualDigest, StringComparison.Ordinal)
            || !Regex.IsMatch(evidenceDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(new("exit-criteria", "approval_evidence_digest_mismatch", "C7", path));
        }

        Dictionary<string, int> expectedTiming = new(StringComparer.Ordinal)
        {
            ["lock_renewal_interval_seconds"] = 30,
            ["authorization_revalidation_interval_seconds"] = 15,
            ["revocation_effect_slo_seconds"] = 60,
            ["expired_to_stale_threshold_seconds"] = 60,
        };
        Dictionary<string, int> observedTiming = new(StringComparer.Ordinal);
        bool timingInvalid = false;

        if (!row.Children.TryGetValue(new YamlScalarNode("timing_profile"), out YamlNode? timingNode)
            || timingNode is not YamlMappingNode timing)
        {
            timingInvalid = true;
        }
        else
        {
            string[] observedKeys = timing.Children.Keys
                .OfType<YamlScalarNode>()
                .Select(key => key.Value ?? string.Empty)
                .ToArray();
            if (observedKeys.Length != expectedTiming.Count
                || observedKeys.Any(key => !expectedTiming.ContainsKey(key)))
            {
                timingInvalid = true;
            }

            foreach (KeyValuePair<string, int> expected in expectedTiming)
            {
                string? raw = TryScalar(timing, expected.Key);
                if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                    || value <= 0
                    || value != expected.Value)
                {
                    timingInvalid = true;
                    continue;
                }

                observedTiming[expected.Key] = value;
            }

            if (observedTiming.TryGetValue("authorization_revalidation_interval_seconds", out int revalidation)
                && observedTiming.TryGetValue("revocation_effect_slo_seconds", out int revocationSlo)
                && revalidation > revocationSlo)
            {
                timingInvalid = true;
            }
        }

        if (timingInvalid)
        {
            diagnostics.Add(new("exit-criteria", "c7_timing_profile_invalid", "C7", path));
        }

        string[] expectedAuthorities = ["Architecture", "Security"];
        if (!row.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? approvalNode)
            || approvalNode is not YamlMappingNode approval)
        {
            diagnostics.Add(new("exit-criteria", "approval_record_missing", "C7", path));
            return diagnostics.ToArray();
        }

        string[] requiredAuthorities = approval.Children.TryGetValue(new YamlScalarNode("required_authorities"), out YamlNode? authoritiesNode)
            && authoritiesNode is YamlSequenceNode authorities
                ? authorities.Children.OfType<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray()
                : [];
        if (requiredAuthorities.Length != expectedAuthorities.Length
            || requiredAuthorities.Distinct(StringComparer.Ordinal).Count() != expectedAuthorities.Length
            || expectedAuthorities.Any(authority => !requiredAuthorities.Contains(authority, StringComparer.Ordinal)))
        {
            diagnostics.Add(new("exit-criteria", "approval_authority_unsatisfied", "C7:required-authorities", path));
        }

        YamlMappingNode[] records = approval.Children.TryGetValue(new YamlScalarNode("records"), out YamlNode? recordsNode)
            && recordsNode is YamlSequenceNode recordSequence
                ? recordSequence.Children.OfType<YamlMappingNode>().ToArray()
                : [];
        if (records.Length != expectedAuthorities.Length)
        {
            diagnostics.Add(new("exit-criteria", "approval_authority_unsatisfied", "C7:record-count", path));
        }

        foreach (string authority in expectedAuthorities)
        {
            YamlMappingNode[] matches = records
                .Where(record => string.Equals(TryScalar(record, "authority"), authority, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(new("exit-criteria", "approval_authority_unsatisfied", $"C7:{authority}", path));
                continue;
            }

            YamlMappingNode record = matches[0];
            string identifier = $"C7:{authority}";
            if (!string.Equals(TryScalar(record, "approver"), "Administrator", StringComparison.Ordinal))
            {
                diagnostics.Add(new("exit-criteria", "c7_approval_identity_mismatch", identifier, path));
            }

            if (!string.Equals(TryScalar(record, "approved_on"), "2026-09-12", StringComparison.Ordinal))
            {
                diagnostics.Add(new("exit-criteria", "c7_approval_date_mismatch", identifier, path));
            }
            else
            {
                DateOnly approvedOn = new(2026, 9, 12);
                if (approvedOn > today)
                {
                    diagnostics.Add(new("exit-criteria", "approval_date_future", identifier, path));
                }
                else if (today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
                {
                    diagnostics.Add(new("exit-criteria", "approval_stale", identifier, path));
                }
            }

            if (!string.Equals(TryScalar(record, "evidence_version"), ApprovedC7Version, StringComparison.Ordinal))
            {
                diagnostics.Add(new("exit-criteria", "approval_evidence_version_mismatch", identifier, path));
            }

            string? recordDigest = TryScalar(record, "evidence_sha256");
            if (recordDigest is null)
            {
                diagnostics.Add(new("exit-criteria", "approval_evidence_digest_missing", identifier, path));
            }
            else if (!string.Equals(recordDigest, actualDigest, StringComparison.Ordinal)
                || !Regex.IsMatch(recordDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                diagnostics.Add(new("exit-criteria", "approval_evidence_digest_mismatch", identifier, path));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateOq2Evidence(
        YamlMappingNode evidence,
        string actualDigest,
        ApprovalPolicy policy,
        DateOnly today)
    {
        const string path = "docs/contract/oq2-file-policy-evidence.yaml";
        List<GateDiagnostic> diagnostics = [];

        void ExpectScalar(string key, string expected)
        {
            string? observed = TryScalar(evidence, key);
            if (observed is null)
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", $"OQ2:{key}", path));
            }
            else if (!string.Equals(observed, expected, StringComparison.Ordinal))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_evidence_mismatch", $"OQ2:{key}", path));
            }
        }

        ExpectScalar("schema_version", "1.0.0");
        ExpectScalar("evidence_id", "OQ2");
        ExpectScalar("status", "design-approved");
        ExpectScalar("release_gate_status", "in-progress");
        ExpectScalar("policy_version", ApprovedOq2Version);
        ExpectScalar("policy_path", "docs/contract/file-context-contract-groups.md");
        ExpectScalar("approved_on", ApprovedOq2Date);
        ExpectScalar("reopen_policy", ApprovedOq2ReopenPolicy);

        string? policyDigest = TryScalar(evidence, "policy_sha256");
        if (policyDigest is null)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:policy_sha256", path));
        }
        else if (!string.Equals(policyDigest, actualDigest, StringComparison.Ordinal)
            || !Regex.IsMatch(policyDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(new("oq2-file-policy", "approval_evidence_digest_mismatch", "OQ2", path));
        }

        string[] expectedSurfaces =
        [
            "docs/contract/file-context-contract-groups.md",
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml",
            "src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        ];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("canonical_surfaces"), out YamlNode? surfacesNode)
            || surfacesNode is not YamlSequenceNode surfaces)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:canonical-surfaces", path));
        }
        else
        {
            string[] observedSurfaces = surfaces.Children.OfType<YamlScalarNode>()
                .Select(node => node.Value ?? string.Empty).ToArray();
            if (observedSurfaces.Length != expectedSurfaces.Length
                || !observedSurfaces.SequenceEqual(expectedSurfaces, StringComparer.Ordinal)
                || observedSurfaces.Any(surface => !IsRepositoryRelativePath(surface) || !PathExists(surface)))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_evidence_mismatch", "OQ2:canonical-surfaces", path));
            }
        }

        if (!evidence.Children.TryGetValue(new YamlScalarNode("runtime_posture"), out YamlNode? runtimeNode)
            || runtimeNode is not YamlMappingNode runtime)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:runtime-posture", path));
        }
        else
        {
            string[] expectedStories = ["12.1", "12.3", "4.20"];
            bool invalidRuntime = !string.Equals(TryScalar(runtime, "status"), "incomplete", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(TryScalar(runtime, "evidence_claim"));

            if (!runtime.Children.TryGetValue(new YamlScalarNode("incomplete_stories"), out YamlNode? storiesNode)
                || storiesNode is not YamlSequenceNode stories)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedStories = stories.Children.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedStories.SequenceEqual(expectedStories, StringComparer.Ordinal);
            }

            string[] expectedRequirements = ["FR32", "FR33", "FR34", "FR35"];
            if (!runtime.Children.TryGetValue(new YamlScalarNode("functional_requirements"), out YamlNode? requirementsNode)
                || requirementsNode is not YamlMappingNode requirements)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedRequirements = requirements.Children.Keys.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedRequirements.SequenceEqual(expectedRequirements, StringComparer.Ordinal)
                    || requirements.Children.Values.Any(node => node is not YamlScalarNode { Value: "incomplete" });
            }

            if (invalidRuntime)
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_evidence_mismatch", "OQ2:runtime-posture", path));
            }
        }

        string[] expectedAuthorities = ["PM", "Architecture", "Security"];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? approvalNode)
            || approvalNode is not YamlMappingNode approval)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:approval", path));
            return diagnostics.ToArray();
        }

        string[] requiredAuthorities = approval.Children.TryGetValue(new YamlScalarNode("required_authorities"), out YamlNode? authoritiesNode)
            && authoritiesNode is YamlSequenceNode authoritySequence
                ? authoritySequence.Children.OfType<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray()
                : [];
        if (requiredAuthorities.Length == 0)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:required-authorities", path));
        }
        else
        {
            foreach (string authority in expectedAuthorities.Where(authority => !requiredAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_approval_incomplete", $"OQ2:{authority}", path));
            }

            if (requiredAuthorities.Length != expectedAuthorities.Length
                || requiredAuthorities.Distinct(StringComparer.Ordinal).Count() != expectedAuthorities.Length
                || requiredAuthorities.Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_approval_extra", "OQ2:required-authorities", path));
            }
        }

        YamlSequenceNode? recordSequence = approval.Children.TryGetValue(new YamlScalarNode("records"), out YamlNode? recordsNode)
            ? recordsNode as YamlSequenceNode
            : null;
        YamlMappingNode[] records = recordSequence?.Children.OfType<YamlMappingNode>().ToArray() ?? [];
        if (records.Length == 0)
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_evidence_missing", "OQ2:approval-records", path));
        }
        else if (recordSequence!.Children.Count != expectedAuthorities.Length
            || records.Length != expectedAuthorities.Length
            || records.Select(record => TryScalar(record, "authority") ?? string.Empty).Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
        {
            diagnostics.Add(new("oq2-file-policy", "oq2_approval_extra", "OQ2:record-count", path));
        }

        foreach (string authority in expectedAuthorities)
        {
            YamlMappingNode[] matches = records
                .Where(record => string.Equals(TryScalar(record, "authority"), authority, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_approval_incomplete", $"OQ2:{authority}", path));
                if (matches.Length > 1)
                {
                    diagnostics.Add(new("oq2-file-policy", "oq2_approval_extra", "OQ2:record-count", path));
                }

                continue;
            }

            YamlMappingNode record = matches[0];
            string identifier = $"OQ2:{authority}";
            if (!string.Equals(TryScalar(record, "approver"), "Administrator", StringComparison.Ordinal))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_approval_identity_mismatch", identifier, path));
            }

            if (!string.Equals(TryScalar(record, "approved_on"), ApprovedOq2Date, StringComparison.Ordinal))
            {
                diagnostics.Add(new("oq2-file-policy", "oq2_approval_date_mismatch", identifier, path));
            }
            else
            {
                DateOnly approvedOn = new(2026, 9, 14);
                if (approvedOn > today)
                {
                    diagnostics.Add(new("oq2-file-policy", "approval_date_future", identifier, path));
                }
                else if (today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
                {
                    diagnostics.Add(new("oq2-file-policy", "approval_stale", identifier, path));
                }
            }

            if (!string.Equals(TryScalar(record, "evidence_version"), ApprovedOq2Version, StringComparison.Ordinal))
            {
                diagnostics.Add(new("oq2-file-policy", "approval_evidence_version_mismatch", identifier, path));
            }

            string? recordDigest = TryScalar(record, "evidence_sha256");
            if (recordDigest is null)
            {
                diagnostics.Add(new("oq2-file-policy", "approval_evidence_digest_missing", identifier, path));
            }
            else if (!string.Equals(recordDigest, actualDigest, StringComparison.Ordinal)
                || !Regex.IsMatch(recordDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                diagnostics.Add(new("oq2-file-policy", "approval_evidence_digest_mismatch", identifier, path));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateOq3Evidence(
        YamlMappingNode evidence,
        string actualDigest,
        ApprovalPolicy policy,
        DateOnly today)
    {
        const string path = "docs/contract/oq3-authorization-evidence.yaml";
        const string gate = "oq3-authorization-matrix";
        List<GateDiagnostic> diagnostics = [];

        void ExpectScalar(string key, string expected)
        {
            string? observed = TryScalar(evidence, key);
            if (observed is null)
            {
                diagnostics.Add(new(gate, "oq3_evidence_missing", $"OQ3:{key}", path));
            }
            else if (!string.Equals(observed, expected, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq3_evidence_mismatch", $"OQ3:{key}", path));
            }
        }

        ExpectScalar("schema_version", "1.0.0");
        ExpectScalar("evidence_id", "OQ3");
        ExpectScalar("status", "design-approved");
        ExpectScalar("release_gate_status", "in-progress");
        ExpectScalar("matrix_version", ApprovedOq3Version);
        ExpectScalar("matrix_path", "docs/contract/authorization-matrix.md");
        ExpectScalar("approved_on", ApprovedOq3Date);
        ExpectScalar("reopen_policy", ApprovedOq3ReopenPolicy);

        string? matrixDigest = TryScalar(evidence, "matrix_sha256");
        if (matrixDigest is null)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:matrix_sha256", path));
        }
        else if (!string.Equals(matrixDigest, actualDigest, StringComparison.Ordinal)
            || !Regex.IsMatch(matrixDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", "OQ3", path));
        }

        string[] expectedSurfaces =
        [
            "docs/contract/authorization-matrix.md",
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        ];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("canonical_surfaces"), out YamlNode? surfacesNode)
            || surfacesNode is not YamlSequenceNode surfaces)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:canonical-surfaces", path));
        }
        else
        {
            string[] observedSurfaces = surfaces.Children.OfType<YamlScalarNode>()
                .Select(node => node.Value ?? string.Empty).ToArray();
            if (observedSurfaces.Length != expectedSurfaces.Length
                || !observedSurfaces.SequenceEqual(expectedSurfaces, StringComparer.Ordinal)
                || observedSurfaces.Any(surface => !IsRepositoryRelativePath(surface) || !PathExists(surface)))
            {
                diagnostics.Add(new(gate, "oq3_evidence_mismatch", "OQ3:canonical-surfaces", path));
            }
        }

        (string key, string expected)[] expectedDenominator =
        [
            ("access_states", "12"),
            ("canonical_actors", "6"),
            ("negative_access_states", "6"),
            ("denial_routing_states", "8"),
            ("operation_families", "11"),
            ("spine_operations", "49"),
            ("scope_dimensions", "8"),
        ];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("denominator"), out YamlNode? denominatorNode)
            || denominatorNode is not YamlMappingNode denominator)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:denominator", path));
        }
        else
        {
            string[] observedKeys = denominator.Children.Keys.OfType<YamlScalarNode>()
                .Select(node => node.Value ?? string.Empty).ToArray();
            bool invalidDenominator = !observedKeys.SequenceEqual(expectedDenominator.Select(entry => entry.key), StringComparer.Ordinal)
                || expectedDenominator.Any(entry => !string.Equals(TryScalar(denominator, entry.key), entry.expected, StringComparison.Ordinal));
            if (invalidDenominator)
            {
                diagnostics.Add(new(gate, "oq3_evidence_mismatch", "OQ3:denominator", path));
            }
        }

        if (!evidence.Children.TryGetValue(new YamlScalarNode("recorded_gap_ids"), out YamlNode? gapNode)
            || gapNode is not YamlSequenceNode gapIds)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:recorded-gap-ids", path));
        }
        else
        {
            string[] observedGaps = gapIds.Children.OfType<YamlScalarNode>()
                .Select(node => node.Value ?? string.Empty).ToArray();
            if (!observedGaps.SequenceEqual(ApprovedOq3GapIds, StringComparer.Ordinal))
            {
                diagnostics.Add(new(gate, "oq3_evidence_mismatch", "OQ3:recorded-gap-ids", path));
            }
        }

        if (!evidence.Children.TryGetValue(new YamlScalarNode("runtime_posture"), out YamlNode? runtimeNode)
            || runtimeNode is not YamlMappingNode runtime)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:runtime-posture", path));
        }
        else
        {
            bool invalidRuntime = !string.Equals(TryScalar(runtime, "status"), "incomplete", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(TryScalar(runtime, "evidence_claim"));

            if (!runtime.Children.TryGetValue(new YamlScalarNode("incomplete_stories"), out YamlNode? storiesNode)
                || storiesNode is not YamlSequenceNode stories)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedStories = stories.Children.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedStories.SequenceEqual(ApprovedOq3IncompleteStories, StringComparer.Ordinal);
            }

            string[] expectedRequirements = ["FR8", "FR9", "FR10"];
            if (!runtime.Children.TryGetValue(new YamlScalarNode("functional_requirements"), out YamlNode? requirementsNode)
                || requirementsNode is not YamlMappingNode requirements)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedRequirements = requirements.Children.Keys.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedRequirements.SequenceEqual(expectedRequirements, StringComparer.Ordinal)
                    || requirements.Children.Values.Any(node => node is not YamlScalarNode { Value: "incomplete" });
            }

            if (invalidRuntime)
            {
                diagnostics.Add(new(gate, "oq3_evidence_mismatch", "OQ3:runtime-posture", path));
            }
        }

        string[] expectedAuthorities = ["Security", "PM"];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? approvalNode)
            || approvalNode is not YamlMappingNode approval)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:approval", path));
            return diagnostics.ToArray();
        }

        string[] requiredAuthorities = approval.Children.TryGetValue(new YamlScalarNode("required_authorities"), out YamlNode? authoritiesNode)
            && authoritiesNode is YamlSequenceNode authoritySequence
                ? authoritySequence.Children.OfType<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray()
                : [];
        if (requiredAuthorities.Length == 0)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:required-authorities", path));
        }
        else
        {
            foreach (string authority in expectedAuthorities.Where(authority => !requiredAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new(gate, "oq3_approval_incomplete", $"OQ3:{authority}", path));
            }

            if (requiredAuthorities.Length != expectedAuthorities.Length
                || requiredAuthorities.Distinct(StringComparer.Ordinal).Count() != expectedAuthorities.Length
                || requiredAuthorities.Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new(gate, "oq3_approval_extra", "OQ3:required-authorities", path));
            }
        }

        YamlSequenceNode? recordSequence = approval.Children.TryGetValue(new YamlScalarNode("records"), out YamlNode? recordsNode)
            ? recordsNode as YamlSequenceNode
            : null;
        YamlMappingNode[] records = recordSequence?.Children.OfType<YamlMappingNode>().ToArray() ?? [];
        if (records.Length == 0)
        {
            diagnostics.Add(new(gate, "oq3_evidence_missing", "OQ3:approval-records", path));
        }
        else if (recordSequence!.Children.Count != expectedAuthorities.Length
            || records.Length != expectedAuthorities.Length
            || records.Select(record => TryScalar(record, "authority") ?? string.Empty).Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
        {
            diagnostics.Add(new(gate, "oq3_approval_extra", "OQ3:record-count", path));
        }

        foreach (string authority in expectedAuthorities)
        {
            YamlMappingNode[] matches = records
                .Where(record => string.Equals(TryScalar(record, "authority"), authority, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(new(gate, "oq3_approval_incomplete", $"OQ3:{authority}", path));
                if (matches.Length > 1)
                {
                    diagnostics.Add(new(gate, "oq3_approval_extra", "OQ3:record-count", path));
                }

                continue;
            }

            YamlMappingNode record = matches[0];
            string identifier = $"OQ3:{authority}";
            if (!string.Equals(TryScalar(record, "approver"), "Administrator", StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq3_approval_identity_mismatch", identifier, path));
            }

            if (!string.Equals(TryScalar(record, "approved_on"), ApprovedOq3Date, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq3_approval_date_mismatch", identifier, path));
            }
            else
            {
                DateOnly approvedOn = ParseDate(ApprovedOq3Date, "approved_on");
                if (approvedOn > today)
                {
                    diagnostics.Add(new(gate, "approval_date_future", identifier, path));
                }
                else if (today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
                {
                    diagnostics.Add(new(gate, "approval_stale", identifier, path));
                }
            }

            if (!string.Equals(TryScalar(record, "evidence_version"), ApprovedOq3Version, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "approval_evidence_version_mismatch", identifier, path));
            }

            string? recordDigest = TryScalar(record, "evidence_sha256");
            if (recordDigest is null)
            {
                diagnostics.Add(new(gate, "approval_evidence_digest_missing", identifier, path));
            }
            else if (!string.Equals(recordDigest, actualDigest, StringComparison.Ordinal)
                || !Regex.IsMatch(recordDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", identifier, path));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateOq4Evidence(
        YamlMappingNode evidence,
        string actualDigest,
        ApprovalPolicy policy,
        DateOnly today)
    {
        const string path = "docs/contract/oq4-provider-compatibility-evidence.yaml";
        const string gate = "oq4-provider-compatibility-catalog";
        List<GateDiagnostic> diagnostics = [];

        void ExpectScalar(string key, string expected)
        {
            string? observed = TryScalar(evidence, key);
            if (observed is null)
            {
                diagnostics.Add(new(gate, "oq4_evidence_missing", $"OQ4:{key}", path));
            }
            else if (!string.Equals(observed, expected, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq4_evidence_mismatch", $"OQ4:{key}", path));
            }
        }

        void ExpectSequence(string key, string identifier, IReadOnlyList<string> expected)
        {
            if (!evidence.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node)
                || node is not YamlSequenceNode sequence)
            {
                diagnostics.Add(new(gate, "oq4_evidence_missing", $"OQ4:{identifier}", path));
                return;
            }

            string[] observed = sequence.Children.OfType<YamlScalarNode>()
                .Select(item => item.Value ?? string.Empty).ToArray();
            if (!observed.SequenceEqual(expected, StringComparer.Ordinal))
            {
                diagnostics.Add(new(gate, "oq4_evidence_mismatch", $"OQ4:{identifier}", path));
            }
        }

        ExpectScalar("schema_version", "1.0.0");
        ExpectScalar("evidence_id", "OQ4");
        ExpectScalar("status", "design-approved");
        ExpectScalar("release_gate_status", "in-progress");
        ExpectScalar("catalog_version", ApprovedOq4Version);
        ExpectScalar("catalog_path", "docs/contract/provider-compatibility-catalog.md");
        ExpectScalar("approved_on", ApprovedOq4Date);
        ExpectScalar("reopen_policy", ApprovedOq4ReopenPolicy);

        string? catalogDigest = TryScalar(evidence, "catalog_sha256");
        if (catalogDigest is null)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:catalog_sha256", path));
        }
        else if (!string.Equals(catalogDigest, actualDigest, StringComparison.Ordinal)
            || !Regex.IsMatch(catalogDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", "OQ4", path));
        }

        string[] expectedSurfaces =
        [
            "docs/contract/provider-compatibility-catalog.md",
            "tests/contracts/github/pinned-profile.json",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/ProviderCompatibilityCatalogContractTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        ];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("canonical_surfaces"), out YamlNode? surfacesNode)
            || surfacesNode is not YamlSequenceNode surfaces)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:canonical-surfaces", path));
        }
        else
        {
            string[] observedSurfaces = surfaces.Children.OfType<YamlScalarNode>()
                .Select(node => node.Value ?? string.Empty).ToArray();
            if (!observedSurfaces.SequenceEqual(expectedSurfaces, StringComparer.Ordinal)
                || observedSurfaces.Any(surface => !IsRepositoryRelativePath(surface) || !PathExists(surface)))
            {
                diagnostics.Add(new(gate, "oq4_evidence_mismatch", "OQ4:canonical-surfaces", path));
            }
        }

        ExpectSequence("governed_providers", "governed-providers", ApprovedOq4GovernedProviders);
        ExpectSequence("published_ceiling_ids", "published-ceiling-ids", ApprovedOq4CeilingIds);
        ExpectSequence("recorded_gap_ids", "recorded-gap-ids", ApprovedOq4GapIds);

        if (!evidence.Children.TryGetValue(new YamlScalarNode("runtime_posture"), out YamlNode? runtimeNode)
            || runtimeNode is not YamlMappingNode runtime)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:runtime-posture", path));
        }
        else
        {
            // Approval never converts into runtime or credentialed-live completion: the posture must stay
            // incomplete, the live-provider lane must stay not-run, and the incomplete-story list must stay whole.
            bool invalidRuntime = !string.Equals(TryScalar(runtime, "status"), "incomplete", StringComparison.Ordinal)
                || !string.Equals(TryScalar(runtime, "credentialed_live_provider_evidence"), "not-run", StringComparison.Ordinal)
                || !string.Equals(TryScalar(runtime, "c12_evidence_standard"), ApprovedOq4C12EvidenceStandard, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(TryScalar(runtime, "evidence_claim"));

            if (!runtime.Children.TryGetValue(new YamlScalarNode("incomplete_stories"), out YamlNode? storiesNode)
                || storiesNode is not YamlSequenceNode stories)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedStories = stories.Children.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedStories.SequenceEqual(ApprovedOq4IncompleteStories, StringComparer.Ordinal);
            }

            string[] expectedRequirements = ["FR16", "FR17", "FR22", "FR23"];
            if (!runtime.Children.TryGetValue(new YamlScalarNode("functional_requirements"), out YamlNode? requirementsNode)
                || requirementsNode is not YamlMappingNode requirements)
            {
                invalidRuntime = true;
            }
            else
            {
                string[] observedRequirements = requirements.Children.Keys.OfType<YamlScalarNode>()
                    .Select(node => node.Value ?? string.Empty).ToArray();
                invalidRuntime |= !observedRequirements.SequenceEqual(expectedRequirements, StringComparer.Ordinal)
                    || requirements.Children.Values.Any(node => node is not YamlScalarNode { Value: "incomplete" });
            }

            if (invalidRuntime)
            {
                diagnostics.Add(new(gate, "oq4_evidence_mismatch", "OQ4:runtime-posture", path));
            }
        }

        string[] expectedAuthorities = ["Provider", "Architecture", "PM"];
        if (!evidence.Children.TryGetValue(new YamlScalarNode("approval"), out YamlNode? approvalNode)
            || approvalNode is not YamlMappingNode approval)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:approval", path));
            return diagnostics.ToArray();
        }

        string[] requiredAuthorities = approval.Children.TryGetValue(new YamlScalarNode("required_authorities"), out YamlNode? authoritiesNode)
            && authoritiesNode is YamlSequenceNode authoritySequence
                ? authoritySequence.Children.OfType<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray()
                : [];
        if (requiredAuthorities.Length == 0)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:required-authorities", path));
        }
        else
        {
            foreach (string authority in expectedAuthorities.Where(authority => !requiredAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new(gate, "oq4_approval_incomplete", $"OQ4:{authority}", path));
            }

            if (requiredAuthorities.Length != expectedAuthorities.Length
                || requiredAuthorities.Distinct(StringComparer.Ordinal).Count() != expectedAuthorities.Length
                || requiredAuthorities.Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
            {
                diagnostics.Add(new(gate, "oq4_approval_extra", "OQ4:required-authorities", path));
            }
        }

        YamlSequenceNode? recordSequence = approval.Children.TryGetValue(new YamlScalarNode("records"), out YamlNode? recordsNode)
            ? recordsNode as YamlSequenceNode
            : null;
        YamlMappingNode[] records = recordSequence?.Children.OfType<YamlMappingNode>().ToArray() ?? [];
        if (records.Length == 0)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:approval-records", path));
        }
        else if (recordSequence!.Children.Count != expectedAuthorities.Length
            || records.Length != expectedAuthorities.Length
            || records.Select(record => TryScalar(record, "authority") ?? string.Empty).Any(authority => !expectedAuthorities.Contains(authority, StringComparer.Ordinal)))
        {
            diagnostics.Add(new(gate, "oq4_approval_extra", "OQ4:record-count", path));
        }

        foreach (string authority in expectedAuthorities)
        {
            YamlMappingNode[] matches = records
                .Where(record => string.Equals(TryScalar(record, "authority"), authority, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                diagnostics.Add(new(gate, "oq4_approval_incomplete", $"OQ4:{authority}", path));
                if (matches.Length > 1)
                {
                    diagnostics.Add(new(gate, "oq4_approval_extra", "OQ4:record-count", path));
                }

                continue;
            }

            YamlMappingNode record = matches[0];
            string identifier = $"OQ4:{authority}";
            if (!string.Equals(TryScalar(record, "approver"), "Administrator", StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq4_approval_identity_mismatch", identifier, path));
            }

            if (!string.Equals(TryScalar(record, "approved_on"), ApprovedOq4Date, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "oq4_approval_date_mismatch", identifier, path));
            }
            else
            {
                DateOnly approvedOn = ParseDate(ApprovedOq4Date, "approved_on");
                if (approvedOn > today)
                {
                    diagnostics.Add(new(gate, "approval_date_future", identifier, path));
                }
                else if (today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
                {
                    diagnostics.Add(new(gate, "approval_stale", identifier, path));
                }
            }

            if (!string.Equals(TryScalar(record, "evidence_version"), ApprovedOq4Version, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "approval_evidence_version_mismatch", identifier, path));
            }

            string? recordDigest = TryScalar(record, "evidence_sha256");
            if (recordDigest is null)
            {
                diagnostics.Add(new(gate, "approval_evidence_digest_missing", identifier, path));
            }
            else if (!string.Equals(recordDigest, actualDigest, StringComparison.Ordinal)
                || !Regex.IsMatch(recordDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", identifier, path));
            }
        }

        return diagnostics.ToArray();
    }

    private static string? TryScalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
            && value is YamlScalarNode { Value: { Length: > 0 } } scalar
                ? scalar.Value
                : null;

    private static YamlMappingNode SyntheticApprovalRow(
        string criterionId,
        string[] requiredAuthorities,
        (string authority, string approver, string approvedOn)[] records,
        string? reviewBy = null)
    {
        YamlSequenceNode authorities = new(requiredAuthorities.Select(authority => (YamlNode)new YamlScalarNode(authority)));
        YamlSequenceNode recordNodes = new(records.Select(record => (YamlNode)new YamlMappingNode(
            new YamlScalarNode("authority"), new YamlScalarNode(record.authority),
            new YamlScalarNode("approver"), new YamlScalarNode(record.approver),
            new YamlScalarNode("approved_on"), new YamlScalarNode(record.approvedOn))));

        YamlMappingNode approval = new(
            new YamlScalarNode("required_authorities"), authorities,
            new YamlScalarNode("records"), recordNodes);

        if (reviewBy is not null)
        {
            approval.Children[new YamlScalarNode("review_by")] = new YamlScalarNode(reviewBy);
        }

        return new YamlMappingNode(
            new YamlScalarNode("criterion_id"), new YamlScalarNode(criterionId),
            new YamlScalarNode("approval"), approval);
    }

    private static YamlMappingNode RowWithoutApproval(string criterionId) =>
        new(new YamlScalarNode("criterion_id"), new YamlScalarNode(criterionId));

    private static GateDiagnostic[] EvaluateExitCriteriaRows(IReadOnlyList<YamlMappingNode> rows)
    {
        List<GateDiagnostic> diagnostics = [];
        Dictionary<string, int> counts = rows
            .Select(row => RequiredScalar(row, "criterion_id"))
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (string criterion in Criteria)
        {
            if (!counts.ContainsKey(criterion))
            {
                diagnostics.Add(new("exit-criteria", "exit_criteria_missing", criterion, "docs/exit-criteria/c0-c13-governance-evidence.yaml"));
            }
            else if (counts[criterion] > 1)
            {
                diagnostics.Add(new("exit-criteria", "exit_criteria_duplicate", criterion, "docs/exit-criteria/c0-c13-governance-evidence.yaml"));
            }
        }

        foreach (YamlMappingNode row in rows)
        {
            string criterion = RequiredScalar(row, "criterion_id");
            string owner = RequiredScalar(row, "owner");
            string status = RequiredScalar(row, "status");
            string artifact = RequiredScalar(row, "artifact_path");
            string command = RequiredScalar(row, "verification_command");
            string summary = RequiredScalar(row, "result_summary");

            if (new[] { owner, status, command, summary }.Any(IsInvalidPlaceholder)
                || !status.Equals("approved", StringComparison.Ordinal)
                    && !status.Equals("reference_pending", StringComparison.Ordinal))
            {
                diagnostics.Add(new("exit-criteria", "exit_criteria_malformed", criterion, "docs/exit-criteria/c0-c13-governance-evidence.yaml"));
            }

            if (!IsRepositoryRelativePath(artifact) || !PathExists(artifact))
            {
                diagnostics.Add(new("exit-criteria", "artifact_path_invalid", criterion, artifact));
            }

            if (status == "reference_pending" && RequiredSequence(row, "open_policy_placeholders").Children.Count == 0)
            {
                diagnostics.Add(new("exit-criteria", "exit_criteria_malformed", criterion, "docs/exit-criteria/c0-c13-governance-evidence.yaml"));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateSampleConsumption(IReadOnlyList<YamlMappingNode> rows, IReadOnlyList<string> sampleIds)
    {
        List<GateDiagnostic> diagnostics = [];
        HashSet<string> corpus = sampleIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, int> counts = rows
            .Select(row => RequiredScalar(row, "sample_id"))
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (string id in corpus)
        {
            if (!counts.ContainsKey(id))
            {
                diagnostics.Add(new("idempotency-encoding", "idempotency_sample_unmapped", id, "tests/fixtures/idempotency-encoding-corpus-consumption.yaml"));
            }
        }

        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (!corpus.Contains(entry.Key))
            {
                diagnostics.Add(new("idempotency-encoding", "idempotency_sample_stale", entry.Key, "tests/fixtures/idempotency-encoding-corpus-consumption.yaml"));
            }

            if (entry.Value > 1)
            {
                diagnostics.Add(new("idempotency-encoding", "idempotency_sample_duplicate", entry.Key, "tests/fixtures/idempotency-encoding-corpus-consumption.yaml"));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateParityCompleteness(IReadOnlyList<string> operationIds, IReadOnlyList<YamlMappingNode> rows)
    {
        List<GateDiagnostic> diagnostics = [];
        HashSet<string> operations = operationIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, int> rowCounts = rows
            .Select(row => RequiredScalar(row, "operation_id"))
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (string operation in operations)
        {
            if (!rowCounts.ContainsKey(operation))
            {
                diagnostics.Add(new("parity-completeness", "parity_missing_row", operation, "tests/fixtures/parity-contract.yaml"));
            }
        }

        foreach (KeyValuePair<string, int> row in rowCounts)
        {
            if (!operations.Contains(row.Key))
            {
                diagnostics.Add(new("parity-completeness", "parity_stale_row", row.Key, "tests/fixtures/parity-contract.yaml"));
            }

            if (row.Value > 1)
            {
                diagnostics.Add(new("parity-completeness", "parity_duplicate_row", row.Key, "tests/fixtures/parity-contract.yaml"));
            }
        }

        diagnostics.AddRange(EvaluateDuplicateOpenApiOperationIds(operationIds));
        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateDuplicateOpenApiOperationIds(IReadOnlyList<string> operationIds) =>
        operationIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new GateDiagnostic("parity-completeness", "openapi_duplicate_operation_id", group.Key, "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml"))
            .ToArray();

    private static GateDiagnostic? EvaluateCacheKeyCandidate(CacheKeyCandidate candidate)
    {
        if (candidate.HasTenantScope || !string.Equals(candidate.DataScope, "tenant-data", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ExceptionRuleId))
        {
            return null;
        }

        return new("cache-key-lint", "cache_key_unscoped", $"line-{candidate.Line}", candidate.RepositoryPath);
    }

    private static GateDiagnostic[] ScanRepositoryForTenantCacheKeyCandidates()
    {
        string[] includeRoots = ["src", "tests"];
        string[] patterns = ["IMemoryCache", "IDistributedCache", "GetStateAsync", "SaveStateAsync", "StringSetAsync"];
        List<GateDiagnostic> diagnostics = [];

        foreach (string root in includeRoots)
        {
            string absoluteRoot = Path.Combine(RepositoryRoot, root);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedOrBuildOutput(path)))
            {
                string text = File.ReadAllText(file);
                if (!patterns.Any(pattern => text.Contains(pattern, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (text.Contains("CACHE-NON-TENANT", StringComparison.Ordinal))
                {
                    continue;
                }

                string repositoryPath = ToRepositoryPath(file);
                diagnostics.Add(new("cache-key-lint", "cache_key_unscoped", "candidate", repositoryPath));
            }
        }

        string workflowsRoot = Path.Combine(RepositoryRoot, ".github", "workflows");
        if (Directory.Exists(workflowsRoot))
        {
            foreach (string file in Directory.EnumerateFiles(workflowsRoot, "*.yml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(workflowsRoot, "*.yaml", SearchOption.AllDirectories)))
            {
                string text = File.ReadAllText(file);
                if (!text.Contains("actions/cache", StringComparison.Ordinal))
                {
                    continue;
                }

                if (text.Contains("CACHE-NON-TENANT", StringComparison.Ordinal))
                {
                    continue;
                }

                diagnostics.Add(new("cache-key-lint", "cache_key_unscoped", "workflow-cache", ToRepositoryPath(file)));
            }
        }

        return diagnostics.ToArray();
    }

    private static bool IsGeneratedOrBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal)
            || normalized.Contains("/Generated/", StringComparison.Ordinal)
            || normalized.Contains("/quarantine/", StringComparison.Ordinal);
    }

    private static string[] ReadCorpusSampleIds()
    {
        using JsonDocument corpus = JsonDocument.Parse(File.ReadAllText(CorpusPath));
        return RequiredArray(corpus.RootElement, "cases")
            .EnumerateArray()
            .Select(item => RequiredString(item, "id"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static GateDiagnostic[] ValidateCorpusAgainstSchema(JsonDocument corpus, JsonDocument schema)
    {
        List<GateDiagnostic> diagnostics = [];
        const string corpusPath = "tests/fixtures/idempotency-encoding-corpus.json";

        string[] rootRequired = RequiredArray(schema.RootElement, "required").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        foreach (string field in rootRequired)
        {
            if (!corpus.RootElement.TryGetProperty(field, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                diagnostics.Add(new("idempotency-encoding", "corpus_required_field_missing", field, corpusPath));
            }
        }

        if (corpus.RootElement.TryGetProperty("schema_version", out JsonElement schemaVersionElement))
        {
            bool versionAccepted = schemaVersionElement.ValueKind == JsonValueKind.String
                && CorpusConstraints.Value.SchemaVersionPattern.IsMatch(schemaVersionElement.GetString() ?? string.Empty);
            if (!versionAccepted)
            {
                diagnostics.Add(new("idempotency-encoding", "corpus_schema_version_invalid", "schema_version", corpusPath));
            }
        }

        if (!corpus.RootElement.TryGetProperty("cases", out JsonElement casesElement) || casesElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new("idempotency-encoding", "corpus_cases_invalid", "cases", corpusPath));
            return diagnostics.ToArray();
        }

        JsonElement caseSchema = schema.RootElement.GetProperty("$defs").GetProperty("case");
        string[] caseRequired = RequiredArray(caseSchema, "required").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

        HashSet<string> seenIds = new(StringComparer.Ordinal);
        foreach (JsonElement caseElement in casesElement.EnumerateArray())
        {
            string identifier = caseElement.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? string.Empty
                : "<missing-id>";

            foreach (string field in caseRequired)
            {
                if (!caseElement.TryGetProperty(field, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                {
                    diagnostics.Add(new("idempotency-encoding", "corpus_case_required_field_missing", $"{identifier}.{field}", corpusPath));
                }
            }

            if (caseElement.TryGetProperty("id", out JsonElement idValue))
            {
                if (idValue.ValueKind != JsonValueKind.String)
                {
                    diagnostics.Add(new("idempotency-encoding", "corpus_case_id_invalid", identifier, corpusPath));
                }
                else
                {
                    string idText = idValue.GetString() ?? string.Empty;
                    if (!CorpusConstraints.Value.IdPattern.IsMatch(idText))
                    {
                        diagnostics.Add(new("idempotency-encoding", "corpus_case_id_invalid", identifier, corpusPath));
                    }
                    else if (!seenIds.Add(idText))
                    {
                        diagnostics.Add(new("idempotency-encoding", "corpus_case_id_duplicate", identifier, corpusPath));
                    }
                }
            }

            if (caseElement.TryGetProperty("category", out JsonElement categoryElement))
            {
                bool categoryAccepted = categoryElement.ValueKind == JsonValueKind.String
                    && CorpusConstraints.Value.CategoryEnum.Contains(categoryElement.GetString() ?? string.Empty);
                if (!categoryAccepted)
                {
                    diagnostics.Add(new("idempotency-encoding", "corpus_case_category_invalid", identifier, corpusPath));
                }
            }

            if (caseElement.TryGetProperty("equivalence_classification", out JsonElement classElement))
            {
                bool classificationAccepted = classElement.ValueKind == JsonValueKind.String
                    && CorpusConstraints.Value.EquivalenceEnum.Contains(classElement.GetString() ?? string.Empty);
                if (!classificationAccepted)
                {
                    diagnostics.Add(new("idempotency-encoding", "corpus_case_equivalence_invalid", identifier, corpusPath));
                }
            }

            if (caseElement.TryGetProperty("synthetic_data_only", out JsonElement syntheticElement)
                && (syntheticElement.ValueKind != JsonValueKind.True))
            {
                diagnostics.Add(new("idempotency-encoding", "corpus_case_synthetic_data_only_invalid", identifier, corpusPath));
            }

            if (caseElement.TryGetProperty("contains_payload_material", out JsonElement payloadElement)
                && (payloadElement.ValueKind != JsonValueKind.False))
            {
                diagnostics.Add(new("idempotency-encoding", "corpus_case_payload_material_invalid", identifier, corpusPath));
            }
        }

        return diagnostics.ToArray();
    }

    private static string[] LoadOpenApiOperationIds(string path)
    {
        YamlMappingNode root = LoadYamlMapping(path);
        HashSet<string> methodKeys = new(StringComparer.OrdinalIgnoreCase) { "get", "post", "put", "patch", "delete", "head", "options", "trace" };
        List<string> operations = [];

        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                if (methodEntry.Key is not YamlScalarNode keyScalar || string.IsNullOrWhiteSpace(keyScalar.Value))
                {
                    continue;
                }

                if (!methodKeys.Contains(keyScalar.Value!))
                {
                    continue;
                }

                if (methodEntry.Value is not YamlMappingNode operationMapping)
                {
                    continue;
                }

                operations.Add(RequiredScalar(operationMapping, "operationId"));
            }
        }

        return operations.Order(StringComparer.Ordinal).ToArray();
    }

    private static YamlMappingNode[] LoadParityRows(string path)
    {
        YamlStream yaml = LoadYamlStream(path);
        if (yaml.Documents.Count == 0)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: parity-rows-empty: {path}");
        }

        YamlNode rootNode = yaml.Documents[0].RootNode;
        if (rootNode is not YamlSequenceNode sequence)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: parity-rows-not-sequence: {path}");
        }

        return sequence.Children.Cast<YamlMappingNode>().ToArray();
    }

    private static YamlMappingNode[] LoadCriteriaRows(string path) =>
        RequiredSequence(LoadYamlMapping(path), "criteria").Children.Cast<YamlMappingNode>().ToArray();

    private static YamlMappingNode SyntheticParityRow(string operationId)
    {
        YamlMappingNode row = new(
            new YamlScalarNode("operation_id"),
            new YamlScalarNode(operationId),
            new YamlScalarNode("operation_family"),
            new YamlScalarNode("mutating_command"),
            new YamlScalarNode("read_consistency_class"),
            new YamlScalarNode("not_applicable"),
            new YamlScalarNode("adapter_expectations"),
            new YamlSequenceNode(new YamlScalarNode("rest")),
            new YamlScalarNode("transport_parity"),
            new YamlMappingNode(
                new YamlScalarNode("idempotency_key_rule"),
                new YamlScalarNode("required_for_mutating_command"),
                new YamlScalarNode("error_code_set"),
                new YamlSequenceNode(new YamlScalarNode("validation_error"))),
            new YamlScalarNode("behavioral_parity"),
            new YamlMappingNode(new YamlScalarNode("mcp_failure_kind"), new YamlScalarNode("none")));
        return row;
    }

    private static YamlMappingNode CloneRow(YamlMappingNode row)
    {
        YamlStream stream = new(new YamlDocument(row));
        using StringWriter writer = new();
        stream.Save(writer, false);
        using StringReader reader = new(writer.ToString());
        YamlStream copy = new();
        copy.Load(reader);
        return copy.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static void SetScalar(YamlMappingNode row, string key, string value) =>
        row.Children[new YamlScalarNode(key)] = new YamlScalarNode(value);

    private static bool IsInvalidPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    private static bool PathExists(string repositoryPath) =>
        File.Exists(Path.Combine(RepositoryRoot, NormalizeForFileSystem(repositoryPath)));

    private static bool IsRepositoryRelativePath(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        string normalized = repositoryPath.Replace('\\', '/');
        return !Path.IsPathFullyQualified(normalized)
            && !normalized.StartsWith("../", StringComparison.Ordinal)
            && !normalized.Split('/').Contains("..", StringComparer.Ordinal);
    }

    private static void AssertMetadataOnly(string value)
    {
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "raw_payload",
            "file_content",
            "cache-key-value",
            "https://github.com/",
            "https://api.github.com",
            "https://prod.",
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            "/home/",
            "/Users/",
        ];

        foreach (string forbiddenValue in forbidden)
        {
            value.ShouldNotContain(forbiddenValue, Case.Insensitive);
        }

        AbsoluteWindowsDrivePathPattern.IsMatch(value).ShouldBeFalse(value);
    }

    private static YamlStream LoadYamlStream(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: yaml-not-found: {ToRepositoryPath(path)}");
        }

        try
        {
            using StreamReader reader = File.OpenText(path);
            YamlStream yaml = new();
            yaml.Load(reader);
            return yaml;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: yaml-malformed: {ToRepositoryPath(path)}", ex);
        }
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        YamlStream yaml = LoadYamlStream(path);
        if (yaml.Documents.Count == 0)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: yaml-empty: {ToRepositoryPath(path)}");
        }

        YamlNode rootNode = yaml.Documents[0].RootNode;
        if (rootNode is not YamlMappingNode mapping)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: yaml-not-mapping: {ToRepositoryPath(path)}");
        }

        return mapping;
    }

    private static void AssertA6bRegisterStateIsCoherent(string matrixDigest)
    {
        const string matrixPath = "docs/contract/authorization-matrix.md";
        const string conformancePath = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml";
        string[] expectedAuthorities = ["Product", "Architecture", "Security"];

        YamlMappingNode register = LoadYamlMapping(ApprovalRegisterPath);
        YamlMappingNode[] a6bRecords = RequiredSequence(register, "records").Children
            .OfType<YamlMappingNode>()
            .Where(record => string.Equals(TryScalar(record, "gate_id"), "A6b", StringComparison.Ordinal))
            .ToArray();
        a6bRecords.Length.ShouldBe(1, "The approval register must contain exactly one A6b record.");

        YamlMappingNode a6b = a6bRecords[0];
        string decisionPayloadDigest = RequiredScalar(a6b, "decision_payload_sha256");
        Regex.IsMatch(decisionPayloadDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant).ShouldBeTrue();
        RequiredSequence(a6b, "required_authorities").Children
            .Select(node => RequiredScalar(node, "required_authority"))
            .ToArray().ShouldBe(expectedAuthorities);

        string conformanceDigest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(V2ConformanceSetPath)));
        YamlMappingNode conformance = LoadYamlMapping(V2ConformanceSetPath);
        string candidateSetDigest = RequiredScalar(conformance, "candidate_set_sha256");
        string artifactCount = RequiredScalar(conformance, "artifact_count");
        RequiredScalar(conformance, "authorization_matrix_sha256").ShouldBe(matrixDigest);

        YamlMappingNode[] requiredArtifacts = RequiredSequence(a6b, "required_bound_artifacts").Children
            .OfType<YamlMappingNode>()
            .ToArray();
        requiredArtifacts.Length.ShouldBe(2, "A6b must bind exactly the authorization matrix and generated conformance set.");
        AssertA6bBoundArtifacts(
            requiredArtifacts,
            matrixPath,
            matrixDigest,
            conformancePath,
            conformanceDigest,
            candidateSetDigest,
            artifactCount);

        string approvalStatus = RequiredScalar(a6b, "approval_status");
        approvalStatus.ShouldBeOneOf("pending", "approved");
        YamlMappingNode[] approvals = RequiredSequence(a6b, "approvals").Children
            .OfType<YamlMappingNode>()
            .ToArray();

        if (approvalStatus == "pending")
        {
            RequiredScalar(a6b, "approval_readiness").ShouldBe("ready-for-explicit-reapproval");
            approvals.ShouldBeEmpty("A pending A6b record must not retain current approvals.");
            return;
        }

        RequiredScalar(a6b, "approval_readiness").ShouldBe("exact-bound-artifacts-approved");
        approvals.Select(approval => RequiredScalar(approval, "authority"))
            .ToArray().ShouldBe(expectedAuthorities, ignoreOrder: true);
        approvals.Length.ShouldBe(expectedAuthorities.Length);

        ApprovalPolicy policy = LoadApprovalPolicy(LoadYamlMapping(EvidencePath));
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (YamlMappingNode approval in approvals)
        {
            string authority = RequiredScalar(approval, "authority");
            string approver = RequiredScalar(approval, "approver");
            policy.GenericApproverTokens.Contains(approver.Trim().ToLowerInvariant()).ShouldBeFalse(authority);
            ParseRequiredDate(approval, "approved_on").ShouldBeLessThanOrEqualTo(today, authority);
            RequiredScalar(approval, "payload_version").ShouldNotBeNullOrWhiteSpace(authority);
            RequiredScalar(approval, "payload_sha256").ShouldBe(decisionPayloadDigest, authority);

            YamlMappingNode[] boundArtifacts = RequiredSequence(approval, "bound_artifacts").Children
                .OfType<YamlMappingNode>()
                .ToArray();
            boundArtifacts.Length.ShouldBe(2, authority);
            AssertA6bBoundArtifacts(
                boundArtifacts,
                matrixPath,
                matrixDigest,
                conformancePath,
                conformanceDigest,
                candidateSetDigest,
                artifactCount);
        }
    }

    private static void AssertA6bBoundArtifacts(
        IReadOnlyCollection<YamlMappingNode> artifacts,
        string matrixPath,
        string matrixDigest,
        string conformancePath,
        string conformanceDigest,
        string candidateSetDigest,
        string artifactCount)
    {
        YamlMappingNode matrix = artifacts.Single(artifact => RequiredScalar(artifact, "path") == matrixPath);
        (TryScalar(matrix, "version") ?? RequiredScalar(matrix, "required_version")).ShouldBe("2.0.0");
        RequiredScalar(matrix, "sha256").ShouldBe(matrixDigest);

        YamlMappingNode conformance = artifacts.Single(artifact => RequiredScalar(artifact, "path") == conformancePath);
        RequiredScalar(conformance, "sha256").ShouldBe(conformanceDigest);
        RequiredScalar(conformance, "declared_candidate_set_sha256").ShouldBe(candidateSetDigest);
        RequiredScalar(conformance, "declared_authorization_matrix_sha256").ShouldBe(matrixDigest);
        RequiredScalar(conformance, "artifact_count").ShouldBe(artifactCount);
    }

    private static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static JsonElement RequiredArray(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        value.ValueKind.ShouldBe(JsonValueKind.Array, property);
        return value;
    }

    private static string RequiredString(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        value.ValueKind.ShouldBe(JsonValueKind.String, property);
        string? text = value.GetString();
        text.ShouldNotBeNullOrWhiteSpace(property);
        return text!;
    }

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return RequiredScalar(value, key);
    }

    private static string RequiredScalar(YamlNode node, string name)
    {
        string? value = node.ShouldBeOfType<YamlScalarNode>().Value;
        value.ShouldNotBeNullOrWhiteSpace(name);
        return value!;
    }

    private static bool ParseRequiredBoolean(YamlMappingNode mapping, string key)
    {
        string raw = RequiredScalar(mapping, key);
        return raw.ToLowerInvariant() switch
        {
            "true" or "yes" or "on" or "y" => true,
            "false" or "no" or "off" or "n" => false,
            _ => throw new FormatException($"GOVERNANCE-PREREQUISITE-DRIFT: boolean-invalid: {key}={raw}"),
        };
    }

    private static DateOnly ParseRequiredDate(YamlMappingNode mapping, string key) =>
        ParseDate(RequiredScalar(mapping, key), key);

    private static DateOnly ParseDate(string raw, string name)
    {
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value))
        {
            throw new FormatException($"GOVERNANCE-PREREQUISITE-DRIFT: date-invalid: {name}={raw}");
        }

        return value;
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace("\\", "/", StringComparison.Ordinal);

    private static string ReadRootTargetFramework()
    {
        string buildPropsPath = Path.Combine(RepositoryRoot, "Directory.Build.props");
        string content = File.ReadAllText(buildPropsPath);
        Match match = Regex.Match(content, "<TargetFramework>(?<tfm>[^<]+)</TargetFramework>");
        match.Success.ShouldBeTrue("Directory.Build.props missing TargetFramework element");
        return match.Groups["tfm"].Value.Trim();
    }

    private static string FindRepositoryRoot()
    {
        foreach (string seed in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? current = new(seed);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: repository root was not found.");
    }

    private static CorpusSchemaConstraints LoadCorpusSchemaConstraints()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(CorpusSchemaPath));
        JsonElement properties = schema.RootElement.GetProperty("properties");
        JsonElement defs = schema.RootElement.GetProperty("$defs");

        string versionPattern = properties.GetProperty("schema_version").GetProperty("pattern").GetString()
            ?? throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: corpus-schema-malformed: schema_version.pattern missing");
        string idPattern = defs.GetProperty("case").GetProperty("properties").GetProperty("id").GetProperty("pattern").GetString()
            ?? throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: corpus-schema-malformed: case.id.pattern missing");

        HashSet<string> categoryEnum = defs.GetProperty("category").GetProperty("enum").EnumerateArray()
            .Select(element => element.GetString() ?? throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: corpus-schema-malformed: category enum value not string"))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> equivalenceEnum = defs.GetProperty("equivalence_classification").GetProperty("enum").EnumerateArray()
            .Select(element => element.GetString() ?? throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: corpus-schema-malformed: equivalence_classification enum value not string"))
            .ToHashSet(StringComparer.Ordinal);

        return new CorpusSchemaConstraints(
            new Regex(versionPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
            new Regex(idPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
            categoryEnum,
            equivalenceEnum);
    }

    private static GateDiagnostic[] EvaluateCacheKeyExceptionApprovalStates(IReadOnlyList<YamlMappingNode> exceptions)
    {
        const string ManifestPath = "tests/fixtures/cache-key-exceptions.yaml";
        List<GateDiagnostic> diagnostics = [];

        foreach (YamlMappingNode exception in exceptions)
        {
            string ruleId = exception.Children.TryGetValue(new YamlScalarNode("rule_id"), out YamlNode? ruleNode)
                && ruleNode is YamlScalarNode { Value: { Length: > 0 } } ruleScalar
                    ? ruleScalar.Value!
                    : "<missing-rule-id>";

            string status = exception.Children.TryGetValue(new YamlScalarNode("review_status"), out YamlNode? statusNode)
                && statusNode is YamlScalarNode { Value: { Length: > 0 } } statusScalar
                    ? statusScalar.Value!
                    : string.Empty;

            if (!string.Equals(status, "approved", StringComparison.Ordinal))
            {
                diagnostics.Add(new("cache-key-lint", "cache_key_exception_not_approved", ruleId, ManifestPath));
            }
        }

        return diagnostics.ToArray();
    }

    private static YamlMappingNode SyntheticCacheKeyException(string ruleId, string reviewStatus, string? lastReviewedOn = null) =>
        new(
            new YamlScalarNode("rule_id"), new YamlScalarNode(ruleId),
            new YamlScalarNode("owner"), new YamlScalarNode("Governance Gates"),
            new YamlScalarNode("reason"), new YamlScalarNode("Synthetic review-status test fixture; not durable tenant data."),
            new YamlScalarNode("scope"), new YamlScalarNode("synthetic-test-fixture"),
            new YamlScalarNode("review_status"), new YamlScalarNode(reviewStatus),
            new YamlScalarNode("last_reviewed_on"), new YamlScalarNode(lastReviewedOn ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new YamlScalarNode("evidence_link"), new YamlScalarNode("tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs"));

    private sealed record CacheKeyCandidate(string RepositoryPath, int Line, string DataScope, bool HasTenantScope, string? ExceptionRuleId);

    private sealed record ApprovalPolicy(int MaxAgeDays, HashSet<string> GenericApproverTokens);

    private sealed record CorpusSchemaConstraints(
        Regex SchemaVersionPattern,
        Regex IdPattern,
        HashSet<string> CategoryEnum,
        HashSet<string> EquivalenceEnum);

    private sealed record GateDiagnostic(string Gate, string Category, string Identifier, string RepositoryPath)
    {
        public override string ToString() =>
            $"{Gate}:{Category}: id={Identifier}; path={RepositoryPath}";
    }
}
