using System.Text.Json;
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

        foreach (string operation in MutatingCommands)
        {
            string row = FindMarkdownRow(content, "## Mutating Command Equivalence", operation);
            row.ShouldContain("required", Case.Insensitive, $"{operation} should require idempotency metadata.");
            row.ShouldContain("tenant_id", Case.Sensitive, $"{operation} equivalence should be tenant-scoped.");
            row.ShouldContain("idempotency_conflict", Case.Sensitive, $"{operation} should define conflicting-payload outcome.");
        }

        foreach (string operation in NonMutatingOperations)
        {
            string row = FindMarkdownRow(content, "## Non-Mutating Read Consistency", operation);
            row.ShouldContain("does-not-accept", Case.Sensitive, $"{operation} should document non-idempotent query semantics.");
            row.ShouldMatch("(snapshot-per-task|read-your-writes|eventually-consistent)");
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
            spine.ShouldContain("paths: {}", Case.Sensitive, "Story 1.5 must not introduce operation paths into the Contract Spine.");
        }

        File.Exists(Path.Combine(root, "tests", "fixtures", "parity-contract.yaml"))
            .ShouldBeFalse("Story 1.5 must not generate parity result rows.");
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
        allowedAdapters.ShouldBe(["rest", "sdk", "cli", "mcp"], ignoreOrder: true);

        string[] failureKinds = rootElement.GetProperty("$defs").GetProperty("mcp_failure_kind").GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        failureKinds.ShouldContain("usage_error");
        failureKinds.ShouldContain("credential_missing");
        failureKinds.ShouldContain("tenant_access_denied");
        failureKinds.ShouldContain("idempotency_conflict");
        failureKinds.ShouldContain("provider_outcome_unknown");
        failureKinds.ShouldContain("state_transition_invalid");
    }

    [Fact]
    public void EncodingCorpusCoversRequiredSyntheticCategories()
    {
        string root = RepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.json")));

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
        }
    }

    private static string FindMarkdownRow(string content, string sectionHeading, string operation)
    {
        int sectionStart = content.IndexOf(sectionHeading, StringComparison.Ordinal);
        sectionStart.ShouldBeGreaterThanOrEqualTo(0, $"{sectionHeading} should exist.");

        int nextSection = content.IndexOf("\n## ", sectionStart + sectionHeading.Length, StringComparison.Ordinal);
        string section = nextSection < 0 ? content[sectionStart..] : content[sectionStart..nextSection];
        string marker = $"| `{operation}` |";
        return section
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Single(line => line.StartsWith(marker, StringComparison.Ordinal));
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
