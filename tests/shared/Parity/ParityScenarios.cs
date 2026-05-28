using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// xUnit <see cref="TheoryData{T}"/> providers derived from the parity oracle, shared (linked) by the CLI,
/// MCP, SDK, REST, and end-to-end conformance tests so every surface iterates the <b>same</b> oracle rows
/// from one loader. Each provider yields only serializable primitives; the adapter-specific assertion is
/// supplied by the consuming test. Story 5.4 introduced the behavioral providers (CLI/MCP). Story 5.5 adds
/// the transport providers (REST/SDK).
/// </summary>
internal static class ParityScenarios
{
    /// <summary>The number of canonical operation rows the oracle is expected to carry.</summary>
    public const int ExpectedOperationCount = 47;

    /// <summary>The vocabulary of <c>auth_outcome_class</c> values permitted by the oracle schema.</summary>
    public static readonly IReadOnlySet<string> AuthOutcomeClassVocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "tenant_authorized",
        "tenant_access_denied",
        "folder_acl_denied",
        "audit_access_denied",
        "credential_missing",
        "safe_not_found",
    };

    /// <summary>The vocabulary of <c>idempotency_key_rule</c> values permitted by the oracle schema.</summary>
    public static readonly IReadOnlySet<string> IdempotencyKeyRuleVocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "required_for_mutating_command",
        "required_with_operation_id",
        "not_accepted_for_non_mutating_operation",
    };

    /// <summary>The vocabulary of transport-terminal state classes (one per operation family).</summary>
    public static readonly IReadOnlySet<string> TerminalStateVocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "accepted",
        "projected",
        "context_returned",
        "audit_returned",
        "projection_returned",
    };

    /// <summary>The vocabulary of <c>operation_family</c> values permitted by the oracle schema.</summary>
    public static readonly IReadOnlySet<string> OperationFamilyVocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "mutating_command",
        "query_status",
        "context_query",
        "audit",
        "operations_console_projection",
    };

    /// <summary>
    /// Maps each <c>operation_family</c> to its (single) <c>transport_parity.terminal_states</c> class. The
    /// oracle is consistent: every row in a family carries exactly one terminal state, and every family has
    /// a distinct class. See <see cref="FamilyTerminalStateConsistencyHolds"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FamilyToTerminalState = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mutating_command"] = "accepted",
        ["query_status"] = "projected",
        ["context_query"] = "context_returned",
        ["audit"] = "audit_returned",
        ["operations_console_projection"] = "projection_returned",
    };

    /// <summary>
    /// Every <c>outcome_mapping</c> entry flattened across all rows as
    /// <c>(operation_id, canonical_error_category, cli_exit_code)</c> for the CLI exit-code conformance theory.
    /// </summary>
    /// <returns>The flattened CLI outcome tuples.</returns>
    public static TheoryData<string, string, int> CliOutcomeTuples()
    {
        TheoryData<string, string, int> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                data.Add(row.OperationId, mapping.CanonicalErrorCategory, mapping.CliExitCode);
            }
        }

        return data;
    }

    /// <summary>
    /// Every <c>outcome_mapping</c> entry flattened across all rows as
    /// <c>(operation_id, canonical_error_category, mcp_failure_kind)</c> for the MCP failure-kind conformance theory.
    /// </summary>
    /// <returns>The flattened MCP outcome tuples.</returns>
    public static TheoryData<string, string, string> McpOutcomeTuples()
    {
        TheoryData<string, string, string> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                data.Add(row.OperationId, mapping.CanonicalErrorCategory, mapping.McpFailureKind);
            }
        }

        return data;
    }

    /// <summary>The per-operation rows, keyed by operation id (look the row up via <see cref="Row"/>).</summary>
    /// <returns>The operation ids.</returns>
    public static TheoryData<string> OperationIds()
    {
        TheoryData<string> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            data.Add(row.OperationId);
        }

        return data;
    }

    /// <summary>
    /// One row per <c>sdk</c>-expected oracle operation as <c>(operation_id, operation_family)</c> — the SDK
    /// surface coverage iterates this set (Story 5.5).
    /// </summary>
    /// <returns>The (id, family) tuples for SDK-expected rows.</returns>
    public static TheoryData<string, string> SdkOperations()
    {
        TheoryData<string, string> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            if (row.AdapterExpectations.Contains("sdk", StringComparer.Ordinal))
            {
                data.Add(row.OperationId, row.OperationFamily);
            }
        }

        return data;
    }

    /// <summary>
    /// One row per <c>rest</c>-expected oracle operation as <c>(operation_id, operation_family)</c> — the REST
    /// surface coverage iterates this set (Story 5.5).
    /// </summary>
    /// <returns>The (id, family) tuples for REST-expected rows.</returns>
    public static TheoryData<string, string> RestOperations()
    {
        TheoryData<string, string> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            if (row.AdapterExpectations.Contains("rest", StringComparer.Ordinal))
            {
                data.Add(row.OperationId, row.OperationFamily);
            }
        }

        return data;
    }

    /// <summary>
    /// Deduplicated <c>(operation_family, idempotency_key_rule)</c> partition tuples — the consistency theory
    /// asserts the partition holds (mutating ⟺ required_*; non-mutating ⟺ not_accepted_*).
    /// </summary>
    /// <returns>The deduplicated (family, rule) pairs observed in the oracle.</returns>
    public static TheoryData<string, string> FamilyIdempotencyPartitions()
    {
        TheoryData<string, string> data = [];
        foreach (var pair in ParityOracle.Rows
            .Select(row => (row.OperationFamily, row.Transport.IdempotencyKeyRule))
            .Distinct())
        {
            data.Add(pair.OperationFamily, pair.IdempotencyKeyRule);
        }

        return data;
    }

    /// <summary>
    /// Deduplicated <c>(operation_family, terminal_state_class)</c> tuples — the family→terminal-state
    /// consistency theory asserts every family carries exactly one transport-terminal class.
    /// </summary>
    /// <returns>The deduplicated (family, terminal-state) pairs observed in the oracle.</returns>
    public static TheoryData<string, string> FamilyTerminalStatePartitions()
    {
        TheoryData<string, string> data = [];
        foreach (var pair in ParityOracle.Rows
            .SelectMany(row => row.Transport.TerminalStates.Select(state => (row.OperationFamily, TerminalState: state)))
            .Distinct())
        {
            data.Add(pair.OperationFamily, pair.TerminalState);
        }

        return data;
    }

    /// <summary>Looks up the oracle row for an operation id.</summary>
    /// <param name="operationId">The PascalCase operation id.</param>
    /// <returns>The matching row.</returns>
    /// <exception cref="InvalidOperationException">No row matches the operation id.</exception>
    public static ParityRow Row(string operationId)
        => ParityOracle.Rows.SingleOrDefault(row => string.Equals(row.OperationId, operationId, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"Oracle has no row for operation '{operationId}'.");

    /// <summary>
    /// Returns <c>true</c> when every <c>operation_family</c> in the oracle carries exactly one
    /// <c>terminal_states</c> class across all of its rows — i.e. no family branches into two transport
    /// terminal classes (the family-level invariant called out in AC #1/#8).
    /// </summary>
    /// <returns><c>true</c> when the family→terminal-state mapping is single-valued.</returns>
    public static bool FamilyTerminalStateConsistencyHolds()
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (string state in row.Transport.TerminalStates)
            {
                if (map.TryGetValue(row.OperationFamily, out string? existing) && !string.Equals(existing, state, StringComparison.Ordinal))
                {
                    return false;
                }

                map[row.OperationFamily] = state;
            }
        }

        return true;
    }
}
