using System.Globalization;
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
    private static readonly string CorpusPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus.json");
    private static readonly string CorpusSchemaPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus.schema.json");
    private static readonly string CorpusConsumptionPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "idempotency-encoding-corpus-consumption.yaml");
    private static readonly string PatternManifestPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "pattern-example-manifest.yaml");
    private static readonly string CacheKeyExceptionsPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "cache-key-exceptions.yaml");
    private static readonly string ParityContractPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml");
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string WorkflowPath = Path.Combine(RepositoryRoot, ".github", "workflows", "contract-spine.yml");
    private static readonly string GateScriptPath = Path.Combine(RepositoryRoot, "tests", "tools", "run-governance-completeness-gates.ps1");
    private static readonly string GateDocumentationPath = Path.Combine(RepositoryRoot, "docs", "contract", "governance-and-completeness-ci-gates.md");
    private static readonly string SolutionPath = Path.Combine(RepositoryRoot, "Hexalith.Folders.slnx");

    private static readonly Regex MarkerPattern = new(
        @"^<!-- hexalith-example: [a-z][a-z0-9-]* -->$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AbsoluteWindowsDrivePathPattern = new(
        @"[A-Za-z]:\\",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Lazy<CorpusSchemaConstraints> CorpusConstraints = new(LoadCorpusSchemaConstraints);

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
    ];

    [Fact]
    public void WorkflowAndScriptExposeOneOfflineGovernanceCompletenessCommand()
    {
        string workflow = File.ReadAllText(WorkflowPath);
        string script = File.ReadAllText(GateScriptPath);
        string documentation = File.ReadAllText(GateDocumentationPath);

        workflow.ShouldContain("./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild");
        workflow.ShouldContain("actions/checkout@v6");
        workflow.ShouldContain("submodules: false");
        workflow.ShouldContain("actions/setup-dotnet@v5");
        workflow.ShouldContain("global-json-file: global.json");
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj");
        script.ShouldContain("FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests");
        script.ShouldContain("tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj");
        script.ShouldContain("_bmad-output/gates/governance-completeness/latest.json");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("utf8NoBOM");
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
        AssertMetadataOnly(documentation);
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

        // Validate every row that declares an approval block (pinned or future), so a newly approval-backed
        // criterion is covered even before someone extends ApprovalBackedCriteria.
        GateDiagnostic[] diagnostics = rows
            .Where(HasApprovalBlock)
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
