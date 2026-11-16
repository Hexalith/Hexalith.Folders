using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Shouldly;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
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
    // Historical approval bytes and current technical evidence have separate bindings.
    private const string CurrentOq4Sha256 = "4a9d360e446ddbc622a6bc977a5a7357bd0dbc99d30d6d39d3c36efe0dde1474";
    private const string CurrentOq4PolicySha256 = "156929f0695d364d515dc71adfdf3292903b4f08538e723f6015633655ebd8a4";
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
        "docs/governance/approval-policy.md",
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
    public void Oq8DesignDocumentsIdempotencyContract()
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
            else if (policy.MaxAgeDays > 0 && today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
            {
                diagnostics.Add(new("exit-criteria", "approval_stale", $"{criterion}:{authority}", path));
            }
        }

        // Optional per-criterion review-by / expiry date: if present it must be a valid date strictly
        // in the future, so an approval cannot sit indefinitely past its own declared re-review date.
        KeyValuePair<YamlNode, YamlNode>[] reviewByEntries = approval.Children
            .Where(entry => entry.Key is YamlScalarNode { Value: "review_by" })
            .ToArray();
        if (reviewByEntries.Length > 0)
        {
            if (reviewByEntries.Length != 1
                || !IsYamlString(reviewByEntries[0].Key)
                || !IsYamlString(reviewByEntries[0].Value)
                || !DateOnly.TryParseExact(((YamlScalarNode)reviewByEntries[0].Value).Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly reviewBy))
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
                else if (policy.MaxAgeDays > 0 && today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
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
                else if (policy.MaxAgeDays > 0 && today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
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
                else if (policy.MaxAgeDays > 0 && today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
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
        else if (!string.Equals(catalogDigest, ApprovedOq4Sha256, StringComparison.Ordinal)
            || !Regex.IsMatch(catalogDigest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", "OQ4", path));
        }

        if (!evidence.Children.TryGetValue(new YamlScalarNode("current_evidence"), out YamlNode? currentNode)
            || currentNode is not YamlMappingNode current)
        {
            diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:current-evidence", path));
        }
        else
        {
            (string Key, string Expected)[] currentBindings =
            [
                ("status", "technical-refresh"),
                ("catalog_version", ApprovedOq4Version),
                ("catalog_sha256", CurrentOq4Sha256),
                ("historical_catalog_sha256", ApprovedOq4Sha256),
                ("refreshed_on", "2026-10-08"),
                ("policy_path", "docs/governance/approval-policy.md"),
                ("policy_sha256", CurrentOq4PolicySha256),
                ("source_revision", "6b7274ac06f1bdcb03bc6b10071384406bfd7386"),
                ("change_scope", "forgejo-live-evidence-classification"),
                ("owner_decision_required", "false"),
            ];
            string[] expectedFields = currentBindings.Select(binding => binding.Key).Append("rationale").Order(StringComparer.Ordinal).ToArray();
            string[] observedFields = current.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
            if (current.Children.Count != observedFields.Length
                || !observedFields.SequenceEqual(expectedFields, StringComparer.Ordinal))
            {
                diagnostics.Add(new(gate, "oq4_evidence_mismatch", "OQ4:current-evidence:fields", path));
            }

            foreach ((string key, string expected) in currentBindings)
            {
                string? observed = TryScalar(current, key);
                if (observed is null)
                {
                    diagnostics.Add(new(gate, "oq4_evidence_missing", $"OQ4:current-evidence:{key}", path));
                }
                else if (!string.Equals(observed, expected, StringComparison.Ordinal))
                {
                    diagnostics.Add(new(gate, "oq4_evidence_mismatch", $"OQ4:current-evidence:{key}", path));
                }
            }

            if (!string.Equals(actualDigest, CurrentOq4Sha256, StringComparison.Ordinal)
                || !string.Equals(TryScalar(current, "catalog_sha256"), actualDigest, StringComparison.Ordinal))
            {
                diagnostics.Add(new(gate, "approval_evidence_digest_mismatch", "OQ4:current-evidence", path));
            }

            if (string.IsNullOrWhiteSpace(TryScalar(current, "rationale")))
            {
                diagnostics.Add(new(gate, "oq4_evidence_missing", "OQ4:current-evidence:rationale", path));
            }
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
                else if (policy.MaxAgeDays > 0 && today.DayNumber - approvedOn.DayNumber > policy.MaxAgeDays)
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
            else if (!string.Equals(recordDigest, ApprovedOq4Sha256, StringComparison.Ordinal)
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
            if (counts[criterion] > 1 && Criteria.Contains(criterion, StringComparer.Ordinal))
            {
                continue;
            }

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

        return diagnostics.ToArray();
    }

    private static bool IsGeneratedOrBuildOutput(string path)
    {
        string normalized = "/" + path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Generated/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/quarantine/", StringComparison.OrdinalIgnoreCase);
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

    private static string[] LoadOpenApiOperationIds(string path) => LoadOpenApiOperationIds(LoadYamlMapping(path));

    private static string[] LoadOpenApiOperationIds(YamlMappingNode root)
    {
        Dictionary<string, YamlNode> rootFields = ReadOpenApiFields(root, "openapi-paths-invalid");
        if (!rootFields.TryGetValue("paths", out YamlNode? pathsNode))
        {
            throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-paths-invalid");
        }

        Dictionary<string, YamlNode> paths = ReadOpenApiFields(pathsNode, "openapi-paths-invalid", "openapi-path-key-invalid");
        HashSet<string> methodKeys = new(StringComparer.Ordinal) { "get", "post", "put", "patch", "delete", "head", "options", "trace" };
        HashSet<string> metadataKeys = new(StringComparer.Ordinal) { "summary", "description", "parameters", "servers" };
        List<string> operations = [];

        foreach (KeyValuePair<string, YamlNode> pathEntry in paths)
        {
            string pathKey = pathEntry.Key;
            if (IsOpenApiExtension(pathKey))
            {
                continue;
            }

            if (!pathKey.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-path-key-invalid");
            }

            Dictionary<string, YamlNode> pathItem = ReadOpenApiFields(pathEntry.Value, "openapi-path-item-invalid", "openapi-method-key-invalid");
            if (pathItem.ContainsKey("$ref"))
            {
                throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-path-item-invalid");
            }

            foreach (KeyValuePair<string, YamlNode> methodEntry in pathItem)
            {
                string methodKey = methodEntry.Key;
                if (metadataKeys.Contains(methodKey))
                {
                    bool valid = methodKey is "summary" or "description"
                        ? IsYamlString(methodEntry.Value, allowEmpty: true)
                        : methodEntry.Value is YamlSequenceNode && HasYamlCollectionTag(methodEntry.Value, "seq");
                    if (!valid)
                    {
                        throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-path-metadata-invalid");
                    }

                    continue;
                }

                if (IsOpenApiExtension(methodKey))
                {
                    continue;
                }

                if (!methodKeys.Contains(methodKey))
                {
                    throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-method-key-invalid");
                }

                Dictionary<string, YamlNode> operation = ReadOpenApiFields(methodEntry.Value, "openapi-operation-invalid");
                if (operation.ContainsKey("$ref")
                    || !operation.TryGetValue("operationId", out YamlNode? idNode)
                    || !IsYamlString(idNode))
                {
                    throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: openapi-operation-invalid");
                }

                operations.Add(((YamlScalarNode)idNode).Value!);
            }
        }

        return operations.Order(StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, YamlNode> ReadOpenApiFields(YamlNode node, string reason, string? keyReason = null)
    {
        if (node is not YamlMappingNode mapping || !HasYamlCollectionTag(node, "map"))
        {
            throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: " + reason);
        }

        Dictionary<string, YamlNode> fields = new(StringComparer.Ordinal);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            if (!IsYamlString(entry.Key)
                || !fields.TryAdd(((YamlScalarNode)entry.Key).Value!, entry.Value))
            {
                throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: " + (keyReason ?? reason));
            }
        }

        return fields;
    }

    private static bool HasYamlCollectionTag(YamlNode node, string kind) =>
        node.Tag.IsEmpty || node.Tag.ToString() is "?" or "!" || node.Tag.ToString() == "tag:yaml.org,2002:" + kind;

    private static bool IsOpenApiExtension(string key) =>
        key.Length > 2 && key.StartsWith("x-", StringComparison.Ordinal);

    private static bool IsYamlString(YamlNode node, bool allowEmpty = false)
    {
        if (node is not YamlScalarNode scalar || scalar.Value is null
            || (!allowEmpty && string.IsNullOrWhiteSpace(scalar.Value)))
        {
            return false;
        }

        string tag = scalar.Tag.ToString();
        if (tag is "tag:yaml.org,2002:str" or "!")
        {
            return true;
        }

        if (!scalar.Tag.IsEmpty && tag != "?")
        {
            return false;
        }

        // The representation model keeps untagged plain scalars as text. Resolve the
        // YAML 1.2 core non-string forms without culture-dependent numeric parsing.
        return scalar.Style is not (ScalarStyle.Plain or ScalarStyle.Any)
            || (scalar.Value.Length > 0 && !NonStringYamlCoreScalarPattern().IsMatch(scalar.Value));
    }

    [GeneratedRegex(@"\A(?:~|null|Null|NULL|true|True|TRUE|false|False|FALSE|0o[0-7]+|0x[0-9a-fA-F]+|[-+]?(?:\.[0-9]+|[0-9]+(?:\.[0-9]*)?)(?:[eE][-+]?[0-9]+)?|[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN))\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NonStringYamlCoreScalarPattern();

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

    private static YamlMappingNode CloneRow(YamlMappingNode row) =>
        (YamlMappingNode)CloneYamlNode(row, new Dictionary<YamlNode, YamlNode>(ReferenceEqualityComparer.Instance));

    private static YamlNode CloneYamlNode(YamlNode node, Dictionary<YamlNode, YamlNode> copies)
    {
        if (copies.TryGetValue(node, out YamlNode? copy))
        {
            return copy;
        }

        copy = node switch
        {
            YamlScalarNode scalar => new YamlScalarNode(scalar.Value) { Style = scalar.Style },
            YamlSequenceNode sequence => new YamlSequenceNode { Style = sequence.Style },
            YamlMappingNode mapping => new YamlMappingNode { Style = mapping.Style },
            _ => throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: yaml-node-kind-unsupported"),
        };
        copy.Tag = node.Tag;
        copy.Anchor = node.Anchor;
        copies.Add(node, copy);

        // Aliases share a node in the representation graph, including recursive
        // collections. Register each copy before walking its children.
        if (node is YamlMappingNode sourceMapping && copy is YamlMappingNode targetMapping)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> child in sourceMapping.Children)
            {
                targetMapping.Add(CloneYamlNode(child.Key, copies), CloneYamlNode(child.Value, copies));
            }
        }
        else if (node is YamlSequenceNode sourceSequence && copy is YamlSequenceNode targetSequence)
        {
            foreach (YamlNode child in sourceSequence.Children)
            {
                targetSequence.Add(CloneYamlNode(child, copies));
            }
        }

        return copy;
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
        catch (YamlDotNet.Core.YamlException)
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: yaml-malformed: {ToRepositoryPath(path)}");
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

    private static string ReadRootTargetFramework() =>
        ReadRootTargetFramework(File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Build.props")));

    private static string ReadRootTargetFramework(string content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(content);
        }
        catch (XmlException)
        {
            throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: target-framework-xml-invalid");
        }

        XElement? project = document.Root;
        XElement[] declarations = project?.Name.LocalName == "Project"
            && (project.Name.Namespace == XNamespace.None || project.Name.NamespaceName == "http://schemas.microsoft.com/developer/msbuild/2003")
            ? project.Elements(project.Name.Namespace + "PropertyGroup")
                .Where(group => string.IsNullOrWhiteSpace((string?)group.Attribute("Condition")))
                .SelectMany(group => group.Elements(project.Name.Namespace + "TargetFramework"))
                .Where(property => string.IsNullOrWhiteSpace((string?)property.Attribute("Condition")))
                .ToArray()
            : [];

        if (declarations.Length != 1 || declarations[0].HasElements
            || !Regex.IsMatch(declarations[0].Value.Trim(), @"\Anet[0-9]+\.[0-9]+(?:-[A-Za-z0-9.]+)?\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: target-framework-declaration-invalid");
        }

        return declarations[0].Value.Trim();
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
