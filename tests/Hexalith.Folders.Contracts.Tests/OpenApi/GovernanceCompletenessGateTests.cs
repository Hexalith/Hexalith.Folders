using System.Text.Json;
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
        script.ShouldNotContain("--recursive", Case.Insensitive);

        documentation.ShouldContain(".\\tests\\tools\\run-governance-completeness-gates.ps1");
        documentation.ShouldContain("prerequisite_drift");
        documentation.ShouldContain("idempotency_sample_unmapped");
        documentation.ShouldContain("cache_key_unscoped");
        documentation.ShouldContain("parity_completeness_mismatch");
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
                RequiredSequence(row, "open_policy_placeholders").Children.Count.ShouldBeGreaterThan(0, RequiredScalar(row, "criterion_id"));
            }
        }
    }

    [Fact]
    public void ExitCriteriaNegativeControlsFailClosedWithBoundedDiagnostics()
    {
        YamlMappingNode[] rows = LoadCriteriaRows(EvidencePath);
        List<YamlMappingNode> missing = rows.Where(row => RequiredScalar(row, "criterion_id") != "C13").ToList();
        List<YamlMappingNode> duplicate = rows.Concat([rows[0]]).ToList();
        YamlMappingNode invalidPlaceholder = CloneRow(rows[0]);
        SetScalar(invalidPlaceholder, "owner", "PLACEHOLDER");
        YamlMappingNode invalidPath = CloneRow(rows[1]);
        SetScalar(invalidPath, "artifact_path", "D:/not/repository/local.md");

        EvaluateExitCriteriaRows(missing).ShouldContain(d => d.Category == "exit_criteria_missing" && d.Identifier == "C13");
        EvaluateExitCriteriaRows(duplicate).ShouldContain(d => d.Category == "exit_criteria_duplicate" && d.Identifier == "C0");
        EvaluateExitCriteriaRows([invalidPlaceholder]).ShouldContain(d => d.Category == "exit_criteria_malformed");
        EvaluateExitCriteriaRows([invalidPath]).ShouldContain(d => d.Category == "artifact_path_invalid");

        foreach (GateDiagnostic diagnostic in EvaluateExitCriteriaRows([invalidPlaceholder, invalidPath]))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void IdempotencyCorpusSchemaAndStableConsumptionMapCoverEverySample()
    {
        using JsonDocument corpus = JsonDocument.Parse(File.ReadAllText(CorpusPath));
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(CorpusSchemaPath));
        string[] requiredCaseFields = RequiredArray(schema.RootElement.GetProperty("$defs").GetProperty("case"), "required").SelectText();

        JsonElement[] cases = RequiredArray(corpus.RootElement, "cases").EnumerateArray().ToArray();
        cases.ShouldNotBeEmpty();
        foreach (JsonElement item in cases)
        {
            foreach (string requiredField in requiredCaseFields)
            {
                item.TryGetProperty(requiredField, out _).ShouldBeTrue(requiredField);
            }
        }

        YamlMappingNode[] consumption = RequiredSequence(LoadYamlMapping(CorpusConsumptionPath), "samples").Children.Cast<YamlMappingNode>().ToArray();
        Dictionary<string, string> classifications = cases.ToDictionary(
            item => RequiredString(item, "id"),
            item => RequiredString(item, "equivalence_classification"),
            StringComparer.Ordinal);

        consumption.Select(row => RequiredScalar(row, "sample_id")).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(classifications.Keys.Order(StringComparer.Ordinal).ToArray());
        consumption.Select(row => RequiredScalar(row, "sample_id")).Distinct(StringComparer.Ordinal).Count().ShouldBe(consumption.Length);

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

        EvaluateSampleConsumption(rows.Where(row => RequiredScalar(row, "sample_id") != sampleIds[0]), sampleIds).ShouldContain(d => d.Category == "idempotency_sample_unmapped");
        EvaluateSampleConsumption(rows.Concat([rows[0]]), sampleIds).ShouldContain(d => d.Category == "idempotency_sample_duplicate");

        YamlMappingNode stale = CloneRow(rows[0]);
        SetScalar(stale, "sample_id", "deleted-synthetic-sample");
        EvaluateSampleConsumption([stale], sampleIds).ShouldContain(d => d.Category == "idempotency_sample_stale");
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

        YamlMappingNode[] examples = RequiredSequence(manifest, "examples").Children.Cast<YamlMappingNode>().ToArray();
        examples.ShouldContain(row => RequiredScalar(row, "classification") == "compilable-csharp");
        examples.ShouldContain(row => RequiredScalar(row, "classification") == "documentation-only");

        foreach (YamlMappingNode example in examples)
        {
            RequiredScalar(example, "marker").ShouldStartWith("<!-- hexalith-example:");
            RequiredScalar(example, "synthetic_data_only").ShouldBe("true");
            PathExists(RequiredScalar(example, "source_path")).ShouldBeTrue(RequiredScalar(example, "example_id"));
        }
    }

    [Fact]
    public void CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope()
    {
        YamlMappingNode[] exceptions = RequiredSequence(LoadYamlMapping(CacheKeyExceptionsPath), "exceptions").Children.Cast<YamlMappingNode>().ToArray();
        exceptions.ShouldNotBeEmpty();
        foreach (YamlMappingNode exception in exceptions)
        {
            RequiredScalar(exception, "rule_id").ShouldStartWith("CACHE-");
            RequiredScalar(exception, "owner").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(exception, "reason").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(exception, "scope").ShouldNotBeNullOrWhiteSpace();
            RequiredScalar(exception, "review_status").ShouldBeOneOf("approved", "expires_on");
            PathExists(RequiredScalar(exception, "evidence_link")).ShouldBeTrue();
        }

        GateDiagnostic[] diagnostics = ScanRepositoryForTenantCacheKeyCandidates();
        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
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
        YamlMappingNode create = SyntheticParityRow("CreateFolder");
        YamlMappingNode stale = SyntheticParityRow("RemovedOperation");

        EvaluateParityCompleteness(operations, [create]).ShouldContain(d => d.Category == "parity_missing_row" && d.Identifier == "GetWorkspaceStatus");
        EvaluateParityCompleteness(operations, [create, stale]).ShouldContain(d => d.Category == "parity_stale_row" && d.Identifier == "RemovedOperation");
        EvaluateParityCompleteness(operations, [create, create]).ShouldContain(d => d.Category == "parity_duplicate_row" && d.Identifier == "CreateFolder");
        EvaluateDuplicateOpenApiOperationIds(["CreateFolder", "CreateFolder"]).ShouldContain(d => d.Category == "openapi_duplicate_operation_id");
    }

    private static GateDiagnostic[] EvaluateExitCriteriaRows(IEnumerable<YamlMappingNode> rows)
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

    private static GateDiagnostic[] EvaluateSampleConsumption(IEnumerable<YamlMappingNode> rows, string[] sampleIds)
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
            else if (entry.Value > 1)
            {
                diagnostics.Add(new("idempotency-encoding", "idempotency_sample_duplicate", entry.Key, "tests/fixtures/idempotency-encoding-corpus-consumption.yaml"));
            }
        }

        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateParityCompleteness(string[] operationIds, IEnumerable<YamlMappingNode> rows)
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
            else if (row.Value > 1)
            {
                diagnostics.Add(new("parity-completeness", "parity_duplicate_row", row.Key, "tests/fixtures/parity-contract.yaml"));
            }
        }

        diagnostics.AddRange(EvaluateDuplicateOpenApiOperationIds(operationIds));
        return diagnostics.ToArray();
    }

    private static GateDiagnostic[] EvaluateDuplicateOpenApiOperationIds(string[] operationIds) =>
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
        string[] includeRoots = ["src", "tests", ".github", "docs"];
        string[] patterns = ["IMemoryCache", "IDistributedCache", "GetStateAsync", "SaveStateAsync", "StringSetAsync"];
        List<GateDiagnostic> diagnostics = [];

        foreach (string root in includeRoots)
        {
            string absoluteRoot = Path.Combine(RepositoryRoot, root);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*.*", SearchOption.AllDirectories).Where(IsTextFile).Where(path => !IsGeneratedOrBuildOutput(path)))
            {
                string text = File.ReadAllText(file);
                if (!patterns.Any(pattern => text.Contains(pattern, StringComparison.Ordinal)))
                {
                    continue;
                }

                string repositoryPath = ToRepositoryPath(file);
                if (!text.Contains("tenant", StringComparison.OrdinalIgnoreCase)
                    && !text.Contains("CACHE-NON-TENANT", StringComparison.Ordinal))
                {
                    diagnostics.Add(new("cache-key-lint", "cache_key_unscoped", "candidate", repositoryPath));
                }
            }
        }

        return diagnostics.ToArray();
    }

    private static bool IsTextFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".cs" or ".csproj" or ".props" or ".targets" or ".json" or ".yaml" or ".yml" or ".md" or ".ps1";
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

    private static string[] LoadOpenApiOperationIds(string path)
    {
        YamlMappingNode root = LoadYamlMapping(path);
        List<string> operations = [];
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = RequiredScalar(methodEntry.Key, "method").ToLowerInvariant();
                if (method is "get" or "post" or "put" or "patch" or "delete")
                {
                    operations.Add(RequiredScalar(methodEntry.Value.ShouldBeOfType<YamlMappingNode>(), "operationId"));
                }
            }
        }

        return operations.Order(StringComparer.Ordinal).ToArray();
    }

    private static YamlMappingNode[] LoadParityRows(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlSequenceNode>().Children.Cast<YamlMappingNode>().ToArray();
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
        File.Exists(Path.Combine(RepositoryRoot, NormalizeForFileSystem(repositoryPath)))
        || Directory.Exists(Path.Combine(RepositoryRoot, NormalizeForFileSystem(repositoryPath)));

    private static bool IsRepositoryRelativePath(string repositoryPath) =>
        !string.IsNullOrWhiteSpace(repositoryPath)
        && !Path.IsPathFullyQualified(repositoryPath)
        && !repositoryPath.Contains('\\', StringComparison.Ordinal)
        && !repositoryPath.StartsWith("../", StringComparison.Ordinal)
        && !repositoryPath.Split('/').Contains("..", StringComparer.Ordinal);

    private static void AssertMetadataOnly(string value)
    {
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "raw_payload",
            "file_content",
            "cache-key-value=",
            "https://github.com/",
            "https://api.github.com",
            "https://prod.",
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            RepositoryRoot.Replace("\\", "\\\\", StringComparison.Ordinal),
            "C:\\",
            "D:\\",
            "/home/",
            "/Users/",
        ];

        foreach (string forbiddenValue in forbidden)
        {
            value.ShouldNotContain(forbiddenValue, Case.Insensitive);
        }
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
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
        return value.GetString() ?? string.Empty;
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

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace("\\", "/", StringComparison.Ordinal);

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

    private sealed record CacheKeyCandidate(string RepositoryPath, int Line, string DataScope, bool HasTenantScope, string? ExceptionRuleId);

    private sealed record GateDiagnostic(string Gate, string Category, string Identifier, string RepositoryPath)
    {
        public override string ToString() =>
            $"{Gate}:{Category}: id={Identifier}; path={RepositoryPath}";
    }
}
