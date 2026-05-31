using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

// Disambiguate the generated SDK Client type from the enclosing Hexalith.Folders.Client namespace.
using GeneratedSdkClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.IntegrationTests.EndToEnd;

/// <summary>
/// Golden-lifecycle dual-surface conformance (Story 5.5 AC #4/#5/#6/#7/#10). Drives the in-process server
/// host through both <b>REST</b> (raw <see cref="HttpClient"/> against <c>/api/v1/...</c>) and the
/// generated <b>SDK</b> (<see cref="IClient"/>) against the <i>same</i> host, asserting that each
/// golden-lifecycle step reaches its oracle transport-terminal state class and echoes the explicit
/// <c>X-Correlation-Id</c> unchanged on both surfaces. A representative negative step proves the canonical
/// RFC 9457 <c>application/problem+json</c> shape (with <c>category</c> ∈ the row's <c>error_code_set</c>)
/// is identical across REST and SDK.
/// </summary>
/// <remarks>
/// <para>The shared golden-lifecycle step list lives in <c>tests/shared/Parity/GoldenLifecycle.cs</c>,
/// linked (not copied) into this project. The shared oracle reader and scenarios live alongside it.</para>
/// <para><b>Hermeticity (AC #10).</b> The host binds to <c>http://127.0.0.1:0</c> (loopback, port 0), uses
/// in-memory repository, lifecycle read model, tenant projection store, and permissions read model, with
/// an in-process gateway stub that round-trips the command through <c>/process</c> against the same host.
/// <i>No</i> Dapr/Keycloak/Redis sidecars, <i>no</i> provider credentials, <i>no</i> network, <i>no</i>
/// nested submodule init.</para>
/// <para><b>Audit step substitution (drift-aware).</b> The canonical AC #7 audit-inspection step pins to an
/// audit-family operation_id, but the REST server does not yet implement an audit-family <c>/api/v1</c>
/// endpoint (<c>ListAuditTrail</c> et al. are in <c>Server.Tests.TransportParityConformanceTests</c>'s
/// known REST surface gap). Per the drift-aware reconciliation, both surfaces use
/// <c>GetFolderLifecycleStatus</c> as the in-process inspection step so the dual-surface run is
/// transport-equivalent against the same host. The golden-lifecycle step list documents this directly.</para>
/// <para><b>SDK exception body access.</b> The NSwag-generated client's <see cref="HexalithFoldersApiException"/>
/// surfaces a deserialized <c>ProblemDetails</c> via the typed
/// <see cref="HexalithFoldersApiException{TResult}.Result"/> property; the raw <c>Response</c> string is empty
/// by default (the stream-based reader does not retain raw text). The dual-surface negative assertion compares
/// the SDK's typed <c>ProblemDetails</c> against the REST raw body's parsed JSON.</para>
/// </remarks>
public sealed class GoldenLifecycleParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GoldenLifecycleStepListPinsToOracleRowsForEverySurface()
    {
        GoldenLifecycle.Steps.ShouldNotBeEmpty();
        foreach (GoldenLifecycleStep step in GoldenLifecycle.Steps)
        {
            // Every step's SDK operation_id and REST operation_id resolve to an oracle row.
            _ = ParityScenarios.Row(step.SdkOperationId);
            _ = ParityScenarios.Row(step.RestOperationId);
        }

        // The canonical flow per AC #7 (provider readiness → repository binding → prepare → lock →
        // file change → commit → context query → status → audit inspection).
        string[] stepNames = GoldenLifecycle.Steps.Select(s => s.StepName).ToArray();
        stepNames.ShouldContain("provider_readiness");
        stepNames.ShouldContain("repository_binding");
        stepNames.ShouldContain("prepare_workspace");
        stepNames.ShouldContain("lock_workspace");
        stepNames.ShouldContain("commit_workspace");
        stepNames.ShouldContain("context_query");
        stepNames.ShouldContain("workspace_status");
        stepNames.ShouldContain("audit_inspection");
    }

    [Fact]
    public async Task CrossSurfaceMutatingStepReachesAcceptedTransportTerminalAndEchoesCorrelation()
    {
        // ArchiveFolder is a mutating_command (terminal-state class 'accepted' → 202). The same
        // logical step is driven through REST and SDK against the same in-process host; both surfaces
        // must reach the 'accepted' transport-terminal class and echo the explicit X-Correlation-Id.
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.OperationFamily.ShouldBe("mutating_command");
        archive.Transport.TerminalStates.ShouldContain("accepted");

        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            // REST run.
            const string restCorrelation = "correlation-rest-archive";
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-key-rest", restCorrelation, "task-rest");
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST mutating step must reach 'accepted' transport-terminal class.");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(restCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run against the same host. The folder is now archived after the REST step; the SDK
            // ArchiveFolder call exercises the transport path (correlation, idempotency, terminal class)
            // against the post-REST state. The aggregate's archive-from-archive transition is idempotent,
            // so the SDK call surfaces the same 'accepted' transport-terminal class.
            const string sdkCorrelation = "correlation-sdk-archive";
            AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: "archive-key-sdk",
                x_Correlation_Id: sdkCorrelation,
                x_Hexalith_Task_Id: "task-sdk",
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                    ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            sdkResult.ShouldNotBeNull("SDK mutating step must reach 'accepted' transport-terminal class.");
            sdkResult.CorrelationId.ShouldBe(sdkCorrelation, "SDK response body must carry the explicit correlation echoed by the server.");
            sdkResult.Status.ShouldBe(AcceptedCommandStatus.Accepted, "SDK reaches the 'accepted' transport-terminal class.");

            // Cross-surface equivalence: both reached the same transport-terminal class (202/accepted) and
            // both echoed correlation. The in-process gateway round-trips through /process on both calls,
            // so both surfaces hit the same aggregate.
            host.Gateway.ProcessCalls.ShouldBe(2, "both surfaces invoked the in-process /process round-trip exactly once.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CrossSurfaceLifecycleInspectionStepReachesProjectedTransportTerminalAndEchoesCorrelation()
    {
        // GetFolderLifecycleStatus is a query_status row (terminal-state class 'projected' → 200). Both
        // surfaces drive the same in-process lifecycle read model and reach the 'projected' class with
        // X-Correlation-Id echoed unchanged. This is the in-process surrogate for the AC #7 audit
        // inspection step (the audit-family operations have no /api/v1 route yet; see Dev Notes).
        ParityRow lifecycle = ParityScenarios.Row("GetFolderLifecycleStatus");
        lifecycle.OperationFamily.ShouldBe("query_status");
        lifecycle.Transport.TerminalStates.ShouldContain("projected");
        lifecycle.Transport.IdempotencyKeyRule.ShouldBe("not_accepted_for_non_mutating_operation");

        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", "folder-a", correlationId: "corr-shared");

            // REST run — the lifecycle endpoint requires the request correlation to match the snapshot's
            // EvidenceScope correlation (compatible-evidence-snapshot invariant — see
            // FolderLifecycleStatusEndpointTests.LifecycleStatusRouteShouldIgnorePrincipalQueryStringValue).
            const string sharedCorrelation = "corr-shared";
            using HttpRequestMessage restRequest = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
            restRequest.Headers.Add("X-Correlation-Id", sharedCorrelation);
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "REST query_status step must reach 'projected' transport-terminal class (200).");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(sharedCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run — non-mutating SDK methods declare no idempotency_Key (AC #3).
            FolderLifecycleStatus sdkResult = await host.SdkClient.GetFolderLifecycleStatusAsync(
                folderId: "folder-a",
                x_Correlation_Id: sharedCorrelation,
                x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            sdkResult.ShouldNotBeNull("SDK query_status step must reach 'projected' transport-terminal class.");
            sdkResult.FolderId.ShouldBe("folder-a", "SDK response carries the inspected folder identity.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CrossSurfaceNegativeStepEmitsCanonicalRfc9457ProblemWithCategoryInErrorCodeSet()
    {
        // AC #5 — provoke the same negative on both surfaces (authentication failure, by withholding
        // the tenant context) and assert that the canonical RFC 9457 problem shape and the emitted
        // category ∈ error_code_set are identical across REST and SDK. ArchiveFolder error_code_set
        // includes 'authentication_failure'. The SDK access path is the typed
        // HexalithFoldersApiException<ProblemDetails>.Result (the NSwag stream-based reader leaves the
        // raw Response string empty).
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.Transport.ErrorCodeSet.ShouldContain("authentication_failure");

        TestHost host = await TestHost.StartAsync(tenantId: null, principalId: null).ConfigureAwait(true);
        try
        {
            // REST run: raw HttpClient receives a 401 RFC 9457 problem body.
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-rest-neg", "correlation-rest-neg", "task-rest-neg");
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            string restJson = await restResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument restDoc = JsonDocument.Parse(restJson);
            JsonElement restRoot = restDoc.RootElement;

            string restCategory = restRoot.GetProperty("category").GetString()!;
            archive.Transport.ErrorCodeSet.ShouldContain(restCategory, $"REST category '{restCategory}' is outside ArchiveFolder error_code_set.");
            AssertCanonicalProblemShape(restRoot, expectCorrelation: "correlation-rest-neg");

            // SDK run: HexalithFoldersApiException<ProblemDetails> carries the deserialized problem body
            // via the typed Result property (the stream-based reader leaves the raw Response empty).
            HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
                await host.SdkClient.ArchiveFolderAsync(
                    folderId: "folder-a",
                    idempotency_Key: "archive-sdk-neg",
                    x_Correlation_Id: "correlation-sdk-neg",
                    x_Hexalith_Task_Id: "task-sdk-neg",
                    body: new ArchiveFolderRequest
                    {
                        RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                        ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                    },
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ConfigureAwait(true);

            sdkException.StatusCode.ShouldBe((int)HttpStatusCode.Unauthorized);
            sdkException.ShouldBeAssignableTo<HexalithFoldersApiException<ProblemDetails>>(
                "SDK must surface the RFC 9457 problem as the typed HexalithFoldersApiException<ProblemDetails>.");

            ProblemDetails sdkProblem = ((HexalithFoldersApiException<ProblemDetails>)sdkException).Result;
            sdkProblem.ShouldNotBeNull();
            sdkProblem.CorrelationId.ShouldBe("correlation-sdk-neg");
            sdkProblem.Details.ShouldNotBeNull();
            sdkProblem.Details["visibility"].ShouldBe("metadata_only");

            // Cross-surface category equivalence: REST and SDK emit the same canonical category for the
            // same provoked failure. Resolve the SDK's typed Category to its snake_case wire value via the
            // generated enum's [EnumMember] attribute (same pattern as Client.Tests.TransportParityConformanceTests),
            // so no Newtonsoft.Json dependency is needed for the cross-surface comparison.
            string sdkCategoryWire = ResolveCanonicalCategoryWireValue(sdkProblem.Category);
            sdkCategoryWire.ShouldBe(restCategory, "REST and SDK must emit the same canonical category for the same provoked failure.");
            archive.Transport.ErrorCodeSet.ShouldContain(sdkCategoryWire, $"SDK category '{sdkCategoryWire}' is outside ArchiveFolder error_code_set.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// One row per golden-lifecycle step, expanded to expose both the SDK <c>operation_id</c> and (when it
    /// differs) the REST-substituted <c>operation_id</c>. Lets the per-step oracle-contract theory iterate
    /// every surface variant of every step.
    /// </summary>
    /// <returns>The (step-label, operation_id) tuples for the golden-lifecycle theory.</returns>
    public static TheoryData<string, string> GoldenLifecycleStepRows()
    {
        TheoryData<string, string> data = [];
        foreach (GoldenLifecycleStep step in GoldenLifecycle.Steps)
        {
            data.Add(step.StepName + ":sdk", step.SdkOperationId);
            if (!string.Equals(step.SdkOperationId, step.RestOperationId, StringComparison.Ordinal))
            {
                data.Add(step.StepName + ":rest", step.RestOperationId);
            }
        }

        return data;
    }

    /// <summary>
    /// AC #7 per-step contract — every golden-lifecycle step's oracle row carries the transport-contract
    /// values its operation family declares (terminal-state class, idempotency rule, universal correlation
    /// field path) and is both <c>sdk</c>- and <c>rest</c>-expected. A drifted oracle row joining the
    /// canonical flow without matching its family's transport contract fails this theory loudly.
    /// </summary>
    /// <param name="stepLabel">The step label (snake_case + ":sdk"/":rest" surface tag).</param>
    /// <param name="operationId">The operation id pinned to that step on that surface.</param>
    [Theory]
    [MemberData(nameof(GoldenLifecycleStepRows))]
    public void GoldenLifecycleStepCarriesOracleTransportContractForFamily(string stepLabel, string operationId)
    {
        _ = stepLabel;
        ParityRow row = ParityScenarios.Row(operationId);

        // AC #6 — terminal-state class follows family→state partition.
        ParityScenarios.FamilyToTerminalState.ShouldContainKey(row.OperationFamily);
        string expectedTerminal = ParityScenarios.FamilyToTerminalState[row.OperationFamily];
        row.Transport.TerminalStates.ShouldContain(
            expectedTerminal,
            $"{operationId}: family '{row.OperationFamily}' implies terminal-state class '{expectedTerminal}' but oracle row carries '[{string.Join(", ", row.Transport.TerminalStates)}]'.");

        // AC #3 — idempotency-rule follows family partition (mutating ⟺ required_*; non-mutating ⟺ not_accepted_*).
        bool familyIsMutating = string.Equals(row.OperationFamily, "mutating_command", StringComparison.Ordinal);
        bool ruleIsMutating = !string.Equals(row.Transport.IdempotencyKeyRule, "not_accepted_for_non_mutating_operation", StringComparison.Ordinal);
        ruleIsMutating.ShouldBe(
            familyIsMutating,
            $"{operationId}: family '{row.OperationFamily}' but idempotency_key_rule '{row.Transport.IdempotencyKeyRule}' — golden-lifecycle step breaks the family↔rule partition.");

        // AC #4 — correlation field path is universal headers.X-Correlation-Id across the canonical flow.
        row.Transport.CorrelationFieldPath.ShouldBe("headers.X-Correlation-Id");

        // AC #2 / AC #8 — the step's operation_id is sdk- and rest-expected (precondition for the dual-surface run).
        row.AdapterExpectations.ShouldContain("sdk", $"{operationId}: missing 'sdk' in adapter_expectations.");
        row.AdapterExpectations.ShouldContain("rest", $"{operationId}: missing 'rest' in adapter_expectations.");
    }

    /// <summary>
    /// AC #4 — task-scoped operations must echo the explicit <c>X-Hexalith-Task-Id</c> on the response
    /// across both surfaces. The REST surface echoes the header; the SDK surface surfaces it on the
    /// <c>AcceptedCommand.TaskId</c> response body. Both must reflect the caller-supplied value.
    /// </summary>
    [Fact]
    public async Task CrossSurfaceMutatingStepEchoesExplicitTaskIdOnResponse()
    {
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.OperationFamily.ShouldBe("mutating_command", "task-id echo is asserted on a task-scoped mutating row.");

        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

            const string restTaskId = "task-echo-rest-0001";
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-key-rest-echo", "correlation-rest-echo", restTaskId);
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            restResponse.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe(restTaskId, "REST must echo the explicit X-Hexalith-Task-Id unchanged on the response header.");

            const string sdkTaskId = "task-echo-sdk-0001";
            AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: "archive-key-sdk-echo",
                x_Correlation_Id: "correlation-sdk-echo",
                x_Hexalith_Task_Id: sdkTaskId,
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                    ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            sdkResult.ShouldNotBeNull();
            sdkResult.TaskId.ShouldBe(sdkTaskId, "SDK must surface the explicit X-Hexalith-Task-Id on the AcceptedCommand.TaskId response field.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// AC #5 (metadata-only audit boundary) — drives the substituted audit-inspection step
    /// (<c>GetFolderLifecycleStatus</c>; the audit-family endpoints have no <c>/api/v1</c> route yet, see
    /// the golden-lifecycle <c>RestInspectionOperationId</c> substitution) and asserts that:
    /// <list type="number">
    ///   <item><description>every <c>audit_metadata_key</c> the in-process projection actually populates
    ///     is present in the response body (snake_case oracle key → camelCase response field;
    ///     <see cref="ProjectionSurfacedAuditKeys"/> is the subset the substitute op surfaces);</description></item>
    ///   <item><description>the response carries <b>none</b> of the forbidden content patterns
    ///     (secrets/tokens/credentials/raw file contents/diffs/provider payloads/absolute paths).</description></item>
    /// </list>
    /// The remaining oracle audit keys (<c>operation_id</c>, <c>result</c>) are worker-produced audit-record
    /// keys, not query-response keys; the boundary is documented in the Story 5.5 Dev Notes.
    /// </summary>
    [Fact]
    public async Task AuditInspectionStepSurfacesProjectionAuditMetadataKeysAndCarriesNoForbiddenContent()
    {
        GoldenLifecycleStep auditStep = GoldenLifecycle.Steps.Single(step => string.Equals(step.StepName, "audit_inspection", StringComparison.Ordinal));
        ParityRow auditRow = ParityScenarios.Row(auditStep.RestOperationId);
        auditRow.Transport.AuditMetadataKeys.ShouldNotBeEmpty("oracle row must declare audit_metadata_keys for the audit-inspection step.");

        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", "folder-a", correlationId: "corr-audit");

            using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", "corr-audit");
            using HttpResponseMessage response = await host.HttpClient.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, "audit-inspection step must reach 'projected' transport-terminal class.");
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            // (1) Every projection-surfaced audit metadata key must be in the oracle row AND in the response.
            foreach (string oracleKey in ProjectionSurfacedAuditKeys)
            {
                auditRow.Transport.AuditMetadataKeys.ShouldContain(
                    oracleKey,
                    $"projection-surfaced key '{oracleKey}' must be declared in the oracle row's audit_metadata_keys.");
                string camelCaseKey = ToCamelCase(oracleKey);
                root.TryGetProperty(camelCaseKey, out _).ShouldBeTrue(
                    $"audit-inspection response must surface oracle audit_metadata_key '{oracleKey}' as response field '{camelCaseKey}'.");
            }

            // (2) Metadata-only invariant: no forbidden content patterns in the response body. The
            // patterns are deliberately broad (token-like substrings, secret-like substrings, raw file
            // contents, diffs, provider payloads, absolute paths) so an accidental leak fails loudly.
            foreach (string forbidden in ForbiddenContentPatterns)
            {
                body.ShouldNotContain(
                    forbidden,
                    Case.Insensitive,
                    $"metadata-only invariant: audit-inspection response must not contain '{forbidden}'.");
            }
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The subset of <c>audit_metadata_keys</c> the in-process substituted projection surfaces in
    /// its response body (the remaining oracle keys are worker-produced audit-record keys, not query-response
    /// keys — the boundary is documented in the Story 5.5 Dev Notes).</summary>
    private static readonly IReadOnlyList<string> ProjectionSurfacedAuditKeys = new[]
    {
        "folder_id",
        "lifecycle_state",
    };

    /// <summary>Forbidden content patterns the metadata-only invariant rules out of every audit-trail-shaped
    /// response. The list is intentionally broad — any one of these substrings appearing in the body fails.</summary>
    private static readonly IReadOnlyList<string> ForbiddenContentPatterns = new[]
    {
        "BEGIN PRIVATE KEY",
        "BEGIN RSA PRIVATE KEY",
        "ghp_",            // GitHub personal access token prefix
        "github_pat_",     // GitHub fine-grained PAT prefix
        "Bearer ey",       // JWT-shaped bearer token (JWT header decodes to {"alg":...} starting with ey)
        "password=",
        "secret=",
        "api_key=",
        "client_secret=",
        "/mnt/",           // absolute WSL path
        "C:\\",            // absolute Windows path
        "/home/",          // absolute Unix path
        "diff --git",      // raw diff content
        "@@ -",            // unified diff hunk marker
    };

    private static string ToCamelCase(string snakeCase)
    {
        string[] parts = snakeCase.Split('_');
        if (parts.Length == 1)
        {
            return parts[0];
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static void AssertCanonicalProblemShape(JsonElement root, string expectCorrelation)
    {
        root.TryGetProperty("category", out _).ShouldBeTrue();
        root.TryGetProperty("code", out _).ShouldBeTrue();
        root.TryGetProperty("message", out _).ShouldBeTrue();
        root.GetProperty("correlationId").GetString().ShouldBe(expectCorrelation);
        root.TryGetProperty("retryable", out _).ShouldBeTrue();
        root.TryGetProperty("clientAction", out _).ShouldBeTrue();
        root.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
    }

    private static string ResolveCanonicalCategoryWireValue(CanonicalErrorCategory category)
    {
        FieldInfo field = typeof(CanonicalErrorCategory).GetField(category.ToString())
            ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} has no reflectable field.");
        EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()
            ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} is missing [EnumMember]; cannot resolve wire value.");
        return attribute.Value ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} [EnumMember(Value=...)] is null.");
    }

    private static HttpRequestMessage CreateArchiveRequest(string folderId, string idempotencyKey, string correlationId, string taskId)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/{folderId}/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                archiveReasonCode = "caller_requested",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);
        return request;
    }

    private static void SeedTenant(InMemoryFolderTenantAccessProjectionStore store, string tenantId, string principalId)
        => store.SaveAsync(
            new FolderTenantAccessProjection
            {
                TenantId = tenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    [principalId] = new(principalId, "Owner"),
                },
                Watermark = 1,
                LastEventTimestamp = Now.AddMinutes(-1),
                ProjectionWatermark = $"{tenantId}:1",
            },
            TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private static void SeedPermissions(
        InMemoryEffectivePermissionsReadModel readModel,
        string tenantId,
        string organizationId,
        string folderId,
        string principalId)
        => readModel.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            folderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "archive_folder", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "read_metadata", Sequence: 2, EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static void SeedFolder(InMemoryFolderRepository repository, string tenantId, string organizationId, string folderId)
        => repository.Seed(
            FolderStreamName.Create(tenantId, folderId),
            [
                new FolderCreated(
                    tenantId,
                    organizationId,
                    folderId,
                    "Folder",
                    null,
                    null,
                    [],
                    FolderLifecycleState.Active,
                    FolderRepositoryBindingState.Unbound,
                    "user-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key",
                    "seed-fingerprint",
                    Now.AddMinutes(-2)),
            ]);

    private static void SeedLifecycleStatus(
        InMemoryFolderLifecycleStatusReadModel readModel,
        string tenantId,
        string folderId,
        string correlationId)
        => readModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            ManagedTenantId: tenantId,
            FolderId: folderId,
            LifecycleState: FolderLifecycleProjectionState.Active,
            BindingStatus: FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            Freshness: new FolderLifecycleFreshness(
                ReadConsistency: "eventually_consistent",
                ObservedAt: Now,
                ProjectionWatermark: "lifecycle_watermark_v1",
                Stale: false,
                ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: tenantId,
                PrincipalId: "user-a",
                ActionToken: "read_metadata",
                TaskId: null,
                CorrelationId: correlationId,
                AuthorizationWatermark: "permission-watermark-a"),
            DiagnosticSentinels: []));

    /// <summary>Bundles the in-process host together with the two clients that drive it (raw HttpClient + SDK IClient).</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(
            WebApplication app,
            HttpClient httpClient,
            IClient sdkClient,
            InProcessEventStoreGatewayClient gateway,
            InMemoryFolderRepository repository,
            InMemoryFolderTenantAccessProjectionStore tenantStore,
            InMemoryEffectivePermissionsReadModel permissions,
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel)
        {
            App = app;
            HttpClient = httpClient;
            SdkClient = sdkClient;
            Gateway = gateway;
            Repository = repository;
            TenantStore = tenantStore;
            Permissions = permissions;
            LifecycleReadModel = lifecycleReadModel;
        }

        public WebApplication App { get; }

        public HttpClient HttpClient { get; }

        public IClient SdkClient { get; }

        public InProcessEventStoreGatewayClient Gateway { get; }

        public InMemoryFolderRepository Repository { get; }

        public InMemoryFolderTenantAccessProjectionStore TenantStore { get; }

        public InMemoryEffectivePermissionsReadModel Permissions { get; }

        public InMemoryFolderLifecycleStatusReadModel LifecycleReadModel { get; }

        public static Task<TestHost> StartAsync() => StartAsync(tenantId: "tenant-a", principalId: "user-a");

        public static async Task<TestHost> StartAsync(string? tenantId, string? principalId)
        {
            MutableTenantAndClaimContext context = new(tenantId, principalId);
            InMemoryFolderTenantAccessProjectionStore tenantStore = new();
            InMemoryEffectivePermissionsReadModel permissions = new();
            FixedUtcClock clock = new(Now);
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(clock);
            TimeProvider timeProvider = new FixedTimeProvider(Now);
            InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
            Func<HttpClient>? eventStoreClientFactory = null;
            InProcessEventStoreGatewayClient gateway = new(() => eventStoreClientFactory!(), context);

            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddFoldersServerTestDefaults();
            builder.Services.AddFoldersServer();
            builder.Services.RemoveAll<IEventStoreGatewayClient>();
            builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
            builder.Services.RemoveAll<ITenantContextAccessor>();
            builder.Services.AddSingleton<ITenantContextAccessor>(context);
            builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
            builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(context);
            builder.Services.RemoveAll<IFolderRepository>();
            builder.Services.AddSingleton<IFolderRepository>(repository);
            builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
            builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycleReadModel);
            builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
            builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenantStore);
            builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
            builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
            builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
            builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
            builder.Services.RemoveAll<IUtcClock>();
            builder.Services.AddSingleton<IUtcClock>(clock);
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton(timeProvider);

            WebApplication app = builder.Build();
            app.MapFoldersServerEndpoints();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            eventStoreClientFactory = app.GetTestClient;

            HttpClient httpClient = app.GetTestClient();
            // The SDK client points at the same in-process host. Per the generated Client constructor,
            // the HttpClient's BaseAddress is what's used; no other DI plumbing is required for the test.
            IClient sdkClient = new GeneratedSdkClient(app.GetTestClient());

            return new TestHost(app, httpClient, sdkClient, gateway, repository, tenantStore, permissions, lifecycleReadModel);
        }

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class MutableTenantAndClaimContext(string? tenantId, string? principalId)
        : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;

        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(
                AuthoritativeTenantId ?? string.Empty,
                PrincipalId ?? string.Empty,
                [actionToken]);
    }

    private sealed class InProcessEventStoreGatewayClient(
        Func<HttpClient> clientFactory,
        MutableTenantAndClaimContext context) : IEventStoreGatewayClient
    {
        public int ProcessCalls { get; private set; }

        public async Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            using HttpClient client = clientFactory();
            CommandEnvelope envelope = new(
                request.MessageId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.CommandType,
                JsonSerializer.SerializeToUtf8Bytes(request.Payload),
                request.CorrelationId ?? request.MessageId,
                CausationId: null,
                context.PrincipalId ?? "actor-present",
                request.Extensions);

            HttpResponseMessage response = await client
                .PostAsJsonAsync("/process", new DomainServiceRequest(envelope, CurrentState: null), cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId);
        }

        public Task<EventStoreQueryResult> SubmitQueryAsync(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
            SubmitQueryRequest request,
            string? ifNoneMatch = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamReadPage> ReadStreamAsync(
            StreamReadRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
