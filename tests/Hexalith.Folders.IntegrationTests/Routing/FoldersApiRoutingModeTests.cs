using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Builder;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>
/// Story 1.17 plan B route preparation: the composed server host defaults to the v1-only hold, coexistence
/// routes both versions, and the retired mode answers external v1 with the canonical 404 while the v2 seam's
/// internal historical dispatch keeps working. An empty or unknown mode fails host startup.
/// </summary>
public sealed class FoldersApiRoutingModeTests
{
    private const string V1LifecyclePath = $"/api/v1/folders/{RoutingModeTestHost.FolderId}/lifecycle-status";
    private const string V2LifecyclePath = $"/api/v2/folders/{RoutingModeTestHost.FolderId}/lifecycle-status";

    [Theory]
    [InlineData(null)]
    [InlineData("V1Only")]
    public async Task HoldModeRoutesV1AndDoesNotRouteV2(string? mode)
    {
        RoutingModeTestHost host = await RoutingModeTestHost.StartAsync(mode).ConfigureAwait(true);
        await using ConfiguredAsyncDisposable hostScope = host.ConfigureAwait(true);

        host.Mode.ShouldBe(FoldersApiRoutingMode.V1Only);
        using HttpResponseMessage v1 = await GetAsync(host, V1LifecyclePath).ConfigureAwait(true);
        v1.StatusCode.ShouldBe(HttpStatusCode.OK);
        int lifecycleReadsAfterV1 = host.Lifecycle.Reads;
        lifecycleReadsAfterV1.ShouldBe(1);

        using HttpResponseMessage v2 = await GetAsync(host, V2LifecyclePath).ConfigureAwait(true);
        v2.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the hold composes no v2 seam, so no endpoint matches /api/v2");
        (await v2.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeEmpty("an unrouted path must not reach the seam's canonical problem writer");
        host.AuditSink.Records.ShouldBeEmpty("the v2 authorization seam is not composed in the hold");
        host.Lifecycle.Reads.ShouldBe(lifecycleReadsAfterV1);
    }

    [Fact]
    public async Task CoexistenceRoutesV1AndV2()
    {
        RoutingModeTestHost host = await RoutingModeTestHost.StartAsync("Coexistence").ConfigureAwait(true);
        await using ConfiguredAsyncDisposable hostScope = host.ConfigureAwait(true);

        host.Mode.ShouldBe(FoldersApiRoutingMode.Coexistence);
        using HttpResponseMessage v1 = await GetAsync(host, V1LifecyclePath).ConfigureAwait(true);
        v1.StatusCode.ShouldBe(HttpStatusCode.OK);
        host.AuditSink.Records.ShouldBeEmpty("a v1 request does not pass through the v2 seam");

        using HttpResponseMessage v2 = await GetAsync(host, V2LifecyclePath).ConfigureAwait(true);
        v2.StatusCode.ShouldBe(HttpStatusCode.OK);
        v2.Headers.GetValues("X-Hexalith-Freshness").ShouldBe(["eventually_consistent"]);
        host.AuditSink.Records.Select(static record => (record.Operation, record.Result))
            .ShouldBe([("GetFolderLifecycleStatus", "allow")]);
        host.Lifecycle.Reads.ShouldBe(2);
    }

    [Theory]
    [InlineData("Coexistence")]
    [InlineData("V2Only")]
    public async Task AuthenticationRunsBeforeTheSeamWithTheRealClaimsBasedAccessors(string mode)
    {
        RoutingModeTestHost host = await RoutingModeTestHost.StartAsync(mode, claimsIdentity: true).ConfigureAwait(true);
        await using ConfiguredAsyncDisposable hostScope = host.ConfigureAwait(true);

        using HttpRequestMessage request = new(HttpMethod.Get, V2LifecyclePath);
        request.Headers.Add("X-Correlation-Id", RoutingModeTestHost.CorrelationId);
        request.Headers.TryAddWithoutValidation("Authorization", RoutingModeAuthenticationHandler.SchemeName);
        using HttpResponseMessage v2 = await host.Client
            .SendAsync(request, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        v2.StatusCode.ShouldBe(HttpStatusCode.OK, "the seam must see the principal that UseAuthentication established");
        host.AuditSink.Records.Select(static record => (record.Actor, record.Tenant, record.Operation, record.Result))
            .ShouldBe([(RoutingModeTestHost.PrincipalId, RoutingModeTestHost.TenantId, "GetFolderLifecycleStatus", "allow")]);
    }

    [Fact]
    public async Task RetiredModeAnswersExternalV1WithCanonical404BeforeAnyLookupAndKeepsV2Dispatch()
    {
        RoutingModeTestHost host = await RoutingModeTestHost.StartAsync("V2Only").ConfigureAwait(true);
        await using ConfiguredAsyncDisposable hostScope = host.ConfigureAwait(true);

        host.Mode.ShouldBe(FoldersApiRoutingMode.V2Only);
        foreach (string path in new[]
        {
            V1LifecyclePath,
            V1LifecyclePath.ToUpperInvariant().Replace(RoutingModeTestHost.FolderId.ToUpperInvariant(), RoutingModeTestHost.FolderId, StringComparison.Ordinal),
            "/api/v1",
        })
        {
            using HttpResponseMessage retired = await GetAsync(host, path).ConfigureAwait(true);
            await AssertCanonicalSafeDenialAsync(retired).ConfigureAwait(true);
        }

        using HttpRequestMessage mutation = new(HttpMethod.Post, "/api/v1/folders")
        {
            Content = new StringContent(
                $$"""{"folderId":"{{RoutingModeTestHost.FolderId}}","organizationId":"org-a","displayName":"Folder"}""",
                Encoding.UTF8,
                "application/json"),
        };
        mutation.Headers.Add("X-Correlation-Id", RoutingModeTestHost.CorrelationId);
        mutation.Headers.Add("Idempotency-Key", "idempotency_routing_0001");
        using HttpResponseMessage retiredMutation = await host.Client
            .SendAsync(mutation, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await AssertCanonicalSafeDenialAsync(retiredMutation).ConfigureAwait(true);

        host.TenantStore.Reads.ShouldBe(0, "retired v1 is rejected before any authorization lookup");
        host.Lifecycle.Reads.ShouldBe(0, "retired v1 is rejected before any protected read");
        host.AuditSink.Records.ShouldBeEmpty("retired v1 never reaches an authorization decision");
        host.Gateway.ReceivedCalls().ShouldBeEmpty("retired v1 causes no command effect");

        using HttpResponseMessage v2 = await GetAsync(host, V2LifecyclePath).ConfigureAwait(true);
        v2.StatusCode.ShouldBe(HttpStatusCode.OK, "the seam's internal historical dispatch still reaches the v1 handler");
        host.Lifecycle.Reads.ShouldBe(1);
        host.AuditSink.Records.Select(static record => (record.Operation, record.Result))
            .ShouldBe([("GetFolderLifecycleStatus", "allow")]);

        using HttpResponseMessage liveness = await GetAsync(host, "/health/live").ConfigureAwait(true);
        liveness.StatusCode.ShouldBe(HttpStatusCode.OK, "only the historical API is retired");
    }

    [Fact]
    public async Task RetiredV1DenialIsByteEquivalentToTheSeamsUndeclaredRouteDenial()
    {
        RoutingModeTestHost host = await RoutingModeTestHost.StartAsync("V2Only").ConfigureAwait(true);
        await using ConfiguredAsyncDisposable hostScope = host.ConfigureAwait(true);

        using HttpResponseMessage retired = await GetAsync(host, V1LifecyclePath).ConfigureAwait(true);
        using HttpResponseMessage undeclared = await GetAsync(
            host,
            $"/api/v2/folders/{RoutingModeTestHost.FolderId}/not-declared").ConfigureAwait(true);

        retired.StatusCode.ShouldBe(undeclared.StatusCode);
        retired.Content.Headers.ContentType?.ToString().ShouldBe(undeclared.Content.Headers.ContentType?.ToString());
        (await retired.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBe(await undeclared.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" V1Only")]
    [InlineData("v2only")]
    [InlineData("1")]
    [InlineData("V1Only,V2Only")]
    [InlineData("Unknown")]
    public void InvalidModeFailsHostStartup(string mode)
    {
        WebApplicationBuilder builder = RoutingModeTestHost.CreateBuilder(mode);

        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => builder.AddFoldersServerHost());

        failure.Message.ShouldContain(FoldersApiRouting.ModeConfigurationKey, Case.Sensitive);
    }

    private static async Task<HttpResponseMessage> GetAsync(RoutingModeTestHost host, string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-Id", RoutingModeTestHost.CorrelationId);
        return await host.Client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task AssertCanonicalSafeDenialAsync(HttpResponseMessage response)
    {
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
        problem.GetProperty("correlationId").GetString().ShouldBe(RoutingModeTestHost.CorrelationId);
        problem.GetProperty("retryable").GetBoolean().ShouldBeFalse();
        problem.GetProperty("details").EnumerateObject().Select(static property => property.Name)
            .ShouldBe(["visibility"]);
    }
}
