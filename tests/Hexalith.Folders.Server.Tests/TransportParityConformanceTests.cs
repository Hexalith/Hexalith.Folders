using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Server;
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
/// remaining 19 (audit, ACL, several queries, a few mutators, diagnostics) are <c>rest</c>-expected by
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
    public const int ImplementedRestOperationCount = 28;

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
    /// The 19 oracle operations that are <c>rest</c>-expected but have no registered <c>/api/v1</c>
    /// endpoint in the current server. Enumerated explicitly so a NEW unimplemented row (or a silently
    /// filled gap) fails the surface-gap guard. Recorded in Story 5.5 Dev Notes as the known REST gap.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownRestSurfaceGap = new HashSet<string>(StringComparer.Ordinal)
    {
        // Queries that the SDK exposes but the server does not yet route.
        "GetProviderBinding",
        "GetRepositoryBinding",
        "GetWorkspaceRetryEligibility",
        "GetWorkspaceTransitionEvidence",
        "GetProjectionFreshness",

        // Audit-family operations (no /api/v1/audit-trail route).
        "GetAuditRecord",
        "GetOperationTimelineEntry",
        "ListAuditTrail",
        "ListOperationTimeline",

        // ACL.
        "ListFolderAclEntries",
        "UpdateFolderAclEntry",

        // Mutating commands the SDK exposes but the server does not yet route.
        "CreateFolder",
        "ConfigureProviderBinding",

        // Diagnostics / operations-console projections.
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

    private sealed class FixedTenantContextAccessor(string? tenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = tenantId;

        public string? PrincipalId { get; } = principalId;
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
