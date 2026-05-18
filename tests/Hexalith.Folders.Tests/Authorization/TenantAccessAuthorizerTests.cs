using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class TenantAccessAuthorizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MutationShouldBeAllowedOnlyWithFreshEnabledMembershipEvidence()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), cancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);

        TenantAccessAuthorizationResult result = await authorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext("tenant-a", "user-a", "tenant-a"),
            cancellationToken);

        result.Outcome.ShouldBe(TenantAccessOutcome.Allowed);
        result.Code.ShouldBe("allowed");
        result.FreshnessStatus.ShouldBe(TenantProjectionFreshnessStatus.Fresh);
        result.ProjectionWatermark.ShouldBe("tenant-a:7");
    }

    [Theory]
    [InlineData(null, "user-a", "tenant-a", TenantAccessOutcome.MissingAuthoritativeTenant)]
    [InlineData("tenant-a", "user-a", "tenant-b", TenantAccessOutcome.TenantMismatch)]
    [InlineData("tenant-a", "user-b", "tenant-a", TenantAccessOutcome.Denied)]
    public async Task MutationShouldRejectInvalidAuthorityOrPrincipal(
        string? authoritativeTenantId,
        string principalId,
        string requestedTenantId,
        TenantAccessOutcome expected)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), cancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);

        TenantAccessAuthorizationResult result = await authorizer.AuthorizeMutationAsync(
            new TenantAccessAuthorizationContext(authoritativeTenantId, principalId, requestedTenantId),
            cancellationToken);

        result.Outcome.ShouldBe(expected);
    }

    [Fact]
    public async Task MutationShouldFailClosedForStaleDisabledReplayConflictOrUnavailableProjection()
    {
        InMemoryFolderTenantAccessProjectionStore staleStore = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await staleStore.SaveAsync(Projection("tenant-a", Now.AddMinutes(-10), enabled: true, principals: ["user-a"]), cancellationToken);

        InMemoryFolderTenantAccessProjectionStore disabledStore = new();
        await disabledStore.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: false, principals: ["user-a"]), cancellationToken);

        InMemoryFolderTenantAccessProjectionStore conflictStore = new();
        await conflictStore.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, replayConflict: true, principals: ["user-a"]), cancellationToken);

        (TenantAccessAuthorizer Authorizer, TenantAccessOutcome Expected)[] cases =
        [
            (CreateAuthorizer(staleStore), TenantAccessOutcome.StaleProjection),
            (CreateAuthorizer(disabledStore), TenantAccessOutcome.DisabledTenant),
            (CreateAuthorizer(conflictStore), TenantAccessOutcome.ReplayConflict),
            (CreateAuthorizer(new ThrowingFolderTenantAccessProjectionStore()), TenantAccessOutcome.UnavailableProjection),
        ];

        foreach ((TenantAccessAuthorizer authorizer, TenantAccessOutcome expected) in cases)
        {
            TenantAccessAuthorizationResult result = await authorizer.AuthorizeMutationAsync(
                new TenantAccessAuthorizationContext("tenant-a", "user-a", "tenant-a"),
                cancellationToken);

            result.Outcome.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task DiagnosticReadShouldAllowBoundedStaleProjectionWithFreshnessMetadata()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-10), enabled: true, principals: ["user-a"]), cancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);

        TenantAccessAuthorizationResult result = await authorizer.AuthorizeDiagnosticReadAsync(
            new TenantAccessAuthorizationContext("tenant-a", "user-a", "tenant-a"),
            cancellationToken);

        result.Outcome.ShouldBe(TenantAccessOutcome.Allowed);
        result.FreshnessStatus.ShouldBe(TenantProjectionFreshnessStatus.Stale);
        result.ProjectionAge.ShouldBe(TimeSpan.FromMinutes(10));
        result.Source.ShouldBe("local-projection");
    }

    private static TenantAccessAuthorizer CreateAuthorizer(IFolderTenantAccessProjectionStore store)
        => new(store, new FixedUtcClock(Now), new TenantAccessOptions());

    private static FolderTenantAccessProjection Projection(
        string tenantId,
        DateTimeOffset lastEventTimestamp,
        bool enabled,
        bool replayConflict = false,
        params string[] principals)
    {
        Dictionary<string, FolderTenantPrincipalEvidence> principalEvidence = principals
            .ToDictionary(
                static principal => principal,
                static principal => new FolderTenantPrincipalEvidence(principal, "Member"),
                StringComparer.Ordinal);

        return new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = enabled,
            Principals = principalEvidence,
            Watermark = 7,
            LastEventTimestamp = lastEventTimestamp,
            ProjectionWatermark = "tenant-a:7",
            ReplayConflict = replayConflict,
        };
    }

    private sealed class ThrowingFolderTenantAccessProjectionStore : IFolderTenantAccessProjectionStore
    {
        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("store unavailable");

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("store unavailable");
    }
}
