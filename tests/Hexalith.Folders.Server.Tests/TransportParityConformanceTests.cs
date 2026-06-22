using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// REST transport-parity conformance (Story 5.5 AC #2/#3/#4/#5/#6/#8). Drives the in-process server host
/// and asserts that the registered <c>/api/v1</c> surface honors the oracle's <c>transport_parity</c>
/// columns: operation identity (drift-aware against a documented REST-surface gap), idempotency-key
/// transport rule, RFC 9457 error/auth shape, transport-terminal state per family, and X-Correlation-Id
/// echo. The shared oracle reader and golden-lifecycle list live in <c>tests/shared/Parity/</c>, linked
/// (not copied) into this project.
/// </summary>
/// <remarks>
/// <para><b>Drift-aware REST coverage.</b> The contract spine declares all 47 operation ids and the SDK
/// is generated from it, so the SDK exposes all 47 methods. The current REST server implementation
/// registers <see cref="ImplementedRestOperationCount"/> of those 47 as <c>/api/v1</c> endpoints; the
/// remaining 15 (ACL, several queries, a few mutators, diagnostics) are <c>rest</c>-expected by
/// the oracle but have no server endpoint yet. Per Story 5.5's "surface drift in Dev Notes, do not edit
/// production code or oracle" directive, the coverage guard here:
/// <list type="bullet">
///   <item><description>asserts every registered <c>/api/v1</c> endpoint name maps back to an oracle row
///     (orphan-endpoint guard — a new endpoint not in the oracle fails),</description></item>
///   <item><description>asserts every <c>rest</c>-expected oracle row is either implemented OR appears in
///     the enumerated <see cref="KnownRestSurfaceGap"/> set (new gaps and silently filled gaps both
///     fail),</description></item>
///   <item><description>documents three ASP.NET endpoint-name aliases where the server's
///     <c>.WithName(...)</c> diverges from the spine/SDK operationId (file mutation endpoints), via
///     <see cref="EndpointNameAliases"/>.</description></item>
/// </list></para>
/// </remarks>
public sealed class TransportParityConformanceTests
{
    /// <summary>The count of oracle operations the REST server currently implements as <c>/api/v1</c> endpoints.</summary>
    /// <remarks>Story 8.1 raised this from 32 to 40 by implementing the 8 Bucket-A canonical REST routes.</remarks>
    public const int ImplementedRestOperationCount = 40;

    /// <summary>
    /// Documented endpoint-name aliases where the ASP.NET <c>.WithName(...)</c> diverges from the contract
    /// spine / SDK operationId. The route + SDK both match the contract operationId; only the server's
    /// internal endpoint-name metadata uses a more specific name. Recorded in Story 5.5 Dev Notes.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EndpointNameAliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AddWorkspaceFile"] = "AddFile",
        ["ChangeWorkspaceFile"] = "ChangeFile",
        ["RemoveWorkspaceFile"] = "RemoveFile",
    };

    /// <summary>
    /// The 7 oracle operations that are <c>rest</c>-expected but have no registered <c>/api/v1</c>
    /// endpoint in the current server. Enumerated explicitly so a NEW unimplemented row (or a silently
    /// filled gap) fails the surface-gap guard. Story 8.1 implemented the 8 Bucket-A routes (removed from
    /// this set); the remaining 7 are the Bucket-B ops-console diagnostics tracked by Story 8.2.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownRestSurfaceGap = new HashSet<string>(StringComparer.Ordinal)
    {
        // Diagnostics / operations-console projections (Bucket B — Story 8.2).
        "GetProjectionFreshness",
        "GetDirtyStateDiagnostics",
        "GetFailedOperationDiagnostics",
        "GetLockDiagnostics",
        "GetProviderStatusDiagnostics",
        "GetReadinessDiagnostics",
        "GetSyncStatusDiagnostics",
    };

    // ---------------------------------------------------------------------------------------------------
    // AC #2 / AC #8 — REST identity / coverage / drift-aware guards.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void OracleCarriesTheExpectedFortySevenDistinctRows()
    {
        ParityOracle.Rows.Count.ShouldBe(ParityScenarios.ExpectedOperationCount);
        ParityOracle.Rows.Select(row => row.OperationId).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(ParityScenarios.ExpectedOperationCount);
    }

    [Fact]
    public void KnownRestSurfaceGapAccountsForEveryRestExpectedRowWithoutAnEndpoint()
    {
        // Coverage invariant: implemented + known-gap = every rest-expected row. A new oracle row
        // joining without a server endpoint (and without being documented here), or an existing gap
        // being silently filled in the server, breaks one direction of this guard.
        HashSet<string> registeredOperationIds = LoadRegisteredOperationIds();
        HashSet<string> restExpected = ParityOracle.Rows
            .Where(row => row.AdapterExpectations.Contains("rest", StringComparer.Ordinal))
            .Select(row => row.OperationId)
            .ToHashSet(StringComparer.Ordinal);

        // Direction 1: implemented endpoints all map back to an oracle row (orphan-endpoint guard).
        string[] orphans = registeredOperationIds.Where(id => !restExpected.Contains(id)).ToArray();
        orphans.ShouldBeEmpty($"Registered /api/v1 endpoint names with no matching rest-expected oracle row: {string.Join(", ", orphans)}");

        // Direction 2: rest-expected rows with no endpoint exactly equal the documented gap.
        string[] missing = restExpected.Where(id => !registeredOperationIds.Contains(id)).ToArray();
        HashSet<string> missingSet = new(missing, StringComparer.Ordinal);

        string[] surpriseMissing = missingSet.Except(KnownRestSurfaceGap, StringComparer.Ordinal).ToArray();
        string[] surpriseImplemented = KnownRestSurfaceGap.Except(missingSet, StringComparer.Ordinal).ToArray();

        surpriseMissing.ShouldBeEmpty(
            $"rest-expected oracle rows newly missing a /api/v1 endpoint (add to KnownRestSurfaceGap or wire the endpoint): {string.Join(", ", surpriseMissing)}");
        surpriseImplemented.ShouldBeEmpty(
            $"oracle rows listed in KnownRestSurfaceGap now appear to be implemented — remove them from the gap set: {string.Join(", ", surpriseImplemented)}");
    }

    [Fact]
    public void ImplementedRestSurfaceMatchesTheRecordedCount()
    {
        HashSet<string> registered = LoadRegisteredOperationIds();
        registered.Count.ShouldBe(
            ImplementedRestOperationCount,
            $"REST surface has {registered.Count} oracle-mapped endpoints; expected {ImplementedRestOperationCount} per the Story 5.5 baseline (update the constant + Dev Notes if endpoints were added/removed).");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #3 — Idempotency-key transport rule, representative pair (mutating accepts; query rejects).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MutatingEndpointAcceptsIdempotencyKeyAndReplayReturnsAcceptedTransportTerminal()
    {
        // ArchiveFolder oracle row: idempotency_key_rule == required_for_mutating_command, terminal_states == [accepted].
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.Transport.IdempotencyKeyRule.ShouldBe("required_for_mutating_command");
        archive.Transport.TerminalStates.ShouldContain("accepted");

        RecordingEventStoreGatewayClient gateway = new()
        {
            Response = new SubmitCommandResponse(
                "correlation-archive",
                JsonSerializer.SerializeToElement(new { idempotentReplay = false })),
        };
        await RunAgainstHostAsync(gateway, "tenant-a", "principal-a", async client =>
        {
            using HttpRequestMessage first = CreateValidArchiveRequest();
            using HttpResponseMessage firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken).ConfigureAwait(true);
            firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "first mutating call must reach 'accepted' transport-terminal class.");

            gateway.Response = new SubmitCommandResponse(
                "correlation-archive",
                JsonSerializer.SerializeToElement(new { idempotentReplay = true }));
            using HttpRequestMessage replay = CreateValidArchiveRequest();
            using HttpResponseMessage replayResponse = await client.SendAsync(replay, TestContext.Current.CancellationToken).ConfigureAwait(true);
            replayResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, "replay with the same idempotency key must remain in the 'accepted' class.");

            using JsonDocument replayDoc = JsonDocument.Parse(
                await replayResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            replayDoc.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue("replay must surface idempotentReplay=true (no duplicate side-effect).");
            replayDoc.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
        }).ConfigureAwait(true);

        gateway.Requests.Count.ShouldBe(2, "both calls reach the gateway; idempotency is enforced at the aggregate, not blocked at the endpoint.");
        gateway.Requests[0].MessageId.ShouldBe(gateway.Requests[1].MessageId);
    }

    [Fact]
    public async Task QueryEndpointRejectsIdempotencyKeyWithCanonicalSafeProblem()
    {
        // ListFolderFiles is a context_query whose shared ValidateContextQueryEnvelope rejects
        // Idempotency-Key with canonical 'validation_error'/'idempotency_key_not_allowed', and whose
        // oracle error_code_set declares 'validation_error' (so the emitted category is in the allow-list).
        // This is the representative non-mutating endpoint for AC #3's "query rejects Idempotency-Key"
        // half of the rule. (Note: a separate known REST drift — several other query endpoints e.g.
        // GetWorkspaceLock and GetEffectivePermissions either emit a category not in their error_code_set
        // or silently accept the header; that's recorded in the Story 5.5 Dev Notes and is outside this
        // story's test-only scope.)
        ParityRow row = ParityScenarios.Row("ListFolderFiles");
        row.Transport.IdempotencyKeyRule.ShouldBe("not_accepted_for_non_mutating_operation");

        RecordingEventStoreGatewayClient gateway = new();
        await RunAgainstHostAsync(gateway, "tenant-a", "principal-a", async client =>
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/context/tree");
            request.Headers.Add("X-Correlation-Id", "correlation-a");
            request.Headers.Add("X-Hexalith-Task-Id", "task-a");
            request.Headers.Add("Idempotency-Key", "idempotency-a");

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement rootElement = document.RootElement;
            string category = rootElement.GetProperty("category").GetString()!;
            rootElement.GetProperty("code").GetString().ShouldBe("idempotency_key_not_allowed");

            // AC #5: emitted category must be in the row's error_code_set.
            row.Transport.ErrorCodeSet.ShouldContain(
                category,
                $"ListFolderFiles emitted category '{category}' is outside its oracle error_code_set.");
            AssertCanonicalProblemShape(rootElement, expectCorrelation: "correlation-a");
        }).ConfigureAwait(true);

        gateway.Requests.ShouldBeEmpty("Idempotency-Key rejection must happen before any gateway submit.");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #5 — Error / authorization transport shape (RFC 9457 + category ∈ error_code_set).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task UnauthenticatedRequestSurfacesCanonicalAuthenticationFailureWithinTheOracleErrorCodeSet()
    {
        // ArchiveFolder error_code_set includes 'authentication_failure'; the row's auth_outcome_class is
        // 'folder_acl_denied' (which is also in the set) — withholding the tenant claim provokes the
        // authentication boundary, not the ACL boundary, so the emitted category is authentication_failure
        // (within the allow-list) and the shape is canonical RFC 9457 metadata-only.
        ParityRow row = ParityScenarios.Row("ArchiveFolder");
        row.Transport.ErrorCodeSet.ShouldContain("authentication_failure");

        RecordingEventStoreGatewayClient gateway = new();
        await RunAgainstHostAsync(gateway, tenantId: null, principalId: null, async client =>
        {
            using HttpRequestMessage request = CreateValidArchiveRequest();
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string category = root.GetProperty("category").GetString()!;

            category.ShouldBe("authentication_failure");
            row.Transport.ErrorCodeSet.ShouldContain(
                category,
                $"ArchiveFolder emitted category '{category}' is outside its oracle error_code_set.");
            AssertCanonicalProblemShape(root, expectCorrelation: "correlation-a");
        }).ConfigureAwait(true);

        gateway.Requests.ShouldBeEmpty("authentication denial must precede any gateway submit.");
    }

    [Fact]
    public async Task ReservedSystemTenantRequestSurfacesCanonicalTenantAccessDeniedWithinTheOracleErrorCodeSet()
    {
        // Second representative AC #5 negative (covers the auth_outcome_class side that the unauthenticated
        // case does not). Driving the request with tenantId == "system" (the reserved tenant) provokes the
        // tenant_access_denied envelope-validation branch; the wire category must be the oracle-listed
        // 'tenant_access_denied' (which IS in ArchiveFolder.error_code_set) with the canonical RFC 9457
        // metadata-only shape and status 403.
        ParityRow row = ParityScenarios.Row("ArchiveFolder");
        row.Transport.ErrorCodeSet.ShouldContain("tenant_access_denied");

        RecordingEventStoreGatewayClient gateway = new();
        await RunAgainstHostAsync(gateway, tenantId: "system", principalId: "principal-a", async client =>
        {
            using HttpRequestMessage request = CreateValidArchiveRequest();
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string category = root.GetProperty("category").GetString()!;

            category.ShouldBe("tenant_access_denied");
            row.Transport.ErrorCodeSet.ShouldContain(
                category,
                $"ArchiveFolder emitted category '{category}' is outside its oracle error_code_set.");
            AssertCanonicalProblemShape(root, expectCorrelation: "correlation-a");
        }).ConfigureAwait(true);

        gateway.Requests.ShouldBeEmpty("tenant_access_denied envelope rejection must precede any gateway submit.");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #6 — Terminal transport state class per family (mutating → accepted/202).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MutatingFamilyEndpointReturnsAcceptedTransportTerminalClass()
    {
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.OperationFamily.ShouldBe("mutating_command");
        archive.Transport.TerminalStates.ShouldContain("accepted");
        ParityScenarios.FamilyToTerminalState[archive.OperationFamily].ShouldBe("accepted");

        RecordingEventStoreGatewayClient gateway = new();
        await RunAgainstHostAsync(gateway, "tenant-a", "principal-a", async client =>
        {
            using HttpRequestMessage request = CreateValidArchiveRequest();
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            document.RootElement.GetProperty("status").GetString().ShouldBe("accepted",
                "mutating_command transport-terminal class is 'accepted' (202 + status='accepted').");
        }).ConfigureAwait(true);
    }

    [Fact]
    public async Task ContextQueryFamilyEndpointReturnsContextReturnedTransportTerminalClass()
    {
        // ListFolderFiles oracle row: operation_family == 'context_query', terminal_states ∋ 'context_returned' (HTTP 200).
        // Drives the in-process file-context source with an allowing tenant/permission stack so the query
        // reaches the 200 success path (not 4xx safe-denial). This is the second of the five
        // FamilyToTerminalState classes exercised on the REST surface (paired with 'accepted' above; the
        // remaining three — 'projected', 'audit_returned', 'projection_returned' — are exercised on REST
        // by GoldenLifecycleParityTests / are part of the documented REST surface gap respectively).
        ParityRow context = ParityScenarios.Row("ListFolderFiles");
        context.OperationFamily.ShouldBe("context_query");
        context.Transport.TerminalStates.ShouldContain("context_returned");
        ParityScenarios.FamilyToTerminalState[context.OperationFamily].ShouldBe("context_returned");

        RecordingFileContextSource source = new();
        await RunAgainstContextHostAsync(source, "tenant-a", "user-a", async client =>
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/context/tree?limit=10");
            request.Headers.Add("X-Correlation-Id", "correlation-context");
            request.Headers.Add("X-Hexalith-Task-Id", "task-context");
            request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                "context_query transport-terminal class is 'context_returned' (HTTP 200 with metadata-only items).");
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument document = JsonDocument.Parse(body);
            document.RootElement.GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0,
                "context_returned terminal class surfaces metadata-only items in the response body.");
            body.ShouldNotContain("contentBytes", Case.Sensitive,
                "context_query metadata response must remain metadata-only (no contentBytes).");
        }).ConfigureAwait(true);

        source.Requests.Count.ShouldBe(1, "successful context_query reaches the source exactly once.");
    }

    // ---------------------------------------------------------------------------------------------------
    // AC #4 — Correlation transport, oracle-driven (correlation_field_path = headers.X-Correlation-Id).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExplicitCorrelationIdIsEchoedUnchangedOnResponseHeader()
    {
        // All current oracle rows declare correlation_field_path == headers.X-Correlation-Id.
        ParityRow archive = ParityScenarios.Row("ArchiveFolder");
        archive.Transport.CorrelationFieldPath.ShouldBe("headers.X-Correlation-Id");

        RecordingEventStoreGatewayClient gateway = new();
        await RunAgainstHostAsync(gateway, "tenant-a", "principal-a", async client =>
        {
            // The server enforces lowercase canonical identifiers on caller-supplied correlation ids.
            const string explicitCorrelation = "correlation-fixed-0123456789abcdef";
            using HttpRequestMessage request = CreateValidArchiveRequest();
            request.Headers.Remove("X-Correlation-Id");
            request.Headers.Add("X-Correlation-Id", explicitCorrelation);

            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(explicitCorrelation);
        }).ConfigureAwait(true);
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers — host harness + assertions.
    // ---------------------------------------------------------------------------------------------------

    /// <summary>Asserts the RFC 9457 canonical problem shape SafeProblem produces.</summary>
    /// <param name="root">The parsed problem document root.</param>
    /// <param name="expectCorrelation">The expected echoed correlation id.</param>
    private static void AssertCanonicalProblemShape(JsonElement root, string expectCorrelation)
    {
        root.TryGetProperty("category", out _).ShouldBeTrue("category extension is required by AC #5.");
        root.TryGetProperty("code", out _).ShouldBeTrue("code extension is required by AC #5.");
        root.TryGetProperty("message", out _).ShouldBeTrue("message extension is required by AC #5.");
        root.GetProperty("correlationId").GetString().ShouldBe(expectCorrelation, "correlationId must echo the request correlation.");
        root.TryGetProperty("retryable", out _).ShouldBeTrue("retryable extension is required by AC #5.");
        root.TryGetProperty("clientAction", out _).ShouldBeTrue("clientAction extension is required by AC #5.");
        JsonElement details = root.GetProperty("details");
        details.GetProperty("visibility").GetString().ShouldBe("metadata_only",
            "AC #5 mandates details.visibility == metadata_only on every problem body.");
    }

    /// <summary>Loads the registered <c>/api/v1</c> endpoint names from a built host, normalized via the
    /// documented alias map so file-mutation endpoint names match their oracle <c>operation_id</c>.</summary>
    /// <returns>The set of oracle-mapped operation ids registered as <c>/api/v1</c> endpoints.</returns>
    private static HashSet<string> LoadRegisteredOperationIds()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        using WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();

        IEnumerable<RouteEndpoint> endpoints = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(static endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true);

        HashSet<string> mapped = new(StringComparer.Ordinal);
        foreach (RouteEndpoint endpoint in endpoints)
        {
            string? name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            mapped.Add(EndpointNameAliases.TryGetValue(name, out string? canonical) ? canonical : name);
        }

        return mapped;
    }

    /// <summary>Starts the in-process host, runs <paramref name="body"/> against an HttpClient pointed at it,
    /// and tears the host down (matches the project's try/finally lifecycle pattern with <c>ConfigureAwait(true)</c>).</summary>
    /// <param name="gateway">The recording gateway stub.</param>
    /// <param name="tenantId">The tenant id (<c>null</c> provokes authentication failure).</param>
    /// <param name="principalId">The principal id (<c>null</c> provokes authentication failure).</param>
    /// <param name="body">The test body, called with an HttpClient bound to the host's loopback URI.</param>
    private static async Task RunAgainstHostAsync(
        RecordingEventStoreGatewayClient gateway,
        string? tenantId,
        string? principalId,
        Func<HttpClient, Task> body)
    {
        WebApplication app = await StartAppAsync(gateway, tenantId, principalId).ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            await body(client).ConfigureAwait(true);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<WebApplication> StartAppAsync(
        RecordingEventStoreGatewayClient gateway,
        string? tenantId,
        string? principalId)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new FixedTenantContextAccessor(tenantId, principalId));

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return app;
    }

    private static HttpRequestMessage CreateValidArchiveRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                archiveReasonCode = "caller_requested",
            }),
        };
        request.Headers.Add("Idempotency-Key", "idempotency-archive-a");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-archive-a");
        return request;
    }

    /// <summary>Starts the in-process host wired with an allowing tenant/permission stack and a stub
    /// <see cref="IWorkspaceFileContextSource"/>, runs <paramref name="body"/> against it, and tears the host
    /// down. Mirrors <see cref="RunAgainstHostAsync"/> but layers the additional file-context dependencies
    /// the <c>/api/v1/.../context/...</c> endpoints require so the query reaches its <c>context_returned</c>
    /// transport-terminal class (HTTP 200) rather than a safe-denial 4xx.</summary>
    /// <param name="source">The recording file-context source stub.</param>
    /// <param name="tenantId">The tenant id.</param>
    /// <param name="principalId">The principal id.</param>
    /// <param name="body">The test body, called with an HttpClient bound to the host's loopback URI.</param>
    private static async Task RunAgainstContextHostAsync(
        RecordingFileContextSource source,
        string tenantId,
        string principalId,
        Func<HttpClient, Task> body)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new FixedTenantContextAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero)));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(BuildAllowingTenantStore(tenantId, principalId));
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IWorkspaceFileContextSource>();
        builder.Services.AddSingleton<IWorkspaceFileContextSource>(source);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            await body(client).ConfigureAwait(true);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static IFolderTenantAccessProjectionStore BuildAllowingTenantStore(string tenantId, string principalId)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [principalId] = new(principalId, "Member"),
            },
            Watermark = 1,
            LastEventTimestamp = new DateTimeOffset(2026, 5, 28, 11, 59, 0, TimeSpan.Zero),
            ProjectionWatermark = "tenant_watermark_v1",
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class FixedTenantContextAccessor(string? tenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string? tenantId, string? principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);
            return string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
        }
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1"));
    }

    private sealed class AllowingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
    }

    private sealed class RecordingFileContextSource : IWorkspaceFileContextSource
    {
        public List<WorkspaceFileContextSourceRequest> Requests { get; } = [];

        public Task<WorkspaceFileContextSourceResult> QueryAsync(
            WorkspaceFileContextSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            DateTimeOffset now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
            WorkspaceFileContextLimits limits = new(
                "tree",
                request.Limit,
                1,
                128,
                1,
                false,
                "not_truncated");
            FolderLifecycleFreshness freshness = new("snapshot_per_task", now, "context_watermark_v1", Stale: false, ReasonCode: null);
            PathMetadata path = new("docs/readme.md", "readme.md", "tenant_sensitive_document", "NFC");

            return Task.FromResult(new WorkspaceFileContextSourceResult(
                WorkspaceFileContextSourceStatus.Available,
                [new WorkspaceFileContextItem(path, "file", 1, "tenant_sensitive", "not_redacted")],
                null,
                null,
                null,
                new WorkspaceFileContextPage(null, request.Limit, false, null),
                limits,
                freshness));
        }
    }

    private sealed class RecordingEventStoreGatewayClient : IEventStoreGatewayClient
    {
        public List<SubmitCommandRequest> Requests { get; } = [];

        public SubmitCommandResponse? Response { get; set; }

        public Exception? Exception { get; set; }

        public Task<SubmitCommandResponse> SubmitCommandAsync(
            SubmitCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Response ?? new SubmitCommandResponse(request.CorrelationId ?? request.MessageId));
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
}
