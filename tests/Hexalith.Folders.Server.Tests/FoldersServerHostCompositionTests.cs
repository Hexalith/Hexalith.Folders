using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    /// <summary>Checks that each actual callback requires its own workload operation.</summary>
    /// <param name="route">The callback route.</param>
    /// <param name="operation">The only operation accepted by the route's policy.</param>
    [Theory]
    [InlineData(FoldersServerModule.ProcessRoute, EventStoreWorkloadOperations.DomainServiceProcess)]
    [InlineData(FoldersServerModule.ProjectRoute, EventStoreWorkloadOperations.DomainServiceProject)]
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
