using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class OrganizationAclReauthorizationTests
{
    public static IEnumerable<object[]> ChangedAuthorityCases()
    {
        foreach (string failure in new[] { "membership", "owner", "disabled", "stale", "future",
            "replay", "malformed", "foreign", "principal", "timestamp", "store" })
        {
            foreach (int read in new[] { 2, 3, 4 })
            {
                yield return [FoldersServerModule.InitializeOrganizationAclBaselineCommandType, failure, read];
            }

            if (failure != "owner")
            {
                yield return [FoldersServerModule.GrantOrganizationAclPrincipalCommandType, failure, 2];
                yield return [FoldersServerModule.RevokeOrganizationAclPrincipalCommandType, failure, 2];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ChangedAuthorityCases))]
    public async Task ChangedTenantAuthorityCannotReturnAppendableAclEventsOnEitherActorEvaluation(
        string commandType, string failure, int failureRead)
    {
        ChangingTenantAccessProjectionStore tenants = new(failure, failureRead);
        await using WebApplication app = CreateApp(tenants);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        CommandEnvelope command = Command(commandType);
        DomainServiceCurrentState? state = commandType == FoldersServerModule.InitializeOrganizationAclBaselineCommandType
            ? null : RetainedBaseline();
        for (int evaluation = 0; evaluation < 2; evaluation++)
        {
            tenants.Reset();
            using HttpResponseMessage response = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
                new DomainServiceRequest(command, state), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("metadata_only");
            body.ShouldNotContain("events");
            body.ShouldNotContain("tenant-a");
            body.ShouldNotContain("user-a");
            body.ShouldNotContain("fingerprint");
        }
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(FoldersServerModule.GrantOrganizationAclPrincipalCommandType)]
    [InlineData(FoldersServerModule.RevokeOrganizationAclPrincipalCommandType)]
    public async Task ExplicitAclManagersMayGrantOrRevokeAfterOwnerRoleRemoval(string commandType)
    {
        await using WebApplication app = CreateApp(new ChangingTenantAccessProjectionStore("owner", 1));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpResponseMessage response = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(Command(commandType), RetainedBaseline()), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(commandType == FoldersServerModule.GrantOrganizationAclPrincipalCommandType
            ? nameof(OrganizationAclPrincipalGranted) : nameof(OrganizationAclPrincipalRevoked));
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ForeignBootstrapActorIsDeniedBeforeAnyStateLookup()
    {
        ChangingTenantAccessProjectionStore tenants = new("store", 1);
        await using WebApplication app = CreateApp(tenants);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        using HttpResponseMessage response = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(Command(FoldersServerModule.InitializeOrganizationAclBaselineCommandType)
                with
            { AggregateId = "tenant-b" }, null), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        tenants.Reads.ShouldBe(0);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("events");
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AclRevocationBetweenAuthorizationAndDecisionDeniesRepeatedEvaluation()
    {
        ChangingOrganizationAclPermissionEvidenceProvider evidence = new();
        await using WebApplication app = CreateApp(new ChangingTenantAccessProjectionStore("valid", 1), evidence);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        for (int evaluation = 0; evaluation < 2; evaluation++)
        {
            evidence.Reset();
            using HttpResponseMessage response = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
                new DomainServiceRequest(Command(FoldersServerModule.GrantOrganizationAclPrincipalCommandType),
                    RetainedBaseline()), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("events");
            evidence.Reads.ShouldBe(2);
        }
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WebApplication CreateApp(IFolderTenantAccessProjectionStore tenants,
        IFolderPermissionEvidenceProvider? evidence = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor, ProductionProcessTenantContextAccessor>();
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor, ProductionProcessClaimTransformEvidenceAccessor>();
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton(tenants);
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        if (evidence is null)
        {
            InMemoryEffectivePermissionsReadModel permissions = new();
            permissions.Save(new EffectivePermissionsReadModelSnapshot("tenant-a", "tenant-a", "organization_baseline",
                EffectivePermissionsFolderLifecycleState.Active,
                [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "manage_organization_acl", 1, DateTimeOffset.UtcNow)],
                new("read_your_writes", DateTimeOffset.UtcNow, "2", false, null), true, null));
            builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
            builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        }
        else
        {
            builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
            builder.Services.AddSingleton(evidence);
        }
        builder.Services.RemoveAll<IOrganizationAclRepository>();
        builder.Services.RemoveAll<IOrganizationProviderBindingRepository>();
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();
        builder.Services.AddScoped<ActorFoldedOrganizationProviderBindingRepository>();
        builder.Services.AddScoped<IOrganizationAclRepository>(sp => sp.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());
        builder.Services.AddScoped<IOrganizationProviderBindingRepository>(sp => sp.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());
        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static CommandEnvelope Command(string type)
        => new("message-new", "tenant-a", "folders", "tenant-a", type,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                operations = new[]
                { new { principalKind = "User", principalId = "user-a",
                    action = type == FoldersServerModule.RevokeOrganizationAclPrincipalCommandType ? "create_folder" : "bind_repository" } }
            }),
            "correlation-a", null, "user-a", new Dictionary<string, string> { ["taskId"] = "task-a" });

    private static DomainServiceCurrentState RetainedBaseline()
    {
        IOrganizationAclEvent[] values = [new OrganizationAclBaselineInitialized("tenant-a", "tenant-a",
            "correlation-old", "task-old", "key-old", "fingerprint-old"),
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "create_folder", "correlation-old", "task-old", "key-old", "fingerprint-old")];
        return new(null, values.Select((value, index) => new EventEnvelope(new EventMetadata(
            "message-old", "tenant-a", "organization", "tenant-a", "folders", index + 1, 1,
            DateTimeOffset.UtcNow, "correlation-old", "message-old", "user-a", "test", value.GetType().FullName!,
            1, "json"), JsonSerializer.SerializeToUtf8Bytes(value, value.GetType()), null)).ToArray(), 0, 2);
    }
}
