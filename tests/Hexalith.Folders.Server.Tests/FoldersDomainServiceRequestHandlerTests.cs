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
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions["code"].ShouldBe("safe_not_found");
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
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest(commandTenantId: "tenant-b", userId: "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Extensions["code"].ShouldBe("claim_transform_denied");
    }

    [Fact]
    public async Task ProcessShouldDenyWhenAuthoritativeTenantIsMissing()
    {
        FakeTenantContextAccessor tenantContext = new(authoritativeTenantId: null, principalId: "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.ProblemDetails.Extensions["code"].ShouldBe("authentication_denied");
    }

    [Fact]
    public async Task ProcessShouldReturn501WhenNoDomainProcessorIsRegistered()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new([], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status501NotImplemented);
    }

    [Fact]
    public async Task ProcessShouldNotInvokeDomainProcessorWhenFolderAclEvidenceDenies()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        CountingDomainProcessor processor = new();
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(
            store,
            new FixedFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.FromStatus(
                FolderPermissionEvidenceStatus.Denied,
                "folder_watermark_v1")));
        FoldersDomainServiceRequestHandler handler = new([processor], authorizer, tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions["code"].ShouldBe("folder_acl_denied");
        processor.ProcessCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessShouldReturn500WhenMultipleDomainProcessorsAreRegistered()
    {
        FakeTenantContextAccessor tenantContext = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore store = new();
        await store.SaveAsync(Projection("tenant-a", Now.AddMinutes(-1), enabled: true, principals: ["user-a"]), TestContext.Current.CancellationToken);
        LayeredFolderAuthorizationService authorizer = CreateAuthorizer(store);
        FoldersDomainServiceRequestHandler handler = new(
            [new StubDomainProcessor(), new StubDomainProcessor()],
            authorizer,
            tenantContext);

        IResult result = await handler.ProcessAsync(CreateRequest("tenant-a", "user-a"), TestContext.Current.CancellationToken);

        ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static LayeredFolderAuthorizationService CreateAuthorizer(
        IFolderTenantAccessProjectionStore store,
        IFolderPermissionEvidenceProvider? folderPermissionEvidenceProvider = null)
        => new(
            new TenantAccessAuthorizer(store, new FixedUtcClock(Now), new TenantAccessOptions()),
            folderPermissionEvidenceProvider ?? new AllowingFolderPermissionEvidenceProvider(),
            new AllowingEventStoreAuthorizationValidator(),
            new ConfigurationDaprPolicyEvidenceProvider(new DaprPolicyEvidenceOptions()),
            new FixedUtcClock(Now));

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

    private sealed class CountingDomainProcessor : IDomainProcessor
    {
        public int ProcessCalls { get; private set; }

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        {
            ProcessCalls++;
            return Task.FromResult(DomainResult.NoOp());
        }
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
    }

    private sealed class FixedFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult result) : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
