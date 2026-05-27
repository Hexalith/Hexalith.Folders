using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using Hexalith.Folders.Cli;
using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Oracle-driven CLI conformance (Story 5.4). Every expectation here is read from the committed parity oracle
/// (<c>tests/fixtures/parity-contract.yaml</c>) via the shared <see cref="ParityOracle"/> reader — the oracle
/// row values are the source of truth and <see cref="ErrorProjection"/> / the CLI pre-SDK guards are the things
/// under test. This closes the loop the hand-typed <see cref="ErrorProjectionTests"/> opens: that suite
/// restates the map independently of the oracle (defense-in-depth, AC #8); this suite proves the projection
/// agrees with the oracle file itself, so projection drift and oracle drift are caught from opposite sides.
/// </summary>
public sealed class ParityOracleConformanceTests
{
    private const string BaseAddress = "https://folders.test/";
    private const string Token = "synthetic-jwt";

    private static readonly int[] CanonicalExitCodes = [0, 1, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75];

    // ---------------------------------------------------------------------------------------------------
    // AC #2 — post-SDK exit-code conformance, oracle-driven.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ParityScenarios.CliOutcomeTuples), MemberType = typeof(ParityScenarios))]
    public void ProjectsEveryOracleOutcomeCategoryToItsCliExitCode(string operationId, string category, int cliExitCode)
        => ErrorProjection.Project(ParseCategory(category))
            .ShouldBe(cliExitCode, $"oracle row '{operationId}' maps category '{category}' to cli_exit_code {cliExitCode}");

    [Fact]
    public void SuccessMarkerProjectsToZero()
    {
        // The behavioral_parity success row carries cli_exit_code 0 for every operation (category 'success').
        ParityOracle.Rows.ShouldAllBe(row => row.SuccessCliExitCode == 0);
        FoldersExitCodes.Success.ShouldBe(0);
        ErrorProjection.Project(CanonicalErrorCategory.Success).ShouldBe(FoldersExitCodes.Success);
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #6 — completeness & drift guards ("missing rows or unsupported categories fail tests").
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void OracleContainsExactlyTheExpectedDistinctOperationRows()
    {
        IReadOnlyList<ParityRow> rows = ParityOracle.Rows;
        rows.Count.ShouldBe(ParityScenarios.ExpectedOperationCount);
        rows.Select(row => row.OperationId).Distinct(StringComparer.Ordinal).Count().ShouldBe(ParityScenarios.ExpectedOperationCount);
    }

    [Theory]
    [MemberData(nameof(ParityScenarios.OperationIds), MemberType = typeof(ParityScenarios))]
    public void EveryOperationRowDeclaresCliAndMcpAdapters(string operationId)
    {
        ParityRow row = ParityScenarios.Row(operationId);
        row.AdapterExpectations.ShouldContain("cli");
        row.AdapterExpectations.ShouldContain("mcp");
    }

    [Fact]
    public void EveryOracleCategoryProjectsToItsDedupedExitCodeWithinTheCanonicalSet()
    {
        // CategoryCliExitCodes() throws if a category carries two different exit codes across rows (the
        // cross-row consistency invariant). Then prove the projection agrees with the deduped value.
        foreach (KeyValuePair<string, int> entry in ParityOracle.CategoryCliExitCodes())
        {
            int projected = ErrorProjection.Project(ParseCategory(entry.Key));
            projected.ShouldBe(entry.Value, $"category '{entry.Key}'");
            CanonicalExitCodes.ShouldContain(projected);
        }
    }

    [Fact]
    public void EveryOracleExitCodeIsInTheCanonicalVocabulary()
    {
        foreach (ParityRow row in ParityOracle.Rows)
        {
            CanonicalExitCodes.ShouldContain(row.SuccessCliExitCode);
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                CanonicalExitCodes.ShouldContain(mapping.CliExitCode);
            }
        }
    }

    [Fact]
    public void RangeUnsatisfiableIsAbsentFromTheOracleAndProjectsToInternalError()
    {
        // The documented drift exception: SDK enum member 43 is deliberately not in the oracle → CLI exit 1.
        ParityOracle.DistinctCategories().ShouldNotContain("range_unsatisfiable");
        FoldersExitCodes.InternalError.ShouldBe(1);
        ErrorProjection.Project(CanonicalErrorCategory.Range_unsatisfiable).ShouldBe(FoldersExitCodes.InternalError);
    }

    [Fact]
    public void EveryEnumMemberAbsentFromTheOracleIsExplicitlyAccountedFor()
    {
        // "Unsupported categories fail tests": a CanonicalErrorCategory the oracle does not carry as an
        // outcome must be an explicitly-handled pre-SDK/success category or the documented drift exception —
        // never a silent catch-all fall-through. Adding a new enum member without an oracle row (and without
        // accounting for it here) fails this guard.
        IReadOnlySet<string> oracleCategories = ParityOracle.DistinctCategories();
        HashSet<CanonicalErrorCategory> accountedForWithoutOracleOutcomeRow =
        [
            CanonicalErrorCategory.Success,                     // behavioral_parity success marker → 0
            CanonicalErrorCategory.Client_configuration_error,  // pre-SDK usage category → 64
            CanonicalErrorCategory.Credential_missing,          // credential family → 65 (oracle carries it only as a pre_sdk_error_class)
            CanonicalErrorCategory.Range_unsatisfiable,         // documented drift exception → 1
        ];

        foreach (CanonicalErrorCategory member in Enum.GetValues<CanonicalErrorCategory>())
        {
            if (oracleCategories.Contains(EnumMemberValue(member)))
            {
                continue;
            }

            accountedForWithoutOracleOutcomeRow.ShouldContain(
                member,
                $"enum member '{member}' is absent from the oracle outcome_mapping and is not a documented exception — the oracle dropped a category or a new category needs handling.");
        }

        oracleCategories.Count.ShouldBe(43); // 43 post-SDK categories carry an outcome_mapping row.
    }

    [Fact]
    public void DedupedCategoryExitCodeMapCoversEveryDistinctCategory()
        => ParityOracle.CategoryCliExitCodes().Count.ShouldBe(ParityOracle.DistinctCategories().Count);

    // ---------------------------------------------------------------------------------------------------
    // AC #4 — pre-SDK sourcing conformance, oracle-driven. CLI command names are not kebab-case of the
    // operation_id, so we bind a representative command to its oracle row explicitly; the EXPECTATION (which
    // exit code, whether the key is accepted) is still read from the oracle column. Every assertion proves no
    // HTTP call is made (ClientFactoryInvoked == false).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MutatingCommandMissingIdempotencyKeyIsUsageErrorAndMakesNoCall()
    {
        // CreateRepositoryBackedFolder ⇒ `folder create-repo-backed`.
        ParityScenarios.Row("CreateRepositoryBackedFolder").IdempotencyKeySourcing.ShouldBe("caller_provided");
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress, "--token", Token, "--task-id", "task_1", "--request", "{}");

        exit.ShouldBe(FoldersExitCodes.UsageError);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task TaskScopedMutatingCommandMissingTaskIdIsUsageErrorAndMakesNoCall()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").IsTaskScoped.ShouldBeTrue();
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress, "--token", Token, "--idempotency-key", "key_1", "--request", "{}");

        exit.ShouldBe(FoldersExitCodes.UsageError);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingCredentialIsCredentialExitAndMakesNoCall()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CredentialSourcing.ShouldBe("sdk_configuration");
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress, "--task-id", "task_1", "--idempotency-key", "key_1", "--request", "{}");

        exit.ShouldBe(FoldersExitCodes.CredentialMissing);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task QueryCommandRejectsIdempotencyKeyAndMakesNoCall()
    {
        // GetFolderLifecycleStatus ⇒ `folder status`; oracle marks queries idempotency_key_sourcing 'not_accepted'.
        ParityScenarios.Row("GetFolderLifecycleStatus").IdempotencyKeySourcing.ShouldBe("not_accepted");
        CliTestHarness harness = new();

        int exit = await harness.RunAsync(
            "folder", "status", "--folder-id", "folder_1",
            "--base-address", BaseAddress, "--token", Token, "--idempotency-key", "k");

        exit.ShouldBe(FoldersExitCodes.UsageError);
        harness.ClientFactoryInvoked.ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #5 — correlation-sourcing conformance, oracle-driven (correlation_id_sourcing == caller_provided).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExplicitCorrelationIdIsEchoedUnchangedToWireAndStderr()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CorrelationIdSourcing.ShouldBe("caller_provided");
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        int exit = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress, "--token", Token, "--task-id", "task_1",
            "--idempotency-key", "key_1", "--correlation-id", "corr_FIXED_0123456789ABCDEF", "--request", "{}");

        exit.ShouldBe(FoldersExitCodes.Success);
        handler.Header("X-Correlation-Id").ShouldBe("corr_FIXED_0123456789ABCDEF");
        harness.Console.StdErr.ShouldContain("correlation-id: corr_FIXED_0123456789ABCDEF");
    }

    [Fact]
    public async Task OmittedCorrelationIdIsAFreshUlidOnWireAndStderr()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CorrelationIdSourcing.ShouldBe("caller_provided");
        CliTestHarness harness = new();
        CapturingHttpHandler handler = harness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());

        _ = await harness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", BaseAddress, "--token", Token, "--task-id", "task_1",
            "--idempotency-key", "key_1", "--request", "{}");

        string? wireCorrelation = handler.Header("X-Correlation-Id");
        wireCorrelation.ShouldNotBeNullOrWhiteSpace();
        wireCorrelation!.Length.ShouldBe(26); // fresh ULID shape
        harness.Console.StdErr.ShouldContain($"correlation-id: {wireCorrelation}");
    }

    private static CanonicalErrorCategory ParseCategory(string oracleValue)
        => Enum.GetValues<CanonicalErrorCategory>().Single(category => EnumMemberValue(category) == oracleValue);

    private static string EnumMemberValue(CanonicalErrorCategory value)
        => typeof(CanonicalErrorCategory).GetField(value.ToString())!.GetCustomAttribute<EnumMemberAttribute>()!.Value!;
}
