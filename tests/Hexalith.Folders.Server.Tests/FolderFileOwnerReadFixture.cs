using Xunit;
using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server.Tests;

internal sealed class FolderFileOwnerReadFixture
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    internal FolderOwnerReadStore Store { get; } = new();
    internal FolderFilePermissionEvidenceProvider Permissions { get; } = new();
    internal FolderFileIdentityReadModel Row { get; set; } = new("tenant-a", "folder-a", 5, Now,
        [new("owner-file", "référence-1", true)]);
    internal async Task SeedAsync()
    {
        Store.ForbidWrites = false;
        await Store.TrySaveAsync("statestore", FolderFileIdentityReadModel.Key("tenant-a", "folder-a"), Row, "", TestContext.Current.CancellationToken).ConfigureAwait(false);
        Store.ReadKeys.Clear();
        Store.IdentityReadsOnly = true;
        Store.ForbidWrites = true;
    }
    internal static QueryEnvelope Query(bool inverse = false) => new("tenant-a", "folders", "folder-a",
        inverse ? FolderFileReferenceReadQuery.QueryType : FolderFileOwnerReadQuery.QueryType,
        JsonSerializer.SerializeToUtf8Bytes(inverse ? (object)new FolderFileReferenceReadQuery("référence-1") : new FolderFileOwnerReadQuery("owner-file"), JsonSerializerOptions.Web),
        "correlation", "actor")
    {
        OriginalActorId = "actor",
        AuthenticatedWorkloadId = "projects",
        IsDelegated = true,
        EntityId = "folder-a",
        CallerAuthority = new("delegated-owner-query", [], ["read_metadata"])
        { OriginatingContext = new("actor", "original-client", false, null, ["projects.read"], ["hexalith-eventstore"]) },
    };
    internal async Task<QueryResult> DispatchAsync(QueryEnvelope? query = null, bool verified = true)
    {
        query ??= Query();
        var clock = new FixedUtcClock(Now);
        var tenants = new InMemoryFolderTenantAccessProjectionStore();
        var tenant = new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now
        };
        tenant.Principals["actor"] = new("actor", "TenantOwner");
        await tenants.SaveAsync(tenant, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        List<Claim> claims = [new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery)];
        if (verified) claims.AddRange(QueryCallerAuthorityBinding.Bind(query).Select(pair => new Claim(pair.Key, pair.Value)));
        accessor.HttpContext.User = new(new ClaimsIdentity(claims, "workload"));
        var layered = new LayeredFolderAuthorizationService(new TenantAccessAuthorizer(tenants, clock, new()), Permissions,
            new VerifiedReceiptEventStoreAuthorizationValidator(accessor), new ConfigurationDaprPolicyEvidenceProvider(new()), clock);
        var owner = new FolderFileOwnerReadQueryHandler(layered, accessor, Store);
        using var services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<IDomainQueryHandler>(owner).AddSingleton<IDomainQueryHandler>(new FolderFileReferenceReadQueryHandler(owner)).BuildServiceProvider();
        return await DomainQueryDispatcher.ExecuteAsync(services, query, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
