using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderOwnerReadQueryHandlerTests
{
    [Theory]
    [InlineData("allowed")]
    [InlineData("revoked")]
    [InlineData("missing-permission")]
    [InlineData("stale")]
    [InlineData("unavailable")]
    [InlineData("tenant-stale")]
    public async Task DispatcherReauthorizesDelegatedActorAgainstCurrentLayeredOwnerEvidence(string state)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new FixedUtcClock(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));
        var tenants = new InMemoryFolderTenantAccessProjectionStore();
        var tenant = new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Watermark = 1,
            ProjectionWatermark = "tenant-a:1",
            LastEventTimestamp = clock.UtcNow
        };
        tenant.Principals["actor"] = new("actor", "TenantOwner"); await tenants.SaveAsync(tenant, token);
        var store = new FolderOwnerReadStore(); var heads = new FolderOwnerReadHeads();
        var organization = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, heads);
        var folders = new EventStoreFolderEffectivePermissionsReadModel(store, clock, heads, organization);
        await folders.ApplyAsync("tenant-a", "folder-a", "created", 1, "created", new FolderCreated("tenant-a", "tenant-a", "folder-a", "Folder", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "actor", "corr", "task", "create", "create", clock.UtcNow), token);
        await folders.ApplyAsync("tenant-a", "folder-a", "granted", 2, "granted", new FolderAccessGranted("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
            "actor", "read_metadata", "actor", "corr", "task", "grant", "grant", 2, clock.UtcNow), token);
        heads.Values["folder-a"] = 2;
        var context = new DefaultHttpContext(); var accessor = new HttpContextAccessor { HttpContext = context };
        var layered = new LayeredFolderAuthorizationService(new TenantAccessAuthorizer(tenants, clock, new()),
            new EffectivePermissionsFolderPermissionEvidenceProvider(folders, clock), new VerifiedReceiptEventStoreAuthorizationValidator(accessor),
            new ConfigurationDaprPolicyEvidenceProvider(new()), clock);
        await using ServiceProvider services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<IDomainQueryHandler>(new FolderOwnerReadQueryHandler(layered, accessor)).BuildServiceProvider();
        QueryEnvelope query = new("tenant-a", "folders", "folder-a", FolderOwnerReadQuery.QueryType, "{}"u8.ToArray(), "corr", "actor")
        { EntityId = "folder-a", OriginalActorId = "actor", IsDelegated = true, CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]) };
        async Task<QueryResult> DispatchAsync(QueryEnvelope sent)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery),
                .. QueryCallerAuthorityBinding.Bind(sent).Select(pair => new Claim(pair.Key, pair.Value))], "workload"));
            return await DomainQueryDispatcher.ExecuteAsync(services, sent, token).ConfigureAwait(false);
        }
        (await DispatchAsync(query)).Success.ShouldBeTrue();
        if (state == "revoked")
        {
            await folders.ApplyAsync("tenant-a", "folder-a", "revoked", 3, "revoked", new FolderAccessRevoked("tenant-a", "tenant-a", "folder-a", FolderAccessPrincipalKind.User,
                "actor", "read_metadata", "actor", "corr", "task", "revoke", "revoke", 3, clock.UtcNow), token);
            heads.Values["folder-a"] = 3;
        }
        if (state == "missing-permission") query = query with { CallerAuthority = new("delegated-owner-query", [], ["read_content"]) };
        if (state == "stale") heads.Values["folder-a"] = 3;
        if (state == "unavailable") heads.Unavailable = true;
        if (state == "tenant-stale") { tenant = (await tenants.GetAsync("tenant-a", token))!; tenant.LastEventTimestamp = clock.UtcNow.AddMinutes(-10); await tenants.SaveAsync(tenant, token); }
        QueryResult result = await DispatchAsync(query);
        if (state is "revoked" or "missing-permission") { result.ErrorMessage.ShouldBe("safe-denial"); result.PayloadBytes.ShouldBeNull(); }
        else
        {
            result.Success.ShouldBeTrue();
            JsonSerializer.Deserialize<FolderOwnerReadResult>(result.PayloadBytes!, JsonSerializerOptions.Web)!.Outcome.ShouldBe(state switch
            { "stale" or "tenant-stale" => FolderOwnerReadOutcome.Stale, "unavailable" => FolderOwnerReadOutcome.Unavailable, _ => FolderOwnerReadOutcome.Allowed });
        }
        VerifiedQueryCaller.Get(context).ShouldBeNull();
    }
}
