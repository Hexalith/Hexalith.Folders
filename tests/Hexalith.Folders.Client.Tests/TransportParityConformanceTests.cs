using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

/// <summary>
/// SDK transport-parity conformance (Story 5.5 AC #2/#3/#4/#6/#8). Reflects over the generated
/// <see cref="IClient"/> and asserts every <c>sdk</c>-expected oracle row maps to a
/// <c>{operation_id}Async</c> method whose parameter set honors the oracle's <c>transport_parity</c> rules
/// (idempotency, correlation, task) and the row-level <c>read_consistency_class</c>. Coverage and
/// vocabulary guards make any future drift fail loudly without consulting any expectation table outside
/// the oracle.
/// </summary>
/// <remarks>
/// <para>Each operation generates two overloads on <see cref="IClient"/>: one without a
/// <see cref="System.Threading.CancellationToken"/> and one with — this test consumes the
/// cancellation-token overload (the fuller surface), since both overloads carry the same header
/// parameter set.</para>
/// <para>This is the transport mirror of the Story 5.4 CLI/MCP behavioral conformance. The shared
/// oracle reader and scenarios live in <c>tests/shared/Parity/</c>, linked (not copied) into this
/// project.</para>
/// </remarks>
public sealed class TransportParityConformanceTests
{
    private static readonly IReadOnlyDictionary<string, MethodInfo> SdkAsyncMethods = BuildCancellationOverloadIndex();

    /// <summary>The SDK enum's snake_case wire vocabulary (driven by <c>[EnumMember]</c>), used to validate
    /// that every <c>error_code_set</c> member the oracle declares is a real <see cref="CanonicalErrorCategory"/>.</summary>
    private static readonly IReadOnlySet<string> SdkCanonicalErrorCategoryVocabulary = Enum.GetValues<CanonicalErrorCategory>()
        .Select(value => typeof(CanonicalErrorCategory).GetField(value.ToString())!.GetCustomAttribute<EnumMemberAttribute>()!.Value!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>The serializable per-operation tuple consumed by the per-row theories.</summary>
    /// <returns>The (operation_id, operation_family) tuples for every <c>sdk</c>-expected oracle row.</returns>
    public static TheoryData<string, string> SdkOperations() => ParityScenarios.SdkOperations();

    /// <summary>The serializable family→idempotency-rule partition tuples for the partition theory.</summary>
    /// <returns>The deduplicated (family, idempotency_key_rule) pairs observed in the oracle.</returns>
    public static TheoryData<string, string> FamilyIdempotencyPartitions() => ParityScenarios.FamilyIdempotencyPartitions();

    /// <summary>The serializable family→terminal-state partition tuples for the partition theory.</summary>
    /// <returns>The deduplicated (family, terminal_state) pairs observed in the oracle.</returns>
    public static TheoryData<string, string> FamilyTerminalStatePartitions() => ParityScenarios.FamilyTerminalStatePartitions();

    // ---------------------------------------------------------------------------------------------------
    // AC #2 / AC #8 — Operation identity, oracle-driven, both directions.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void OracleCarriesTheExpectedFortySevenDistinctRows()
    {
        ParityOracle.Rows.Count.ShouldBe(ParityScenarios.ExpectedOperationCount);
        ParityOracle.Rows.Select(row => row.OperationId).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(ParityScenarios.ExpectedOperationCount);
    }

    [Fact]
    public void EveryOracleRowListsSdkAndRestInAdapterExpectations()
    {
        // Precondition for AC #8 surface-coverage guards: the current oracle universally targets SDK and REST.
        foreach (ParityRow row in ParityOracle.Rows)
        {
            row.AdapterExpectations.ShouldContain("sdk", $"row {row.OperationId} is missing 'sdk' in adapter_expectations.");
            row.AdapterExpectations.ShouldContain("rest", $"row {row.OperationId} is missing 'rest' in adapter_expectations.");
        }
    }

    [Fact]
    public void EverySdkExpectedRowExposesAnAsyncMethodOnIClient()
    {
        string[] missing = ParityOracle.Rows
            .Where(row => row.AdapterExpectations.Contains("sdk", StringComparer.Ordinal))
            .Where(row => !SdkAsyncMethods.ContainsKey(row.OperationId + "Async"))
            .Select(row => row.OperationId)
            .ToArray();

        missing.ShouldBeEmpty($"SDK is missing async methods for oracle rows: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryAsyncMethodOnIClientMapsToASdkExpectedOracleRow()
    {
        HashSet<string> sdkExpected = ParityOracle.Rows
            .Where(row => row.AdapterExpectations.Contains("sdk", StringComparer.Ordinal))
            .Select(row => row.OperationId + "Async")
            .ToHashSet(StringComparer.Ordinal);

        string[] orphans = SdkAsyncMethods.Keys
            .Where(name => !sdkExpected.Contains(name))
            .ToArray();

        orphans.ShouldBeEmpty($"IClient exposes async methods with no matching oracle row: {string.Join(", ", orphans)}");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #3 — Idempotency-key transport rule, oracle-driven.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SdkOperations))]
    public void SdkMethodIdempotencyKeyPresenceMatchesOracleRule(string operationId, string operationFamily)
    {
        ParityRow row = ParityScenarios.Row(operationId);
        MethodInfo method = SdkAsyncMethods[operationId + "Async"];
        bool methodHasIdempotencyKey = method.GetParameters()
            .Any(p => string.Equals(p.Name, "idempotency_Key", StringComparison.Ordinal));

        if (row.RequiresIdempotencyKey)
        {
            methodHasIdempotencyKey.ShouldBeTrue(
                $"{operationId}: idempotency_key_rule '{row.Transport.IdempotencyKeyRule}' requires Idempotency-Key but SDK method declares no idempotency_Key parameter.");
            operationFamily.ShouldBe(
                "mutating_command",
                $"{operationId}: idempotency_key_rule is mutating but operation_family is '{operationFamily}'.");
        }
        else
        {
            methodHasIdempotencyKey.ShouldBeFalse(
                $"{operationId}: idempotency_key_rule '{row.Transport.IdempotencyKeyRule}' forbids Idempotency-Key but SDK method declares idempotency_Key.");
            operationFamily.ShouldNotBe(
                "mutating_command",
                $"{operationId}: idempotency_key_rule is non-mutating but operation_family is 'mutating_command'.");
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #4 — Correlation/task transport, oracle-driven (correlation_field_path is headers.X-Correlation-Id
    // for every current oracle row; task header presence is driven by task_id_sourcing).
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SdkOperations))]
    public void SdkMethodAlwaysDeclaresCorrelationParameter(string operationId, string operationFamily)
    {
        _ = operationFamily;
        ParityRow row = ParityScenarios.Row(operationId);
        row.Transport.CorrelationFieldPath.ShouldBe("headers.X-Correlation-Id");

        MethodInfo method = SdkAsyncMethods[operationId + "Async"];
        method.GetParameters().Select(p => p.Name).ShouldContain(
            "x_Correlation_Id",
            $"{operationId}: SDK method is missing x_Correlation_Id parameter despite correlation_field_path '{row.Transport.CorrelationFieldPath}'.");
    }

    [Theory]
    [MemberData(nameof(SdkOperations))]
    public void SdkMethodTaskIdPresenceMatchesOracleSourcing(string operationId, string operationFamily)
    {
        _ = operationFamily;
        ParityRow row = ParityScenarios.Row(operationId);
        MethodInfo method = SdkAsyncMethods[operationId + "Async"];

        // The oracle's task_id_sourcing column abstracts wire location: most task-scoped operations carry
        // the task id as an X-Hexalith-Task-Id header, but GetTaskStatus sources the task id from the URL
        // path ({taskId}). Both are caller-provided. The test accepts either evidence.
        ParameterInfo[] parameters = method.GetParameters();
        bool hasTaskHeader = parameters.Any(p => string.Equals(p.Name, "x_Hexalith_Task_Id", StringComparison.Ordinal));
        bool hasTaskPath = parameters.Any(p => string.Equals(p.Name, "taskId", StringComparison.Ordinal));
        bool methodIsTaskScoped = hasTaskHeader || hasTaskPath;

        methodIsTaskScoped.ShouldBe(
            row.IsTaskScoped,
            $"{operationId}: task_id_sourcing '{row.TaskIdSourcing}' but SDK method exposes neither x_Hexalith_Task_Id header nor a taskId path parameter (hasTaskHeader={hasTaskHeader}, hasTaskPath={hasTaskPath}).");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #6 — Read-consistency / freshness transport. Non-mutating ⟺ read_consistency_class != not_applicable
    // ⟺ SDK method declares x_Hexalith_Freshness.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SdkOperations))]
    public void SdkMethodFreshnessPresenceMatchesReadConsistencyClass(string operationId, string operationFamily)
    {
        ParityRow row = ParityScenarios.Row(operationId);
        MethodInfo method = SdkAsyncMethods[operationId + "Async"];
        bool methodHasFreshness = method.GetParameters()
            .Any(p => string.Equals(p.Name, "x_Hexalith_Freshness", StringComparison.Ordinal));
        bool isNonMutating = !string.Equals(operationFamily, "mutating_command", StringComparison.Ordinal);
        bool oracleDeclaresReadConsistency = !string.Equals(row.ReadConsistencyClass, "not_applicable", StringComparison.Ordinal);

        oracleDeclaresReadConsistency.ShouldBe(
            isNonMutating,
            $"{operationId}: operation_family '{operationFamily}' and read_consistency_class '{row.ReadConsistencyClass}' disagree on mutating status.");
        methodHasFreshness.ShouldBe(
            isNonMutating,
            $"{operationId}: read_consistency_class '{row.ReadConsistencyClass}' but SDK method x_Hexalith_Freshness presence is {methodHasFreshness}.");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #8 — Vocabulary guards (auth_outcome_class, idempotency_key_rule, terminal_states, error_code_set).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryAuthOutcomeClassValueIsInTheSchemaVocabulary()
    {
        foreach (ParityRow row in ParityOracle.Rows)
        {
            ParityScenarios.AuthOutcomeClassVocabulary.ShouldContain(
                row.Transport.AuthOutcomeClass,
                $"row {row.OperationId} auth_outcome_class '{row.Transport.AuthOutcomeClass}' is outside the schema vocabulary.");
        }
    }

    [Fact]
    public void EveryIdempotencyKeyRuleValueIsInTheSchemaVocabulary()
    {
        foreach (ParityRow row in ParityOracle.Rows)
        {
            ParityScenarios.IdempotencyKeyRuleVocabulary.ShouldContain(
                row.Transport.IdempotencyKeyRule,
                $"row {row.OperationId} idempotency_key_rule '{row.Transport.IdempotencyKeyRule}' is outside the schema vocabulary.");
        }
    }

    [Fact]
    public void EveryTerminalStateValueIsInTheTransportVocabulary()
    {
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (string state in row.Transport.TerminalStates)
            {
                ParityScenarios.TerminalStateVocabulary.ShouldContain(
                    state,
                    $"row {row.OperationId} terminal_states value '{state}' is outside the transport-terminal vocabulary.");
            }
        }
    }

    [Fact]
    public void EveryErrorCodeSetMemberIsARealCanonicalErrorCategory()
    {
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (string code in row.Transport.ErrorCodeSet)
            {
                SdkCanonicalErrorCategoryVocabulary.ShouldContain(
                    code,
                    $"row {row.OperationId} error_code_set value '{code}' is not a known CanonicalErrorCategory (per the SDK enum [EnumMember] values).");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #1 / AC #8 — Family-level invariants (single terminal-state class per family; family↔rule partition).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void FamilyToTerminalStateMappingIsSingleValued()
        => ParityScenarios.FamilyTerminalStateConsistencyHolds().ShouldBeTrue(
            "An operation_family carries two different terminal_states classes in the oracle.");

    [Theory]
    [MemberData(nameof(FamilyTerminalStatePartitions))]
    public void FamilyAndTerminalStateMatchTheExpectedTransportMapping(string family, string terminalState)
    {
        ParityScenarios.OperationFamilyVocabulary.ShouldContain(family);
        ParityScenarios.TerminalStateVocabulary.ShouldContain(terminalState);
        ParityScenarios.FamilyToTerminalState[family].ShouldBe(
            terminalState,
            $"family '{family}' should map to '{ParityScenarios.FamilyToTerminalState[family]}' but oracle carries '{terminalState}'.");
    }

    [Theory]
    [MemberData(nameof(FamilyIdempotencyPartitions))]
    public void FamilyAndIdempotencyRulePartitionHoldsMutatingVersusNonMutating(string family, string rule)
    {
        ParityScenarios.OperationFamilyVocabulary.ShouldContain(family);
        ParityScenarios.IdempotencyKeyRuleVocabulary.ShouldContain(rule);

        bool familyIsMutating = string.Equals(family, "mutating_command", StringComparison.Ordinal);
        bool ruleIsMutating = !string.Equals(rule, "not_accepted_for_non_mutating_operation", StringComparison.Ordinal);
        ruleIsMutating.ShouldBe(
            familyIsMutating,
            $"family '{family}' paired with idempotency_key_rule '{rule}' — partition broken (mutating ⟺ required_*).");
    }

    private static IReadOnlyDictionary<string, MethodInfo> BuildCancellationOverloadIndex()
    {
        // Each operation has exactly one no-token overload and one with-token overload. The with-token
        // overload is the fuller surface and carries the same header-parameter set, so we index by it.
        return typeof(IClient).GetMethods()
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(System.Threading.CancellationToken)))
            .ToDictionary(m => m.Name, StringComparer.Ordinal);
    }
}
