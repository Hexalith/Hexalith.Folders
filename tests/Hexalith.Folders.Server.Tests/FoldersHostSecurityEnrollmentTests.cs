using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing.Patterns;
using Hexalith.Folders.Projections.TenantAccess;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Exercises the named human/channel policies and real catalog refusal in the production pipeline.</summary>
public sealed class FoldersHostSecurityEnrollmentTests
{
    private const string Issuer = "https://identity.example.test/realms/folders";
    private const string Channel = "enrollment-receiver-channel";

    /// <summary>Preserves safe v1 denials and prevents protected observation for each credential kind and route group.</summary>
    /// <param name="method">The v1 HTTP method.</param>
    /// <param name="route">The protected route group representative.</param>
    [Theory]
    [InlineData("GET", "/api/v1/folders/folder/lifecycle-status")]
    [InlineData("GET", "/api/v1/tasks/task/status?folderId=folder")]
    [InlineData("PUT", "/api/v1/provider-bindings/binding")]
    [InlineData("GET", "/api/v1/provider-bindings/binding")]
    [InlineData("GET", "/api/v1/provider-readiness/support-evidence")]
    [InlineData("GET", "/api/v1/ops-console/readiness-diagnostics")]
    [InlineData("GET", "/api/v1/folders/folder/audit-trail")]
    [InlineData("POST", "/api/v1/folders/folder/workspaces/workspace/content/disposition")]
    [InlineData("POST", "/api/v1/folders/folder/workspaces/workspace/native/task-lease")]
    public async Task EveryHumanRouteGroupRejectsMissingInvalidWorkloadAndMixedCredentialsWithSafeV1Problems(string method, string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa);
        var authority = new DeniedTenantAccessProjectionStore();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(authority);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        foreach (string kind in new[] { "anonymous", "invalid", "channel", "workload", "workload-bearer", "mixed" })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            request.Headers.Add("X-Correlation-Id", "enrollment-correlation");
            request.Content = new StringContent("{}");
            if (kind == "invalid") request.Headers.Authorization = new("Bearer", "invalid");
            if (kind == "channel") request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
            if (kind is "workload" or "mixed")
            {
                request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(rsa, true));
            }
            if (kind == "workload-bearer") request.Headers.Authorization = new("Bearer", Token(rsa, true));
            if (kind == "mixed") request.Headers.Authorization = new("Bearer", Token(rsa, false));
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            bool forbidden = kind is "workload-bearer" or "mixed";
            response.StatusCode.ShouldBe(forbidden ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, kind);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            problem.RootElement.GetProperty("category").GetString().ShouldBe(forbidden ? "tenant_access_denied" : "authentication_failure");
            problem.RootElement.GetProperty("code").GetString().ShouldBe(forbidden ? "tenant_access_denied" : "authentication_failure");
            problem.RootElement.GetProperty("retryable").GetBoolean().ShouldBeFalse();
            problem.RootElement.GetProperty("clientAction").GetString().ShouldBe("no_action");
            problem.RootElement.GetProperty("correlationId").GetString().ShouldBe("enrollment-correlation");
            string evidenceSource = route.Contains("/ops-console/", StringComparison.Ordinal) ? "ops_console_diagnostics"
                : route.Contains("/provider-readiness/", StringComparison.Ordinal) || method == "GET" && route.Contains("/provider-bindings/", StringComparison.Ordinal) ? "provider_readiness"
                : route.Contains("/audit-trail", StringComparison.Ordinal) ? "audit" : "http_boundary";
            problem.RootElement.GetProperty("details").GetProperty("evidenceSource").GetString().ShouldBe(evidenceSource);
            problem.RootElement.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
        }
        authority.Calls.ShouldBe(0);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Exercises exact probes, authenticated descriptions and aliases, and actual receiver-channel discovery.</summary>
    [Fact]
    public async Task ExactProbesAreAnonymousRootAndAliasesRequireHumanAndDiscoveryRequiresActualChannel()
    {
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        EventStoreDomainServiceEndpointInventory.Validate(endpoints, null).ShouldBeEmpty();
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, app.Services)).ShouldBeEmpty();
        using HttpClient client = app.GetTestClient();
        foreach (string probe in new[] { "/health", "/alive" })
        {
            using HttpResponseMessage response = await client.GetAsync(probe, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using (HttpResponseMessage ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken))
            ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable); // No activated catalog is fabricated.
        foreach (string route in new[] { "/", "/health/live", "/health/ready", "/dapr/subscribe" })
        {
            using HttpResponseMessage denied = await client.GetAsync(route, TestContext.Current.CancellationToken);
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, route);
        }
        using (var human = new HttpRequestMessage(HttpMethod.Get, "/"))
        {
            human.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(rsa, false));
            using HttpResponseMessage response = await client.SendAsync(human, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using (var human = new HttpRequestMessage(HttpMethod.Get, "/health/live"))
        {
            human.Headers.Authorization = new("Bearer", Token(rsa, false));
            using HttpResponseMessage response = await client.SendAsync(human, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        foreach (string channel in new[] { "wrong", Channel })
        {
            using var discovery = new HttpRequestMessage(HttpMethod.Get, "/dapr/subscribe");
            discovery.Headers.Add(DaprAppChannelToken.HeaderName, channel);
            using HttpResponseMessage response = await client.SendAsync(discovery, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(channel == Channel ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        }
        using (var callback = new HttpRequestMessage(HttpMethod.Post, "/project"))
        {
            callback.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
            callback.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(rsa, true));
            callback.Content = new StringContent("{\"tenantId\":\"tenant\",\"domain\":\"folders\",\"aggregateId\":\"folder\",\"events\":[]}", System.Text.Encoding.UTF8, "application/json");
            using HttpResponseMessage refused = await client.SendAsync(callback, TestContext.Current.CancellationToken);
            refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Rejects an unclassified endpoint during production startup.</summary>
    [Fact]
    public async Task UnclassifiedEndpointFailsStartupEvenWithAnAuthenticatedFallback()
    {
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        app.MapGet("/unclassified", () => "must not run");
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken));
        failure.Message.ShouldContain("must name a policy");
    }

    /// <summary>Rejects a canonical override that lacks the actual receiver catalog convention.</summary>
    [Fact]
    public async Task WeakCanonicalOverrideFailsStartupWithoutCatalogConvention()
    {
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        app.MapPost("/project", () => "must not run").WithOrder(-1)
            .RequireAuthorization(EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/project").Policy);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken));
        failure.Message.ShouldContain("catalog guard");
    }

    /// <summary>Denies every credential after a late endpoint loses classification, before handler work.</summary>
    [Fact]
    public async Task LateUnclassifiedReplacementDeniesEveryCredentialBeforeHandlerWork()
    {
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        int calls = 0;
        var initial = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/late-surface"), 0);
        initial.Metadata.Add(new HttpMethodMetadata(["GET"]));
        initial.Metadata.Add(new AuthorizeAttribute(EventStoreHttpHostSecurityExtensions.HumanPolicy));
        var source = new FoldersMutableEndpointSource(initial.Build());
        ((IEndpointRouteBuilder)app).DataSources.Add(source);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var replacement = new RouteEndpointBuilder(_ => { calls++; return Task.CompletedTask; }, RoutePatternFactory.Parse("/late-surface"), 0);
        replacement.Metadata.Add(new HttpMethodMetadata(["GET"]));
        source.Replace(replacement.Build());
        using HttpClient client = app.GetTestClient();
        foreach (string kind in new[] { "anonymous", "human", "channel", "workload", "mixed" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/late-surface");
            if (kind is "human" or "mixed") request.Headers.Authorization = new("Bearer", Token(rsa, false));
            if (kind is "channel" or "workload" or "mixed") request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
            if (kind is "workload" or "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(rsa, true));
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, kind);
        }
        calls.ShouldBe(0);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Refuses a proposed catalog target that disagrees with the actual mapped receiver.</summary>
    [Fact]
    public async Task CatalogCandidateCannotReplaceIndependentlyObservedExecutableTarget()
    {
        using RSA rsa = RSA.Create(2048);
        WebApplicationBuilder builder = CreateBuilder(rsa, development: true);
        var catalog = new FoldersProjectionCatalogFixture();
        catalog.Configure(builder);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        var failure = await Should.ThrowAsync<Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException>(
            () => catalog.ActivateAsync(app, mismatchTarget: true));
        failure.Message.ShouldContain("targets");
    }

    private static WebApplicationBuilder CreateBuilder(RSA rsa, bool development = false)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(environmentName: development ? Microsoft.Extensions.Hosting.Environments.Development : Microsoft.Extensions.Hosting.Environments.Production, useCallbackCatalog: false);
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = Channel;
        builder.Configuration["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256;
        var oidc = new OpenIdConnectConfiguration { Issuer = Issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "enrollment" });
        foreach (string scheme in new[] { JwtBearerDefaults.AuthenticationScheme, EventStoreWorkloadAuthenticationDefaults.WorkloadScheme })
            builder.Services.PostConfigure<JwtBearerOptions>(scheme, options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
        return builder;
    }

    private static string Token(RSA rsa, bool workload)
    {
        DateTime now = DateTime.UtcNow;
        var claims = new Dictionary<string, object> { ["sub"] = "enrollment-user", ["eventstore:tenant"] = "tenant" };
        if (workload)
        {
            claims[EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore";
            claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProject;
        }
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer, Audience = "folders", IssuedAt = now, NotBefore = now, Expires = now.AddMinutes(2), Claims = claims,
            SigningCredentials = new(new RsaSecurityKey(rsa) { KeyId = "enrollment" }, SecurityAlgorithms.RsaSha256),
        });
    }
}
