using System.Text.Json;
using Hexalith.Folders.Testing.Tests.Helpers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class ContractRulesArtifactTests
{
    private static readonly string[] MvpOperations =
    [
        "ValidateProviderReadiness",
        "CreateFolder",
        "CreateRepositoryBackedFolder",
        "BindRepository",
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "AddFile",
        "ChangeFile",
        "RemoveFile",
        "CommitWorkspace",
        "GetWorkspaceStatus",
        "GetWorkspaceCleanupStatus",
        "ListFolderFiles",
        "SearchFolderFiles",
        "ReadFileRange",
        "GetAuditTrail"
    ];

    private static readonly string[] PhaseOneCandidates =
    [
        "GetEffectivePermissions",
        "ArchiveFolder",
        "GetFolderLifecycleStatus",
        "GetProviderSupportEvidence",
        "GetOperationTimeline",
        "ListOperationsConsoleFolders",
        "GetOperationsConsoleWorkspace",
        "GetOperationsConsoleAuditTimeline"
    ];

    private static readonly string[] MutatingCommands =
    [
        "CreateFolder",
        "CreateRepositoryBackedFolder",
        "BindRepository",
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "AddFile",
        "ChangeFile",
        "RemoveFile",
        "CommitWorkspace",
        "ArchiveFolder"
    ];

    private static readonly string[] NonMutatingOperations =
    [
        "ValidateProviderReadiness",
        "GetWorkspaceStatus",
        "GetWorkspaceCleanupStatus",
        "ListFolderFiles",
        "SearchFolderFiles",
        "ReadFileRange",
        "GetAuditTrail",
        "GetEffectivePermissions",
        "GetFolderLifecycleStatus",
        "GetProviderSupportEvidence",
        "GetOperationTimeline",
        "ListOperationsConsoleFolders",
        "GetOperationsConsoleWorkspace",
        "GetOperationsConsoleAuditTimeline"
    ];

    [Fact]
    public void VerificationCoverageMatchesStoryAcceptanceCriteriaAndTestMethods()
    {
        const string rulesArtifact = "docs/contract/idempotency-and-parity-rules.md";
        string root = RepositoryArtifacts.FindRoot();
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, rulesArtifact));
        string story = RepositoryArtifacts.ReadText(Path.Combine(
            root, "_bmad-output", "implementation-artifacts", "1-5-finalize-idempotency-equivalence-and-adapter-parity-rules.md"));

        string[] storyCriteria = FindSection(story, "## Acceptance Criteria")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(". ", 2)[0])
            .Where(number => number.All(char.IsAsciiDigit))
            .Select(number => $"AC{number}")
            .ToArray();

        string stableTables = nameof(ContractRulesArtifactDeclaresStableTablesAndOperationRows);
        string readAndWriteScope = nameof(ContractRulesArtifactCoversIdempotencyReadConsistencyAndNegativeScope);
        (string Artifact, string Check, string Criteria, string Test, string? Helper)[] expected =
        [
            (rulesArtifact, "all MVP operations and Phase 1 candidates appear in stable table rows", "AC1, AC8, AC9", stableTables, null),
            (rulesArtifact, "every mutating command declares tenant-scoped equivalence, idempotency key behavior, TTL tier, duplicate outcome, conflict outcome, correlation behavior, and task identity behavior", "AC2, AC8", readAndWriteScope, null),
            (rulesArtifact, "every non-mutating operation declares read-consistency class, safe denial shape, audit keys, correlation behavior, projection expectation, and non-idempotent semantics", "AC3, AC8, AC9", readAndWriteScope, null),
            (rulesArtifact, "SDK, CLI, and MCP sourcing and projection rules preserve architecture Adapter Parity Contract dimensions", "AC4", stableTables, null),
            ("tests/fixtures/parity-contract.schema.json", "transport and behavioral parity columns are required and adapter/failure-kind enums are bounded", "AC5", nameof(ParitySchemaDeclaresTransportAndBehavioralColumns), null),
            ("tests/fixtures/idempotency-encoding-corpus.json", "synthetic corpus covers Unicode normalization, zero-width joiner, casing, ordering, whitespace, null versus omitted, percent encoding, malformed keys, and duplicate keys; schema_version is pinned and every case carries comparison_input plus field_path", "AC6", nameof(EncodingCorpusCoversRequiredSyntheticCategories), null),
            ("tests/fixtures/idempotency-encoding-corpus.schema.json", "case shape, category enum, and equivalence_classification enum are declared so future contributors cannot omit required fields", "AC6", nameof(EncodingCorpusSchemaDeclaresRequiredCaseShape), null),
            ("negative-scope guardrail", "no OpenAPI spine reshape, parity result rows, generated SDK output, NSwag configuration, parity-oracle runner, REST/CLI/MCP behavior, CI workflow gate, or nested-submodule initialization is added by this story", "AC7, AC8", readAndWriteScope, nameof(AssertNoStory15ContrabandScope)),
        ];

        foreach (string test in expected.Select(row => row.Test).Distinct(StringComparer.Ordinal))
        {
            (typeof(ContractRulesArtifactTests).GetMethod(test)?.IsDefined(typeof(FactAttribute), inherit: false) == true)
                .ShouldBeTrue($"{test} must be an executable test.");
        }

        string[][] rows = FindSection(content, "## Verification Coverage")
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('|'))
            .Select(SplitRowCells)
            .Skip(2)
            .ToArray();
        foreach (string[] row in rows)
        {
            row.Length.ShouldBe(5, "Verification Coverage rows must have five columns.");
            row[4].ShouldBe("implemented", $"Verification Coverage status for {row[1]}");
        }

        var actual = rows.Select(row => (Artifact: row[0].Trim('`'), Check: row[1], Criteria: row[2], Method: row[3])).ToArray();
        var coverage = expected.Select(row => (
            row.Artifact,
            row.Check,
            row.Criteria,
            Method: $"`{nameof(ContractRulesArtifactTests)}.{row.Test}`" + (row.Helper is null ? string.Empty : $" plus `{row.Helper}`")));
        actual.Order().ShouldBe(coverage.Order(), "Verification Coverage must cite the tests that cover each criterion.");
        string[] coveredCriteria = actual.SelectMany(row => row.Criteria.Split(", ")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        coveredCriteria.SequenceEqual(storyCriteria.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            .ShouldBeTrue("Verification Coverage must account for all story criteria.");
    }

    [Fact]
    public void ContractRulesArtifactDeclaresStableTablesAndOperationRows()
    {
        string root = RepositoryArtifacts.FindRoot();
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "contract", "idempotency-and-parity-rules.md"));

        string[] requiredHeadings =
        [
            "## Decision Record",
            "## Operation Inventory",
            "## Mutating Command Equivalence",
            "## Non-Mutating Read Consistency",
            "## Adapter Parity Dimensions",
            "## Adapter Outcome Parity",
            "## Deferred Owner",
            "## Verification Coverage"
        ];

        string[] requiredHeaders =
        [
            "| operation_id | inventory_status | operation_family | mutating | idempotency_requirement | read_consistency_class | resource_scope | authoritative_tenant_source | identity_requirements | lock_requirement | state_or_projection_family | canonical_error_categories | audit_metadata_keys | parity_surfaces | deferred_owner |",
            "| operation_id | idempotency_key_rule | idempotency_key_sourcing | x_hexalith_idempotency_equivalence | idempotency_ttl_tier | duplicate_equivalent_outcome | conflicting_payload_outcome | correlation_behavior | task_identity_behavior | parser_policy | negative_examples | deferred_owner |",
            "| operation_id | read_consistency_class | authorization_outcome_class | safe_denial_shape | audit_metadata_keys | correlation_behavior | terminal_or_projection_expectation | non_idempotent_semantics | c3_c4_constraints | negative_examples | deferred_owner |",
            "| dimension | sdk | cli | mcp | parity_assertion | schema_or_oracle_column |",
            "| outcome | sdk_projection | cli_projection | mcp_projection | canonical_category | cli_exit_code | mcp_failure_kind |",
            "| deferred_owner | deferred_scope | blocking_input | consumer_story | status |",
            "| artifact | check | acceptance_criteria | verification_method | status |"
        ];

        foreach (string heading in requiredHeadings)
        {
            content.ShouldContain(heading, Case.Sensitive);
        }

        foreach (string header in requiredHeaders)
        {
            content.ShouldContain(header, Case.Sensitive);
        }

        AssertAdapterParityRules(content);

        foreach (string operation in MvpOperations.Concat(PhaseOneCandidates))
        {
            content.ShouldContain($"| `{operation}` |", Case.Sensitive, $"Operation inventory should include {operation}.");
        }

        foreach (string candidate in PhaseOneCandidates)
        {
            content.ShouldContain($"| `{candidate}` | Phase 1 inventory candidate |", Case.Sensitive, $"{candidate} should be explicitly marked as a candidate.");
        }
    }

    [Fact]
    public void ContractRulesArtifactCoversIdempotencyReadConsistencyAndNegativeScope()
    {
        string root = RepositoryArtifacts.FindRoot();
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "contract", "idempotency-and-parity-rules.md"));

        const int mutatingEquivalenceColumnCount = 12;
        foreach (string operation in MutatingCommands)
        {
            string row = FindMarkdownRow(content, "## Mutating Command Equivalence", operation);
            string[] cells = SplitRowCells(row);
            cells.Length.ShouldBe(mutatingEquivalenceColumnCount, $"{operation} row in Mutating Command Equivalence should have {mutatingEquivalenceColumnCount} columns; observed row: {row}");

            string idempotencyKeyRule = cells[1];
            idempotencyKeyRule.ShouldStartWith("required", Case.Sensitive, $"{operation} idempotency_key_rule column should start with 'required'.");

            string equivalenceList = cells[3];
            equivalenceList.ShouldContain("tenant_id", Case.Sensitive, $"{operation} equivalence list (column 4) should include tenant_id as the tenant-scoping anchor.");

            string conflictingPayloadOutcome = cells[6];
            conflictingPayloadOutcome.ShouldContain("idempotency_conflict", Case.Sensitive, $"{operation} conflicting_payload_outcome column should reference idempotency_conflict.");
        }

        const int nonMutatingColumnCount = 11;
        foreach (string operation in NonMutatingOperations)
        {
            string row = FindMarkdownRow(content, "## Non-Mutating Read Consistency", operation);
            string[] cells = SplitRowCells(row);
            cells.Length.ShouldBe(nonMutatingColumnCount, $"{operation} row in Non-Mutating Read Consistency should have {nonMutatingColumnCount} columns; observed row: {row}");

            string readConsistencyCell = cells[1];
            readConsistencyCell.ShouldBeOneOf("snapshot-per-task", "read-your-writes", "eventually-consistent");

            string nonIdempotentCell = cells[7];
            nonIdempotentCell.ShouldContain("does-not-accept", Case.Sensitive, $"{operation} non_idempotent_semantics column should explicitly state does-not-accept-idempotency-key.");
        }

        string[] negativeGuardrails =
        [
            "cross-tenant key reuse",
            "changed payload semantics",
            "changed credential scope",
            "missing idempotency key",
            "malformed idempotency key",
            "pre-SDK validation failure",
            "post-SDK service failure",
            "non-equivalent replay"
        ];

        foreach (string guardrail in negativeGuardrails)
        {
            content.ShouldContain(guardrail, Case.Insensitive);
        }

        string spinePath = Path.Combine(root, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
        if (File.Exists(spinePath))
        {
            string spine = RepositoryArtifacts.ReadText(spinePath);
            spine.ShouldContain("openapi: 3.1.0", Case.Sensitive, "Story 1.5 must not reshape the Contract Spine away from its OpenAPI 3.1 foundation; if the spine is present it must remain Story 1.6's foundation.");
            SpineContractAssertions.AssertNoDownstreamOperationGroups(spine);
        }

        File.Exists(Path.Combine(root, "tests", "fixtures", "parity-contract.yaml"))
            .ShouldBeTrue("Story 1.13 owns the generated parity result rows.");

        AssertNoStory15ContrabandScope(root);
    }

    [Fact]
    public void ParitySchemaDeclaresTransportAndBehavioralColumns()
    {
        string root = RepositoryArtifacts.FindRoot();
        using JsonDocument document = JsonDocument.Parse(RepositoryArtifacts.ReadText(Path.Combine(root, "tests", "fixtures", "parity-contract.schema.json")));

        JsonElement rootElement = document.RootElement;
        string[] rootRequired = rootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        rootRequired.ShouldContain("operation_id");
        rootRequired.ShouldContain("read_consistency_class");
        rootRequired.ShouldContain("transport_parity");
        rootRequired.ShouldContain("behavioral_parity");

        JsonElement transport = rootElement.GetProperty("properties").GetProperty("transport_parity");
        string[] transportRequired = transport.GetProperty("required").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        transportRequired.ShouldContain("auth_outcome_class");
        transportRequired.ShouldContain("error_code_set");
        transportRequired.ShouldContain("idempotency_key_rule");
        transportRequired.ShouldContain("audit_metadata_keys");
        transportRequired.ShouldContain("correlation_field_path");
        transportRequired.ShouldContain("terminal_states");

        JsonElement behavioral = rootElement.GetProperty("properties").GetProperty("behavioral_parity");
        string[] behavioralRequired = behavioral.GetProperty("required").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        behavioralRequired.ShouldContain("pre_sdk_error_class");
        behavioralRequired.ShouldContain("idempotency_key_sourcing");
        behavioralRequired.ShouldContain("correlation_id_sourcing");
        behavioralRequired.ShouldContain("task_id_sourcing");
        behavioralRequired.ShouldContain("credential_sourcing");
        behavioralRequired.ShouldContain("cli_exit_code");
        behavioralRequired.ShouldContain("mcp_failure_kind");

        string[] allowedAdapters = rootElement.GetProperty("$defs").GetProperty("adapter_name").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        allowedAdapters.ShouldBe(["rest", "sdk", "cli", "mcp", "ui"], ignoreOrder: true);

        string[] operationFamilies = rootElement.GetProperty("properties").GetProperty("operation_family").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        operationFamilies.ShouldContain("context_query", "operation_family enum must include context_query to cover ListFolderFiles/SearchFolderFiles/ReadFileRange.");

        string[] failureKinds = rootElement.GetProperty("$defs").GetProperty("mcp_failure_kind").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        failureKinds.ShouldContain("usage_error");
        failureKinds.ShouldContain("credential_missing");
        failureKinds.ShouldContain("tenant_access_denied");
        failureKinds.ShouldContain("folder_acl_denied");
        failureKinds.ShouldNotContain("audit_access_denied");
        failureKinds.ShouldContain("input_limit_exceeded");
        failureKinds.ShouldContain("response_limit_exceeded");
        failureKinds.ShouldContain("query_timeout");
        failureKinds.ShouldContain("read_model_unavailable");
        failureKinds.ShouldContain("idempotency_conflict");
        failureKinds.ShouldContain("idempotency_key_expired");
        failureKinds.ShouldContain("concurrency_conflict");
        // Story 1.13 P-26 collapsed the duplicate spelling 'provider_outcome_unknown' to the canonical
        // 'unknown_provider_outcome'. Both still describe the same failure kind.
        failureKinds.ShouldContain("unknown_provider_outcome");
        failureKinds.ShouldNotContain("provider_outcome_unknown");
        failureKinds.ShouldContain("state_transition_invalid");

        string[] errorCategories = rootElement.GetProperty("$defs").GetProperty("canonical_error_category").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        foreach (string category in new[]
        {
            "credential_reference_missing",
            "workspace_not_ready",
            "dirty_workspace",
            "commit_failed",
            "file_operation_failed",
            "path_validation_failed",
            "provider_permission_insufficient",
            "unsupported_provider_capability",
            "repository_conflict",
            "duplicate_binding",
            "idempotency_key_expired"
        })
        {
            errorCategories.ShouldContain(category, $"canonical_error_category enum must include {category} so operation inventory rows validate.");
        }
    }

    [Fact]
    public void EncodingCorpusCoversRequiredSyntheticCategories()
    {
        string root = RepositoryArtifacts.FindRoot();
        using JsonDocument document = JsonDocument.Parse(RepositoryArtifacts.ReadText(Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.json")));

        document.RootElement.GetProperty("schema_version").GetString().ShouldBe("0.2.0-story-1-5", "schema_version must match the value Story 1.5 bumped to; later stories that change the corpus shape must bump again.");

        string[] categories = document.RootElement.GetProperty("cases").EnumerateArray()
            .Select(item => item.GetProperty("category").GetString() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        string[] requiredCategories =
        [
            "NFC",
            "NFD",
            "NFKC",
            "NFKD",
            "zero-width-joiner",
            "casing",
            "ULID casing",
            "ordering",
            "whitespace",
            "null-vs-omitted",
            "percent-encoding",
            "malformed-idempotency-key",
            "duplicate-json-key"
        ];

        foreach (string category in requiredCategories)
        {
            categories.ShouldContain(category);
        }

        foreach (JsonElement sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            sample.GetProperty("synthetic_data_only").GetBoolean().ShouldBeTrue();
            sample.GetProperty("contains_payload_material").GetBoolean().ShouldBeFalse();
            sample.GetProperty("equivalence_classification").GetString().ShouldNotBeNullOrWhiteSpace();
            sample.GetProperty("field_path").GetString().ShouldNotBeNullOrWhiteSpace();
            sample.GetProperty("comparison_input").ValueKind.ShouldNotBe(JsonValueKind.Undefined, "every corpus case must declare a comparison_input so paired equivalence behavior is unambiguous.");
        }
    }

    [Fact]
    public void EncodingCorpusSchemaDeclaresRequiredCaseShape()
    {
        string root = RepositoryArtifacts.FindRoot();
        using JsonDocument schema = JsonDocument.Parse(RepositoryArtifacts.ReadText(Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.schema.json")));

        JsonElement caseDefinition = schema.RootElement.GetProperty("$defs").GetProperty("case");
        string[] required = caseDefinition.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        required.ShouldContain("id");
        required.ShouldContain("category");
        required.ShouldContain("input");
        required.ShouldContain("comparison_input");
        required.ShouldContain("field_path");
        required.ShouldContain("code_points");
        required.ShouldContain("equivalence_classification");
        required.ShouldContain("synthetic_data_only");
        required.ShouldContain("contains_payload_material");

        string[] allowedCategories = schema.RootElement.GetProperty("$defs").GetProperty("category").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        allowedCategories.ShouldContain("NFC");
        allowedCategories.ShouldContain("NFD");
        allowedCategories.ShouldContain("NFKC");
        allowedCategories.ShouldContain("NFKD");
        allowedCategories.ShouldContain("duplicate-json-key");
        allowedCategories.ShouldContain("malformed-idempotency-key");

        string[] equivalenceClassifications = schema.RootElement.GetProperty("$defs").GetProperty("equivalence_classification").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        equivalenceClassifications.ShouldContain("parser-rejected", "Encoding-corpus schema must enumerate parser-rejected as a valid equivalence classification.");
    }

    private static void AssertAdapterParityRules(string content)
    {
        (string Dimension, string Sdk, string Cli, string Mcp)[] dimensions =
        [
            ("idempotency key sourcing", "`IIdempotencyKeyProvider`", "`--idempotency-key` or `--allow-auto-key`", "required `idempotencyKey`"),
            ("correlation ID sourcing", "`ICorrelationIdProvider`", "`--correlation-id`", "optional `correlationId`"),
            ("task ID sourcing", "caller-provided header", "required `--task-id`", "required `taskId`"),
            ("credential sourcing", "DI access token provider", "`HEXALITH_TOKEN`, credential file, then `--token`", "`auth.token` or `auth.tokenFile`"),
            ("pre-SDK error projection", "before HTTP call", "usage/config errors exit 64; missing credentials exit 65", "missing credentials `credential_missing`"),
            ("post-SDK error projection", "canonical Problem Details fields", "canonical category maps to CLI exit code", "canonical category maps to MCP failure kind"),
        ];
        foreach ((string dimension, string sdk, string cli, string mcp) in dimensions)
        {
            string[] cells = SplitRowCells(FindMarkdownRow(content, "## Adapter Parity Dimensions", dimension));
            cells.Length.ShouldBe(6, dimension);
            cells[1].ShouldContain(sdk, Case.Sensitive, dimension);
            cells[2].ShouldContain(cli, Case.Sensitive, dimension);
            cells[3].ShouldContain(mcp, Case.Sensitive, dimension);
        }

        string[] credentials = SplitRowCells(FindMarkdownRow(content, "## Adapter Outcome Parity", "missing credentials"));
        credentials.Length.ShouldBe(7, "missing credentials");
        credentials[4..].ShouldBe(["credential_missing", "65", "credential_missing"]);
    }

    private static string FindMarkdownRow(string content, string sectionHeading, string rowLabel)
    {
        string section = FindSection(content, sectionHeading);
        string[] matches = section
            .Split('\n')
            .Where(line => line.StartsWith($"| {rowLabel} |", StringComparison.Ordinal)
                || line.StartsWith($"| `{rowLabel}` |", StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one row for '{rowLabel}' in section '{sectionHeading.Trim()}', but found {matches.Length}.");
        }

        return matches[0];
    }

    private static string FindSection(string content, string heading)
    {
        content = content.ReplaceLineEndings("\n");
        int start = content.IndexOf($"\n{heading}\n", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"{heading} should exist.");
        start += heading.Length + 2;
        int end = content.IndexOf("\n## ", start, StringComparison.Ordinal);
        return end < 0 ? content[start..] : content[start..end];
    }

    private static string[] SplitRowCells(string row)
    {
        string trimmed = row.Trim();
        if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
        {
            throw new InvalidOperationException($"Row does not look like a markdown table row: {row}");
        }

        string inner = trimmed[1..^1];
        return inner.Split('|').Select(cell => cell.Trim()).ToArray();
    }

    private static void AssertNoStory15ContrabandScope(string root)
    {
        string[] generatedSdkDirs =
        [
            Path.Combine(root, "src", "Hexalith.Folders.Client.Generated"),
            Path.Combine(root, "src", "Hexalith.Folders.Sdk.Generated"),
            Path.Combine(root, "src", "Hexalith.Folders.Contracts", "Generated")
        ];

        foreach (string generatedDir in generatedSdkDirs)
        {
            Directory.Exists(generatedDir).ShouldBeFalse($"Story 1.5 must not produce generated SDK output at '{generatedDir}'; Story 1.12 owns NSwag generation.");
        }

        string[] nswagConfigs =
        [
            Path.Combine(root, "nswag.json"),
            Path.Combine(root, "src", "Hexalith.Folders.Sdk", "nswag.json"),
            Path.Combine(root, "src", "Hexalith.Folders.Contracts", "nswag.json")
        ];

        foreach (string nswagConfig in nswagConfigs)
        {
            File.Exists(nswagConfig).ShouldBeFalse($"Story 1.5 must not introduce NSwag configuration at '{nswagConfig}'; Story 1.12 owns NSwag wiring.");
        }

        string parityOracleRunner = Path.Combine(root, "tests", "tools", "parity-oracle-generator", "Program.cs");
        File.Exists(parityOracleRunner).ShouldBeTrue("Story 1.13 owns the parity-oracle generator runner.");
    }
}
