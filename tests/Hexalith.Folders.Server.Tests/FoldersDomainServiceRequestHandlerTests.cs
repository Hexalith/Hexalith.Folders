using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FoldersDomainServiceRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessShouldDenyWhenAuthoritativeTenantIsNotPresentInProjection()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Extensions["code"].ShouldBe("unknown_tenant");
        // Defense in depth: the deny response must not leak tenant or projection metadata.
        problem.ProblemDetails.Extensions.ShouldNotContainKey("tenantId");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("projectionWatermark");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("lastEventTimestamp");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("projectionAge");
    }

    [Fact]
    public async Task ProcessShouldRejectMutationWhenAuthenticatedTenantDiffersFromRequestBodyTenant()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest(commandTenantId: "tenant-b", userId: "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Extensions["code"].ShouldBe("tenant_mismatch");
    }

    [Fact]
    public async Task ProcessShouldDenyWhenAuthoritativeTenantIsMissing()
    {
        FakeTenantContextAccessor tenantContext = new(authoritativeTenantId: null, principalId: "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Extensions["code"].ShouldBe("missing_authoritative_tenant");
    }

    [Fact]
    public async Task ProcessShouldReturn501WhenNoDomainProcessorIsRegistered()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status501NotImplemented);
    }

    [Fact]
    public async Task ProcessShouldReturn500WhenMultipleDomainProcessorsAreRegistered()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        TenantAccessAuthorizer authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new(
            [new StubDomainProcessor(), new StubDomainProcessor()],
            authorizer,
            tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static TenantAccessAuthorizer CreateAuthorizer(IFolderTenantAccessProjectionStore store)
        => new(store, new FixedUtcClock(Now), new TenantAccessOptions());

    private static FolderTenantAccessProjection Projection(
        string tenantId,
        DateTimeOffset lastEventTimestamp,
        bool enabled,
        params string[] principals)
    {
        Dictionary<string, FolderTenantPrincipalEvidence> principalEvidence = principals.ToDictionary(
            static p => p,
            static p => new FolderTenantPrincipalEvidence(p, "Member"),
            StringComparer.Ordinal);

        return new FolderTenantAccessProjection
        {
            TenantId = tenantId,
            Enabled = enabled,
            Principals = principalEvidence,
            Watermark = 1,
            LastEventTimestamp = lastEventTimestamp,
            ProjectionWatermark = $"{tenantId}:1",
        };
    }

    private static DomainServiceRequest CreateRequest(string commandTenantId, string userId)
        => new(
            new CommandEnvelope(
                MessageId: "01J00000000000000000000001",
                TenantId: commandTenantId,
                Domain: "folders",
                AggregateId: "agg-1",
                CommandType: "Hexalith.Folders.Commands.TestCommand",
                Payload: [0x01],
                CorrelationId: "corr-1",
                CausationId: null,
                UserId: userId,
                Extensions: null),
            CurrentState: null);

    private sealed class FakeTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId { get; } = authoritativeTenantId;

        public string? PrincipalId { get; } = principalId;
    }

    private sealed class StubDomainProcessor : IDomainProcessor
    {
        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
            => Task.FromResult(DomainResult.NoOp());
    }
}
