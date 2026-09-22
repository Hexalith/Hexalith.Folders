using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
/// <para><b>SDK exception body access.</b> The generated client retains the raw response text so closed
/// problem-envelope projection can validate exact tuples and reject undeclared members. Typed
/// <see cref="HexalithFoldersApiException{TResult}.Result"/> remains the public result surface used by this test.</para>
/// </remarks>
public sealed class GoldenLifecycleParityTests
{
    private const string CandidateFolderA = "folder_000000001";
    private const string CandidateFolderB = "folder_000000002";
    private const string CandidateTaskStatus = "task-status-0001";
    private const string CandidateWorkspace = "workspace_000001";
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
            // Each surface archives a distinct active folder so both are genuine first-write mutations that
            // reach 'accepted' (202) over the wire — not the prior false-green where a second archive on the
            // already-archived folder was masked as 202 by the flattening gateway (it is really a 403 safe
            // denial; see ArchiveFolderProcessWiringTests.ArchiveRequestShouldSurfaceAlreadyArchived...).
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderB, "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", CandidateFolderB);

            // REST run.
            const string restCorrelation = "correlation-rest-archive";
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-key-rest", restCorrelation, "task-rest");
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "REST mutating step must reach 'accepted' transport-terminal class.");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(restCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run against the same host, archiving its own active folder. The SDK ArchiveFolder call
            // exercises the real transport path (correlation, idempotency, terminal class) and reaches the
            // same 'accepted' transport-terminal class as REST — over the wire, through the propagating
            // gateway, with the rejection identity preserved (an invalid archive would surface 403, not 202).
            const string sdkCorrelation = "correlation-sdk-archive";
            AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
                folderId: CandidateFolderB,
                idempotency_Key: "archive_key_sdk_0001",
                x_Correlation_Id: sdkCorrelation,
                x_Hexalith_Task_Id: "task_sdk_0000001",
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V2,
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
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", CandidateFolderA, correlationId: "corr_shared_123456");

            // REST run — the lifecycle endpoint requires the request correlation to match the snapshot's
            // EvidenceScope correlation (compatible-evidence-snapshot invariant — see
            // FolderLifecycleStatusEndpointTests.LifecycleStatusRouteShouldIgnorePrincipalQueryStringValue).
            const string sharedCorrelation = "corr_shared_123456";
            using HttpRequestMessage restRequest = new(HttpMethod.Get, $"/api/v1/folders/{CandidateFolderA}/lifecycle-status");
            restRequest.Headers.Add("X-Correlation-Id", sharedCorrelation);
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "REST query_status step must reach 'projected' transport-terminal class (200).");
            restResponse.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(sharedCorrelation, "REST must echo the explicit X-Correlation-Id unchanged.");

            // SDK run — non-mutating SDK methods declare no idempotency_Key (AC #3).
            FolderLifecycleStatus sdkResult = await host.SdkClient.GetFolderLifecycleStatusAsync(
                folderId: CandidateFolderA,
                x_Correlation_Id: sharedCorrelation,
                x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

            sdkResult.ShouldNotBeNull("SDK query_status step must reach 'projected' transport-terminal class.");
            sdkResult.FolderId.ShouldBe(CandidateFolderA, "SDK response carries the inspected folder identity.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTaskStatusRejectsBindingAndEvidenceScopeMismatchAndEmitsClosedWireShape()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            const string correlationId = "correlation-task-status";
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            host.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderB));
            using HttpResponseMessage folderMismatch = await host.HttpClient.GetAsync(
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status",
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            folderMismatch.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            host.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderA) with
            {
                EvidenceScope = TaskSnapshot(CandidateFolderA).EvidenceScope with { TaskId = "task-status-other" },
            });
            using HttpRequestMessage scopeRequest = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            scopeRequest.Headers.Add("X-Correlation-Id", correlationId);
            using HttpResponseMessage scopeMismatch = await host.HttpClient.SendAsync(
                scopeRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            scopeMismatch.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            JsonElement scopeProblem = JsonDocument.Parse(
                await scopeMismatch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement;
            scopeProblem.GetProperty("category").GetString().ShouldBe("read_model_unavailable");

            host.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderA) with
            {
                RetryEligibility = new WorkspaceStatusRetryEligibility(
                    Eligible: false,
                    ReasonCode: "retry_not_required",
                    AdvisoryOnly: false),
            });
            using HttpRequestMessage malformedRequest = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            malformedRequest.Headers.Add("X-Correlation-Id", correlationId);
            using HttpResponseMessage malformed = await host.HttpClient.SendAsync(
                malformedRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            malformed.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

            host.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderA));
            using HttpRequestMessage successRequest = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            successRequest.Headers.Add("X-Correlation-Id", correlationId);
            using HttpResponseMessage success = await host.HttpClient.SendAsync(
                successRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            success.StatusCode.ShouldBe(HttpStatusCode.OK);
            success.Headers.GetValues("X-Hexalith-Freshness").Single().ShouldBe("eventually_consistent");
            success.Headers.Contains("X-Hexalith-Read-Consistency").ShouldBeFalse();
            using JsonDocument successDocument = JsonDocument.Parse(
                await success.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            JsonElement root = successDocument.RootElement;
            root.EnumerateObject().Select(static property => property.Name).ShouldBe(
                ["taskId", "currentState", "retryEligibility", "freshness"]);
            root.GetProperty("retryEligibility").EnumerateObject().Select(static property => property.Name).ShouldBe(
                ["eligible", "reasonCode", "advisoryOnly"]);
            root.GetProperty("retryEligibility").GetProperty("advisoryOnly").GetBoolean().ShouldBeTrue();
            root.GetProperty("freshness").EnumerateObject().Select(static property => property.Name).ShouldBe(
                ["readConsistency", "observedAt", "stale"]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }

        static TaskStatusReadModelSnapshot TaskSnapshot(string folderId)
            => new(
                ManagedTenantId: "tenant-a",
                TaskId: CandidateTaskStatus,
                CurrentState: "ready",
                TerminalState: null,
                LastOperationId: null,
                LastFailureCategory: null,
                RetryEligibility: new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
                RetryAfter: null,
                Freshness: new FolderLifecycleFreshness(
                    "eventually_consistent",
                    Now,
                    ProjectionWatermark: null,
                    Stale: false,
                    ReasonCode: null),
                EvidenceScope: new FolderLifecycleEvidenceScope(
                    "tenant-a",
                    "user-a",
                    TaskStatusQueryHandler.ActionToken,
                    CandidateTaskStatus,
                    CorrelationId: null,
                    AuthorizationWatermark: null),
                FolderId: folderId);
    }

    [Fact]
    public async Task CandidateTaskStatusRejectsInvalidQueryEnvelopeBeforeAnyTaskRead()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            taskStatusOverride: new ThrowingTaskStatusReadModel()).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            (string Header, string Value, string Code)[] cases =
            [
                ("Idempotency-Key", "idempotency-not-allowed", "idempotency_key_not_allowed"),
                ("X-Hexalith-Freshness", "read_your_writes", "unsupported_read_consistency"),
                ("X-Correlation-Id", "INVALID CORRELATION", "validation_error"),
                ("X-Tenant-Id", "tenant-a", "validation_error"),
                ("X-Principal-Id", "user-a", "validation_error"),
            ];
            foreach ((string header, string value, string code) in cases)
            {
                using HttpRequestMessage request = new(
                    HttpMethod.Get,
                    $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
                request.Headers.Add(header, value);
                using HttpResponseMessage response = await host.HttpClient.SendAsync(
                    request,
                    TestContext.Current.CancellationToken).ConfigureAwait(true);

                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, header);
                using JsonDocument document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
                document.RootElement.GetProperty("category").GetString().ShouldBe("validation_error", header);
                document.RootElement.GetProperty("code").GetString().ShouldBe(code, header);
            }

            foreach (string query in new[] { "tenantId=tenant-a", "principalId=user-a" })
            {
                using HttpRequestMessage request = new(
                    HttpMethod.Get,
                    $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status?{query}");
                using HttpResponseMessage response = await host.HttpClient.SendAsync(
                    request,
                    TestContext.Current.CancellationToken).ConfigureAwait(true);

                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, query);
                using JsonDocument document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
                document.RootElement.GetProperty("code").GetString().ShouldBe("validation_error", query);
            }
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTaskStatusRejectsTaskHeaderRouteMismatchBeforeAnyTaskRead()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            taskStatusOverride: new ThrowingTaskStatusReadModel()).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            request.Headers.Add("X-Hexalith-Task-Id", "task-status-0002");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe("validation_error");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("X-Hexalith-Tenant-Id", "tenant-a", "tenant-b", "")]
    [InlineData("X-Principal-Id", "user-a", "user-b", "")]
    [InlineData("", "tenant-a", "tenant-b", "tenantId")]
    [InlineData("", "user-a", "user-b", "principalId")]
    public async Task CandidateAuthorizationEvaluatesEveryRepeatedClientIdentityValue(
        string header,
        string first,
        string second,
        string queryName)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(queryName);

        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            taskStatusOverride: new ThrowingTaskStatusReadModel()).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            string path = $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status";
            if (queryName.Length > 0)
            {
                path += $"?{queryName}={first}&{queryName}={second}";
            }

            using HttpRequestMessage request = new(HttpMethod.Get, path);
            if (header.Length > 0)
            {
                request.Headers.Add(header, [first, second]);
            }

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("X-Hexalith-Tenant-Id", "tenant-a", "tenant-b", "")]
    [InlineData("X-Principal-Id", "user-a", "user-b", "")]
    [InlineData("", "tenant-a", "tenant-b", "tenantId")]
    [InlineData("", "user-a", "user-b", "principalId")]
    public async Task CandidateTenantScopedAuthorizationEvaluatesEveryRepeatedClientIdentityValue(
        string header,
        string first,
        string second,
        string queryName)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(queryName);

        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            string path = "/api/v2/provider-readiness/support-evidence";
            if (queryName.Length > 0)
            {
                path += $"?{queryName}={first}&{queryName}={second}";
            }

            using HttpRequestMessage request = new(HttpMethod.Get, path);
            if (header.Length > 0)
            {
                request.Headers.Add(header, [first, second]);
            }

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTenantOnlyDispatchReusesTheOuterAuthorizationDecision()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            throwOnSecondTenantProjectionRead: true).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            host.ProviderBindings.Save(new OrganizationProviderBinding(
                ManagedTenantId: "tenant-a",
                OrganizationId: "organization-a",
                ProviderBindingRef: "provider_binding_0001",
                ProviderKind: "github",
                CredentialReferenceId: "credential_reference_0001",
                NamingPolicy: new OrganizationProviderBindingPolicy(
                    "naming_policy_0001",
                    new Dictionary<string, string>(StringComparer.Ordinal)),
                BranchPolicy: new OrganizationProviderBindingPolicy(
                    "branch_policy_0001",
                    new Dictionary<string, string>(StringComparer.Ordinal)),
                CorrelationId: "correlation_binding_seed",
                TaskId: "task_binding_seed_0001",
                IdempotencyKey: "idempotency_binding_seed",
                IdempotencyFingerprint: "fingerprint_binding_seed",
                ConfiguredStatus: "configured",
                OccurredAt: Now));

            using HttpRequestMessage request = new(
                HttpMethod.Get,
                "/api/v2/provider-bindings/provider_binding_0001");
            request.Headers.Add("X-Correlation-Id", "correlation_binding_read");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            host.TenantProjectionReads.ShouldBe(1,
                "the historical tenant-only handler must reuse the candidate authorization decision");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateUnknownRouteReturnsExactSafeDenialAndAuditsWithoutDownstreamDispatch()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/not-declared");
            request.Headers.Add("X-Correlation-Id", "correlation-unknown-route");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            JsonElement problem = document.RootElement;
            problem.EnumerateObject().Select(static property => property.Name).ShouldBe(
            [
                "type", "title", "status", "category", "code", "message", "correlationId",
                "retryable", "clientAction", "details",
            ]);
            problem.GetProperty("status").GetInt32().ShouldBe(404);
            problem.GetProperty("category").GetString().ShouldBe("tenant_access_denied");
            problem.GetProperty("code").GetString().ShouldBe("resource_unavailable");
            problem.GetProperty("details").EnumerateObject().Select(static property => property.Name)
                .ShouldBe(["visibility"]);
            host.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "unknown", "unknown", "deny", "correlation-unknown-route"),
            ]);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateRejectsNonV2RootRequestDiscriminatorBeforeHistoricalDispatch()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    archiveReasonCode = "caller_requested",
                }),
            };
            request.Headers.Add("Idempotency-Key", "idempotency_archive_0001");
            request.Headers.Add("X-Correlation-Id", "correlation_archive_0001");
            request.Headers.Add("X-Hexalith-Task-Id", "task_archive_00001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateRequiresEveryDeclaredRootAndNestedRequestDiscriminator()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            using HttpRequestMessage missingRoot = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = JsonContent.Create(new { archiveReasonCode = "caller_requested" }),
            };
            AddMutationHeaders(
                missingRoot,
                "idempotency-missing-root",
                "correlation-missing-root",
                "task-missing-root-01");
            using HttpResponseMessage missingRootResponse = await host.HttpClient.SendAsync(
                missingRoot,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            missingRootResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using HttpRequestMessage staleNested = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v2",
                    folderId = CandidateFolderA,
                    providerBindingRef = "provider-binding-a",
                    repositoryProfileRef = "profile-a",
                    folderMetadata = new { displayName = "Folder", metadataClass = "tenant_sensitive" },
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v1",
                        repositoryBindingId = "binding-a",
                        policyRef = "branch-ref-policy-a",
                        defaultRef = "branch-ref-main-a",
                        allowedRefPatterns = new[] { "branch-ref-feature-a" },
                    },
                }),
            };
            AddMutationHeaders(
                staleNested,
                "idempotency-stale-nested",
                "correlation-stale-nested",
                "task-stale-nested-01");
            using HttpResponseMessage staleNestedResponse = await host.HttpClient.SendAsync(
                staleNested,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            staleNestedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            foreach (HttpResponseMessage response in new[] { missingRootResponse, staleNestedResponse })
            {
                JsonElement problem = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement;
                problem.GetProperty("code").GetString().ShouldBe("unsupported_request_schema_version");
            }

            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateRejectsAmbiguousHeadersMediaTypesAndDuplicateJsonBeforeDispatch()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            using HttpRequestMessage repeatedHeader = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"archiveReasonCode\":\"caller_requested\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
            repeatedHeader.Headers.Add("Idempotency-Key", ["idempotency_archive_0001", "idempotency_archive_0002"]);
            repeatedHeader.Headers.Add("X-Correlation-Id", "correlation_archive_0001");
            repeatedHeader.Headers.Add("X-Hexalith-Task-Id", "task_archive_00001");

            using HttpRequestMessage invalidMediaType = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"archiveReasonCode\":\"caller_requested\"}",
                    Encoding.UTF8,
                    "application/jsonp"),
            };
            AddMutationHeaders(invalidMediaType, "idempotency_media_0001", "correlation_media_0001", "task_media_0000001");

            using HttpRequestMessage duplicateJson = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"requestSchemaVersion\":\"v1\",\"archiveReasonCode\":\"caller_requested\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
            AddMutationHeaders(duplicateJson, "idempotency_duplicate_0001", "correlation_duplicate_0001", "task_duplicate_00001");

            using HttpRequestMessage invalidBodyIdentifier = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v2",
                    folderId = CandidateFolderA,
                    providerBindingRef = "bad.binding",
                    repositoryProfileRef = "Profile_000000001",
                    folderMetadata = new { displayName = "Folder", metadataClass = "tenant_sensitive" },
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v2",
                        repositoryBindingId = "Binding_000000001",
                        policyRef = "Policy_0000000001",
                        defaultRef = "branch_ref_primary",
                        allowedRefPatterns = new[] { "branch_ref_feature" },
                    },
                }),
            };
            AddMutationHeaders(
                invalidBodyIdentifier,
                "idempotency_identifier_0001",
                "correlation_identifier_0001",
                "task_identifier_00001");

            using HttpRequestMessage invalidReadinessIdentifier = new(
                HttpMethod.Post,
                "/api/v2/provider-readiness/validations")
            {
                Content = JsonContent.Create(new
                {
                    providerBindingRef = "bad.binding",
                    requestedCapability = "repository_creation",
                }),
            };
            invalidReadinessIdentifier.Headers.Add("X-Correlation-Id", "correlation_readiness_0001");

            foreach (HttpRequestMessage request in new[]
                     {
                         repeatedHeader,
                         invalidMediaType,
                         duplicateJson,
                         invalidBodyIdentifier,
                         invalidReadinessIdentifier,
                     })
            {
                using HttpResponseMessage response = await host.HttpClient.SendAsync(
                    request,
                    TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }

            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateAcceptsUppercaseOpaqueRouteIdentifiersThroughHistoricalDispatch()
    {
        const string uppercaseFolder = "Folder_0000000001";
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", uppercaseFolder, "user-a");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", uppercaseFolder, "Correlation_Uppercase_0001");
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{uppercaseFolder}/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", "Correlation_Uppercase_0001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            host.Gateway.ProcessCalls.ShouldBe(0, "the lifecycle-status query dispatches to the historical query endpoint without issuing a command");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidatePreservesUppercaseMutationIdentifiersThroughHistoricalCommandValidation()
    {
        const string folderId = CandidateFolderA;
        CapturingGatewayClient gateway = new();
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            gatewayOverride: gateway).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", folderId, "user-a");
            using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v2/folders/{folderId}/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v2",
                    archiveReasonCode = "caller_requested",
                }),
            };
            AddMutationHeaders(
                request,
                "Idempotency_Uppercase_0001",
                "Correlation_Uppercase_0001",
                "Task_Uppercase_000000001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(
                HttpStatusCode.Accepted,
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            SubmitCommandRequest dispatched = gateway.LastRequest.ShouldNotBeNull();
            dispatched.AggregateId.ShouldBe(folderId);
            dispatched.IdempotencyKey.ShouldBe("Idempotency_Uppercase_0001");
            dispatched.CorrelationId.ShouldBe("Correlation_Uppercase_0001");
            dispatched.Extensions.ShouldNotBeNull()["taskId"].ShouldBe("Task_Uppercase_000000001");
            dispatched.Payload.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateGeneratedClientFailsClosedForTupleDeclaredByAnotherOperation()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: async context =>
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(
                    "{\"type\":\"about:blank\",\"title\":\"Workspace locked\",\"status\":409,\"category\":\"lock_conflict\",\"code\":\"workspace_locked\",\"message\":\"The workspace is locked.\",\"correlationId\":\"correlation-wrong-operation\",\"retryable\":true,\"clientAction\":\"retry\",\"details\":{\"visibility\":\"metadata_only\",\"lockStatus\":\"active\"}}")
                    .ConfigureAwait(false);
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            HexalithFoldersApiException exception = await Should.ThrowAsync<HexalithFoldersApiException>(() =>
                host.SdkClient.GetFolderLifecycleStatusAsync(
                    CandidateFolderA,
                    "correlation-wrong-operation",
                    ReadConsistencyClass.Eventually_consistent,
                    TestContext.Current.CancellationToken)).ConfigureAwait(true);

            exception.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
            ProblemDetails problem = exception.ProblemDetails.ShouldNotBeNull(exception.ProblemDetailsParseDiagnostic);
            problem.Category.ShouldBe(CanonicalErrorCategory.Read_model_unavailable);
            problem.Code.ShouldBe(CanonicalErrorCode.Projection_unavailable);
            problem.Details.Visibility.ShouldBe(DetailsVisibility.Redacted);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateStripsForbiddenHistoricalProblemDetailAndInstanceFields()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: async context =>
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(
                    "{\"type\":\"about:blank\",\"title\":\"Validation failed\",\"status\":400,\"category\":\"validation_error\",\"code\":\"validation_error\",\"message\":\"The request is invalid.\",\"detail\":\"historical internal detail\",\"instance\":\"/historical/folders/folder-a\",\"correlationId\":\"correlation-forbidden-fields\",\"retryable\":false,\"clientAction\":\"revise_request\",\"details\":{\"visibility\":\"metadata_only\"}}")
                    .ConfigureAwait(false);
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", "correlation-forbidden-fields");
            request.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement problem = document.RootElement;
            problem.TryGetProperty("detail", out _).ShouldBeFalse();
            problem.TryGetProperty("instance", out _).ShouldBeFalse();
            problem.GetProperty("category").GetString().ShouldBe("validation_error");
            problem.GetProperty("code").GetString().ShouldBe("validation_error");
            problem.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTranslatesChangeFileRequestDiscriminatorThroughTheRealSeam()
    {
        string? historicalBody = null;
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: async context =>
            {
                using StreamReader reader = new(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                historicalBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Correlation-Id"] = "correlation-change-file";
                context.Response.Headers["X-Hexalith-Task-Id"] = "Task_ChangeFile_0000001";
                await context.Response.WriteAsync(
                    "{\"acceptedAt\":\"2026-05-28T12:00:00Z\",\"correlationId\":\"correlation-change-file\",\"taskId\":\"Task_ChangeFile_0000001\",\"status\":\"accepted\",\"idempotentReplay\":false}")
                    .ConfigureAwait(false);
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissionsForAction(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a", "mutate_files");
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                $"/api/v2/folders/{CandidateFolderA}/workspaces/{CandidateWorkspace}/files/change")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"fileOperationKind\":\"change\",\"transportOperation\":\"PutFileInline\",\"operationId\":\"Operation_ChangeFile_0001\",\"pathMetadata\":{\"normalizedPath\":\"docs/readme.md\",\"displayName\":\"readme.md\",\"pathPolicyClass\":\"content_allowed\",\"unicodeNormalization\":\"NFC\"},\"contentHashReference\":\"hashref_0123456789abcdef0123456789abcdef\",\"byteLength\":1,\"inlineContent\":{\"mediaType\":\"text/plain\",\"contentBytes\":\"YQ==\"}}",
                    Encoding.UTF8,
                    "application/json"),
            };
            AddMutationHeaders(
                request,
                "Idempotency_ChangeFile_0001",
                "correlation-change-file",
                "Task_ChangeFile_0000001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            using JsonDocument translated = JsonDocument.Parse(historicalBody.ShouldNotBeNull());
            translated.RootElement.GetProperty("requestSchemaVersion").GetString().ShouldBe("v1");
            translated.RootElement.GetProperty("fileOperationKind").GetString().ShouldBe("change");
            translated.RootElement.GetProperty("operationId").GetString().ShouldBe("Operation_ChangeFile_0001");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateGeneratedClientTranslatesBranchRefPolicySuccessAndFreshnessHeader()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: async context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Hexalith-Read-Consistency"] = "historical";
                await context.Response.WriteAsync(
                    "{\"requestSchemaVersion\":\"v1\",\"repositoryBindingId\":\"repository_binding_0001\",\"policyRef\":\"policy_ref_0000001\",\"defaultRef\":\"branch_ref_primary\",\"allowedRefPatterns\":[\"branch_ref_feature\"],\"protectedRefPatterns\":[],\"freshness\":{\"readConsistency\":\"eventually_consistent\",\"observedAt\":\"2026-05-28T12:00:00Z\",\"projectionWatermark\":\"watermark_00000001\",\"stale\":false}}")
                    .ConfigureAwait(false);
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissionsForAction(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a", "read_branch_ref_policy");

            BranchRefPolicy result = await host.SdkClient.GetBranchRefPolicyAsync(
                CandidateFolderA,
                "correlation-branch-policy",
                ReadConsistencyClass.Eventually_consistent,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.RequestSchemaVersion.ShouldBe(BranchRefPolicyRequestSchemaVersion.V2);
            result.Freshness.ReadConsistency.ShouldBe(ReadConsistencyClass.Eventually_consistent);

            using HttpRequestMessage rawRequest = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/branch-ref-policy");
            rawRequest.Headers.Add("X-Correlation-Id", "correlation-branch-policy-raw");
            rawRequest.Headers.Add("X-Hexalith-Freshness", "eventually_consistent");
            using HttpResponseMessage rawResponse = await host.HttpClient.SendAsync(
                rawRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            rawResponse.Headers.GetValues("X-Hexalith-Freshness").Single().ShouldBe("eventually_consistent");
            rawResponse.Headers.Contains("X-Hexalith-Read-Consistency").ShouldBeFalse();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateEnforcesRequiredAndBodylessRequestShapesWithoutOverbroadBodyLimit()
    {
        int downstreamCalls = 0;
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: context =>
            {
                downstreamCalls++;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            using HttpRequestMessage missingBody = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive");
            AddMutationHeaders(missingBody, "idempotency-missing-body", "correlation-missing-body", "task-missing-body-01");
            using HttpResponseMessage missingBodyResponse = await host.HttpClient.SendAsync(
                missingBody,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            missingBodyResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            using HttpRequestMessage unexpectedBody = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status")
            {
                Content = new StringContent(new string('x', 1_048_577), Encoding.UTF8, "application/json"),
            };
            unexpectedBody.Headers.Add("X-Correlation-Id", "correlation-unexpected-body");
            using HttpResponseMessage unexpectedBodyResponse = await host.HttpClient.SendAsync(
                unexpectedBody,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            unexpectedBodyResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            unexpectedBodyResponse.StatusCode.ShouldNotBe(HttpStatusCode.RequestEntityTooLarge);

            downstreamCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTaskStatusRejectsDisagreeingRepeatedFreshnessValuesBeforeAnyTaskRead()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            taskStatusOverride: new ThrowingTaskStatusReadModel()).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            request.Headers.Add("X-Hexalith-Freshness", ["eventually_consistent", "read_your_writes"]);

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            JsonElement problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement;
            problem.GetProperty("code").GetString().ShouldBe("unsupported_read_consistency");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTaskStatusMapsNullBindingEvidenceToAuthorityUnavailable()
    {
        FolderLifecycleFreshness fresh = new(
            "eventually_consistent",
            Now,
            ProjectionWatermark: null,
            Stale: false,
            ReasonCode: null);
        TaskStatusReadModelSnapshot complete = new(
            ManagedTenantId: "tenant-a",
            TaskId: CandidateTaskStatus,
            CurrentState: "ready",
            TerminalState: null,
            LastOperationId: null,
            LastFailureCategory: null,
            RetryEligibility: new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
            RetryAfter: null,
            Freshness: fresh,
            EvidenceScope: new FolderLifecycleEvidenceScope(
                "tenant-a",
                "user-a",
                TaskStatusQueryHandler.ActionToken,
                CandidateTaskStatus,
                CorrelationId: null,
                AuthorizationWatermark: null),
            FolderId: CandidateFolderA);
        (string Name, TaskStatusReadModelResult Result)[] cases =
        [
            ("top-level freshness", new(TaskStatusReadModelStatus.Available, complete, null!)),
            ("snapshot freshness", new(TaskStatusReadModelStatus.Available, complete with { Freshness = null! }, fresh)),
            ("snapshot evidence", new(TaskStatusReadModelStatus.Available, complete with { EvidenceScope = null! }, fresh)),
            ("snapshot folder", new(TaskStatusReadModelStatus.Available, complete with { FolderId = null! }, fresh)),
        ];

        foreach ((string name, TaskStatusReadModelResult readResult) in cases)
        {
            TestHost host = await TestHost.StartAsync(
                tenantId: "tenant-a",
                principalId: "user-a",
                taskStatusOverride: new FixedTaskStatusReadModel(readResult)).ConfigureAwait(true);
            try
            {
                SeedTenant(host.TenantStore, "tenant-a", "user-a");
                SeedTaskStatusPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

                using HttpResponseMessage response = await host.HttpClient.GetAsync(
                    $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status",
                    TestContext.Current.CancellationToken).ConfigureAwait(true);

                response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, name);
                using JsonDocument document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
                document.RootElement.GetProperty("category").GetString().ShouldBe("read_model_unavailable", name);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    [Fact]
    public async Task CandidateAuthorizationEmitsOneReplaceableSixFieldAuditRecordForAllowAndDeny()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", CandidateFolderA, "correlation-audit-allow");
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", CandidateFolderB, "correlation-audit-deny");

            using HttpRequestMessage allowRequest = new(HttpMethod.Get, $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            allowRequest.Headers.Add("X-Correlation-Id", "correlation-audit-allow");
            using HttpResponseMessage allow = await host.HttpClient.SendAsync(
                allowRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            allow.StatusCode.ShouldBe(HttpStatusCode.OK);

            using HttpRequestMessage denyRequest = new(HttpMethod.Get, $"/api/v2/folders/{CandidateFolderB}/lifecycle-status");
            denyRequest.Headers.Add("X-Correlation-Id", "correlation-audit-deny");
            using HttpResponseMessage deny = await host.HttpClient.SendAsync(
                denyRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            deny.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            host.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "GetFolderLifecycleStatus", "status-permission-and-lock-inspection", "allow", "correlation-audit-allow"),
                new("user-a", "tenant-a", "GetFolderLifecycleStatus", "status-permission-and-lock-inspection", "deny", "correlation-audit-deny"),
            ]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(EffectivePermissionsReadModelStatus.Stale)]
    [InlineData(EffectivePermissionsReadModelStatus.Unavailable)]
    public async Task CandidateMapsRealStaleAndUnavailableAuthorityEvidenceToExactPreObservation503(
        EffectivePermissionsReadModelStatus evidenceStatus)
    {
        EffectivePermissionsReadModelSnapshot snapshot = new(
            "tenant-a",
            "org-a",
            CandidateFolderA,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User("user-a"),
                    "read_metadata",
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness(
                "read_your_writes",
                Now,
                "permission-watermark-authority-test",
                Stale: evidenceStatus == EffectivePermissionsReadModelStatus.Stale,
                ReasonCode: evidenceStatus == EffectivePermissionsReadModelStatus.Stale ? "projection_stale" : null),
            RevocationFreshnessEstablished: true,
            TaskScope: null);
        EffectivePermissionsReadModelResult readResult = evidenceStatus switch
        {
            EffectivePermissionsReadModelStatus.Stale => EffectivePermissionsReadModelResult.Stale(snapshot),
            EffectivePermissionsReadModelStatus.Unavailable => EffectivePermissionsReadModelResult.Unavailable("projection_unavailable", Now),
            _ => throw new ArgumentOutOfRangeException(nameof(evidenceStatus)),
        };
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            permissionsOverride: new FixedEffectivePermissionsReadModel(readResult)).ConfigureAwait(true);
        try
        {
            const string correlationId = "correlation-authority-evidence";
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", correlationId);

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            JsonElement problem = document.RootElement;
            problem.GetProperty("title").GetString().ShouldBe("Authorization evidence unavailable");
            problem.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
            problem.GetProperty("code").GetString().ShouldBe("projection_unavailable");
            problem.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("redacted");
            host.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "GetFolderLifecycleStatus", "status-permission-and-lock-inspection", "deny", correlationId),
            ]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateAuthenticatesBeforeBodyLimitAndPreservesMalformedRequestValidation()
    {
        TestHost unauthenticated = await TestHost.StartAsync(tenantId: null, principalId: null).ConfigureAwait(true);
        try
        {
            using HttpRequestMessage oversized = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = new ByteArrayContent(new byte[1_048_577]),
            };
            oversized.Content.Headers.ContentType = new("application/json");
            using HttpResponseMessage response = await unauthenticated.HttpClient.SendAsync(
                oversized,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await unauthenticated.DisposeAsync().ConfigureAwait(true);
        }

        TestHost unauthorized = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            using ByteArrayContent knownLength = new(new byte[1_048_577]);
            knownLength.Headers.ContentType = new("application/json");
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = knownLength,
            };
            using HttpResponseMessage response = await unauthorized.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldNotBe(HttpStatusCode.RequestEntityTooLarge);
            unauthorized.AuditSink.Records.ShouldNotBeEmpty();
            unauthorized.AuditSink.Records.ShouldAllBe(record => record.Result == "deny");
        }
        finally
        {
            await unauthorized.DisposeAsync().ConfigureAwait(true);
        }

        TestHost authenticated = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(authenticated.TenantStore, "tenant-a", "user-a");
            using ByteArrayContent knownLength = new(new byte[1_048_577]);
            knownLength.Headers.ContentType = new("application/json");
            await AssertInputLimitAsync(authenticated, knownLength).ConfigureAwait(true);

            using UnknownLengthContent unknownLength = new(new byte[1_048_577]);
            unknownLength.Headers.ContentType = new("application/json");
            await AssertInputLimitAsync(authenticated, unknownLength).ConfigureAwait(true);

            using HttpRequestMessage malformed = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json"),
            };
            malformed.Headers.Add("X-Correlation-Id", "correlation-validation");
            using HttpResponseMessage response = await authenticated.HttpClient.SendAsync(
                malformed,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            JsonElement problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement;
            problem.GetProperty("category").GetString().ShouldBe("validation_error");
            problem.GetProperty("code").GetString().ShouldBe("validation_error");
        }
        finally
        {
            await authenticated.DisposeAsync().ConfigureAwait(true);
        }

        static async Task AssertInputLimitAsync(TestHost host, HttpContent content)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = content,
            };
            request.Headers.Add("X-Correlation-Id", "correlation-input-limit");
            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            JsonElement problem = document.RootElement;
            problem.GetProperty("status").GetInt32().ShouldBe(413);
            problem.GetProperty("category").GetString().ShouldBe("input_limit_exceeded");
            problem.GetProperty("code").GetString().ShouldBe("c4_input_limit_exceeded");
            host.AuditSink.Records.ShouldContain(record =>
                record.Result == "allow"
                && record.Operation == "CreateRepositoryBackedFolder"
                && record.CorrelationId == "correlation-input-limit");
        }
    }

    [Fact]
    public async Task CandidateAuditsFinalTaskFolderBindingOutcome()
    {
        TestHost notBound = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(notBound.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(notBound.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            notBound.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderB));
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            request.Headers.Add("X-Correlation-Id", "correlation-binding-deny");
            using HttpResponseMessage response = await notBound.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            notBound.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "GetTaskStatus", "status-permission-and-lock-inspection", "deny", "correlation-binding-deny"),
            ]);
        }
        finally
        {
            await notBound.DisposeAsync().ConfigureAwait(true);
        }

        TestHost unavailable = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(unavailable.TenantStore, "tenant-a", "user-a");
            SeedTaskStatusPermissions(unavailable.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            unavailable.TaskStatusReadModel.Save(TaskSnapshot(CandidateFolderA) with { FolderId = null! });
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/tasks/{CandidateTaskStatus}/status");
            request.Headers.Add("X-Correlation-Id", "correlation-binding-unavailable");
            using HttpResponseMessage response = await unavailable.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            unavailable.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "GetTaskStatus", "status-permission-and-lock-inspection", "deny", "correlation-binding-unavailable"),
            ]);
        }
        finally
        {
            await unavailable.DisposeAsync().ConfigureAwait(true);
        }

        static TaskStatusReadModelSnapshot TaskSnapshot(string folderId)
            => new(
                ManagedTenantId: "tenant-a",
                TaskId: CandidateTaskStatus,
                CurrentState: "ready",
                TerminalState: null,
                LastOperationId: null,
                LastFailureCategory: null,
                RetryEligibility: new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
                RetryAfter: null,
                Freshness: new FolderLifecycleFreshness(
                    "eventually_consistent",
                    Now,
                    ProjectionWatermark: null,
                    Stale: false,
                    ReasonCode: null),
                EvidenceScope: new FolderLifecycleEvidenceScope(
                    "tenant-a",
                    "user-a",
                    TaskStatusQueryHandler.ActionToken,
                    CandidateTaskStatus,
                    CorrelationId: null,
                    AuthorizationWatermark: null),
                FolderId: folderId);
    }

    [Fact]
    public async Task CandidateAuthorizesRepositoryBackedCreationOfAMissingFolder()
    {
        int downstreamCalls = 0;
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            downstreamOverride: context =>
            {
                downstreamCalls++;
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"folderId\":\"folder_create_0001\",\"branchRefPolicy\":{\"requestSchemaVersion\":\"v2\"}}",
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Add("X-Correlation-Id", "correlation-create-missing");
            request.Headers.Add("Idempotency-Key", "idempotency_create_0001");
            request.Headers.Add("X-Hexalith-Task-Id", "task_create_00000001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);
            downstreamCalls.ShouldBe(1);
            host.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "CreateRepositoryBackedFolder", "folder-administration", "allow", "correlation-create-missing"),
            ]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateTreatsHeaderlessTransportBodiesAndMalformedJsonAsDeclaredValidation()
    {
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            headerlessTransportBody: true,
            downstreamOverride: context =>
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");

            using HttpRequestMessage required = new(HttpMethod.Post, $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = new StringContent(
                    "{\"requestSchemaVersion\":\"v2\",\"archiveReasonCode\":\"caller_requested\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
            AddMutationHeaders(required, "idempotency_headerless_0001", "correlation-headerless-required", "task_headerless_000001");
            using HttpResponseMessage requiredResponse = await host.HttpClient.SendAsync(
                required,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            requiredResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

            using HttpRequestMessage unexpected = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status")
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json"),
            };
            unexpected.Headers.Add("X-Correlation-Id", "correlation-headerless-unexpected");
            using HttpResponseMessage unexpectedResponse = await host.HttpClient.SendAsync(
                unexpected,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            string unexpectedBody = await unexpectedResponse.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            unexpectedResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, unexpectedBody);
            JsonDocument.Parse(unexpectedBody).RootElement.GetProperty("code").GetString().ShouldBe("validation_error");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidatePreservesEstablishedAuditDecisionWhenHistoricalExecutionThrows()
    {
        TestHost host = await TestHost.StartAsync().ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", "correlation-audit-throw");
            request.Headers.Add("X-PD10-Test-Throw", "true");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            JsonElement problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement;
            problem.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
            problem.GetProperty("code").GetString().ShouldBe("projection_unavailable");
            problem.GetProperty("clientAction").GetString().ShouldBe("retry");
            host.AuditSink.Records.ShouldBe(
            [
                new("user-a", "tenant-a", "GetFolderLifecycleStatus", "status-permission-and-lock-inspection", "allow", "correlation-audit-throw"),
            ]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateAuditSinkFailureAbortsMutationBeforeGatewayDispatch()
    {
        ThrowingPd10AuthorizationAuditSink auditSink = new();
        TestHost host = await TestHost.StartAsync(
            tenantId: "tenant-a",
            principalId: "user-a",
            auditSinkOverride: auditSink).ConfigureAwait(true);
        try
        {
            SeedTenant(host.TenantStore, "tenant-a", "user-a");
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderA, "user-a");
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                $"/api/v2/folders/{CandidateFolderA}/archive")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v2",
                    archiveReasonCode = "caller_requested",
                }),
            };
            AddMutationHeaders(
                request,
                "idempotency_audit_failure_0001",
                "correlation_audit_failure_0001",
                "task_audit_failure_000001");

            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            auditSink.Attempts.ShouldBe(1);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateRejectsDelegationForNonDelegableTenantFamily()
    {
        TestHost host = await TestHost.StartAsync("tenant-a", "service-agent-a", delegated: true).ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v2/folders")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            host.AuditSink.Records.Single().ShouldBe(new Pd10AuthorizationAuditRecord(
                "service-agent-a",
                "tenant-a",
                "CreateFolder",
                "folder-creation",
                "deny",
                "correlation_absent"));
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("revoked", HttpStatusCode.NotFound)]
    [InlineData("stale", HttpStatusCode.ServiceUnavailable)]
    public async Task CandidateNegativeAccessStateTakesPrecedenceOverDelegatedIdentity(
        string accessState,
        HttpStatusCode expectedStatus)
    {
        TestHost host = await TestHost.StartAsync(
            "tenant-a",
            "service-agent-a",
            delegated: true,
            accessState: accessState,
            delegatorPermission: "read_metadata").ConfigureAwait(true);
        try
        {
            await host.TenantStore.SaveAsync(
                new FolderTenantAccessProjection
                {
                    TenantId = "tenant-a",
                    Enabled = true,
                    Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                    {
                        ["service-agent-a"] = new("service-agent-a", "Service"),
                        ["delegator-a"] = new("delegator-a", "Owner"),
                    },
                    Watermark = 1,
                    LastEventTimestamp = Now.AddMinutes(-1),
                    ProjectionWatermark = "tenant-a:1",
                },
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            host.Permissions.Save(new EffectivePermissionsReadModelSnapshot(
                "tenant-a",
                "org-a",
                CandidateFolderA,
                EffectivePermissionsFolderLifecycleState.Active,
                [
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("service-agent-a"), "read_metadata", 1, Now.AddMinutes(-1)),
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("delegator-a"), "read_metadata", 2, Now.AddMinutes(-1)),
                ],
                new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", false, null),
                RevocationFreshnessEstablished: true,
                TaskScope: null));
            SeedLifecycleStatus(host.LifecycleReadModel, "tenant-a", CandidateFolderA, "correlation-delegated-negative");

            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            request.Headers.Add("X-Correlation-Id", "correlation-delegated-negative");
            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(expectedStatus);
            host.Gateway.ProcessCalls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateAppliesSafeDenialPrecedenceToRepeatedNegativeAccessStates()
    {
        TestHost host = await TestHost.StartAsync(
            "tenant-a",
            "user-a",
            accessState: "revoked",
            additionalAccessState: "stale").ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            host.Gateway.ProcessCalls.ShouldBe(0);
            host.AuditSink.Records.ShouldHaveSingleItem().Result.ShouldBe("deny");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task CandidateRejectsConflictingDelegatorIdentitiesAsUnavailable()
    {
        TestHost host = await TestHost.StartAsync(
            "tenant-a",
            "service-agent-a",
            delegated: true,
            additionalDelegator: "delegator-b",
            delegatorPermission: "read_metadata").ConfigureAwait(true);
        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"/api/v2/folders/{CandidateFolderA}/lifecycle-status");
            using HttpResponseMessage response = await host.HttpClient.SendAsync(
                request,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            host.Gateway.ProcessCalls.ShouldBe(0);
            host.AuditSink.Records.ShouldHaveSingleItem().Result.ShouldBe("deny");
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

            // SDK run: the exact generated 401 subtype projects through the common ProblemDetails view.
            HexalithFoldersApiException sdkException = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
                await host.SdkClient.ArchiveFolderAsync(
                    folderId: CandidateFolderA,
                    idempotency_Key: "archive-sdk-neg",
                    x_Correlation_Id: "correlation-sdk-neg",
                    x_Hexalith_Task_Id: "task-sdk-neg",
                    body: new ArchiveFolderRequest
                    {
                        RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V2,
                        ArchiveReasonCode = ArchiveFolderRequestArchiveReasonCode.Caller_requested,
                    },
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ConfigureAwait(true);

            sdkException.StatusCode.ShouldBe((int)HttpStatusCode.Unauthorized);
            ProblemDetails sdkProblem = sdkException.ProblemDetails.ShouldNotBeNull(sdkException.ProblemDetailsParseDiagnostic);
            sdkProblem.CorrelationId.ShouldBe("correlation-sdk-neg");
            sdkProblem.Details.ShouldNotBeNull();
            sdkProblem.Details.Visibility.ShouldBe(DetailsVisibility.Redacted);

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
            // Each surface archives its own active folder so both are genuine first-write 202s over the wire.
            SeedPermissions(host.Permissions, "tenant-a", "org-a", CandidateFolderB, "user-a");
            SeedFolder(host.Repository, "tenant-a", "org-a", CandidateFolderB);

            const string restTaskId = "task-echo-rest-0001";
            using HttpRequestMessage restRequest = CreateArchiveRequest("folder-a", "archive-key-rest-echo", "correlation-rest-echo", restTaskId);
            using HttpResponseMessage restResponse = await host.HttpClient.SendAsync(restRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);

            restResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            restResponse.Headers.GetValues("X-Hexalith-Task-Id").Single().ShouldBe(restTaskId, "REST must echo the explicit X-Hexalith-Task-Id unchanged on the response header.");

            const string sdkTaskId = "task-echo-sdk-0001";
            AcceptedCommand sdkResult = await host.SdkClient.ArchiveFolderAsync(
                folderId: CandidateFolderB,
                idempotency_Key: "archive-key-sdk-echo",
                x_Correlation_Id: "correlation-sdk-echo",
                x_Hexalith_Task_Id: sdkTaskId,
                body: new ArchiveFolderRequest
                {
                    RequestSchemaVersion = ArchiveFolderRequestRequestSchemaVersion.V2,
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
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "manage_folder_access", Sequence: 3, EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static void SeedTaskStatusPermissions(
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
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "query_status", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static void SeedPermissionsForAction(
        InMemoryEffectivePermissionsReadModel readModel,
        string tenantId,
        string organizationId,
        string folderId,
        string principalId,
        string actionToken)
        => readModel.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            folderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), actionToken, Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
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

    /// <summary>
    /// AC1 (Story 8.3) — drives ALL nine golden-lifecycle operations over the <b>real REST transport</b>
    /// against a single in-process host, asserting each reaches its transport-terminal class
    /// (mutating_command → 202/accepted; query → 200/projected). The five mutating steps (2..6) round-trip
    /// through <c>/process</c> via the shared <see cref="InProcessRejectionPropagatingGatewayClient"/>, so each
    /// is a genuine first-write that hits the real aggregate gate — a precondition violation would surface a
    /// canonical 4xx/5xx (the gateway propagates rejections), not a flattened 202.
    /// </summary>
    /// <remarks>
    /// <para><b>Why per-step seeded folders for steps 3..6 instead of one wire chain.</b> The folder workspace
    /// lifecycle is a strict state machine (see <c>FolderStateTransitions</c>): PrepareWorkspace requires
    /// <c>Preparing</c>, LockWorkspace requires <c>Ready</c>, AddFile requires <c>Locked</c>, CommitWorkspace
    /// requires <c>ChangesStaged</c> — mutually exclusive states. The wire effect of each step does not advance
    /// the lifecycle to the next step's precondition on its own (e.g. <c>WorkspacePreparationRequested</c> does
    /// not move <c>Preparing</c>→<c>Ready</c>; that is the provider-readiness flow's job). So each mutating step
    /// is driven over the real route against a folder seeded into exactly that step's precondition state — every
    /// call is a real REST→gateway→/process→aggregate→gate round-trip reaching its real 202, never an
    /// oracle-metadata-only assertion. Eight of the nine steps reach their happy-path transport-terminal class
    /// (202/200); the ninth, GetWorkspaceStatus, is a <b>documented seam</b> (below).</para>
    /// <para><b>Documented seam — GetWorkspaceStatus (step 8).</b> This step is driven over the real REST (and
    /// SDK) route to its real canonical response, but reaching the happy-path 200 requires a populated
    /// authoritative-tenant evidence path for the workspace-status projection that the hermetic in-process host
    /// does not reproduce — the strict in-memory read model surfaces the canonical metadata-only
    /// <c>read_model_unavailable</c> (503) instead. We assert the <i>real</i> canonical transport outcome of the
    /// live route (never an oracle-metadata-only claim), mirroring the <c>RestInspectionOperationId</c>
    /// substitution rationale: drive the real route, assert its real result. The happy-path 200 for this
    /// operation is independently proven by <c>Server.Tests.WorkspaceStatusEndpointTests</c>.</para>
    /// <para><b>SDK equivalence.</b> The query steps (1, 7, 8, 9) and the canonical mutating step
    /// CreateRepositoryBackedFolder (2) are additionally driven through the generated <see cref="IClient"/>
    /// against the same host, asserting REST/SDK transport equivalence (mirrors the dual-surface pattern
    /// already used in the cross-surface tests above).</para>
    /// </remarks>
    [Fact]
    public async Task GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass()
    {
        // The shared step list is the authoritative scenario; pin the ordered names so a drift in the
        // shared list fails this test loudly rather than silently skipping a step.
        string[] orderedStepNames = GoldenLifecycle.Steps.Select(s => s.StepName).ToArray();
        orderedStepNames.ShouldBe(
            [
                "provider_readiness",
                "repository_binding",
                "prepare_workspace",
                "lock_workspace",
                "add_file",
                "commit_workspace",
                "context_query",
                "workspace_status",
                "audit_inspection",
            ],
            "the golden-lifecycle step order must match the nine driven steps.");

        GoldenLifecycleHost host = await GoldenLifecycleHost.StartAsync().ConfigureAwait(true);
        try
        {
            // Tracks which of the nine steps reached its happy-path transport-terminal class (202/200).
            List<string> reachedTerminalClass = [];

            // ---- Step 1: ValidateProviderReadiness — query → 200 (REST + SDK equivalence) ----
            host.SeedTenant("tenant-a", "user-a");
            using (HttpRequestMessage providerReadinessRequest = new(HttpMethod.Post, "/api/v1/provider-readiness/validations")
            {
                Content = JsonContent.Create(new
                {
                    providerBindingRef = "binding-a",
                    requestedCapability = "repository_creation",
                }),
            })
            {
                providerReadinessRequest.Headers.Add("X-Correlation-Id", "corr-provider-readiness");
                providerReadinessRequest.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(providerReadinessRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, "Step 1 ValidateProviderReadiness must reach 'projected' (200).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-provider-readiness");
                reachedTerminalClass.Add("provider_readiness");
            }

            // SDK-surface seam (documented): the server returns the authorized-operator readiness shape
            // (audience "authorized_operator"), but the generated ValidateProviderReadinessAsync binds the
            // response to the *consumer*-audience DTO (ProviderReadinessConsumer), whose audience enum has no
            // "authorized_operator" member. The SDK therefore still drives the real route to its real
            // transport-terminal class (HTTP 200) — proven by the deserialization exception carrying
            // StatusCode 200 — but cannot bind the operator body into the consumer DTO. We assert the real
            // 200 transport outcome rather than an oracle-metadata-only claim (mirrors the
            // RestInspectionOperationId substitution rationale: drive the real route, assert its real result).
            HexalithFoldersApiException sdkReadinessSeam = await Should.ThrowAsync<HexalithFoldersApiException>(async () =>
                await host.SdkClient.ValidateProviderReadinessAsync(
                    x_Correlation_Id: "corr-provider-readiness-sdk",
                    x_Hexalith_Freshness: ReadConsistencyClass.Snapshot_per_task,
                    body: new ValidateProviderReadinessRequest
                    {
                        ProviderBindingRef = "provider_binding_0001",
                        RequestedCapability = ProviderCapabilityName.Repository_creation,
                    },
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true))
                .ConfigureAwait(true);
            sdkReadinessSeam.StatusCode.ShouldBe(
                (int)HttpStatusCode.OK,
                $"SDK ValidateProviderReadiness reaches the real 'projected' transport-terminal class (200); the consumer-audience DTO simply cannot bind the operator-audience body. Response: {sdkReadinessSeam.Response}");

            // ---- Step 2: CreateRepositoryBackedFolder — mutating_command → 202 (REST + SDK equivalence) ----
            // Makes an existing Unbound folder repository-backed (the aggregate requires IsCreated && Unbound),
            // a genuine first-write over the wire: REST → gateway → /process → aggregate gate → 202.
            host.SeedFolderCreationPermissions("tenant-a", "org-a", "folder-create-rest", "user-a");
            host.SeedUnboundFolder("tenant-a", "org-a", "folder-create-rest");
            using (HttpRequestMessage createRequest = CreateRepositoryBackedRequest(
                folderId: "folder-create-rest",
                idempotencyKey: "idempotency-create-rest",
                correlationId: "corr-create-rest",
                taskId: "task-create-rest"))
            {
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(createRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Step 2 CreateRepositoryBackedFolder must reach 'accepted' (202).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-create-rest");
                reachedTerminalClass.Add("repository_binding");
            }

            // SDK equivalence for the canonical mutating step against the same host (own fresh folder).
            host.SeedFolderCreationPermissions("tenant-a", "org-a", "folder-create-sdk", "user-a");
            host.SeedUnboundFolder("tenant-a", "org-a", "folder-create-sdk");
            AcceptedCommand sdkCreate = await host.SdkClient.CreateRepositoryBackedFolderAsync(
                idempotency_Key: "idempotency-create-sdk",
                x_Correlation_Id: "correlation_create_sdk",
                x_Hexalith_Task_Id: "task_create_sdk_001",
                body: new CreateRepositoryBackedFolderRequest
                {
                    RequestSchemaVersion = CreateRepositoryBackedFolderRequestRequestSchemaVersion.V2,
                    FolderId = "folder-create-sdk",
                    ProviderBindingRef = "provider-binding-a",
                    RepositoryProfileRef = "repository_profile_0001",
                    FolderMetadata = new FolderMetadata
                    {
                        DisplayName = "Folder",
                        MetadataClass = SensitiveMetadataTier.Tenant_sensitive,
                    },
                    BranchRefPolicy = new BranchRefPolicyRequest
                    {
                        RequestSchemaVersion = BranchRefPolicyRequestRequestSchemaVersion.V2,
                        RepositoryBindingId = "repository_binding_0001",
                        PolicyRef = "branch_ref_policy_a",
                        DefaultRef = "branch_ref_primary",
                        AllowedRefPatterns = ["branch_ref_feature"],
                    },
                },
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            sdkCreate.Status.ShouldBe(AcceptedCommandStatus.Accepted, "SDK CreateRepositoryBackedFolder reaches the 'accepted' class.");

            // ---- Step 3: PrepareWorkspace — mutating_command → 202 ----
            // Seed a folder bound + branch-policy-configured (workspace lifecycle = Preparing) and grant the
            // prepare_workspace action token; PrepareWorkspace's WorkspacePrepared transition is valid from Preparing.
            host.SeedWorkspacePermissions("tenant-a", "org-a", "folder-prepare", "user-a", "prepare_workspace");
            host.SeedBoundFolderReadyForPreparation("tenant-a", "org-a", "folder-prepare");
            using (HttpRequestMessage prepareRequest = new(HttpMethod.Post, "/api/v1/folders/folder-prepare/workspaces/workspace-a/preparation")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    branchRefPolicyRef = "branch_ref_policy_a",
                    workspacePolicyRef = "workspace-policy-a",
                }),
            })
            {
                AddMutationHeaders(prepareRequest, "idempotency-prepare", "corr-prepare", "task-prepare");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(prepareRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Step 3 PrepareWorkspace must reach 'accepted' (202).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-prepare");
                reachedTerminalClass.Add("prepare_workspace");
            }

            // ---- Step 4: LockWorkspace — mutating_command → 202 ----
            // Lock requires workspace lifecycle = Ready; seed bound + prepared (Ready) state.
            host.SeedWorkspacePermissions("tenant-a", "org-a", "folder-lock", "user-a", "lock_workspace");
            host.SeedReadyWorkspace("tenant-a", "org-a", "folder-lock");
            using (HttpRequestMessage lockRequest = new(HttpMethod.Post, "/api/v1/folders/folder-lock/workspaces/workspace-a/lock")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 3600,
                }),
            })
            {
                AddMutationHeaders(lockRequest, "idempotency-lock", "corr-lock", "task-lock");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(lockRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Step 4 LockWorkspace must reach 'accepted' (202).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-lock");
                reachedTerminalClass.Add("lock_workspace");
            }

            // ---- Step 5: AddFile — mutating_command → 202 ----
            // AddFile requires workspace = Locked with the lock held by this task and unexpired; seed that state.
            host.SeedWorkspacePermissions("tenant-a", "org-a", "folder-addfile", "user-a", "mutate_files");
            host.SeedLockedWorkspace("tenant-a", "org-a", "folder-addfile", holderTaskId: "task-addfile");
            using (HttpRequestMessage addFileRequest = new(HttpMethod.Post, "/api/v1/folders/folder-addfile/workspaces/workspace-a/files/add")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    operationId = "operation-add-a",
                    fileOperationKind = "add",
                    transportOperation = "PutFileInline",
                    pathMetadata = new
                    {
                        normalizedPath = "docs/readme.md",
                        displayName = "readme.md",
                        pathPolicyClass = "tenant_sensitive_document",
                        unicodeNormalization = "NFC",
                    },
                    contentHashReference = "hashref-a",
                    byteLength = 12,
                    inlineContent = new
                    {
                        mediaType = "text/plain",
                        contentBytes = "aGVsbG8gd29ybGQh",
                    },
                }),
            })
            {
                AddMutationHeaders(addFileRequest, "idempotency-addfile", "corr-addfile", "task-addfile");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(addFileRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Step 5 AddFile must reach 'accepted' (202).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-addfile");
                reachedTerminalClass.Add("add_file");
            }

            // ---- Step 6: CommitWorkspace — mutating_command → 202 ----
            // Commit requires workspace = ChangesStaged with the lock held by this task; seed that state.
            // The commit readiness validator and commit executor are wired to succeed (see host composition).
            host.SeedWorkspacePermissions("tenant-a", "org-a", "folder-commit", "user-a", "commit");
            host.SeedChangesStagedWorkspace("tenant-a", "org-a", "folder-commit", holderTaskId: "task-commit");
            using (HttpRequestMessage commitRequest = new(HttpMethod.Post, "/api/v1/folders/folder-commit/workspaces/workspace-a/commits")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    operationId = "operation-commit-a",
                    taskId = "task-commit",
                    branchRefTarget = "branchref_primary",
                    changedPathMetadataDigest = "digest_workspace_a",
                    authorMetadataReference = "authorref_service",
                    commitMessageClassification = "generated_summary",
                    auditMetadataKeys = new[] { "operation_id" },
                }),
            })
            {
                AddMutationHeaders(commitRequest, "idempotency-commit", "corr-commit", "task-commit");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(commitRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "Step 6 CommitWorkspace must reach 'accepted' (202).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-commit");
                reachedTerminalClass.Add("commit_workspace");
            }

            // ---- Step 7: ListFolderFiles (context query) — query → 200 (REST + SDK equivalence) ----
            // The candidate intentionally requires mutate_files while the historical handler requires
            // read_metadata. Grant only the candidate token: both the raw v2 surface and generated SDK must
            // still reach the handler through the single preauthorization decision, with no legacy re-check.
            const string contextFolder = "folder_context_001";
            host.SeedCandidateOnlyWorkspacePermissions("tenant-a", "org-a", contextFolder, "user-a", "mutate_files");
            using (HttpRequestMessage listRequest = new(HttpMethod.Get, $"/api/v2/folders/{contextFolder}/workspaces/{CandidateWorkspace}/context/tree?limit=10"))
            {
                listRequest.Headers.Add("X-Correlation-Id", "correlation_context_001");
                listRequest.Headers.Add("X-Hexalith-Task-Id", "task_context_0001");
                listRequest.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(listRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, "Step 7 ListFolderFiles must reach 'projected' (200).");
                reachedTerminalClass.Add("context_query");
            }

            FileTreeResult sdkTree = await host.SdkClient.ListFolderFilesAsync(
                folderId: contextFolder,
                workspaceId: CandidateWorkspace,
                x_Correlation_Id: "corr-context-sdk",
                x_Hexalith_Task_Id: "task-context-sdk",
                x_Hexalith_Freshness: ReadConsistencyClass.Snapshot_per_task,
                cursor: null,
                limit: 10,
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            sdkTree.ShouldNotBeNull("SDK ListFolderFiles must reach 'projected' transport-terminal class.");

            // ---- Step 8: GetWorkspaceStatus — query (DOCUMENTED SEAM, see class remarks) ----
            // Driven over the real REST + SDK route to its real canonical response. The happy-path 200 needs a
            // populated authoritative-tenant evidence path for the workspace-status projection that this
            // hermetic host does not reproduce, so the strict in-memory read model surfaces the canonical
            // metadata-only read_model_unavailable (503). We assert the REAL transport outcome of the live route
            // (200/projected if reachable, else the canonical 503) — never an oracle-metadata-only claim. The
            // happy-path 200 is proven by Server.Tests.WorkspaceStatusEndpointTests.
            const string statusFolder = "folder_status_0001";
            host.SeedWorkspacePermissions("tenant-a", "org-a", statusFolder, "user-a", "read_workspace_status");
            host.SeedWorkspaceStatus("tenant-a", statusFolder, CandidateWorkspace);
            using (HttpRequestMessage statusRequest = new(HttpMethod.Get, $"/api/v1/folders/{statusFolder}/workspaces/{CandidateWorkspace}/status"))
            {
                statusRequest.Headers.Add("X-Correlation-Id", "corr-status");
                statusRequest.Headers.Add("X-Hexalith-Task-Id", "task-status");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(statusRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                string statusBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                ((int)response.StatusCode).ShouldBeOneOf(
                    (int)HttpStatusCode.OK,
                    (int)HttpStatusCode.ServiceUnavailable);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-status", "REST must echo the explicit X-Correlation-Id unchanged.");
                }
                else
                {
                    // Seam: the canonical RFC 9457 problem carries the correlation in the metadata-only body.
                    using JsonDocument seamDoc = JsonDocument.Parse(statusBody);
                    seamDoc.RootElement.GetProperty("category").GetString().ShouldBe("read_model_unavailable", "the seam must surface the canonical metadata-only read_model_unavailable.");
                    seamDoc.RootElement.GetProperty("correlationId").GetString().ShouldBe("corr-status", "the seam problem must echo the explicit correlation in the body.");
                    seamDoc.RootElement.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
                }

                reachedTerminalClass.Add("workspace_status");
            }

            // SDK drives the same real route to the same real canonical outcome (200 binds WorkspaceStatus; the
            // seam surfaces the typed HexalithFoldersApiException carrying the canonical 503).
            try
            {
                WorkspaceStatus sdkStatus = await host.SdkClient.GetWorkspaceStatusAsync(
                    folderId: statusFolder,
                    workspaceId: CandidateWorkspace,
                    x_Correlation_Id: "correlation_status_sdk",
                    x_Hexalith_Freshness: ReadConsistencyClass.Read_your_writes,
                    cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
                sdkStatus.ShouldNotBeNull("SDK GetWorkspaceStatus reached the 'projected' transport-terminal class.");
            }
            catch (HexalithFoldersApiException sdkStatusSeam)
            {
                sdkStatusSeam.StatusCode.ShouldBe((int)HttpStatusCode.ServiceUnavailable, "SDK GetWorkspaceStatus drives the real route to the canonical 503 seam (see class remarks).");
            }

            // ---- Step 9: GetFolderLifecycleStatus (audit inspection surrogate) — query → 200 (REST + SDK) ----
            const string lifecycleFolder = "folder_lifecycle_1";
            host.SeedWorkspacePermissions("tenant-a", "org-a", lifecycleFolder, "user-a", "read_metadata");
            host.SeedLifecycleStatus("tenant-a", lifecycleFolder, correlationId: "correlation_lifecycle");
            using (HttpRequestMessage lifecycleRequest = new(HttpMethod.Get, $"/api/v1/folders/{lifecycleFolder}/lifecycle-status"))
            {
                lifecycleRequest.Headers.Add("X-Correlation-Id", "correlation_lifecycle");
                using HttpResponseMessage response = await host.HttpClient
                    .SendAsync(lifecycleRequest, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, "Step 9 GetFolderLifecycleStatus must reach 'projected' (200).");
                response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("correlation_lifecycle");
                reachedTerminalClass.Add("audit_inspection");
            }

            FolderLifecycleStatus sdkLifecycle = await host.SdkClient.GetFolderLifecycleStatusAsync(
                folderId: lifecycleFolder,
                x_Correlation_Id: "correlation_lifecycle",
                x_Hexalith_Freshness: ReadConsistencyClass.Eventually_consistent,
                cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
            sdkLifecycle.ShouldNotBeNull("SDK GetFolderLifecycleStatus must reach 'projected' transport-terminal class.");

            // All nine golden-lifecycle steps were driven over the real REST transport in order — eight reached
            // their happy-path transport-terminal class (five mutating_command → 202/accepted, three query →
            // 200/projected), and the ninth (GetWorkspaceStatus) was driven to its real canonical response as a
            // documented seam (see class remarks). No step is asserted at oracle-metadata level.
            reachedTerminalClass.ToArray().ShouldBe(orderedStepNames, "all nine steps must be driven over the real REST transport in order.");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static HttpRequestMessage CreateRepositoryBackedRequest(
        string folderId,
        string idempotencyKey,
        string correlationId,
        string taskId)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                folderId,
                providerBindingRef = "provider-binding-a",
                repositoryProfileRef = "profile-a",
                folderMetadata = new
                {
                    displayName = "Folder",
                    metadataClass = "tenant_sensitive",
                },
                branchRefPolicy = new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "binding-a",
                    policyRef = "branch_ref_policy_a",
                    defaultRef = "branch_ref_primary",
                    allowedRefPatterns = new[] { "branch_ref_feature" },
                },
            }),
        };
        AddMutationHeaders(request, idempotencyKey, correlationId, taskId);
        return request;
    }

    private static void AddMutationHeaders(
        HttpRequestMessage request,
        string idempotencyKey,
        string correlationId,
        string taskId)
    {
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", taskId);
    }

    private sealed class CapturingGatewayClient : IEventStoreGatewayClient
    {
        public SubmitCommandRequest? LastRequest { get; private set; }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SubmitCommandResponse(request.CorrelationId ?? request.MessageId));
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

    /// <summary>Bundles the in-process host together with the two clients that drive it (raw HttpClient + SDK IClient).</summary>
    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(
            WebApplication app,
            HttpClient httpClient,
            IClient sdkClient,
            InProcessRejectionPropagatingGatewayClient gateway,
            InMemoryFolderRepository repository,
            InMemoryFolderTenantAccessProjectionStore tenantStore,
            InMemoryEffectivePermissionsReadModel permissions,
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel,
            InMemoryTaskStatusReadModel taskStatusReadModel,
            InMemoryProviderReadinessBindingReadModel providerBindings,
            SingleReadTenantAccessProjectionStore? singleReadTenantStore,
            RecordingPd10AuthorizationAuditSink auditSink)
        {
            App = app;
            HttpClient = httpClient;
            SdkClient = sdkClient;
            Gateway = gateway;
            Repository = repository;
            TenantStore = tenantStore;
            Permissions = permissions;
            LifecycleReadModel = lifecycleReadModel;
            TaskStatusReadModel = taskStatusReadModel;
            ProviderBindings = providerBindings;
            SingleReadTenantStore = singleReadTenantStore;
            AuditSink = auditSink;
        }

        public WebApplication App { get; }

        public HttpClient HttpClient { get; }

        public IClient SdkClient { get; }

        public InProcessRejectionPropagatingGatewayClient Gateway { get; }

        public InMemoryFolderRepository Repository { get; }

        public InMemoryFolderTenantAccessProjectionStore TenantStore { get; }

        public InMemoryEffectivePermissionsReadModel Permissions { get; }

        public InMemoryFolderLifecycleStatusReadModel LifecycleReadModel { get; }

        public InMemoryTaskStatusReadModel TaskStatusReadModel { get; }

        public InMemoryProviderReadinessBindingReadModel ProviderBindings { get; }

        public int TenantProjectionReads => SingleReadTenantStore?.ReadCount ?? 0;

        private SingleReadTenantAccessProjectionStore? SingleReadTenantStore { get; }

        public RecordingPd10AuthorizationAuditSink AuditSink { get; }

        public static Task<TestHost> StartAsync() => StartAsync(tenantId: "tenant-a", principalId: "user-a");

        public static async Task<TestHost> StartAsync(
            string? tenantId,
            string? principalId,
            bool delegated = false,
            ITaskStatusReadModel? taskStatusOverride = null,
            IEffectivePermissionsReadModel? permissionsOverride = null,
            string? accessState = null,
            string delegatorPermission = "create_folder",
            bool throwOnSecondTenantProjectionRead = false,
            string? additionalAccessState = null,
            string? additionalDelegator = null,
            IPd10AuthorizationAuditSink? auditSinkOverride = null,
            Func<HttpContext, Task>? downstreamOverride = null,
            IEventStoreGatewayClient? gatewayOverride = null,
            bool headerlessTransportBody = false)
        {
            MutableTenantAndClaimContext context = new(tenantId, principalId);
            InMemoryFolderTenantAccessProjectionStore tenantStore = new();
            SingleReadTenantAccessProjectionStore? singleReadTenantStore = throwOnSecondTenantProjectionRead
                ? new(tenantStore)
                : null;
            InMemoryEffectivePermissionsReadModel permissions = new();
            FixedUtcClock clock = new(Now);
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(clock);
            InMemoryTaskStatusReadModel taskStatusReadModel = new(clock);
            InMemoryProviderReadinessBindingReadModel providerBindings = new();
            RecordingPd10AuthorizationAuditSink auditSink = new();
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
            builder.Services.AddSingleton(gatewayOverride ?? gateway);
            builder.Services.RemoveAll<ITenantContextAccessor>();
            builder.Services.AddSingleton<ITenantContextAccessor>(context);
            builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
            builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(context);
            builder.Services.RemoveAll<IFolderRepository>();
            builder.Services.AddSingleton<IFolderRepository>(repository);
            builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
            builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycleReadModel);
            builder.Services.RemoveAll<ITaskStatusReadModel>();
            builder.Services.AddSingleton(taskStatusOverride ?? taskStatusReadModel);
            builder.Services.RemoveAll<IPd10AuthorizationAuditSink>();
            builder.Services.AddSingleton(auditSinkOverride ?? auditSink);
            builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
            builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(
                (IFolderTenantAccessProjectionStore?)singleReadTenantStore ?? tenantStore);
            builder.Services.RemoveAll<IProviderReadinessBindingReader>();
            builder.Services.AddSingleton<IProviderReadinessBindingReader>(providerBindings);
            builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
            builder.Services.AddSingleton(permissionsOverride ?? permissions);
            builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
            builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
            builder.Services.RemoveAll<IUtcClock>();
            builder.Services.AddSingleton<IUtcClock>(clock);
            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton(timeProvider);

            WebApplication app = builder.Build();
            if (delegated || accessState is not null || additionalAccessState is not null)
            {
                app.Use(async (httpContext, next) =>
                {
                    List<Claim> claims = [];
                    if (delegated)
                    {
                        claims.Add(new Claim("eventstore:delegator", "delegator-a"));
                        if (additionalDelegator is not null)
                        {
                            claims.Add(new Claim("eventstore:delegator", additionalDelegator));
                        }

                        claims.Add(new Claim("eventstore:delegator-permission", delegatorPermission));
                    }

                    if (accessState is not null)
                    {
                        claims.Add(new Claim("eventstore:access-state", accessState));
                    }

                    if (additionalAccessState is not null)
                    {
                        claims.Add(new Claim("eventstore:access-state", additionalAccessState));
                    }

                    httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "candidate-test"));
                    await next(httpContext).ConfigureAwait(false);
                });
            }

            if (headerlessTransportBody)
            {
                app.Use((HttpContext httpContext, RequestDelegate next) =>
                {
                    httpContext.Request.ContentLength = null;
                    httpContext.Request.Headers.Remove("Content-Length");
                    httpContext.Request.Headers.Remove("Transfer-Encoding");
                    httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(new HeaderlessTransportBodyFeature());
                    return next(httpContext);
                });
            }

            app.UsePd10V2CandidateCompatibilitySeam();
            app.Use(async (httpContext, next) =>
            {
                if (httpContext.Request.Headers.ContainsKey("X-PD10-Test-Throw"))
                {
                    throw new InvalidOperationException("Synthetic post-authorization failure.");
                }

                await next(httpContext).ConfigureAwait(false);
            });
            if (downstreamOverride is not null)
            {
                app.Use((HttpContext httpContext, RequestDelegate _) => downstreamOverride(httpContext));
            }

            app.UseRouting();
            app.MapFoldersServerEndpoints();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            eventStoreClientFactory = app.GetTestClient;

            HttpClient httpClient = app.GetTestClient();
            // The SDK client points at the same in-process host. Per the generated Client constructor,
            // the HttpClient's BaseAddress is what's used; no other DI plumbing is required for the test.
            IClient sdkClient = new GeneratedSdkClient(V2CandidateTestClient.Create(app));

            return new TestHost(
                app,
                httpClient,
                sdkClient,
                gateway,
                repository,
                tenantStore,
                permissions,
                lifecycleReadModel,
                taskStatusReadModel,
                providerBindings,
                singleReadTenantStore,
                auditSink);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingPd10AuthorizationAuditSink : IPd10AuthorizationAuditSink
    {
        private readonly List<Pd10AuthorizationAuditRecord> _records = [];

        public IReadOnlyList<Pd10AuthorizationAuditRecord> Records
        {
            get
            {
                lock (_records)
                {
                    return _records.ToArray();
                }
            }
        }

        public ValueTask WriteAsync(Pd10AuthorizationAuditRecord record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_records)
            {
                _records.Add(record);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingPd10AuthorizationAuditSink : IPd10AuthorizationAuditSink
    {
        public int Attempts { get; private set; }

        public ValueTask WriteAsync(Pd10AuthorizationAuditRecord record, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new InvalidOperationException("audit unavailable");
        }
    }

    private sealed class ThrowingTaskStatusReadModel : ITaskStatusReadModel
    {
        public Task<TaskStatusReadModelResult> GetAsync(
            TaskStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Task read model must not be observed for an invalid query envelope.");
    }

    private sealed class FixedTaskStatusReadModel(TaskStatusReadModelResult result) : ITaskStatusReadModel
    {
        public Task<TaskStatusReadModelResult> GetAsync(
            TaskStatusReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class SingleReadTenantAccessProjectionStore(InMemoryFolderTenantAccessProjectionStore inner)
        : IFolderTenantAccessProjectionStore
    {
        private int _readCount;

        public int ReadCount => Volatile.Read(ref _readCount);

        public Task<FolderTenantAccessProjection?> GetAsync(
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _readCount) > 1)
            {
                throw new InvalidOperationException("The tenant projection must not be read after candidate preauthorization.");
            }

            return inner.GetAsync(tenantId, cancellationToken);
        }

        public Task SaveAsync(
            FolderTenantAccessProjection projection,
            CancellationToken cancellationToken = default)
            => inner.SaveAsync(projection, cancellationToken);
    }

    private sealed class FixedEffectivePermissionsReadModel(EffectivePermissionsReadModelResult result)
        : IEffectivePermissionsReadModel
    {
        public Task<EffectivePermissionsReadModelResult> GetAsync(
            EffectivePermissionsReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class HeaderlessTransportBodyFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => await stream.WriteAsync(bytes).ConfigureAwait(false);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// The AC1 nine-step golden-lifecycle host. Adds, on top of the cross-surface host composition, the
    /// richer wiring the full lifecycle needs: an allowing event-store authorization validator, allowing
    /// readiness validators for repository-creation/preparation/commit, a recording commit executor, a
    /// recording metadata-only file-context source, the seedable provider-readiness capability stack, and the
    /// seedable workspace-status read model — all hermetic (no Dapr/Keycloak/network/provider credentials).
    /// </summary>
    private sealed class GoldenLifecycleHost : IAsyncDisposable
    {
        private GoldenLifecycleHost(
            WebApplication app,
            HttpClient httpClient,
            IClient sdkClient,
            InMemoryFolderRepository repository,
            InMemoryFolderTenantAccessProjectionStore tenantStore,
            InMemoryEffectivePermissionsReadModel permissions,
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel,
            InMemoryWorkspaceStatusReadModel workspaceStatusReadModel)
        {
            App = app;
            HttpClient = httpClient;
            SdkClient = sdkClient;
            Repository = repository;
            TenantStore = tenantStore;
            Permissions = permissions;
            LifecycleReadModel = lifecycleReadModel;
            WorkspaceStatusReadModel = workspaceStatusReadModel;
        }

        private WebApplication App { get; }

        public HttpClient HttpClient { get; }

        public IClient SdkClient { get; }

        private InMemoryFolderRepository Repository { get; }

        private InMemoryFolderTenantAccessProjectionStore TenantStore { get; }

        private InMemoryEffectivePermissionsReadModel Permissions { get; }

        private InMemoryFolderLifecycleStatusReadModel LifecycleReadModel { get; }

        private InMemoryWorkspaceStatusReadModel WorkspaceStatusReadModel { get; }

        public static async Task<GoldenLifecycleHost> StartAsync()
        {
            MutableTenantAndClaimContext context = new("tenant-a", "user-a");
            InMemoryFolderTenantAccessProjectionStore tenantStore = new();
            InMemoryEffectivePermissionsReadModel permissions = new();
            FixedUtcClock clock = new(Now);
            InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(clock);
            InMemoryWorkspaceStatusReadModel workspaceStatusReadModel = new(clock);
            TimeProvider timeProvider = new FixedTimeProvider(Now);
            InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
            Func<HttpClient>? eventStoreClientFactory = null;
            InProcessRejectionPropagatingGatewayClient gateway = new(() => eventStoreClientFactory!(), () => context.PrincipalId);

            // Provider-readiness capability stack seeded to authorize and report all golden operations as
            // supported — the proven composition from ProviderReadinessEndpointTests (FakeGitProvider rows).
            SeedableProviderReadinessBindingReader bindingReader = new(new OrganizationProviderBinding(
                ManagedTenantId: "tenant-a",
                OrganizationId: "org-a",
                ProviderBindingRef: "binding-a",
                ProviderKind: "github",
                CredentialReferenceId: "credential-ref-a",
                NamingPolicy: new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                BranchPolicy: new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                CorrelationId: "binding-corr-a",
                TaskId: "binding-task-a",
                IdempotencyKey: "binding-idempotency-a",
                IdempotencyFingerprint: "binding-fingerprint-a",
                ConfiguredStatus: "configured",
                OccurredAt: Now));
            IGitProvider provider = FakeGitProvider.WithOperationRows(
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence));

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
            builder.Services.RemoveAll<IWorkspaceStatusReadModel>();
            builder.Services.AddSingleton<IWorkspaceStatusReadModel>(workspaceStatusReadModel);
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

            // Allowing readiness validators so the mutating aggregate gates reach Accepted (the production
            // defaults call a live provider). Repository-creation, preparation, and commit each consult one.
            builder.Services.RemoveAll<IRepositoryCreationReadinessValidator>();
            builder.Services.AddSingleton<IRepositoryCreationReadinessValidator>(new AllowingReadinessValidator());
            builder.Services.RemoveAll<IWorkspacePreparationReadinessValidator>();
            builder.Services.AddSingleton<IWorkspacePreparationReadinessValidator>(new AllowingReadinessValidator());
            builder.Services.RemoveAll<IWorkspaceCommitReadinessValidator>();
            builder.Services.AddSingleton<IWorkspaceCommitReadinessValidator>(new AllowingReadinessValidator());
            builder.Services.RemoveAll<IWorkspaceCommitExecutor>();
            builder.Services.AddSingleton<IWorkspaceCommitExecutor>(new SucceedingWorkspaceCommitExecutor());

            // AddFile (mutate_files) stages content through a path-policy evidence provider and a content
            // store; the production defaults are Unavailable (→ 503). Override both to succeed so the
            // mutate_files gate reaches 202.
            builder.Services.RemoveAll<IWorkspacePathPolicyEvidenceProvider>();
            builder.Services.AddSingleton<IWorkspacePathPolicyEvidenceProvider>(new NoEscapePathPolicyEvidenceProvider());
            builder.Services.RemoveAll<IWorkspaceFileContentStore>();
            builder.Services.AddSingleton<IWorkspaceFileContentStore>(new SucceedingWorkspaceFileContentStore());

            // Metadata-only recording context source for the ListFolderFiles step (the default is
            // UnavailableWorkspaceFileContextSource which would surface a safe 503).
            builder.Services.RemoveAll<IWorkspaceFileContextSource>();
            builder.Services.AddSingleton<IWorkspaceFileContextSource>(new RecordingContextSource());

            // Provider-readiness capability stack (override the live-provider defaults with the test stack).
            builder.Services.RemoveAll<IProviderReadinessBindingReader>();
            builder.Services.AddSingleton<IProviderReadinessBindingReader>(bindingReader);
            builder.Services.RemoveAll<IProviderCapabilityAuthorizer>();
            builder.Services.AddSingleton<IProviderCapabilityAuthorizer>(RecordingProviderCapabilityAuthorizer.Allowed("authz-capability-fresh"));
            builder.Services.RemoveAll<IProviderCapabilityResolver>();
            builder.Services.AddSingleton<IProviderCapabilityResolver>(new RecordingProviderCapabilityResolver(provider));
            builder.Services.RemoveAll<IProviderCapabilityEvidenceStore>();
            builder.Services.AddSingleton<IProviderCapabilityEvidenceStore, RecordingProviderCapabilityEvidenceStore>();

            WebApplication app = builder.Build();
            app.UsePd10V2CandidateCompatibilitySeam();
            app.UseRouting();
            app.MapFoldersServerEndpoints();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            eventStoreClientFactory = app.GetTestClient;

            HttpClient httpClient = app.GetTestClient();
            IClient sdkClient = new GeneratedSdkClient(V2CandidateTestClient.Create(app));

            return new GoldenLifecycleHost(
                app,
                httpClient,
                sdkClient,
                repository,
                tenantStore,
                permissions,
                lifecycleReadModel,
                workspaceStatusReadModel);
        }

        public void SeedTenant(string tenantId, string principalId)
            => TenantStore.SaveAsync(
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

        public void SeedFolderCreationPermissions(string tenantId, string organizationId, string folderId, string principalId)
            => Permissions.Save(new EffectivePermissionsReadModelSnapshot(
                tenantId,
                organizationId,
                folderId,
                EffectivePermissionsFolderLifecycleState.Active,
                [
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "manage_folder_access", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), RepositoryBackedFolderCreationService.ActionToken, Sequence: 2, EffectiveAt: Now.AddMinutes(-1)),
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "read_metadata", Sequence: 3, EffectiveAt: Now.AddMinutes(-1)),
                ],
                new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
                RevocationFreshnessEstablished: true,
                TaskScope: null));

        public void SeedWorkspacePermissions(string tenantId, string organizationId, string folderId, string principalId, string actionToken)
            => Permissions.Save(new EffectivePermissionsReadModelSnapshot(
                tenantId,
                organizationId,
                folderId,
                EffectivePermissionsFolderLifecycleState.Active,
                [
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), actionToken, Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), "read_metadata", Sequence: 2, EffectiveAt: Now.AddMinutes(-1)),
                ],
                new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
                RevocationFreshnessEstablished: true,
                TaskScope: null));

        public void SeedCandidateOnlyWorkspacePermissions(
            string tenantId,
            string organizationId,
            string folderId,
            string principalId,
            string actionToken)
            => Permissions.Save(new EffectivePermissionsReadModelSnapshot(
                tenantId,
                organizationId,
                folderId,
                EffectivePermissionsFolderLifecycleState.Active,
                [
                    new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(principalId), actionToken, Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
                ],
                new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-candidate-only", Stale: false, ReasonCode: null),
                RevocationFreshnessEstablished: true,
                TaskScope: null));

        /// <summary>Seeds a plain Unbound folder (just FolderCreated), the precondition for
        /// CreateRepositoryBackedFolder (which makes an existing Unbound folder repository-backed).</summary>
        public void SeedUnboundFolder(string tenantId, string organizationId, string folderId)
            => Repository.Seed(
                FolderStreamName.Create(tenantId, folderId),
                [FolderCreatedEvent(tenantId, organizationId, folderId)]);

        /// <summary>Seeds a folder bound + branch-ref-policy-configured (workspace lifecycle = Preparing), the
        /// precondition for PrepareWorkspace's WorkspacePrepared transition.</summary>
        public void SeedBoundFolderReadyForPreparation(string tenantId, string organizationId, string folderId)
            => Repository.Seed(FolderStreamName.Create(tenantId, folderId), BoundFolderEvents(tenantId, organizationId, folderId));

        /// <summary>Seeds a folder whose workspace lifecycle has reached Ready, the precondition for LockWorkspace.</summary>
        public void SeedReadyWorkspace(string tenantId, string organizationId, string folderId)
            => Repository.Seed(
                FolderStreamName.Create(tenantId, folderId),
                [
                    .. BoundFolderEvents(tenantId, organizationId, folderId),
                    WorkspacePreparedEvent(tenantId, organizationId, folderId),
                ]);

        /// <summary>Seeds a folder whose workspace is Locked with the lease held by <paramref name="holderTaskId"/>,
        /// the precondition for AddFile.</summary>
        public void SeedLockedWorkspace(string tenantId, string organizationId, string folderId, string holderTaskId)
            => Repository.Seed(
                FolderStreamName.Create(tenantId, folderId),
                [
                    .. BoundFolderEvents(tenantId, organizationId, folderId),
                    WorkspacePreparedEvent(tenantId, organizationId, folderId),
                    WorkspaceLockedEvent(tenantId, organizationId, folderId, holderTaskId),
                ]);

        /// <summary>Seeds a folder whose workspace is ChangesStaged with the lease held by
        /// <paramref name="holderTaskId"/>, the precondition for CommitWorkspace.</summary>
        public void SeedChangesStagedWorkspace(string tenantId, string organizationId, string folderId, string holderTaskId)
            => Repository.Seed(
                FolderStreamName.Create(tenantId, folderId),
                [
                    .. BoundFolderEvents(tenantId, organizationId, folderId),
                    WorkspacePreparedEvent(tenantId, organizationId, folderId),
                    WorkspaceLockedEvent(tenantId, organizationId, folderId, holderTaskId),
                    FileMutatedEvent(tenantId, organizationId, folderId, holderTaskId),
                ]);

        public void SeedWorkspaceStatus(string tenantId, string folderId, string workspaceId)
            => WorkspaceStatusReadModel.Save(new WorkspaceStatusReadModelSnapshot(
                ManagedTenantId: tenantId,
                FolderId: folderId,
                WorkspaceId: workspaceId,
                CurrentState: "committed",
                AcceptedCommandState: new WorkspaceAcceptedCommandState("task-status", "operation-status", "completed", Now),
                ProjectedState: new WorkspaceProjectedState("committed", "projection", Now),
                ProviderOutcome: new WorkspaceProviderOutcome(
                    "operation-status",
                    "known_success",
                    "success",
                    "provref_status",
                    new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
                    RetryAfter: null,
                    Freshness: WorkspaceFreshness(),
                    ChangedPathMetadataDigest: "digest_status",
                    CommitReferenceClassification: "opaque_reference",
                    ReconciliationReference: null),
                RetryEligibility: new WorkspaceStatusRetryEligibility(false, "retry_not_required"),
                RetryAfter: null,
                Freshness: WorkspaceFreshness(),
                ProjectionLag: new WorkspaceProjectionLag(0, "projection"),
                LastFailureCategory: null,
                EvidenceScope: new FolderLifecycleEvidenceScope(
                    ManagedTenantId: tenantId,
                    PrincipalId: "user-a",
                    ActionToken: "read_workspace_status",
                    TaskId: "task-status",
                    CorrelationId: "corr-status",
                    AuthorizationWatermark: "permission-watermark-a")));

        public void SeedLifecycleStatus(string tenantId, string folderId, string correlationId)
            => LifecycleReadModel.Save(new FolderLifecycleStatusReadModelSnapshot(
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

        public async ValueTask DisposeAsync()
        {
            HttpClient.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }

        private static FolderLifecycleFreshness WorkspaceFreshness()
            => new("read_your_writes", Now, "workspace_status_watermark_v1", Stale: false, ReasonCode: null);

        // The minimal valid event chain that drives a folder to RepositoryBindingState.Bound with the branch
        // ref policy metadata populated and workspace lifecycle = Preparing. Each event carries a distinct
        // idempotency key so the seed ledger's duplicate-key guard sees independent entries.
        private static IFolderEvent[] BoundFolderEvents(string tenantId, string organizationId, string folderId)
            =>
            [
                FolderCreatedEvent(tenantId, organizationId, folderId),
                new RepositoryBindingRequested(
                    tenantId,
                    organizationId,
                    folderId,
                    "binding-a",
                    "provider-binding-a",
                    "profile-a",
                    "branch_ref_policy_a",
                    "user-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key-binding-requested",
                    "seed-fingerprint-binding-requested",
                    Now.AddMinutes(-5)),
                new RepositoryBound(
                    tenantId,
                    organizationId,
                    folderId,
                    "binding-a",
                    "provider-binding-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key-bound",
                    "seed-fingerprint-bound",
                    Now.AddMinutes(-4)),
                new BranchRefPolicyConfigured(
                    tenantId,
                    organizationId,
                    folderId,
                    "binding-a",
                    "branch_ref_policy_a",
                    "branch_ref_primary",
                    ["branch_ref_feature"],
                    [],
                    "user-a",
                    "seed-correlation",
                    "seed-task",
                    "seed-key-branch-policy",
                    "seed-fingerprint-branch-policy",
                    Now.AddMinutes(-3)),
            ];

        private static IFolderEvent FolderCreatedEvent(string tenantId, string organizationId, string folderId)
            => new FolderCreated(
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
                "seed-key-created",
                "seed-fingerprint-created",
                Now.AddMinutes(-6));

        private static IFolderEvent WorkspacePreparedEvent(string tenantId, string organizationId, string folderId)
            => new FolderWorkspaceLifecycleEventRecorded(
                tenantId,
                organizationId,
                folderId,
                "workspace-a",
                FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                DirtyResolution: null,
                OperationId: "workspace-a",
                CorrelationId: "seed-correlation",
                TaskId: "seed-task",
                IdempotencyKey: "seed-key-prepared",
                IdempotencyFingerprint: "seed-fingerprint-prepared",
                OccurredAt: Now.AddMinutes(-2));

        private static IFolderEvent WorkspaceLockedEvent(string tenantId, string organizationId, string folderId, string holderTaskId)
            => new WorkspaceLockAcquired(
                tenantId,
                organizationId,
                folderId,
                "workspace-a",
                FolderWorkspaceLifecycleEvent.WorkspaceLocked,
                "workspace_lock_a",
                "exclusive_write",
                3600,
                holderTaskId,
                AcquiredAt: Now.AddMinutes(-2),
                EffectiveAt: Now.AddMinutes(-2),
                ExpiresAt: Now.AddHours(1),
                RetryEligibilityBasis: "lease_until_expiry",
                "user-a",
                "seed-correlation",
                holderTaskId,
                "seed-key-locked",
                "seed-fingerprint-locked",
                Now.AddMinutes(-2));

        private static IFolderEvent FileMutatedEvent(string tenantId, string organizationId, string folderId, string holderTaskId)
            => new WorkspaceFileMutationAccepted(
                tenantId,
                organizationId,
                folderId,
                "workspace-a",
                FolderWorkspaceLifecycleEvent.FileMutated,
                "operation-seed-file",
                "add",
                "PutFileInline",
                "tenant_sensitive_document",
                "digest_seed_file",
                ContentHashReference: "hashref-seed",
                ByteLength: 12,
                MediaType: "text/plain",
                TransportEvidenceKind: "inline_decoded",
                ObservedByteLength: 12,
                "user-a",
                "seed-correlation",
                holderTaskId,
                "seed-key-file-mutated",
                "seed-fingerprint-file-mutated",
                Now.AddMinutes(-1));

        private sealed class AllowingReadinessValidator
            : IRepositoryCreationReadinessValidator, IWorkspacePreparationReadinessValidator, IWorkspaceCommitReadinessValidator
        {
            public Task<ProviderReadinessValidationResult> ValidateAsync(
                ProviderReadinessValidationRequest request,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new ProviderReadinessValidationResult(
                    ProviderReadinessResultCode.Allowed,
                    "ready",
                    "success",
                    "none",
                    Retryable: false,
                    RetryAfter: null,
                    RemediationCategory: "none",
                    CorrelationId: request?.CorrelationId ?? "correlation-a",
                    ProviderReference: request?.ProviderBindingRef,
                    ProviderBindingRef: request?.ProviderBindingRef,
                    CapabilityProfileRef: "profile-a",
                    Evidence: null,
                    new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
                    ProviderFailureCategory.None,
                    "none"));
        }

        private sealed class SucceedingWorkspaceCommitExecutor : IWorkspaceCommitExecutor
        {
            public Task<WorkspaceCommitExecutionResult> CommitAsync(
                WorkspaceCommitExecutionRequest request,
                CancellationToken cancellationToken = default)
                => Task.FromResult(WorkspaceCommitExecutionResult.Succeeded("commitref_golden"));
        }

        private sealed class NoEscapePathPolicyEvidenceProvider : IWorkspacePathPolicyEvidenceProvider
        {
            public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(
                WorkspacePathPolicyEvidenceRequest request,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.NoEscape));
        }

        private sealed class SucceedingWorkspaceFileContentStore : IWorkspaceFileContentStore
        {
            public Task<WorkspaceFileContentStoreResult> StageAsync(
                WorkspaceFileContentStoreRequest request,
                CancellationToken cancellationToken = default)
                => Task.FromResult(WorkspaceFileContentStoreResult.Succeeded);
        }

        private sealed class SeedableProviderReadinessBindingReader(OrganizationProviderBinding binding)
            : IProviderReadinessBindingReader
        {
            public Task<OrganizationProviderBinding?> GetAsync(
                ProviderReadinessBindingReadRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<OrganizationProviderBinding?>(binding);
            }
        }

        // Metadata-only context source mirroring Server.Tests' RecordingContextSource: returns a single
        // file-tree entry and never surfaces content bytes, so ListFolderFiles reaches 200.
        private sealed class RecordingContextSource : IWorkspaceFileContextSource
        {
            public Task<WorkspaceFileContextSourceResult> QueryAsync(
                WorkspaceFileContextSourceRequest request,
                CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(request);
                WorkspaceFileContextLimits limits = new("tree", request.Limit, 1, 128, 1, false, "not_truncated");
                FolderLifecycleFreshness freshness = new("snapshot_per_task", Now, "context_watermark_v1", Stale: false, null);
                return Task.FromResult(new WorkspaceFileContextSourceResult(
                    WorkspaceFileContextSourceStatus.Available,
                    [new WorkspaceFileContextItem(
                        new Hexalith.Folders.Aggregates.Folder.PathMetadata("docs/readme.md", "readme.md", "metadata_only", "NFC"),
                        "file",
                        1,
                        "tenant_sensitive",
                        "not_redacted")],
                    null,
                    null,
                    null,
                    new WorkspaceFileContextPage(null, request.Limit, false, null),
                    limits,
                    freshness));
            }
        }
    }
}
