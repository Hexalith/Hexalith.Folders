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
        "BindRepository",
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "AddFile",
        "ChangeFile",
        "RemoveFile",
        "CommitWorkspace",
        "GetWorkspaceStatus",
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
    public void ContractRulesArtifactDeclaresStableTablesAndOperationRows()
    {
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "contract", "idempotency-and-parity-rules.md"));

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
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "contract", "idempotency-and-parity-rules.md"));

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
            string spine = File.ReadAllText(spinePath);
            spine.ShouldContain("openapi: 3.1.0", Case.Sensitive, "Story 1.5 must not reshape the Contract Spine away from its OpenAPI 3.1 foundation; if the spine is present it must remain Story 1.6's foundation.");
            SpineContractAssertions.AssertNoDownstreamOperationGroups(spine);
        }

        File.Exists(Path.Combine(root, "tests", "fixtures", "parity-contract.yaml"))
            .ShouldBeFalse("Story 1.5 must not generate parity result rows; Story 1.13 owns parity-contract.yaml.");

        AssertNoStory15ContrabandScope(root);
    }

    [Fact]
    public void ParitySchemaDeclaresTransportAndBehavioralColumns()
    {
        string root = RepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "parity-contract.schema.json")));

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
        failureKinds.ShouldContain("audit_access_denied");
        failureKinds.ShouldContain("input_limit_exceeded");
        failureKinds.ShouldContain("response_limit_exceeded");
        failureKinds.ShouldContain("query_timeout");
        failureKinds.ShouldContain("read_model_unavailable");
        failureKinds.ShouldContain("idempotency_conflict");
        failureKinds.ShouldContain("provider_outcome_unknown");
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
            "duplicate_binding"
        })
        {
            errorCategories.ShouldContain(category, $"canonical_error_category enum must include {category} so operation inventory rows validate.");
        }
    }

    [Fact]
    public void EncodingCorpusCoversRequiredSyntheticCategories()
    {
        string root = RepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.json")));

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
        string root = RepositoryRoot();
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.schema.json")));

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

    private static string FindMarkdownRow(string content, string sectionHeading, string operation)
    {
        int sectionStart = content.IndexOf(sectionHeading, StringComparison.Ordinal);
        sectionStart.ShouldBeGreaterThanOrEqualTo(0, $"{sectionHeading} should exist.");

        int nextSection = content.IndexOf("\n## ", sectionStart + sectionHeading.Length, StringComparison.Ordinal);
        string section = nextSection < 0 ? content[sectionStart..] : content[sectionStart..nextSection];
        string marker = $"| `{operation}` |";
        string[] matches = section
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(marker, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one row starting with '{marker}' in section '{sectionHeading.Trim()}', but found {matches.Length}. " +
                "Either the operation is missing from the table, the row is duplicated, or the section heading is wrong.");
        }

        return matches[0];
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
        File.Exists(parityOracleRunner).ShouldBeFalse("Story 1.5 must not introduce a parity-oracle generator runner; Story 1.13 owns oracle generation.");
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

}
