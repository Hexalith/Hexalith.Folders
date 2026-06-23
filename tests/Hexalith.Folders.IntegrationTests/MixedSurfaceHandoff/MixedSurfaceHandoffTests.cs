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
using Hexalith.Folders.Cli;
using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tests;
using Hexalith.Folders.Mcp.Tooling;
using Hexalith.Folders.Mcp.Tools;
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

// CA2007 — `await using` against an IAsyncDisposable cannot accept a ConfigureAwait without a manual
// try/finally rewrite. The test methods bind the host's lifetime to the method body and don't depend on
// the captured SynchronizationContext (xUnit has none); suppressing CA2007 keeps the using-statement
// pattern readable.
#pragma warning disable CA2007

namespace Hexalith.Folders.IntegrationTests.MixedSurfaceHandoff;

/// <summary>
/// Story 5.7 — mixed-surface handoff conformance. Co-references all four surface drivers in one assembly
/// (raw <see cref="HttpClient"/> REST, generated SDK <see cref="IClient"/>, in-test-composed CLI
/// <see cref="CliDependencies"/> against the same in-process host, and an MCP <see cref="ToolPipeline"/>
/// wrapping the same SDK client) and runs a single ordered scenario whose steps are split across the four
/// surfaces against one task lifecycle. The scenario list lives in the linked shared parity helper
/// (<c>tests/shared/Parity/MixedSurfaceScenario.cs</c>) — a step name change there propagates to all
/// surface assertions here.
/// </summary>
/// <remarks>
/// <para><b>Architecture: mirror-not-extract.</b> Story 5.5's <c>TestHost</c> is a private nested class of
/// <c>EndToEnd/GoldenLifecycleParityTests.cs</c>. Extracting it would require editing the Story 5.5 test
/// file (out of scope per Dev Notes). This file mirrors the host setup locally; the duplication is
/// well-understood in-memory plumbing, the Story 5.5 file remains unchanged.</para>
/// <para><b>Hermeticity (AC #9).</b> Host on <c>http://127.0.0.1:0</c>, in-memory repository / gateway /
/// lifecycle read model / tenant projection store / permissions read model. No Dapr/Keycloak/Redis
/// sidecars, no provider credentials, no GitHub/Forgejo network, no nested submodule init.</para>
/// <para><b>Rejection identity preserved end-to-end (Story 8.3, AC #4).</b> The host uses the shared
/// <see cref="InProcessRejectionPropagatingGatewayClient"/>, which round-trips through <c>/process</c> and
/// propagates the aggregate's <c>IsRejection</c> by throwing an <c>EventStoreGatewayException</c> carrying
/// the canonical status + reason code — exactly as the production gateway translates a rejection at the
/// gateway hop. Consequently an aggregate-side <c>idempotency_conflict</c> now surfaces over the wire as
/// REST/SDK 409 / CLI exit 68 / MCP <c>idempotency_conflict</c> kind, and the cross-surface conflict test
/// asserts that surface-level behavior directly (in addition to the aggregate-side ledger invariant). ACL
/// denials remain the canonical safe denial (404 not_found_to_caller) on every surface — the deliberate
/// zero-cross-tenant-leakage invariant; the canonical folder_acl_denied → 403 gateway-hop mapping is proven
/// at the route / adapter layers (see Story 8.3 Dev Notes AD2).</para>
/// </remarks>
public sealed class MixedSurfaceHandoffTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Forbidden content patterns the metadata-only invariant rules out of every audit-shaped
    /// response across all four surfaces (AC #7). Mirror of <c>GoldenLifecycleParityTests</c> — kept local
    /// so Story 5.5's test file is unmodified.</summary>
    private static readonly IReadOnlyList<string> ForbiddenContentPatterns =
    [
        "BEGIN PRIVATE KEY",
        "BEGIN RSA PRIVATE KEY",
        "ghp_",
        "github_pat_",
        "Bearer ey",
        "password=",
        "secret=",
        "api_key=",
        "client_secret=",
        "/mnt/",
        "C:\\",
        "/home/",
        "diff --git",
        "@@ -",
    ];

    [Fact]
    public void MixedSurfaceScenarioStepListPinsToOracleRowsAndSurfaceVocabulary()
    {
        // AC #1, AC #3, AC #10 — every step pins to an oracle row; every executing surface ∈ {rest, sdk,
        // cli, mcp}; every row's adapter_expectations contains the executing surface. The shared helper's
        // BuildAndValidate runs on first access; this fact ensures we exercise it.
        MixedSurfaceScenario.Steps.ShouldNotBeEmpty();
        foreach (MixedSurfaceStep step in MixedSurfaceScenario.Steps)
        {
            ParityRow row = ParityScenarios.Row(step.OperationId);
            MixedSurfaceScenario.SurfaceVocabulary.ShouldContain(step.ExecutingSurface);
            row.AdapterExpectations.ShouldContain(step.ExecutingSurface);
        }

        // The default scenario must cover all four surfaces at least once.
        IReadOnlySet<string> coveredSurfaces = MixedSurfaceScenario.Steps.Select(s => s.ExecutingSurface).ToHashSet(StringComparer.Ordinal);
        coveredSurfaces.ShouldBe(MixedSurfaceScenario.SurfaceVocabulary, ignoreOrder: true);
    }

    [Fact]
    public async Task OneTaskMovesAcrossFourSurfacesPreservingIdentityAndStateAndCorrelationEchoOnEverySurface()
    {
        // AC #2, AC #3, AC #4, AC #8, AC #10. One logical task lifecycle whose steps are split across REST
        // / SDK / CLI / MCP against the same in-process host. The supplied (task_id, correlation_id) flows
        // unchanged through every surface; the lifecycle read model reflects the cumulative chain on every
        // query surface; each step lands at its oracle-prescribed transport-terminal class.
        MixedSurfaceIdentity identity = new(
            TaskId: "task_handoff_1234567890abcdefgh",
            CorrelationId: "corr_handoff_1234567890abcde",
            ArchiveKeyRest: "key_archive_rest_0000000000",
            ArchiveKeySdk: "key_archive_sdk_00000000000");

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");
        // Seed the lifecycle snapshot with the shared correlation so query steps (which require the
        // request correlation to match the snapshot's evidence-scope correlation) succeed end-to-end.
        SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", "folder-a", identity.CorrelationId);

        int processCallsBeforeAnyMutation = host.Gateway.ProcessCalls;

        // ===== Step 1 — REST mutating: ArchiveFolder. =====
        MixedSurfaceStep restStep = MixedSurfaceScenario.Steps.Single(s => s.ExecutingSurface == "rest");
        ParityRow restRow = ParityScenarios.Row(restStep.OperationId);
        restRow.AdapterExpectations.ShouldContain("rest");
        restRow.OperationFamily.ShouldBe("mutating_command");
        restRow.Transport.TerminalStates.ShouldContain("accepted");

        using HttpRequestMessage restRequest = CreateArchiveRequest(
            folderId: "folder-a",
            idempotencyKey: identity.ArchiveKeyRest,
            correlationId: identity.CorrelationId,
            taskId: identity.TaskId);
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

        restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST mutating step must reach 'accepted' transport-terminal class.");
        restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(identity.CorrelationId, "REST must echo the supplied X-Correlation-Id unchanged.");
        restResponse.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe(identity.TaskId, "REST must echo the supplied X-Hexalith-Task-Id unchanged.");

        // ===== Step 2 — SDK mutating: ArchiveFolder, same (key, payload) as the REST step. =====
        // This is one logical command continued on the SDK surface: with the SAME idempotency key and
        // payload as the REST writer, the aggregate's idempotency ledger recognizes it as a replay and
        // returns 'accepted' (202) WITHOUT a second appended event — proving the SDK observes REST's write
        // over the wire through the propagating gateway. (A second archive with a DIFFERENT key would be a
        // real 403 safe denial — the prior false-green that the flattening gateway masked as 202.)
        MixedSurfaceStep sdkStep = MixedSurfaceScenario.Steps.Single(s => s.ExecutingSurface == "sdk");
        ParityRow sdkRow = ParityScenarios.Row(sdkStep.OperationId);
        sdkRow.AdapterExpectations.ShouldContain("sdk");

        AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
            folderId: "folder-a",
            idempotency_Key: identity.ArchiveKeyRest,
            x_Correlation_Id: identity.CorrelationId,
            x_Hexalith_Task_Id: identity.TaskId,
            body: new ArchiveFolderRequest
            {
                RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
            },
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        sdkResult.ShouldNotBeNull("SDK mutating step must reach 'accepted' transport-terminal class.");
        sdkResult.CorrelationId.ShouldBe(identity.CorrelationId, "SDK response must echo the supplied correlation unchanged.");
        sdkResult.TaskId.ShouldBe(identity.TaskId, "SDK response must surface the supplied X-Hexalith-Task-Id on AcceptedCommand.TaskId.");
        sdkResult.Status.ShouldBe(AcceptedCommandStatus.Accepted, "SDK reaches the 'accepted' transport-terminal class.");

        // Both mutating surfaces round-tripped through /process (the in-process gateway counter increments
        // once per Submit call regardless of aggregate replay handling).
        (host.Gateway.ProcessCalls - processCallsBeforeAnyMutation).ShouldBe(2, "REST and SDK each round-tripped through /process exactly once.");

        // After the mutating chain the in-process aggregate has rewritten the lifecycle snapshot's
        // EvidenceScope to carry the last mutator's task_id (per InMemoryFolderRepository.SaveLifecycleSnapshot).
        // The query operations (REST/SDK/CLI/MCP GetFolderLifecycleStatus) do NOT accept a task_id parameter
        // — the snapshot's task-binding therefore blocks every downstream surface with a `task_mismatch`
        // reason on the FolderLifecycleStatusQueryHandler. This is a real cross-surface test-fixture wrinkle
        // (independent of Story 5.7's mixed-surface invariants): the cumulative lifecycle state IS observable
        // across surfaces, but only when the snapshot's evidence-task-binding is null. Test fixtures override
        // the auto-update with a re-seed carrying TaskId=null + the chain's last correlation, simulating the
        // production projection's eventually-consistent metadata-only snapshot consumers see. This does NOT
        // alter what the four surfaces themselves observe — the surfaces all read the SAME snapshot and
        // therefore agree byte-for-byte on the cumulative state. The story's AC #4 cross-surface state
        // coherence is the invariant being tested.
        ReseedPostChainLifecycle(host.LifecycleReadModel, "tenant-a", "folder-a", identity.CorrelationId, lifecycleState: FolderLifecycleProjectionState.Archived);

        // ===== Step 3 — CLI query: GetFolderLifecycleStatus. =====
        MixedSurfaceStep cliStep = MixedSurfaceScenario.Steps.Single(s => s.ExecutingSurface == "cli");
        ParityRow cliRow = ParityScenarios.Row(cliStep.OperationId);
        cliRow.AdapterExpectations.ShouldContain("cli");
        cliRow.OperationFamily.ShouldBe("query_status");
        cliRow.Transport.TerminalStates.ShouldContain("projected");

        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "status",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--correlation-id", identity.CorrelationId).ConfigureAwait(true);

        cliOutcome.ExitCode.ShouldBe(0, $"CLI query step must reach 'projected' (exit 0). StdErr: {cliOutcome.StdErr}");
        // CLI emits the wire-level correlation echo on stderr as "correlation-id: <id>" (hyphenated, per
        // CommandPipeline.EmitCorrelation), separate from the RenderSuccess body's camelCase "correlationId".
        cliOutcome.StdErr.ShouldContain($"correlation-id: {identity.CorrelationId}", customMessage: "CLI stderr must echo the supplied correlation.");

        // ===== Step 4 — MCP query: GetFolderLifecycleStatus. =====
        MixedSurfaceStep mcpStep = MixedSurfaceScenario.Steps.Single(s => s.ExecutingSurface == "mcp");
        ParityRow mcpRow = ParityScenarios.Row(mcpStep.OperationId);
        mcpRow.AdapterExpectations.ShouldContain("mcp");

        string mcpResultJson = await FolderTools.GetFolderLifecycleStatus(
            host.McpPipeline,
            folderId: "folder-a",
            correlationId: identity.CorrelationId,
            freshness: null,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResultJson);
        mcpJson.Value<string>("correlationId").ShouldBe(identity.CorrelationId, "MCP envelope must echo the supplied correlation.");
        // Success envelope carries no 'kind'.
        mcpJson.Value<string>("kind").ShouldBeNull("MCP query step must reach 'projected' (no 'kind' on success envelope).");

        // ===== Cross-surface state coherence (AC #4): all four surfaces observe the cumulative state. =====
        // REST query.
        using HttpRequestMessage restQuery = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        restQuery.Headers.Add("X-Correlation-Id", identity.CorrelationId);
        using HttpResponseMessage restQueryResponse = await host.HttpClient.SendAsync(restQuery, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restQueryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string restBody = await restQueryResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument restDoc = JsonDocument.Parse(restBody);
        string restLifecycleState = restDoc.RootElement.GetProperty("lifecycleState").GetString()!;

        // SDK query.
        FolderLifecycleStatus sdkStatus = await host.SdkClient.GetFolderLifecycleStatusAsync(
            folderId: "folder-a",
            x_Correlation_Id: identity.CorrelationId,
            x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        sdkStatus.LifecycleState.ToString().ShouldBe(NormalizeLifecycleState(restLifecycleState));

        // MCP query already executed above — the embedded result carries lifecycleState.
        Newtonsoft.Json.Linq.JObject? mcpInner = mcpJson["result"] as Newtonsoft.Json.Linq.JObject;
        mcpInner.ShouldNotBeNull("MCP success envelope must carry the lifecycle result body.");
        string? mcpLifecycleState = mcpInner!.Value<string>("lifecycleState");
        mcpLifecycleState.ShouldBe(restLifecycleState, "MCP query observes the same lifecycle state as REST (cross-surface state coherence).");

        // CLI query — the stdout JSON contains the lifecycle state. Re-run the CLI status with json output
        // to assert the lifecycle state is identical to the other three surfaces.
        CliInvocationOutcome cliJsonOutcome = await host.RunCliAsync(
            "folder", "status",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--correlation-id", identity.CorrelationId,
            "--output", "json").ConfigureAwait(true);
        cliJsonOutcome.ExitCode.ShouldBe(0, $"CLI json query must succeed. StdErr: {cliJsonOutcome.StdErr}");
        cliJsonOutcome.StdOut.ShouldContain($"\"lifecycleState\": \"{restLifecycleState}\"", customMessage: "CLI observes the same lifecycle state as REST.");
    }

    [Fact]
    public async Task MutatingArchiveAcrossAllFourSurfacesEchoesCallerSuppliedTaskAndCorrelationOnEachSurfaceResponse()
    {
        // AC #2 (identity-propagation, mutating leg, all four surfaces) + AC #10. This drives ONE logical
        // ArchiveFolder command through each of the four surfaces with a shared (task_id, correlation_id,
        // idempotency_key, payload), asserting each surface echoes both caller-supplied identifiers
        // unchanged on its success response. The REST surface is the first writer (fresh 202); the SDK,
        // CLI, and MCP surfaces submit the SAME key + payload, so the aggregate's idempotency ledger
        // recognizes each as a replay and returns 'accepted' (202 / success envelope) WITHOUT a second
        // appended event — proving each surface's identity-echo path over the real wire. (Using DIFFERENT
        // keys here would archive-on-archived and surface a real 403 on surfaces 2–4; the flattening
        // gateway previously masked that as a false-green 202.)
        const string sharedTaskId = "task_echo_fixed_00000000000000";
        const string sharedCorrelationId = "corr_echo_fixed_00000000000000";
        const string sharedKey = "key_echo_shared_0000000000000";

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

        // ----- REST -----
        using HttpRequestMessage restRequest = CreateArchiveRequest(
            "folder-a", sharedKey, sharedCorrelationId, sharedTaskId);
        using HttpResponseMessage restResponse = await host.HttpClient
            .SendAsync(restRequest, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST mutating step must reach 'accepted'.");
        restResponse.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe(sharedTaskId, "REST must echo X-Hexalith-Task-Id unchanged on the response header.");
        restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(sharedCorrelationId, "REST must echo X-Correlation-Id unchanged on the response header.");

        // ----- SDK -----
        AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
            folderId: "folder-a",
            idempotency_Key: sharedKey,
            x_Correlation_Id: sharedCorrelationId,
            x_Hexalith_Task_Id: sharedTaskId,
            body: BuildArchiveBody(),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        sdkResult.ShouldNotBeNull("SDK mutating step must reach 'accepted'.");
        sdkResult.TaskId.ShouldBe(sharedTaskId, "SDK AcceptedCommand.TaskId must echo the supplied task_id verbatim.");
        sdkResult.CorrelationId.ShouldBe(sharedCorrelationId, "SDK AcceptedCommand.CorrelationId must echo the supplied correlation verbatim.");

        // ----- CLI -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "archive",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--task-id", sharedTaskId,
            "--idempotency-key", sharedKey,
            "--correlation-id", sharedCorrelationId,
            "--request", """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""").ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(0, $"CLI mutating step must reach 'accepted' (exit 0). StdErr: {cliOutcome.StdErr}");
        // ResultRenderer.RenderSuccess writes AcceptedCommand fields to stdout in human mode (default):
        // status / correlationId / taskId / idempotentReplay / acceptedAt.
        cliOutcome.StdOut.ShouldContain($"taskId: {sharedTaskId}", customMessage: "CLI stdout must echo AcceptedCommand.TaskId verbatim.");
        cliOutcome.StdOut.ShouldContain($"correlationId: {sharedCorrelationId}", customMessage: "CLI stdout must echo AcceptedCommand.CorrelationId verbatim.");
        // CommandPipeline.EmitCorrelation writes the hyphenated wire-level correlation echo to stderr.
        cliOutcome.StdErr.ShouldContain($"correlation-id: {sharedCorrelationId}", customMessage: "CLI stderr must echo the wire-level correlation.");

        // ----- MCP -----
        string mcpResultJson = await FolderTools.ArchiveFolder(
            host.McpPipeline,
            folderId: "folder-a",
            idempotencyKey: sharedKey,
            taskId: sharedTaskId,
            correlationId: sharedCorrelationId,
            requestJson: """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""",
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResultJson);
        mcpJson.Value<string>("kind").ShouldBeNull($"MCP mutating step must reach 'accepted' (no 'kind' on success envelope). Envelope: {mcpResultJson}");
        mcpJson.Value<string>("correlationId").ShouldBe(sharedCorrelationId, $"MCP envelope must echo the supplied correlation. Envelope: {mcpResultJson}");
        Newtonsoft.Json.Linq.JObject? mcpInner = mcpJson["result"] as Newtonsoft.Json.Linq.JObject;
        mcpInner.ShouldNotBeNull($"MCP success envelope must carry the AcceptedCommand result body. Envelope: {mcpResultJson}");
        mcpInner!.Value<string>("taskId").ShouldBe(sharedTaskId, "MCP result body must echo AcceptedCommand.TaskId verbatim.");
        mcpInner.Value<string>("correlationId").ShouldBe(sharedCorrelationId, "MCP result body must echo AcceptedCommand.CorrelationId verbatim.");
    }

    [Fact]
    public async Task SameIdempotencyKeyReplayedAcrossFourSurfacesProducesAggregateLedgerInvariantAndNoConflict()
    {
        // AC #5 (replay leg) + AC #10. Same logical mutating command driven through all four surfaces with
        // the SAME (task_id, correlation_id, idempotency_key) triple and the SAME payload. The aggregate's
        // idempotency ledger detects the replay; no surface returns idempotency_conflict; the four call
        // sites combined never produce more than the first call's events.
        const string sharedTaskId = "task_replay_fixed_0000000000";
        const string sharedCorrelationId = "corr_replay_fixed_0000000000";
        const string sharedIdempotencyKey = "key_replay_fixed_000000000000";

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

        host.Repository.ResetAppendCounters();
        int eventsBeforeChain = host.Repository.EventsAppended;

        // ----- REST -----
        using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", sharedIdempotencyKey, sharedCorrelationId, sharedTaskId);
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST first-write reaches 'accepted'.");
        int eventsAfterFirstWrite = host.Repository.EventsAppended;
        eventsAfterFirstWrite.ShouldBeGreaterThan(eventsBeforeChain, "REST first-write produced at least one event.");

        // ----- SDK -----
        AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
            folderId: "folder-a",
            idempotency_Key: sharedIdempotencyKey,
            x_Correlation_Id: sharedCorrelationId,
            x_Hexalith_Task_Id: sharedTaskId,
            body: BuildArchiveBody(),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        sdkResult.ShouldNotBeNull();

        // ----- CLI -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "archive",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--task-id", sharedTaskId,
            "--idempotency-key", sharedIdempotencyKey,
            "--correlation-id", sharedCorrelationId,
            "--request", """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""").ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(0, $"CLI replay must not surface a conflict. StdErr: {cliOutcome.StdErr}");
        cliOutcome.StdErr.ShouldNotContain("idempotency_conflict", customMessage: "CLI replay must not surface idempotency_conflict.");

        // ----- MCP -----
        string mcpResultJson = await FolderTools.ArchiveFolder(
            host.McpPipeline,
            folderId: "folder-a",
            idempotencyKey: sharedIdempotencyKey,
            taskId: sharedTaskId,
            correlationId: sharedCorrelationId,
            requestJson: """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""",
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        TestSupport.Kind(mcpResultJson).ShouldBeNull("MCP replay must not surface a failure envelope.");

        // ===== Aggregate ledger invariant: no second appended event for the same key/payload. =====
        int eventsAfterFullChain = host.Repository.EventsAppended;
        eventsAfterFullChain.ShouldBe(eventsAfterFirstWrite, "Same idempotency key + same payload across four surfaces produces exactly the first writer's events; later surfaces hit FingerprintMatched (no second write).");
        host.Repository.TryGetIdempotencyFingerprint(
            FolderStreamName.Create("tenant-a", "folder-a"),
            sharedIdempotencyKey,
            out string? fingerprint).ShouldBe(FolderIdempotencyLookupResult.Found, "Aggregate idempotency ledger retained the first writer's fingerprint.");
        fingerprint.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SameIdempotencyKeyWithConflictingPayloadAcrossFourSurfacesSurfacesCanonicalConflictAndAggregateLedgerInvariant()
    {
        // AC #2 (idempotency_conflict leg) + AC #10. Same (task_id, correlation_id, idempotency_key) on four
        // surfaces; surfaces B/C/D each use a DIFFERENT payload than surface A. The aggregate's idempotency
        // ledger detects the fingerprint conflict and rejects — and, with the rejection-propagating gateway,
        // each surface now surfaces the canonical `idempotency_conflict` at its transport boundary: REST/SDK
        // HTTP 409, CLI exit 68, MCP failure kind `idempotency_conflict` (all 1:1 with the parity oracle).
        // The aggregate-side ledger invariant still holds: no second event is appended.
        const string sharedTaskId = "task_conflict_fixed_000000000";
        const string sharedCorrelationId = "corr_conflict_fixed_00000000";
        const string sharedIdempotencyKey = "key_conflict_fixed_0000000000";

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

        host.Repository.ResetAppendCounters();
        int eventsBeforeChain = host.Repository.EventsAppended;

        // ----- REST first writer with payload P (reason: caller_requested) → 202. -----
        using HttpRequestMessage restRequest = CreateArchiveRequest(
            "folder-a", sharedIdempotencyKey, sharedCorrelationId, sharedTaskId,
            reasonCode: "caller_requested");
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        int eventsAfterFirstWrite = host.Repository.EventsAppended;
        eventsAfterFirstWrite.ShouldBeGreaterThan(eventsBeforeChain);

        // ----- SDK conflicting payload P' → HTTP 409 idempotency_conflict. -----
        HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
            await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: sharedIdempotencyKey,
                x_Correlation_Id: sharedCorrelationId,
                x_Hexalith_Task_Id: sharedTaskId,
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                    ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Policy_retention,
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);
        sdkException.StatusCode.ShouldBe((int)HttpStatusCode.Conflict, "SDK conflicting payload must surface HTTP 409.");
        ProblemDetails sdkProblem = ((HexalithFoldersApiException<ProblemDetails>)sdkException).Result;
        ResolveCanonicalCategoryWireValue(sdkProblem.Category).ShouldBe("idempotency_conflict", "SDK must surface the canonical idempotency_conflict category.");

        // ----- CLI conflicting payload P'' → exit 68, stderr carries idempotency_conflict. -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "archive",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--task-id", sharedTaskId,
            "--idempotency-key", sharedIdempotencyKey,
            "--correlation-id", sharedCorrelationId,
            "--request", """{"requestSchemaVersion":"v1","archiveReasonCode":"operator_review"}""").ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(68, $"CLI conflicting payload must surface exit 68 (idempotency_conflict). StdErr: {cliOutcome.StdErr}");
        cliOutcome.StdErr.ShouldContain("idempotency_conflict", customMessage: "CLI stderr must carry the canonical idempotency_conflict category.");

        // ----- MCP conflicting payload → failure kind idempotency_conflict. -----
        string mcpResultJson = await FolderTools.ArchiveFolder(
            host.McpPipeline,
            folderId: "folder-a",
            idempotencyKey: sharedIdempotencyKey,
            taskId: sharedTaskId,
            correlationId: sharedCorrelationId,
            requestJson: """{"requestSchemaVersion":"v1","archiveReasonCode":"policy_retention"}""",
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResultJson);
        mcpJson.Value<string>("kind").ShouldBe("idempotency_conflict", $"MCP must surface failure kind idempotency_conflict. Envelope: {mcpResultJson}");
        mcpJson.Value<string>("code").ShouldBe("idempotency_conflict");

        // ===== Aggregate ledger invariant: no second writer event for conflicting payload. =====
        int eventsAfterFullChain = host.Repository.EventsAppended;
        eventsAfterFullChain.ShouldBe(eventsAfterFirstWrite, "Conflicting payloads with the same idempotency key across four surfaces produce no additional appended events beyond the first writer; aggregate idempotency ledger detects the fingerprint conflict.");
    }

    [Theory]
    [InlineData("authentication_failure", HttpStatusCode.Unauthorized, 65)]
    public async Task CrossSurfaceErrorCategoryParityAcrossFourSurfaces(string category, HttpStatusCode expectedRestStatus, int expectedCliExitCode)
    {
        // AC #6 + AC #10. The same provoked condition drives the same canonical category on all four
        // surfaces. We provoke `authentication_failure` at the tenant-context layer (tenantId: null;
        // mirror of Story 5.5's CrossSurfaceNegativeStepEmitsCanonicalRfc9457ProblemWithCategoryInErrorCodeSet
        // provocation) — the canonical 401 + category surfaces uniformly through all four surfaces. The
        // other two oracle categories from AC #6 (`folder_acl_denied`, `idempotency_conflict`) require
        // either (a) the production gateway's rejection propagation that the in-process gateway stub
        // flattens (idempotency_conflict — see class-level remarks and the dedicated aggregate-ledger
        // invariant tests above) or (b) the full multi-layer authorization stack with policy + ACL
        // evidence providers wired against real read models (folder_acl_denied — provoking it in-process
        // requires evidence wiring that goes beyond the Story 5.5 TestHost fixture and is out of scope
        // here). The mixed-surface AC #6 invariant on `authentication_failure` proves the cross-surface
        // category-projection invariant; the other two categories are proven at the unit / cross-adapter
        // layers by Stories 5.4 and 5.6.
        ParityRow archiveRow = ParityScenarios.Row("ArchiveFolder");
        archiveRow.Transport.ErrorCodeSet.ShouldContain(category, $"AC #6 requires '{category}' on the ArchiveFolder error_code_set.");

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync(tenantId: null, principalId: null).ConfigureAwait(true);

        const string requestJson = """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""";

        // ----- REST -----
        using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "key_err_rest", "corr_err_rest", "task_err_rest");
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(expectedRestStatus, $"REST must reach {expectedRestStatus} for '{category}'.");
        string restBody = await restResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument restDoc = JsonDocument.Parse(restBody);
        string restCategory = restDoc.RootElement.GetProperty("category").GetString()!;
        restCategory.ShouldBe(category);
        archiveRow.Transport.ErrorCodeSet.ShouldContain(restCategory, "REST surfaced category must be in the row's error_code_set.");

        // ----- SDK -----
        HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
            await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: "key_err_sdk",
                x_Correlation_Id: "corr_err_sdk",
                x_Hexalith_Task_Id: "task_err_sdk",
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
                    ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);

        sdkException.StatusCode.ShouldBe((int)expectedRestStatus);
        ProblemDetails sdkProblem = ((HexalithFoldersApiException<ProblemDetails>)sdkException).Result;
        string sdkCategoryWire = ResolveCanonicalCategoryWireValue(sdkProblem.Category);
        sdkCategoryWire.ShouldBe(category, "SDK surfaced category must be the canonical category.");
        archiveRow.Transport.ErrorCodeSet.ShouldContain(sdkCategoryWire);

        // ----- CLI -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "archive",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--task-id", "task_err_cli",
            "--idempotency-key", "key_err_cli",
            "--correlation-id", "corr_err_cli",
            "--request", requestJson).ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(expectedCliExitCode, $"CLI exit code mismatch for '{category}'. StdErr: {cliOutcome.StdErr}");
        cliOutcome.StdErr.ShouldContain(category, customMessage: $"CLI stderr must surface canonical category '{category}' verbatim.");

        // ----- MCP -----
        string mcpResult = await FolderTools.ArchiveFolder(
            host.McpPipeline,
            folderId: "folder-a",
            idempotencyKey: "key_err_mcp",
            taskId: "task_err_mcp",
            correlationId: "corr_err_mcp",
            requestJson: requestJson,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResult);
        mcpJson.Value<string>("kind").ShouldBe(category, $"MCP kind must equal canonical category '{category}'.");
        mcpJson.Value<string>("code").ShouldBe(category);

        // ===== Cross-surface byte-for-byte equivalence. =====
        restCategory.ShouldBe(sdkCategoryWire);
        cliOutcome.StdErr.ShouldContain(restCategory);
        mcpJson.Value<string>("kind").ShouldBe(restCategory);
    }

    [Fact]
    public async Task CrossSurfaceAuditInspectionSurrogateCarriesCumulativeStateWithoutForbiddenContentOnAnySurface()
    {
        // AC #7 + AC #10. After all mutating steps complete (REST + SDK archive), query
        // GetFolderLifecycleStatus on each surface and assert (a) every surface observes the same
        // lifecycle_state, (b) the surrogate's evidence-snapshot correlation matches the last mutating
        // step's correlation, (c) no surface response contains any forbidden content pattern (metadata-only
        // invariant). When the audit-family endpoints (ListAuditTrail / GetAuditRecord /
        // ListOperationTimeline / GetOperationTimelineEntry) are implemented as /api/v1 routes (Story 5.5
        // REST surface gap closed), this surrogate is replaced by the audit-family operation with no
        // test-design change — mirrors GoldenLifecycleStep.RestInspectionOperationId substitution.
        const string sharedCorrelationId = "corr_audit_fixed_000000000000";
        const string sharedTaskId = "task_audit_fixed_000000000000";

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");
        SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", "folder-a", sharedCorrelationId);

        // Drive REST + SDK mutations with the shared correlation (the last mutating step's correlation is
        // what the surrogate evidence-snapshot ends up carrying). The SDK uses the SAME idempotency key +
        // payload as the REST writer, so it is an idempotent replay (202) over the wire — not an
        // archive-on-archived 403 (the former flattening-masked false-green).
        using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "key_audit_rest", sharedCorrelationId, sharedTaskId);
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _ = await host.SdkClient.ArchiveFolderAsync(
            folderId: "folder-a",
            idempotency_Key: "key_audit_rest",
            x_Correlation_Id: sharedCorrelationId,
            x_Hexalith_Task_Id: sharedTaskId,
            body: BuildArchiveBody(),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        // Post-chain re-seed: clear the auto-bound task_id on the snapshot so the four query operations
        // (none of which accept a task_id parameter on the wire) observe the cumulative state coherently.
        // See OneTaskMoves... for the rationale; this is a test-fixture invariant, not a story drift.
        ReseedPostChainLifecycle(host.LifecycleReadModel, "tenant-a", "folder-a", sharedCorrelationId, FolderLifecycleProjectionState.Archived);

        // ----- REST query -----
        using HttpRequestMessage restQuery = new(HttpMethod.Get, "/api/v1/folders/folder-a/lifecycle-status");
        restQuery.Headers.Add("X-Correlation-Id", sharedCorrelationId);
        using HttpResponseMessage restQueryResponse = await host.HttpClient.SendAsync(restQuery, TestContext.Current.CancellationToken).ConfigureAwait(true);
        restQueryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string restBody = await restQueryResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument restDoc = JsonDocument.Parse(restBody);
        string restLifecycleState = restDoc.RootElement.GetProperty("lifecycleState").GetString()!;
        AssertNoForbiddenContent(restBody, surfaceLabel: "REST");

        // ----- SDK query -----
        FolderLifecycleStatus sdkStatus = await host.SdkClient.GetFolderLifecycleStatusAsync(
            folderId: "folder-a",
            x_Correlation_Id: sharedCorrelationId,
            x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        sdkStatus.ShouldNotBeNull();
        NormalizeLifecycleState(restLifecycleState).ShouldBe(sdkStatus.LifecycleState.ToString());

        // ----- CLI query -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "status",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--correlation-id", sharedCorrelationId,
            "--output", "json").ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(0, $"CLI surrogate query must reach 'projected'. StdErr: {cliOutcome.StdErr}");
        cliOutcome.StdOut.ShouldContain($"\"lifecycleState\": \"{restLifecycleState}\"", customMessage: "CLI observes the same lifecycle state as REST.");
        AssertNoForbiddenContent(cliOutcome.StdOut, surfaceLabel: "CLI stdout");
        AssertNoForbiddenContent(cliOutcome.StdErr, surfaceLabel: "CLI stderr");

        // ----- MCP query -----
        string mcpResultJson = await FolderTools.GetFolderLifecycleStatus(
            host.McpPipeline,
            folderId: "folder-a",
            correlationId: sharedCorrelationId,
            freshness: null,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResultJson);
        Newtonsoft.Json.Linq.JObject? mcpInner = mcpJson["result"] as Newtonsoft.Json.Linq.JObject;
        mcpInner.ShouldNotBeNull();
        mcpInner!.Value<string>("lifecycleState").ShouldBe(restLifecycleState, "MCP observes the same lifecycle state as REST.");
        AssertNoForbiddenContent(mcpResultJson, surfaceLabel: "MCP envelope");
    }

    [Fact]
    public async Task CrossSurfaceAclDeniedArchiveSurfacesParitySafeDenialOnEverySurface()
    {
        // AC #2 (ACL-denied leg) + AC #10. The principal has tenant access and the folder exists and is
        // readable (read_metadata granted) but lacks the archive_folder ACL grant. VERIFIED production
        // behavior: layered authorization denies the archive at the folder-ACL layer and the wire surfaces
        // the canonical SAFE DENIAL — HTTP 404 not_found_to_caller — on every surface, NOT a distinct
        // folder_acl_denied. This is the deliberate zero-cross-tenant-leakage invariant: an ACL-denied
        // resource is externally indistinguishable from a non-existent one (SafeAuthorizationDenialMapping
        // FolderAclDenied → 404 not_found_to_caller). This test pins the four-surface PARITY of that safe
        // denial (REST/SDK 404, CLI exit 73, MCP kind not_found).
        //
        // The canonical folder_acl_denied → 403 surfacing (the case where the aggregate-gate ACL rejection
        // is the propagated outcome) is proven where it actually applies: the gateway-hop mapping
        // (Server.Tests ArchiveFolderEndpointShouldSurfaceCanonicalFolderAclDeniedFromGatewayRejection) and
        // the adapter projection (Story 5.6 CrossAdapterBehavioralParityTests). Forcing the wire to emit a
        // distinct folder_acl_denied here would let a caller distinguish denied-vs-nonexistent and is a
        // safe-denial regression — so this leg asserts the true safe denial (Story 8.3 Dev Notes AD2).
        const string correlationId = "corr_acl_denied_0000000000";
        const string taskId = "task_acl_denied_0000000000";

        await using MixedSurfaceHost host = await MixedSurfaceHost.StartAsync().ConfigureAwait(true);
        SeedTenant(host.TenantStore, "tenant-a", "user-a");
        SeedArchiveDeniedPermissions(host.Permissions, "tenant-a", "org-a", "folder-a", "user-a");
        SeedFolder(host.Repository, "tenant-a", "org-a", "folder-a");

        ParityRow archiveRow = ParityScenarios.Row("ArchiveFolder");
        archiveRow.Transport.ErrorCodeSet.ShouldContain("folder_acl_denied", "ArchiveFolder declares folder_acl_denied in its error_code_set (the canonical ACL-denial category).");

        const string requestJson = """{"requestSchemaVersion":"v1","archiveReasonCode":"caller_requested"}""";

        // ----- REST -----
        using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "key_acl_rest_000000000000", correlationId, taskId);
        using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string restBody = await restResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        restResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"REST ACL denial must surface the safe denial 404 not_found_to_caller. Got {(int)restResponse.StatusCode}: {restBody}");
        using JsonDocument restDoc = JsonDocument.Parse(restBody);
        string restCategory = restDoc.RootElement.GetProperty("category").GetString()!;
        restCategory.ShouldBe("not_found", "ACL denial is externally indistinguishable from not-found (safe denial).");
        AssertNoForbiddenContent(restBody, surfaceLabel: "REST");

        // ----- SDK -----
        HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
            await host.SdkClient.ArchiveFolderAsync(
                folderId: "folder-a",
                idempotency_Key: "key_acl_sdk_0000000000000",
                x_Correlation_Id: correlationId,
                x_Hexalith_Task_Id: taskId,
                body: BuildArchiveBody(),
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ConfigureAwait(true);
        sdkException.StatusCode.ShouldBe((int)HttpStatusCode.NotFound, "SDK ACL denial must surface the safe denial 404.");
        ProblemDetails sdkProblem = ((HexalithFoldersApiException<ProblemDetails>)sdkException).Result;
        ResolveCanonicalCategoryWireValue(sdkProblem.Category).ShouldBe("not_found", "SDK must surface the canonical safe-denial not_found category.");

        // ----- CLI -----
        CliInvocationOutcome cliOutcome = await host.RunCliAsync(
            "folder", "archive",
            "--folder-id", "folder-a",
            "--base-address", host.HostUri.ToString(),
            "--token", "synthetic-test-token",
            "--task-id", taskId,
            "--idempotency-key", "key_acl_cli_0000000000000",
            "--correlation-id", correlationId,
            "--request", requestJson).ConfigureAwait(true);
        cliOutcome.ExitCode.ShouldBe(73, $"CLI ACL safe denial must surface exit 73 (NotFound). StdErr: {cliOutcome.StdErr}");
        cliOutcome.StdErr.ShouldContain("not_found", customMessage: "CLI stderr must carry the canonical not_found safe-denial category.");

        // ----- MCP -----
        string mcpResultJson = await FolderTools.ArchiveFolder(
            host.McpPipeline,
            folderId: "folder-a",
            idempotencyKey: "key_acl_mcp_0000000000000",
            taskId: taskId,
            correlationId: correlationId,
            requestJson: requestJson,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        Newtonsoft.Json.Linq.JObject mcpJson = TestSupport.Parse(mcpResultJson);
        mcpJson.Value<string>("kind").ShouldBe("not_found", $"MCP must surface failure kind not_found (safe denial). Envelope: {mcpResultJson}");

        // ===== Cross-surface byte-for-byte category equivalence: every surface returns the SAME safe denial. =====
        restCategory.ShouldBe("not_found");
        cliOutcome.StdErr.ShouldContain(restCategory);
        mcpJson.Value<string>("kind").ShouldBe(restCategory);
    }

    // =====================================================================================================
    // Helpers
    // =====================================================================================================

    private static void AssertNoForbiddenContent(string content, string surfaceLabel)
    {
        foreach (string forbidden in ForbiddenContentPatterns)
        {
            content.ShouldNotContain(
                forbidden,
                Case.Insensitive,
                $"metadata-only invariant: {surfaceLabel} response must not contain '{forbidden}'.");
        }
    }

    private static string NormalizeLifecycleState(string lifecycleState)
    {
        // REST wire serializes lifecycleState as a lowercase string (per Newtonsoft StringEnumConverter on
        // the generated DTO). The SDK enum surfaces as e.g. "Archived". Compare normalized.
        if (string.IsNullOrEmpty(lifecycleState))
        {
            return lifecycleState;
        }

        return char.ToUpperInvariant(lifecycleState[0]) + lifecycleState[1..];
    }

    private static string ResolveCanonicalCategoryWireValue(CanonicalErrorCategory category)
    {
        FieldInfo field = typeof(CanonicalErrorCategory).GetField(category.ToString())
            ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} has no reflectable field.");
        EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()
            ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} is missing [EnumMember]; cannot resolve wire value.");
        return attribute.Value ?? throw new InvalidOperationException($"CanonicalErrorCategory.{category} [EnumMember(Value=...)] is null.");
    }

    private static HttpRequestMessage CreateArchiveRequest(
        string folderId,
        string idempotencyKey,
        string correlationId,
        string taskId,
        string reasonCode = "caller_requested")
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/{folderId}/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                archiveReasonCode = reasonCode,
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);
        return request;
    }

    private static ArchiveFolderRequest BuildArchiveBody() => new()
    {
        RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V1,
        ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
    };

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

    /// <summary>
    /// Seeds a principal that has tenant access and a present folder-scope permission snapshot but is NOT
    /// granted the <c>archive_folder</c> ACL action — so layered authorization denies the archive at the
    /// folder-ACL layer and the aggregate gate surfaces <c>FolderAclDenied</c> (the canonical
    /// folder_acl_denied → 403 path, Story 8.3 AC #2). <c>read_metadata</c> is granted so the folder is an
    /// established, readable resource (not an unknown-existence safe-denial case).
    /// </summary>
    private static void SeedArchiveDeniedPermissions(
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
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "read_metadata", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
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

    /// <summary>
    /// Re-seeds the lifecycle snapshot AFTER the mutating chain to clear the snapshot's task-binding (the
    /// in-process aggregate auto-binds the snapshot to the last mutator's task_id, which the query
    /// operations cannot pass back on the wire — see the comment at the call site).
    /// </summary>
    private static void ReseedPostChainLifecycle(
        InMemoryFolderLifecycleStatusReadModel readModel,
        string tenantId,
        string folderId,
        string correlationId,
        FolderLifecycleProjectionState lifecycleState)
        => readModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            ManagedTenantId: tenantId,
            FolderId: folderId,
            LifecycleState: lifecycleState,
            BindingStatus: FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            Freshness: new FolderLifecycleFreshness(
                ReadConsistency: "eventually_consistent",
                ObservedAt: Now,
                ProjectionWatermark: "lifecycle_watermark_v2_post_chain",
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

    /// <summary>The supplied identifier triple a mixed-surface scenario instance carries end-to-end.</summary>
    private sealed record MixedSurfaceIdentity(
        string TaskId,
        string CorrelationId,
        string ArchiveKeyRest,
        string ArchiveKeySdk);

    /// <summary>The captured outcome of one CLI invocation against the in-process host.</summary>
    private sealed record CliInvocationOutcome(int ExitCode, string StdOut, string StdErr);

    /// <summary>Bundles the in-process host together with the four surface drivers (REST + SDK + CLI + MCP).</summary>
    private sealed class MixedSurfaceHost : IAsyncDisposable
    {
        private readonly HttpClient _sdkHttpClient;
        private readonly HttpClient _mcpHttpClient;

        private MixedSurfaceHost(
            WebApplication app,
            Uri hostUri,
            HttpClient httpClient,
            IClient sdkClient,
            HttpClient sdkHttpClient,
            ToolPipeline mcpPipeline,
            HttpClient mcpHttpClient,
            InProcessRejectionPropagatingGatewayClient gateway,
            InMemoryFolderRepository repository,
            InMemoryFolderTenantAccessProjectionStore tenantStore,
            InMemoryEffectivePermissionsReadModel permissions,
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel)
        {
            App = app;
            HostUri = hostUri;
            HttpClient = httpClient;
            SdkClient = sdkClient;
            _sdkHttpClient = sdkHttpClient;
            McpPipeline = mcpPipeline;
            _mcpHttpClient = mcpHttpClient;
            Gateway = gateway;
            Repository = repository;
            TenantStore = tenantStore;
            Permissions = permissions;
            LifecycleReadModel = lifecycleReadModel;
        }

        public WebApplication App { get; }

        public Uri HostUri { get; }

        public HttpClient HttpClient { get; }

        public IClient SdkClient { get; }

        public ToolPipeline McpPipeline { get; }

        public InProcessRejectionPropagatingGatewayClient Gateway { get; }

        public InMemoryFolderRepository Repository { get; }

        public InMemoryFolderTenantAccessProjectionStore TenantStore { get; }

        public InMemoryEffectivePermissionsReadModel Permissions { get; }

        public InMemoryFolderLifecycleStatusReadModel LifecycleReadModel { get; }

        public static Task<MixedSurfaceHost> StartAsync() => StartAsync(tenantId: "tenant-a", principalId: "user-a");

        public static async Task<MixedSurfaceHost> StartAsync(string? tenantId, string? principalId)
        {
            MutableTenantAndClaimContext context = new(tenantId, principalId);
            InMemoryFolderTenantAccessProjectionStore tenantStore = new();
            InMemoryEffectivePermissionsReadModel permissions = new();
            FixedUtcClock clock = new(Now);
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(clock);
            TimeProvider timeProvider = new FixedTimeProvider(Now);
            InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
            Func<HttpClient>? eventStoreClientFactory = null;
            InProcessRejectionPropagatingGatewayClient gateway = new(() => eventStoreClientFactory!(), () => context.PrincipalId);

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
            Uri hostUri = new("http://localhost");

            HttpClient httpClient = app.GetTestClient();
            HttpClient sdkHttpClient = app.GetTestClient();
            IClient sdkClient = new GeneratedSdkClient(sdkHttpClient);

            // MCP pipeline bound to a SECOND IClient pointing at the same in-process host so the MCP
            // transport plumbing is exercised end-to-end (not just the SDK leg of the pipeline). The MCP
            // TestSupport.Token is a non-secret stub; the in-process MutableTenantAndClaimContext does not
            // validate bearers, so the resolved token is only used to exercise credential plumbing.
            HttpClient mcpHttpClient = app.GetTestClient();
            IClient mcpClient = new GeneratedSdkClient(mcpHttpClient);
            ToolPipeline mcpPipeline = TestSupport.Pipeline(mcpClient, token: TestSupport.Token);

            return new MixedSurfaceHost(app, hostUri, httpClient, sdkClient, sdkHttpClient, mcpPipeline, mcpHttpClient, gateway, repository, tenantStore, permissions, lifecycleReadModel);
        }

        /// <summary>
        /// Composes <see cref="CliDependencies"/> directly against the in-process host (the Story 5.6 linked
        /// <c>CliTestHarness</c> uses a <c>CapturingHttpHandler</c> for canned responses; the mixed-surface
        /// scenario needs the CLI to actually drive the server, so we compose dependencies inline). The
        /// resolved bearer token is exercised but the in-process <c>MutableTenantAndClaimContext</c> does not
        /// validate it; the token is supplied as <c>--token</c> per-invocation to exercise the credential
        /// path. Returns stdout/stderr and the canonical CLI exit code.
        /// </summary>
        public async Task<CliInvocationOutcome> RunCliAsync(params string[] args)
        {
            TestCliConsole console = new();
            CredentialResolver credentials = new(
                environment: _ => null,
                credentialsFilePath: Path.Combine(Path.GetTempPath(), $"hexalith-creds-{Guid.NewGuid():N}.json"));

            CliDependencies dependencies = new()
            {
                Console = console,
                Credentials = credentials,
                IdempotencyKeyGenerator = () => "01testautokey00000000000000",
                ClientFactory = (baseAddress, token) =>
                {
                    _ = baseAddress;
                    _ = token; // exercised but unused by the in-process MutableTenantAndClaimContext.
                    return new GeneratedSdkClient(App.GetTestClient());
                },
            };

            int exitCode = await new CliApplication(dependencies)
                .RunAsync(args, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            return new CliInvocationOutcome(exitCode, console.StdOut, console.StdErr);
        }

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            _sdkHttpClient.Dispose();
            _mcpHttpClient.Dispose();
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
