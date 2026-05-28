using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Errors;
using Hexalith.Folders.Mcp.Tests;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;
using Hexalith.Folders.Parity.Testing;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.IntegrationTests.AdapterParity;

/// <summary>
/// Story 5.6 — the cross-adapter behavioral-parity proof. Co-references the CLI <see cref="ErrorProjection"/>
/// and the MCP <see cref="FailureKindProjection"/> in one assembly so that, for every oracle row × category
/// in <c>tests/fixtures/parity-contract.yaml</c>, the CLI exit-code projection and the MCP failure-kind
/// projection are asserted to agree with the oracle <b>in the same iteration of the loop</b>. Per-adapter
/// conformance is already proven in isolation by Story 5.4's <c>ParityOracleConformanceTests</c> living in
/// each adapter's test project; this suite closes the transitive claim (CLI ⇔ oracle ⇔ MCP) into a single
/// assertion site so a row that drifts on either surface fails one test.
/// </summary>
public sealed class CrossAdapterBehavioralParityTests
{
    private static readonly int[] CanonicalCliExitCodes = [0, 1, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75];

    /// <summary>
    /// The MCP failure-kind vocabulary the cross-adapter assertions accept: every canonical post-SDK category
    /// name (from <see cref="ParityOracle.CategoryMcpFailureKinds"/>) plus the pre-SDK / success markers layered
    /// by the tool pipeline on top of the post-SDK projection. <c>"internal_error"</c> appears in both sets and
    /// is the catch-all fall-through on each surface.
    /// </summary>
    private static readonly HashSet<string> McpFailureKindVocabulary = BuildMcpVocabulary();

    [Theory]
    [MemberData(nameof(CrossAdapterOutcomeTuples))]
    public void OracleRowProjectsToTheSameExitCodeOnCliAndTheSameKindOnMcp(
        string operationId,
        string canonicalErrorCategory,
        int oracleCliExitCode,
        string oracleMcpFailureKind)
    {
        CanonicalErrorCategory category = ParseCategory(canonicalErrorCategory);

        int cliProjected = ErrorProjection.Project(category);
        string mcpProjected = FailureKindProjection.Project(category);

        cliProjected.ShouldBe(
            oracleCliExitCode,
            $"oracle row '{operationId}' category '{canonicalErrorCategory}' → CLI exit code {oracleCliExitCode}");
        mcpProjected.ShouldBe(
            oracleMcpFailureKind,
            $"oracle row '{operationId}' category '{canonicalErrorCategory}' → MCP failure kind '{oracleMcpFailureKind}'");

        // Post-SDK invariant the oracle declares: kind == canonical category name verbatim.
        oracleMcpFailureKind.ShouldBe(canonicalErrorCategory);
        mcpProjected.ShouldBe(canonicalErrorCategory);

        // Vocabulary guards (AC #10): both surfaces' observed values stay inside the canonical sets.
        CanonicalCliExitCodes.ShouldContain(cliProjected);
        McpFailureKindVocabulary.ShouldContain(mcpProjected);
    }

    [Fact]
    public void SuccessMarkerProjectsToZeroOnCliAndSuccessKindOnMcp()
    {
        ParityOracle.Rows.ShouldAllBe(row => row.SuccessCliExitCode == 0 && row.SuccessMcpFailureKind == "none");
        ErrorProjection.Project(CanonicalErrorCategory.Success).ShouldBe(0);
        FailureKindProjection.Project(CanonicalErrorCategory.Success).ShouldBe("success");
    }

    [Fact]
    public void ClientConfigurationErrorIsPreSdkUsageOnBothSurfaces()
    {
        // pre-SDK category, never carried as an outcome_mapping row; both adapters must project it deterministically.
        ParityOracle.DistinctCategories().ShouldNotContain("client_configuration_error");
        ErrorProjection.Project(CanonicalErrorCategory.Client_configuration_error).ShouldBe(64);
        FailureKindProjection.Project(CanonicalErrorCategory.Client_configuration_error).ShouldBe("client_configuration_error");
    }

    [Fact]
    public void CredentialMissingIsPreSdkOnBothSurfaces()
    {
        ParityOracle.DistinctCategories().ShouldNotContain("credential_missing");
        ErrorProjection.Project(CanonicalErrorCategory.Credential_missing).ShouldBe(65);
        FailureKindProjection.Project(CanonicalErrorCategory.Credential_missing).ShouldBe("credential_missing");
    }

    [Fact]
    public void RangeUnsatisfiableIsTheDocumentedDriftExceptionOnBothSurfaces()
    {
        // SDK enum member 43 is deliberately absent from the oracle; both adapters fall through to internal_error.
        ParityOracle.DistinctCategories().ShouldNotContain("range_unsatisfiable");
        ErrorProjection.Project(CanonicalErrorCategory.Range_unsatisfiable).ShouldBe(1);
        FailureKindProjection.Project(CanonicalErrorCategory.Range_unsatisfiable).ShouldBe(FailureKindProjection.InternalError);
    }

    [Fact]
    public void EveryCanonicalErrorCategoryIsExplicitlyAccountedForOnBothAdapters()
    {
        // Cross-adapter drift guard (AC #10): enum members carried by the oracle must hit an explicit
        // projection arm on BOTH adapters (CLI exit code != 1 unless oracle says so; MCP kind != "internal_error"
        // unless oracle says so). Members absent from the oracle must be the 4-row documented exception set.
        IReadOnlySet<string> oracleCategories = ParityOracle.DistinctCategories();
        IReadOnlyDictionary<string, int> oracleCliExitCodes = ParityOracle.CategoryCliExitCodes();
        IReadOnlyDictionary<string, string> oracleMcpFailureKinds = ParityOracle.CategoryMcpFailureKinds();
        HashSet<CanonicalErrorCategory> documentedAbsent =
        [
            CanonicalErrorCategory.Success,
            CanonicalErrorCategory.Client_configuration_error,
            CanonicalErrorCategory.Credential_missing,
            CanonicalErrorCategory.Range_unsatisfiable,
        ];

        foreach (CanonicalErrorCategory member in Enum.GetValues<CanonicalErrorCategory>())
        {
            string canonical = EnumMemberValue(member);
            if (oracleCategories.Contains(canonical))
            {
                int cliExit = ErrorProjection.Project(member);
                string mcpKind = FailureKindProjection.Project(member);

                cliExit.ShouldBe(oracleCliExitCodes[canonical], $"CLI projection drift for '{canonical}'");
                mcpKind.ShouldBe(oracleMcpFailureKinds[canonical], $"MCP projection drift for '{canonical}'");

                // Neither surface collapses an oracle-carried category to its catch-all (CLI 1 / MCP internal_error)
                // unless the oracle itself says so for that category.
                if (oracleCliExitCodes[canonical] != 1)
                {
                    cliExit.ShouldNotBe(1, $"CLI silently collapsed oracle-carried category '{canonical}' to InternalError (1)");
                }

                if (!string.Equals(oracleMcpFailureKinds[canonical], FailureKindProjection.InternalError, StringComparison.Ordinal))
                {
                    mcpKind.ShouldNotBe(FailureKindProjection.InternalError, $"MCP silently collapsed oracle-carried category '{canonical}' to internal_error");
                }
            }
            else
            {
                documentedAbsent.ShouldContain(
                    member,
                    $"enum member '{member}' is absent from the oracle outcome_mapping and is not a documented exception (cross-adapter drift guard)");
            }
        }

        oracleCategories.Count.ShouldBe(43);
        Enum.GetValues<CanonicalErrorCategory>().Length.ShouldBe(47);
    }

    // =====================================================================================================
    // Task 3 — end-to-end behavioral symmetry: pre-SDK (credential / idempotency-key / task-ID sourcing).
    // Each test drives the SAME provoked condition through BOTH adapters in one method, with one of the two
    // assertion legs guaranteeing the other surface cannot silently diverge.
    // =====================================================================================================

    private const string CliBaseAddress = "https://folders.test/";
    private const string CliToken = "synthetic-jwt";

    // CreateRepositoryBackedFolder — mutating + task-scoped, the canonical cross-adapter operation.
    // CLI argv:        folder create-repo-backed ...
    // MCP tool name:   create-repository-backed-folder
    // GetFolderLifecycleStatus — the canonical query.
    // CLI argv:        folder status ...
    // MCP tool name:   get-folder-lifecycle-status
    // ListFolderFiles — task-scoped context query.
    // CLI argv:        context list ...
    // MCP tool name:   list-folder-files

    [Fact]
    public async Task MissingCredentialIsCredentialMissingOnBothSurfacesWithNoHttpCall()
    {
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.CredentialSourcing.ShouldBe("sdk_configuration"); // gate the symmetry on the oracle column.

        // CLI: no --token, no env, no credentials file → exit 65 + stderr credential_missing, no client built.
        CliTestHarness cliHarness = new();
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        cliExit.ShouldBe(65);
        cliHarness.ClientFactoryInvoked.ShouldBeFalse();
        cliHarness.Console.StdErr.ShouldContain("credential_missing");

        // MCP: token: null on the resolver → kind credential_missing, no IClient call.
        IClient mcpClient = Substitute.For<IClient>();
        ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient, token: null);
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "corr_X",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson.Value<string>("kind").ShouldBe("credential_missing");
        mcpJson.Value<string>("code").ShouldBe("credential_missing");
        mcpJson.Value<bool>("retryable").ShouldBeFalse();
        mcpJson.Value<string>("clientAction").ShouldBe("check_credentials");
        mcpJson.Value<string>("correlationId").ShouldBe("corr_X");
        mcpClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingIdempotencyKeyOnMutatingIsUsageErrorOnBothSurfacesWithNoHttpCall()
    {
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.IdempotencyKeySourcing.ShouldBe("caller_provided");

        // CLI: no --idempotency-key on a mutating command → exit 64 + stderr client_configuration_error.
        CliTestHarness cliHarness = new();
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--request", "{}");

        cliExit.ShouldBe(64);
        cliHarness.ClientFactoryInvoked.ShouldBeFalse();
        cliHarness.Console.StdErr.ShouldContain("client_configuration_error");

        // MCP: blank idempotencyKey on a mutating tool → kind usage_error, no IClient call.
        IClient mcpClient = Substitute.For<IClient>();
        ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient);
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "",
            taskId: "task_1",
            correlationId: "corr_idem",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        TestSupport.Kind(mcpResult).ShouldBe("usage_error");
        TestSupport.CorrelationId(mcpResult).ShouldBe("corr_idem");
        mcpClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryRefusesIdempotencyKeyOnBothSurfacesWithNoHttpCall()
    {
        ParityRow row = ParityScenarios.Row("GetFolderLifecycleStatus");
        row.IdempotencyKeySourcing.ShouldBe("not_accepted");

        // CLI: --idempotency-key on a query command → exit 64 (usage error).
        CliTestHarness cliHarness = new();
        int cliExit = await cliHarness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--idempotency-key", "k");

        cliExit.ShouldBe(64);
        cliHarness.ClientFactoryInvoked.ShouldBeFalse();

        // MCP: the matching tool MUST NOT declare an idempotencyKey parameter — that is the schema-level rejection
        // (a callsite passing idempotencyKey would not type-check / would be rejected by the MCP tool schema).
        MethodInfo queryMethod = typeof(FolderTools).GetMethod(nameof(FolderTools.GetFolderLifecycleStatus), BindingFlags.Public | BindingFlags.Static)!;
        queryMethod.GetParameters().Any(parameter => parameter.Name == "idempotencyKey")
            .ShouldBeFalse("MCP query tool 'get-folder-lifecycle-status' must not declare idempotencyKey (oracle 'not_accepted')");
    }

    [Fact]
    public async Task MissingTaskIdOnTaskScopedMutationIsUsageErrorOnBothSurfacesWithNoHttpCall()
    {
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.TaskIdSourcing.ShouldBe("caller_provided");

        // CLI: no --task-id on a task-scoped mutating command → exit 64.
        CliTestHarness cliHarness = new();
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--idempotency-key", "key_1",
            "--request", "{}");

        cliExit.ShouldBe(64);
        cliHarness.ClientFactoryInvoked.ShouldBeFalse();

        // MCP: blank taskId on a task-scoped mutating tool → kind usage_error.
        IClient mcpClient = Substitute.For<IClient>();
        ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient);
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: " ",
            correlationId: "corr_task",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        TestSupport.Kind(mcpResult).ShouldBe("usage_error");
        mcpClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTaskIdOnTaskScopedQueryIsUsageErrorOnBothSurfacesWithNoHttpCall()
    {
        ParityRow row = ParityScenarios.Row("ListFolderFiles");
        row.TaskIdSourcing.ShouldBe("caller_provided");

        // CLI: no --task-id on context list (task-scoped query) → exit 64.
        CliTestHarness cliHarness = new();
        int cliExit = await cliHarness.RunAsync(
            "context", "list",
            "--folder-id", "folder_1",
            "--workspace-id", "workspace_1",
            "--base-address", CliBaseAddress,
            "--token", CliToken);

        cliExit.ShouldBe(64);
        cliHarness.ClientFactoryInvoked.ShouldBeFalse();

        // MCP: blank taskId on list-folder-files → kind usage_error.
        IClient mcpClient = Substitute.For<IClient>();
        ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient);
        string mcpResult = await ContextTools.ListFolderFiles(
            mcpPipeline,
            folderId: "folder_1",
            workspaceId: "workspace_1",
            taskId: "",
            correlationId: "corr_q",
            cancellationToken: TestContext.Current.CancellationToken);

        TestSupport.Kind(mcpResult).ShouldBe("usage_error");
        mcpClient.ReceivedCalls().ShouldBeEmpty();
    }

    // =====================================================================================================
    // Task 4 — end-to-end behavioral symmetry: correlation defaults.
    // Explicit correlation is echoed byte-for-byte through both surfaces (caller value preserved on the wire
    // X-Correlation-Id header and surfaced back to the caller). Omitted correlation is replaced by a freshly
    // generated 26-char ULID per surface — the values differ per invocation, the SHAPE is identical on both.
    // =====================================================================================================

    private const string ExplicitCorrelation = "corr_FIXED_0123456789ABCDEF";

    [Fact]
    public async Task ExplicitCorrelationIsEchoedByteForByteThroughBothAdapters()
    {
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.CorrelationIdSourcing.ShouldBe("caller_provided");
        row.Transport.CorrelationFieldPath.ShouldBe("headers.X-Correlation-Id");

        // CLI: explicit --correlation-id, run against a 202 Accepted canned response.
        CliTestHarness cliHarness = new();
        CapturingHttpHandler cliHandler = cliHarness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", ExplicitCorrelation,
            "--request", "{}");

        cliExit.ShouldBe(0);
        string? cliWireCorrelation = cliHandler.Header("X-Correlation-Id");
        cliWireCorrelation.ShouldBe(ExplicitCorrelation);
        cliHarness.Console.StdErr.ShouldContain($"correlation-id: {ExplicitCorrelation}");

        // MCP: explicit correlationId, run against a 202 Accepted canned response.
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: ExplicitCorrelation,
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        mcpHandler.Requests.ShouldHaveSingleItem();
        string? mcpWireCorrelation = mcpHandler.Requests[0].CorrelationId;
        mcpWireCorrelation.ShouldBe(ExplicitCorrelation);
        TestSupport.CorrelationId(mcpResult).ShouldBe(ExplicitCorrelation);

        // Cross-adapter: the wire-observed correlation value is identical byte-for-byte on both surfaces.
        cliWireCorrelation.ShouldBe(mcpWireCorrelation);
    }

    [Fact]
    public async Task OmittedCorrelationIsAFresh26CharUlidOnBothAdapters()
    {
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.CorrelationIdSourcing.ShouldBe("caller_provided");

        // CLI: no --correlation-id → wire header is a 26-char ULID, surfaced to stderr.
        CliTestHarness cliHarness = new();
        CapturingHttpHandler cliHandler = cliHarness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());
        _ = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--request", "{}");

        string? cliWireCorrelation = cliHandler.Header("X-Correlation-Id");
        cliWireCorrelation.ShouldNotBeNullOrWhiteSpace();
        cliWireCorrelation!.Length.ShouldBe(26);
        cliHarness.Console.StdErr.ShouldContain($"correlation-id: {cliWireCorrelation}");

        // MCP: correlationId: null → wire header + result correlationId are a fresh 26-char ULID.
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: null,
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        mcpHandler.Requests.ShouldHaveSingleItem();
        string? mcpWireCorrelation = mcpHandler.Requests[0].CorrelationId;
        mcpWireCorrelation.ShouldNotBeNullOrWhiteSpace();
        mcpWireCorrelation!.Length.ShouldBe(26);
        TestSupport.CorrelationId(mcpResult).ShouldBe(mcpWireCorrelation);

        // Cross-adapter: same SHAPE on both surfaces (both 26-char ULID). The VALUES differ per invocation —
        // each surface generates its own fresh ULID — that is the symmetric contract, not a fixed-value claim.
        cliWireCorrelation.Length.ShouldBe(mcpWireCorrelation.Length);
    }

    // =====================================================================================================
    // Task 5 — end-to-end behavioral symmetry: post-SDK error categories driven through a fake
    // HttpMessageHandler. CreateRepositoryBackedFolder declares 400/401/403/404/409/422/503 — wide enough to
    // exercise 7 distinct typed-projection categories on a single operation, plus one undeclared status (500)
    // for the bare-exception fall-through that both adapters project to internal_error.
    // =====================================================================================================

    private const string ServerCorrelation = "corr_SERVER_0123456789ABCDEF";

    public static TheoryData<string, int, int, string, bool, string> CrossAdapterPostSdkTuples()
    {
        TheoryData<string, int, int, string, bool, string> data = [];

        // (canonical_error_category, http_status, expected_cli_exit_code, expected_mcp_failure_kind, server_retryable, server_client_action)
        // Status codes pair the oracle's category with a CreateRepositoryBackedFolder-declared response so the
        // SDK reads the body as ProblemDetails (typed projection). The category in the body drives the kind.
        data.Add("authentication_failure", 401, 65, "authentication_failure", false, "check_credentials");
        data.Add("folder_acl_denied", 403, 66, "folder_acl_denied", false, "no_action");
        data.Add("idempotency_conflict", 409, 68, "idempotency_conflict", false, "revise_request");
        data.Add("validation_error", 422, 69, "validation_error", false, "revise_request");
        data.Add("workspace_locked", 409, 67, "workspace_locked", true, "retry");
        data.Add("not_found", 404, 73, "not_found", false, "no_action");
        data.Add("unknown_provider_outcome", 503, 71, "unknown_provider_outcome", false, "wait_for_reconciliation");

        return data;
    }

    [Theory]
    [MemberData(nameof(CrossAdapterPostSdkTuples))]
    public async Task PostSdkCategorySymmetryDrivenThroughBothAdaptersAgainstFakeHttpHandler(
        string category,
        int httpStatus,
        int expectedCliExitCode,
        string expectedMcpFailureKind,
        bool serverRetryable,
        string serverClientAction)
    {
        // Sanity-tie the expectation to the oracle's deduped category map.
        ParityOracle.CategoryCliExitCodes()[category].ShouldBe(expectedCliExitCode);
        ParityOracle.CategoryMcpFailureKinds()[category].ShouldBe(expectedMcpFailureKind);

        string problemJson = BuildProblemJson(category, httpStatus, ServerCorrelation, serverRetryable, serverClientAction);

        // ---- CLI ----
        CliTestHarness cliHarness = new();
        CapturingHttpHandler cliHandler = cliHarness.UseRealClient((HttpStatusCode)httpStatus, problemJson);
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "client_correlation_cli",
            "--request", "{}");
        cliExit.ShouldBe(expectedCliExitCode);
        cliHandler.Header("X-Correlation-Id").ShouldBe("client_correlation_cli"); // caller-supplied correlation is on the wire unchanged.
        cliHarness.Console.StdErr.ShouldContain(ServerCorrelation); // server-supplied correlation is surfaced to the operator.

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new((HttpStatusCode)httpStatus, problemJson, "application/problem+json");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "client_correlation_mcp",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson.Value<string>("kind").ShouldBe(expectedMcpFailureKind);
        mcpJson.Value<string>("code").ShouldBe(category); // server code echoed verbatim
        mcpJson.Value<bool>("retryable").ShouldBe(serverRetryable);
        mcpJson.Value<string>("clientAction").ShouldBe(serverClientAction);
        mcpJson.Value<string>("correlationId").ShouldBe(ServerCorrelation); // server-supplied correlation echoed
        mcpHandler.Requests.ShouldHaveSingleItem();
        mcpHandler.Requests[0].CorrelationId.ShouldBe("client_correlation_mcp"); // caller correlation on the wire.

        // ---- Cross-adapter vocabulary guards ----
        CanonicalCliExitCodes.ShouldContain(cliExit);
        McpFailureKindVocabulary.ShouldContain(mcpJson.Value<string>("kind"));
    }

    [Fact]
    public async Task UndeclaredStatusProducesInternalErrorOnBothAdaptersViaBareExceptionPath()
    {
        // Status 500 is not declared on CreateRepositoryBackedFolder → SDK throws bare HexalithFoldersApiException
        // (no Category). Both adapters project bare to CLI=1, MCP="internal_error". The oracle row for
        // internal_error confirms (CLI 1 / MCP "internal_error"), so cross-adapter symmetry holds without typed
        // body deserialization.
        ParityOracle.CategoryCliExitCodes()["internal_error"].ShouldBe(1);
        ParityOracle.CategoryMcpFailureKinds()["internal_error"].ShouldBe("internal_error");

        const string anyBody = "{\"detail\":\"server crashed\"}";

        // ---- CLI ----
        CliTestHarness cliHarness = new();
        _ = cliHarness.UseRealClient(HttpStatusCode.InternalServerError, anyBody);
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "client_correlation_cli",
            "--request", "{}");
        cliExit.ShouldBe(1);

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.InternalServerError, anyBody);
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "client_correlation_mcp",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        TestSupport.Kind(mcpResult).ShouldBe("internal_error");
        TestSupport.CorrelationId(mcpResult).ShouldBe("client_correlation_mcp"); // bare-exception path uses caller correlation.
    }

    [Fact]
    public void PreSdkAndPostSdkClassesAreMutuallyExclusiveAcrossAdapters()
    {
        // Architecture invariant: pre-SDK error classes (configuration, credential-missing) are mutually
        // exclusive with post-SDK error classes; "no operation can return both" — and no cross-adapter drift
        // can have one surface classify a scenario as pre-SDK while the other classifies it as post-SDK.
        // Concretely: every oracle outcome_mapping category projects to a post-SDK CLI exit code (NOT 64 the
        // pre-SDK usage code) and to a post-SDK MCP kind (NOT "usage_error"/"credential_missing"). The success
        // marker and pre-SDK markers are excluded from the oracle outcome_mapping by construction; the only
        // legitimate cross-set overlap is CLI=1 / MCP="internal_error" (the catch-all on both surfaces).
        foreach (KeyValuePair<string, int> entry in ParityOracle.CategoryCliExitCodes())
        {
            entry.Value.ShouldNotBe(64, $"category '{entry.Key}' must not collapse to the pre-SDK usage code 64 on CLI");
        }

        foreach (KeyValuePair<string, string> entry in ParityOracle.CategoryMcpFailureKinds())
        {
            entry.Value.ShouldNotBe(FailureKindProjection.UsageError, $"category '{entry.Key}' must not collapse to the pre-SDK 'usage_error' kind on MCP");
            entry.Value.ShouldNotBe(FailureKindProjection.CredentialMissing, $"category '{entry.Key}' must not collapse to the pre-SDK 'credential_missing' kind on MCP");
        }
    }

    private static string BuildProblemJson(string category, int httpStatus, string serverCorrelation, bool retryable, string clientAction)
    {
        string retryableJson = retryable ? "true" : "false";
        return $$"""
            {"type":"about:blank","title":"{{category}}","status":{{httpStatus}},"category":"{{category}}","code":"{{category}}","message":"Synthetic problem","correlationId":"{{serverCorrelation}}","retryable":{{retryableJson}},"clientAction":"{{clientAction}}"}
            """;
    }

    // =====================================================================================================
    // Task 6 — canonical names / state language / evidence fields / error categories are preserved
    // byte-for-byte across CLI and MCP. The SDK serializes all wire types through Newtonsoft + CamelCase +
    // [JsonProperty] + StringEnumConverter + [EnumMember]; both adapter rendering pipelines reuse exactly
    // that stack (CLI Rendering/ResultRenderer + Infrastructure/MetadataOnlyJson; MCP Tooling/ToolPipeline +
    // Infrastructure/MetadataOnlyJson), so the canonical names MUST surface identically. Any divergence is a
    // real cross-adapter drift to surface in Dev Notes — never silently papered over.
    // =====================================================================================================

    [Fact]
    public async Task CanonicalLifecycleStateValueAppearsByteForByteOnBothSurfaces()
    {
        // Build a canned 200 OK FolderLifecycleStatus carrying LifecycleState.Committed (EnumMember "committed").
        // Both adapters serialize the SDK shape through Newtonsoft StringEnumConverter → the canonical wire
        // string "committed" must round-trip into BOTH outputs verbatim.
        string lifecycleJson = """
            {"folderId":"folder_1","lifecycleState":"committed","archived":false,"repositoryBindingId":"binding_1","providerBindingRef":"provider_ref_1","freshness":{"readConsistencyClass":"snapshot_per_task","readyForReads":true}}
            """;

        // ---- CLI --output json ----
        CliTestHarness cliHarness = new();
        _ = cliHarness.UseRealClient(HttpStatusCode.OK, lifecycleJson);
        int cliExit = await cliHarness.RunAsync(
            "folder", "status",
            "--folder-id", "folder_1",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--output", "json");

        cliExit.ShouldBe(0);
        string cliStdOut = cliHarness.Console.StdOut;
        cliStdOut.ShouldContain("\"lifecycleState\": \"committed\"");
        cliStdOut.ShouldNotContain("\"Committed\"", Shouldly.Case.Sensitive); // PascalCase translation would fail AC #9.
        cliStdOut.ShouldNotContain("\"lifecycle_state\"", Shouldly.Case.Sensitive); // snake_case key would fail AC #9.

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.OK, lifecycleJson);
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.GetFolderLifecycleStatus(
            mcpPipeline,
            folderId: "folder_1",
            correlationId: "corr_1",
            cancellationToken: TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        Newtonsoft.Json.Linq.JObject mcpResultObj = (Newtonsoft.Json.Linq.JObject)mcpJson["result"]!;
        mcpResultObj.Value<string>("lifecycleState").ShouldBe("committed");

        // Cross-adapter: extract the canonical state string from both and assert byte-for-byte equality.
        string cliLifecycleState = ExtractJsonValue(cliStdOut, "lifecycleState");
        string mcpLifecycleState = mcpResultObj.Value<string>("lifecycleState")!;
        cliLifecycleState.ShouldBe(mcpLifecycleState);
        cliLifecycleState.ShouldBe("committed");
    }

    [Fact]
    public async Task CanonicalEvidenceFieldNamesAppearOnBothSurfacesWithIdenticalCasing()
    {
        // Drive a successful mutating operation (CreateRepositoryBackedFolder) with a canned 202 Accepted body
        // carrying the canonical evidence vocabulary (correlationId, taskId, status, idempotentReplay,
        // acceptedAt). The CLI --output json and the MCP success envelope must both surface these field NAMES
        // verbatim — no surface localizes (correlationId → correlation_id, taskId → task_id, etc.).
        string acceptedJson = TestData.AcceptedJson();

        // ---- CLI ----
        CliTestHarness cliHarness = new();
        _ = cliHarness.UseRealClient(HttpStatusCode.Accepted, acceptedJson);
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "corr_evidence_cli",
            "--output", "json",
            "--request", "{}");

        cliExit.ShouldBe(0);
        string cliStdOut = cliHarness.Console.StdOut;
        cliStdOut.ShouldContain("\"correlationId\":");
        cliStdOut.ShouldContain("\"taskId\":");
        cliStdOut.ShouldContain("\"acceptedAt\":");
        cliStdOut.ShouldContain("\"idempotentReplay\":");
        cliStdOut.ShouldContain("\"status\":");
        cliStdOut.ShouldNotContain("\"correlation_id\":");
        cliStdOut.ShouldNotContain("\"task_id\":");
        cliStdOut.ShouldNotContain("\"accepted_at\":");

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, acceptedJson);
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "corr_evidence_mcp",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson["correlationId"].ShouldNotBeNull(); // envelope-level correlationId.
        Newtonsoft.Json.Linq.JObject mcpInner = (Newtonsoft.Json.Linq.JObject)mcpJson["result"]!;
        mcpInner["correlationId"].ShouldNotBeNull();
        mcpInner["taskId"].ShouldNotBeNull();
        mcpInner["acceptedAt"].ShouldNotBeNull();
        mcpInner["status"].ShouldNotBeNull();
        mcpInner["idempotentReplay"].ShouldNotBeNull();
        mcpResult.ShouldNotContain("\"correlation_id\":");
        mcpResult.ShouldNotContain("\"task_id\":");
    }

    [Fact]
    public async Task CanonicalErrorCategoryStringAppearsVerbatimOnBothSurfaces()
    {
        // folder_acl_denied is a representative authorization-denial category (oracle: 66 / folder_acl_denied).
        // Drive it through both adapters via a fake HttpMessageHandler. Assert the canonical snake_case
        // category string surfaces verbatim on BOTH surfaces. No adapter may localize/translate/abbreviate
        // ("ACL denied", "AccessDenied") or hide the category vocabulary.
        const string category = "folder_acl_denied";
        string problemJson = BuildProblemJson(category, 403, ServerCorrelation, retryable: false, clientAction: "no_action");

        // ---- CLI ----
        CliTestHarness cliHarness = new();
        _ = cliHarness.UseRealClient(HttpStatusCode.Forbidden, problemJson);
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "client_correlation_cli",
            "--request", "{}");

        cliExit.ShouldBe(66);
        string cliStdErr = cliHarness.Console.StdErr;
        // The canonical snake_case category must surface verbatim in CLI stderr (via the server-supplied
        // problem.Code field which the CLI emits as `code: folder_acl_denied`).
        cliStdErr.ShouldContain(category);
        // And it must not be replaced by a localized / abbreviated form.
        cliStdErr.ShouldNotContain("AccessDenied");
        cliStdErr.ShouldNotContain("\"ACL denied\"");

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Forbidden, problemJson, "application/problem+json");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "client_correlation_mcp",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        TestSupport.Kind(mcpResult).ShouldBe(category);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson.Value<string>("code").ShouldBe(category);
        mcpResult.ShouldNotContain("AccessDenied");
        mcpResult.ShouldNotContain("ACL denied");
    }

    // =====================================================================================================
    // QA gap coverage — strengthens cross-adapter parity beyond the explicit Task 3/4/5/6 assertions.
    // These complement the existing suite by asserting transport-shape and pre-SDK envelope symmetry that
    // the per-task tests only cover transitively (both adapters wrap the same SDK ⇒ same wire shape).
    // =====================================================================================================

    [Fact]
    public async Task WireHeadersForMutatingCallEchoCallerInputsByteForByteAcrossBothAdapters()
    {
        // Cross-adapter symmetry strengthening: ExplicitCorrelationIsEchoedByteForByteThroughBothAdapters
        // already proves X-Correlation-Id byte-for-byte. The full canonical header set on a task-scoped
        // mutating call is (Idempotency-Key, X-Hexalith-Task-Id, X-Correlation-Id). Caller-supplied values
        // must surface byte-for-byte on the wire AND must be equal across the two adapter surfaces.
        const string idempotencyKey = "key_FIXED_0123456789ABCDEF";
        const string taskId = "task_FIXED_0123456789ABCDEF";
        const string correlationId = "corr_FIXED_0123456789ABCDEF";

        // ---- CLI ----
        CliTestHarness cliHarness = new();
        CapturingHttpHandler cliHandler = cliHarness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", taskId,
            "--idempotency-key", idempotencyKey,
            "--correlation-id", correlationId,
            "--request", "{}");

        cliExit.ShouldBe(0);
        string? cliIdempotencyKey = cliHandler.Header("Idempotency-Key");
        string? cliTaskId = cliHandler.Header("X-Hexalith-Task-Id");
        string? cliCorrelationId = cliHandler.Header("X-Correlation-Id");
        cliIdempotencyKey.ShouldBe(idempotencyKey);
        cliTaskId.ShouldBe(taskId);
        cliCorrelationId.ShouldBe(correlationId);

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        _ = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: idempotencyKey,
            taskId: taskId,
            correlationId: correlationId,
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        mcpHandler.Requests.ShouldHaveSingleItem();
        mcpHandler.Requests[0].IdempotencyKey.ShouldBe(idempotencyKey);
        mcpHandler.Requests[0].TaskId.ShouldBe(taskId);
        mcpHandler.Requests[0].CorrelationId.ShouldBe(correlationId);

        // ---- Cross-adapter: byte-for-byte equality on every canonical wire header ----
        cliIdempotencyKey.ShouldBe(mcpHandler.Requests[0].IdempotencyKey);
        cliTaskId.ShouldBe(mcpHandler.Requests[0].TaskId);
        cliCorrelationId.ShouldBe(mcpHandler.Requests[0].CorrelationId);
    }

    [Fact]
    public async Task HttpMethodAndPathSymmetryForSameOperationAcrossBothAdapters()
    {
        // Both adapters wrap the same SDK and so must produce the same transport shape (method + path) for
        // the same operation. The projection-equivalence theory proves error-vocabulary parity at the
        // projection layer; this fact proves URI-shape parity at the wire layer.
        CliTestHarness cliHarness = new();
        CapturingHttpHandler cliHandler = cliHarness.UseRealClient(HttpStatusCode.Accepted, TestData.AcceptedJson());
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "corr_shape",
            "--request", "{}");
        cliExit.ShouldBe(0);

        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, "{}");
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        _ = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "corr_shape",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        cliHandler.Request.ShouldNotBeNull();
        mcpHandler.Requests.ShouldHaveSingleItem();
        cliHandler.Request!.Method.Method.ShouldBe(mcpHandler.Requests[0].Method);
        cliHandler.Request!.RequestUri!.AbsolutePath.ShouldBe(mcpHandler.Requests[0].Uri!.AbsolutePath);
    }

    [Fact]
    public async Task UsageErrorMcpEnvelopeCarriesCanonicalPreSdkProblemFields()
    {
        // The credential_missing test asserts the full pre-SDK MCP envelope (kind/code/retryable/
        // clientAction/correlationId). The usage_error path is symmetric — the tool pipeline emits the
        // same 5-field envelope. Existing usage_error tests only check kind + correlationId; this gap
        // test closes the asymmetric pre-SDK coverage and proves the canonical Problem-shape parity.
        ParityRow row = ParityScenarios.Row("CreateRepositoryBackedFolder");
        row.IdempotencyKeySourcing.ShouldBe("caller_provided");

        IClient mcpClient = Substitute.For<IClient>();
        ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient);
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: string.Empty,
            taskId: "task_1",
            correlationId: "corr_usage_envelope",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson.Value<string>("kind").ShouldBe(FailureKindProjection.UsageError);
        mcpJson.Value<string>("code").ShouldNotBeNullOrWhiteSpace();
        mcpJson["retryable"].ShouldNotBeNull();
        mcpJson.Value<bool>("retryable").ShouldBeFalse();
        mcpJson.Value<string>("clientAction").ShouldNotBeNullOrWhiteSpace();
        mcpJson.Value<string>("correlationId").ShouldBe("corr_usage_envelope");
        mcpClient.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task IdempotentReplaySuccessIsSurfacedConsistentlyOnBothAdapters()
    {
        // Architecture invariant: "identical idempotency replay across CLI/MCP/SDK/REST". When the server
        // returns idempotentReplay:true on a 202 Accepted, both adapters must surface that field truthy in
        // their success output — no surface silently flattens replay to a fresh accept.
        string replayJson = TestData.AcceptedJson(idempotentReplay: true);

        // ---- CLI --output json ----
        CliTestHarness cliHarness = new();
        _ = cliHarness.UseRealClient(HttpStatusCode.Accepted, replayJson);
        int cliExit = await cliHarness.RunAsync(
            "folder", "create-repo-backed",
            "--base-address", CliBaseAddress,
            "--token", CliToken,
            "--task-id", "task_1",
            "--idempotency-key", "key_1",
            "--correlation-id", "corr_replay_cli",
            "--output", "json",
            "--request", "{}");
        cliExit.ShouldBe(0);
        string cliStdOut = cliHarness.Console.StdOut;
        cliStdOut.ShouldContain("\"idempotentReplay\": true");

        // ---- MCP ----
        TestSupport.CapturingHandler mcpHandler = new(HttpStatusCode.Accepted, replayJson);
        ToolPipeline mcpPipeline = TestSupport.Pipeline(TestSupport.RealClient(mcpHandler));
        string mcpResult = await FolderTools.CreateRepositoryBackedFolder(
            mcpPipeline,
            idempotencyKey: "key_1",
            taskId: "task_1",
            correlationId: "corr_replay_mcp",
            requestJson: "{}",
            TestContext.Current.CancellationToken);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        Newtonsoft.Json.Linq.JObject mcpInner = (Newtonsoft.Json.Linq.JObject)mcpJson["result"]!;
        mcpInner.Value<bool>("idempotentReplay").ShouldBeTrue();
    }

    private static string ExtractJsonValue(string json, string fieldName)
    {
        // Lightweight scanner: find "<fieldName>" : "<value>" — sufficient for hermetic test extraction.
        int keyIndex = json.IndexOf("\"" + fieldName + "\"", StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return string.Empty;
        }

        int colonIndex = json.IndexOf(':', keyIndex);
        int startQuote = json.IndexOf('"', colonIndex + 1);
        int endQuote = json.IndexOf('"', startQuote + 1);
        return json.Substring(startQuote + 1, endQuote - startQuote - 1);
    }

    /// <summary>
    /// Flattens every <c>outcome_mapping</c> row into a cross-adapter tuple
    /// <c>(operation_id, canonical_error_category, cli_exit_code, mcp_failure_kind)</c> so a single theory
    /// iterates every oracle entry and asserts symmetry on both surfaces in the same iteration.
    /// </summary>
    /// <returns>The flattened cross-adapter tuples.</returns>
    public static TheoryData<string, string, int, string> CrossAdapterOutcomeTuples()
    {
        TheoryData<string, string, int, string> data = [];
        foreach (ParityRow row in ParityOracle.Rows)
        {
            foreach (OutcomeMapping mapping in row.OutcomeMappings)
            {
                data.Add(row.OperationId, mapping.CanonicalErrorCategory, mapping.CliExitCode, mapping.McpFailureKind);
            }
        }

        return data;
    }

    private static CanonicalErrorCategory ParseCategory(string oracleValue)
        => Enum.GetValues<CanonicalErrorCategory>().Single(category => EnumMemberValue(category) == oracleValue);

    private static string EnumMemberValue(CanonicalErrorCategory value)
        => typeof(CanonicalErrorCategory).GetField(value.ToString())!.GetCustomAttribute<EnumMemberAttribute>()!.Value!;

    private static HashSet<string> BuildMcpVocabulary()
    {
        HashSet<string> vocabulary = new(StringComparer.Ordinal)
        {
            "none",
            FailureKindProjection.UsageError,
            FailureKindProjection.CredentialMissing,
            FailureKindProjection.InternalError,
        };
        foreach (string canonical in ParityOracle.CategoryMcpFailureKinds().Values)
        {
            vocabulary.Add(canonical);
        }

        return vocabulary;
    }
}
