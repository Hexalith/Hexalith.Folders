using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
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

/// <summary>Guards the production service graph and the callback authority boundary.</summary>
public sealed class FoldersServerHostCompositionTests
{
    /// <summary>Checks the installed receipt validator, durable tenant store, and workload defaults.</summary>
    [Fact]
    public void ProductionHostRegistersReceiptAndDurableTenantAuthority()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();

        app.Services.GetRequiredService<IEventStoreAuthorizationValidator>()
            .ShouldBeOfType<VerifiedReceiptEventStoreAuthorizationValidator>();
        app.Services.GetRequiredService<HumanAuthorityReceiptVerifier>().ShouldNotBeNull();
        app.Services.GetRequiredService<IFolderTenantAccessProjectionStore>()
            .ShouldBeOfType<EventStoreFolderTenantAccessProjectionStore>();

        WorkloadAuthenticationOptions workload = app.Services
            .GetRequiredService<IOptionsMonitor<WorkloadAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        workload.Audience.ShouldBe("folders");
        workload.AllowedCallers.ShouldBe(["eventstore"]);
    }

    [Fact]
    public async Task ProtectedProductionDiscoveryAdvertisesOnlyProcessorBackedNamedAndSharedRoutes()
    {
        const string issuer = "https://identity.example.test/realms/folders";
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa) { KeyId = "metadata-test" };
        OpenIdConnectConfiguration oidc = new() { Issuer = issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = signingKey.KeyId });
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Configuration["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256;
        builder.Services.PostConfigure<JwtBearerOptions>(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
        builder.Services.AddSingleton<IAsyncDomainProjectionHandler, OrphanOperationalProjectionHandler>();
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("folders")
            .ShouldBeSameAs(scope.ServiceProvider.GetServices<IDomainProcessor>().OfType<FolderDomainProcessor>().Single());
        app.Services.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(typeof(IDomainProcessor), "orphan").ShouldBeFalse();
        using HttpClient client = app.GetTestClient();
        AdminOperationalIndexMetadata.Request request = new(["folders"]) { AppId = "folders", ServiceVersion = "v1" };
        using (HttpResponseMessage denied = await client.PostAsJsonAsync("/admin/operational-index-metadata", request, TestContext.Current.CancellationToken))
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        DateTime issued = DateTime.UtcNow;
        string token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = "folders",
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceMetadata,
            },
            SigningCredentials = new(signingKey, SecurityAlgorithms.RsaSha256),
        });
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, token);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/admin/operational-index-metadata", request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = (await response.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken))!;
        var domain = metadata.Domains.ShouldHaveSingleItem();
        domain.Domain.ShouldBe("folders");
        domain.NamedProjectionTypes.ShouldBe([OperationalStatusKeys.ProjectionType]);
        domain.SharedEpochProjectionTypes.ShouldBe([OperationalStatusKeys.ProjectionType]);
        domain.AggregateTypes.ShouldBeEmpty();
        metadata.CatalogFingerprint.ShouldNotBeNullOrWhiteSpace();
        using HttpResponseMessage orphanResponse = await client.PostAsJsonAsync("/admin/operational-index-metadata",
            request with { Domains = ["orphan"] }, TestContext.Current.CancellationToken);
        orphanResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unserved = (await orphanResponse.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken))!;
        unserved.Domains.ShouldBeEmpty();
        unserved.DispatchCapability.ShouldBeNull();
        unserved.CatalogFingerprint.ShouldBeNull();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Checks that each actual callback requires its own workload operation.</summary>
    /// <param name="route">The callback route.</param>
    /// <param name="operation">The only operation accepted by the route's policy.</param>
    [Theory]
    [InlineData(FoldersServerModule.ProcessRoute, EventStoreWorkloadOperations.DomainServiceProcess)]
    [InlineData(FoldersServerModule.ProjectRoute, EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("/query", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("/admin/operational-index-metadata", EventStoreWorkloadOperations.DomainServiceMetadata)]
    public void ProductionCallbackUsesItsOperationPolicy(string route, string operation)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();

        string policyName = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, operation);
        AuthorizationPolicy? policy = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(policyName);
        policy.ShouldNotBeNull();
        policy.AuthenticationSchemes.ShouldBe([EventStoreWorkloadAuthenticationDefaults.WorkloadScheme]);
        policy.Requirements.ShouldContain(static requirement => requirement is DenyAnonymousAuthorizationRequirement);
        ClaimsAuthorizationRequirement operationRequirement = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Single(static requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.OperationClaimType);
        operationRequirement.AllowedValues.ShouldBe([operation]);
        policy.Requirements.OfType<ClaimsAuthorizationRequirement>()
            .ShouldContain(static requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType);

        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == route);
        endpoint.Metadata.OfType<IAuthorizeData>()
            .ShouldContain(authorize => authorize.Policy == policyName);
    }
}
