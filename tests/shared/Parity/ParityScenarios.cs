using System;
using System.Linq;

using Xunit;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// xUnit <see cref="TheoryData{T}"/> providers derived from the parity oracle, shared (linked) by the CLI and
/// MCP conformance tests so both surfaces iterate the <b>same</b> oracle rows from one loader. Each provider
/// yields only serializable primitives; the adapter-specific assertion is supplied by the consuming test. The
/// CLI and MCP outcome projections are two views of the identical <see cref="ParityOracle.Rows"/> set (kept as
/// separate tuples only so each <c>[Theory]</c> consumes every parameter — no unused-parameter analyzer noise).
/// </summary>
internal static class ParityScenarios
{
    /// <summary>The number of canonical operation rows the oracle is expected to carry.</summary>
    public const int ExpectedOperationCount = 47;

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

    /// <summary>Looks up the oracle row for an operation id.</summary>
    /// <param name="operationId">The PascalCase operation id.</param>
    /// <returns>The matching row.</returns>
    /// <exception cref="InvalidOperationException">No row matches the operation id.</exception>
    public static ParityRow Row(string operationId)
        => ParityOracle.Rows.SingleOrDefault(row => string.Equals(row.OperationId, operationId, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"Oracle has no row for operation '{operationId}'.");
}
