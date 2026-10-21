using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Services.Auth;
using Hexalith.Memories.Client.Rest;
using Hexalith.Tenants.Contracts;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantUsers;
using Hexalith.Tenants.UI.State.UserTenants;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MemoriesSearchResult = Hexalith.Memories.Contracts.V1.SearchResult;
using MemoriesScoredResult = Hexalith.Memories.Contracts.V1.ScoredResult;
using MemoriesSourceType = Hexalith.Memories.Contracts.V1.SourceType;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Services.Gateways;

public sealed class TenantQueryGatewayTests
{
    [Fact]
    public async Task My_tenants_never_retargets_a_scoped_read_during_an_A_B_A_context_sequence()
    {
        int reads = 0;
        IUserContextAccessor context = Substitute.For<IUserContextAccessor>();
        context.UserId.Returns(_ => ++reads == 3 ? "caller-two" : "caller-one");
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("private-tenant", "Private", TenantStatus.Active, TenantRole.TenantOwner)], null, false)));

        UserTenantMembershipSnapshot result = await CreateGateway(client, userContextAccessor: context)
            .GetMyTenantsAsync(new UserTenantMembershipRequest(), null);

        _ = client.DidNotReceive().GetUserTenantsAsync(Arg.Is<GetUserTenantsQuery>(query => query.UserId == "caller-two"),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        result.Rows.ShouldBeEmpty();
        result.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task Search_scope_uses_the_captured_subject_during_an_A_B_A_context_sequence()
    {
        int reads = 0;
        IUserContextAccessor context = Substitute.For<IUserContextAccessor>();
        context.UserId.Returns(_ => ++reads == 4 ? "caller-two" : "caller-one");
        ThrowingSearchCursorCodec codec = new();
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 0));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();

        TenantListSnapshot result = await CreateGateway(client, userContextAccessor: context, memoriesClient: memories, searchCursorCodec: codec)
            .ListTenantsAsync(new TenantListRequest(Search: "needle"), null);

        codec.DecodedScope.ShouldBe(TenantSearchCursorScopes.Create("caller-one", "needle", null,
            TenantListSortColumns.TenantId, false, 20));
        result.Rows.ShouldBeEmpty();
        result.IsAuthoritativeSearch.ShouldBeFalse();
    }

    [Theory]
    [InlineData("matching", true)]
    [InlineData("different", false)]
    [InlineData("anonymous", false)]
    [InlineData("pending", false)]
    [InlineData("faulted", false)]
    [InlineData("missing-active-provider", false)]
    [InlineData("circuit-preferred", true)]
    [InlineData("circuit-disagrees", false)]
    public async Task Gateway_corroborates_request_context_with_authoritative_circuit_identity(string scenario, bool allowed)
    {
        AuthenticationStateProvider provider = GatewayAuthenticationProvider("caller-one");
        AuthenticationStateProvider injected = GatewayAuthenticationProvider("caller-one");
        Task<AuthenticationState> state = scenario switch
        {
            "different" or "circuit-disagrees" => Task.FromResult(new AuthenticationState(GatewayPrincipal("caller-two"))),
            "anonymous" => Task.FromResult(new AuthenticationState(new ClaimsPrincipal())),
            "pending" => new TaskCompletionSource<AuthenticationState>().Task,
            "faulted" => Task.FromException<AuthenticationState>(new InvalidOperationException("Identity unavailable")),
            _ => Task.FromResult(new AuthenticationState(GatewayPrincipal("caller-one"))),
        };
        provider.GetAuthenticationStateAsync().Returns(state);
        if (scenario == "circuit-preferred")
        {
            injected.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
        }
        using ServiceProvider circuit = scenario == "missing-active-provider"
            ? new ServiceCollection().BuildServiceProvider()
            : new ServiceCollection().AddSingleton(provider).BuildServiceProvider();
        var accessor = new CircuitServicesAccessor { Services = circuit };
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(() => "200", validators, () => Task.CompletedTask),
            userId: "caller-one", circuitServicesAccessor: accessor, authenticationStateProvider: injected);

        TenantDetailSnapshot result = await gateway.GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null);

        (result.Detail is not null).ShouldBe(allowed);
        validators.Count.ShouldBe(allowed ? 1 : 0);
        if (!allowed)
        {
            result.ETag.ShouldBeNull();
            result.Configuration.IsAvailable.ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("provider-only", false)]
    [InlineData("no-provider", true)]
    public async Task Direct_request_operation_is_preserved_only_when_no_authoritative_provider_exists(string scenario, bool allowed)
    {
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(() => "200", validators, () => Task.CompletedTask),
            userId: "caller-one", authenticationStateProvider: scenario == "provider-only" ? GatewayAuthenticationProvider("caller-two") : null);

        TenantDetailSnapshot result = await gateway.GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null);

        (result.Detail is not null).ShouldBe(allowed);
        validators.Count.ShouldBe(allowed ? 1 : 0);
    }

    [Theory]
    [MemberData(nameof(CallerBoundReadCases))]
    public async Task Authentication_events_reject_active_reads_even_when_subject_returns_to_A(string surface, string outcome)
    {
        AuthenticationStateProvider provider = GatewayAuthenticationProvider("caller-one");
        string response = "200";
        Task responseGate = Task.CompletedTask;
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(() => response, validators, () => responseGate),
            userId: "caller-one", authenticationStateProvider: provider);
        object previous = await ReadCallerBoundSnapshotAsync(surface, gateway);
        validators.Clear();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        responseGate = pending.Task;
        response = outcome;
        Task<object> read = ReadCallerBoundSnapshotAsync(surface, gateway, previous, "confirmed-etag");
        NotifyGatewayAuthentication(provider, Task.FromResult(new AuthenticationState(GatewayPrincipal("caller-two"))));
        NotifyGatewayAuthentication(provider, Task.FromResult(new AuthenticationState(GatewayPrincipal("caller-one"))));
        pending.SetResult();

        object result = await read;

        CallerBoundSnapshotFacts(result).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(result).ETag.ShouldBeNull();
        validators.ShouldHaveSingleItem();
        response = "200";
        CallerBoundSnapshotFacts(await ReadCallerBoundSnapshotAsync(surface, gateway)).Count.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("compose", "changed")]
    [InlineData("compose", "signed-out")]
    [InlineData("compose", "faulted")]
    [InlineData("reauthorize", "changed")]
    [InlineData("reauthorize", "signed-out")]
    [InlineData("reauthorize", "faulted")]
    public async Task Final_publication_withholds_detail_and_configuration_after_pending_BFF_identity_change(string path, string transition)
    {
        string? userId = "operator-user";
        bool faulted = false;
        IUserContextAccessor context = Substitute.For<IUserContextAccessor>();
        context.UserId.Returns(_ => faulted ? throw new InvalidOperationException("Identity unavailable") : userId);
        var pending = new TaskCompletionSource<TenantConfigurationComposition>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<TenantConfigurationComposition>(pending.Task));
        composition.ReauthorizeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<TenantConfigurationSafeModel>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<TenantConfigurationComposition>(pending.Task));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(Detail("tenant.alpha"), "new-validator") with
            { Metadata = ProjectionBackedMetadata(isStale: false, isDegraded: path == "reauthorize", eTag: "new-validator", projectionVersion: "tenant-sequence:42") });
        TenantDetailSnapshot? prior = path == "reauthorize" ? ReadyConfigurationSnapshot() : null;
        Task<TenantDetailSnapshot> read = CreateGateway(client, userContextAccessor: context, bffComposition: composition)
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), prior);
        if (path == "compose")
        {
            _ = composition.Received(1).ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<CancellationToken>());
        }
        else
        {
            _ = composition.Received(1).ReauthorizeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<TenantConfigurationSafeModel>(), true, Arg.Any<CancellationToken>());
        }
        userId = transition == "signed-out" ? null : "caller-two";
        faulted = transition == "faulted";
        pending.SetResult(new TenantConfigurationComposition(
            TenantConfigurationSafeComposer.SanitizeDetail(Detail("tenant.alpha")),
            TenantConfigurationSafeModel.Available("tenant.alpha", [new TenantConfigurationSafeRow("billing", "billing.mode", "old-private-value")]),
            TenantConfigurationManagementContext.Unavailable("tenant.alpha")));

        TenantDetailSnapshot result = await read;

        result.Detail.ShouldBeNull();
        result.Configuration.IsAvailable.ShouldBeFalse();
        result.ConfigurationManagement.IsAvailable.ShouldBeFalse();
        result.ETag.ShouldBeNull();
        result.ProjectionVersion.ShouldBeNull();
    }

    [Theory]
    [InlineData("preview-set")]
    [InlineData("preview-remove")]
    [InlineData("proof-set")]
    [InlineData("proof-remove")]
    [InlineData("legacy-set")]
    [InlineData("legacy-remove")]
    public async Task Configuration_fresh_reads_reject_authentication_events_during_their_await(string operation)
    {
        AuthenticationStateProvider provider = GatewayAuthenticationProvider("operator-user");
        var pending = new TaskCompletionSource<TenantsRestQueryResponse<TenantDetail>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson), authenticationStateProvider: provider);
        TenantSetConfigurationIntent set = new("tenant.alpha", "billing", "mode", "billing.mode", TenantSetConfigurationValueFingerprint.Create("trial"));
        TenantRemoveConfigurationIntent remove = new("tenant.alpha", "billing", "billing.mode");
        Task<object> read = ReadConfigurationAsync();
        _ = client.Received(1).GetTenantAsync(Arg.Any<GetTenantQuery>(), null, Arg.Any<CancellationToken>());
        NotifyGatewayAuthentication(provider, Task.FromResult(new AuthenticationState(GatewayPrincipal("operator-user"))));
        pending.SetResult(DirectResponse(Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }), "validator", "tenant-sequence:42"));

        object proof = await read;

        switch (proof)
        {
            case TenantSetConfigurationPreview preview: preview.IsComplete.ShouldBeFalse(); preview.ProjectionVersion.ShouldBeNull(); break;
            case TenantRemoveConfigurationPreview preview: preview.IsAuthoritative.ShouldBeFalse(); preview.ProjectionVersion.ShouldBeNull(); break;
            case TenantConfigurationProjectionProof projection: projection.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable); projection.ProjectionVersion.ShouldBeNull(); break;
        }
        client.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantsRestQueryClient.GetTenantAsync)).ShouldBe(1);

        async Task<object> ReadConfigurationAsync() => operation switch
        {
            "preview-set" => await gateway.GetSetConfigurationPreviewAsync(set),
            "preview-remove" => await gateway.GetRemoveConfigurationPreviewAsync(remove),
            "proof-set" => await gateway.GetSetConfigurationProjectionProofAsync(set),
            "proof-remove" => await gateway.GetRemoveConfigurationProjectionProofAsync(remove),
            "legacy-set" => await gateway.GetSetConfigurationProjectionProofAsync(new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial")),
            _ => await gateway.GetRemoveConfigurationProjectionProofAsync(new RemoveTenantConfiguration("tenant.alpha", "billing.mode")),
        };
    }

    [Theory]
    [InlineData("preview-set")]
    [InlineData("preview-remove")]
    [InlineData("proof-set")]
    [InlineData("proof-remove")]
    [InlineData("legacy-set")]
    [InlineData("legacy-remove")]
    public async Task Configuration_policy_and_preview_publication_reject_authentication_events_while_BFF_is_pending(string operation)
    {
        AuthenticationStateProvider provider = GatewayAuthenticationProvider("operator-user");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition.IsConfigurationKeyAuthorizedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<bool>(AwaitPolicyAsync()));
        composition.ComposeSetConfigurationPreviewAsync(Arg.Any<TenantDetail>(), Arg.Any<TenantSetConfigurationIntent>(),
                Arg.Any<ReadModelFreshnessState>(), Arg.Any<ProjectionLifecycleState>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<TenantSetConfigurationPreview>(AwaitSetAsync(call.Arg<TenantSetConfigurationIntent>())));
        composition.ComposeRemoveConfigurationPreviewAsync(Arg.Any<TenantDetail>(), Arg.Any<TenantRemoveConfigurationIntent>(),
                Arg.Any<ReadModelFreshnessState>(), Arg.Any<ProjectionLifecycleState>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<TenantRemoveConfigurationPreview>(AwaitRemoveAsync(call.Arg<TenantRemoveConfigurationIntent>())));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(Detail("tenant.alpha"), "validator", "tenant-sequence:42"));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition, authenticationStateProvider: provider);
        TenantSetConfigurationIntent set = new("tenant.alpha", "billing", "mode", "billing.mode", TenantSetConfigurationValueFingerprint.Create("trial"));
        TenantRemoveConfigurationIntent remove = new("tenant.alpha", "billing", "billing.mode");
        Task<object> read = ReadConfigurationAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        NotifyGatewayAuthentication(provider, Task.FromResult(new AuthenticationState(GatewayPrincipal("operator-user"))));
        pending.SetResult();

        object proof = await read;

        switch (proof)
        {
            case TenantSetConfigurationPreview preview: preview.IsComplete.ShouldBeFalse(); preview.ProjectionVersion.ShouldBeNull(); break;
            case TenantRemoveConfigurationPreview preview: preview.IsAuthoritative.ShouldBeFalse(); preview.ProjectionVersion.ShouldBeNull(); break;
            case TenantConfigurationProjectionProof projection: projection.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable); projection.ProjectionVersion.ShouldBeNull(); break;
        }
        client.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantsRestQueryClient.GetTenantAsync))
            .ShouldBe(operation is "legacy-set" or "legacy-remove" ? 0 : 1);

        async Task<bool> AwaitPolicyAsync() { entered.SetResult(); await pending.Task; return true; }
        async Task<TenantSetConfigurationPreview> AwaitSetAsync(TenantSetConfigurationIntent intent)
        {
            entered.SetResult(); await pending.Task;
            return TenantSetConfigurationPreview.Create(intent, TenantStatus.Active, TenantSetConfigurationCurrentState.Matching,
                ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, "tenant-sequence:42", true);
        }
        async Task<TenantRemoveConfigurationPreview> AwaitRemoveAsync(TenantRemoveConfigurationIntent intent)
        {
            entered.SetResult(); await pending.Task;
            return TenantRemoveConfigurationPreview.Create(intent, TenantStatus.Active, TenantRemoveConfigurationCurrentState.Present,
                ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, "tenant-sequence:42", true);
        }
        async Task<object> ReadConfigurationAsync() => operation switch
        {
            "preview-set" => await gateway.GetSetConfigurationPreviewAsync(set),
            "preview-remove" => await gateway.GetRemoveConfigurationPreviewAsync(remove),
            "proof-set" => await gateway.GetSetConfigurationProjectionProofAsync(set),
            "proof-remove" => await gateway.GetRemoveConfigurationProjectionProofAsync(remove),
            "legacy-set" => await gateway.GetSetConfigurationProjectionProofAsync(new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial")),
            _ => await gateway.GetRemoveConfigurationProjectionProofAsync(new RemoveTenantConfiguration("tenant.alpha", "billing.mode")),
        };
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("same-subject-event")]
    [InlineData("signed-out")]
    [InlineData("pending")]
    public async Task Memories_completion_cannot_send_recovery_or_hydration_after_authentication_transition(string transition)
    {
        AuthenticationStateProvider provider = GatewayAuthenticationProvider("operator-user");
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string cursor = codec.Encode(TenantSearchCursorScopes.Create("operator-user", "needle", null, TenantListSortColumns.TenantId, false, 20), 50);
        var pending = new TaskCompletionSource<MemoriesSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StubMemoriesClient memories = new();
        memories.Enqueue(pending.Task);
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        Task<TenantListSnapshot> read = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec, authenticationStateProvider: provider)
            .ListTenantsAsync(new TenantListRequest(Search: "needle", SearchCursor: cursor), null);
        Task<AuthenticationState> state = transition switch
        {
            "signed-out" => Task.FromResult(new AuthenticationState(new ClaimsPrincipal())),
            "pending" => new TaskCompletionSource<AuthenticationState>().Task,
            _ => Task.FromResult(new AuthenticationState(GatewayPrincipal(transition == "changed" ? "caller-two" : "operator-user"))),
        };
        NotifyGatewayAuthentication(provider, state);
        pending.SetResult(SearchResult("needle", 1)); // Would otherwise request recovery page zero.

        TenantListSnapshot result = await read;

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(50);
        client.ReceivedCalls().ShouldBeEmpty();
        result.Rows.ShouldBeEmpty();
        result.ETag.ShouldBeNull();
        result.IsAuthoritativeSearch.ShouldBeFalse();
    }

    private static ClaimsPrincipal GatewayPrincipal(string subject)
        => new(new ClaimsIdentity([new Claim("sub", subject), new Claim("global_admin", "false")], "test"));

    private static AuthenticationStateProvider GatewayAuthenticationProvider(string subject)
    {
        AuthenticationStateProvider provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(GatewayPrincipal(subject))));
        return provider;
    }

    private static void NotifyGatewayAuthentication(AuthenticationStateProvider provider, Task<AuthenticationState> state)
    {
        provider.GetAuthenticationStateAsync().Returns(state);
        typeof(AuthenticationStateProvider).GetMethod("NotifyAuthenticationStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(provider, [state]);
    }

    public static IEnumerable<object[]> CallerBoundReadCases()
        => new[] { "detail", "members", "memberships", "self-memberships", "administrators", "list", "audit" }
            .SelectMany(surface => new[] { "304", "throw" }.Select(outcome => new object[] { surface, outcome }));

    [Theory]
    [MemberData(nameof(CallerBoundReadCases))]
    public async Task Retained_reads_discard_other_caller_payloads_and_validators(string surface, string outcome)
    {
        string? userId = "caller-one";
        string responseKind = "200";
        var validators = new List<string?>();
        ITenantsRestQueryClient client = CallerBoundReadClient(
            () => responseKind, validators, () => Task.CompletedTask);
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => userId);
        TenantQueryGateway gateway = CreateGateway(client, userContextAccessor: userContext);
        object previous = await ReadCallerBoundSnapshotAsync(surface, gateway);
        CallerBoundSnapshotFacts(previous).Count.ShouldBeGreaterThan(0);
        CallerBoundSnapshotFacts(previous).Scope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-one"));
        validators.Clear();

        userId = "caller-two";
        responseKind = outcome;
        object failed = await ReadCallerBoundSnapshotAsync(surface, gateway, previous, "confirmed-etag");

        CallerBoundSnapshotFacts(failed).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(failed).ETag.ShouldBeNull();
        validators.ShouldNotBeEmpty();
        validators.ShouldAllBe(validator => validator == null);

        responseKind = "200";
        object reacquired = await ReadCallerBoundSnapshotAsync(surface, gateway, failed);
        CallerBoundSnapshotFacts(reacquired).Count.ShouldBeGreaterThan(0);
        CallerBoundSnapshotFacts(reacquired).Scope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-two"));
    }

    [Theory]
    [MemberData(nameof(CallerBoundReadCases))]
    public async Task Retained_reads_require_explicit_caller_provenance_even_for_the_same_user(string surface, string outcome)
    {
        string responseKind = "200";
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(() => responseKind, validators, () => Task.CompletedTask));
        object bound = await ReadCallerBoundSnapshotAsync(surface, gateway);
        object unbound = bound switch
        {
            TenantDetailSnapshot detail => TenantDetailSnapshot.Ready(detail.Detail!, detail.ETag, detail.Freshness, detail.Lifecycle, detail.ProjectionVersion),
            TenantUsersSnapshot members => members with { CallerScope = null },
            UserTenantMembershipSnapshot memberships => memberships with { CallerScope = null },
            GlobalAdministratorsSnapshot administrators => administrators with { CallerScope = null },
            TenantListSnapshot list => list with { CallerScope = null },
            TenantAuditSnapshot audit => audit with { CallerScope = null },
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };
        validators.Clear();
        responseKind = outcome;

        object result = await ReadCallerBoundSnapshotAsync(surface, gateway, unbound, "confirmed-etag");

        CallerBoundSnapshotFacts(result).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(result).ETag.ShouldBeNull();
        validators.ShouldAllBe(validator => validator == null);
    }

    [Theory]
    [MemberData(nameof(CallerBoundReadCases))]
    public async Task Retained_reads_keep_qualified_same_caller_payloads(string surface, string outcome)
    {
        string responseKind = "200";
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(
            () => responseKind, validators, () => Task.CompletedTask));
        object previous = await ReadCallerBoundSnapshotAsync(surface, gateway);
        responseKind = outcome;

        object retained = await ReadCallerBoundSnapshotAsync(surface, gateway, previous, "confirmed-etag");

        CallerBoundSnapshotFacts(retained).Count.ShouldBe(CallerBoundSnapshotFacts(previous).Count);
        CallerBoundSnapshotFacts(retained).ETag.ShouldBe("confirmed-etag");
        CallerBoundSnapshotFacts(retained).Scope.ShouldBe(TenantQueryGateway.AuditCallerScope("operator-user"));
    }

    public static IEnumerable<object[]> CallerTransitionReadCases()
        => new[] { "detail", "members", "memberships", "self-memberships", "administrators", "list", "audit" }
            .SelectMany(surface => new[] { "200", "304", "throw", "invalid-cursor" }
                .SelectMany(outcome => new[] { "caller-two", "signed-out", "identity-fault" }
                    .Select(nextCaller => new object[] { surface, outcome, nextCaller })));

    [Theory]
    [MemberData(nameof(CallerTransitionReadCases))]
    public async Task Retained_reads_reject_caller_changes_during_response_failure_or_recovery(
        string surface, string outcome, string nextCaller)
    {
        string? userId = "caller-one";
        bool identityFault = false;
        string responseKind = "200";
        Task responseGate = Task.CompletedTask;
        var validators = new List<string?>();
        ITenantsRestQueryClient client = CallerBoundReadClient(() => responseKind, validators, () => responseGate);
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => identityFault ? throw new InvalidOperationException("Identity unavailable") : userId);
        TenantQueryGateway gateway = CreateGateway(client, userContextAccessor: userContext);
        object previous = await ReadCallerBoundSnapshotAsync(surface, gateway);
        validators.Clear();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        responseGate = pending.Task;
        responseKind = outcome;
        Task<object> load = ReadCallerBoundSnapshotAsync(surface, gateway, previous, "confirmed-etag", "expired-cursor");
        validators.ShouldHaveSingleItem();

        userId = nextCaller == "signed-out" ? null : "caller-two";
        identityFault = nextCaller == "identity-fault";
        pending.SetResult();
        object rejected = await load;

        CallerBoundSnapshotFacts(rejected).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(rejected).ETag.ShouldBeNull();
        validators.Count.ShouldBe(1); // No 304 or cursor follow-up may run under the changed caller.
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("members")]
    [InlineData("memberships")]
    [InlineData("self-memberships")]
    [InlineData("administrators")]
    [InlineData("list")]
    [InlineData("audit")]
    public async Task Retained_reads_contain_initial_identity_faults_without_egress(string surface)
    {
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => throw new InvalidOperationException("Identity unavailable"));
        var validators = new List<string?>();
        TenantQueryGateway gateway = CreateGateway(
            CallerBoundReadClient(() => "200", validators, () => Task.CompletedTask),
            userContextAccessor: userContext);

        object snapshot = await ReadCallerBoundSnapshotAsync(surface, gateway);

        CallerBoundSnapshotFacts(snapshot).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(snapshot).ETag.ShouldBeNull();
        validators.ShouldBeEmpty();
    }

    private static ITenantsRestQueryClient CallerBoundReadClient(
        Func<string> responseKind, List<string?> validators, Func<Task> responseGate)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(Detail("tenant.alpha"), call.ArgAt<string?>(1)));
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(new PaginatedResult<TenantMember>(
                [new TenantMember("member-confirmed", TenantRole.TenantReader)], null, false), call.ArgAt<string?>(1)));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader)], null, false), call.ArgAt<string?>(1)));
        client.GetGlobalAdministratorsAsync(Arg.Any<GetGlobalAdministratorsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(new PaginatedResult<GlobalAdministratorSummary>(
                [new GlobalAdministratorSummary("administrator-confirmed")], null, false), call.ArgAt<string?>(1)));
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(new PaginatedResult<TenantSummary>(
                [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)], null, false), call.ArgAt<string?>(1)));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => RespondAsync(new PaginatedResult<TenantAuditEntry>(
                [AuditEntry("event-confirmed", AuditEventCategory.Access)], null, false), call.ArgAt<string?>(1)));
        return client;

        async Task<TenantsRestQueryResponse<T>> RespondAsync<T>(T payload, string? validator)
        {
            validators.Add(validator);
            await responseGate();
            return responseKind() switch
            {
                "304" => NotModifiedResponse<T>("confirmed-etag"),
                "throw" => throw new InvalidOperationException("Read unavailable"),
                "invalid-cursor" => FailureResponse<T>(TenantsRestQueryFailureKind.InvalidCursor, 400),
                _ => DirectResponse(payload, "confirmed-etag"),
            };
        }
    }

    private static async Task<object> ReadCallerBoundSnapshotAsync(
        string surface, TenantQueryGateway gateway, object? previous = null, string? eTag = null, string? cursor = null)
        => surface switch
        {
            "detail" => await gateway.GetTenantAsync(new TenantDetailRequest("tenant.alpha", eTag), previous as TenantDetailSnapshot),
            "members" => await gateway.GetTenantUsersAsync(new TenantUsersRequest("tenant.alpha", cursor, ETag: eTag), previous as TenantUsersSnapshot),
            "memberships" => await gateway.GetUserTenantsAsync(new UserTenantMembershipRequest("target.user", cursor, ETag: eTag), previous as UserTenantMembershipSnapshot),
            "self-memberships" => await gateway.GetMyTenantsAsync(new UserTenantMembershipRequest(Cursor: cursor, ETag: eTag), previous as UserTenantMembershipSnapshot),
            "administrators" => await gateway.GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(cursor, ETag: eTag), previous as GlobalAdministratorsSnapshot),
            "list" => await gateway.ListTenantsAsync(new TenantListRequest(Cursor: cursor, ETag: eTag), previous as TenantListSnapshot),
            "audit" => await gateway.GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha", Cursor: cursor, ETag: eTag), previous as TenantAuditSnapshot),
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };

    private static (int Count, string? ETag, string? Scope) CallerBoundSnapshotFacts(object snapshot)
        => snapshot switch
        {
            TenantDetailSnapshot detail => (detail.Detail is null ? 0 : 1, detail.ETag, detail.CallerScope),
            TenantUsersSnapshot members => (members.Rows.Count, members.ETag, members.CallerScope),
            UserTenantMembershipSnapshot memberships => (memberships.Rows.Count, memberships.ETag, memberships.CallerScope),
            GlobalAdministratorsSnapshot administrators => (administrators.Rows.Count, administrators.ETag, administrators.CallerScope),
            TenantListSnapshot list => (list.Rows.Count, list.ETag, list.CallerScope),
            TenantAuditSnapshot audit => (audit.Rows.Count, audit.ETag, audit.CallerScope),
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };

    [Theory]
    [MemberData(nameof(CallerBoundReadCases))]
    public async Task Observed_indeterminate_identity_cannot_be_redeemed_by_the_original_caller(string surface, string outcome)
    {
        bool failOneIdentityRead = false;
        string responseKind = "200";
        Task responseGate = Task.CompletedTask;
        var validators = new List<string?>();
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ =>
        {
            if (failOneIdentityRead)
            {
                failOneIdentityRead = false;
                return null;
            }
            return "caller-one";
        });
        TenantQueryGateway gateway = CreateGateway(CallerBoundReadClient(() => responseKind, validators, () => responseGate), userContextAccessor: userContext);
        object previous = await ReadCallerBoundSnapshotAsync(surface, gateway);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        responseGate = pending.Task;
        responseKind = outcome;
        Task<object> read = ReadCallerBoundSnapshotAsync(surface, gateway, previous, "confirmed-etag");
        failOneIdentityRead = true;
        pending.SetResult();

        object snapshot = await read;

        CallerBoundSnapshotFacts(snapshot).Count.ShouldBe(0);
        CallerBoundSnapshotFacts(snapshot).ETag.ShouldBeNull();
        CallerBoundSnapshotFacts(snapshot).Scope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-one"));
    }

    [Fact]
    public async Task Global_correction_proof_uses_protected_raw_audit_provenance_while_public_rows_are_redacted()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        DateTimeOffset original = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture);
        DateTimeOffset submitted = original.AddMinutes(1);
        TenantAuditEntry entry = AuditEntry("corrective-event", AuditEventCategory.Access,
            timestamp: submitted.AddMinutes(1), eventType: "GlobalAdministratorSet", tenantId: "system",
            narrative: new Dictionary<string, string> { ["userId"] = "admin-user" }) with
        {
            CommandCorrelationId = "exact-command",
            AggregateSequence = 2,
        };
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => DirectResponse(new PaginatedResult<TenantAuditEntry>(
                call.ArgAt<GetTenantAuditQuery>(0).Stream == "global-administrators" ? [entry] :
                    [entry with { EventId = "public-row", CommandCorrelationId = null, AggregateSequence = null }], null, false),
                projectionVersion: call.ArgAt<GetTenantAuditQuery>(0).Stream == "global-administrators" ? "tenant-sequence:2" : "audit-composite:public"));
        TenantQueryGateway gateway = CreateGateway(client);

        (GlobalAdministratorAuditProof? proof, bool complete) = await gateway
            .FindGlobalAdministratorCorrectionProofAsync("exact-command", 2, "admin-user",
                "GlobalAdministratorSet", original, submitted);
        TenantAuditSnapshot publicAudit = await gateway.GetTenantAuditAsync(new TenantAuditRequest("system"), null);

        complete.ShouldBeTrue();
        proof.ShouldNotBeNull();
        proof.Row.EventReference.ShouldBe("corrective-event");
        proof.Row.CommandCorrelationId.ShouldBeNull();
        proof.Row.AggregateSequence.ShouldBeNull();
        publicAudit.Rows.ShouldHaveSingleItem().CommandCorrelationId.ShouldBeNull();
        publicAudit.Rows[0].AggregateSequence.ShouldBeNull();
        _ = client.Received().GetTenantAuditAsync(
            Arg.Is<GetTenantAuditQuery>(query => query.From == submitted && query.Stream == "global-administrators"), null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("global-administrators", "TenantCreated", AuditEventCategory.Administrative)]
    [InlineData("tenant", "GlobalAdministratorSet", AuditEventCategory.Access)]
    public async Task SelectedAuditOwnerRejectsOtherOwnerRestEvidence(string stream, string eventType, AuditEventCategory category)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantAuditEntry entry = AuditEntry("approved-reference", category, eventType: eventType, tenantId: "system");
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantAuditEntry>([entry], null, false), projectionVersion: "tenant-sequence:2"));
        TenantAuditSnapshot result = await CreateGateway(client).GetTenantAuditAsync(new TenantAuditRequest("system", Stream: stream), null);
        result.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        result.Rows.ShouldBeEmpty();
        result.ProjectionVersion.ShouldBeNull();
        _ = client.Received(1).GetTenantAuditAsync(Arg.Is<GetTenantAuditQuery>(query => query.Stream == stream), null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("UserRemovedFromTenant", true)]
    [InlineData("UserAddedToTenant", false)]
    [InlineData("GlobalAdministratorRemoved", false)]
    public async Task AuditGatewayPreservesOnlyInternalRemovalProofAndRedactsSerializedEvidence(string eventType, bool retained)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantAuditEntry entry = AuditEntry("approved-event-reference", AuditEventCategory.Access,
            timestamp: DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture), eventType: eventType) with
        {
            CommandCorrelationId = "exact-attempt-correlation",
            AggregateSequence = 42,
        };
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantAuditEntry>([entry], null, false), projectionVersion: "opaque-audit-version"));
        TenantAuditSnapshot snapshot = await CreateGateway(client).GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null);
        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.CommandCorrelationId.ShouldBe(retained ? "exact-attempt-correlation" : null);
        row.AggregateSequence.ShouldBe(retained ? 42L : null);
        string serialized = JsonSerializer.Serialize(row);
        serialized.ShouldContain("approved-event-reference");
        serialized.ShouldNotContain("CommandCorrelationId");
        serialized.ShouldNotContain("AggregateSequence");
        serialized.ShouldNotContain("exact-attempt-correlation");
        string snapshotJson = JsonSerializer.Serialize(snapshot);
        snapshotJson.ShouldContain("approved-event-reference");
        snapshotJson.ShouldNotContain("exact-attempt-correlation");
        TenantAuditReceipt receipt = TenantAuditReceipt.FromRow(row);
        string receiptJson = JsonSerializer.Serialize(receipt);
        receiptJson.ShouldContain("approved-event-reference");
        receiptJson.ShouldNotContain("exact-attempt-correlation");

        // Controls prove these actual serialized surfaces can disclose the value if it enters public evidence.
        TenantAuditRow unsafeRow = row with { ReferenceContext = "exact-attempt-correlation" };
        JsonSerializer.Serialize(unsafeRow).ShouldContain("exact-attempt-correlation");
        JsonSerializer.Serialize(snapshot with { Rows = [unsafeRow] }).ShouldContain("exact-attempt-correlation");
        JsonSerializer.Serialize(receipt with { CommandReference = "exact-attempt-correlation" })
            .ShouldContain("exact-attempt-correlation");
    }

    [Fact]
    public async Task AuditPageCorrectionCaptureUsesOneReadForMultipleTargets()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantDetail raw = Detail("tenant.alpha");
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(DirectResponse(raw));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        string[] targets = ["first-user", "second-user"];
        TenantCorrectionProjection[] safe = targets.Select(target => new TenantCorrectionProjection("tenant.alpha", target,
            TenantStatus.Active, null, false, true, false, true, ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked)).ToArray();
        composition.ComposeTenantCorrectionProjectionsAsync("tenant.alpha", targets, raw, ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<TenantCorrectionProjection>>(safe));
        IReadOnlyList<TenantCorrectionProjection> captures = await CreateGateway(client, bffComposition: composition)
            .GetTenantCorrectionProjectionsAsync("tenant.alpha", targets);
        captures.ShouldBe(safe.Select(capture => capture with { ProjectionVersion = "projection-v1" }).ToArray());
        _ = client.Received(1).GetTenantAsync(Arg.Any<GetTenantQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CorrectionCaptureAlwaysReadsWithoutValidatorAndReturnsOnlyComposedFacts()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantDetail raw = Detail("tenant.alpha");
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(raw, "secret-etag", "secret-version"));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        TenantCorrectionProjection safe = new("tenant.alpha", "target-user", TenantStatus.Active, TenantRole.TenantContributor,
            false, true, false, true, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked);
        composition.ComposeTenantCorrectionProjectionsAsync("tenant.alpha", Arg.Is<IReadOnlyList<string>>(targets => targets.Count == 1 && targets[0] == "target-user"), raw, ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<TenantCorrectionProjection>>([safe]));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);
        TenantCorrectionProjection capture = await gateway.GetTenantCorrectionProjectionAsync("tenant.alpha", "target-user");
        capture.ShouldBe(safe with { ProjectionVersion = "secret-version" });
        _ = client.Received(1).GetTenantAsync(Arg.Is<GetTenantQuery>(query => query.TenantId == "tenant.alpha"), null, Arg.Any<CancellationToken>());
        gateway.SupportsTenantCorrectionStart.ShouldBeTrue();
    }

    [Fact]
    public async Task CorrectionCaptureUsesProjectionVersionIndependentlyOfETag()
    {
        const string version = "tenant-sequence:42";
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(Detail("tenant.alpha"), eTag: "1", projectionVersion: version));
        var composition = new TenantsBffComposition(
            Substitute.For<ITenantCommandGateway>(),
            principalResolver: new StubConfigurationPrincipalResolver(
                TenantConfigurationPrincipalEvidence.NonAdministrator("owner-user")));

        TenantCorrectionProjection capture = await CreateGateway(client, bffComposition: composition)
            .GetTenantCorrectionProjectionAsync("tenant.alpha", "reader-user");

        capture.IsCurrent.ShouldBeTrue();
        capture.IsAuthorized.ShouldBeTrue();
        capture.HasVerifiedMembership.ShouldBeTrue();
        capture.CurrentRole.ShouldBe(TenantRole.TenantReader);
        capture.ProjectionVersion.ShouldBe(version);
        _ = client.Received(1).GetTenantAsync(Arg.Any<GetTenantQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CorrectionGatewayRetainsComposedOwnerCountForBatchAndSingleTargetReads()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantDetail raw = Detail("tenant.alpha") with
        {
            Members =
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("second-owner", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ],
        };
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(raw));
        var composition = new TenantsBffComposition(Substitute.For<ITenantCommandGateway>(),
            principalResolver: new StubConfigurationPrincipalResolver(
                TenantConfigurationPrincipalEvidence.NonAdministrator("owner-user")));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        IReadOnlyList<TenantCorrectionProjection> batch = await gateway.GetTenantCorrectionProjectionsAsync(
            "tenant.alpha", ["reader-user", "second-owner"]);
        TenantCorrectionProjection single = await gateway.GetTenantCorrectionProjectionAsync(
            "tenant.alpha", "reader-user");

        batch.Count.ShouldBe(2);
        batch.ShouldAllBe(capture => capture.OwnerCount == 2 && capture.IsAuthorized && capture.HasVerifiedMembership);
        single.OwnerCount.ShouldBe(2);
        single.CurrentRole.ShouldBe(TenantRole.TenantReader);
        single.IsAuthorized.ShouldBeTrue();
        _ = client.Received(2).GetTenantAsync(Arg.Any<GetTenantQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-modified")]
    [InlineData("null")]
    [InlineData("degraded")]
    [InlineData("unauthorized")]
    public async Task CorrectionCaptureDoesNotRetainOrComposeUnusablePayload(string kind)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantsRestQueryResponse<TenantDetail> response = kind switch
        {
            "not-modified" => NotModifiedResponse<TenantDetail>("secret-etag"),
            "null" => DirectResponse<TenantDetail>(null!),
            "degraded" => DirectResponse(Detail("tenant.alpha")) with { Metadata = ProjectionBackedMetadata(isStale: false) with { IsDegraded = true } },
            _ => FailureResponse<TenantDetail>(TenantsRestQueryFailureKind.Unauthorized, 401),
        };
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(response);
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        TenantCorrectionProjection capture = await CreateGateway(client, bffComposition: composition)
            .GetTenantCorrectionProjectionAsync("tenant.alpha", "target-user");
        capture.IsCurrent.ShouldBeFalse();
        capture.HasVerifiedMembership.ShouldBeFalse();
        _ = composition.DidNotReceiveWithAnyArgs().ComposeTenantCorrectionProjectionsAsync(default!, default!, default!, default, default, default);
    }

    [Theory]
    [InlineData(true, ProjectionLifecycleState.Unknown, QueryResponseProvenance.ProjectionBacked)]
    [InlineData(false, ProjectionLifecycleState.Rebuilding, QueryResponseProvenance.ProjectionBacked)]
    [InlineData(false, ProjectionLifecycleState.Current, QueryResponseProvenance.HandlerComputed)]
    public async Task SuccessfulCorrectionReadStillRequiresCurrentProjectionMetadata(
        bool isStale,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        TenantsRestQueryResponse<TenantDetail> response = DirectResponse(Detail("tenant.alpha")) with
        {
            Metadata = ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle, provenance: provenance),
        };
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(response);
        var composition = new TenantsBffComposition(Substitute.For<ITenantCommandGateway>(),
            principalResolver: new StubConfigurationPrincipalResolver(
                TenantConfigurationPrincipalEvidence.NonAdministrator("owner-user")));

        TenantCorrectionProjection capture = await CreateGateway(client, bffComposition: composition)
            .GetTenantCorrectionProjectionAsync("tenant.alpha", "reader-user");

        capture.IsAuthorized.ShouldBeTrue();
        capture.HasVerifiedMembership.ShouldBeTrue();
        capture.CurrentRole.ShouldBe(TenantRole.TenantReader);
        capture.IsCurrent.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorrectionCaptureRedactsReadAndPrincipalResolverFailures(bool principalFailure)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => principalFailure ? DirectResponse(Detail("tenant.alpha"))
                : throw new HttpRequestException("raw query URI"));
        ITenantConfigurationPrincipalResolver resolver = new ThrowingPrincipalResolver();
        var composition = new TenantsBffComposition(Substitute.For<ITenantCommandGateway>(), principalResolver: resolver);
        TenantCorrectionProjection capture = await CreateGateway(client, bffComposition: composition)
            .GetTenantCorrectionProjectionAsync("tenant.alpha", "target-user");
        capture.IsCurrent.ShouldBeFalse();
        capture.IsAuthorized.ShouldBeFalse();
        capture.HasVerifiedMembership.ShouldBeFalse();
        capture.CurrentRole.ShouldBeNull();
    }

    [Fact]
    public async Task Gateway_constructs_each_direct_typed_query_at_the_production_client_boundary()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>([], null, false)));
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(Detail("tenant/detail")));
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantMember>([], null, false)));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<UserTenantMembership>([], null, false)));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantAuditEntry>([], null, false)));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<GlobalAdministratorSummary>([], null, false)));
        TenantQueryGateway gateway = CreateGateway(client, userId: "user.self");
        DateTimeOffset from = DateTimeOffset.Parse("2026-06-01T00:00:00Z", CultureInfo.InvariantCulture);
        DateTimeOffset to = DateTimeOffset.Parse("2026-06-02T00:00:00Z", CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();

        _ = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "list-cursor", PageSize: 11, ETag: "list-etag"), null, cancellation.Token);
        _ = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant/detail", "detail-etag"), null, cancellation.Token);
        _ = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant/users", "users-cursor", 12, "users-etag"), null, cancellation.Token);
        _ = await gateway.GetMyTenantsAsync(
            new UserTenantMembershipRequest(Cursor: "self-cursor", PageSize: 13, ETag: "self-etag"),
            null,
            cancellation.Token);
        _ = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target/user", "target-cursor", 14, "target-etag"),
            null,
            cancellation.Token);
        _ = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest(
                "tenant/audit",
                from,
                to,
                AuditEventCategory.Access,
                "audit-cursor",
                15,
                "audit-etag"),
            null,
            cancellation.Token);
        _ = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest("admin-cursor", 16, "admin-etag"), null, cancellation.Token);

        _ = client.Received(1).ListTenantsAsync(
            Arg.Is<ListTenantsQuery>(query => query != null && query.Cursor == "list-cursor" && query.PageSize == 11),
            null,
            cancellation.Token);
        _ = client.Received(1).GetTenantAsync(
            Arg.Is<GetTenantQuery>(query => query != null && query.TenantId == "tenant/detail"),
            null,
            cancellation.Token);
        _ = client.Received(1).GetTenantUsersAsync(
            Arg.Is<GetTenantUsersQuery>(query => query != null && query.TenantId == "tenant/users"
                && query.Cursor == "users-cursor"
                && query.PageSize == 12),
            null,
            cancellation.Token);
        _ = client.Received(1).GetUserTenantsAsync(
            Arg.Is<GetUserTenantsQuery>(query => query != null && query.UserId == "user.self"
                && query.Cursor == "self-cursor"
                && query.PageSize == 13),
            null,
            cancellation.Token);
        _ = client.Received(1).GetUserTenantsAsync(
            Arg.Is<GetUserTenantsQuery>(query => query != null && query.UserId == "target/user"
                && query.Cursor == "target-cursor"
                && query.PageSize == 14),
            null,
            cancellation.Token);
        _ = client.Received(1).GetTenantAuditAsync(
            Arg.Is<GetTenantAuditQuery>(query => query != null && query.TenantId == "tenant/audit"
                && query.From == from
                && query.To == to
                && query.Category == AuditEventCategory.Access
                && query.Cursor == "audit-cursor"
                && query.PageSize == 15),
            null,
            cancellation.Token);
        _ = client.Received(1).GetGlobalAdministratorsAsync(
            Arg.Is<GetGlobalAdministratorsQuery>(query => query != null
                && query.Cursor == "admin-cursor"
                && query.PageSize == 16),
            null,
            cancellation.Token);
    }

    [Fact]
    public async Task Conditional_not_modified_reuse_requires_the_retained_validator_on_every_direct_read()
    {
        const string retainedETag = "retained-a";
        const string requestETag = "request-b";
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(Detail("tenant.alpha"))
                : NotModifiedResponse<TenantDetail>(requestETag));
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<TenantMember>(
                    [new TenantMember("member-new", TenantRole.TenantReader)],
                    null,
                    false))
                : NotModifiedResponse<PaginatedResult<TenantMember>>(requestETag));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<UserTenantMembership>(
                    [new UserTenantMembership("tenant.new", "New", TenantStatus.Active, TenantRole.TenantReader)],
                    null,
                    false))
                : NotModifiedResponse<PaginatedResult<UserTenantMembership>>(requestETag));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<GlobalAdministratorSummary>(
                    [new GlobalAdministratorSummary("admin-new")],
                    null,
                    false))
                : NotModifiedResponse<PaginatedResult<GlobalAdministratorSummary>>(requestETag));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<TenantAuditEntry>(
                    [AuditEntry("event-new", AuditEventCategory.Access)],
                    null,
                    false))
                : NotModifiedResponse<PaginatedResult<TenantAuditEntry>>(requestETag));
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<TenantSummary>([], null, false))
                : NotModifiedResponse<PaginatedResult<TenantSummary>>(requestETag));
        TenantQueryGateway gateway = CreateGateway(client);
        TenantAuditRequest auditRequest = new("tenant.alpha", ETag: requestETag);

        TenantDetailSnapshot detail = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", requestETag),
            TenantDetailSnapshot.Ready(
                Detail("tenant.alpha"),
                retainedETag,
                ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user")),
            CancellationToken.None);
        TenantUsersSnapshot users = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", ETag: requestETag),
            TenantUsersSnapshot.Ready(
                "tenant.alpha",
                [new TenantMember("member-old", TenantRole.TenantReader)],
                null,
                false,
                retainedETag,
                "members-v1",
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") },
            CancellationToken.None);
        UserTenantMembershipSnapshot memberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", ETag: requestETag),
            UserTenantMembershipSnapshot.Ready(
                [new UserTenantMembershipRow(
                    "tenant.old",
                    "Old",
                    TenantStatus.Active,
                    TenantRole.TenantReader,
                    ReadModelFreshnessState.Current)],
                null,
                false,
                retainedETag,
                ReadModelFreshnessState.Current,
                "target.user") with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") },
            CancellationToken.None);
        GlobalAdministratorsSnapshot administrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(ETag: requestETag),
            GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("admin-old", ReadModelFreshnessState.Current)],
                null,
                false,
                retainedETag,
                ReadModelFreshnessState.Current) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") },
            CancellationToken.None);
        TenantAuditSnapshot audit = await gateway.GetTenantAuditAsync(
            auditRequest,
            TenantAuditSnapshot.Ready(
                [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
                null,
                false,
                retainedETag,
                ReadModelFreshnessState.Current,
                auditRequest with { ETag = retainedETag }) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            },
            CancellationToken.None);
        TenantListSnapshot list = await gateway.ListTenantsAsync(
            new TenantListRequest(ETag: requestETag),
            TenantListSnapshot.Ready(
                [TenantListRow.FromSummary(new TenantSummary("tenant.old", "Old", TenantStatus.Active))],
                null,
                false,
                retainedETag,
                ReadModelFreshnessState.Current,
                isDegraded: false) with
            { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") },
            CancellationToken.None);

        detail.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        users.Rows.ShouldHaveSingleItem().UserId.ShouldBe("member-new");
        memberships.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.new");
        administrators.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-new");
        audit.Rows.ShouldHaveSingleItem().EventReference.ShouldBe("event-new");
        list.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        _ = client.Received(1).GetTenantAsync(Arg.Any<GetTenantQuery>(), null, Arg.Any<CancellationToken>());
        _ = client.Received(1).GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), null, Arg.Any<CancellationToken>());
        _ = client.Received(1).GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), null, Arg.Any<CancellationToken>());
        _ = client.Received(1).GetGlobalAdministratorsAsync(
            Arg.Any<GetGlobalAdministratorsQuery>(), null, Arg.Any<CancellationToken>());
        _ = client.Received(1).GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), null, Arg.Any<CancellationToken>());
        _ = client.Received(1).ListTenantsAsync(Arg.Any<ListTenantsQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retention_requires_a_confirmed_snapshot_kind_instead_of_a_matching_default_scope()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantMember>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<UserTenantMembership>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<GlobalAdministratorSummary>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantAuditEntry>>(TenantsRestQueryFailureKind.Unavailable));
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantSummary>>(TenantsRestQueryFailureKind.Unavailable));
        TenantQueryGateway gateway = CreateGateway(client);
        TenantAuditRequest auditRequest = new("tenant.alpha");

        TenantUsersSnapshot users = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha"),
            TenantUsersSnapshot.Unavailable("tenant.alpha"),
            CancellationToken.None);
        UserTenantMembershipSnapshot memberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user"),
            UserTenantMembershipSnapshot.Unavailable(targetUserId: "target.user"),
            CancellationToken.None);
        GlobalAdministratorsSnapshot administrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            GlobalAdministratorsSnapshot.Unavailable(),
            CancellationToken.None);
        TenantAuditSnapshot audit = await gateway.GetTenantAuditAsync(
            auditRequest,
            TenantAuditSnapshot.Unavailable(auditRequest),
            CancellationToken.None);
        TenantListSnapshot list = await gateway.ListTenantsAsync(
            new TenantListRequest(),
            TenantListSnapshot.Error(),
            CancellationToken.None);

        users.Kind.ShouldBe(TenantUsersSurfaceKind.Unavailable);
        memberships.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Unavailable);
        administrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unavailable);
        audit.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
        list.Kind.ShouldBe(TenantListSurfaceKind.Error);
    }

    [Fact]
    public async Task Confirmed_empty_global_administrators_are_retained_on_a_transient_refresh_failure()
    {
        const string eTag = "confirmed-empty";
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<GlobalAdministratorSummary>>(TenantsRestQueryFailureKind.Unavailable));
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current,
            eTag) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "global-v1",
            IsCompleteEvidence = true,
        };

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(ETag: eTag),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Degraded);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.ETag.ShouldBe(eTag);
        snapshot.IsCompleteEvidence.ShouldBeFalse();
    }

    [Fact]
    public async Task Unavailable_failure_category_maps_raw_server_statuses_to_first_load_unavailable_states()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<TenantDetail>(
                TenantsRestQueryFailureKind.Unavailable,
                (int)HttpStatusCode.InternalServerError));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<UserTenantMembership>>(
                TenantsRestQueryFailureKind.Unavailable,
                (int)HttpStatusCode.NoContent));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot detail = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);
        UserTenantMembershipSnapshot memberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user"),
            previous: null,
            CancellationToken.None);

        detail.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        memberships.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Unavailable);
    }

    [Fact]
    public async Task Current_not_modified_evidence_clears_prior_transport_degradation()
    {
        const string eTag = "known";
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(NotModifiedResponse<PaginatedResult<UserTenantMembership>>(eTag));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(NotModifiedResponse<PaginatedResult<GlobalAdministratorSummary>>(eTag, "global-v2"));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(NotModifiedResponse<PaginatedResult<TenantAuditEntry>>(eTag));
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(NotModifiedResponse<PaginatedResult<TenantSummary>>(eTag));
        TenantQueryGateway gateway = CreateGateway(client);
        TenantAuditRequest auditRequest = new("tenant.alpha", ETag: eTag);
        UserTenantMembershipSnapshot previousMemberships = UserTenantMembershipSnapshot.Degraded(
            [new UserTenantMembershipRow(
                "tenant.alpha",
                "Alpha",
                TenantStatus.Active,
                TenantRole.TenantReader,
                ReadModelFreshnessState.Unknown)],
            UserTenantMembershipReason.GatewayFailure,
            eTag,
            targetUserId: "target.user") with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ProjectionVersion = "memberships-v1",
        };
        GlobalAdministratorsSnapshot previousAdministrators = GlobalAdministratorsSnapshot.Degraded(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Unknown)],
            GlobalAdministratorsReason.GatewayFailure,
            eTag) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ProjectionVersion = "global-v1",
        };
        TenantAuditSnapshot previousAudit = TenantAuditSnapshot.Degraded(
            [TenantAuditRow.FromEntry(AuditEntry("event-1", AuditEventCategory.Access), ReadModelFreshnessState.Unknown)],
            TenantAuditReason.GatewayFailure,
            auditRequest,
            eTag) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ProjectionVersion = "audit-v1",
        };
        TenantListSnapshot previousList = TenantListSnapshot.Degraded(
            [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
            TenantListReason.GatewayUnavailable) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ETag = eTag,
            ProjectionVersion = "list-v1",
        };

        UserTenantMembershipSnapshot memberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", ETag: eTag),
            previousMemberships,
            CancellationToken.None);
        GlobalAdministratorsSnapshot administrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(ETag: eTag),
            previousAdministrators,
            CancellationToken.None);
        TenantAuditSnapshot audit = await gateway.GetTenantAuditAsync(
            auditRequest,
            previousAudit,
            CancellationToken.None);
        TenantListSnapshot list = await gateway.ListTenantsAsync(
            new TenantListRequest(ETag: eTag),
            previousList,
            CancellationToken.None);

        memberships.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        memberships.Reason.ShouldBe(UserTenantMembershipReason.None);
        memberships.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        administrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        administrators.Reason.ShouldBe(GlobalAdministratorsReason.None);
        administrators.ProjectionVersion.ShouldBe("global-v2");
        administrators.IsCompleteEvidence.ShouldBeTrue();
        audit.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        audit.Reason.ShouldBe(TenantAuditReason.None);
        list.Kind.ShouldBe(TenantListSurfaceKind.Ready);
        list.Reason.ShouldBe(TenantListReason.None);
        list.IsDegraded.ShouldBeFalse();
    }

    [Fact]
    public async Task Paged_sibling_reads_reject_mismatched_cursor_or_page_size_retention()
    {
        const string eTag = "known";
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ListTenantsQuery query = call.ArgAt<ListTenantsQuery>(0);
                string? validator = call.ArgAt<string?>(1);
                return query.PageSize == 21
                    ? FailureResponse<PaginatedResult<TenantSummary>>(TenantsRestQueryFailureKind.Unavailable)
                    : validator is null
                        ? DirectResponse(new PaginatedResult<TenantSummary>([], null, false))
                        : NotModifiedResponse<PaginatedResult<TenantSummary>>(eTag);
            });
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GetUserTenantsQuery query = call.ArgAt<GetUserTenantsQuery>(0);
                string? validator = call.ArgAt<string?>(1);
                return query.PageSize == 21
                    ? FailureResponse<PaginatedResult<UserTenantMembership>>(TenantsRestQueryFailureKind.Unavailable)
                    : validator is null
                        ? DirectResponse(new PaginatedResult<UserTenantMembership>(
                            [new UserTenantMembership("tenant.new", "New", TenantStatus.Active, TenantRole.TenantReader)],
                            null,
                            false))
                        : NotModifiedResponse<PaginatedResult<UserTenantMembership>>(eTag);
            });
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GetGlobalAdministratorsQuery query = call.ArgAt<GetGlobalAdministratorsQuery>(0);
                string? validator = call.ArgAt<string?>(1);
                return query.PageSize == 21
                    ? FailureResponse<PaginatedResult<GlobalAdministratorSummary>>(TenantsRestQueryFailureKind.Unavailable)
                    : validator is null
                        ? DirectResponse(new PaginatedResult<GlobalAdministratorSummary>(
                            [new GlobalAdministratorSummary("admin-new")],
                            null,
                            false))
                        : NotModifiedResponse<PaginatedResult<GlobalAdministratorSummary>>(eTag);
            });
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GetTenantAuditQuery query = call.ArgAt<GetTenantAuditQuery>(0);
                string? validator = call.ArgAt<string?>(1);
                return query.PageSize == 21
                    ? FailureResponse<PaginatedResult<TenantAuditEntry>>(TenantsRestQueryFailureKind.Unavailable)
                    : validator is null
                        ? DirectResponse(new PaginatedResult<TenantAuditEntry>(
                            [AuditEntry("event-new", AuditEventCategory.Access)],
                            null,
                            false))
                        : NotModifiedResponse<PaginatedResult<TenantAuditEntry>>(eTag);
            });
        TenantQueryGateway gateway = CreateGateway(client);
        TenantListSnapshot previousList = TenantListSnapshot.Ready(
            [TenantListRow.FromSummary(new TenantSummary("tenant.old", "Old", TenantStatus.Active))],
            null,
            false,
            eTag,
            ReadModelFreshnessState.Current,
            isDegraded: false) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            RequestCursor = "old-page",
            RequestPageSize = 20,
        };
        UserTenantMembershipSnapshot previousMemberships = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow(
                "tenant.old",
                "Old",
                TenantStatus.Active,
                TenantRole.TenantReader,
                ReadModelFreshnessState.Current)],
            null,
            false,
            eTag,
            ReadModelFreshnessState.Current,
            "target.user") with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            RequestCursor = "old-page",
            RequestPageSize = 20,
        };
        GlobalAdministratorsSnapshot previousAdministrators = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-old", ReadModelFreshnessState.Current)],
            null,
            false,
            eTag,
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            RequestCursor = "old-page",
            RequestPageSize = 20,
        };
        TenantAuditRequest previousAuditRequest = new(
            "tenant.alpha",
            Cursor: "old-page",
            PageSize: 20,
            ETag: eTag);
        TenantAuditSnapshot previousAudit = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            null,
            false,
            eTag,
            ReadModelFreshnessState.Current,
            previousAuditRequest) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };

        TenantListSnapshot recoveredList = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "new-page", PageSize: 20, ETag: eTag),
            previousList,
            CancellationToken.None);
        UserTenantMembershipSnapshot recoveredMemberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", "new-page", 20, eTag),
            previousMemberships,
            CancellationToken.None);
        GlobalAdministratorsSnapshot recoveredAdministrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest("new-page", 20, eTag),
            previousAdministrators,
            CancellationToken.None);
        TenantAuditSnapshot recoveredAudit = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha", Cursor: "new-page", PageSize: 20, ETag: eTag),
            previousAudit,
            CancellationToken.None);
        TenantListSnapshot rejectedList = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "old-page", PageSize: 21, ETag: eTag),
            previousList,
            CancellationToken.None);
        UserTenantMembershipSnapshot rejectedMemberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", "old-page", 21, eTag),
            previousMemberships,
            CancellationToken.None);
        GlobalAdministratorsSnapshot rejectedAdministrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest("old-page", 21, eTag),
            previousAdministrators,
            CancellationToken.None);
        TenantAuditSnapshot rejectedAudit = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha", Cursor: "old-page", PageSize: 21, ETag: eTag),
            previousAudit,
            CancellationToken.None);

        recoveredList.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        recoveredMemberships.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.new");
        recoveredAdministrators.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-new");
        recoveredAudit.Rows.ShouldHaveSingleItem().EventReference.ShouldBe("event-new");
        rejectedList.Kind.ShouldBe(TenantListSurfaceKind.Error);
        rejectedMemberships.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Unavailable);
        rejectedAdministrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unavailable);
        rejectedAudit.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
        _ = client.Received(1).ListTenantsAsync(
            Arg.Is<ListTenantsQuery>(query => query != null && query.Cursor == "new-page"),
            null,
            Arg.Any<CancellationToken>());
        _ = client.Received(1).GetUserTenantsAsync(
            Arg.Is<GetUserTenantsQuery>(query => query != null && query.Cursor == "new-page"),
            null,
            Arg.Any<CancellationToken>());
        _ = client.Received(1).GetGlobalAdministratorsAsync(
            Arg.Is<GetGlobalAdministratorsQuery>(query => query != null && query.Cursor == "new-page"),
            null,
            Arg.Any<CancellationToken>());
        _ = client.Received(1).GetTenantAuditAsync(
            Arg.Is<GetTenantAuditQuery>(query => query != null && query.Cursor == "new-page"),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_tenant_users_uses_dedicated_typed_read_and_retains_independent_evidence()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantMember>(
                [new TenantMember("member-user", TenantRole.TenantReader)],
                "next-page",
                HasMore: true),
            eTag: "members-etag",
            metadata: ProjectionBackedMetadata(
                isStale: false,
                eTag: "members-etag",
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "members-v7"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantUsersSnapshot snapshot = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant/alpha", Cursor: "cursor-value", PageSize: 12),
            previous: null,
            CancellationToken.None);

        NSubstitute.Core.ICall submitted = ReadCalls(client).ShouldHaveSingleItem();
        ((GetTenantUsersQuery)submitted.GetArguments()[0]!).TenantId.ShouldBe("tenant/alpha");
        ((GetTenantUsersQuery)submitted.GetArguments()[0]!).Cursor.ShouldBe("cursor-value");
        ((GetTenantUsersQuery)submitted.GetArguments()[0]!).PageSize.ShouldBe(12);
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("member-user");
        snapshot.ETag.ShouldBe("members-etag");
        snapshot.ProjectionVersion.ShouldBe("members-v7");
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
    }

    [Fact]
    public async Task Get_tenant_users_not_modified_retains_only_matching_member_snapshot()
    {
        TenantUsersSnapshot previous = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("member-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-old",
            projectionVersion: "members-v6",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueTenantUsersNotModified(clientResponses, "members-old", "members-v7");
        TenantQueryGateway gateway = CreateGateway(client);

        TenantUsersSnapshot snapshot = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", ETag: "members-old"),
            previous,
            CancellationToken.None);

        snapshot.Rows.ShouldBeSameAs(previous.Rows);
        snapshot.ETag.ShouldBe("members-old");
        snapshot.ProjectionVersion.ShouldBe("members-v6");
        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Ready);
    }

    [Fact]
    public async Task Get_tenant_users_does_not_reuse_a_previous_page_validator_for_a_new_cursor()
    {
        TenantUsersSnapshot previous = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("page-one-user", TenantRole.TenantReader)],
            nextCursor: "page-two",
            hasMore: true,
            eTag: "members-etag",
            projectionVersion: "members-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            RequestCursor = null,
            RequestPageSize = 20,
        };
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(new PaginatedResult<TenantMember>(
                    [new TenantMember("page-two-user", TenantRole.TenantReader)],
                    Cursor: null,
                    HasMore: false))
                : new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                    null,
                    ProjectionBackedMetadata(
                        isStale: false,
                        eTag: "members-etag",
                        isNotModified: true,
                        lifecycle: ProjectionLifecycleState.Current,
                        projectionVersion: "members-v1"),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.NotModified));

        TenantUsersSnapshot snapshot = await CreateGateway(client).GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", Cursor: "page-two"),
            previous,
            CancellationToken.None);

        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("page-two-user");
        snapshot.RequestCursor.ShouldBe("page-two");
        _ = client.Received(1).GetTenantUsersAsync(
            Arg.Is<GetTenantUsersQuery>(query => query != null && query.Cursor == "page-two"),
            null,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TenantsRestQueryFailureKind.Unauthorized, TenantUsersSurfaceKind.Unauthorized, TenantUsersReason.Unauthorized)]
    [InlineData(TenantsRestQueryFailureKind.Forbidden, TenantUsersSurfaceKind.Unauthorized, TenantUsersReason.Unauthorized)]
    [InlineData(TenantsRestQueryFailureKind.NotFound, TenantUsersSurfaceKind.NotFound, TenantUsersReason.NotFound)]
    [InlineData(TenantsRestQueryFailureKind.InvalidCursor, TenantUsersSurfaceKind.Invalid, TenantUsersReason.InvalidCursor)]
    [InlineData(TenantsRestQueryFailureKind.InvalidRequest, TenantUsersSurfaceKind.Invalid, TenantUsersReason.GatewayFailure)]
    [InlineData(TenantsRestQueryFailureKind.Timeout, TenantUsersSurfaceKind.Unavailable, TenantUsersReason.GatewayUnavailable)]
    [InlineData(TenantsRestQueryFailureKind.Unavailable, TenantUsersSurfaceKind.Unavailable, TenantUsersReason.GatewayUnavailable)]
    // Review loop 13. These three used to fall to the mapper's default arm and report Error, blaming the
    // server for a response this client refused to validate. The member read is the one read that switches
    // on FailureKind directly instead of going through ToEventStoreResult, so decision D3's
    // "a corrupt response is an outage" override never reached it.
    [InlineData(TenantsRestQueryFailureKind.InvalidPayload, TenantUsersSurfaceKind.Unavailable, TenantUsersReason.GatewayUnavailable)]
    [InlineData(TenantsRestQueryFailureKind.InvalidMetadata, TenantUsersSurfaceKind.Unavailable, TenantUsersReason.GatewayUnavailable)]
    [InlineData(TenantsRestQueryFailureKind.Unknown, TenantUsersSurfaceKind.Unavailable, TenantUsersReason.GatewayUnavailable)]
    // A route identity rejected locally is an input defect, not an outage -- the same conclusion the detail
    // mapper draws, so the member panel and the detail panel no longer disagree about one malformed id.
    [InlineData(TenantsRestQueryFailureKind.UnsupportedRouteIdentifier, TenantUsersSurfaceKind.NotFound, TenantUsersReason.NotFound)]
    public async Task Get_tenant_users_maps_transport_failure_categories(
        TenantsRestQueryFailureKind failureKind,
        TenantUsersSurfaceKind expectedKind,
        TenantUsersReason expectedReason)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                null,
                new QueryResponseMetadata(),
                failureKind,
                failureKind is TenantsRestQueryFailureKind.Unauthorized ? 401
                    : failureKind is TenantsRestQueryFailureKind.Forbidden ? 403
                    : failureKind is TenantsRestQueryFailureKind.NotFound ? 404
                    : failureKind is TenantsRestQueryFailureKind.InvalidCursor or TenantsRestQueryFailureKind.InvalidRequest ? 400
                    : 503));

        TenantUsersSnapshot snapshot = await CreateGateway(client).GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", Cursor: "opaque"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_users_retains_only_the_same_page_on_an_unavailable_refresh()
    {
        TenantUsersSnapshot previous = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("member-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-etag",
            projectionVersion: "members-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            RequestCursor = "page-two",
            RequestPageSize = 10,
        };
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                null,
                new QueryResponseMetadata(),
                TenantsRestQueryFailureKind.Unavailable,
                503));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantUsersSnapshot retained = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", "page-two", 10, "members-etag"),
            previous,
            CancellationToken.None);
        TenantUsersSnapshot rejected = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", "page-three", 10, "members-etag"),
            previous,
            CancellationToken.None);

        retained.Kind.ShouldBe(TenantUsersSurfaceKind.Degraded);
        retained.Rows.ShouldHaveSingleItem().UserId.ShouldBe("member-user");
        rejected.Kind.ShouldBe(TenantUsersSurfaceKind.Unavailable);
        rejected.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// A degraded tenant-users response must not keep the payload's projection lifecycle.
    /// </summary>
    /// <remarks>
    /// The snapshot is built from the payload lifecycle before the degraded arm runs, so without the reset a
    /// degraded projection reports <c>Lifecycle = Current</c> — a degraded read claiming a current
    /// projection, the conflation the whole freshness contract exists to prevent, and the input to five
    /// mutation gates. Deleting the reset left the suite green; the sibling
    /// <c>IsAuthorizationScopedEmpty</c> clear on the adjacent line was already covered.
    /// </remarks>
    [Fact]
    public async Task Degraded_tenant_users_metadata_resets_the_projection_lifecycle_to_unknown()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                new PaginatedResult<TenantMember>(
                    [new TenantMember("member-user", TenantRole.TenantReader)],
                    null,
                    false),
                ProjectionBackedMetadata(
                    isStale: false,
                    isDegraded: true,
                    eTag: "members-etag",
                    lifecycle: ProjectionLifecycleState.Current,
                    projectionVersion: "projection-v1"),
                TenantsRestQueryFailureKind.None,
                (int)HttpStatusCode.OK));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantUsersSnapshot snapshot = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", null, 10, null),
            null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Degraded);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        snapshot.IsAuthorizationScopedEmpty.ShouldBeFalse();
    }

    /// <summary>
    /// A page-one recovery that itself failed must not be stamped as a recovery.
    /// </summary>
    /// <remarks>
    /// <c>PagingRecovered</c> tells the consumer "this is page one, not the page you asked for". The guard
    /// checked only transport success, but the mapping can still fail on a successful response — a null
    /// <c>Items</c> payload becomes <c>Error</c> — and stamping that rendered the polite "restarted at the
    /// first page" notice over a failed read, then made <c>TenantDetailPage</c> take the
    /// <c>PagingRecovered</c> branch ahead of the branch that retains last-confirmed rows.
    /// </remarks>
    [Fact]
    public async Task A_failed_page_one_recovery_is_not_stamped_as_recovered()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                // The requested page reports an explicitly signalled invalid cursor...
                _ => new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                    null,
                    new QueryResponseMetadata(),
                    TenantsRestQueryFailureKind.InvalidCursor,
                    (int)HttpStatusCode.BadRequest),
                // ...and the page-one re-read succeeds at the transport level but carries no items.
                _ => new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                    null,
                    ProjectionBackedMetadata(
                        isStale: false,
                        lifecycle: ProjectionLifecycleState.Current,
                        projectionVersion: "projection-v1"),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.OK));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantUsersSnapshot snapshot = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", "expired-cursor", 10, null),
            null,
            CancellationToken.None);

        snapshot.Rows.ShouldBeEmpty();
        snapshot.PagingRecovered.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_tenant_users_does_not_infer_invalid_cursor_from_plain_bad_request()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                    null,
                    new QueryResponseMetadata(),
                    TenantsRestQueryFailureKind.InvalidRequest,
                    (int)HttpStatusCode.BadRequest),
                new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                    new PaginatedResult<TenantMember>(
                        [new TenantMember("member-user", TenantRole.TenantReader)],
                        Cursor: null,
                        HasMore: false),
                    ProjectionBackedMetadata(
                        isStale: false,
                        lifecycle: ProjectionLifecycleState.Current,
                        projectionVersion: "members-v8"),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.OK));
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns("operator-user");
        var gateway = new TenantQueryGateway(
            client,
            userContext,
            new StubMemoriesClient(),
            new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()));

        TenantUsersSnapshot snapshot = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", Cursor: "invalid-cursor", ETag: "members-old"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Invalid);
        snapshot.Reason.ShouldBe(TenantUsersReason.GatewayFailure);
        snapshot.Rows.ShouldBeEmpty();
        _ = client.Received(1).GetTenantUsersAsync(
            Arg.Is<GetTenantUsersQuery>(query => query != null && query.Cursor == "invalid-cursor"),
            null,
            Arg.Any<CancellationToken>());
        _ = client.Received(1).GetTenantUsersAsync(
            Arg.Any<GetTenantUsersQuery>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Get_tenant_without_authenticated_user_fails_closed_without_querying_event_store(string? userId)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, userId);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unauthorized);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_submits_literal_detail_query_and_maps_counts_source()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                servedAt: DateTimeOffset.UtcNow,
                projectionVersion: "detail-v7"));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), null, CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client)[0];
        ((GetTenantQuery)query.GetArguments()[0]!).TenantId.ShouldBe("tenant.alpha");
        query.GetArguments()[1].ShouldBeNull();
        ReadCalls(client).Count.ShouldBe(1);
        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.ProjectionVersion.ShouldBe("detail-v7");
    }

    [Fact]
    public async Task Update_metadata_projection_proof_reads_current_same_tenant_detail_without_a_validator()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(
                Detail("tenant.alpha"),
                eTag: "proof-etag",
                projectionVersion: "projection-v9"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot proof = await gateway.GetUpdateMetadataProjectionProofAsync(
            new UpdateTenant("tenant.alpha", "Alpha", "Tenant alpha description"),
            CancellationToken.None);

        _ = client.Received(1).GetTenantAsync(
            Arg.Is<GetTenantQuery>(query => query != null && query.TenantId == "tenant.alpha"),
            null,
            Arg.Any<CancellationToken>());
        proof.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        proof.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        proof.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        proof.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        proof.Detail!.Name.ShouldBe("Alpha");
        proof.ProjectionVersion.ShouldBe("projection-v9");
    }

    [Fact]
    public async Task Lifecycle_projection_proof_reads_current_same_tenant_detail_without_a_validator()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(
                Detail("Tenant.Mixed-01") with { Status = TenantStatus.Disabled },
                eTag: "proof-etag",
                projectionVersion: "tenant-sequence:42"));
        TenantQueryGateway gateway = CreateGateway(client);
        var request = new TenantLifecycleCommandRequest(
            "Tenant.Mixed-01",
            TenantLifecycleOperation.DisableTenant);

        TenantDetailSnapshot proof = await gateway.GetLifecycleProjectionProofAsync(
            request,
            CancellationToken.None);

        _ = client.Received(1).GetTenantAsync(
            Arg.Is<GetTenantQuery>(query => query != null && query.TenantId == "Tenant.Mixed-01"),
            null,
            Arg.Any<CancellationToken>());
        proof.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        proof.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        proof.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        proof.Detail.ShouldNotBeNull().TenantId.ShouldBe("Tenant.Mixed-01");
        proof.Detail!.Status.ShouldBe(TenantStatus.Disabled);
        proof.ProjectionVersion.ShouldBe("tenant-sequence:42");
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown, false)]
    [InlineData(QueryResponseProvenance.Unknown, true)]
    [InlineData(QueryResponseProvenance.HandlerComputed, false)]
    [InlineData(QueryResponseProvenance.HandlerComputed, true)]
    [InlineData((QueryResponseProvenance)999, false)]
    public async Task Get_tenant_non_projection_backed_freshness_evidence_remains_unknown(
        QueryResponseProvenance provenance,
        bool isStale)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: new QueryResponseMetadata(IsStale: isStale)
            {
                Provenance = provenance,
            });

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, TenantDetailSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, TenantDetailSurfaceKind.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding, false, ReadModelFreshnessState.Unknown, TenantDetailSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Degraded, false, ReadModelFreshnessState.Unknown, TenantDetailSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Unavailable, false, ReadModelFreshnessState.Unknown, TenantDetailSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.LocalOnly, false, ReadModelFreshnessState.Unknown, TenantDetailSurfaceKind.Ready)]
    [InlineData((ProjectionLifecycleState)999, false, ReadModelFreshnessState.Unknown, TenantDetailSurfaceKind.Ready)]
    public async Task Get_tenant_projection_lifecycle_precedes_legacy_stale_evidence(
        ProjectionLifecycleState lifecycle,
        bool isStale,
        ReadModelFreshnessState expectedFreshness,
        TenantDetailSurfaceKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(
            lifecycle is >= ProjectionLifecycleState.Unknown and <= ProjectionLifecycleState.LocalOnly
                ? lifecycle
                : ProjectionLifecycleState.Unknown);

        // The surface kind is what the operator actually sees. Asserting freshness alone left the
        // Stale/Ready branch selection at TenantQueryGateway.cs:109-113 unpinned.
        snapshot.Kind.ShouldBe(expectedKind);
    }

    [Fact]
    public async Task Get_tenant_not_modified_refetches_and_recomposes_even_with_a_matching_snapshot()
    {
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            Detail("tenant.alpha"),
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            projectionVersion: "detail-v6").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known");
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            eTag: "fresh",
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "detail-v7"));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        snapshot.ETag.ShouldBe("fresh");
        snapshot.ProjectionVersion.ShouldBe("detail-v7");
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[0].GetArguments()[1].ShouldBe("known");
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_not_modified_without_lifecycle_fails_closed_instead_of_inheriting_previous_lifecycle()
    {
        // Unquoted throughout: TenantsRestQueryClient.GetStrongETag trims the surrounding quotes off the wire
        // value and NormalizeValidator refuses to send one containing them, so a gateway-level ETag never
        // carries literal quote characters. The quoted form this used to pass is an input production cannot
        // produce.
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            Detail("tenant.alpha"),
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            lifecycle: ProjectionLifecycleState.Current,
            projectionVersion: "detail-v6").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known", isStale: null, lifecycle: ProjectionLifecycleState.Unknown);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            eTag: "fresh",
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Unknown));

        TenantDetailSnapshot snapshot = await CreateGateway(client)
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), previous, CancellationToken.None);

        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
    }

    [Fact]
    public async Task Get_tenant_applies_stale_freshness_from_not_modified_response()
    {
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            Detail("tenant.alpha"),
            eTag: "known",
            freshness: ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known", isStale: true, lifecycle: ProjectionLifecycleState.Stale);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            eTag: "known",
            metadata: ProjectionBackedMetadata(isStale: true));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Stale);
        snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
    }

    [Fact]
    public async Task Get_tenant_not_modified_without_matching_snapshot_refetches_unconditionally()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known", isStale: false, lifecycle: ProjectionLifecycleState.Current);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            eTag: "known",
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), previous: null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_double_not_modified_without_matching_snapshot_is_unknown()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known");
        EnqueueDetailNotModified(clientResponses, "still-known");

        TenantDetailSnapshot snapshot = await CreateGateway(client).GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", ETag: "known"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unknown);
        snapshot.Detail.ShouldBeNull();
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_not_modified_refetches_raw_detail_so_current_policy_can_approve_a_new_row()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "prior");
        EnqueueQueryResult(clientResponses,
            Detail(
                "tenant.alpha",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["billing.mode"] = "fresh-mode",
                    ["billing.region"] = "newly-approved",
                }),
            eTag: "fresh",
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(
                """
                {
                  "Tenants": {
                    "ConfigurationReadPolicy": {
                      "PrefixGrants": [
                        { "TenantId": "tenant.alpha", "Subject": "operator-user", "Prefix": "billing" }
                      ],
                      "DisplaySafe": ["billing.mode", "billing.region"]
                    }
                  }
                }
                """));

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", ETag: "prior"),
            previous,
            CancellationToken.None);

        snapshot.Configuration.Rows.Select(static row => row.Key)
            .ShouldBe(["billing.mode", "billing.region"]);
        snapshot.Configuration.Rows.Single(static row => row.Key == "billing.region").Value
            .ShouldBe("newly-approved");
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_not_modified_applies_expanded_revoked_and_invalid_reloaded_policy_from_one_provider()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:TenantId"] = "tenant.alpha",
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:Subject"] = "operator-user",
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:Prefix"] = "billing",
                ["Tenants:ConfigurationReadPolicy:DisplaySafe:0"] = "billing.mode",
            })
            .Build();
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["billing.mode"] = "trial",
            ["billing.region"] = "eu-west",
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(configuration));

        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", raw),
            eTag: "v1",
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantDetailSnapshot initial = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);
        initial.Configuration.Rows.Select(static row => row.Key).ShouldBe(["billing.mode"]);

        configuration["Tenants:ConfigurationReadPolicy:DisplaySafe:1"] = "billing.region";
        configuration.Reload();
        EnqueueDetailNotModified(clientResponses, "v1");
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", raw),
            eTag: "v2",
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantDetailSnapshot expanded = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", ETag: "v1"),
            initial,
            CancellationToken.None);
        expanded.Configuration.Rows.Select(static row => row.Key)
            .ShouldBe(["billing.mode", "billing.region"]);

        configuration["Tenants:ConfigurationReadPolicy:PrefixGrants:0:Subject"] = "operator.other";
        configuration.Reload();
        EnqueueDetailNotModified(clientResponses, "v2");
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", raw),
            eTag: "v3",
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantDetailSnapshot revoked = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", ETag: "v2"),
            expanded,
            CancellationToken.None);
        revoked.Configuration.IsAvailable.ShouldBeTrue();
        revoked.Configuration.Rows.ShouldBeEmpty();

        configuration["Tenants:ConfigurationReadPolicy:PrefixGrants"] = "invalid-scalar";
        configuration.Reload();
        EnqueueDetailNotModified(clientResponses, "v3");
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", raw),
            eTag: "v4",
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantDetailSnapshot invalid = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha", ETag: "v3"),
            revoked,
            CancellationToken.None);
        invalid.Configuration.IsAvailable.ShouldBeFalse();
        invalid.Configuration.Rows.ShouldBeEmpty();

        ReadCalls(client).Count.ShouldBe(7);
        ReadCalls(client).Where(static query => query.GetArguments()[1] is null).Count().ShouldBe(4);
    }

    [Fact]
    public async Task Get_tenant_filters_raw_configuration_before_constructing_snapshot_state()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail(
            "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["billing.mode"] = "visible",
                ["billing.secret"] = "hidden-undefined",
                ["private.mode"] = "hidden-namespace",
            }));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(
                """
                {
                  "Tenants": {
                    "ConfigurationReadPolicy": {
                      "PrefixGrants": [
                        { "TenantId": "tenant.alpha", "Subject": "operator-user", "Prefix": "billing" }
                      ],
                      "DisplaySafe": ["billing.mode", "private.mode"]
                    }
                  }
                }
                """));

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);

        snapshot.Detail.ShouldNotBeNull().Configuration.ShouldBeEmpty();
        TenantConfigurationSafeRow row = snapshot.Configuration.Rows.ShouldHaveSingleItem();
        row.Key.ShouldBe("billing.mode");
        row.Value.ShouldBe("visible");
        snapshot.ConfigurationManagement.RemovableRows.ShouldHaveSingleItem().Key.ShouldBe("billing.mode");
        // Pinned by equality. The absence pair that stood here ran against this type's default class
        // ToString(), which emitted only the type name, so it could not have failed for any payload.
        snapshot.ToString().ShouldBe(
            "TenantDetailSnapshot { Kind = Ready, HasDetail = True, Freshness = Current, "
            + "Lifecycle = Unknown, HasErrorMessage = False }");
    }

    [Fact]
    public async Task Get_tenant_initial_composition_failure_is_unavailable_without_raw_fallback()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(lifecycle: ProjectionLifecycleState.Current));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<TenantConfigurationComposition>(
                new InvalidOperationException("raw secret policy details")));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        snapshot.Detail.ShouldBeNull();
        snapshot.Configuration.IsAvailable.ShouldBeFalse();
        snapshot.ErrorMessage.ShouldNotBeNull().ShouldNotContain("raw secret policy details", Case.Sensitive);
    }

    [Fact]
    public async Task Get_tenant_initial_composition_propagates_caller_cancellation()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        using var cancellation = new CancellationTokenSource();
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<TenantConfigurationComposition>(cancellation.Token);
            });
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        await Should.ThrowAsync<OperationCanceledException>(() => gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            cancellation.Token));

        cancellation.IsCancellationRequested.ShouldBeTrue();
        ReadCalls(client).ShouldHaveSingleItem();
        _ = composition.Received(1).ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), cancellation.Token);
    }

    [Fact]
    public async Task Get_tenant_never_reuses_previous_safe_state_from_a_different_literal_tenant()
    {
        TenantConfigurationSafeRow priorRow = new("billing", "billing.mode", "prior-visible");
        TenantConfigurationComposition priorComposition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(Detail("tenant.other")),
            TenantConfigurationSafeModel.Available("tenant.other", [priorRow]),
            TenantConfigurationManagementContext.Available(
                "tenant.other",
                TenantStatus.Active,
                false,
                ["billing"],
                [priorRow],
                TenantConfigurationAuthorityState.TenantOwner));
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            priorComposition,
            "prior",
            ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueException(clientResponses, new InvalidOperationException("raw payload token secret stack trace correlation-123"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        snapshot.Detail.ShouldBeNull();
        string errorMessage = snapshot.ErrorMessage.ShouldNotBeNull();
        errorMessage.ShouldNotContain("raw payload", Case.Insensitive);
        errorMessage.ShouldNotContain("token", Case.Insensitive);
        errorMessage.ShouldNotContain("correlation-123", Case.Insensitive);
        snapshot.Configuration.IsAvailable.ShouldBeFalse();
        snapshot.ToString().ShouldBe(
            "TenantDetailSnapshot { Kind = Unavailable, HasDetail = False, Freshness = Unknown, "
            + "Lifecycle = Unknown, HasErrorMessage = True }");
    }

    [Fact]
    public async Task Get_tenant_never_reuses_a_prior_with_a_mismatched_management_tenant()
    {
        TenantConfigurationSafeRow priorRow = new("billing", "billing.mode", "prior-visible");
        TenantConfigurationComposition priorComposition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(Detail("tenant.alpha")),
            TenantConfigurationSafeModel.Available("tenant.alpha", [priorRow]),
            TenantConfigurationManagementContext.Available(
                "tenant.other",
                TenantStatus.Active,
                false,
                ["billing"],
                [priorRow],
                TenantConfigurationAuthorityState.TenantOwner));
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            priorComposition,
            "prior",
            ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueException(clientResponses, new InvalidOperationException("gateway unavailable"));

        TenantDetailSnapshot snapshot = await CreateGateway(client).GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        snapshot.Detail.ShouldBeNull();
        snapshot.Configuration.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_wrong_tenant_payload_without_same_tenant_prior_is_unavailable()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail(
            "tenant.other",
            new Dictionary<string, string> { ["billing.mode"] = "wrong-tenant-value" }));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        snapshot.Detail.ShouldBeNull();
        snapshot.Configuration.IsAvailable.ShouldBeFalse();
        snapshot.ToString().ShouldBe(
            "TenantDetailSnapshot { Kind = Unavailable, HasDetail = False, Freshness = Unknown, "
            + "Lifecycle = Unknown, HasErrorMessage = True }");
    }

    [Fact]
    public async Task Get_tenant_degraded_payload_retains_only_reauthorized_same_tenant_safe_rows()
    {
        TenantConfigurationSafeRow priorRow = new("billing", "billing.mode", "prior-visible");
        TenantConfigurationComposition priorComposition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(Detail("tenant.alpha")),
            TenantConfigurationSafeModel.Available("tenant.alpha", [priorRow]),
            TenantConfigurationManagementContext.Available(
                "tenant.alpha",
                TenantStatus.Active,
                false,
                ["billing"],
                [priorRow],
                TenantConfigurationAuthorityState.TenantOwner));
        TenantDetailSnapshot previous = TenantDetailSnapshot.Ready(
            priorComposition,
            "prior",
            ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.secret"] = "new-raw-secret" }),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                isDegraded: true,
                lifecycle: ProjectionLifecycleState.Degraded));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(
                """
                {
                  "Tenants": {
                    "ConfigurationReadPolicy": {
                      "PrefixGrants": [
                        { "TenantId": "tenant.alpha", "Subject": "operator-user", "Prefix": "billing" }
                      ],
                      "DisplaySafe": ["billing.mode"]
                    }
                  }
                }
                """));

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
        snapshot.Configuration.IsDegraded.ShouldBeTrue();
        snapshot.Configuration.Rows.ShouldHaveSingleItem().Value.ShouldBe("prior-visible");
        snapshot.Detail.ShouldNotBeNull().Configuration.ShouldBeEmpty();
        snapshot.ToString().ShouldBe(
            "TenantDetailSnapshot { Kind = Degraded, HasDetail = True, Freshness = Unknown, "
            + "Lifecycle = Degraded, HasErrorMessage = True }");
    }

    [Fact]
    public async Task Get_tenant_retained_reauthorization_failure_makes_configuration_unavailable()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.secret"] = "new-raw-secret" }),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                isDegraded: true,
                lifecycle: ProjectionLifecycleState.Degraded));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .ReauthorizeTenantDetailAsync(
                Arg.Any<TenantDetail>(),
                Arg.Any<TenantConfigurationSafeModel>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<TenantConfigurationComposition>(
                new InvalidOperationException("raw secret authorization details")));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        // Rows may be retained only after current policy and principal evidence are successfully reauthorized.
        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Degraded);
        snapshot.Detail.ShouldNotBeNull().Configuration.ShouldBeEmpty();
        snapshot.Configuration.IsAvailable.ShouldBeFalse();
        snapshot.Configuration.IsDegraded.ShouldBeFalse();
        snapshot.Configuration.Rows.ShouldBeEmpty();
        snapshot.ConfigurationManagement.IsAvailable.ShouldBeFalse();
        snapshot.ErrorMessage.ShouldNotBeNull().ShouldNotContain("raw secret authorization details", Case.Sensitive);
    }

    [Fact]
    public async Task Get_tenant_compose_and_reauthorization_failure_drops_prior_safe_rows()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "fresh" }),
            metadata: ProjectionBackedMetadata(isStale: false, isDegraded: false));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .ComposeTenantDetailAsync(Arg.Any<TenantDetail>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<TenantConfigurationComposition>(
                new InvalidOperationException("compose blew up with secret")));
        // Retain path reauth also fails, so current authorization cannot be proven.
        composition
            .ReauthorizeTenantDetailAsync(
                Arg.Any<TenantDetail>(),
                Arg.Any<TenantConfigurationSafeModel>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<TenantConfigurationComposition>(
                new InvalidOperationException("reauth also failed")));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        snapshot.Configuration.IsAvailable.ShouldBeFalse();
        snapshot.Configuration.IsDegraded.ShouldBeFalse();
        snapshot.Configuration.Rows.ShouldBeEmpty();
        snapshot.ErrorMessage.ShouldNotBeNull().ShouldNotContain("secret", Case.Insensitive);
    }

    [Fact]
    public async Task Get_tenant_wrong_tenant_payload_with_same_tenant_prior_retains_reauthorized_safe_rows()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail(
            "tenant.other",
            new Dictionary<string, string> { ["billing.mode"] = "wrong-tenant-value" }));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        snapshot.Detail!.Configuration.ShouldBeEmpty();
        snapshot.Configuration.IsAvailable.ShouldBeTrue();
        snapshot.Configuration.IsDegraded.ShouldBeTrue();
        snapshot.Configuration.Rows.ShouldHaveSingleItem().Value.ShouldBe("prior-visible");
        snapshot.Configuration.Rows[0].Value.ShouldNotBe("wrong-tenant-value");
    }

    [Fact]
    public async Task Get_tenant_transient_gateway_failure_with_same_tenant_prior_retains_reauthorized_safe_rows()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantDetailSnapshot snapshot = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        snapshot.Configuration.IsAvailable.ShouldBeTrue();
        snapshot.Configuration.IsDegraded.ShouldBeTrue();
        snapshot.Configuration.Rows.ShouldHaveSingleItem().Value.ShouldBe("prior-visible");
        snapshot.Detail.ShouldNotBeNull().Configuration.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_retained_reauthorization_propagates_caller_cancellation()
    {
        TenantDetailSnapshot previous = ReadyConfigurationSnapshot();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(isStale: false, isDegraded: true));
        using var cancellation = new CancellationTokenSource();
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .ReauthorizeTenantDetailAsync(
                Arg.Any<TenantDetail>(),
                Arg.Any<TenantConfigurationSafeModel>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<TenantConfigurationComposition>(cancellation.Token);
            });
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        await Should.ThrowAsync<OperationCanceledException>(() => gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous,
            cancellation.Token));

        cancellation.IsCancellationRequested.ShouldBeTrue();
        ReadCalls(client).ShouldHaveSingleItem();
        _ = composition.Received(1).ReauthorizeTenantDetailAsync(
            Arg.Any<TenantDetail>(),
            Arg.Any<TenantConfigurationSafeModel>(),
            true,
            cancellation.Token);
    }

    [Theory]
    [InlineData("trial", TenantConfigurationProjectionProofKind.SetConfirmed)]
    [InlineData("different", TenantConfigurationProjectionProofKind.SetNotConfirmed)]
    public async Task Set_configuration_projection_proof_uses_current_matching_tenant_detail_only(
        string expectedValue,
        TenantConfigurationProjectionProofKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }),
            metadata: ProjectionBackedMetadata(eTag: "etag", isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", expectedValue),
            CancellationToken.None);

        proof.TenantId.ShouldBe("tenant.alpha");
        proof.Kind.ShouldBe(expectedKind);
        NSubstitute.Core.ICall query = ReadCalls(client).ShouldHaveSingleItem();
        ((GetTenantQuery)query.GetArguments()[0]!).TenantId.ShouldBe("tenant.alpha");
        query.GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Safe_set_preview_and_proof_share_exact_fingerprint_and_ordered_projection_version()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        QueryResponseMetadata metadata = ProjectionBackedMetadata(
            isStale: false,
            lifecycle: ProjectionLifecycleState.Current,
            projectionVersion: "tenant-sequence:41");
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string> { ["Billing.Mode"] = "Enterprise" }) with
        {
            Members = [new TenantMember("operator-user", TenantRole.TenantOwner)],
        };
        EnqueueQueryResult(clientResponses,
            detail,
            metadata: metadata);
        EnqueueQueryResult(clientResponses,
            detail,
            metadata: metadata);
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(BillingGrantPolicyJson.Replace("billing", "Billing", StringComparison.Ordinal)));
        TenantSetConfigurationIntent intent = new(
            "tenant.alpha",
            "Billing",
            "Mode",
            "Billing.Mode",
            TenantSetConfigurationValueFingerprint.Create("Enterprise"));

        TenantSetConfigurationPreview preview = await gateway.GetSetConfigurationPreviewAsync(intent);
        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(intent);

        preview.IsAlreadyApplied.ShouldBeTrue();
        preview.ProjectionVersion.ShouldBe("tenant-sequence:41");
        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.SetConfirmed);
        proof.ProjectionVersion.ShouldBe("tenant-sequence:41");
        proof.Matches(intent).ShouldBeTrue();
        ReadCalls(client).All(query => query.GetArguments()[1] is null).ShouldBeTrue();
    }

    [Fact]
    public async Task Safe_remove_preview_and_proof_share_exact_fingerprint_and_ordered_projection_version()
    {
        const string rawValue = "raw-remove-secret";
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantDetail present = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Billing.Mode"] = rawValue }) with
        {
            Members = [new TenantMember("operator-user", TenantRole.TenantOwner)],
        };
        TenantDetail absent = present with
        {
            Configuration = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Billing.Other"] = rawValue,
            },
        };
        EnqueueQueryResult(clientResponses,
            present,
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "tenant-sequence:41"));
        EnqueueQueryResult(clientResponses,
            absent,
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "tenant-sequence:42"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(
                BillingGrantPolicyJson.Replace("billing", "Billing", StringComparison.Ordinal)));
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "Billing", "Billing.Mode");

        TenantRemoveConfigurationPreview preview = await gateway.GetRemoveConfigurationPreviewAsync(intent);
        TenantConfigurationProjectionProof proof = await gateway.GetRemoveConfigurationProjectionProofAsync(intent);

        preview.IsComplete.ShouldBeTrue();
        preview.CurrentState.ShouldBe(TenantRemoveConfigurationCurrentState.Present);
        preview.ProjectionVersion.ShouldBe("tenant-sequence:41");
        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.RemoveConfirmed);
        proof.ProjectionVersion.ShouldBe("tenant-sequence:42");
        TenantLifecycleProjectionVersion.Compare(preview.ProjectionVersion, proof.ProjectionVersion)
            .ShouldBe(TenantLifecycleProjectionVersionComparison.Advanced);
        proof.Matches(intent).ShouldBeTrue();
        proof.Matches(intent with { FullKey = "Billing.Other" }).ShouldBeFalse();
        proof.Matches(intent with { NamespacePrefix = "Other" }).ShouldBeFalse();
        ReadCalls(client).All(query => query.GetArguments()[1] is null).ShouldBeTrue();
    }

    [Fact]
    public async Task Remove_configuration_projection_proof_reports_matching_present_key_as_not_confirmed()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantDetail present = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Billing.Mode"] = "raw-remove-secret" }) with
        {
            Members = [new TenantMember("operator-user", TenantRole.TenantOwner)],
        };
        EnqueueQueryResult(clientResponses,
            present,
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "tenant-sequence:41"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(
                BillingGrantPolicyJson.Replace("billing", "Billing", StringComparison.Ordinal)));
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "Billing", "Billing.Mode");

        TenantConfigurationProjectionProof proof = await gateway.GetRemoveConfigurationProjectionProofAsync(intent);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.RemoveNotConfirmed);
        proof.ProjectionVersion.ShouldBe("tenant-sequence:41");
        proof.Matches(intent).ShouldBeTrue();
        proof.Matches(intent with { FullKey = "Billing.Other" }).ShouldBeFalse();
    }

    [Theory]
    [InlineData("wrong-tenant")]
    [InlineData("stale")]
    [InlineData("noncurrent-lifecycle")]
    [InlineData("unversioned")]
    public async Task Remove_configuration_projection_proof_fails_closed_for_mismatched_or_unavailable_evidence(
        string outcome)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantDetail detail = Detail(
            outcome == "wrong-tenant" ? "tenant.beta" : "tenant.alpha",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["billing.other"] = "raw-remove-secret" }) with
        {
            Members = [new TenantMember("operator-user", TenantRole.TenantOwner)],
        };
        EnqueueQueryResult(clientResponses,
            detail,
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: outcome switch
                {
                    "stale" => ProjectionLifecycleState.Stale,
                    "noncurrent-lifecycle" => ProjectionLifecycleState.Rebuilding,
                    _ => ProjectionLifecycleState.Current,
                },
                projectionVersion: outcome == "unversioned" ? null : "tenant-sequence:42"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition(BillingGrantPolicyJson));
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "billing", "billing.mode");

        TenantConfigurationProjectionProof proof = await gateway.GetRemoveConfigurationProjectionProofAsync(intent);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        proof.ProjectionVersion.ShouldBeNull();
        proof.Matches(intent).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    public async Task Configuration_projection_proof_fails_closed_when_the_projection_lifecycle_is_not_current(
        ProjectionLifecycleState lifecycle)
    {
        // Freshness alone is not proof. Both command flows and the read landmark refuse to act unless the
        // lifecycle is Current; without the same clause here a rebuilding projection reporting Current
        // freshness still produced SetConfirmed, making confirmation weaker than the submission gate.
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }),
            metadata: ProjectionBackedMetadata(eTag: "etag", isStale: false, lifecycle: lifecycle));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
    }

    [Theory]
    [InlineData(true, TenantConfigurationProjectionProofKind.RemoveNotConfirmed)]
    [InlineData(false, TenantConfigurationProjectionProofKind.RemoveConfirmed)]
    public async Task Remove_configuration_projection_proof_reports_only_key_presence(bool containsTarget, TenantConfigurationProjectionProofKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        IReadOnlyDictionary<string, string> configuration = containsTarget
            ? new Dictionary<string, string> { ["billing.mode"] = "trial" }
            : new Dictionary<string, string> { ["billing.other"] = "kept" };
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha", configuration),
            metadata: ProjectionBackedMetadata(eTag: "etag", isStale: false, lifecycle: ProjectionLifecycleState.Current));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantConfigurationProjectionProof proof = await gateway.GetRemoveConfigurationProjectionProofAsync(
            new RemoveTenantConfiguration("tenant.alpha", "billing.mode"),
            CancellationToken.None);

        proof.Kind.ShouldBe(expectedKind);
        // Also pinned: this type is a class too, so the absence pair ran against its type name.
        proof.ToString().ShouldBe(
            $"TenantConfigurationProjectionProof {{ Kind = {expectedKind}, HasTenantId = True }}");
    }

    [Theory]
    [InlineData("secret.key")]
    [InlineData("billingother.key")]
    [InlineData("Billing.mode")]
    public async Task Configuration_projection_proof_fails_closed_for_a_key_outside_policy_scope(string key)
    {
        // Without a policy gate on this path the method answers "does key K exist" and "is K equal to
        // V" for any key a caller supplies. The submitted-query assertion is the load-bearing one: the
        // gate must run before the backend read, so no oracle response is produced at all.
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha", new Dictionary<string, string> { [key] = "hidden" }));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantConfigurationProjectionProof proof = await gateway.GetRemoveConfigurationProjectionProofAsync(
            new RemoveTenantConfiguration("tenant.alpha", key),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Configuration_projection_proof_fails_closed_without_a_composition_seam()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Configuration_projection_proof_fails_closed_when_policy_is_unavailable()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }));
        TenantQueryGateway gateway = CreateGateway(
            client,
            bffComposition: ConfigurationComposition("{ \"Tenants\": { \"ConfigurationReadPolicy\": { \"PrefixGrants\": \"scalar\", \"DisplaySafe\": [] } } }"));

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Configuration_projection_proof_contains_policy_authorization_failure()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        ITenantsBffComposition composition = Substitute.For<ITenantsBffComposition>();
        composition
            .IsConfigurationKeyAuthorizedAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<bool>(
                new InvalidOperationException("raw secret authorization details")));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: composition);

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_reports_a_payloadless_success_with_no_prior_as_unknown_rather_than_degraded()
    {
        // AC5 requires unknown and degraded to stay distinct. Routing a null payload through the
        // retention path labelled the surface degraded, which tells the user last-confirmed evidence is
        // being shown when there is none to show.
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult<TenantDetail>(clientResponses, null!);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), previous: null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unknown);
        snapshot.Detail.ShouldBeNull();
    }

    [Fact]
    public async Task Configuration_projection_proof_rejects_wrong_tenant_payload()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("tenant.other", new Dictionary<string, string> { ["billing.mode"] = "trial" }));
        TenantQueryGateway gateway = CreateGateway(client, bffComposition: ConfigurationComposition(BillingGrantPolicyJson));

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        proof.TenantId.ShouldBe("tenant.alpha");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("not-modified")]
    [InlineData("stale")]
    [InlineData("degraded")]
    [InlineData("unknown")]
    [InlineData("exception")]
    public async Task Configuration_projection_proof_fails_closed_without_current_payload(string outcome)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        switch (outcome)
        {
            case "missing":
                EnqueueDetailResult(clientResponses, null, ProjectionBackedMetadata(isStale: false));
                break;
            case "not-modified":
                EnqueueDetailNotModified(clientResponses, "etag");
                break;
            case "stale":
                EnqueueQueryResult(clientResponses, Detail("tenant.alpha"), metadata: ProjectionBackedMetadata(isStale: true));
                break;
            case "degraded":
                EnqueueQueryResult(clientResponses, Detail("tenant.alpha"), metadata: ProjectionBackedMetadata(isStale: false, isDegraded: true));
                break;
            case "unknown":
                EnqueueQueryResult(clientResponses, Detail("tenant.alpha"), metadata: new QueryResponseMetadata(IsStale: false));
                break;
            case "exception":
                EnqueueException(clientResponses, new InvalidOperationException("raw projection secret"));
                break;
        }

        TenantQueryGateway gateway = CreateGateway(client);
        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        proof.ToString().ShouldBe(
            "TenantConfigurationProjectionProof { Kind = Unavailable, HasTenantId = True }");
    }

    [Fact]
    public async Task Configuration_projection_proof_without_authenticated_user_does_not_query()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, userId: null);

        TenantConfigurationProjectionProof proof = await gateway.GetSetConfigurationProjectionProofAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "trial"),
            CancellationToken.None);

        proof.Kind.ShouldBe(TenantConfigurationProjectionProofKind.Unavailable);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_without_previous_snapshot_reports_unavailable_when_second_request_fails()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailNotModified(clientResponses, "known");
        EnqueueException(clientResponses, new InvalidOperationException("unconditional read failed with secret"));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha", ETag: "known"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
        snapshot.Detail.ShouldBeNull();
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_with_etag_but_no_freshness_metadata_reports_unknown()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            eTag: "tenant-etag",
            metadata: new QueryResponseMetadata(ETag: "tenant-etag"));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Theory]
    [InlineData(401, TenantDetailSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, TenantDetailSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(404, TenantDetailSurfaceKind.NotFound, TenantsRestQueryFailureKind.NotFound)]
    [InlineData(503, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    // Decision D3 stopped flattening every 5xx to 503, so the raw status now reaches this mapper. It must
    // treat the whole range as an outage: 500/502/504 were enumerated but 501, 505, 506, 507, 508, 510 and
    // 511 fell to the Degraded default arm, which claims retained evidence on a first load that has none.
    // Deleting the `>= 500 and < 600` arm sends every row below to Degraded.
    [InlineData(500, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(501, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(502, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(504, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(507, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(511, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    // Decision D-I distinguishes a route identity rejected locally from a 400 returned by the server. This
    // native InvalidRequest models the latter: a server rejection is not proof that the tenant does not exist.
    [InlineData(400, TenantDetailSurfaceKind.Unavailable, TenantsRestQueryFailureKind.InvalidRequest)]
    public async Task Get_tenant_maps_gateway_status_to_safe_detail_state(int statusCode, TenantDetailSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(true, false, TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale)]
    [InlineData(false, true, TenantDetailSurfaceKind.Degraded, ReadModelFreshnessState.Unknown)]
    public async Task Get_tenant_maps_stale_and_degraded_metadata_to_safe_states(
        bool isStale,
        bool isDegraded,
        TenantDetailSurfaceKind expectedKind,
        ReadModelFreshnessState expectedFreshness)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(isStale: isStale, isDegraded: isDegraded));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantDetailSnapshot snapshot = await gateway
            .GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        if (isDegraded)
        {
            snapshot.Detail.ShouldBeNull();
        }
        else
        {
            snapshot.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task List_tenants_without_authenticated_user_fails_closed_without_querying_dependencies(string? userId)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, userId);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(Search: "term"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Unauthorized);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_tenants_passes_cursor_without_offset_conversion()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            "next-cursor",
            true));
        EnqueueQueryResult(clientResponses, new TenantDetail(
            "tenant.alpha",
            "Alpha",
            null,
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ],
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(Cursor: "opaque-cursor", PageSize: 10), null, CancellationToken.None);

        NSubstitute.Core.ICall listQuery = ReadCalls(client)[0];
        ListTenantsQuery payload = (ListTenantsQuery)listQuery.GetArguments()[0]!;
        payload.Cursor.ShouldBe("opaque-cursor");
        snapshot.NextCursor.ShouldBe("next-cursor");
        snapshot.Rows.ShouldHaveSingleItem().MemberCount.ShouldBe(TenantCountValue.Known(2));
        snapshot.Rows[0].OwnerCount.ShouldBe(TenantCountValue.Known(1));
    }

    [Fact]
    public async Task List_tenants_requeries_page_one_once_for_safe_invalid_cursor_reason()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, 400);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            "fresh-protected-cursor",
            true));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(
                Cursor: "expired-protected-cursor",
                PageSize: 50,
                ETag: "stale-etag"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(3);
        ((ListTenantsQuery)ReadCalls(client)[0].GetArguments()[0]!).Cursor.ShouldBe("expired-protected-cursor");
        ReadCalls(client)[0].GetArguments()[1].ShouldBeNull();
        ((ListTenantsQuery)ReadCalls(client)[1].GetArguments()[0]!).Cursor.ShouldBeNull();
        ((ListTenantsQuery)ReadCalls(client)[1].GetArguments()[0]!).PageSize.ShouldBe(50);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        snapshot.NextCursor.ShouldBe("fresh-protected-cursor");
        snapshot.Notice.ShouldBe(TenantListReason.ListRefreshed);
    }

    [Fact]
    public async Task List_tenants_maps_typed_transport_failure_before_recovering_invalid_cursor()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(
                Arg.Any<ListTenantsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new TenantsRestQueryResponse<PaginatedResult<TenantSummary>>(
                    null,
                    new QueryResponseMetadata(),
                    TenantsRestQueryFailureKind.InvalidCursor,
                    (int)HttpStatusCode.BadRequest),
                new TenantsRestQueryResponse<PaginatedResult<TenantSummary>>(
                    new PaginatedResult<TenantSummary>([], Cursor: null, HasMore: false),
                    ProjectionBackedMetadata(
                        isStale: false,
                        lifecycle: ProjectionLifecycleState.Current,
                        projectionVersion: "tenant-list-v1"),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.OK));
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns("operator-user");
        TenantQueryGateway gateway = new(
            client,
            userContext,
            new StubMemoriesClient(),
            new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()));

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "expired-protected-cursor", ETag: "stale-etag"),
            previous: null,
            CancellationToken.None);

        await client.Received(1).ListTenantsAsync(
            Arg.Is<ListTenantsQuery>(query => query != null && query.Cursor == "expired-protected-cursor"),
            null,
            Arg.Any<CancellationToken>());
        await client.Received(1).ListTenantsAsync(
            Arg.Is<ListTenantsQuery>(query => query != null && query.Cursor == null),
            null,
            Arg.Any<CancellationToken>());
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        snapshot.Notice.ShouldBe(TenantListReason.ListRefreshed);
    }

    [Fact]
    public async Task List_tenants_invalid_cursor_retry_failure_is_sanitized_and_not_retried_again()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, 400);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "expired-protected-cursor"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(2);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Error);
        snapshot.Reason.ShouldBe(TenantListReason.GatewayUnavailable);
        snapshot.Notice.ShouldBe(TenantListReason.None);
    }

    [Fact]
    public async Task List_tenants_does_not_retry_unrecognized_bad_request_as_invalid_cursor()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidRequest, 400);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Cursor: "opaque-protected-cursor"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(1);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Error);
        snapshot.Reason.ShouldBe(TenantListReason.GatewayUnavailable);
        snapshot.Notice.ShouldBe(TenantListReason.None);
    }

    [Fact]
    public async Task List_tenants_maps_authorized_empty_without_error()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        snapshot.IsAuthorizationScopedEmpty.ShouldBeTrue();
        snapshot.Reason.ShouldBe(TenantListReason.None);
    }

    [Fact]
    public async Task Get_global_administrators_submits_fixed_platform_scope_query()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<GlobalAdministratorSummary>(
            [new GlobalAdministratorSummary("admin-1")],
            "next-cursor",
            true));

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(Cursor: "opaque-cursor", PageSize: 10, ETag: "known"), null, CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client).ShouldHaveSingleItem();
        query.GetArguments()[1].ShouldBeNull();
        GetGlobalAdministratorsQuery payload = (GetGlobalAdministratorsQuery)query.GetArguments()[0]!;
        payload.Cursor.ShouldBe("opaque-cursor");
        payload.PageSize.ShouldBe(10);
        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
        snapshot.NextCursor.ShouldBe("next-cursor");
        snapshot.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_global_administrators_preserves_previous_rows_for_not_modified()
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueGlobalAdministratorsNotModified(clientResponses, "known");

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
        snapshot.ETag.ShouldBe("known");
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
    }

    [Fact]
    public async Task Get_global_administrators_applies_stale_freshness_from_not_modified_response()
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueGlobalAdministratorsNotModified(clientResponses, "known", isStale: true, lifecycle: ProjectionLifecycleState.Stale);

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Reason.ShouldBe(GlobalAdministratorsReason.ProjectionStale);
    }

    [Theory]
    [InlineData(false, GlobalAdministratorsSurfaceKind.Ready)]
    [InlineData(true, GlobalAdministratorsSurfaceKind.Empty)]
    public async Task Get_global_administrators_current_not_modified_promotes_unknown_truth_and_recomputes_completeness(
        bool empty,
        GlobalAdministratorsSurfaceKind expectedKind)
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Unknown(
            empty ? [] : [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Unknown)],
            nextCursor: null,
            hasMore: false,
            eTag: "known") with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ProjectionVersion = "projection-old",
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueGlobalAdministratorsNotModified(clientResponses,
            "known",
            isStale: false,
            lifecycle: ProjectionLifecycleState.Current,
            projectionVersion: "projection-current");

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client)
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: "known"),
                previous,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
        snapshot.ProjectionVersion.ShouldBe("projection-current");
        snapshot.IsCompleteEvidence.ShouldBeTrue();
        snapshot.IsAuthorizationScopedEmpty.ShouldBe(empty);
        snapshot.Reason.ShouldBe(GlobalAdministratorsReason.None);
        snapshot.Rows.ShouldAllBe(static row => row.Freshness == ReadModelFreshnessState.Current);
    }

    // ResolveNotModifiedFreshness carries its own AD-15 provenance gate, because its fall-through
    // returns the retained `previous` freshness WITHOUT passing through ResolveFreshness. Without
    // these rows, deleting either the provenance gate or the lifecycle clause leaves the suite green
    // while a non-projection-backed 304 keeps re-affirming a Current claim.
    [Theory]
    [InlineData(false, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Unknown, null, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, GlobalAdministratorsSurfaceKind.Degraded, GlobalAdministratorsReason.GatewayFailure)]
    [InlineData(true, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, GlobalAdministratorsSurfaceKind.Unknown, GlobalAdministratorsReason.None)]
    [InlineData(true, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, GlobalAdministratorsSurfaceKind.Unknown, GlobalAdministratorsReason.None)]
    [InlineData(true, (QueryResponseProvenance)999, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, GlobalAdministratorsSurfaceKind.Unknown, GlobalAdministratorsReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Stale, GlobalAdministratorsSurfaceKind.Stale, GlobalAdministratorsReason.ProjectionStale)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, GlobalAdministratorsSurfaceKind.Ready, GlobalAdministratorsReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Degraded, false, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Degraded, GlobalAdministratorsSurfaceKind.Degraded, GlobalAdministratorsReason.ProjectionDegraded)]
    public async Task Get_global_administrators_not_modified_gates_freshness_on_provenance_and_lifecycle(
        bool emitMetadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        bool? isStale,
        ReadModelFreshnessState expectedFreshness,
        ProjectionLifecycleState expectedLifecycle,
        GlobalAdministratorsSurfaceKind expectedKind,
        GlobalAdministratorsReason expectedReason)
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueGlobalAdministratorsNotModified(clientResponses, "known", isStale, lifecycle, provenance, emitMetadata);

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(ETag: "known"), previous, CancellationToken.None);

        // Matching retained evidence with a missing version degrades without an unconditional refetch.
        ReadCalls(client).ShouldHaveSingleItem();
        clientResponses.ShouldBeEmpty();
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(expectedLifecycle);
        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(expectedLifecycle);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, GlobalAdministratorsSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, GlobalAdministratorsSurfaceKind.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding, true, ReadModelFreshnessState.Unknown, GlobalAdministratorsSurfaceKind.Unknown)]
    public async Task Get_global_administrators_projection_lifecycle_precedes_legacy_stale_evidence(
        ProjectionLifecycleState lifecycle,
        bool isStale,
        ReadModelFreshnessState expectedFreshness,
        GlobalAdministratorsSurfaceKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle));

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client)
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(lifecycle);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(lifecycle);
    }

    // Degradation describes confirmed rows held under reduced confidence. A failed FIRST read holds none, so
    // retaining from it produced Kind = Degraded with an empty row set, and the page then rendered
    // "Last confirmed administrators remain visible" over an empty table. Unavailable() has a null cursor and
    // the default page size, so it matched its own page scope and validator on Retry and slipped through.
    [Fact]
    public async Task Get_global_administrators_first_load_failure_cannot_be_retained_as_degraded()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);

        TenantQueryGateway gateway = CreateGateway(client);
        GlobalAdministratorsSnapshot first = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        first.Rows.ShouldBeEmpty();
        first.Kind.ShouldNotBe(GlobalAdministratorsSurfaceKind.Degraded);

        // Retry with the same scope and no validator, still failing. The empty prior snapshot must not be
        // promoted into "last confirmed" evidence.
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);
        GlobalAdministratorsSnapshot retried = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), first, CancellationToken.None);

        retried.Rows.ShouldBeEmpty();
        retried.Kind.ShouldNotBe(GlobalAdministratorsSurfaceKind.Degraded);
        retried.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        retried.IsCompleteEvidence.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_global_administrators_retains_confirmed_rows_as_degraded_on_a_later_failure()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "global-admin-v7"));

        TenantQueryGateway gateway = CreateGateway(client);
        GlobalAdministratorsSnapshot confirmed = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);
        confirmed.Rows.ShouldHaveSingleItem();

        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);
        GlobalAdministratorsSnapshot degraded = await gateway
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: confirmed.ETag),
                confirmed,
                CancellationToken.None);

        // Real retention: rows genuinely were confirmed, so Degraded is honest here.
        degraded.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Degraded);
        degraded.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
        degraded.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        degraded.IsCompleteEvidence.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, false, GlobalAdministratorsSurfaceKind.Stale, ReadModelFreshnessState.Stale)]
    [InlineData(false, true, GlobalAdministratorsSurfaceKind.Degraded, ReadModelFreshnessState.Unknown)]
    public async Task Get_global_administrators_maps_stale_and_degraded_metadata_without_losing_rows(
        bool isStale,
        bool isDegraded,
        GlobalAdministratorsSurfaceKind expectedKind,
        ReadModelFreshnessState expectedFreshness)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(isStale: isStale, isDegraded: isDegraded));

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData((QueryResponseProvenance)999)]
    public async Task Get_global_administrators_non_projection_stale_evidence_remains_unknown(
        QueryResponseProvenance provenance)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: new QueryResponseMetadata(IsStale: true)
            {
                Provenance = provenance,
            });

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unknown);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public async Task Get_global_administrators_only_marks_current_first_page_without_more_rows_complete()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "global-admin-v7"));
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-2")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "global-admin-v8"));

        TenantQueryGateway gateway = CreateGateway(client);
        GlobalAdministratorsSnapshot firstPage = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);
        GlobalAdministratorsSnapshot laterPage = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(Cursor: "opaque-page-two"),
            previous: null,
            CancellationToken.None);

        firstPage.IsCompleteEvidence.ShouldBeTrue();
        laterPage.IsCompleteEvidence.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Get_global_administrators_requires_a_non_blank_projection_version_for_complete_evidence(
        string? projectionVersion)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: projectionVersion));

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.IsCompleteEvidence.ShouldBeFalse();
    }

    // Review loop 9. The kind census over this file was Unavailable x20, None x7, InvalidCursor x4,
    // InvalidRequest x3, Unauthorized/Forbidden/NotFound x2 each, Timeout x1, InvalidPayload x1,
    // InvalidMetadata/Unknown x0 -- and the auth kinds appeared ONLY against GetTenantUsersAsync, which
    // switches on FailureKind and bypasses ToEventStoreResult entirely. So `or Unauthorized or Forbidden or
    // NotFound` could be added to the 503 override and every test still passed, while a real 401 on the
    // detail or global-administrator read rendered as an outage instead of offering sign-in.
    [Theory]
    [InlineData(TenantsRestQueryFailureKind.Unauthorized, 401, TenantDetailSurfaceKind.Unauthorized)]
    [InlineData(TenantsRestQueryFailureKind.Forbidden, 403, TenantDetailSurfaceKind.Unauthorized)]
    [InlineData(TenantsRestQueryFailureKind.NotFound, 404, TenantDetailSurfaceKind.NotFound)]
    public async Task Authorization_failures_reach_the_detail_mapper_as_themselves(
        TenantsRestQueryFailureKind failureKind,
        int statusCode,
        TenantDetailSurfaceKind expected)
    {
        TenantDetailSnapshot snapshot = await CreateGateway(
                new FixedFailureRestQueryClient(failureKind, statusCode))
            .GetTenantAsync(
                new TenantDetailRequest("tenant.alpha"),
                previous: null,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.Kind.ShouldNotBe(TenantDetailSurfaceKind.Unavailable);
    }

    [Theory]
    [InlineData(TenantsRestQueryFailureKind.InvalidRequest, TenantDetailSurfaceKind.Unavailable)]
    [InlineData(TenantsRestQueryFailureKind.UnsupportedRouteIdentifier, TenantDetailSurfaceKind.NotFound)]
    public async Task Server_rejection_and_locally_unsupported_route_identity_remain_distinguishable(
        TenantsRestQueryFailureKind failureKind,
        TenantDetailSurfaceKind expected)
    {
        TenantDetailSnapshot snapshot = await CreateGateway(
                new FixedFailureRestQueryClient(failureKind, (int)HttpStatusCode.BadRequest))
            .GetTenantAsync(
                new TenantDetailRequest("tenant.alpha"),
                previous: null,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TenantsRestQueryFailureKind.Unauthorized, 401)]
    [InlineData(TenantsRestQueryFailureKind.Forbidden, 403)]
    public async Task Authorization_failures_reach_the_global_administrator_mapper_as_themselves(
        TenantsRestQueryFailureKind failureKind,
        int statusCode)
    {
        GlobalAdministratorsSnapshot snapshot = await CreateGateway(
                new FixedFailureRestQueryClient(failureKind, statusCode))
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(),
                previous: null,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unauthorized);
        snapshot.IsCompleteEvidence.ShouldBeFalse();
    }

    // Review loop 9, decision D3. AC6 requires error to stay distinct from unavailable. Before this change
    // ToEventStoreResult flattened EVERY unclassified status into 503, so GlobalAdministratorsSnapshot.Error()
    // had no reachable producer: the tenants-global-admins-error copy plus its Retry/Reset affordances shipped
    // dead, while the tests that "covered" it reached it only by throwing EventStoreGatewayException straight
    // at the mapper through the harness -- which bypasses ToEventStoreResult entirely and so proved nothing
    // about the status the gateway would really see. These tests drive the REAL ITenantsRestQueryClient seam.
    [Theory]
    [InlineData(500, GlobalAdministratorsSurfaceKind.Error)]
    [InlineData(502, GlobalAdministratorsSurfaceKind.Error)]
    [InlineData(504, GlobalAdministratorsSurfaceKind.Error)]
    [InlineData(503, GlobalAdministratorsSurfaceKind.Unavailable)]
    [InlineData(501, GlobalAdministratorsSurfaceKind.Unavailable)]
    public async Task A_server_error_status_is_distinguished_from_a_declared_outage(
        int statusCode,
        GlobalAdministratorsSurfaceKind expected)
    {
        GlobalAdministratorsSnapshot snapshot = await CreateGateway(
                new FixedFailureRestQueryClient(TenantsRestQueryFailureKind.Unavailable, statusCode))
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(),
                previous: null,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.IsCompleteEvidence.ShouldBeFalse();
    }

    [Theory]
    // A non-200 SUCCESS code is not a server fault; it must keep collapsing to the declared-outage code so it
    // cannot reach a mapper's default arm as if the server had failed. This is the guard that stops D3's
    // change widening past real 5xx responses.
    [InlineData(TenantsRestQueryFailureKind.Unavailable, 204)]
    // Transport-level kinds carry no meaningful HTTP status and stay normalized to 503, which is what keeps
    // a corrupt payload or unusable metadata from re-presenting retained rows as "last confirmed".
    [InlineData(TenantsRestQueryFailureKind.InvalidPayload, 200)]
    [InlineData(TenantsRestQueryFailureKind.InvalidMetadata, 304)]
    [InlineData(TenantsRestQueryFailureKind.Timeout, 503)]
    public async Task Statuses_without_a_meaningful_server_fault_still_normalize_to_unavailable(
        TenantsRestQueryFailureKind failureKind,
        int statusCode)
    {
        GlobalAdministratorsSnapshot snapshot = await CreateGateway(
                new FixedFailureRestQueryClient(failureKind, statusCode))
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(),
                previous: null,
                CancellationToken.None);

        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unavailable);
    }

    [Fact]
    public async Task Get_global_administrators_requires_projection_lifecycle_evidence_for_complete_evidence()
    {
        // The projection-version clause of IsCompleteGlobalAdministratorsEvidence has the theory above; the
        // lifecycle clause had no counterpart, and the diff added two lifecycle tests to the Grant twin and
        // none to Remove.
        //
        // The pairing below -- is-stale false with lifecycle Unknown -- is NOT something the owned producer
        // can emit: every handler goes through ToQueryResponseMetadata, which couples Lifecycle = Unknown to
        // IsStale = null. (An earlier version of this comment claimed it was "the reachable wire shape";
        // that was wrong, and review loop 10 corrected it.) It is a non-conforming-producer shape, and
        // asserting it is still worth doing: this gate is what makes the surface fail closed against one.
        // Delete `lifecycle == ProjectionLifecycleState.Current` from that predicate and such a producer can
        // have the absence of a row reported to the operator as a *confirmed* removal of a global
        // administrator. Decision D-F (2026-07-31) reversed D6 for exactly this class of evidence.
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Unknown,
                projectionVersion: "global-admin-v8"));

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);

        // Freshness is Current and the page is a complete first page, so every other clause of the
        // predicate is satisfied: only the lifecycle clause can be holding the gate closed.
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        snapshot.IsCompleteEvidence.ShouldBeFalse();
        snapshot.IsMutationEvidenceBacked.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_global_administrators_invalid_later_cursor_recovers_to_page_one_honestly()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, 400);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("admin-1")], null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "global-admin-v9"));

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(Cursor: "expired-protected-cursor"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(2);
        ((GetGlobalAdministratorsQuery)ReadCalls(client)[1].GetArguments()[0]!).Cursor.ShouldBeNull();
        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Reason.ShouldBe(GlobalAdministratorsReason.PageRecovered);
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
    }

    [Theory]
    [InlineData(500, GlobalAdministratorsSurfaceKind.Error, GlobalAdministratorsReason.GatewayFailure, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(400, GlobalAdministratorsSurfaceKind.Invalid, GlobalAdministratorsReason.GatewayFailure, TenantsRestQueryFailureKind.InvalidRequest)]
    [InlineData(503, GlobalAdministratorsSurfaceKind.Unavailable, GlobalAdministratorsReason.GatewayUnavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(403, GlobalAdministratorsSurfaceKind.Unauthorized, GlobalAdministratorsReason.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    public async Task Get_global_administrators_failed_first_page_retry_preserves_the_real_failure(
        int retryStatus,
        GlobalAdministratorsSurfaceKind expectedKind,
        GlobalAdministratorsReason expectedReason, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, 400);
        EnqueueFailure(clientResponses, failureKind, retryStatus);

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(Cursor: "expired-protected-cursor"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(2);
        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.PagingRecovered.ShouldBeFalse();
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Incomplete_gateway_page_cannot_confirm_absence_for_a_remove_consumer()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<GlobalAdministratorSummary>([new GlobalAdministratorSummary("other-admin")], "next-page", true),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: "projection-v10"));
        GlobalAdministratorsSnapshot page = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);
        GlobalAdministratorRemoveCommandSnapshot pending = GlobalAdministratorRemoveCommandSnapshot
            .Idle()
            .Preview(
                GlobalAdministratorRemovePreview.Create(
                    "target-admin",
                    "operator-admin",
                    GlobalAdministratorsSnapshot.Ready(
                        [
                            new GlobalAdministratorRow("target-admin", ReadModelFreshnessState.Current),
                            new GlobalAdministratorRow("other-admin", ReadModelFreshnessState.Current),
                        ],
                        null,
                        false,
                        "\"baseline\"",
                        ReadModelFreshnessState.Current) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
                        Lifecycle = ProjectionLifecycleState.Current,
                        ProjectionVersion = "projection-v9",
                        IsCompleteEvidence = true,
                    },
                    isAuthorized: true),
                "01J00000000000000000000000")
            .RequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted("01J00000000000000000000000", "correlation-1"))
            .ApplyStatus(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1));

        GlobalAdministratorRemoveCommandSnapshot confirmation = pending.ConfirmProjection(page);

        page.IsCompleteEvidence.ShouldBeFalse();
        confirmation.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        confirmation.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        confirmation.FocusTarget.ShouldBe(TenantCommandFocusTarget.Refresh);
    }

    [Fact]
    public async Task Get_global_administrators_does_not_recover_an_unrecognized_bad_request()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidRequest, 400);

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client).GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(Cursor: "protected-cursor"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).ShouldHaveSingleItem();
        snapshot.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Invalid);
        snapshot.PagingRecovered.ShouldBeFalse();
        snapshot.Reason.ShouldBe(GlobalAdministratorsReason.GatewayFailure);
    }

    [Fact]
    public void Global_administrator_diagnostics_omit_protected_and_identity_values()
    {
        GlobalAdministratorsRequest request = new("opaque-cursor-secret", 20, "etag-secret");
        GlobalAdministratorsSnapshot snapshot = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("literal-admin-secret", ReadModelFreshnessState.Current)],
            "next-cursor-secret",
            hasMore: true,
            "etag-secret",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            ProjectionVersion = "projection-secret",
        };

        string diagnostics = $"{request} {snapshot}";

        diagnostics.ShouldNotContain("opaque-cursor-secret");
        diagnostics.ShouldNotContain("next-cursor-secret");
        diagnostics.ShouldNotContain("etag-secret");
        diagnostics.ShouldNotContain("literal-admin-secret");
        diagnostics.ShouldNotContain("projection-secret");
    }

    [Theory]
    [InlineData(401, GlobalAdministratorsSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, GlobalAdministratorsSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(400, GlobalAdministratorsSurfaceKind.Invalid, TenantsRestQueryFailureKind.InvalidRequest)]
    [InlineData(404, GlobalAdministratorsSurfaceKind.Unavailable, TenantsRestQueryFailureKind.NotFound)]
    [InlineData(501, GlobalAdministratorsSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(503, GlobalAdministratorsSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(500, GlobalAdministratorsSurfaceKind.Error, TenantsRestQueryFailureKind.Unavailable)]
    public async Task Get_global_administrators_maps_gateway_status_to_safe_snapshot_state(
        int statusCode,
        GlobalAdministratorsSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot snapshot = await gateway
            .GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.Rows.ShouldBeEmpty();
        if (statusCode == 500)
        {
            snapshot.Reason.ShouldBe(GlobalAdministratorsReason.GatewayFailure);
        }
        ReadCalls(client).Count.ShouldBe(1);
        ReadCalls(client).ShouldAllBe(call => call.GetArguments()[0] is GetGlobalAdministratorsQuery);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Get_tenant_audit_without_authenticated_user_fails_closed_without_querying_event_store(string? userId)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, userId);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Unauthorized);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_audit_submits_exact_audit_query_shape_and_preserves_opaque_cursor()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry(
                "event-1",
                AuditEventCategory.Access,
                timestamp: DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture)) with
                {
                    CommandCorrelationId = "raw-command-correlation",
                    AggregateSequence = 42,
                }],
            "next-audit-cursor",
            true));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(
                new TenantAuditRequest(
                    "tenant.alpha",
                    From: DateTimeOffset.Parse("2026-06-01T00:00:00Z", CultureInfo.InvariantCulture),
                    To: DateTimeOffset.Parse("2026-06-02T00:00:00Z", CultureInfo.InvariantCulture),
                    Category: AuditEventCategory.Access,
                    Cursor: "opaque-audit-cursor",
                    PageSize: 25,
                    ETag: "known"),
                null,
                CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client).ShouldHaveSingleItem();
        ((GetTenantAuditQuery)query.GetArguments()[0]!).TenantId.ShouldBe("tenant.alpha");
        query.GetArguments()[1].ShouldBeNull();
        GetTenantAuditQuery payload = (GetTenantAuditQuery)query.GetArguments()[0]!;
        payload.From.ShouldBe(DateTimeOffset.Parse("2026-06-01T00:00:00Z", CultureInfo.InvariantCulture));
        payload.To.ShouldBe(DateTimeOffset.Parse("2026-06-02T00:00:00Z", CultureInfo.InvariantCulture));
        payload.Category.ToString().ShouldBe(nameof(AuditEventCategory.Access));
        payload.Cursor.ShouldBe("opaque-audit-cursor");
        payload.PageSize.ShouldBe(25);
        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().CommandCorrelationId.ShouldBeNull();
        snapshot.Rows.ShouldHaveSingleItem().AggregateSequence.ShouldBeNull();
        snapshot.NextCursor.ShouldBe("next-audit-cursor");
        snapshot.HasMore.ShouldBeTrue();
        snapshot.Rows.ShouldHaveSingleItem().ReferenceContext.ShouldContain("userId: target-user");
    }

    [Fact]
    public async Task Get_tenant_audit_requeries_page_one_for_invalid_cursor_and_reports_list_refreshed()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, 400);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-2", AuditEventCategory.Administrative)],
            "fresh-cursor",
            true));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(
                new TenantAuditRequest(
                    "tenant.alpha",
                    Category: AuditEventCategory.Administrative,
                    Cursor: "expired-protected-cursor",
                    PageSize: 25),
                null,
                CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(2);
        ((GetTenantAuditQuery)ReadCalls(client)[0].GetArguments()[0]!).Cursor.ShouldBe("expired-protected-cursor");
        ((GetTenantAuditQuery)ReadCalls(client)[1].GetArguments()[0]!).Cursor.ShouldBeNull();
        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.ListRefreshed);
        snapshot.Reason.ShouldBe(TenantAuditReason.ListRefreshed);
        snapshot.NextCursor.ShouldBe("fresh-cursor");
    }

    [Theory]
    [InlineData(true, false, TenantAuditSurfaceKind.Stale, ReadModelFreshnessState.Stale)]
    [InlineData(false, true, TenantAuditSurfaceKind.Degraded, ReadModelFreshnessState.Unknown)]
    public async Task Get_tenant_audit_maps_stale_and_degraded_metadata_to_distinct_states(
        bool isStale,
        bool isDegraded,
        TenantAuditSurfaceKind expectedKind,
        ReadModelFreshnessState expectedFreshness)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantAuditEntry>([AuditEntry("event-3", AuditEventCategory.Access)], null, false),
            metadata: ProjectionBackedMetadata(isStale: isStale, isDegraded: isDegraded));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(expectedFreshness);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData((QueryResponseProvenance)999)]
    public async Task Get_tenant_audit_non_projection_stale_evidence_remains_unknown(
        QueryResponseProvenance provenance)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantAuditEntry>([AuditEntry("event-3", AuditEventCategory.Access)], null, false),
            metadata: new QueryResponseMetadata(IsStale: true)
            {
                Provenance = provenance,
            });
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public async Task Get_tenant_audit_reuses_not_modified_snapshot_only_for_same_scope()
    {
        TenantAuditRequest originalRequest = new("tenant.alpha", Category: AuditEventCategory.Access, ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-4", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            originalRequest) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueAuditNotModified(clientResponses, "known");
        EnqueueAuditNotModified(clientResponses, "known");
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-5", AuditEventCategory.Administrative)],
            Cursor: null,
            HasMore: false));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot sameScope = await gateway
            .GetTenantAuditAsync(originalRequest, previous, CancellationToken.None);
        TenantAuditSnapshot differentScope = await gateway
            .GetTenantAuditAsync(originalRequest with { Category = AuditEventCategory.Administrative }, previous, CancellationToken.None);

        sameScope.Rows.ShouldHaveSingleItem().EventReference.ShouldBe("event-4");
        differentScope.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        differentScope.Rows.ShouldHaveSingleItem().EventReference.ShouldBe("event-5");
        ReadCalls(client)[2].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_tenant_audit_applies_stale_freshness_from_not_modified_response()
    {
        TenantAuditRequest request = new("tenant.alpha", Category: AuditEventCategory.Access, ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-4", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueAuditNotModified(clientResponses, "known", isStale: true, lifecycle: ProjectionLifecycleState.Stale);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Reason.ShouldBe(TenantAuditReason.ProjectionStale);
    }

    [Theory]
    [InlineData(false, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Unknown, null, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, QueryResponseProvenance.Unknown, TenantAuditSurfaceKind.Degraded, TenantAuditReason.GatewayFailure)]
    [InlineData(true, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, QueryResponseProvenance.HandlerComputed, TenantAuditSurfaceKind.Ready, TenantAuditReason.None)]
    [InlineData(true, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, QueryResponseProvenance.Unknown, TenantAuditSurfaceKind.Ready, TenantAuditReason.None)]
    [InlineData(true, (QueryResponseProvenance)999, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, QueryResponseProvenance.Unknown, TenantAuditSurfaceKind.Ready, TenantAuditReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Stale, QueryResponseProvenance.ProjectionBacked, TenantAuditSurfaceKind.Stale, TenantAuditReason.ProjectionStale)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked, TenantAuditSurfaceKind.Ready, TenantAuditReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Degraded, false, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Degraded, QueryResponseProvenance.ProjectionBacked, TenantAuditSurfaceKind.Ready, TenantAuditReason.None)]
    public async Task Get_tenant_audit_not_modified_gates_freshness_on_provenance_and_lifecycle(
        bool emitMetadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        bool? isStale,
        ReadModelFreshnessState expectedFreshness,
        ProjectionLifecycleState expectedLifecycle,
        QueryResponseProvenance expectedProvenance,
        TenantAuditSurfaceKind expectedKind,
        TenantAuditReason expectedReason)
    {
        TenantAuditRequest request = new("tenant.alpha", Category: AuditEventCategory.Access, ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-4", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueAuditNotModified(clientResponses, "known", isStale, lifecycle, provenance, emitMetadata);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(expectedLifecycle);
        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(expectedLifecycle);
        snapshot.Rows.ShouldHaveSingleItem().Provenance.ShouldBe(expectedProvenance);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, TenantAuditSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, TenantAuditSurfaceKind.Stale)]
    [InlineData(ProjectionLifecycleState.Degraded, true, ReadModelFreshnessState.Unknown, TenantAuditSurfaceKind.Ready)]
    public async Task Get_tenant_audit_projection_lifecycle_precedes_legacy_stale_evidence(
        ProjectionLifecycleState lifecycle,
        bool isStale,
        ReadModelFreshnessState expectedFreshness,
        TenantAuditSurfaceKind expectedKind)
    {
        TenantAuditRequest request = new("tenant.alpha");
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantAuditEntry>([AuditEntry("event-lifecycle", AuditEventCategory.Access)], null, false),
            metadata: ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(request, null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(lifecycle);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(lifecycle);
    }

    [Theory]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current)]
    [InlineData(true, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Current, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Unknown)]
    [InlineData(true, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Current, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Unknown)]
    [InlineData(true, (QueryResponseProvenance)999, ProjectionLifecycleState.Current, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Unknown)]
    [InlineData(false, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Unknown)]
    public async Task Get_tenant_audit_transports_declared_route_provenance_onto_rows(
        bool emitMetadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance expectedProvenance,
        ProjectionLifecycleState expectedLifecycle)
    {
        // Audit rows carry the declared route provenance so a consumer mutation gate can apply
        // ProjectionLifecyclePolicy against real evidence instead of re-deriving it from freshness.
        // Lifecycle still normalizes to Unknown off a projection-backed route (Story 2.11).
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantAuditEntry>([AuditEntry("event-provenance", AuditEventCategory.Access)], null, false),
            metadata: emitMetadata
                ? ProjectionBackedMetadata(isStale: false, lifecycle: lifecycle, provenance: provenance)
                : null,
            emitDefaultMetadata: false);

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Provenance.ShouldBe(expectedProvenance);
        row.Lifecycle.ShouldBe(expectedLifecycle);
    }

    [Fact]
    public async Task Get_tenant_audit_not_modified_takes_provenance_from_the_current_response()
    {
        // A 304 must never inherit provenance from the retained snapshot: the evidence that matters is
        // what THIS response declared. A non-projection-backed 304 fails closed to Unknown provenance.
        TenantAuditRequest request = new("tenant.alpha", ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [
                TenantAuditRow.FromEntry(AuditEntry("event-retained", AuditEventCategory.Access), ReadModelFreshnessState.Current)
                    with { Lifecycle = ProjectionLifecycleState.Current, Provenance = QueryResponseProvenance.ProjectionBacked }
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueAuditNotModified(clientResponses, "known", provenance: QueryResponseProvenance.HandlerComputed);

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
        row.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
    }

    [Fact]
    public async Task Get_tenant_audit_maps_missing_payload_without_retained_rows_to_safe_error_state()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult<PaginatedResult<TenantAuditEntry>?>(clientResponses, null, metadata: new QueryResponseMetadata(ServedAt: DateTimeOffset.UtcNow));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Reason.ShouldBe(TenantAuditReason.GatewayFailure);
        snapshot.Rows.ShouldBeEmpty();
        ReadCalls(client).ShouldHaveSingleItem();
        ReadCalls(client).ShouldAllBe(call => call.GetArguments()[0] is GetTenantAuditQuery);
    }

    [Fact]
    public async Task Get_tenant_audit_maps_wrong_persisted_projection_shape_without_retained_rows_to_safe_error_state()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantAuditEntry>(null!, null, false),
            metadata: ProjectionBackedMetadata(
                isStale: false,
                lifecycle: ProjectionLifecycleState.Current,
                provenance: QueryResponseProvenance.ProjectionBacked));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Reason.ShouldBe(TenantAuditReason.GatewayFailure);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_audit_preserves_previous_rows_for_missing_payload_when_scope_matches()
    {
        TenantAuditRequest request = new("tenant.alpha", Category: AuditEventCategory.Access);
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [
                TenantAuditRow.FromEntry(AuditEntry("event-5", AuditEventCategory.Access), ReadModelFreshnessState.Current)
                    with { Lifecycle = ProjectionLifecycleState.Current, Provenance = QueryResponseProvenance.ProjectionBacked }
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult<PaginatedResult<TenantAuditEntry>?>(clientResponses, null, metadata: new QueryResponseMetadata(ServedAt: DateTimeOffset.UtcNow));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Degraded);
        snapshot.Reason.ShouldBe(TenantAuditReason.MissingPayload);
        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.EventReference.ShouldBe("event-5");
        row.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        row.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        row.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
    }

    [Theory]
    [InlineData(401, TenantAuditSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, TenantAuditSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(404, TenantAuditSurfaceKind.Unavailable, TenantsRestQueryFailureKind.NotFound)]
    [InlineData(503, TenantAuditSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    [InlineData(500, TenantAuditSurfaceKind.Error, TenantsRestQueryFailureKind.Unavailable)]
    public async Task Get_tenant_audit_maps_gateway_status_to_safe_snapshot_state(int statusCode, TenantAuditSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_audit_maps_only_support_safe_narrative_fields()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [
                new TenantAuditEntry(
                    "event-safe-reference",
                    "TenantConfigurationSet",
                    AuditEventCategory.Administrative,
                    "actor-user",
                    DateTimeOffset.UtcNow,
                    "tenant.alpha",
                    new Dictionary<string, string>
                    {
                        ["userId"] = "target-user",
                        ["key"] = "billing.mode",
                        ["rawPayload"] = "raw payload token secret",
                        ["correlationId"] = "correlation-123",
                        ["etag"] = "etag",
                    }),
            ],
            null,
            false));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Target.ShouldBe("target-user");
        row.ReferenceContext.ShouldContain("userId: target-user");
        row.ReferenceContext.ShouldContain("key: billing.mode");
        row.ReferenceContext.ShouldNotContain("raw payload", Case.Insensitive);
        row.ReferenceContext.ShouldNotContain("token", Case.Insensitive);
        row.ReferenceContext.ShouldNotContain("correlation-123", Case.Insensitive);
        row.ReferenceContext.ShouldNotContain("etag", Case.Insensitive);
    }

    [Theory]
    [InlineData("person@example.test")]
    [InlineData("person＠example.test")]
    [InlineData("+1-202-555-0100")]
    [InlineData("202\u00A0555\u00A00100")]
    [InlineData("user\u2502spoof")]
    public async Task Get_tenant_audit_rejects_pii_and_boundary_user_ids_before_target_fallback(string unsafeUserId)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [new TenantAuditEntry("event-safe-reference", "TenantConfigurationSet", AuditEventCategory.Administrative,
                "actor-user", DateTimeOffset.UtcNow, "tenant.alpha",
                new Dictionary<string, string> { ["userId"] = unsafeUserId, ["key"] = "billing.mode" })],
            null, false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Target.ShouldBe("billing.mode");
        row.Narrative.ShouldNotBeNull().UserId.ShouldBeNull();
        row.ReferenceContext.ShouldNotContain(unsafeUserId);
    }

    [Fact]
    public async Task Get_tenant_audit_scrubs_unsafe_row_fields_before_rendering()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [
                new TenantAuditEntry(
                    "event-safe-reference",
                    "TenantConfigurationSet",
                    AuditEventCategory.Administrative,
                    "actor-user",
                    DateTimeOffset.UtcNow,
                    "tenant.alpha",
                    new Dictionary<string, string>
                    {
                        ["userId"] = "raw payload token secret",
                        ["key"] = "billing.mode",
                    }),
            ],
            null,
            false));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantAuditSnapshot snapshot = await gateway
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Target.ShouldBe("billing.mode");
        row.Scope.ShouldBe("tenant.alpha");
        row.Outcome.ShouldBe("TenantConfigurationSet");
        row.ReferenceContext.ShouldContain("key: billing.mode");
        row.ReferenceContext.ShouldNotContain("raw payload", Case.Insensitive);
        row.ReferenceContext.ShouldNotContain("token", Case.Insensitive);
        row.Target.ShouldNotContain("raw payload", Case.Insensitive);
        row.Scope.ShouldNotContain("cursor", Case.Insensitive);
        row.Outcome.ShouldNotContain("stack trace", Case.Insensitive);
    }

    [Theory]
    [InlineData("non-utc-from")]
    [InlineData("non-utc-to")]
    [InlineData("default-to")]
    [InlineData("reversed")]
    [InlineData("unknown-category")]
    public async Task Get_tenant_audit_rejects_invalid_filter_contracts_without_a_backend_read(string scenario)
    {
        DateTimeOffset start = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        TenantAuditRequest request = scenario switch
        {
            "non-utc-from" => new("tenant.alpha", From: start.ToOffset(TimeSpan.FromHours(2))),
            "non-utc-to" => new("tenant.alpha", To: start.ToOffset(TimeSpan.FromHours(2))),
            "default-to" => new("tenant.alpha", To: default(DateTimeOffset)),
            "reversed" => new("tenant.alpha", From: start.AddMinutes(1), To: start),
            "unknown-category" => new("tenant.alpha", Category: (AuditEventCategory)999),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(request, null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Reason.ShouldBe(TenantAuditReason.InvalidFilters);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("operator-user"));
        ReadCalls(client).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("UserAddedToTenant", AuditEventCategory.Access, true)]
    [InlineData("UserAddedToTenant", AuditEventCategory.Administrative, false)]
    [InlineData("UserRemovedFromTenant", AuditEventCategory.Access, true)]
    [InlineData("UserRemovedFromTenant", AuditEventCategory.Administrative, false)]
    [InlineData("UserRoleChanged", AuditEventCategory.Access, true)]
    [InlineData("UserRoleChanged", AuditEventCategory.Administrative, false)]
    [InlineData("GlobalAdministratorSet", AuditEventCategory.Access, true)]
    [InlineData("GlobalAdministratorSet", AuditEventCategory.Administrative, false)]
    [InlineData("GlobalAdministratorRemoved", AuditEventCategory.Access, true)]
    [InlineData("GlobalAdministratorRemoved", AuditEventCategory.Administrative, false)]
    [InlineData("TenantCreated", AuditEventCategory.Administrative, true)]
    [InlineData("TenantCreated", AuditEventCategory.Access, false)]
    [InlineData("TenantUpdated", AuditEventCategory.Administrative, true)]
    [InlineData("TenantUpdated", AuditEventCategory.Access, false)]
    [InlineData("TenantDisabled", AuditEventCategory.Administrative, true)]
    [InlineData("TenantDisabled", AuditEventCategory.Access, false)]
    [InlineData("TenantEnabled", AuditEventCategory.Administrative, true)]
    [InlineData("TenantEnabled", AuditEventCategory.Access, false)]
    [InlineData("TenantConfigurationSet", AuditEventCategory.Administrative, true)]
    [InlineData("TenantConfigurationSet", AuditEventCategory.Access, false)]
    [InlineData("TenantConfigurationRemoved", AuditEventCategory.Administrative, true)]
    [InlineData("TenantConfigurationRemoved", AuditEventCategory.Access, false)]
    [InlineData("TenantAuditExported", AuditEventCategory.Administrative, false)]
    public async Task Get_tenant_audit_enforces_the_complete_event_category_matrix(
        string eventType,
        AuditEventCategory category,
        bool expectedReady)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-matrix", category, eventType: eventType)],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedReady ? TenantAuditSurfaceKind.Ready : TenantAuditSurfaceKind.Error);
        snapshot.Rows.Count.ShouldBe(expectedReady ? 1 : 0);
    }

    [Fact]
    public async Task Get_tenant_audit_preserves_authoritative_response_order()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [
                AuditEntry("event-z", AuditEventCategory.Access),
                AuditEntry("event-a", AuditEventCategory.Access),
            ],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Rows.Select(static row => row.EventReference).ShouldBe(["event-z", "event-a"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer reference")]
    [InlineData("event-token")]
    [InlineData("event%2Dauthorization")]
    [InlineData("event%252Dsecret")]
    [InlineData("event-password")]
    [InlineData("event-credential")]
    [InlineData("event-connection_string")]
    [InlineData("event-%ZZ")]
    public async Task Get_tenant_audit_rejects_missing_or_unsafe_event_references(string eventReference)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry(eventReference, AuditEventCategory.Access)],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("event-token")]
    [InlineData("event%252Dsecret")]
    public void Tenant_audit_row_mapper_rejects_unapproved_event_references(string eventReference)
    {
        Should.Throw<ArgumentException>(() => TenantAuditRow.FromEntry(
            AuditEntry(eventReference, AuditEventCategory.Access),
            ReadModelFreshnessState.Current));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "opaque-next")]
    [InlineData(true, " ")]
    public async Task Get_tenant_audit_rejects_inconsistent_next_cursor_metadata(bool hasMore, string? cursor)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-safe", AuditEventCategory.Access)], cursor, hasMore));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, null, "opaque-next")]
    [InlineData(false, "opaque-current", "opaque-current")]
    public async Task Get_tenant_audit_rejects_nonadvancing_or_empty_continuation_pages(
        bool emptyPage,
        string? requestCursor,
        string nextCursor)
    {
        IReadOnlyList<TenantAuditEntry> items = emptyPage
            ? []
            : [AuditEntry("event-safe", AuditEventCategory.Access)];
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(items, nextCursor, true));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha", Cursor: requestCursor), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_audit_rejects_duplicate_event_references()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [
                AuditEntry("event-duplicate", AuditEventCategory.Access),
                AuditEntry("event-duplicate", AuditEventCategory.Access),
            ],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("count")]
    [InlineData("default-timestamp")]
    [InlineData("non-utc-timestamp")]
    [InlineData("before-from")]
    [InlineData("after-to")]
    [InlineData("category")]
    [InlineData("tenant")]
    public async Task Get_tenant_audit_rejects_response_scope_and_shape_violations(string scenario)
    {
        DateTimeOffset from = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddHours(1);
        TenantAuditRequest request = new(
            "tenant.alpha",
            From: from,
            To: to,
            Category: AuditEventCategory.Access,
            PageSize: scenario is "count" ? 1 : 25);
        IReadOnlyList<TenantAuditEntry> entries = scenario switch
        {
            "count" =>
            [
                AuditEntry("event-1", AuditEventCategory.Access, from),
                AuditEntry("event-2", AuditEventCategory.Access, from.AddMinutes(1)),
            ],
            "default-timestamp" => [AuditEntry("event-1", AuditEventCategory.Access, default(DateTimeOffset))],
            "non-utc-timestamp" => [AuditEntry("event-1", AuditEventCategory.Access, from.ToOffset(TimeSpan.FromHours(2)))],
            "before-from" => [AuditEntry("event-1", AuditEventCategory.Access, from.AddTicks(-1))],
            "after-to" => [AuditEntry("event-1", AuditEventCategory.Access, to.AddTicks(1))],
            "category" => [AuditEntry("event-1", AuditEventCategory.Administrative, from, "TenantCreated")],
            "tenant" => [AuditEntry("event-1", AuditEventCategory.Access, from, tenantId: "tenant.beta")],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(entries, null, false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(request, null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_tenant_audit_refetches_a_not_modified_response_when_retained_rows_belong_to_another_caller()
    {
        TenantAuditRequest request = new("tenant.alpha", ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            null,
            false,
            "known",
            ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("caller-one"),
        };
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                NotModifiedResponse<PaginatedResult<TenantAuditEntry>>("known"),
                DirectResponse(new PaginatedResult<TenantAuditEntry>(
                    [AuditEntry("event-new", AuditEventCategory.Access)],
                    null,
                    false), "new"));

        TenantAuditSnapshot snapshot = await CreateGateway(client, userId: "caller-two")
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().EventReference.ShouldBe("event-new");
        snapshot.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-two"));
        client.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantsRestQueryClient.GetTenantAuditAsync)).ShouldBe(2);
        _ = client.Received(2).GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_tenant_audit_discards_other_caller_rows_after_an_unexpected_exception()
    {
        TenantAuditRequest request = new("tenant.alpha", ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            null,
            false,
            "known",
            ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("caller-one"),
        };
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>>>(_ => throw new InvalidOperationException("unexpected"));

        TenantAuditSnapshot snapshot = await CreateGateway(client, userId: "caller-two")
            .GetTenantAuditAsync(request, previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-two"));
    }

    [Theory]
    [InlineData(null, TenantAuditSurfaceKind.Unauthorized)]
    [InlineData("caller-two", TenantAuditSurfaceKind.Unavailable)]
    public async Task Get_tenant_audit_discards_an_awaited_response_when_the_caller_changes(
        string? nextUserId,
        TenantAuditSurfaceKind expectedKind)
    {
        string? currentUserId = "caller-one";
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => currentUserId);
        userContext.TenantId.Returns("tenant.context");
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        var pending = new TaskCompletionSource<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        TenantQueryGateway gateway = CreateGateway(client, userContextAccessor: userContext);

        Task<TenantAuditSnapshot> load = gateway.GetTenantAuditAsync(
            new("tenant.alpha"),
            null,
            CancellationToken.None);
        client.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantsRestQueryClient.GetTenantAuditAsync)).ShouldBe(1);
        currentUserId = nextUserId;
        pending.SetResult(DirectResponse(new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-old-caller", AuditEventCategory.Access)],
            null,
            false)));

        TenantAuditSnapshot snapshot = await load;

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.CallerScope.ShouldBe(nextUserId is null ? null : TenantQueryGateway.AuditCallerScope(nextUserId));
    }

    [Fact]
    public async Task Get_tenant_audit_discards_a_refetched_304_response_when_the_caller_changes_during_the_second_read()
    {
        string? currentUserId = "caller-one";
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => currentUserId);
        TenantAuditRequest request = new("tenant.alpha", ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            null, false, "known", ReadModelFreshnessState.Current, request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("another-caller"),
        };
        var pending = new TaskCompletionSource<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(NotModifiedResponse<PaginatedResult<TenantAuditEntry>>("known")),
                pending.Task);
        TenantQueryGateway gateway = CreateGateway(client, userContextAccessor: userContext);

        Task<TenantAuditSnapshot> read = gateway.GetTenantAuditAsync(request, previous, CancellationToken.None);
        client.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ITenantsRestQueryClient.GetTenantAuditAsync))
            .ShouldBe(2);
        currentUserId = "caller-two";
        pending.SetResult(DirectResponse(new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-new", AuditEventCategory.Access)], null, false)));

        TenantAuditSnapshot snapshot = await read;
        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-two"));
    }

    [Fact]
    public async Task Get_tenant_audit_discards_retained_rows_when_the_caller_changes_during_an_unexpected_fault()
    {
        string? currentUserId = "caller-one";
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns(_ => currentUserId);
        TenantAuditRequest request = new("tenant.alpha", ETag: "known");
        TenantAuditSnapshot previous = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-old", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            null, false, "known", ReadModelFreshnessState.Current, request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("caller-one"),
        };
        var pending = new TaskCompletionSource<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        TenantQueryGateway gateway = CreateGateway(client, userContextAccessor: userContext);

        Task<TenantAuditSnapshot> read = gateway.GetTenantAuditAsync(request, previous, CancellationToken.None);
        currentUserId = "caller-two";
        pending.SetException(new InvalidOperationException("transport failed"));

        TenantAuditSnapshot snapshot = await read;
        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("caller-two"));
    }

    [Fact]
    public async Task Get_tenant_audit_maps_typed_roles_without_parsing_display_delimiters()
    {
        IReadOnlyDictionary<string, string> narrative = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["userId"] = "target-user; oldRole: TenantOwner",
            ["oldRole"] = "TenantReader",
            ["newRole"] = "TenantContributor",
            ["role"] = "not-a-role",
        };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [AuditEntry("event-role", AuditEventCategory.Access, eventType: "UserRoleChanged", narrative: narrative)],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        TenantAuditRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.Narrative.ShouldNotBeNull().UserId.ShouldBeNull();
        row.Narrative.OldRole.ShouldBe(TenantRole.TenantReader);
        row.Narrative.NewRole.ShouldBe(TenantRole.TenantContributor);
        row.Narrative.Role.ShouldBeNull();
        row.ReferenceContext.ShouldNotContain("TenantOwner", Case.Insensitive);
    }

    [Theory]
    [InlineData("actor-%2561uthorization")]
    [InlineData("actor-token")]
    [InlineData("actor-secret")]
    [InlineData("actor-password")]
    [InlineData("actor-credential")]
    [InlineData("actor-connection_string")]
    public async Task Get_tenant_audit_blanks_unsafe_actor_identifiers_without_rejecting_safe_evidence(string actorId)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantAuditEntry>(
            [
                new TenantAuditEntry(
                    "event-safe-reference",
                    "UserAddedToTenant",
                    AuditEventCategory.Access,
                    actorId,
                    DateTimeOffset.UtcNow,
                    "tenant.alpha",
                    new Dictionary<string, string> { ["userId"] = "target-user" }),
            ],
            null,
            false));

        TenantAuditSnapshot snapshot = await CreateGateway(client)
            .GetTenantAuditAsync(new("tenant.alpha"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().ActorId.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_tenants_reports_unknown_freshness_when_no_evidence_exists()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantSummary>([], null, false),
            eTag: null,
            metadata: null,
            emitDefaultMetadata: false);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Empty);
    }

    [Fact]
    public async Task List_tenants_does_not_treat_served_at_as_freshness_evidence()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantSummary>([], null, false),
            eTag: null,
            metadata: new QueryResponseMetadata(ServedAt: DateTimeOffset.UtcNow));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public async Task List_tenants_uses_previous_snapshot_for_not_modified_response()
    {
        TenantListSnapshot previous = TenantListSnapshot.Ready(
            [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            isDegraded: false) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"), RequestPageSize = 10 };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueNotModified(clientResponses, "known");

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10, ETag: "known"), previous, CancellationToken.None);

        ReadCalls(client)[0].GetArguments()[1].ShouldBe("known");
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Current);
    }

    [Theory]
    [InlineData(false, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Unknown, null, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, TenantListSurfaceKind.Degraded, TenantListReason.GatewayUnavailable)]
    [InlineData(true, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, TenantListSurfaceKind.Ready, TenantListReason.None)]
    [InlineData(true, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, TenantListSurfaceKind.Ready, TenantListReason.None)]
    [InlineData(true, (QueryResponseProvenance)999, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, TenantListSurfaceKind.Ready, TenantListReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Stale, TenantListSurfaceKind.Stale, TenantListReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, TenantListSurfaceKind.Ready, TenantListReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Degraded, false, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Degraded, TenantListSurfaceKind.Ready, TenantListReason.None)]
    public async Task List_tenants_not_modified_gates_freshness_on_provenance_and_lifecycle(
        bool emitMetadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        bool? isStale,
        ReadModelFreshnessState expectedFreshness,
        ProjectionLifecycleState expectedLifecycle,
        TenantListSurfaceKind expectedKind,
        TenantListReason expectedReason)
    {
        TenantListSnapshot previous = TenantListSnapshot.Ready(
            [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            isDegraded: false) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"), RequestPageSize = 10 };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueNotModified(clientResponses, "known", isStale, lifecycle, provenance, emitMetadata);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10, ETag: "known"), previous, CancellationToken.None);

        // Matching retained evidence with a missing version degrades without an unconditional refetch.
        ReadCalls(client).ShouldHaveSingleItem();
        clientResponses.ShouldBeEmpty();
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(expectedLifecycle);
        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(expectedLifecycle);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, TenantListSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, TenantListSurfaceKind.Stale)]
    [InlineData(ProjectionLifecycleState.Unavailable, true, ReadModelFreshnessState.Unknown, TenantListSurfaceKind.Ready)]
    public async Task List_tenants_projection_lifecycle_precedes_legacy_stale_evidence(
        ProjectionLifecycleState lifecycle,
        bool isStale,
        ReadModelFreshnessState expectedFreshness,
        TenantListSurfaceKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantSummary>([new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)], null, false),
            metadata: ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));

        TenantListSnapshot snapshot = await CreateGateway(client)
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(lifecycle);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(lifecycle);
    }

    // The tenant-list surface was the only query surface with no 200-path provenance theory, while
    // routing through the same ResolveFreshness gate as the four that had one.
    [Theory]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData((QueryResponseProvenance)999)]
    public async Task List_tenants_rejects_non_projection_backed_provenance(QueryResponseProvenance provenance)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantSummary>([], null, false),
            metadata: ProjectionBackedMetadata(isStale: false, provenance: provenance));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Freshness.ShouldBe(
            ReadModelFreshnessState.Unknown,
            "only projection-backed evidence may claim a lifecycle state (AD-15).");
    }

    [Fact]
    public async Task List_tenants_not_modified_preserves_previous_unknown_freshness_without_freshness_header()
    {
        TenantListSnapshot previous = TenantListSnapshot.Ready(
            [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Unknown,
            isDegraded: false) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"), RequestPageSize = 10 };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);

        // Deliberately the metadata-deficient shape: no freshness header and no lifecycle. The helper's
        // default is now the deliverable Current shape, so this case states its own inputs.
        EnqueueNotModified(clientResponses, "known", isStale: null, lifecycle: ProjectionLifecycleState.Unknown);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10, ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public async Task List_tenants_not_modified_uses_stale_header_from_conditional_response()
    {
        TenantListSnapshot previous = TenantListSnapshot.Ready(
            [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))],
            nextCursor: "next",
            hasMore: true,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            isDegraded: false) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"), RequestPageSize = 10 };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueNotModified(clientResponses, "known", isStale: true, lifecycle: ProjectionLifecycleState.Stale);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10, ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.NextCursor.ShouldBe("next");
        snapshot.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task List_tenants_stale_empty_response_surfaces_stale_state()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<TenantSummary>([], null, false),
            metadata: ProjectionBackedMetadata(isStale: true));

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
    }

    [Fact]
    public async Task Detail_enrichment_failure_keeps_unknown_counts_and_degraded_state()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            null,
            false));
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, 403);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Degraded);
        snapshot.Rows.ShouldHaveSingleItem().MemberCount.IsKnown.ShouldBeFalse();
        snapshot.Rows[0].OwnerCount.IsKnown.ShouldBeFalse();
    }

    [Theory]
    [InlineData(401, TenantListSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, TenantListSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(400, TenantListSurfaceKind.Error, TenantsRestQueryFailureKind.InvalidRequest)]
    [InlineData(503, TenantListSurfaceKind.Error, TenantsRestQueryFailureKind.Unavailable)]
    public async Task List_tenants_maps_gateway_status_to_safe_state(int statusCode, TenantListSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);

        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway
            .ListTenantsAsync(new TenantListRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
    }

    [Fact]
    public async Task Get_my_tenants_submits_self_user_query_with_cursor_payload()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>(
            [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantOwner)],
            "opaque-next",
            true));
        TenantQueryGateway gateway = CreateGateway(client, "user.self");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(Cursor: "signed-cursor", PageSize: 12), null, CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client)[0];
        ((GetUserTenantsQuery)query.GetArguments()[0]!).UserId.ShouldBe("user.self");
        GetUserTenantsQuery payload = (GetUserTenantsQuery)query.GetArguments()[0]!;
        payload.Cursor.ShouldBe("signed-cursor");
        payload.PageSize.ShouldBe(12);
        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        snapshot.NextCursor.ShouldBe("opaque-next");
        snapshot.Rows.ShouldHaveSingleItem().Role.ShouldBe(TenantRole.TenantOwner);
    }

    [Fact]
    public async Task Get_my_tenants_keeps_signed_in_user_as_target_even_when_request_has_target()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client, "user.self");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "user.other"), null, CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client)[0];
        ((GetUserTenantsQuery)query.GetArguments()[0]!).UserId.ShouldBe("user.self");
        snapshot.TargetUserId.ShouldBe("user.self");
    }

    [Fact]
    public async Task Get_user_tenants_submits_authenticated_requester_and_explicit_target_user_query()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>(
            [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader)],
            "opaque-next",
            true));
        TenantQueryGateway gateway = CreateGateway(client, "operator-user");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(
                new UserTenantMembershipRequest(
                    TargetUserId: "target.user@example",
                    Cursor: "signed-target-cursor",
                    PageSize: 12,
                    ETag: "known"),
                null,
                CancellationToken.None);

        NSubstitute.Core.ICall query = ReadCalls(client)[0];
        ((GetUserTenantsQuery)query.GetArguments()[0]!).UserId.ShouldBe("target.user@example");
        query.GetArguments()[1].ShouldBeNull();
        GetUserTenantsQuery payload = (GetUserTenantsQuery)query.GetArguments()[0]!;
        payload.Cursor.ShouldBe("signed-target-cursor");
        payload.PageSize.ShouldBe(12);
        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        snapshot.TargetUserId.ShouldBe("target.user@example");
        snapshot.NextCursor.ShouldBe("opaque-next");
    }

    [Fact]
    public async Task Get_user_tenants_reuses_not_modified_snapshot_only_for_same_target_user()
    {
        UserTenantMembershipSnapshot previous = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader, ReadModelFreshnessState.Current)],
            nextCursor: "next",
            hasMore: true,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            targetUserId: "target.one") with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueUserTenantsNotModified(clientResponses, "known");
        EnqueueUserTenantsNotModified(clientResponses, "known");
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>(
            [new UserTenantMembership("tenant.beta", "Beta", TenantStatus.Active, TenantRole.TenantReader)],
            Cursor: null,
            HasMore: false));
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot sameTarget = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.one", ETag: "known"), previous, CancellationToken.None);
        UserTenantMembershipSnapshot differentTarget = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.two", ETag: "known"), previous, CancellationToken.None);

        sameTarget.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        sameTarget.TargetUserId.ShouldBe("target.one");
        differentTarget.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        differentTarget.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.beta");
        differentTarget.TargetUserId.ShouldBe("target.two");
        ReadCalls(client)[2].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_user_tenants_applies_stale_freshness_from_not_modified_response()
    {
        UserTenantMembershipSnapshot previous = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader, ReadModelFreshnessState.Current)],
            nextCursor: "next",
            hasMore: true,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            targetUserId: "target.one") with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueUserTenantsNotModified(clientResponses, "known", isStale: true, lifecycle: ProjectionLifecycleState.Stale);
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.one", ETag: "known"), previous, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.ProjectionStale);
    }

    [Fact]
    public async Task Get_user_tenants_rejects_missing_target_without_backend_call()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: ""), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Invalid);
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.MissingTargetUser);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_user_tenants_maps_authorization_scoped_empty_without_disclosing_hidden_memberships()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client, "operator-user");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.user", PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Empty);
        snapshot.IsAuthorizationScopedEmpty.ShouldBeTrue();
        snapshot.TargetUserId.ShouldBe("target.user");
        snapshot.Rows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false, UserTenantMembershipSurfaceKind.Stale, ReadModelFreshnessState.Stale, UserTenantMembershipReason.ProjectionStale)]
    [InlineData(false, true, UserTenantMembershipSurfaceKind.Degraded, ReadModelFreshnessState.Unknown, UserTenantMembershipReason.ProjectionDegraded)]
    public async Task Get_user_tenants_maps_target_lookup_stale_and_degraded_metadata_to_distinct_states(
        bool isStale,
        bool isDegraded,
        UserTenantMembershipSurfaceKind expectedKind,
        ReadModelFreshnessState expectedFreshness,
        UserTenantMembershipReason expectedReason)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Disabled, TenantRole.TenantReader)],
                "next",
                true),
            metadata: ProjectionBackedMetadata(isStale: isStale, isDegraded: isDegraded));
        TenantQueryGateway gateway = CreateGateway(client, "operator-user");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.user", PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.TargetUserId.ShouldBe("target.user");
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.NextCursor.ShouldBe("next");
        snapshot.HasMore.ShouldBeTrue();
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(expectedFreshness);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    [InlineData((QueryResponseProvenance)999)]
    public async Task Get_user_tenants_non_projection_stale_evidence_remains_unknown(
        QueryResponseProvenance provenance)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Disabled, TenantRole.TenantReader)],
                "next",
                true),
            metadata: new QueryResponseMetadata(IsStale: true)
            {
                Provenance = provenance,
            });
        TenantQueryGateway gateway = CreateGateway(client, "operator-user");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.user", PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.None);
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Theory]
    [InlineData(401, UserTenantMembershipSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, UserTenantMembershipSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(400, UserTenantMembershipSurfaceKind.Invalid, TenantsRestQueryFailureKind.InvalidRequest)]
    [InlineData(503, UserTenantMembershipSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    // Review loop 9, decision D3: 500 now maps to Unavailable, not Degraded. The old expectation was a
    // harness artifact -- it reached MapUserTenantException directly with a raw 500, but through the
    // production path ToEventStoreResult forced every 5xx to 503, so this surface never actually
    // produced Degraded for a server error. The mapper now agrees with what production always did, and
    // a first-load server error stays fail-closed instead of rendering as retained degradation.
    [InlineData(500, UserTenantMembershipSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    public async Task Get_user_tenants_maps_target_lookup_gateway_failures_to_sanitized_states(
        int statusCode,
        UserTenantMembershipSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);
        TenantQueryGateway gateway = CreateGateway(client, "operator-user");

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetUserTenantsAsync(new UserTenantMembershipRequest(TargetUserId: "target.user"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
        snapshot.TargetUserId.ShouldBe("target.user");
    }

    [Fact]
    public async Task Get_my_tenants_requires_authenticated_user_context()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, userId: null);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Unauthorized);
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.MissingAuthenticatedUser);
        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_my_tenants_maps_authorized_empty_without_error()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Empty);
        snapshot.IsAuthorizationScopedEmpty.ShouldBeTrue();
        snapshot.Rows.ShouldBeEmpty();
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.None);
    }

    [Fact]
    public async Task Get_my_tenants_uses_previous_snapshot_for_not_modified_response()
    {
        UserTenantMembershipSnapshot previous = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader, ReadModelFreshnessState.Current)],
            nextCursor: "next",
            hasMore: true,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            targetUserId: "operator-user") with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueUserTenantsNotModified(clientResponses, "known");
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(ETag: "known"), previous, CancellationToken.None);

        ReadCalls(client)[0].GetArguments()[1].ShouldBe("known");
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        snapshot.NextCursor.ShouldBe("next");
        snapshot.HasMore.ShouldBeTrue();
        snapshot.ETag.ShouldBe("known");
    }

    [Theory]
    [InlineData(false, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Unknown, null, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, UserTenantMembershipSurfaceKind.Degraded, UserTenantMembershipReason.GatewayFailure)]
    [InlineData(true, QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, UserTenantMembershipSurfaceKind.Ready, UserTenantMembershipReason.None)]
    [InlineData(true, QueryResponseProvenance.Unknown, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, UserTenantMembershipSurfaceKind.Ready, UserTenantMembershipReason.None)]
    [InlineData(true, (QueryResponseProvenance)999, ProjectionLifecycleState.Stale, true, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Unknown, UserTenantMembershipSurfaceKind.Ready, UserTenantMembershipReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, ProjectionLifecycleState.Stale, UserTenantMembershipSurfaceKind.Stale, UserTenantMembershipReason.ProjectionStale)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, UserTenantMembershipSurfaceKind.Ready, UserTenantMembershipReason.None)]
    [InlineData(true, QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Degraded, false, ReadModelFreshnessState.Unknown, ProjectionLifecycleState.Degraded, UserTenantMembershipSurfaceKind.Ready, UserTenantMembershipReason.None)]
    public async Task Get_my_tenants_not_modified_gates_freshness_on_provenance_and_lifecycle(
        bool emitMetadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        bool? isStale,
        ReadModelFreshnessState expectedFreshness,
        ProjectionLifecycleState expectedLifecycle,
        UserTenantMembershipSurfaceKind expectedKind,
        UserTenantMembershipReason expectedReason)
    {
        UserTenantMembershipSnapshot previous = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader, ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current,
            targetUserId: "operator-user") with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueUserTenantsNotModified(clientResponses, "known", isStale, lifecycle, provenance, emitMetadata);
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(ETag: "known"), previous, CancellationToken.None);

        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(expectedLifecycle);
        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(expectedLifecycle);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Current, true, ReadModelFreshnessState.Current, UserTenantMembershipSurfaceKind.Ready)]
    [InlineData(ProjectionLifecycleState.Stale, false, ReadModelFreshnessState.Stale, UserTenantMembershipSurfaceKind.Stale)]
    [InlineData(ProjectionLifecycleState.LocalOnly, true, ReadModelFreshnessState.Unknown, UserTenantMembershipSurfaceKind.Ready)]
    public async Task Get_my_tenants_projection_lifecycle_precedes_legacy_stale_evidence(
        ProjectionLifecycleState lifecycle,
        bool isStale,
        ReadModelFreshnessState expectedFreshness,
        UserTenantMembershipSurfaceKind expectedKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader)],
                null,
                false),
            metadata: ProjectionBackedMetadata(isStale: isStale, lifecycle: lifecycle));

        UserTenantMembershipSnapshot snapshot = await CreateGateway(client)
            .GetMyTenantsAsync(new UserTenantMembershipRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Lifecycle.ShouldBe(lifecycle);
        snapshot.Rows.ShouldHaveSingleItem().Lifecycle.ShouldBe(lifecycle);
    }

    [Fact]
    public async Task Get_my_tenants_without_previous_snapshot_refetches_not_modified_unconditionally()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueUserTenantsNotModified(clientResponses, "known");
        EnqueueQueryResult(clientResponses, new PaginatedResult<UserTenantMembership>(
            [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader)],
            Cursor: null,
            HasMore: false));
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(ETag: "known"), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        ReadCalls(client).Count.ShouldBe(2);
        ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
    }

    [Theory]
    [InlineData(true, false, UserTenantMembershipSurfaceKind.Stale, ReadModelFreshnessState.Stale, UserTenantMembershipReason.ProjectionStale)]
    [InlineData(false, true, UserTenantMembershipSurfaceKind.Degraded, ReadModelFreshnessState.Unknown, UserTenantMembershipReason.ProjectionDegraded)]
    public async Task Get_my_tenants_maps_stale_and_degraded_metadata_to_distinct_states(
        bool isStale,
        bool isDegraded,
        UserTenantMembershipSurfaceKind expectedKind,
        ReadModelFreshnessState expectedFreshness,
        UserTenantMembershipReason expectedReason)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            new PaginatedResult<UserTenantMembership>(
                [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Disabled, TenantRole.TenantReader)],
                "next",
                true),
            metadata: ProjectionBackedMetadata(isStale: isStale, isDegraded: isDegraded));
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(PageSize: 10), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expectedKind);
        snapshot.Freshness.ShouldBe(expectedFreshness);
        snapshot.Reason.ShouldBe(expectedReason);
        snapshot.NextCursor.ShouldBe("next");
        snapshot.HasMore.ShouldBeTrue();
        snapshot.Rows.ShouldHaveSingleItem().Freshness.ShouldBe(expectedFreshness);
    }

    [Theory]
    [InlineData(401, UserTenantMembershipSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Unauthorized)]
    [InlineData(403, UserTenantMembershipSurfaceKind.Unauthorized, TenantsRestQueryFailureKind.Forbidden)]
    [InlineData(400, UserTenantMembershipSurfaceKind.Invalid, TenantsRestQueryFailureKind.InvalidRequest)]
    [InlineData(503, UserTenantMembershipSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    // Review loop 9, decision D3: 500 now maps to Unavailable, not Degraded. The old expectation was a
    // harness artifact -- it reached MapUserTenantException directly with a raw 500, but through the
    // production path ToEventStoreResult forced every 5xx to 503, so this surface never actually
    // produced Degraded for a server error. The mapper now agrees with what production always did, and
    // a first-load server error stays fail-closed instead of rendering as retained degradation.
    [InlineData(500, UserTenantMembershipSurfaceKind.Unavailable, TenantsRestQueryFailureKind.Unavailable)]
    public async Task Get_my_tenants_maps_gateway_failures_to_sanitized_states(int statusCode, UserTenantMembershipSurfaceKind expected, TenantsRestQueryFailureKind failureKind)
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, failureKind, statusCode);
        TenantQueryGateway gateway = CreateGateway(client);

        UserTenantMembershipSnapshot snapshot = await gateway
            .GetMyTenantsAsync(new UserTenantMembershipRequest(), null, CancellationToken.None);

        snapshot.Kind.ShouldBe(expected);
    }

    [Fact]
    public async Task List_empty_search_uses_ordinary_cursor_path_without_notice()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([new TenantSummary("alpha", "Alpha", TenantStatus.Active)], null, false));
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        StubMemoriesClient memories = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "   "), previous: null, CancellationToken.None); // whitespace term

        memories.SearchRequests.ShouldBeEmpty();
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Ready);
        snapshot.Notice.ShouldBe(TenantListReason.None);
    }

    [Fact]
    public async Task List_non_empty_search_uses_ordinary_cursor_list_without_memories_or_plaintext_cursor()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            "opaque-protected-next-cursor",
            true));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "term", PageSize: 50),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Count.ShouldBe(2);
        ((ListTenantsQuery)ReadCalls(client)[0].GetArguments()[0]!).PageSize.ShouldBe(50);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Ready);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        snapshot.NextCursor.ShouldBe("opaque-protected-next-cursor");
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
    }

    [Fact]
    public async Task List_search_uses_exact_memories_request_and_only_authoritative_hydrated_fields()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            totalCount: 8,
            Hit("not-a-tenant"),
            Hit("tenant:alpha"),
            Hit("tenant:alpha"),
            Hit("tenant:hidden"),
            Hit("tenant:gamma")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha") with { Name = "Authoritative Alpha" });
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, (int)HttpStatusCode.Forbidden);
        EnqueueQueryResult(clientResponses, Detail("gamma") with { Name = "Authoritative Gamma" });
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(
                Search: "needle",
                Status: TenantStatus.Active,
                SortColumn: TenantListSortColumns.Name,
                SortDescending: true,
                PageSize: 5),
            previous: null,
            CancellationToken.None);

        SearchRequest request = memories.SearchRequests.ShouldHaveSingleItem();
        request.TenantId.ShouldBe("tenants-index");
        request.Axis.ShouldBe("syntactic");
        request.Query.ShouldBe("needle");
        request.Offset.ShouldBe(0);
        request.MaxResults.ShouldBe(5);
        request.Explain.ShouldBeFalse();
        request.TokenBudget.ShouldBeNull();
        request.AttributeFilters.ShouldNotBeNull()["status"].ShouldBe(nameof(TenantStatus.Active));
        ReadCalls(client).Select(query => ((GetTenantQuery)query.GetArguments()[0]!).TenantId).ShouldBe(["alpha", "hidden", "gamma"]);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Rows.Select(row => row.TenantId).ShouldBe(["gamma", "alpha"]);
        snapshot.Rows.ShouldAllBe(row => row.PendingState == TenantPendingState.Unknown);
        snapshot.Rows.ShouldAllBe(row => row.Name.StartsWith("Authoritative", StringComparison.Ordinal));
        snapshot.HasMore.ShouldBeTrue();
        snapshot.NextCursor.ShouldNotBeNull();
        snapshot.NextCursor.ShouldNotBe("5");
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            nameof(TenantStatus.Active),
            TenantListSortColumns.Name,
            descending: true,
            pageSize: 5);
        codec.TryDecode(snapshot.NextCursor, scope, out int nextOffset).ShouldBeTrue();
        nextOffset.ShouldBe(5);
    }

    [Fact]
    public async Task List_search_operational_partial_keeps_verified_rows_and_reports_generic_degradation()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 2, Hit("tenant:alpha"), Hit("tenant:beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, (int)HttpStatusCode.ServiceUnavailable);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 2),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Degraded);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Reason.ShouldBe(TenantListReason.SearchPartiallyAvailable);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("alpha");
    }

    [Fact]
    public async Task List_search_total_operational_hydration_loss_falls_back_to_ordinary_list()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, (int)HttpStatusCode.ServiceUnavailable);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("fallback", "Fallback", TenantStatus.Active)],
            "ordinary-next",
            true));
        EnqueueQueryResult(clientResponses, Detail("fallback"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", Cursor: "ordinary-current"),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("fallback");
        ReadCalls(client).Count.ShouldBe(3);
        ((ListTenantsQuery)ReadCalls(client)[1].GetArguments()[0]!).Cursor.ShouldBe("ordinary-current");
    }

    [Fact]
    public async Task List_search_recovers_once_at_page_zero_when_index_shrinks()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(scope, 50);
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 1));
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.Select(request => request.Offset).ShouldBe([50, 0]);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.SearchRefreshed);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("alpha");
    }

    [Fact]
    public async Task List_search_rejects_contradictory_response_and_uses_sanitized_fallback()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:unsafe")) with
        {
            Degraded = true,
            OmittedCount = 1,
            OmittedReason = Hexalith.Memories.Contracts.V1.OmittedReason.Combined,
            UnavailableAxes = ["semantic"],
        });
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle"),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).ShouldHaveSingleItem();
        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
    }

    [Fact]
    public async Task List_search_propagates_caller_cancellation_without_fallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        StubMemoriesClient memories = new();
        memories.Enqueue(new OperationCanceledException(cancellation.Token));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        await Should.ThrowAsync<OperationCanceledException>(() => gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle"),
            previous: null,
            cancellation.Token));

        ReadCalls(client).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_search_keeps_a_short_non_final_page_authoritative_and_advances_by_the_requested_window()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        StubMemoriesClient memories = new();

        // The index legitimately omits its own unusable entries while still reporting the untrimmed total:
        // 18 hits for a 20-wide window whose total says 25 results exist.
        MemoriesScoredResult[] shortPage = Enumerable.Range(0, 18)
            .Select(static index => Hit($"tenant:tenant-{index:D2}"))
            .ToArray();
        memories.Enqueue(SearchResult("needle", totalCount: 25, shortPage));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        foreach (int index in Enumerable.Range(0, 18))
        {
            EnqueueQueryResult(clientResponses, Detail($"tenant-{index:D2}"));
        }

        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec, logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 20),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(0);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.None);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Ready);
        snapshot.Rows.Count.ShouldBe(18);
        snapshot.HasMore.ShouldBeTrue();

        // Advancing by the requested window (not the returned hit count) is the only rule under which the
        // following page neither repeats index entries 18-19 nor skips index entries 20-24.
        codec.TryDecode(snapshot.NextCursor, scope, out int nextOffset).ShouldBeTrue();
        nextOffset.ShouldBe(20);
        logger.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_search_short_page_sequence_neither_repeats_nor_skips_a_candidate()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 5, Hit("tenant:a0"), Hit("tenant:a2")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("a0"));
        EnqueueQueryResult(clientResponses, Detail("a2"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot first = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 3),
            previous: null,
            CancellationToken.None);

        first.IsAuthoritativeSearch.ShouldBeTrue();
        first.HasMore.ShouldBeTrue();

        StubMemoriesClient secondMemories = new();
        secondMemories.Enqueue(SearchResult("needle", totalCount: 5, Hit("tenant:a3"), Hit("tenant:a4")));
        ITenantsRestQueryClient secondClient = CreateReadClient(out Queue<object> secondClientResponses);
        EnqueueQueryResult(secondClientResponses, Detail("a3"));
        EnqueueQueryResult(secondClientResponses, Detail("a4"));
        TenantQueryGateway secondGateway = CreateGateway(
            secondClient,
            memoriesClient: secondMemories,
            searchCursorCodec: codec);

        TenantListSnapshot second = await secondGateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 3, SearchCursor: first.NextCursor),
            previous: null,
            CancellationToken.None);

        // Page two starts exactly where page one's window ended: no repeated and no skipped raw offset.
        secondMemories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(3);
        second.IsAuthoritativeSearch.ShouldBeTrue();
        second.Notice.ShouldBe(TenantListReason.None);
        second.PagingRecovered.ShouldBeFalse();
        second.Rows.Select(static row => row.TenantId).ShouldBe(["a3", "a4"]);
        second.HasMore.ShouldBeFalse();
        second.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task List_search_recovers_once_when_a_positive_offset_equals_the_shrunken_total()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(scope, 20);
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 20));
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.Select(static request => request.Offset).ShouldBe([20, 0]);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.SearchRefreshed);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("alpha");
    }

    [Fact]
    public async Task List_search_accepts_a_valid_short_final_page_at_a_positive_offset()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(scope, 20);
        MemoriesScoredResult[] hits = Enumerable.Range(20, 5)
            .Select(static index => Hit($"tenant:tenant-{index}"))
            .ToArray();
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 25, hits));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        foreach (int index in Enumerable.Range(20, 5))
        {
            EnqueueQueryResult(clientResponses, Detail($"tenant-{index}"));
        }

        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(20);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.PagingRecovered.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.None);
        snapshot.Rows.Count.ShouldBe(5);
        snapshot.HasMore.ShouldBeFalse();
        snapshot.NextCursor.ShouldBeNull();
        ReadCalls(client).ShouldAllBe(static query => query.GetArguments()[0] is GetTenantQuery);
    }

    [Fact]
    public async Task List_search_keeps_a_null_member_tenant_visible_with_unknown_counts_like_the_ordinary_list()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 2, Hit("tenant:alpha"), Hit("tenant:beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        EnqueueQueryResult(clientResponses, Detail("beta") with { Members = [null!] });
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 2),
            previous: null,
            CancellationToken.None);

        // A tenant that stays visible in the ordinary list must not vanish when the operator searches for it.
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Rows.Select(static row => row.TenantId).ShouldBe(["alpha", "beta"]);
        snapshot.Rows.Single(static row => row.TenantId == "beta").MemberCount.IsKnown.ShouldBeFalse();
        snapshot.Rows.Single(static row => row.TenantId == "beta").OwnerCount.IsKnown.ShouldBeFalse();
        snapshot.Rows.Single(static row => row.TenantId == "alpha").MemberCount.IsKnown.ShouldBeTrue();

        // ... and the surface raises exactly the signal the ordinary list raises for the same payload,
        // instead of presenting a clean Ready surface that hides the missing counts.
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Degraded);
        snapshot.IsDegraded.ShouldBeTrue();
        snapshot.Reason.ShouldBe(TenantListReason.RowEnrichmentUnavailable);

        // Enrichment degradation is not an outage: it must never send the surface to the ordinary list.
        snapshot.Notice.ShouldBe(TenantListReason.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Malformed_member_collection_degrades_identically_on_both_hydration_paths(bool wholeCollectionNull)
    {
        // Driven with BOTH shapes. Every case previously used [null!] -- a non-null list holding a null
        // element -- so the wholly-null branch of HasUsableMembers was never exercised. Reverting that
        // guard to !detail.Members.Any(m => m is null) throws a NullReferenceException, which
        // IsSurfacingDefect deliberately re-raises and which would take down the whole list page, and no
        // test failed.
        IReadOnlyList<TenantMember> malformedMembers = wholeCollectionNull ? null! : [null!];

        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("alpha", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient searchClient = CreateReadClient(out Queue<object> searchClientResponses);
        EnqueueQueryResult(searchClientResponses, Detail("alpha") with { Members = malformedMembers });
        TenantListSnapshot searchSnapshot = await CreateGateway(searchClient, memoriesClient: memories)
            .ListTenantsAsync(new TenantListRequest(Search: "alpha", PageSize: 1), previous: null, CancellationToken.None);

        ITenantsRestQueryClient listClient = CreateReadClient(out Queue<object> listClientResponses);
        EnqueueQueryResult(listClientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("alpha", "Alpha", TenantStatus.Active)],
            null,
            false));
        EnqueueQueryResult(listClientResponses, Detail("alpha") with { Members = malformedMembers });
        TenantListSnapshot listSnapshot = await CreateGateway(listClient)
            .ListTenantsAsync(new TenantListRequest(PageSize: 1), previous: null, CancellationToken.None);

        // The same payload may not produce a degraded banner on one surface and a clean surface on the other.
        searchSnapshot.Kind.ShouldBe(listSnapshot.Kind);
        searchSnapshot.IsDegraded.ShouldBe(listSnapshot.IsDegraded);
        searchSnapshot.Reason.ShouldBe(listSnapshot.Reason);
        searchSnapshot.Freshness.ShouldBe(listSnapshot.Freshness);
        searchSnapshot.Rows.Single().MemberCount.IsKnown.ShouldBe(listSnapshot.Rows.Single().MemberCount.IsKnown);
        listSnapshot.Reason.ShouldBe(TenantListReason.RowEnrichmentUnavailable);
    }

    [Fact]
    public async Task List_search_stays_authoritative_when_every_candidate_carries_a_null_member_element()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 2, Hit("tenant:alpha"), Hit("tenant:beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha") with { Members = [null!] });
        EnqueueQueryResult(clientResponses, Detail("beta") with { Members = [null!] });
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 2),
            previous: null,
            CancellationToken.None);

        // A page whose candidates are all malformed must not collapse the whole surface to the ordinary list.
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.None);
        snapshot.Rows.Select(static row => row.TenantId).ShouldBe(["alpha", "beta"]);
        snapshot.Rows.ShouldAllBe(static row => !row.MemberCount.IsKnown && !row.OwnerCount.IsKnown);
        ReadCalls(client).ShouldAllBe(static query => query.GetArguments()[0] is GetTenantQuery);
        logger.Messages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("out-of-memory")]
    [InlineData("null-reference")]
    [InlineData("object-disposed")]
    [InlineData("argument-null")]
    public async Task List_search_surfaces_every_programming_defect_from_the_cursor_codec_instead_of_mislabelling_it(
        string exceptionKind)
    {
        // ObjectDisposedException derives from InvalidOperationException and ArgumentNullException from
        // ArgumentException -- both contained base types -- so this proves the containment predicate excludes
        // the surfacing set before any base-type match. A torn-down provider must look like a torn-down
        // provider, not like a tampered cursor.
        StubMemoriesClient memories = new();
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(
            client,
            memoriesClient: memories,
            searchCursorCodec: new ThrowingSearchCursorCodec(decodeFailure: () => SurfacingCodecDefect(exceptionKind)));

        Exception thrown = await Should.ThrowAsync<Exception>(() => gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: "any-cursor"),
            previous: null,
            CancellationToken.None));

        thrown.GetType().ShouldBe(SurfacingCodecDefect(exceptionKind).GetType());
        memories.SearchRequests.ShouldBeEmpty();
        ReadCalls(client).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("out-of-memory")]
    [InlineData("null-reference")]
    [InlineData("object-disposed")]
    [InlineData("argument-null")]
    public async Task List_search_surfaces_every_programming_defect_raised_while_protecting_the_next_cursor(
        string exceptionKind)
    {
        // The same two-set rule applies on the encode path, where the surrounding availability predicate
        // also lists InvalidOperationException as a contained base type.
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 2, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            memoriesClient: memories,
            searchCursorCodec: new ThrowingSearchCursorCodec(encodeFailure: () => SurfacingCodecDefect(exceptionKind)));

        Exception thrown = await Should.ThrowAsync<Exception>(() => gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 1),
            previous: null,
            CancellationToken.None));

        thrown.GetType().ShouldBe(SurfacingCodecDefect(exceptionKind).GetType());
        ReadCalls(client).ShouldAllBe(static query => query.GetArguments()[0] is GetTenantQuery);
    }

    [Fact]
    public async Task List_ordinary_path_degrades_safely_for_the_same_null_member_detail_shape()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            null,
            false));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha") with { Members = [null!] });
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Degraded);
        snapshot.Reason.ShouldBe(TenantListReason.RowEnrichmentUnavailable);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
    }

    [Fact]
    public async Task List_search_preserves_both_search_outage_and_fallback_cursor_recovery_notices()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.InvalidCursor, (int)HttpStatusCode.BadRequest);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            null,
            false));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", Cursor: "ordinary-expired-cursor"),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
        snapshot.PagingNotice.ShouldBe(TenantListReason.ListRefreshed);
        snapshot.FallbackPagingRecovered.ShouldBeTrue();
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
    }

    [Fact]
    public async Task List_search_clears_protected_history_when_cursor_invalidation_and_a_memories_outage_share_one_load()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string foreignScope = TenantSearchCursorScopes.Create(
            "another.user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(foreignScope, 40);
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>(
            [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
            null,
            false));
        EnqueueQueryResult(clientResponses, Detail("tenant.alpha"));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);

        // The protected search history must still be cleared even though the same load also lost Memories.
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.PagingNotice.ShouldBe(TenantListReason.SearchRefreshed);
    }

    [Fact]
    public async Task List_search_rejects_a_cross_user_cursor_and_requests_raw_page_zero_exactly_once()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string firstUserScope = TenantSearchCursorScopes.Create(
            "user.one",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(firstUserScope, 20);
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            userId: "user.two",
            memoriesClient: memories,
            searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(0);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.SearchRefreshed);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("alpha");
    }

    [Theory]
    [InlineData("null-response")]
    [InlineData("null-results")]
    [InlineData("negative-total")]
    [InlineData("oversized-total")]
    [InlineData("too-many-results")]
    [InlineData("null-query")]
    [InlineData("mismatched-query")]
    [InlineData("degraded")]
    [InlineData("omitted-count")]
    [InlineData("omitted-reason")]
    [InlineData("unavailable-axis")]
    [InlineData("null-axes")]
    [InlineData("empty-axes")]
    [InlineData("multiple-axes")]
    [InlineData("wrong-axis")]
    [InlineData("index-flag-contradiction")]
    [InlineData("page-exceeds-total")]
    [InlineData("null-hit")]
    [InlineData("wrong-hit-axis")]
    public async Task List_search_rejects_each_unsafe_response_invariant_without_hydrating_index_candidates(string fault)
    {
        MemoriesSearchResult valid = SearchResult(
            "needle",
            totalCount: 2,
            Hit("tenant:unsafe-one"),
            Hit("tenant:unsafe-two"));
        int pageSize = 2;
        MemoriesSearchResult? response = fault switch
        {
            "null-response" => null,
            "null-results" => valid with { Results = null! },
            "negative-total" => valid with { TotalCount = -1 },
            "oversized-total" => valid with { TotalCount = (long)int.MaxValue + 1 },
            "too-many-results" => valid,
            "null-query" => valid with { Query = null! },
            "mismatched-query" => valid with { Query = "other" },
            "degraded" => valid with { Degraded = true },
            "omitted-count" => valid with { OmittedCount = 1 },
            "omitted-reason" => valid with { OmittedReason = Hexalith.Memories.Contracts.V1.OmittedReason.TokenBudget },
            "unavailable-axis" => valid with { UnavailableAxes = ["semantic"] },
            "null-axes" => valid with { AxesUsed = null },
            "empty-axes" => valid with { AxesUsed = [] },
            "multiple-axes" => valid with { AxesUsed = ["syntactic", "semantic"] },
            "wrong-axis" => valid with { AxesUsed = ["semantic"] },
            "index-flag-contradiction" => valid with { HasIndexedMemoryUnits = false },
            "page-exceeds-total" => valid with { TotalCount = 1 },
            "null-hit" => valid with { Results = [Hit("tenant:unsafe-one"), null!] },
            "wrong-hit-axis" => valid with
            {
                Results = [Hit("tenant:unsafe-one"), Hit("tenant:unsafe-two") with { Axis = "semantic" }],
            },
            _ => throw new InvalidOperationException($"Unknown response fault {fault}."),
        };
        if (fault == "too-many-results")
        {
            pageSize = 1;
        }

        StubMemoriesClient memories = new();
        memories.EnqueueNullable(response);
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: pageSize),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
        ReadCalls(client).ShouldHaveSingleItem();
        logger.Messages.ShouldHaveSingleItem().ShouldContain(TenantQueryGateway.SearchResponseInvalidReasonCode);
    }

    [Theory]
    [InlineData(TenantListSortColumns.TenantId, false, "alpha,beta,zeta")]
    [InlineData(TenantListSortColumns.TenantId, true, "zeta,beta,alpha")]
    [InlineData(TenantListSortColumns.Name, false, "beta,zeta,alpha")]
    [InlineData(TenantListSortColumns.Name, true, "alpha,beta,zeta")]
    [InlineData(TenantListSortColumns.Status, false, "alpha,beta,zeta")]
    [InlineData(TenantListSortColumns.Status, true, "zeta,alpha,beta")]
    public async Task List_search_sorts_every_supported_column_with_ordinal_tenant_id_ties(
        string sortColumn,
        bool descending,
        string expectedOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedOrder);

        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            3,
            Hit("tenant:zeta"),
            Hit("tenant:alpha"),
            Hit("tenant:beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("zeta") with { Name = "Same", Status = TenantStatus.Disabled });
        EnqueueQueryResult(clientResponses, Detail("alpha") with { Name = "Zed", Status = TenantStatus.Active });
        EnqueueQueryResult(clientResponses, Detail("beta") with { Name = "same", Status = TenantStatus.Active });
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(
                Search: "needle",
                SortColumn: sortColumn,
                SortDescending: descending,
                PageSize: 3),
            previous: null,
            CancellationToken.None);

        snapshot.Rows.Select(static row => row.TenantId).ShouldBe(expectedOrder.Split(','));
        snapshot.Rows.ShouldAllBe(static row => row.PendingState == TenantPendingState.Unknown);
    }

    [Fact]
    public async Task List_search_rechecks_status_and_aggregates_authoritative_stale_and_unknown_freshness()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            3,
            Hit("tenant:current"),
            Hit("tenant:stale"),
            Hit("tenant:changed-status")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("current"), metadata: ProjectionBackedMetadata(isStale: false));
        EnqueueQueryResult(clientResponses, Detail("stale"), metadata: ProjectionBackedMetadata(isStale: true));
        EnqueueQueryResult(clientResponses, Detail("changed-status") with { Status = TenantStatus.Disabled });
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", Status: TenantStatus.Active, PageSize: 3),
            previous: null,
            CancellationToken.None);

        snapshot.Rows.Select(static row => row.TenantId).ShouldBe(["current", "stale"]);
        snapshot.Rows.Single(static row => row.TenantId == "current").Freshness.ShouldBe(ReadModelFreshnessState.Current);
        snapshot.Rows.Single(static row => row.TenantId == "stale").Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Stale);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("json")]
    [InlineData("invalid-operation")]
    [InlineData("non-caller-cancellation")]
    public async Task List_search_falls_back_for_each_memories_availability_failure_family(string failure)
    {
        Exception exception = failure switch
        {
            "http" => new HttpRequestException("raw query URI"),
            "timeout" => new TimeoutException("raw offset"),
            "json" => new JsonException("raw response"),
            "invalid-operation" => new InvalidOperationException("raw client state"),
            "non-caller-cancellation" => new OperationCanceledException("upstream timeout"),
            _ => throw new InvalidOperationException($"Unknown failure {failure}."),
        };
        StubMemoriesClient memories = new();
        memories.Enqueue(exception);
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle"),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);

        // A dead key ring must stay distinguishable from an unhealthy index, without disclosing anything.
        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain(TenantQueryGateway.SearchIndexUnavailableReasonCode);
        ShouldNotDisclose(logger, "needle", exception.Message);
    }

    [Fact]
    public async Task List_search_silently_drops_forbidden_and_missing_candidates_but_generically_degrades_operational_detail_shapes()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            totalCount: 8,
            Hit("tenant:forbidden"),
            Hit("tenant:missing"),
            Hit("tenant:null-detail"),
            Hit("tenant:mismatch"),
            Hit("tenant:degraded"),
            Hit("tenant:stale")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, (int)HttpStatusCode.Forbidden);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.NotFound, (int)HttpStatusCode.NotFound);
        EnqueueDetailResult(clientResponses, payload: null);
        EnqueueQueryResult(clientResponses, Detail("different-tenant"));
        EnqueueQueryResult(clientResponses, Detail("degraded"), metadata: ProjectionBackedMetadata(isDegraded: true));
        EnqueueQueryResult(clientResponses, Detail("stale"), metadata: ProjectionBackedMetadata(isStale: true));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 6),
            previous: null,
            CancellationToken.None);

        ReadCalls(client).Select(static query => ((GetTenantQuery)query.GetArguments()[0]!).TenantId).ShouldBe(
            ["forbidden", "missing", "null-detail", "mismatch", "degraded", "stale"]);
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("stale");
        snapshot.Rows[0].Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Degraded);
        snapshot.Reason.ShouldBe(TenantListReason.SearchPartiallyAvailable);
        snapshot.HasMore.ShouldBeTrue();
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 6);
        codec.TryDecode(snapshot.NextCursor, scope, out int nextOffset).ShouldBeTrue();
        nextOffset.ShouldBe(6);
    }

    [Fact]
    public async Task List_search_bounds_maximum_page_hydration_concurrency_and_keeps_deterministic_order()
    {
        const int candidateCount = 100;
        MemoriesScoredResult[] hits = Enumerable.Range(0, candidateCount)
            .Select(static index => Hit($"tenant:tenant-{index:D3}"))
            .ToArray();
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", candidateCount, hits));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        int active = 0;
        int maximum = 0;
        client.GetTenantAsync(
                Arg.Any<GetTenantQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                GetTenantQuery query = call.ArgAt<GetTenantQuery>(0);
                CancellationToken cancellationToken = call.ArgAt<CancellationToken>(2);
                ObserveMaximum(ref maximum, Interlocked.Increment(ref active));

                // A fixed delay (rather than a barrier at the expected limit) means a changed production
                // limit fails this assertion instead of deadlocking the suite.
                await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
                _ = Interlocked.Decrement(ref active);
                return new TenantsRestQueryResponse<TenantDetail>(
                    Detail(query.TenantId) with { Name = query.TenantId },
                    ProjectionBackedMetadata(isStale: false),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.OK);
            });
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: candidateCount),
            previous: null,
            timeout.Token);

        // Pinned to an independent literal. Comparing the observed maximum only against the production
        // constant moved both sides together: raising it to the 100 page-size ceiling -- i.e. removing the
        // bound entirely -- satisfied "<= constant" and "> constant / 2" just as well, so the guard could
        // not fail. The bound exists to stop a 100-hit search becoming 100 concurrent authorized reads.
        const int expectedBound = 8;
        TenantQueryGateway.MaximumHydrationConcurrency.ShouldBe(expectedBound);
        maximum.ShouldBeLessThanOrEqualTo(expectedBound);
        maximum.ShouldBeGreaterThan(expectedBound / 2);
        snapshot.Rows.Count.ShouldBe(candidateCount);
        snapshot.Rows.Select(static row => row.TenantId).ShouldBe(
            Enumerable.Range(0, candidateCount).Select(static index => $"tenant-{index:D3}"));
    }

    [Fact]
    public async Task List_search_propagates_caller_cancellation_during_bounded_hydration_without_ordinary_fallback()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            2,
            Hit("tenant:alpha"),
            Hit("tenant:beta")));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int queryCount = 0;
        client.GetTenantAsync(
                Arg.Any<GetTenantQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                _ = Interlocked.Increment(ref queryCount);
                started.TrySetResult();
                CancellationToken cancellationToken = call.ArgAt<CancellationToken>(2);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return DirectResponse(Detail("unreachable"));
            });
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);
        using var cancellation = new CancellationTokenSource();

        Task<TenantListSnapshot> pending = gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 2),
            previous: null,
            cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        queryCount.ShouldBeInRange(1, 2);
        memories.SearchRequests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("cryptographic")]
    [InlineData("format")]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    [InlineData("overflow")]
    [InlineData("json")]
    [InlineData("not-supported")]
    public async Task List_search_contains_every_cursor_decode_exception_type_as_page_one_invalidation(string exceptionKind)
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            memoriesClient: memories,
            searchCursorCodec: new ThrowingSearchCursorCodec(decodeFailure: () => CodecFailure(exceptionKind)));

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: "unsafe-cursor"),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(0);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.SearchRefreshed);
        snapshot.PagingRecovered.ShouldBeTrue();
    }

    [Fact]
    public async Task List_search_ignores_a_failed_codec_out_value_and_requests_page_zero_once()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 1, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        TenantQueryGateway gateway = CreateGateway(
            client,
            memoriesClient: memories,
            searchCursorCodec: new ThrowingSearchCursorCodec(failedDecodeOffset: 73));

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: "invalid-cursor"),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Offset.ShouldBe(0);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Notice.ShouldBe(TenantListReason.SearchRefreshed);
    }

    [Theory]
    [InlineData("cryptographic")]
    [InlineData("format")]
    [InlineData("invalid-operation")]
    [InlineData("argument")]
    [InlineData("overflow")]
    [InlineData("json")]
    [InlineData("not-supported")]
    public async Task List_search_contains_every_cursor_encode_exception_type_and_degrades_to_the_ordinary_list(string exceptionKind)
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", 2, Hit("tenant:alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, Detail("alpha"));
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(
            client,
            memoriesClient: memories,
            searchCursorCodec: new ThrowingSearchCursorCodec(encodeFailure: () => CodecFailure(exceptionKind)),
            logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 1),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
        snapshot.NextCursor.ShouldBeNull();
        string message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain(TenantQueryGateway.SearchCursorProtectionUnavailableReasonCode);
        ShouldNotDisclose(logger, "needle", "alpha", "key ring unavailable");
    }

    [Fact]
    public async Task Search_diagnostics_only_ever_emit_support_safe_reason_codes()
    {
        // Runtime proof that the only diagnostic channel the gateway owns carries reason codes and nothing
        // else, across a healthy page, an index outage, an invalid response, and a dead key ring.
        CapturingLogger logger = new();
        const string query = "needle";
        const string tenantId = "tenant.secret-name";

        StubMemoriesClient healthy = new();
        healthy.Enqueue(SearchResult(query, 1, Hit($"tenant:{tenantId}")));
        ITenantsRestQueryClient healthyClient = CreateReadClient(out Queue<object> healthyClientResponses);
        EnqueueQueryResult(healthyClientResponses, Detail(tenantId));
        _ = await CreateGateway(healthyClient, memoriesClient: healthy, logger: logger).ListTenantsAsync(
            new TenantListRequest(Search: query),
            previous: null,
            CancellationToken.None);
        logger.Messages.ShouldBeEmpty("A healthy authoritative page must be diagnostically silent.");

        StubMemoriesClient outage = new();
        outage.Enqueue(new HttpRequestException($"GET /search?query={query}&offset=40"));
        ITenantsRestQueryClient outageClient = CreateReadClient(out Queue<object> outageClientResponses);
        EnqueueQueryResult(outageClientResponses, new PaginatedResult<TenantSummary>([], null, false));
        _ = await CreateGateway(outageClient, memoriesClient: outage, logger: logger).ListTenantsAsync(
            new TenantListRequest(Search: query),
            previous: null,
            CancellationToken.None);

        StubMemoriesClient invalid = new();
        invalid.Enqueue(SearchResult(query, 1, Hit($"tenant:{tenantId}")) with { Degraded = true });
        ITenantsRestQueryClient invalidClient = CreateReadClient(out Queue<object> invalidClientResponses);
        EnqueueQueryResult(invalidClientResponses, new PaginatedResult<TenantSummary>([], null, false));
        _ = await CreateGateway(invalidClient, memoriesClient: invalid, logger: logger).ListTenantsAsync(
            new TenantListRequest(Search: query),
            previous: null,
            CancellationToken.None);

        // A contained decode failure alone is not a degradation: its forced page-zero retry succeeds
        // authoritatively, so the surface never resolved to the ordinary list and nothing may be emitted.
        StubMemoriesClient recoveredKeyRing = new();
        recoveredKeyRing.Enqueue(SearchResult(query, 1, Hit($"tenant:{tenantId}")));
        ITenantsRestQueryClient recoveredKeyRingClient = CreateReadClient(out Queue<object> recoveredKeyRingClientResponses);
        EnqueueQueryResult(recoveredKeyRingClientResponses, Detail(tenantId));
        TenantListSnapshot recovered = await CreateGateway(
                recoveredKeyRingClient,
                memoriesClient: recoveredKeyRing,
                searchCursorCodec: new ThrowingSearchCursorCodec(
                    decodeFailure: static () => new CryptographicException("key ring unavailable")),
                logger: logger)
            .ListTenantsAsync(
                new TenantListRequest(Search: query, SearchCursor: "protected-cursor-value"),
                previous: null,
                CancellationToken.None);
        recovered.IsAuthoritativeSearch.ShouldBeTrue();
        logger.Messages.Count.ShouldBe(2, "A cursor hiccup that recovers authoritatively degraded nothing.");

        // A key ring that cannot protect the next cursor does resolve to the ordinary list, so it must be
        // reported: this is the signal that distinguishes a dead key ring from a healthy index.
        StubMemoriesClient deadKeyRing = new();
        deadKeyRing.Enqueue(SearchResult(query, totalCount: 40, Hit($"tenant:{tenantId}")));
        ITenantsRestQueryClient deadKeyRingClient = CreateReadClient(out Queue<object> deadKeyRingClientResponses);
        EnqueueQueryResult(deadKeyRingClientResponses, Detail(tenantId));
        EnqueueQueryResult(deadKeyRingClientResponses, new PaginatedResult<TenantSummary>([], null, false));
        _ = await CreateGateway(
                deadKeyRingClient,
                memoriesClient: deadKeyRing,
                searchCursorCodec: new ThrowingSearchCursorCodec(
                    encodeFailure: static () => new CryptographicException("key ring unavailable")),
                logger: logger)
            .ListTenantsAsync(
                new TenantListRequest(Search: query),
                previous: null,
                CancellationToken.None);

        logger.Messages.Count.ShouldBe(3);
        logger.Messages.ShouldContain(static message => message.Contains(TenantQueryGateway.SearchIndexUnavailableReasonCode, StringComparison.Ordinal));
        logger.Messages.ShouldContain(static message => message.Contains(TenantQueryGateway.SearchResponseInvalidReasonCode, StringComparison.Ordinal));

        // A dead or rotated key ring must be distinguishable from routine cursor expiry.
        logger.Messages.ShouldContain(static message
            => message.Contains(TenantQueryGateway.SearchCursorProtectionUnavailableReasonCode, StringComparison.Ordinal));

        ShouldNotDisclose(logger, query, tenantId, "protected-cursor-value", "key ring unavailable");
        foreach (string disclosure in logger.Disclosures)
        {
            disclosure.ShouldNotContain("offset", Case.Insensitive);
        }

        logger.Events.ShouldAllBe(static id
            => id == TenantQueryGateway.SearchDegradedToOrdinaryListEvent
            || id == TenantQueryGateway.SearchAndOrdinaryListUnavailableEvent);
        logger.Events.ShouldAllBe(static id => id.Id != 0);
    }

    [Fact]
    public async Task Search_degradation_signal_reports_that_the_ordinary_list_was_also_unavailable()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, (int)HttpStatusCode.ServiceUnavailable);
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, logger: logger);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Error);
        logger.Events.ShouldBe([
            TenantQueryGateway.DirectTenantsReadFailedEvent,
            TenantQueryGateway.SearchAndOrdinaryListUnavailableEvent,
        ]);
        logger.Messages.Count.ShouldBe(2);
        logger.Messages[0].ShouldContain("FailureKind=Unavailable");
        logger.Messages[1].ShouldContain("also unavailable");
        logger.Exceptions.ShouldAllBe(static exception => exception == null);
    }

    [Fact]
    public async Task List_search_carries_the_invalidation_notice_on_a_terminal_fallback_surface()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string foreignScope = TenantSearchCursorScopes.Create(
            "another.user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(foreignScope, 40);
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, (int)HttpStatusCode.ServiceUnavailable);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        // The notice bars render from the notice reasons alone and never consult Kind, so a terminal
        // surface carries the explanation together with the clearing. No deferral mechanism exists.
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Error);
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.PagingNotice.ShouldBe(TenantListReason.SearchRefreshed);

        // The terminal copy explains only that the ordinary list failed. Without this the operator is
        // never told that whole-set search failed independently. It must not be the ordinary
        // SearchUnavailable reason, whose copy invites the operator to keep browsing the authorized list --
        // on this path that list is exactly what did not load.
        snapshot.Notice.ShouldBe(TenantListReason.SearchAndListUnavailable);
    }

    [Fact]
    public async Task List_search_does_not_invite_browsing_the_list_on_an_unauthorized_terminal_surface()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unauthorized, (int)HttpStatusCode.Unauthorized);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle"),
            previous: null,
            CancellationToken.None);

        // The Unauthorized surface renders "Sign in required". Carrying SearchUnavailable here put "you can
        // continue browsing the authorized tenant list" directly under it, which is a contradiction: the
        // notice bars render from the reason alone and never consult Kind.
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Unauthorized);
        snapshot.Notice.ShouldBe(TenantListReason.SearchAndListUnavailable);
        snapshot.Notice.ShouldNotBe(TenantListReason.SearchUnavailable);
    }

    [Fact]
    public async Task List_search_clears_protected_history_when_the_same_load_falls_back_renderably()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        string foreignScope = TenantSearchCursorScopes.Create(
            "another.user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string cursor = codec.Encode(foreignScope, 40);
        StubMemoriesClient memories = new();
        memories.Enqueue(new HttpRequestException("Memories unavailable."));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", SearchCursor: cursor),
            previous: null,
            CancellationToken.None);

        // Losing Memories on the same load must not swallow the cursor invalidation.
        snapshot.IsAuthoritativeSearch.ShouldBeFalse();
        snapshot.Notice.ShouldBe(TenantListReason.SearchUnavailable);
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.PagingNotice.ShouldBe(TenantListReason.SearchRefreshed);
    }

    [Fact]
    public async Task List_search_suppresses_a_fully_empty_page_from_claiming_a_filter_verdict()
    {
        var codec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        StubMemoriesClient memories = new();

        // The index fully omitted this window while still reporting more total results.
        memories.Enqueue(SearchResult("needle", totalCount: 30));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories, searchCursorCodec: codec);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.SearchPageEmpty);
        snapshot.Kind.ShouldNotBe(TenantListSurfaceKind.FilteredEmpty);
        snapshot.Rows.ShouldBeEmpty();
        ReadCalls(client).ShouldBeEmpty();

        // The index omitted its own unusable entries: no candidate was hidden from this operator, so paging
        // must keep advancing. Collapsing here would strand accessible matches sitting past this window
        // behind a surface claiming nothing matched at all. Only a window emptied by hiding ends paging --
        // see List_search_ends_paging_when_every_candidate_is_hidden.
        snapshot.HasMore.ShouldBeTrue();
        snapshot.NextCursor.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_search_ends_paging_when_every_candidate_is_hidden()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            totalCount: 30,
            Hit("tenant:tenant.alpha"),
            Hit("tenant:tenant.beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, (int)HttpStatusCode.Forbidden);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.NotFound, (int)HttpStatusCode.NotFound);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        // Both candidates were hydrated and both were hidden or absent, so TotalCount = 30 is entirely
        // pre-authorization knowledge. Advertising a further page would disclose the existence and a
        // page-granular count of tenants this operator is not permitted to see.
        ReadCalls(client).Count.ShouldBe(2);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.SearchPageEmpty);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.HasMore.ShouldBeFalse();
        snapshot.NextCursor.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_search_keeps_paging_when_a_hidden_window_also_contains_an_invalid_hit(bool duplicate)
    {
        StubMemoriesClient memories = new();
        MemoriesScoredResult hidden = Hit("tenant:tenant.hidden");
        memories.Enqueue(SearchResult(
            "needle",
            totalCount: 30,
            hidden,
            duplicate ? Hit("tenant:tenant.hidden") : Hit("not-a-tenant-source")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, (int)HttpStatusCode.Forbidden);
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        // A duplicate or malformed raw hit is not an authorization outcome. If either shares a window with
        // a hidden candidate, the gateway cannot truthfully classify every raw hit as hidden or absent and
        // must keep later authorized matches reachable.
        ReadCalls(client).Count.ShouldBe(1);
        snapshot.Kind.ShouldBe(TenantListSurfaceKind.SearchPageEmpty);
        snapshot.Rows.ShouldBeEmpty();
        snapshot.HasMore.ShouldBeTrue();
        snapshot.NextCursor.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_search_keeps_paging_when_the_operators_own_status_filter_empties_the_window()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(
            "needle",
            totalCount: 30,
            Hit("tenant:tenant.alpha"),
            Hit("tenant:tenant.beta")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailResult(clientResponses, Detail("tenant.alpha"), ProjectionBackedMetadata());
        EnqueueDetailResult(clientResponses, Detail("tenant.beta"), ProjectionBackedMetadata());

        // Both hydrate as Active; the operator asked for Disabled, so the authoritative recheck drops both.
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10, Status: TenantStatus.Disabled),
            previous: null,
            CancellationToken.None);

        // Nothing here is a secret: the operator applied this filter themselves and both candidates were
        // readable. Ending paging would report that nothing matched the search while matching, accessible
        // tenants sat past the window.
        snapshot.Rows.ShouldBeEmpty();
        snapshot.HasMore.ShouldBeTrue();
        snapshot.NextCursor.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_search_keeps_paging_when_an_unrenderable_record_empties_the_window()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 30, Hit("tenant:tenant.alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);

        // Name is non-nullable on the contract, so only a malformed projection can produce this.
        EnqueueDetailResult(clientResponses,
            Detail("tenant.alpha") with { Name = null! },
            ProjectionBackedMetadata());
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        // One malformed record is not an outage and not a hidden tenant. It raises the ordinary
        // enrichment-degraded signal -- which can never reach the fallback path -- and paging still advances.
        snapshot.Rows.ShouldBeEmpty();
        snapshot.Reason.ShouldBe(TenantListReason.RowEnrichmentUnavailable);
        snapshot.Reason.ShouldNotBe(TenantListReason.SearchPartiallyAvailable);
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task List_search_with_the_unknown_status_filter_pushes_down_no_attribute_and_keeps_paging()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 30, Hit("tenant:tenant.alpha")));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueDetailResult(clientResponses, Detail("tenant.alpha"), ProjectionBackedMetadata());
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10, Status: TenantStatus.Unknown),
            previous: null,
            CancellationToken.None);

        // The index publisher coerces Unknown to a concrete fallback and never writes status=Unknown, so
        // pushing it down matched nothing. Dropping the push-down only agrees with the ordinary list because
        // the window the recheck then empties keeps advancing: Unknown is the rare sentinel, so its matches
        // almost never sit in the first raw window.
        memories.SearchRequests.ShouldHaveSingleItem().AttributeFilters.ShouldBeNull();
        snapshot.Rows.ShouldBeEmpty();
        snapshot.HasMore.ShouldBeTrue();
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task List_search_applies_the_shared_length_bound_at_its_exact_boundary(int length, bool applied)
    {
        // Pins the boundary itself, not a value far past it: asserting only that 512 is rejected and 256 is
        // accepted left every bound in [256, 511] passing. The gateway constant is the workspace constant, so
        // a direct gateway caller cannot be held to a different limit than the URL surface.
        TenantQueryGateway.MaximumSearchLength.ShouldBe(256);
        string term = new('a', length);
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult(term, totalCount: 0));
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses, new PaginatedResult<TenantSummary>([], null, false));
        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);

        _ = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: term),
            previous: null,
            CancellationToken.None);

        if (applied)
        {
            memories.SearchRequests.ShouldHaveSingleItem().Query.ShouldBe(term);
        }
        else
        {
            // Rejected terms must reach neither Memories nor the request line that would carry them.
            memories.SearchRequests.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task List_search_trims_the_term_before_applying_the_length_bound()
    {
        // Untrimmed, "acme " and "acme" hash to different cursor scopes and silently restart paging.
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 0));
        TenantQueryGateway gateway = CreateGateway(CreateReadClient(out _), memoriesClient: memories);

        _ = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "  needle  "),
            previous: null,
            CancellationToken.None);

        memories.SearchRequests.ShouldHaveSingleItem().Query.ShouldBe("needle");
    }

    [Fact]
    public async Task List_search_renders_a_fully_hidden_window_identically_to_a_genuine_no_match()
    {
        // The disclosure this guards is a *difference*, so one payload is not evidence. Both causes are
        // driven through the same gateway and every operator-visible field is compared field by field.
        //
        // The hidden arm must really hydrate and really be refused. Feeding a zero-hit page and calling it
        // "hidden" compared "the index omitted this window" against "nothing matched" -- neither of which is
        // an authorization outcome -- so it could not have caught the leak it was written for.
        async Task<TenantListSnapshot> LoadAsync(int totalCount, bool withHiddenCandidates)
        {
            StubMemoriesClient memories = new();
            memories.Enqueue(withHiddenCandidates
                ? SearchResult(
                    "needle",
                    totalCount: totalCount,
                    Hit("tenant:tenant.alpha"),
                    Hit("tenant:tenant.beta"))
                : SearchResult("needle", totalCount: totalCount));
            ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
            if (withHiddenCandidates)
            {
                EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Forbidden, (int)HttpStatusCode.Forbidden);
                EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.NotFound, (int)HttpStatusCode.NotFound);
            }

            return await CreateGateway(
                    client,
                    memoriesClient: memories,
                    searchCursorCodec: new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()))
                .ListTenantsAsync(
                    new TenantListRequest(Search: "needle", PageSize: 10),
                    previous: null,
                    CancellationToken.None);
        }

        // 30 matching tenants exist in the index; every candidate in this window is refused to this caller.
        TenantListSnapshot hidden = await LoadAsync(totalCount: 30, withHiddenCandidates: true);
        TenantListSnapshot noMatch = await LoadAsync(totalCount: 0, withHiddenCandidates: false);

        hidden.Kind.ShouldBe(noMatch.Kind);
        hidden.HasMore.ShouldBe(noMatch.HasMore);
        hidden.NextCursor.ShouldBe(noMatch.NextCursor);
        hidden.Reason.ShouldBe(noMatch.Reason);
        hidden.Notice.ShouldBe(noMatch.Notice);
        hidden.Rows.Count.ShouldBe(noMatch.Rows.Count);
        hidden.IsDegraded.ShouldBe(noMatch.IsDegraded);
        hidden.IsAuthorizationScopedEmpty.ShouldBe(noMatch.IsAuthorizationScopedEmpty);
        hidden.Freshness.ShouldBe(noMatch.Freshness);

        // The pinned diagnostic is a disclosure channel too, so it must not separate the two either.
        hidden.ToString().ShouldBe(noMatch.ToString());
    }

    [Fact]
    public void Snapshot_diagnostics_are_pinned_and_disclose_no_row_identity()
    {
        TenantListSnapshot snapshot = new(
            TenantListSurfaceKind.Degraded,
            [
                new TenantListRow(
                    "tenant.secret-name",
                    "Secret Display Name",
                    TenantStatus.Active,
                    TenantCountValue.Known(3),
                    TenantCountValue.Known(1),
                    TenantPendingState.Unknown,
                    ReadModelFreshnessState.Stale),
            ],
            NextCursor: "protected-next-cursor",
            HasMore: true,
            ETag: "secret-etag",
            ReadModelFreshnessState.Stale,
            IsDegraded: true,
            IsAuthorizationScopedEmpty: false,
            Reason: TenantListReason.SearchPartiallyAvailable,
            Notice: TenantListReason.SearchUnavailable,
            IsAuthoritativeSearch: true,
            PagingRecovered: true,
            FallbackPagingRecovered: true,
            PagingNotice: TenantListReason.ListRefreshed);

        // Pinned exactly: any field added to the diagnostic surface must be reviewed here before it ships.
        // A substring scan over this format could never fail, so it is deliberately not used as evidence.
        snapshot.ToString().ShouldBe(
            "TenantListSnapshot { Kind = Degraded, RowCount = 1, HasMore = True, Freshness = Stale, Lifecycle = Unknown, "
            + "IsDegraded = True, IsAuthorizationScopedEmpty = False, Reason = SearchPartiallyAvailable, "
            + "Notice = SearchUnavailable, IsAuthoritativeSearch = True, PagingRecovered = True, "
            + "FallbackPagingRecovered = True, PagingNotice = ListRefreshed }");
    }

    [Fact]
    public void Audit_and_user_membership_snapshot_diagnostics_omit_protected_state()
    {
        TenantAuditRequest auditRequest = new(
            "tenant.audit-secret",
            Category: AuditEventCategory.Access,
            Cursor: "audit-cursor-secret",
            PageSize: 25,
            ETag: "audit-etag-secret");
        TenantAuditSnapshot audit = TenantAuditSnapshot.Ready(
            [TenantAuditRow.FromEntry(AuditEntry("event-safe", AuditEventCategory.Access), ReadModelFreshnessState.Current)],
            nextCursor: "audit-next-secret",
            hasMore: true,
            eTag: "audit-etag-secret",
            ReadModelFreshnessState.Current,
            auditRequest) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "audit-version-secret",
        };
        UserTenantMembershipSnapshot memberships = UserTenantMembershipSnapshot.Ready(
            [new UserTenantMembershipRow(
                "tenant.membership-secret",
                "Secret tenant",
                TenantStatus.Active,
                TenantRole.TenantReader,
                ReadModelFreshnessState.Current)],
            nextCursor: "membership-next-secret",
            hasMore: true,
            eTag: "membership-etag-secret",
            ReadModelFreshnessState.Current,
            targetUserId: "target-user-secret") with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator-user"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "membership-version-secret",
            RequestCursor = "membership-cursor-secret",
            PagingRecovered = true,
        };

        audit.ToString().ShouldBe(
            "TenantAuditSnapshot { Kind = Ready, RowCount = 1, HasMore = True, Freshness = Current, Lifecycle = Current, IsAuthorizationScopedEmpty = False, Reason = None }");
        memberships.ToString().ShouldBe(
            "UserTenantMembershipSnapshot { Kind = Ready, RowCount = 1, HasMore = True, Freshness = Current, Lifecycle = Current, IsAuthorizationScopedEmpty = False, Reason = None, PagingRecovered = True }");
    }

    /// <summary>The exact surfacing set the gateway's containment predicate excludes before any base match.</summary>
    private static Exception SurfacingCodecDefect(string exceptionKind)
        => exceptionKind switch
        {
            "out-of-memory" => new OutOfMemoryException("codec defect"),
            "null-reference" => new NullReferenceException("codec defect"),
            "object-disposed" => new ObjectDisposedException("key-ring"),
            "argument-null" => new ArgumentNullException("scope"),
            _ => throw new InvalidOperationException($"Unknown surfacing defect {exceptionKind}."),
        };

    private static Exception CodecFailure(string exceptionKind)
        => exceptionKind switch
        {
            "cryptographic" => new CryptographicException("key ring unavailable"),
            "format" => new FormatException("key ring unavailable"),
            "invalid-operation" => new InvalidOperationException("key ring unavailable"),
            "argument" => new ArgumentException("key ring unavailable"),
            "overflow" => new OverflowException("key ring unavailable"),
            "json" => new JsonException("key ring unavailable"),
            "not-supported" => new NotSupportedException("key ring unavailable"),
            _ => throw new InvalidOperationException($"Unknown codec failure {exceptionKind}."),
        };

    /// <summary>
    /// Returns one fixed failure for every read, at the real <see cref="ITenantsRestQueryClient"/> seam.
    /// </summary>
    /// <remarks>
    /// Failures flow through TenantQueryGateway.ToEventStoreResult before surface-specific mapping.
    /// </remarks>
    private sealed class FixedFailureRestQueryClient(TenantsRestQueryFailureKind failureKind, int statusCode)
        : ITenantsRestQueryClient
    {
        private TenantsRestQueryResponse<TPayload> Response<TPayload>()
            => new(default, new QueryResponseMetadata(), failureKind, statusCode);

        public Task<TenantsRestQueryResponse<PaginatedResult<TenantSummary>>> ListTenantsAsync(
            ListTenantsQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PaginatedResult<TenantSummary>>());

        public Task<TenantsRestQueryResponse<TenantDetail>> GetTenantAsync(
            GetTenantQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<TenantDetail>());

        public Task<TenantsRestQueryResponse<PaginatedResult<TenantMember>>> GetTenantUsersAsync(
            GetTenantUsersQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PaginatedResult<TenantMember>>());

        public Task<TenantsRestQueryResponse<PaginatedResult<UserTenantMembership>>> GetUserTenantsAsync(
            GetUserTenantsQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PaginatedResult<UserTenantMembership>>());

        public Task<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>> GetTenantAuditAsync(
            GetTenantAuditQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PaginatedResult<TenantAuditEntry>>());

        public Task<TenantsRestQueryResponse<PaginatedResult<GlobalAdministratorSummary>>> GetGlobalAdministratorsAsync(
            GetGlobalAdministratorsQuery query, string? eTag, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PaginatedResult<GlobalAdministratorSummary>>());
    }

    private static TenantQueryGateway CreateGateway(
        ITenantsRestQueryClient client,
        string? userId = "operator-user",
        StubMemoriesClient? memoriesClient = null,
        ITenantSearchCursorCodec? searchCursorCodec = null,
        ITenantsBffComposition? bffComposition = null,
        CapturingLogger? logger = null,
        TimeSpan? enrichmentDeadline = null,
        IUserContextAccessor? userContextAccessor = null,
        CircuitServicesAccessor? circuitServicesAccessor = null,
        AuthenticationStateProvider? authenticationStateProvider = null)
    {
        IUserContextAccessor userContext = userContextAccessor ?? Substitute.For<IUserContextAccessor>();
        if (userContextAccessor is null)
        {
            userContext.UserId.Returns(userId);
            userContext.TenantId.Returns("tenant.context");
        }

        return new TenantQueryGateway(
            client,
            userContext,
            memoriesClient ?? new StubMemoriesClient(),
            searchCursorCodec ?? new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()),
            bffComposition,
            logger,
            enrichmentDeadline,
            circuitServicesAccessor,
            authenticationStateProvider);
    }

    private static TenantDetailSnapshot ReadyConfigurationSnapshot()
    {
        TenantConfigurationSafeRow priorRow = new("billing", "billing.mode", "prior-visible");
        TenantConfigurationComposition priorComposition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(Detail("tenant.alpha")),
            TenantConfigurationSafeModel.Available("tenant.alpha", [priorRow]),
            TenantConfigurationManagementContext.Available(
                "tenant.alpha",
                TenantStatus.Active,
                false,
                ["billing"],
                [priorRow],
                TenantConfigurationAuthorityState.TenantOwner));
        return TenantDetailSnapshot.Ready(
            priorComposition,
            "prior",
            ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator-user"));
    }

    private static void ObserveMaximum(ref int maximum, int current)
    {
        int observed = Volatile.Read(ref maximum);
        while (current > observed)
        {
            int prior = Interlocked.CompareExchange(ref maximum, current, observed);
            if (prior == observed)
            {
                return;
            }

            observed = prior;
        }
    }

    // Grants `billing` on tenant.alpha to the default test subject so projection-proof tests exercise
    // the comparison rather than the policy gate. Proof authorization is namespace-only by design: a
    // key may be commanded under proven scope while remaining absent from the read model.
    private const string BillingGrantPolicyJson = """
        {
          "Tenants": {
            "ConfigurationReadPolicy": {
              "PrefixGrants": [{ "TenantId": "tenant.alpha", "Subject": "operator-user", "Prefix": "billing" }],
              "DisplaySafe": ["billing.mode"]
            }
          }
        }
        """;

    private static ITenantsBffComposition ConfigurationComposition(string json)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();
        return ConfigurationComposition(configuration);
    }

    private static ITenantsBffComposition ConfigurationComposition(IConfiguration configuration)
    {
        ITenantConfigurationPrincipalResolver principalResolver = new StubConfigurationPrincipalResolver(
            TenantConfigurationPrincipalEvidence.NonAdministrator("operator-user"));
        return new TenantsBffComposition(
            new UnavailableTenantCommandGateway(),
            principalResolver: principalResolver,
            policyProvider: new TenantConfigurationReadPolicyProvider(configuration));
    }

    [Fact]
    public async Task List_search_stamps_each_row_with_its_own_projection_lifecycle()
    {
        // The search path stamps ResolveLifecycle(result.Metadata) per hydrated candidate and aggregates
        // across rows, and nothing asserted either: inverting AggregateLifecycle to always return Unknown,
        // or dropping the per-row stamp, failed no test.
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 2, Hit("tenant:tenant.alpha"), Hit("tenant:tenant.beta")));

        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        EnqueueQueryResult(clientResponses,
            Detail("tenant.beta"),
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));

        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);
        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        snapshot.Rows.Count.ShouldBe(2);
        snapshot.Rows.ShouldAllBe(row => row.Lifecycle == ProjectionLifecycleState.Current);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Current);
    }

    [Fact]
    public async Task List_search_aggregates_disagreeing_row_lifecycles_to_unknown()
    {
        // Rows that disagree must not let the surface claim either state for the whole page.
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("needle", totalCount: 2, Hit("tenant:tenant.alpha"), Hit("tenant:tenant.beta")));

        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueQueryResult(clientResponses,
            Detail("tenant.alpha"),
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current));
        EnqueueQueryResult(clientResponses,
            Detail("tenant.beta"),
            metadata: ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Rebuilding));

        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);
        TenantListSnapshot snapshot = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "needle", PageSize: 10),
            previous: null,
            CancellationToken.None);

        snapshot.Rows.Select(static row => row.Lifecycle)
            .ShouldBe([ProjectionLifecycleState.Current, ProjectionLifecycleState.Rebuilding], ignoreOrder: true);
        snapshot.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
    }

    /// <summary>
    /// An unconditional retry that fails must keep the confirmed rows it was meant to recover.
    /// </summary>
    /// <remarks>
    /// Retention required a non-empty request ETag, but "Retry" deliberately refreshes without a validator.
    /// The guard therefore evaluated false on exactly the action offered for recovery, the snapshot
    /// collapsed to Error with an empty row set, and pressing Retry became the thing that destroyed the
    /// confirmed data.
    /// </remarks>
    [Fact]
    public async Task Get_global_administrators_unconditional_retry_failure_retains_confirmed_rows()
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client)
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: null),
                previous,
                CancellationToken.None);

        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin-1");
        snapshot.ETag.ShouldBe("known");
        snapshot.Reason.ShouldBe(GlobalAdministratorsReason.GatewayFailure);
    }

    /// <summary>
    /// A mismatched request validator still blocks retention.
    /// </summary>
    [Fact]
    public async Task Get_global_administrators_failure_with_mismatched_validator_does_not_retain_rows()
    {
        GlobalAdministratorsSnapshot previous = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("admin-1", ReadModelFreshnessState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "known",
            freshness: ReadModelFreshnessState.Current) with
        { CallerScope = TenantQueryGateway.AuditCallerScope("operator-user") };
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueFailure(clientResponses, TenantsRestQueryFailureKind.Unavailable, 503);

        GlobalAdministratorsSnapshot snapshot = await CreateGateway(client)
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: "different"),
                previous,
                CancellationToken.None);

        snapshot.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// A first member load with nothing retained is an error, not retained degradation.
    /// </summary>
    /// <remarks>
    /// Degraded describes confirmed rows held under reduced confidence. Reporting it with no prior snapshot
    /// rendered an empty table as though it carried previously-confirmed data. The audit sibling already
    /// returned a true error state for the same condition.
    /// </remarks>
    [Fact]
    public async Task Get_tenant_users_missing_payload_without_retained_rows_is_an_error_state()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TenantsRestQueryResponse<PaginatedResult<TenantMember>>(
                null,
                ProjectionBackedMetadata(isStale: false, lifecycle: ProjectionLifecycleState.Current, projectionVersion: "v1"),
                TenantsRestQueryFailureKind.None,
                200));
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns("operator-user");

        TenantUsersSnapshot snapshot = await new TenantQueryGateway(
                client,
                userContext,
                new StubMemoriesClient(),
                new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()))
            .GetTenantUsersAsync(new TenantUsersRequest("tenant.alpha"), previous: null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// A transport fault in the member read is contained, not propagated into the render path.
    /// </summary>
    /// <remarks>
    /// This was the only gateway read without exception containment, so a UriFormatException,
    /// InvalidOperationException from URI construction, or a throw from the bearer-relay handler escaped
    /// into OnParametersSetAsync — which catches only OperationCanceledException — and tore down the circuit.
    /// </remarks>
    [Fact]
    public async Task Get_tenant_users_contains_unexpected_transport_faults()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<PaginatedResult<TenantMember>>>(
                _ => throw new InvalidOperationException("the client requires an absolute base address"));
        IUserContextAccessor userContext = Substitute.For<IUserContextAccessor>();
        userContext.UserId.Returns("operator-user");

        TenantUsersSnapshot snapshot = await new TenantQueryGateway(
                client,
                userContext,
                new StubMemoriesClient(),
                new TenantSearchCursorCodec(new EphemeralDataProtectionProvider()))
            .GetTenantUsersAsync(new TenantUsersRequest("tenant.alpha"), previous: null, CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Unavailable);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remaining_paginated_reads_contain_unexpected_transport_faults()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<PaginatedResult<TenantSummary>>>(
                _ => throw new InvalidOperationException("list transport defect"));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<PaginatedResult<UserTenantMembership>>>(
                _ => throw new InvalidOperationException("membership transport defect"));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<PaginatedResult<GlobalAdministratorSummary>>>(
                _ => throw new InvalidOperationException("administrator transport defect"));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<PaginatedResult<TenantAuditEntry>>>(
                _ => throw new InvalidOperationException("audit transport defect"));
        TenantQueryGateway gateway = CreateGateway(client);

        TenantListSnapshot list = await gateway.ListTenantsAsync(new TenantListRequest(), null, CancellationToken.None);
        UserTenantMembershipSnapshot memberships = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user"), null, CancellationToken.None);
        GlobalAdministratorsSnapshot administrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(), null, CancellationToken.None);
        TenantAuditSnapshot audit = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);

        list.Kind.ShouldBe(TenantListSurfaceKind.Error);
        memberships.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Unavailable);
        administrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unavailable);
        audit.Kind.ShouldBe(TenantAuditSurfaceKind.Unavailable);
    }

    [Fact]
    public async Task Paginated_not_modified_without_matching_evidence_refetches_unconditionally()
    {
        ITenantsRestQueryClient listClient = CreateReadClient(out Queue<object> listClientResponses);
        EnqueueNotModified(listClientResponses, "list-etag");
        EnqueueQueryResult(listClientResponses, new PaginatedResult<TenantSummary>([], Cursor: null, HasMore: false));
        TenantListSnapshot list = await CreateGateway(listClient).ListTenantsAsync(
            new TenantListRequest(ETag: "list-etag"),
            previous: null,
            CancellationToken.None);

        ITenantsRestQueryClient administratorClient = CreateReadClient(out Queue<object> administratorClientResponses);
        EnqueueGlobalAdministratorsNotModified(administratorClientResponses, "admin-etag");
        EnqueueQueryResult(administratorClientResponses, new PaginatedResult<GlobalAdministratorSummary>(
            [new GlobalAdministratorSummary("admin.alpha")],
            Cursor: null,
            HasMore: false));
        GlobalAdministratorsSnapshot administrators = await CreateGateway(administratorClient)
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: "admin-etag"),
                previous: null,
                CancellationToken.None);

        list.Kind.ShouldBe(TenantListSurfaceKind.Empty);
        list.Rows.ShouldBeEmpty();
        ReadCalls(listClient)[1].GetArguments()[1].ShouldBeNull();
        administrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Ready);
        administrators.Rows.ShouldHaveSingleItem().UserId.ShouldBe("admin.alpha");
        ReadCalls(administratorClient)[1].GetArguments()[1].ShouldBeNull();
    }

    [Fact]
    public async Task Get_user_tenants_preserves_explicit_invalid_cursor_signal_and_recovers_page_one()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetUserTenantsAsync(
                Arg.Any<GetUserTenantsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<GetUserTenantsQuery>(0).Cursor is null
                ? DirectResponse(new PaginatedResult<UserTenantMembership>(
                    [new UserTenantMembership("tenant.alpha", "Alpha", TenantStatus.Active, TenantRole.TenantReader)],
                    Cursor: null,
                    HasMore: false))
                : FailureResponse<PaginatedResult<UserTenantMembership>>(
                    TenantsRestQueryFailureKind.InvalidCursor,
                    (int)HttpStatusCode.BadRequest));

        UserTenantMembershipSnapshot snapshot = await CreateGateway(client).GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", Cursor: "invalid"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Ready);
        snapshot.Reason.ShouldBe(UserTenantMembershipReason.PageRecovered);
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        _ = client.Received(1).GetUserTenantsAsync(
            Arg.Is<GetUserTenantsQuery>(query => query != null && query.Cursor == null),
            null,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A 304 the client cannot pair with a retained snapshot has its own reason on all four paged reads.
    /// </summary>
    /// <remarks>
    /// The server insists nothing changed while nothing is retained to show, which is neither an outage nor
    /// degraded evidence. <c>NotModifiedWithoutSnapshot</c> names exactly that state and shipped with EN/FR
    /// copy and four enum members but no producer at all -- a test hand-built the state and asserted its copy
    /// rendered, so the suite read as though it were live while an operator could never reach it.
    /// </remarks>
    [Fact]
    public async Task A_not_modified_response_with_nothing_retained_has_its_own_reason_on_every_paged_read()
    {
        ITenantsRestQueryClient client = CreateReadClient(out Queue<object> clientResponses);
        EnqueueNotModified(clientResponses, "unknown-etag");
        EnqueueNotModified(clientResponses, "unknown-etag");
        TenantQueryGateway gateway = CreateGateway(client);
        TenantListSnapshot list = await gateway.ListTenantsAsync(
            new TenantListRequest(ETag: "unknown-etag"),
            previous: null,
            CancellationToken.None);
        list.Reason.ShouldBe(TenantListReason.NotModifiedWithoutSnapshot);
        AssertUnconditionalRetry(client);

        ITenantsRestQueryClient userTenantsClient = CreateReadClient(out Queue<object> userTenantsClientResponses);
        EnqueueUserTenantsNotModified(userTenantsClientResponses, "unknown-etag");
        EnqueueUserTenantsNotModified(userTenantsClientResponses, "unknown-etag");
        UserTenantMembershipSnapshot memberships = await CreateGateway(userTenantsClient).GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", ETag: "unknown-etag"),
            previous: null,
            CancellationToken.None);
        memberships.Reason.ShouldBe(UserTenantMembershipReason.NotModifiedWithoutSnapshot);
        AssertUnconditionalRetry(userTenantsClient);

        ITenantsRestQueryClient administratorsClient = CreateReadClient(out Queue<object> administratorsClientResponses);
        EnqueueGlobalAdministratorsNotModified(administratorsClientResponses, "unknown-etag");
        EnqueueGlobalAdministratorsNotModified(administratorsClientResponses, "unknown-etag");
        GlobalAdministratorsSnapshot administrators = await CreateGateway(administratorsClient)
            .GetGlobalAdministratorsAsync(
                new GlobalAdministratorsRequest(ETag: "unknown-etag"),
                previous: null,
                CancellationToken.None);
        administrators.Reason.ShouldBe(GlobalAdministratorsReason.NotModifiedWithoutSnapshot);
        AssertUnconditionalRetry(administratorsClient);

        ITenantsRestQueryClient auditClient = CreateReadClient(out Queue<object> auditClientResponses);
        EnqueueAuditNotModified(auditClientResponses, "unknown-etag");
        EnqueueAuditNotModified(auditClientResponses, "unknown-etag");
        TenantAuditSnapshot audit = await CreateGateway(auditClient).GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha", ETag: "unknown-etag"),
            previous: null,
            CancellationToken.None);
        audit.Reason.ShouldBe(TenantAuditReason.NotModifiedWithoutSnapshot);
        AssertUnconditionalRetry(auditClient);

        static void AssertUnconditionalRetry(ITenantsRestQueryClient client)
        {
            ReadCalls(client).Count.ShouldBe(2);
            ReadCalls(client)[0].GetArguments()[1].ShouldBeNull();
            ReadCalls(client)[1].GetArguments()[1].ShouldBeNull();
        }
    }

    /// <summary>
    /// A blank route identifier is a malformed request, not an outage.
    /// </summary>
    /// <remarks>
    /// <c>TenantDetailPage</c> builds this request straight from the route value, so <c>/tenants/%20</c>
    /// reaches the gateway. Without the guard the blank id threw inside the typed client, was swallowed by
    /// the generic handler, and surfaced as <c>Unavailable</c> plus a <c>DirectTenantsReadFailed</c> warning:
    /// a malformed request reported as the API being down, in the UI and in the operational log. Making the
    /// guard unable to fire survived the suite.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Get_tenant_treats_a_blank_route_identifier_as_not_found(string tenantId)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        CapturingLogger logger = new();

        TenantDetailSnapshot snapshot = await CreateGateway(client, logger: logger).GetTenantAsync(
            new TenantDetailRequest(tenantId),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantDetailSurfaceKind.NotFound);
        _ = client.DidNotReceive().GetTenantAsync(
            Arg.Any<GetTenantQuery>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        logger.Messages.ShouldBeEmpty("A malformed request is not a read failure and must not be logged as one.");
    }

    /// <summary>
    /// The same guard on the other three route-carrying reads: no request is sent, nothing is logged as a
    /// read failure, and each surface reports its own malformed-request state.
    /// </summary>
    /// <remarks>
    /// Only the detail arm was covered, and only it asserted that nothing is logged. The three guards return
    /// different kinds, so a single shared assertion cannot stand in for them. The audit arm is the one worth
    /// naming: it used to return <c>Degraded([], MissingTenantId)</c>, even though degradation means confirmed
    /// rows held under reduced confidence and the malformed request never produced any evidence. It now uses
    /// the true first-load error shape required by decision D3.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Blank_route_identifiers_are_malformed_requests_on_every_read(string blank)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, logger: logger);

        TenantUsersSnapshot members = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest(blank),
            previous: null,
            CancellationToken.None);
        members.Kind.ShouldBe(TenantUsersSurfaceKind.Invalid);
        members.Reason.ShouldBe(TenantUsersReason.MissingTenantId);

        UserTenantMembershipSnapshot userTenants = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest(TargetUserId: blank),
            previous: null,
            CancellationToken.None);
        userTenants.Kind.ShouldBe(UserTenantMembershipSurfaceKind.Invalid);
        userTenants.Reason.ShouldBe(UserTenantMembershipReason.MissingTargetUser);

        TenantAuditSnapshot audit = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest(blank),
            previous: null,
            CancellationToken.None);
        audit.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        audit.Reason.ShouldBe(TenantAuditReason.MissingTenantId);
        audit.Rows.ShouldBeEmpty();

        _ = client.DidNotReceive().GetTenantUsersAsync(
            Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        _ = client.DidNotReceive().GetUserTenantsAsync(
            Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        _ = client.DidNotReceive().GetTenantAuditAsync(
            Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        logger.Messages.ShouldBeEmpty("A malformed request is not a read failure and must not be logged as one.");
    }

    /// <summary>
    /// A first-page load that never paged must not be announced as a paging recovery.
    /// </summary>
    /// <remarks>
    /// The recovery path stamped <c>ListRefreshed</c> on any invalid-cursor failure, including a page-one
    /// load carrying no cursor at all -- telling the operator that paging had restarted when nothing had
    /// paged. Making the <c>!IsNullOrWhiteSpace(request.Cursor)</c> guard unable to fire survived the suite.
    /// </remarks>
    [Fact]
    public async Task Invalid_cursor_recovery_does_not_announce_a_restart_on_a_first_page_load()
    {
        int reads = 0;
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads++;
                return FailureResponse<PaginatedResult<TenantSummary>>(
                    TenantsRestQueryFailureKind.InvalidCursor,
                    (int)HttpStatusCode.BadRequest);
            });

        TenantListSnapshot snapshot = await CreateGateway(client).ListTenantsAsync(
            new TenantListRequest(),
            previous: null,
            CancellationToken.None);

        snapshot.Notice.ShouldNotBe(TenantListReason.ListRefreshed);
        reads.ShouldBe(1, "There was no cursor to recover from, so no page-one retry may be issued.");
    }

    /// <summary>
    /// Search hydration must contain every failure the typed client can raise for an index-supplied id.
    /// </summary>
    /// <remarks>
    /// Index hits are untrusted input: a <c>tenant:   </c> hit passed the candidate filter, which rejected
    /// null, empty and control characters but not whitespace, and hard-threw inside the typed client.
    /// Uncontained, that escapes <c>Task.WhenAll</c> through the gateway into <c>TenantsWorkspace</c> and
    /// tears down the circuit. Both the widened containment set and the whitespace filter survived their
    /// mutations.
    /// </remarks>
    [Theory]
    [InlineData("tenant:   ")]
    [InlineData("tenant:\t")]
    public async Task Search_hydration_contains_a_whitespace_only_index_candidate(string sourceUri)
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("alpha", 1, Hit(sourceUri)));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<TenantDetail>>(_ => throw new ArgumentException("blank tenant id"));
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>(
                [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
                Cursor: null,
                HasMore: false)));

        // No throw: the malformed hit is dropped before it can reach the typed client at all.
        TenantListSnapshot snapshot = await CreateGateway(client, memoriesClient: memories).ListTenantsAsync(
            new TenantListRequest(Search: "alpha"),
            previous: null,
            CancellationToken.None);

        _ = client.DidNotReceive().GetTenantAsync(
            Arg.Any<GetTenantQuery>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        // The remaining match set is empty, and an empty authoritative match set is still authoritative --
        // the point is that a hostile index value neither reaches the client nor escapes as a circuit fault.
        snapshot.IsAuthoritativeSearch.ShouldBeTrue();
        snapshot.Rows.ShouldBeEmpty();
    }

    /// <summary>
    /// A response the client could not validate is an outage, not a successful non-error status.
    /// </summary>
    /// <remarks>
    /// <c>InvalidPayload</c> carries the raw 200, <c>InvalidMetadata</c> the raw 304, and <c>Timeout</c> and
    /// <c>Unknown</c> carry whatever the transport left behind -- none of them a status any mapper models.
    /// Without the override they reached the per-read mappers as non-error statuses and fell to their default
    /// arms: the detail mapper's is <c>Degraded</c> with no rows, and the global-administrator and audit
    /// mappers' is <c>Error</c>. On a first load "degraded" claims retained evidence that does not exist, and
    /// "error" claims the server failed when in fact the client rejected the response. Removing the three
    /// arms survived the suite. Normalizing to 503 makes all four read as the outage they are.
    /// </remarks>
    [Theory]
    [InlineData(TenantsRestQueryFailureKind.InvalidPayload, (int)HttpStatusCode.OK)]
    [InlineData(TenantsRestQueryFailureKind.InvalidMetadata, (int)HttpStatusCode.NotModified)]
    [InlineData(TenantsRestQueryFailureKind.Unknown, (int)HttpStatusCode.OK)]
    [InlineData(TenantsRestQueryFailureKind.Timeout, (int)HttpStatusCode.OK)]
    public async Task A_corrupt_response_is_an_outage_on_a_first_load(
        TenantsRestQueryFailureKind failureKind,
        int rawStatusCode)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<GlobalAdministratorSummary>>(failureKind, rawStatusCode));
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<TenantDetail>(failureKind, rawStatusCode));

        TenantQueryGateway gateway = CreateGateway(client);

        GlobalAdministratorsSnapshot administrators = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);

        // Not Error: the server did not fail, the response could not be validated.
        administrators.Kind.ShouldBe(GlobalAdministratorsSurfaceKind.Unavailable);
        administrators.IsCompleteEvidence.ShouldBeFalse();

        TenantDetailSnapshot detail = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);

        // Not Degraded: there is nothing retained for a first load to be degraded from.
        detail.Kind.ShouldBe(TenantDetailSurfaceKind.Unavailable);
    }

    /// <summary>
    /// Clearing the search box must not re-present the search-filtered subset as the authorized list.
    /// </summary>
    /// <remarks>
    /// Search snapshots carry no <c>RequestCursor</c>/<c>RequestPageSize</c>, so they default to
    /// <c>(null, 20)</c> -- the exact shape where <c>MatchesPageScope</c> and <c>MatchesRetainedValidator</c>
    /// both pass. Retaining one as the ordinary previous therefore carried the search page's rows,
    /// <c>HasMore</c>, protected search cursor and <c>IsAuthoritativeSearch</c> onto the unfiltered surface.
    /// Replacing the guard with <c>previous</c> survived the suite: no test drove a search snapshot into a
    /// cleared-search read at page one and the default page size.
    /// </remarks>
    [Fact]
    public async Task Clearing_the_search_does_not_retain_the_search_snapshot_as_the_authorized_list()
    {
        StubMemoriesClient memories = new();
        memories.Enqueue(SearchResult("alpha", 1, Hit("tenant:tenant.alpha")));
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(Detail("tenant.alpha")));

        // The ordinary list read fails, so anything rendered afterwards can only have come from retention.
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantSummary>>(TenantsRestQueryFailureKind.Unavailable));

        TenantQueryGateway gateway = CreateGateway(client, memoriesClient: memories);
        TenantListSnapshot searched = await gateway.ListTenantsAsync(
            new TenantListRequest(Search: "alpha"),
            previous: null,
            CancellationToken.None);
        searched.IsAuthoritativeSearch.ShouldBeTrue();
        searched.Rows.ShouldNotBeEmpty();

        TenantListSnapshot cleared = await gateway.ListTenantsAsync(
            new TenantListRequest(),
            previous: searched,
            CancellationToken.None);

        cleared.IsAuthoritativeSearch.ShouldBeFalse();
        cleared.Rows.ShouldBeEmpty("The search-filtered subset must never become the authorized list.");
        cleared.HasMore.ShouldBeFalse();
        cleared.NextCursor.ShouldBeNull();
    }

    /// <summary>
    /// The detail read must not send one tenant's retained validator on another tenant's request.
    /// </summary>
    /// <remarks>
    /// Reverting the tenant-identity clause to <c>request.ETag ?? previous?.ETag</c> survived the suite. The
    /// tenant-users equivalent is pinned; the detail read had no counterpart, so after a route change tenant
    /// alpha's projection-wide ETag went out as <c>If-None-Match</c> on tenant beta's read.
    /// </remarks>
    [Fact]
    public async Task Get_tenant_does_not_reuse_another_tenants_retained_validator()
    {
        List<(string TenantId, string? ETag)> requests = [];
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.ArgAt<GetTenantQuery>(0).TenantId;
                requests.Add((tenantId, call.ArgAt<string?>(1)));
                return DirectResponse(Detail(tenantId), eTag: $"\"{tenantId}-etag\"");
            });

        TenantQueryGateway gateway = CreateGateway(client);
        TenantDetailSnapshot alpha = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);
        alpha.ETag.ShouldNotBeNullOrWhiteSpace();

        _ = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.beta"),
            previous: alpha,
            CancellationToken.None);

        requests.Count.ShouldBe(2);
        requests[1].TenantId.ShouldBe("tenant.beta");
        requests[1].ETag.ShouldBeNull("Tenant alpha's validator must not be sent on tenant beta's read.");

        // ...while the same tenant still gets its conditional read.
        _ = await gateway.GetTenantAsync(
            new TenantDetailRequest("tenant.alpha"),
            previous: alpha,
            CancellationToken.None);
        requests[2].TenantId.ShouldBe("tenant.alpha");
        requests[2].ETag.ShouldBe(alpha.ETag);
    }

    /// <summary>
    /// A stale 304 proves the payload is unchanged, not that it is current at a newer projection version.
    /// </summary>
    /// <remarks>
    /// <c>IsSupportedNotModified</c> accepts <c>Current</c> or <c>Stale</c>, but only the Current arm was
    /// tested, so reverting to an unconditional <c>ProjectionVersion = result.Metadata?.ProjectionVersion</c>
    /// survived. That version is what both mutation gates and <c>IsCompleteEvidence</c> are read against.
    /// </remarks>
    [Fact]
    public async Task Get_global_administrators_stale_not_modified_retains_the_asserted_projection_version()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(1) is null
                ? DirectResponse(
                    new PaginatedResult<GlobalAdministratorSummary>(
                        [new GlobalAdministratorSummary("admin.alpha")],
                        Cursor: null,
                        HasMore: false),
                    eTag: "\"ga-etag\"")
                : new TenantsRestQueryResponse<PaginatedResult<GlobalAdministratorSummary>>(
                    default,
                    ProjectionBackedMetadata(
                        isStale: true,
                        eTag: call.ArgAt<string?>(1),
                        isNotModified: true,
                        lifecycle: ProjectionLifecycleState.Stale,
                        projectionVersion: "projection-v9"),
                    TenantsRestQueryFailureKind.None,
                    (int)HttpStatusCode.NotModified));

        TenantQueryGateway gateway = CreateGateway(client);
        GlobalAdministratorsSnapshot confirmed = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(),
            previous: null,
            CancellationToken.None);
        confirmed.ProjectionVersion.ShouldBe("projection-v1");

        GlobalAdministratorsSnapshot staleNotModified = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(ETag: confirmed.ETag),
            previous: confirmed,
            CancellationToken.None);

        staleNotModified.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
        staleNotModified.ProjectionVersion.ShouldBe(
            "projection-v1",
            "A stale 304 must not advance the version the mutation gates read.");
        staleNotModified.IsCompleteEvidence.ShouldBeFalse();
    }

    /// <summary>
    /// Supplementary list enrichment runs concurrently, is whole-page time-bounded, and refuses a
    /// cross-tenant payload.
    /// </summary>
    /// <remarks>
    /// Three mutations survived the suite: deleting <c>CancelAfter</c> (the whole-page deadline), dropping
    /// <c>MaximumHydrationConcurrency</c> to 1 (restoring the serialized behaviour the rewrite exists to
    /// fix), and neutering the identity comparison that stops tenant B's member and owner counts being
    /// written onto tenant A's row. <c>MaximumHydrationConcurrency</c> was asserted only as a constant value
    /// and the deadline was referenced by no test at all.
    /// </remarks>
    [Fact]
    public async Task List_row_enrichment_runs_concurrently_up_to_its_bound()
    {
        const int expectedBound = 8;
        const int rows = expectedBound * 2;
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>(
                [.. Enumerable.Range(0, rows).Select(index =>
                    new TenantSummary($"tenant.{index}", $"Tenant {index}", TenantStatus.Active))],
                Cursor: null,
                HasMore: false)));

        int active = 0;
        int maximum = 0;
        int arrivals = 0;

        // A rendezvous rather than a fixed delay. The delay made both directions non-deterministic: under
        // scheduler starvation `maximum` could legitimately fall to 1 and fail a healthy build, while a
        // bound reduced from 8 to 2 still satisfied `> 1` and passed. Here the first `expectedBound` calls
        // block until all of them are simultaneously inside, so a correct implementation observes exactly
        // the bound and a serialized one never releases -- and is reported as a timeout, not a flake.
        TaskCompletionSource boundReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                string tenantId = call.ArgAt<GetTenantQuery>(0).TenantId;
                CancellationToken cancellationToken = call.ArgAt<CancellationToken>(2);
                ObserveMaximum(ref maximum, Interlocked.Increment(ref active));
                try
                {
                    if (Interlocked.Increment(ref arrivals) >= expectedBound)
                    {
                        _ = boundReached.TrySetResult();
                    }

                    // Bounded well under the gateway's own whole-page enrichment deadline so a genuine
                    // failure surfaces here rather than as an unrelated deadline cancellation.
                    await boundReached.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    return DirectResponse(Detail(tenantId));
                }
                finally
                {
                    _ = Interlocked.Decrement(ref active);
                }
            });

        TenantListSnapshot snapshot = await CreateGateway(client).ListTenantsAsync(
            new TenantListRequest(),
            previous: null,
            CancellationToken.None);

        snapshot.Rows.Count.ShouldBe(rows);
        snapshot.IsDegraded.ShouldBeFalse();

        // Exact, both directions: the rendezvous cannot release below the bound, and the gate must not let
        // more than the bound run at once. `ShouldBeGreaterThan(1)` passed with the bound cut from 8 to 2.
        TenantQueryGateway.MaximumHydrationConcurrency.ShouldBe(expectedBound);
        maximum.ShouldBe(expectedBound);
    }

    [Fact]
    public async Task List_row_enrichment_is_bounded_by_a_whole_page_deadline()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>(
                [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
                Cursor: null,
                HasMore: false)));

        // Never completes on its own: only the enrichment deadline can end this read.
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.ArgAt<CancellationToken>(2));
                return DirectResponse(Detail("tenant.alpha"));
            });

        // WaitAsync, not a bare await: without the deadline this read never returns, and a hanging test is
        // an unusable signal.
        TenantListSnapshot snapshot = await CreateGateway(
                client,
                enrichmentDeadline: TimeSpan.FromMilliseconds(50))
            .ListTenantsAsync(new TenantListRequest(), previous: null, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        // The page still renders: the deadline degrades supplementary evidence, it does not fail the read.
        snapshot.Rows.ShouldHaveSingleItem().TenantId.ShouldBe("tenant.alpha");
        snapshot.IsDegraded.ShouldBeTrue();

        // The injected 50 ms deadline proves the mechanism; it does not prove the value users actually get.
        // MaximumEnrichmentDuration had no reference outside its declaration and single use, so setting it
        // to Timeout.InfiniteTimeSpan reinstated the unbounded page hang this test claims to prevent and
        // everything still passed. Pin the shipped default to a bound a user could tolerate waiting.
        TenantQueryGateway.MaximumEnrichmentDuration.ShouldBeGreaterThan(TimeSpan.Zero);
        TenantQueryGateway.MaximumEnrichmentDuration.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task List_row_enrichment_refuses_counts_from_a_mismatched_tenant_payload()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>(
                [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
                Cursor: null,
                HasMore: false)));

        // A cache or proxy mix-up, a routing defect, or route confusion: the detail payload is a different
        // tenant's. Its counts must never be written onto the row whose identity came from the list summary.
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new TenantDetail(
                "tenant.beta",
                "Beta",
                null,
                TenantStatus.Active,
                [
                    new TenantMember("beta.owner", TenantRole.TenantOwner),
                    new TenantMember("beta.reader", TenantRole.TenantReader),
                ],
                new Dictionary<string, string>(StringComparer.Ordinal),
                DateTimeOffset.Parse("2026-07-28T08:00:00Z", CultureInfo.InvariantCulture))));

        TenantListSnapshot snapshot = await CreateGateway(client).ListTenantsAsync(
            new TenantListRequest(),
            previous: null,
            CancellationToken.None);

        TenantListRow row = snapshot.Rows.ShouldHaveSingleItem();
        row.TenantId.ShouldBe("tenant.alpha");
        row.MemberCount.IsKnown.ShouldBeFalse();
        row.OwnerCount.IsKnown.ShouldBeFalse();
        snapshot.IsDegraded.ShouldBeTrue();
    }

    /// <summary>
    /// An out-of-range page size is clamped, not silently reset to the default.
    /// </summary>
    /// <remarks>
    /// <c>NormalizePageSize</c> returned the default for anything outside <c>[1, MaximumPageSize]</c>, so a
    /// request for one row over the maximum was served as 20 -- a five-fold reduction with no signal, on all
    /// five paged reads now routed through it. Reducing the whole method to the identity function survived
    /// the suite, so neither the normalization nor the list-path clamp it was factored out of was pinned.
    /// Zero and negative sizes carry no intent, so they still fall to the default.
    /// </remarks>
    [Theory]
    [InlineData(101, 100)]
    [InlineData(1000, 100)]
    [InlineData(100, 100)]
    [InlineData(1, 1)]
    [InlineData(37, 37)]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    public async Task Paged_reads_clamp_an_out_of_range_page_size(int requested, int expected)
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        List<int> tenantListPageSizes = [];
        List<int> tenantUsersPageSizes = [];
        List<int> userTenantsPageSizes = [];
        List<int> tenantAuditPageSizes = [];
        List<int> globalAdministratorPageSizes = [];
        client.ListTenantsAsync(
                Arg.Any<ListTenantsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                tenantListPageSizes.Add(call.ArgAt<ListTenantsQuery>(0).PageSize);
                return DirectResponse(new PaginatedResult<TenantSummary>([], Cursor: null, HasMore: false));
            });
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                tenantUsersPageSizes.Add(call.ArgAt<GetTenantUsersQuery>(0).PageSize);
                return DirectResponse(new PaginatedResult<TenantMember>([], Cursor: null, HasMore: false));
            });
        client.GetUserTenantsAsync(
                Arg.Any<GetUserTenantsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                userTenantsPageSizes.Add(call.ArgAt<GetUserTenantsQuery>(0).PageSize);
                return DirectResponse(new PaginatedResult<UserTenantMembership>([], Cursor: null, HasMore: false));
            });
        client.GetTenantAuditAsync(
                Arg.Any<GetTenantAuditQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                tenantAuditPageSizes.Add(call.ArgAt<GetTenantAuditQuery>(0).PageSize);
                return DirectResponse(new PaginatedResult<TenantAuditEntry>([], Cursor: null, HasMore: false));
            });
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                globalAdministratorPageSizes.Add(call.ArgAt<GetGlobalAdministratorsQuery>(0).PageSize);
                return DirectResponse(new PaginatedResult<GlobalAdministratorSummary>([], Cursor: null, HasMore: false));
            });

        TenantQueryGateway gateway = CreateGateway(client);
        _ = await gateway.ListTenantsAsync(
            new TenantListRequest(PageSize: requested),
            previous: null,
            CancellationToken.None);
        _ = await gateway.GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", PageSize: requested),
            previous: null,
            CancellationToken.None);
        _ = await gateway.GetUserTenantsAsync(
            new UserTenantMembershipRequest("target.user", PageSize: requested),
            previous: null,
            CancellationToken.None);
        _ = await gateway.GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha", PageSize: requested),
            previous: null,
            CancellationToken.None);
        _ = await gateway.GetGlobalAdministratorsAsync(
            new GlobalAdministratorsRequest(PageSize: requested),
            previous: null,
            CancellationToken.None);

        tenantListPageSizes.ShouldHaveSingleItem().ShouldBe(expected);
        tenantUsersPageSizes.ShouldHaveSingleItem().ShouldBe(expected);
        userTenantsPageSizes.ShouldHaveSingleItem().ShouldBe(expected);
        tenantAuditPageSizes.ShouldHaveSingleItem().ShouldBe(expected);
        globalAdministratorPageSizes.ShouldHaveSingleItem().ShouldBe(expected);
    }

    /// <summary>
    /// A defect in row mapping must not be presented as "row enrichment unavailable" on every row.
    /// </summary>
    /// <remarks>
    /// The per-row catch had no <c>IsSurfacingDefect</c> exclusion, unlike the search hydration path it
    /// mirrors, so a <c>NullReferenceException</c> raised while mapping a row was folded into the degraded
    /// flag: the page rendered with rows and a permanent "enrichment unavailable" qualifier, forever,
    /// indistinguishable from the Tenants API being slow and with no diagnostic. Excluding surfacing defects
    /// lets the read's own last-resort handler take it, which is what emits a read-failure diagnostic and
    /// resolves the surface to Error. It is still contained -- an escaping defect would tear down the
    /// circuit -- but it is no longer silent, and it no longer masquerades as a usable page.
    /// </remarks>
    [Fact]
    public async Task List_row_enrichment_does_not_swallow_a_surfacing_defect()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantSummary>(
                [new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)],
                Cursor: null,
                HasMore: false)));
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TenantsRestQueryResponse<TenantDetail>>(_ => throw new NullReferenceException("row mapping defect"));
        CapturingLogger logger = new();

        TenantListSnapshot snapshot = await CreateGateway(client, logger: logger).ListTenantsAsync(
            new TenantListRequest(),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantListSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
        logger.Messages.ShouldNotBeEmpty("A defect must leave a diagnostic, not be folded into the row state.");
        string logged = string.Join(" ", logger.Messages);
        logged.ShouldNotContain("row mapping defect");
    }

    /// <summary>
    /// A member page-one recovery must be visible to the page that has to reset its cursor.
    /// </summary>
    /// <remarks>
    /// The recovery replaced the expired cursor with page one but stamped the result
    /// <c>Reason = ListRefreshed</c>, <c>RequestCursor = null</c>, <c>PagingRecovered = false</c>. The detail
    /// page branches on <c>PagingRecovered</c> and on an explicit <c>InvalidCursor</c> reason, so neither
    /// fired: it committed the dead cursor as the current page, enabled Previous on a page-one view, and
    /// re-sent the dead cursor on every later refresh. The four sibling paged reads all signal the recovery.
    /// </remarks>
    [Fact]
    public async Task Get_tenant_users_signals_a_page_one_recovery_the_page_can_act_on()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantUsersAsync(
                Arg.Any<GetTenantUsersQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<GetTenantUsersQuery>(0).Cursor is null
                ? DirectResponse(new PaginatedResult<TenantMember>(
                    [new TenantMember("owner.user", TenantRole.TenantOwner)],
                    Cursor: null,
                    HasMore: false))
                : FailureResponse<PaginatedResult<TenantMember>>(
                    TenantsRestQueryFailureKind.InvalidCursor,
                    (int)HttpStatusCode.BadRequest));

        TenantUsersSnapshot snapshot = await CreateGateway(client).GetTenantUsersAsync(
            new TenantUsersRequest("tenant.alpha", Cursor: "expired-cursor"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantUsersSurfaceKind.Ready);
        snapshot.Reason.ShouldBe(TenantUsersReason.ListRefreshed);
        snapshot.PagingRecovered.ShouldBeTrue();
        snapshot.RequestCursor.ShouldBeNull();
        snapshot.Rows.ShouldHaveSingleItem().UserId.ShouldBe("owner.user");
        _ = client.Received(1).GetTenantUsersAsync(
            Arg.Is<GetTenantUsersQuery>(query => query != null && query.Cursor == null),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_tenant_audit_rejects_a_cross_tenant_payload_at_the_gateway_boundary()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.GetTenantAuditAsync(
                Arg.Any<GetTenantAuditQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(DirectResponse(new PaginatedResult<TenantAuditEntry>(
                [new TenantAuditEntry(
                    "event-1",
                    "TenantUpdated",
                    AuditEventCategory.Administrative,
                    "actor.user",
                    DateTimeOffset.Parse("2026-07-28T08:00:00Z", CultureInfo.InvariantCulture),
                    "tenant.beta",
                    new Dictionary<string, string>())],
                Cursor: null,
                HasMore: false)));

        TenantAuditSnapshot snapshot = await CreateGateway(client).GetTenantAuditAsync(
            new TenantAuditRequest("tenant.alpha"),
            previous: null,
            CancellationToken.None);

        snapshot.Kind.ShouldBe(TenantAuditSurfaceKind.Error);
        snapshot.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_direct_read_failure_result_emits_only_bounded_read_and_failure_categories()
    {
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantSummary>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<TenantDetail>(TenantsRestQueryFailureKind.Unavailable));
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantMember>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<UserTenantMembership>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<TenantAuditEntry>>(TenantsRestQueryFailureKind.Unavailable));
        client.GetGlobalAdministratorsAsync(
                Arg.Any<GetGlobalAdministratorsQuery>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(FailureResponse<PaginatedResult<GlobalAdministratorSummary>>(TenantsRestQueryFailureKind.Unavailable));
        CapturingLogger logger = new();
        TenantQueryGateway gateway = CreateGateway(client, logger: logger);

        _ = await gateway.ListTenantsAsync(new TenantListRequest(), null, CancellationToken.None);
        _ = await gateway.GetTenantAsync(new TenantDetailRequest("tenant.alpha"), null, CancellationToken.None);
        _ = await gateway.GetTenantUsersAsync(new TenantUsersRequest("tenant.alpha"), null, CancellationToken.None);
        _ = await gateway.GetMyTenantsAsync(new UserTenantMembershipRequest(), null, CancellationToken.None);
        _ = await gateway.GetTenantAuditAsync(new TenantAuditRequest("tenant.alpha"), null, CancellationToken.None);
        _ = await gateway.GetGlobalAdministratorsAsync(new GlobalAdministratorsRequest(), null, CancellationToken.None);

        logger.Events.Count(eventId => eventId == TenantQueryGateway.DirectTenantsReadFailedEvent).ShouldBe(6);
        foreach (string readName in new[]
        {
            TenantQueryGateway.TenantListReadName,
            TenantQueryGateway.TenantDetailReadName,
            TenantQueryGateway.TenantUsersReadName,
            TenantQueryGateway.UserTenantsReadName,
            TenantQueryGateway.TenantAuditReadName,
            TenantQueryGateway.GlobalAdministratorsReadName,
        })
        {
            logger.Messages.ShouldContain(message => message.Contains(readName, StringComparison.Ordinal));
        }

        ShouldNotDisclose(logger, "tenant.alpha", "target.user", "cursor", "etag");
    }

    private static QueryResponseMetadata ProjectionBackedMetadata(
        bool? isStale = null,
        bool? isDegraded = null,
        DateTimeOffset? servedAt = null,
        string? eTag = null,
        bool? isNotModified = null,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Unknown,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        string? projectionVersion = null)
        => new(eTag, isNotModified, isStale, isDegraded, ProjectionVersion: projectionVersion, ServedAt: servedAt)
        {
            Provenance = provenance,
            Lifecycle = lifecycle,
        };

    private static TenantsRestQueryResponse<TPayload> DirectResponse<TPayload>(
        TPayload payload,
        string? eTag = null,
        string projectionVersion = "projection-v1")
        => new(
            payload,
            ProjectionBackedMetadata(
                isStale: false,
                eTag: eTag,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: projectionVersion),
            TenantsRestQueryFailureKind.None,
            (int)HttpStatusCode.OK);

    private static TenantsRestQueryResponse<TPayload> NotModifiedResponse<TPayload>(
        string eTag,
        string projectionVersion = "projection-v2")
        => new(
            default,
            ProjectionBackedMetadata(
                isStale: false,
                eTag: eTag,
                isNotModified: true,
                lifecycle: ProjectionLifecycleState.Current,
                projectionVersion: projectionVersion),
            TenantsRestQueryFailureKind.None,
            (int)HttpStatusCode.NotModified);

    private static TenantsRestQueryResponse<TPayload> FailureResponse<TPayload>(
        TenantsRestQueryFailureKind failureKind,
        int statusCode = (int)HttpStatusCode.ServiceUnavailable)
        => new(
            default,
            new QueryResponseMetadata(),
            failureKind,
            statusCode);

    private static MemoriesSearchResult SearchResult(
        string query,
        long totalCount,
        params MemoriesScoredResult[] results)
        => new()
        {
            Query = query,
            TotalCount = totalCount,
            HasIndexedMemoryUnits = totalCount > 0,
            Results = results,
            AxesUsed = ["syntactic"],
        };

    private static MemoriesScoredResult Hit(string sourceUri)
        => new()
        {
            MemoryUnitId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            Score = 1,
            ContentSnippet = "index-only content that must never render",
            SourceUri = sourceUri,
            SourceType = MemoriesSourceType.Projection,
            Axis = "syntactic",
        };

    // Native fixtures include deliberately malformed metadata for gateway defense tests.
    // HTTP parsing, body redaction and supported-304 validation belong to TenantsRestQueryClientTests.
    private static ITenantsRestQueryClient CreateReadClient(out Queue<object> responses)
    {
        responses = new Queue<object>();
        Queue<object> queue = responses;
        ITenantsRestQueryClient client = Substitute.For<ITenantsRestQueryClient>();
        client.ListTenantsAsync(Arg.Any<ListTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<PaginatedResult<TenantSummary>>(queue));
        client.GetTenantAsync(Arg.Any<GetTenantQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<TenantDetail>(queue));
        client.GetTenantUsersAsync(Arg.Any<GetTenantUsersQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<PaginatedResult<TenantMember>>(queue));
        client.GetUserTenantsAsync(Arg.Any<GetUserTenantsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<PaginatedResult<UserTenantMembership>>(queue));
        client.GetTenantAuditAsync(Arg.Any<GetTenantAuditQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<PaginatedResult<TenantAuditEntry>>(queue));
        client.GetGlobalAdministratorsAsync(Arg.Any<GetGlobalAdministratorsQuery>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => NextReadResponse<PaginatedResult<GlobalAdministratorSummary>>(queue));
        return client;
    }

    private static IReadOnlyList<NSubstitute.Core.ICall> ReadCalls(ITenantsRestQueryClient client)
        => client.ReceivedCalls().ToArray();

    private static TenantsRestQueryResponse<T> NextReadResponse<T>(Queue<object> responses)
    {
        object next = responses.Dequeue();
        if (next is Exception exception)
        {
            throw exception;
        }
        if (next is ValueTuple<TenantsRestQueryFailureKind, int> failure)
        {
            return new(default, new QueryResponseMetadata(), failure.Item1, failure.Item2);
        }
        return (TenantsRestQueryResponse<T>)next;
    }

    private static void EnqueueFailure(Queue<object> responses, TenantsRestQueryFailureKind kind, int statusCode)
        => responses.Enqueue((kind, statusCode));

    private static void EnqueueException(Queue<object> responses, Exception exception)
        => responses.Enqueue(exception);

    private static void EnqueueQueryResult<T>(
        Queue<object> responses,
        T payload,
        string? eTag = "etag",
        QueryResponseMetadata? metadata = null,
        bool emitDefaultMetadata = true)
        => responses.Enqueue(new TenantsRestQueryResponse<T>(
            payload,
            (metadata ?? (emitDefaultMetadata ? ProjectionBackedMetadata(eTag: eTag, isStale: false) : new QueryResponseMetadata())) with
            {
                ETag = eTag ?? metadata?.ETag,
                IsNotModified = false,
            },
            TenantsRestQueryFailureKind.None,
            (int)HttpStatusCode.OK));

    private static void EnqueueDetailResult(Queue<object> responses, TenantDetail? payload, QueryResponseMetadata? metadata = null)
        => EnqueueQueryResult(responses, payload, eTag: metadata?.ETag, metadata: metadata, emitDefaultMetadata: false);

    private static void EnqueueNotModified(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        bool emitMetadata = true)
        => EnqueueNotModified<PaginatedResult<TenantSummary>>(responses, eTag, isStale, lifecycle, provenance, emitMetadata);

    private static void EnqueueDetailNotModified(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current)
        => EnqueueNotModified<TenantDetail>(responses, eTag, isStale, lifecycle);

    private static void EnqueueUserTenantsNotModified(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        bool emitMetadata = true)
        => EnqueueNotModified<PaginatedResult<UserTenantMembership>>(responses, eTag, isStale, lifecycle, provenance, emitMetadata);

    private static void EnqueueTenantUsersNotModified(Queue<object> responses, string eTag, string projectionVersion)
        => EnqueueNotModified<PaginatedResult<TenantMember>>(responses, eTag, projectionVersion: projectionVersion);

    private static void EnqueueGlobalAdministratorsNotModified(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        bool emitMetadata = true,
        string? projectionVersion = "projection-v1")
        => EnqueueNotModified<PaginatedResult<GlobalAdministratorSummary>>(responses, eTag, isStale, lifecycle, provenance, emitMetadata, projectionVersion);

    private static void EnqueueAuditNotModified(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        bool emitMetadata = true)
        => EnqueueNotModified<PaginatedResult<TenantAuditEntry>>(responses, eTag, isStale, lifecycle, provenance, emitMetadata);

    private static void EnqueueNotModified<T>(
        Queue<object> responses,
        string? eTag,
        bool? isStale = false,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        QueryResponseProvenance provenance = QueryResponseProvenance.ProjectionBacked,
        bool emitMetadata = true,
        string? projectionVersion = "projection-v1")
        => responses.Enqueue(new TenantsRestQueryResponse<T>(
            default,
            (emitMetadata
                ? ProjectionBackedMetadata(eTag: eTag, isNotModified: true, isStale: isStale,
                    lifecycle: lifecycle, provenance: provenance, projectionVersion: projectionVersion)
                : new QueryResponseMetadata()) with
            { ETag = eTag, IsNotModified = true },
            TenantsRestQueryFailureKind.None,
            (int)HttpStatusCode.NotModified));

    private sealed class StubMemoriesClient : MemoriesClient
    {
        private readonly Queue<object?> _responses = new();

        public StubMemoriesClient()
            : base(
                new HttpClient { BaseAddress = new Uri("https://memories.invalid") },
                Options.Create(new MemoriesClientOptions()),
                NullLogger<MemoriesClient>.Instance)
        {
        }

        public List<SearchRequest> SearchRequests { get; } = [];

        public void Enqueue(MemoriesSearchResult result)
            => _responses.Enqueue(result);

        public void Enqueue(Task<MemoriesSearchResult> result)
            => _responses.Enqueue(result);

        public void EnqueueNullable(MemoriesSearchResult? result)
            => _responses.Enqueue(result);

        public void Enqueue(Exception exception)
            => _responses.Enqueue(exception);

        public override Task<MemoriesSearchResult> SearchAsync(SearchRequest request, CancellationToken ct)
        {
            SearchRequests.Add(request);
            if (_responses.Count == 0)
            {
                throw new HttpRequestException("Memories unavailable.");
            }

            object? response = _responses.Dequeue();
            if (response is Exception exception)
            {
                throw exception;
            }

            return response is Task<MemoriesSearchResult> pending ? pending : Task.FromResult((MemoriesSearchResult)response!);
        }
    }

    /// <summary>
    /// A codec double whose decode/encode failures can be any exception type, so containment is proven for
    /// the whole family instead of one representative type.
    /// </summary>
    private sealed class ThrowingSearchCursorCodec(
        Func<Exception>? decodeFailure = null,
        Func<Exception>? encodeFailure = null,
        int? failedDecodeOffset = null)
        : ITenantSearchCursorCodec
    {
        public string? DecodedScope { get; private set; }

        public string Encode(string scope, int offset)
            => encodeFailure is null
                ? offset.ToString(CultureInfo.InvariantCulture)
                : throw encodeFailure();

        public bool TryDecode(string? cursor, string scope, out int offset)
        {
            DecodedScope = scope;
            if (decodeFailure is not null)
            {
                throw decodeFailure();
            }

            if (failedDecodeOffset is int unsafeOffset)
            {
                offset = unsafeOffset;
                return false;
            }

            offset = 0;
            return true;
        }
    }

    /// <summary>
    /// Asserts no captured diagnostic discloses <paramref name="forbidden"/> over any channel a support-facing
    /// sink renders. The sink must additionally have received no exception object at all: the gateway's
    /// diagnostics are reason codes by design, and the raw query, offset and cursor material this story
    /// forbids lives in the messages of the exceptions it catches, so attaching one is how the guarantee gets
    /// undone. Asserting only over <c>Messages</c> could not observe that, because the default message
    /// formatter drops the exception argument.
    /// </summary>
    private static void ShouldNotDisclose(CapturingLogger logger, params string[] forbidden)
    {
        logger.Exceptions.ShouldAllBe(static exception => exception == null);
        foreach (string disclosure in logger.Disclosures)
        {
            foreach (string secret in forbidden)
            {
                disclosure.ShouldNotContain(secret, Case.Sensitive);
            }
        }
    }

    /// <summary>Captures the gateway's structured diagnostics so disclosure is actually observable.</summary>
    private sealed class CapturingLogger : ILogger<TenantQueryGateway>
    {
        public List<string> Messages { get; } = [];

        public List<EventId> Events { get; } = [];

        /// <summary>Gets the exception argument of each captured entry, including the nulls.</summary>
        public List<Exception?> Exceptions { get; } = [];

        /// <summary>
        /// Gets every string a support-facing sink renders for the captured entries. The formatted message is
        /// only one of them: the default message formatter ignores the exception argument entirely, so a
        /// non-disclosure assertion made against <see cref="Messages"/> alone cannot observe a raw query,
        /// offset or cursor that reached the sink inside an exception -- which is precisely where such a
        /// regression lands, because attaching the caught exception reads like better logging.
        /// </summary>
        public IEnumerable<string> Disclosures
            => Messages.Concat(Exceptions
                .Where(static exception => exception is not null)
                .Select(static exception => exception!.ToString()));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Events.Add(eventId);
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    private sealed class StubConfigurationPrincipalResolver(TenantConfigurationPrincipalEvidence evidence)
        : ITenantConfigurationPrincipalResolver
    {
        public ValueTask<TenantConfigurationPrincipalEvidence> ResolveAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(evidence);
    }

    private sealed class ThrowingPrincipalResolver : ITenantConfigurationPrincipalResolver
    {
        public ValueTask<TenantConfigurationPrincipalEvidence> ResolveAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<TenantConfigurationPrincipalEvidence>(
                new InvalidOperationException("raw principal failure"));
    }


    private static TenantDetail Detail(string tenantId)
        => Detail(
            tenantId,
            new Dictionary<string, string>
            {
                ["billing.mode"] = "trial",
            });

    private static TenantDetail Detail(string tenantId, IReadOnlyDictionary<string, string> configuration)
        => new(
            tenantId,
            "Alpha",
            "Tenant alpha description",
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ],
            configuration,
            DateTimeOffset.UtcNow);

    private static TenantAuditEntry AuditEntry(
        string eventId,
        AuditEventCategory category,
        DateTimeOffset? timestamp = null,
        string? eventType = null,
        string tenantId = "tenant.alpha",
        IReadOnlyDictionary<string, string>? narrative = null)
        => new(
            eventId,
            eventType ?? (category is AuditEventCategory.Access ? "UserAddedToTenant" : "TenantConfigurationSet"),
            category,
            "actor-user",
            timestamp ?? DateTimeOffset.UtcNow,
            tenantId,
            narrative ?? new Dictionary<string, string>
            {
                ["userId"] = "target-user",
                ["key"] = "billing.mode",
                ["role"] = "TenantReader",
            });
}
