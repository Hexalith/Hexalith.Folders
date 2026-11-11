using System.Net;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Records the blocked public transport and preserves unavailable versus proven-zero consumer semantics.</summary>
[Collection("Source head boundary")]
public sealed class CurrentSdkSourceHeadBoundaryTests
{
    /// <summary>Ordinary source-client credentials cannot issue the SDK's required tenant/domain-bound assertion.</summary>
    /// <param name="caller">The independently enrolled source-reader workload.</param>
    [Theory]
    [InlineData("folders")]
    [InlineData("folders-workers")]
    public async Task OrdinaryAuthorityCredentialsRefuseBoundSourceReadsBeforeAnyTransport(string caller)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        using RSA key = RSA.Create(2048);
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/hexalith",
            ["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/hexalith",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
            ["Authentication:WorkloadIssuer:Workload"] = caller,
            ["Authentication:WorkloadIssuer:ClientId"] = caller,
            ["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
            ["EventStore:Gateway:AppId"] = "eventstore-isolated",
        });
        using var issuerTransport = new MockHttpMessageHandler(_ => throw new InvalidOperationException("No issuer request is admitted."));
        using var sourceTransport = new MockHttpMessageHandler(_ => throw new InvalidOperationException("No source request is admitted."));
        builder.Services.AddEventStoreGatewayNativeSourceMetadataReader("folders")
            .ConfigurePrimaryHttpMessageHandler(() => sourceTransport);
        builder.Services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => issuerTransport);
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();
        IWorkloadAssertionIssuer issuer = provider.GetRequiredService<IWorkloadAssertionIssuer>();
        issuer.ShouldBeOfType<JwtWorkloadAssertionIssuer>();
        issuer.CanBindResources.ShouldBeFalse();
        provider.GetRequiredService<IOptions<WorkloadAssertionIssuerOptions>>().Value.EnableResourceBindingRequests.ShouldBeFalse();

        INativeSourceMetadataReader source = provider.GetRequiredService<INativeSourceMetadataReader>();
        (await source.ReadAsync(new AggregateIdentity("tenant-a", "folders", "tenant-a"), TestContext.Current.CancellationToken)).ShouldBeNull();
        EffectivePermissionsReadModelResult result = await MissingOrganizationAsync(new DaprOrganizationStreamSequenceReader(source));
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
        result.Snapshot.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("organization_stream_unavailable");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        issuerTransport.CallCount.ShouldBe(0);
        sourceTransport.CallCount.ShouldBe(0);
        provider.GetRequiredService<IOptions<EventStoreGatewayClientOptions>>().Value.UseWorkloadTransport.ShouldBeFalse();
        provider.GetService<IEventStoreGatewayWorkloadAssertionSource>().ShouldBeNull();
        provider.GetService<IEventStoreGatewayStreamReadProofSource>().ShouldBeNull();
    }

    /// <summary>The migrated head adapter refuses an unavailable independent observation instead of inventing zero.</summary>
    [Fact]
    public async Task UnavailableIndependentSourceObservationNeverInventsZero()
    {
        var source = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => Task.FromResult<NativeSourceObservation?>(null));
        var reader = new DaprOrganizationStreamSequenceReader(source);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => reader.GetCurrentSequenceAsync(
            "tenant-a", "tenant-a", TestContext.Current.CancellationToken));
        failure.Message.ShouldBe("The independently observed source head is unavailable.");
        ((SourceHeadBoundaryProxy)(object)source).Calls.ShouldBe(1);
    }

    /// <summary>The SDK's registered raw metadata adapter does not supply an independent source observation under this guard.</summary>
    [Fact]
    public async Task SdkRawMetadataAdapterAlsoFailsClosedWithoutSendingAnActorRequest()
    {
        var authority = SourceHeadBoundaryProxy.Create<IRouteCatalogAuthority>((_, _) => throw new InvalidOperationException("Raw reads cannot acquire catalog authority."));
        using var primary = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var invocation = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using HttpClient client = GuardedClient(authority, primary, invocation);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500" }).Build();
        var reader = new DaprNativeSourceMetadataReader(client, configuration, TimeProvider.System);
        (await reader.ReadAsync(new AggregateIdentity("tenant-a", "folders", "tenant-a"), TestContext.Current.CancellationToken)).ShouldBeNull();
        primary.CallCount.ShouldBe(0);
        invocation.CallCount.ShouldBe(0);
        ((SourceHeadBoundaryProxy)(object)authority).Calls.ShouldBe(0);
    }

    /// <summary>A missing projection plus a refused independent source read remains unavailable, never authoritative absence.</summary>
    [Fact]
    public async Task RefusedIndependentReadDoesNotTurnMissingProjectionIntoAProvenZeroHead()
    {
        var authority = SourceHeadBoundaryProxy.Create<IRouteCatalogAuthority>((_, _) => throw new InvalidOperationException("Raw reads cannot acquire catalog authority."));
        using var primary = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0") });
        using var invocation = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("0") });
        using HttpClient client = GuardedClient(authority, primary, invocation);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500" }).Build();
        EffectivePermissionsReadModelResult result = await MissingOrganizationAsync(new DaprOrganizationStreamSequenceReader(
            new DaprNativeSourceMetadataReader(client, configuration, TimeProvider.System)));
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
        result.Snapshot.ShouldBeNull();
        result.Freshness.ReasonCode.ShouldBe("organization_stream_unavailable");
        result.Freshness.ProjectionWatermark.ShouldBeNull();
        invocation.CallCount.ShouldBe(0);
        primary.CallCount.ShouldBe(0);
        ((SourceHeadBoundaryProxy)(object)authority).Calls.ShouldBe(0);
    }

    /// <summary>A positively supplied zero observation remains a distinct consumer input and safe not-found result.</summary>
    [Fact]
    public async Task ProvenZeroSourceHeadRetainsItsExistingSafeNotFoundOutcome()
    {
        var reader = SourceHeadBoundaryProxy.Create<IOrganizationStreamSequenceReader>((_, _) => Task.FromResult(0L));
        EffectivePermissionsReadModelResult result = await MissingOrganizationAsync(reader);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.NotFound);
        result.Freshness.ReasonCode.ShouldBe("organization_projection_missing");
        result.Snapshot.ShouldBeNull();
    }

    /// <summary>The gateway's native proof adapter cannot supply the consumer's absent/zero distinction from unsupported evidence or failures.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("unsupported-zero")]
    [InlineData("backend")]
    public async Task NativeGatewayMissingUnsupportedAndUnavailableEvidenceDoesNotInventAZeroObservation(string scenario)
    {
        StreamReadRequest? requested = null;
        var gateway = SourceHeadBoundaryProxy.Create<IEventStoreGatewayClient>((method, args) =>
        {
            method.Name.ShouldBe(nameof(IEventStoreGatewayClient.ReadStreamAsync));
            requested = (StreamReadRequest)args![0]!;
            return scenario == "unsupported-zero"
                ? Task.FromResult(new StreamReadPage("tenant-a", "folders", "tenant-a", [], new StreamReadMetadata(0, 0, null, 0, 0, false, null)))
                : Task.FromException<StreamReadPage>(new EventStoreGatewayException(scenario == "missing" ? 404 : 503, "component-unavailable"));
        });
        var reader = new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System);
        (await reader.ReadAsync(new AggregateIdentity("tenant-a", "folders", "tenant-a"), TestContext.Current.CancellationToken)).ShouldBeNull();
        ((SourceHeadBoundaryProxy)(object)gateway).Calls.ShouldBe(1);
        requested.ShouldNotBeNull();
        requested.Tenant.ShouldBe("tenant-a");
        requested.Domain.ShouldBe("folders");
        requested.AggregateId.ShouldBe("tenant-a");
        requested.RequireNativeProof.ShouldBeTrue();
        requested.FromSequence.ShouldBe(0);
        requested.ToSequence.ShouldBe(0);
        requested.PageSize.ShouldBe(1);
    }

    private static HttpClient GuardedClient(IRouteCatalogAuthority authority, HttpMessageHandler primary, HttpMessageHandler invocation)
        => new(new RouteCatalogInvocationHandler(authority, primary, Options.Create(new RouteCatalogHostOptions
        { InvocationTransportFactory = _ => invocation })));

    private static Task<EffectivePermissionsReadModelResult> MissingOrganizationAsync(IOrganizationStreamSequenceReader reader)
    {
        var store = SourceHeadBoundaryProxy.Create<IReadModelStore>((method, _) => method.Name == nameof(IReadModelStore.GetAsync)
            ? Task.FromResult(new ReadModelEntry<OrganizationPermissionProjection>(null, null))
            : throw new InvalidOperationException("Read-only component cannot write state."));
        var clock = SourceHeadBoundaryProxy.Create<IUtcClock>((_, _) => DateTimeOffset.UtcNow);
        var model = new EventStoreOrganizationEffectivePermissionsReadModel(store, clock, reader);
        return model.GetAsync(new EffectivePermissionsReadModelRequest("tenant-a", FolderStreamName.ReservedOrganizationPermissionScope,
            [EffectivePermissionPrincipal.User("principal")], null, null, "read_your_writes"), TestContext.Current.CancellationToken);
    }
}
