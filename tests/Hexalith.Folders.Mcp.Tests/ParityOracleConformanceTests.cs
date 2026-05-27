using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Errors;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;
using Hexalith.Folders.Parity.Testing;

using ModelContextProtocol.Server;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Oracle-driven MCP conformance (Story 5.4). Every expectation is read from the committed parity oracle
/// (<c>tests/fixtures/parity-contract.yaml</c>) via the shared <see cref="ParityOracle"/> reader — the oracle
/// row values are the source of truth and <see cref="FailureKindProjection"/> / the tool pipeline's pre-SDK
/// guards are the things under test. The hand/enum-derived <see cref="FailureKindProjectionTests"/> remains as
/// the independent restatement (AC #8): it cross-checks the projection from a source other than the oracle, so
/// projection drift and oracle drift are caught from opposite sides.
/// </summary>
public sealed class ParityOracleConformanceTests
{
    // ---------------------------------------------------------------------------------------------------
    // AC #3 — post-SDK failure-kind conformance, oracle-driven (kind == canonical category name verbatim).
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ParityScenarios.McpOutcomeTuples), MemberType = typeof(ParityScenarios))]
    public void ProjectsEveryOracleOutcomeCategoryToItsFailureKind(string operationId, string category, string mcpFailureKind)
    {
        FailureKindProjection.Project(ParseCategory(category))
            .ShouldBe(mcpFailureKind, $"oracle row '{operationId}' maps category '{category}' to mcp_failure_kind '{mcpFailureKind}'");
        mcpFailureKind.ShouldBe(category); // one-to-one post-SDK invariant: kind == canonical category name.
    }

    [Fact]
    public void SuccessMarkerIsNoneAndNeverAProjectedFailureKind()
    {
        // behavioral_parity success row carries mcp_failure_kind 'none' for every operation; 'none' is the
        // success marker, handled explicitly and never produced as a post-SDK failure kind.
        ParityOracle.Rows.ShouldAllBe(row => row.SuccessMcpFailureKind == "none");
        FailureKindProjection.Project(CanonicalErrorCategory.Success).ShouldBe("success");
        ParityOracle.CategoryMcpFailureKinds().Values.ShouldNotContain("none");
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

    [Fact]
    public void EveryOracleFailureKindIsARealEnumMemberOrTheSuccessMarker()
    {
        HashSet<string> enumValues = Enum.GetValues<CanonicalErrorCategory>().Select(EnumMemberValue).ToHashSet(StringComparer.Ordinal);
        foreach (ParityRow row in ParityOracle.Rows)
        {
            row.SuccessMcpFailureKind.ShouldBe("none"); // the success marker
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                enumValues.ShouldContain(mapping.McpFailureKind, $"oracle kind '{mapping.McpFailureKind}' must be a CanonicalErrorCategory EnumMember value");
            }
        }
    }

    [Fact]
    public void EveryOracleCategoryProjectsToItsVerbatimNameWithNoCollapse()
    {
        // CategoryMcpFailureKinds() throws if a category carries two different kinds across rows. Then prove
        // each category projects to exactly its own canonical name (never collapsed into the catch-all).
        foreach (KeyValuePair<string, string> entry in ParityOracle.CategoryMcpFailureKinds())
        {
            entry.Value.ShouldBe(entry.Key); // oracle invariant: kind == category name
            FailureKindProjection.Project(ParseCategory(entry.Key)).ShouldBe(entry.Value);
        }
    }

    [Fact]
    public void RangeUnsatisfiableIsAbsentFromTheOracleAndProjectsToInternalError()
    {
        // The documented drift exception: SDK enum member 43 is deliberately not in the oracle → internal_error.
        ParityOracle.DistinctCategories().ShouldNotContain("range_unsatisfiable");
        FailureKindProjection.InternalError.ShouldBe("internal_error");
        FailureKindProjection.Project(CanonicalErrorCategory.Range_unsatisfiable).ShouldBe(FailureKindProjection.InternalError);
    }

    [Fact]
    public void EveryEnumMemberAbsentFromTheOracleIsExplicitlyAccountedFor()
    {
        // "Unsupported categories fail tests": a CanonicalErrorCategory the oracle does not carry as an
        // outcome must be an explicitly-handled pre-SDK/success category or the documented drift exception —
        // never a silent catch-all fall-through. A new enum member without an oracle row (and unaccounted for
        // here) fails this guard.
        IReadOnlySet<string> oracleCategories = ParityOracle.DistinctCategories();
        HashSet<CanonicalErrorCategory> accountedForWithoutOracleOutcomeRow =
        [
            CanonicalErrorCategory.Success,                     // success marker → "success"
            CanonicalErrorCategory.Client_configuration_error,  // pre-SDK usage category (kind "client_configuration_error")
            CanonicalErrorCategory.Credential_missing,          // credential family (oracle carries it only as a pre_sdk_error_class)
            CanonicalErrorCategory.Range_unsatisfiable,         // documented drift exception → internal_error
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
    public void DedupedCategoryFailureKindMapCoversEveryDistinctCategory()
        => ParityOracle.CategoryMcpFailureKinds().Count.ShouldBe(ParityOracle.DistinctCategories().Count);

    // ---------------------------------------------------------------------------------------------------
    // AC #4 — pre-SDK sourcing conformance, oracle-driven. MCP tool name == kebab-case(operation_id), so the
    // per-operation binding is deterministic via reflection. Every assertion proves no HTTP call is made.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryToolDeclaresIdempotencyKeyIffItsOracleRowIsMutating()
    {
        Dictionary<string, ParityRow> rowByToolName = ParityOracle.Rows.ToDictionary(row => KebabCase(row.OperationId), row => row, StringComparer.Ordinal);

        IEnumerable<MethodInfo> toolMethods = typeof(ToolPipeline).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        int seen = 0;
        foreach (MethodInfo method in toolMethods)
        {
            string name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;
            rowByToolName.TryGetValue(name, out ParityRow? row).ShouldBeTrue($"tool '{name}' has no matching oracle row (kebab-case of operation_id)");
            ParameterInfo? idempotencyKeyParameter = method.GetParameters().SingleOrDefault(parameter => parameter.Name == "idempotencyKey");
            (idempotencyKeyParameter is not null).ShouldBe(
                row!.IsMutating,
                $"tool '{name}' idempotencyKey presence must match oracle idempotency_key_sourcing ('{row.IdempotencyKeySourcing}')");

            // AC #4: mutating tools declare a *required* idempotencyKey input. A parameter with no default value
            // is emitted as `required` in the MCP tool schema, so the caller must supply the key and it can never
            // be MCP-generated. A default would silently make the key optional — the contract violation this guards.
            if (row.IsMutating)
            {
                idempotencyKeyParameter!.HasDefaultValue.ShouldBeFalse(
                    $"tool '{name}' must declare idempotencyKey as a required input (no default value), per oracle idempotency_key_sourcing ('{row.IdempotencyKeySourcing}')");
            }

            seen++;
        }

        seen.ShouldBe(ParityScenarios.ExpectedOperationCount);
    }

    [Fact]
    public async Task RepresentativeMutatingToolMissingTaskIdIsUsageErrorWithNoCall()
    {
        // CreateRepositoryBackedFolder ⇒ create-repository-backed-folder; oracle marks it task-scoped.
        ParityScenarios.Row("CreateRepositoryBackedFolder").IsTaskScoped.ShouldBeTrue();
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.CreateRepositoryBackedFolder(
            pipeline, idempotencyKey: "idem-1", taskId: " ", correlationId: "corr-1", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe(FailureKindProjection.UsageError);
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task RepresentativeMutatingToolMissingIdempotencyKeyIsUsageErrorWithNoCall()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").IdempotencyKeySourcing.ShouldBe("caller_provided");
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client);

        string result = await FolderTools.CreateRepositoryBackedFolder(
            pipeline, idempotencyKey: "", taskId: "task-1", correlationId: "corr-2", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe(FailureKindProjection.UsageError);
        client.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task RepresentativeMutatingToolMissingCredentialIsCredentialMissingWithNoCall()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CredentialSourcing.ShouldBe("sdk_configuration");
        IClient client = Substitute.For<IClient>();
        ToolPipeline pipeline = TestSupport.Pipeline(client, token: null);

        string result = await FolderTools.CreateRepositoryBackedFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "corr-3", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.Kind(result).ShouldBe(FailureKindProjection.CredentialMissing);
        client.ReceivedCalls().ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #5 — correlation-sourcing conformance, oracle-driven (correlation_id_sourcing == caller_provided).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExplicitCorrelationIsEchoedUnchangedInResultAndOnWire()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CorrelationIdSourcing.ShouldBe("caller_provided");
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.CreateRepositoryBackedFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: "explicit-correlation-01", requestJson: "{}", TestContext.Current.CancellationToken);

        TestSupport.CorrelationId(result).ShouldBe("explicit-correlation-01");
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].CorrelationId.ShouldBe("explicit-correlation-01");
    }

    [Fact]
    public async Task OmittedCorrelationIsAFreshUlidEchoedAndOnWire()
    {
        ParityScenarios.Row("CreateRepositoryBackedFolder").CorrelationIdSourcing.ShouldBe("caller_provided");
        TestSupport.CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline pipeline = TestSupport.Pipeline(TestSupport.RealClient(handler));

        string result = await FolderTools.CreateRepositoryBackedFolder(
            pipeline, idempotencyKey: "idem-1", taskId: "task-1", correlationId: null, requestJson: "{}", TestContext.Current.CancellationToken);

        string? correlation = TestSupport.CorrelationId(result);
        correlation.ShouldNotBeNullOrWhiteSpace();
        correlation!.Length.ShouldBe(26); // fresh ULID shape
        handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].CorrelationId.ShouldBe(correlation);
    }

    private static CanonicalErrorCategory ParseCategory(string oracleValue)
        => Enum.GetValues<CanonicalErrorCategory>().Single(category => EnumMemberValue(category) == oracleValue);

    private static string EnumMemberValue(CanonicalErrorCategory value)
        => typeof(CanonicalErrorCategory).GetField(value.ToString())!.GetCustomAttribute<EnumMemberAttribute>()!.Value!;

    private static string KebabCase(string pascal)
    {
        StringBuilder builder = new(pascal.Length + 8);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
