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

    [Theory]
    [InlineData("malformed-json", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("empty-payload", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("non-object", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("nonempty-object", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("mismatched-entity", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("missing-entity", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("paging", FolderPermissionEvidenceStatus.Stale)]
    [InlineData("malformed-json", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("empty-payload", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("non-object", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("nonempty-object", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("mismatched-entity", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("missing-entity", FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData("paging", FolderPermissionEvidenceStatus.Unavailable)]
    public async Task VerifiedMalformedShapeIsRejectedBeforeReadingCurrentOwnerEvidence(
        string shape, FolderPermissionEvidenceStatus evidenceStatus)
    {
        (QueryResult result, int evidenceReads) = await DispatchWithEvidenceAsync(
            QueryWithShape(shape), evidenceStatus, verified: true);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        result.PayloadBytes.ShouldBeNull();
        evidenceReads.ShouldBe(0);
    }

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("empty-payload")]
    [InlineData("non-object")]
    [InlineData("nonempty-object")]
    [InlineData("mismatched-entity")]
    [InlineData("missing-entity")]
    [InlineData("paging")]
    public async Task UnverifiedMalformedQueryRetainsSafeDenial(string shape)
    {
        (QueryResult result, int evidenceReads) = await DispatchWithEvidenceAsync(
            QueryWithShape(shape), FolderPermissionEvidenceStatus.Unavailable, verified: false);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("safe-denial");
        result.PayloadBytes.ShouldBeNull();
        evidenceReads.ShouldBe(0);
    }

    [Fact]
    public async Task HandlerRejectsUnverifiedMalformedQueryBeforeShapeValidation()
    {
        (QueryResult result, int evidenceReads) = await DispatchWithEvidenceAsync(
            QueryWithShape("malformed-json"), FolderPermissionEvidenceStatus.Unavailable, verified: false, direct: true);

        result.ShouldBe(QueryResult.Failure("safe-denial"));
        evidenceReads.ShouldBe(0);
    }

    [Theory]
    [InlineData(FolderPermissionEvidenceStatus.Stale, FolderOwnerReadOutcome.Stale)]
    [InlineData(FolderPermissionEvidenceStatus.Unavailable, FolderOwnerReadOutcome.Unavailable)]
    public async Task ValidBoundQueryRetainsCurrentOwnerEvidenceOutcome(
        FolderPermissionEvidenceStatus evidenceStatus, FolderOwnerReadOutcome expectedOutcome)
    {
        (QueryResult result, int evidenceReads) = await DispatchWithEvidenceAsync(
            QueryWithShape("valid"), evidenceStatus, verified: true);

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe(FolderOwnerReadQuery.QueryType);
        var payload = JsonSerializer.Deserialize<FolderOwnerReadResult>(result.PayloadBytes!, JsonSerializerOptions.Web)!;
        payload.Outcome.ShouldBe(expectedOutcome);
        payload.Evidence.ShouldBeNull();
        evidenceReads.ShouldBe(1);
    }

    private static QueryEnvelope QueryWithShape(string shape)
    {
        QueryEnvelope query = new("tenant-a", "folders", "folder-a", FolderOwnerReadQuery.QueryType,
            "{}"u8.ToArray(), "corr", "actor")
        {
            EntityId = "folder-a",
            OriginalActorId = "actor",
            IsDelegated = true,
            CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]),
        };
        return shape switch
        {
            "malformed-json" => query with { Payload = "{"u8.ToArray() },
            "empty-payload" => query with { Payload = [] },
            "non-object" => query with { Payload = "[]"u8.ToArray() },
            "nonempty-object" => query with { Payload = "{\"unexpected\":true}"u8.ToArray() },
            "mismatched-entity" => query with { EntityId = "other-folder" },
            "missing-entity" => query with { EntityId = null },
            "paging" => query with { Paging = new QueryPagingOptions(PageSize: 1) },
            "valid" => query,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    private static async Task<(QueryResult Result, int EvidenceReads)> DispatchWithEvidenceAsync(
        QueryEnvelope query, FolderPermissionEvidenceStatus evidenceStatus, bool verified, bool direct = false)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new FixedUtcClock(FolderFileOwnerReadFixture.Now);
        var tenants = new InMemoryFolderTenantAccessProjectionStore();
        var tenant = new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Watermark = 1,
            ProjectionWatermark = "tenant-a:1",
            LastEventTimestamp = clock.UtcNow,
        };
        tenant.Principals["actor"] = new("actor", "TenantOwner");
        await tenants.SaveAsync(tenant, token).ConfigureAwait(false);
        var evidence = new FolderFilePermissionEvidenceProvider
        {
            Evidence = _ => FolderPermissionEvidenceResult.FromStatus(evidenceStatus, "folder-a:2"),
        };
        var context = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = context };
        List<Claim> claims =
        [
            new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery),
        ];
        if (verified)
        {
            claims.AddRange(QueryCallerAuthorityBinding.Bind(query).Select(pair => new Claim(pair.Key, pair.Value)));
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "workload"));
        var layered = new LayeredFolderAuthorizationService(new TenantAccessAuthorizer(tenants, clock, new()),
            evidence, new VerifiedReceiptEventStoreAuthorizationValidator(accessor),
            new ConfigurationDaprPolicyEvidenceProvider(new()), clock);
        FolderOwnerReadQueryHandler handler = new(layered, accessor);
        using ServiceProvider services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<IDomainQueryHandler>(handler).BuildServiceProvider();

        QueryResult result = direct
            ? await handler.ExecuteAsync(query, token).ConfigureAwait(false)
            : await DomainQueryDispatcher.ExecuteAsync(services, query, token).ConfigureAwait(false);

        VerifiedQueryCaller.Get(context).ShouldBeNull();
        return (result, evidence.Checks);
    }
}
