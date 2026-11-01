using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Xunit;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>
/// Boots the real Folders server composition (<see cref="FoldersServerHostComposition"/>, which
/// <c>Program.cs</c> calls) in Development on a test server. Signed fixture Bearer credentials use an in-memory
/// issuer key; only domain identity accessors, the clock, the gateway, and seeded read models are replaced.
/// </summary>
internal sealed class RoutingModeTestHost : IAsyncDisposable
{
    /// <summary>The seeded tenant.</summary>
    public const string TenantId = "tenant-a";

    /// <summary>The seeded principal.</summary>
    public const string PrincipalId = "user-a";

    /// <summary>The seeded folder.</summary>
    public const string FolderId = "folder_000000001";

    /// <summary>The correlation identifier bound to the seeded lifecycle evidence.</summary>
    public const string CorrelationId = "correlation_routing_0001";

    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
    private const string Issuer = "https://identity.example.test/realms/folders";

    private RoutingModeTestHost(
        WebApplication app,
        HttpClient client,
        FoldersApiRoutingMode mode,
        CountingFolderTenantAccessProjectionStore tenantStore,
        CountingFolderLifecycleStatusReadModel lifecycle,
        RecordingRoutingAuditSink auditSink,
        IEventStoreGatewayClient gateway)
    {
        App = app;
        Client = client;
        Mode = mode;
        TenantStore = tenantStore;
        Lifecycle = lifecycle;
        AuditSink = auditSink;
        Gateway = gateway;
    }

    /// <summary>Gets the composed application.</summary>
    public WebApplication App { get; }

    /// <summary>Gets a client bound to the test server.</summary>
    public HttpClient Client { get; }

    /// <summary>Gets the mode that the host composition resolved.</summary>
    public FoldersApiRoutingMode Mode { get; }

    /// <summary>Gets the counting tenant-access store.</summary>
    public CountingFolderTenantAccessProjectionStore TenantStore { get; }

    /// <summary>Gets the counting lifecycle-status read model.</summary>
    public CountingFolderLifecycleStatusReadModel Lifecycle { get; }

    /// <summary>Gets the PD10 authorization audit sink.</summary>
    public RecordingRoutingAuditSink AuditSink { get; }

    /// <summary>Gets the substituted EventStore gateway.</summary>
    public IEventStoreGatewayClient Gateway { get; }

    /// <summary>Creates a Development builder with an optional routing-mode setting.</summary>
    /// <param name="mode">The configured mode value, or <see langword="null"/> to leave the setting absent.</param>
    /// <returns>A builder that has not yet composed the Folders host.</returns>
    public static WebApplicationBuilder CreateBuilder(string? mode)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseTestServer();
        if (mode is not null)
        {
            builder.Configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>(FoldersApiRouting.ModeConfigurationKey, mode),
            ]);
        }

        return builder;
    }

    /// <summary>Composes, seeds, and starts the host.</summary>
    /// <param name="mode">The configured mode value, or <see langword="null"/> to leave the setting absent.</param>
    /// <param name="claimsIdentity">
    /// <see langword="true"/> keeps the real claims-based tenant and evidence accessors;
    /// <see langword="false"/> substitutes a fixed domain identity. Both authenticate signed fixture Bearer credentials.
    /// </param>
    /// <returns>The started host.</returns>
    public static async Task<RoutingModeTestHost> StartAsync(string? mode, bool claimsIdentity = false)
    {
        WebApplicationBuilder builder = CreateBuilder(mode);
        builder.Configuration["Folders:Authentication:Authority"] = Issuer;
        builder.Configuration["Folders:Authentication:ValidIssuer"] = Issuer;
        builder.Configuration["Folders:Authentication:Audience"] = "folders";
        FoldersApiRoutingMode resolved = builder.AddFoldersServerHost();
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa) { KeyId = "routing-caller" };
        OpenIdConnectConfiguration oidc = new() { Issuer = Issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = signingKey.KeyId });
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));

        FixedUtcClock clock = new(Now);
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        await tenants.SaveAsync(
            new FolderTenantAccessProjection
            {
                TenantId = TenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    [PrincipalId] = new(PrincipalId, "Owner"),
                },
                Watermark = 1,
                LastEventTimestamp = Now.AddMinutes(-1),
                ProjectionWatermark = $"{TenantId}:1",
            },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        CountingFolderTenantAccessProjectionStore tenantStore = new(tenants);

        InMemoryEffectivePermissionsReadModel permissions = new();
        permissions.Save(new EffectivePermissionsReadModelSnapshot(
            TenantId,
            "org-a",
            FolderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User(PrincipalId), "read_metadata", Sequence: 1, EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

        InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(clock);
        lifecycleReadModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            ManagedTenantId: TenantId,
            FolderId: FolderId,
            LifecycleState: FolderLifecycleProjectionState.Active,
            BindingStatus: FolderRepositoryBindingStatus.Unbound,
            RepositoryBindingId: null,
            ProviderBindingRef: null,
            Freshness: new FolderLifecycleFreshness(
                ReadConsistency: "eventually_consistent",
                ObservedAt: Now,
                ProjectionWatermark: "lifecycle_watermark_v1",
                Stale: false,
                ReasonCode: null),
            EvidenceScope: new FolderLifecycleEvidenceScope(
                ManagedTenantId: TenantId,
                PrincipalId: PrincipalId,
                ActionToken: "read_metadata",
                TaskId: null,
                CorrelationId: CorrelationId,
                AuthorizationWatermark: "permission-watermark-a"),
            DiagnosticSentinels: []));
        CountingFolderLifecycleStatusReadModel lifecycle = new(lifecycleReadModel);

        TimeProvider timeProvider = new RoutingModeFixedTimeProvider(Now);
        RoutingModeTenantContext identity = new(TenantId, PrincipalId);
        RecordingRoutingAuditSink auditSink = new();
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();

        if (!claimsIdentity)
        {
            builder.Services.AddFoldersServerTestDefaults();
            builder.Services.RemoveAll<ITenantContextAccessor>();
            builder.Services.AddSingleton<ITenantContextAccessor>(identity);
            builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
            builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(identity);
        }

        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton(gateway);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenantStore);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
        builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycle);

        builder.Services.RemoveAll<IPd10AuthorizationAuditSink>();
        builder.Services.AddSingleton<IPd10AuthorizationAuditSink>(auditSink);
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(clock);
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton(timeProvider);

        WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new(JwtBearerDefaults.AuthenticationScheme,
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = "folders",
                Expires = DateTime.UtcNow.AddMinutes(2),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = PrincipalId,
                    [TenantContextOptions.EventStoreTenantClaimType] = TenantId,
                    ["client_id"] = "Hexalith.Projects",
                    ["eventstore:permission"] = "read_metadata",
                    [RouteCatalogSurfacePolicy.ClaimType] = "ui",
                },
                SigningCredentials = new(signingKey, SecurityAlgorithms.RsaSha256),
            }));

        return new RoutingModeTestHost(app, client, resolved, tenantStore, lifecycle, auditSink, gateway);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await App.DisposeAsync().ConfigureAwait(true);
    }
}
