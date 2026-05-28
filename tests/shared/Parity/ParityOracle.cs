using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// Test-only reader for the committed parity oracle (<c>tests/fixtures/parity-contract.yaml</c>).
/// It loads the oracle <b>in place</b> (never a forked copy) and exposes a typed, adapter-agnostic view so the
/// CLI, MCP, SDK, REST, and end-to-end test projects assert their projections against the contract's own
/// columns. Story 5.4 introduced the reader for the <c>behavioral_parity</c> + <c>outcome_mapping</c> columns
/// (CLI/MCP). Story 5.5 extends it <b>additively</b> with the <c>transport_parity</c> block and the row-level
/// <c>read_consistency_class</c> for the REST/SDK transport-parity assertions and the dual-surface golden
/// lifecycle run.
/// </summary>
/// <remarks>
/// <para>This source is linked (not copied) into <c>Hexalith.Folders.Cli.Tests</c>,
/// <c>Hexalith.Folders.Mcp.Tests</c>, <c>Hexalith.Folders.Client.Tests</c>,
/// <c>Hexalith.Folders.Server.Tests</c>, and <c>Hexalith.Folders.IntegrationTests</c>, so every surface is
/// provably driven by one loader and one row set. It deliberately references no adapter projection (the CLI
/// and MCP projections are <c>internal</c> to their own assemblies); it yields raw oracle strings/ints only.
/// The adapter-specific assertion lives in each project's own conformance tests.</para>
/// <para>The loading approach mirrors the proven pattern in
/// <c>Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs</c>: walk up from
/// <see cref="AppContext.BaseDirectory"/> to the directory holding <c>Hexalith.Folders.slnx</c>, then read
/// <c>tests/fixtures/parity-contract.yaml</c> with <c>YamlDotNet.RepresentationModel</c>.</para>
/// </remarks>
internal static class ParityOracle
{
    private static readonly Lazy<IReadOnlyList<ParityRow>> LazyRows = new(LoadRowsFromDisk);

    /// <summary>Gets the absolute path of the committed oracle file, resolved from the repository root.</summary>
    public static string OraclePath
        => Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "parity-contract.yaml");

    /// <summary>Gets the parsed oracle rows (cached after the first read).</summary>
    public static IReadOnlyList<ParityRow> Rows => LazyRows.Value;

    /// <summary>Reads and parses the committed oracle file (bypasses the cache).</summary>
    /// <returns>The parsed rows.</returns>
    public static IReadOnlyList<ParityRow> Load() => LoadRowsFromDisk();

    /// <summary>
    /// Gets the deduplicated <c>canonical_error_category → cli_exit_code</c> map, asserting the invariant that a
    /// category never carries two different exit codes across the operation rows (it must not).
    /// </summary>
    /// <returns>The category→exit-code map.</returns>
    /// <exception cref="InvalidOperationException">A category carries conflicting exit codes across rows.</exception>
    public static IReadOnlyDictionary<string, int> CategoryCliExitCodes()
    {
        Dictionary<string, int> map = new(StringComparer.Ordinal);
        foreach (ParityRow row in Rows)
        {
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                if (map.TryGetValue(mapping.CanonicalErrorCategory, out int existing) && existing != mapping.CliExitCode)
                {
                    throw new InvalidOperationException(
                        $"Oracle category '{mapping.CanonicalErrorCategory}' carries conflicting cli_exit_code {existing} and {mapping.CliExitCode} across operation rows.");
                }

                map[mapping.CanonicalErrorCategory] = mapping.CliExitCode;
            }
        }

        return map;
    }

    /// <summary>
    /// Gets the deduplicated <c>canonical_error_category → mcp_failure_kind</c> map, asserting the invariant that
    /// a category never carries two different kinds across the operation rows (it must not).
    /// </summary>
    /// <returns>The category→failure-kind map.</returns>
    /// <exception cref="InvalidOperationException">A category carries conflicting kinds across rows.</exception>
    public static IReadOnlyDictionary<string, string> CategoryMcpFailureKinds()
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (ParityRow row in Rows)
        {
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                if (map.TryGetValue(mapping.CanonicalErrorCategory, out string? existing) && !string.Equals(existing, mapping.McpFailureKind, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Oracle category '{mapping.CanonicalErrorCategory}' carries conflicting mcp_failure_kind '{existing}' and '{mapping.McpFailureKind}' across operation rows.");
                }

                map[mapping.CanonicalErrorCategory] = mapping.McpFailureKind;
            }
        }

        return map;
    }

    /// <summary>Gets the distinct set of <c>canonical_error_category</c> values appearing in any outcome mapping.</summary>
    /// <returns>The distinct category set (ordinal).</returns>
    public static IReadOnlySet<string> DistinctCategories()
        => Rows.SelectMany(row => row.OutcomeMappings).Select(mapping => mapping.CanonicalErrorCategory).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<ParityRow> LoadRowsFromDisk()
    {
        using StreamReader reader = File.OpenText(OraclePath);
        YamlStream yaml = new();
        yaml.Load(reader);

        YamlSequenceNode root = AsSequence(yaml.Documents[0].RootNode, "<root>");
        return root.Children.Select(node => MapRow(AsMapping(node, "<row>"))).ToArray();
    }

    private static ParityRow MapRow(YamlMappingNode row)
    {
        YamlMappingNode behavioral = RequiredMapping(row, "behavioral_parity");
        YamlMappingNode transport = RequiredMapping(row, "transport_parity");
        OutcomeMapping[] outcomes = RequiredSequence(row, "outcome_mapping").Children
            .Select(node => MapOutcome(AsMapping(node, "outcome_mapping[]")))
            .ToArray();

        return new ParityRow(
            OperationId: RequiredScalar(row, "operation_id"),
            OperationFamily: RequiredScalar(row, "operation_family"),
            ReadConsistencyClass: RequiredScalar(row, "read_consistency_class"),
            PreSdkErrorClass: RequiredScalar(behavioral, "pre_sdk_error_class"),
            IdempotencyKeySourcing: RequiredScalar(behavioral, "idempotency_key_sourcing"),
            CorrelationIdSourcing: RequiredScalar(behavioral, "correlation_id_sourcing"),
            TaskIdSourcing: RequiredScalar(behavioral, "task_id_sourcing"),
            CredentialSourcing: RequiredScalar(behavioral, "credential_sourcing"),
            SuccessCliExitCode: RequiredInt(behavioral, "cli_exit_code"),
            SuccessMcpFailureKind: RequiredScalar(behavioral, "mcp_failure_kind"),
            Transport: MapTransport(transport),
            AdapterExpectations: RequiredSequence(row, "adapter_expectations").Children
                .Select(node => AsScalar(node, "adapter_expectations[]")).ToArray(),
            OutcomeMappings: outcomes);
    }

    private static OutcomeMapping MapOutcome(YamlMappingNode mapping) => new(
        CanonicalErrorCategory: RequiredScalar(mapping, "canonical_error_category"),
        CliExitCode: RequiredInt(mapping, "cli_exit_code"),
        McpFailureKind: RequiredScalar(mapping, "mcp_failure_kind"),
        PreSdkErrorClass: RequiredScalar(mapping, "pre_sdk_error_class"));

    private static TransportParity MapTransport(YamlMappingNode transport) => new(
        AuthOutcomeClass: RequiredScalar(transport, "auth_outcome_class"),
        ErrorCodeSet: RequiredSequence(transport, "error_code_set").Children
            .Select(node => AsScalar(node, "error_code_set[]")).ToArray(),
        IdempotencyKeyRule: RequiredScalar(transport, "idempotency_key_rule"),
        AuditMetadataKeys: RequiredSequence(transport, "audit_metadata_keys").Children
            .Select(node => AsScalar(node, "audit_metadata_keys[]")).ToArray(),
        CorrelationFieldPath: RequiredScalar(transport, "correlation_field_path"),
        TerminalStates: RequiredSequence(transport, "terminal_states").Children
            .Select(node => AsScalar(node, "terminal_states[]")).ToArray());

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            throw new InvalidOperationException($"Oracle row is missing required scalar '{key}'.");
        }

        return AsScalar(value, key);
    }

    private static int RequiredInt(YamlMappingNode mapping, string key)
        => int.Parse(RequiredScalar(mapping, key), CultureInfo.InvariantCulture);

    private static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            throw new InvalidOperationException($"Oracle row is missing required mapping '{key}'.");
        }

        return AsMapping(value, key);
    }

    private static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            throw new InvalidOperationException($"Oracle row is missing required sequence '{key}'.");
        }

        return AsSequence(value, key);
    }

    private static string AsScalar(YamlNode node, string context)
        => (node as YamlScalarNode ?? throw new InvalidOperationException($"Oracle node '{context}' is not a scalar.")).Value ?? string.Empty;

    private static YamlMappingNode AsMapping(YamlNode node, string context)
        => node as YamlMappingNode ?? throw new InvalidOperationException($"Oracle node '{context}' is not a mapping.");

    private static YamlSequenceNode AsSequence(YamlNode node, string context)
        => node as YamlSequenceNode ?? throw new InvalidOperationException($"Oracle node '{context}' is not a sequence.");

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root (Hexalith.Folders.slnx not found walking up from the test base directory).");
    }
}

/// <summary>A single per-category projection from an operation's <c>outcome_mapping</c> table.</summary>
/// <param name="CanonicalErrorCategory">The snake_case canonical error category (the oracle wire vocabulary).</param>
/// <param name="CliExitCode">The CLI exit code this category projects to.</param>
/// <param name="McpFailureKind">The MCP failure kind this category projects to (kind == category name post-SDK).</param>
/// <param name="PreSdkErrorClass">The per-category pre-SDK error class (<c>none</c> or e.g. <c>credential_missing</c>).</param>
internal sealed record OutcomeMapping(
    string CanonicalErrorCategory,
    int CliExitCode,
    string McpFailureKind,
    string PreSdkErrorClass);

/// <summary>
/// The per-operation <c>transport_parity</c> block — the source-of-truth for REST/SDK transport assertions
/// (Story 5.5). Values are raw oracle strings; the consuming test maps them to its own surface vocabulary.
/// </summary>
/// <param name="AuthOutcomeClass">The authorization-denial mapping class (e.g. <c>folder_acl_denied</c>, <c>safe_not_found</c>).</param>
/// <param name="ErrorCodeSet">The allow-list of canonical error categories this operation may emit.</param>
/// <param name="IdempotencyKeyRule">The transport rule (<c>required_for_mutating_command</c>, <c>required_with_operation_id</c>, <c>not_accepted_for_non_mutating_operation</c>).</param>
/// <param name="AuditMetadataKeys">The metadata-only audit keys the operation's audit projection may surface.</param>
/// <param name="CorrelationFieldPath">The correlation-id wire location (<c>headers.X-Correlation-Id</c> for every row in the current oracle).</param>
/// <param name="TerminalStates">The transport-terminal state classes (<c>accepted</c>, <c>projected</c>, <c>context_returned</c>, <c>audit_returned</c>, <c>projection_returned</c>).</param>
internal sealed record TransportParity(
    string AuthOutcomeClass,
    IReadOnlyList<string> ErrorCodeSet,
    string IdempotencyKeyRule,
    IReadOnlyList<string> AuditMetadataKeys,
    string CorrelationFieldPath,
    IReadOnlyList<string> TerminalStates);

/// <summary>
/// A typed view of one operation row in the parity oracle: the operation identity, its
/// <c>behavioral_parity</c> + <c>transport_parity</c> columns, its per-category <c>outcome_mapping</c>
/// projections, and the row-level <c>read_consistency_class</c>.
/// </summary>
/// <param name="OperationId">The PascalCase operation id (e.g. <c>CreateRepositoryBackedFolder</c>).</param>
/// <param name="OperationFamily">The operation family (<c>mutating_command</c>, <c>query_status</c>, <c>context_query</c>, <c>audit</c>, <c>operations_console_projection</c>).</param>
/// <param name="ReadConsistencyClass">The read-consistency class (<c>not_applicable</c>, <c>snapshot-per-task</c>, <c>read-your-writes</c>, <c>eventually-consistent</c>).</param>
/// <param name="PreSdkErrorClass">The operation-level success-row pre-SDK error class (<c>none</c>).</param>
/// <param name="IdempotencyKeySourcing"><c>caller_provided</c> (mutating) or <c>not_accepted</c> (query).</param>
/// <param name="CorrelationIdSourcing">The correlation sourcing rule (<c>caller_provided</c>).</param>
/// <param name="TaskIdSourcing"><c>caller_provided</c> (task-scoped) or <c>not_task_scoped</c>.</param>
/// <param name="CredentialSourcing">The credential sourcing rule (<c>sdk_configuration</c>).</param>
/// <param name="SuccessCliExitCode">The success-row CLI exit code (<c>0</c>).</param>
/// <param name="SuccessMcpFailureKind">The success-row MCP failure kind marker (<c>none</c>).</param>
/// <param name="Transport">The <c>transport_parity</c> block (Story 5.5).</param>
/// <param name="AdapterExpectations">The adapters expected to honor this row (every row lists <c>cli</c>, <c>mcp</c>, <c>rest</c>, and <c>sdk</c>).</param>
/// <param name="OutcomeMappings">The per-category projection rows.</param>
internal sealed record ParityRow(
    string OperationId,
    string OperationFamily,
    string ReadConsistencyClass,
    string PreSdkErrorClass,
    string IdempotencyKeySourcing,
    string CorrelationIdSourcing,
    string TaskIdSourcing,
    string CredentialSourcing,
    int SuccessCliExitCode,
    string SuccessMcpFailureKind,
    TransportParity Transport,
    IReadOnlyList<string> AdapterExpectations,
    IReadOnlyList<OutcomeMapping> OutcomeMappings)
{
    /// <summary>Gets a value indicating whether the operation is a mutating command (idempotency key caller-provided).</summary>
    public bool IsMutating => string.Equals(IdempotencyKeySourcing, "caller_provided", StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether the operation is task-scoped (task id caller-provided).</summary>
    public bool IsTaskScoped => string.Equals(TaskIdSourcing, "caller_provided", StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether the transport rule requires an <c>Idempotency-Key</c> on the wire.</summary>
    public bool RequiresIdempotencyKey
        => !string.Equals(Transport.IdempotencyKeyRule, "not_accepted_for_non_mutating_operation", StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether the operation is a non-mutating query/context/audit/projection (read-consistency applies).</summary>
    public bool IsNonMutating
        => !string.Equals(OperationFamily, "mutating_command", StringComparison.Ordinal);
}
