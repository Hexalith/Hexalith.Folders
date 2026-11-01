using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Guards the production service graph and the callback authority boundary.</summary>
public sealed class FoldersServerHostCompositionTests
{
    /// <summary>Checks the installed receipt validator, durable tenant store, and workload defaults.</summary>
    [Fact]
    public void ProductionHostRegistersReceiptAndDurableTenantAuthority()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();

        app.Services.GetRequiredService<IEventStoreAuthorizationValidator>()
            .ShouldBeOfType<VerifiedReceiptEventStoreAuthorizationValidator>();
        app.Services.GetRequiredService<HumanAuthorityReceiptVerifier>().ShouldNotBeNull();
        app.Services.GetRequiredService<IFolderTenantAccessProjectionStore>()
            .ShouldBeOfType<EventStoreFolderTenantAccessProjectionStore>();

        WorkloadAuthenticationOptions workload = app.Services
            .GetRequiredService<IOptionsMonitor<WorkloadAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        workload.Audience.ShouldBe("folders");
        workload.AllowedCallers.ShouldBe(["eventstore"]);
    }

    [Fact]
    public void ProductionHostBindsIndependentPrivateProfilesAndConfiguredProviderServices()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        foreach (string profile in new[] { "first", "second" })
        {
            string prefix = "Folders:ProviderBindings:Profiles:" + profile + ":";
            builder.Configuration[prefix + "ManagedTenantId"] = "tenant-" + profile;
            builder.Configuration[prefix + "ProviderKind"] = "forgejo";
            builder.Configuration[prefix + "NamingPolicy:Metadata:repository_owner"] = profile;
            builder.Configuration[prefix + "BranchPolicy:Metadata:selected_ref"] = "ref-" + profile;
        }
        using WebApplication app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FoldersProviderBindingOptions>>().Value;
        options.Profiles["first"].NamingPolicy.Metadata["repository_owner"].ShouldBe("first");
        options.Profiles["second"].NamingPolicy.Metadata["repository_owner"].ShouldBe("second");
        options.Profiles["first"].BranchPolicy.Metadata["selected_ref"].ShouldBe("ref-first");
        options.Profiles["first"].NamingPolicy.ShouldNotBeSameAs(options.Profiles["second"].NamingPolicy);
        options.Profiles["first"].BranchPolicy.ShouldNotBeSameAs(options.Profiles["second"].BranchPolicy);
        OrganizationProviderBindingPolicy.Empty.Metadata.ShouldBeEmpty();
        app.Services.GetRequiredService<IProviderReadinessBindingReader>().ShouldBeOfType<ConfiguredProviderReadinessBindingReader>();
        app.Services.GetRequiredService<IProviderRepositoryTargetResolver>().ShouldBeOfType<ConfiguredProviderRepositoryTargetResolver>();
    }

    [Fact]
    public async Task ProtectedProductionDiscoveryAdvertisesOnlyProcessorBackedNamedAndSharedRoutes()
    {
        const string issuer = "https://identity.example.test/realms/folders";
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey signingKey = new(rsa) { KeyId = "metadata-test" };
        OpenIdConnectConfiguration oidc = new() { Issuer = issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = signingKey.KeyId });
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(environmentName: Microsoft.Extensions.Hosting.Environments.Development, useCallbackCatalog: false);
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Configuration["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256;
        builder.Services.PostConfigure<JwtBearerOptions>(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
        builder.Services.AddSingleton<IAsyncDomainProjectionHandler, OrphanOperationalProjectionHandler>();
        var projectionCatalog = new FoldersProjectionCatalogFixture();
        projectionCatalog.Configure(builder);
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await projectionCatalog.ActivateAsync(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        EventStoreDomainServiceEndpointInventory.Validate(endpoints, app.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy).ShouldBeEmpty();
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, app.Services)).ShouldBeEmpty();
        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("folders")
            .ShouldBeSameAs(scope.ServiceProvider.GetServices<IDomainProcessor>().OfType<FolderDomainProcessor>().Single());
        app.Services.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(typeof(IDomainProcessor), "orphan").ShouldBeFalse();
        using HttpClient client = app.GetTestClient();
        AdminOperationalIndexMetadata.Request request = new(["folders"]) { AppId = "folders", ServiceVersion = "v1" };
        using (HttpResponseMessage denied = await client.PostAsJsonAsync("/admin/operational-index-metadata", request, TestContext.Current.CancellationToken))
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        DateTime issued = DateTime.UtcNow;
        string token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = "folders",
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                [RouteCatalogSurfacePolicy.ClaimType] = "service",
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceMetadata,
            },
            SigningCredentials = new(signingKey, SecurityAlgorithms.RsaSha256),
        });
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, token);
        using (HttpResponseMessage unbound = await client.PostAsJsonAsync("/admin/operational-index-metadata", request, TestContext.Current.CancellationToken))
            unbound.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using (var wrongGeneration = new HttpRequestMessage(HttpMethod.Post, "/admin/operational-index-metadata") { Content = JsonContent.Create(request) })
        {
            projectionCatalog.Bind(wrongGeneration);
            wrongGeneration.Headers.Remove(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.EpochHeader);
            wrongGeneration.Headers.Add(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.EpochHeader, "2");
            using HttpResponseMessage refused = await client.SendAsync(wrongGeneration, TestContext.Current.CancellationToken);
            refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/operational-index-metadata") { Content = JsonContent.Create(request) };
        projectionCatalog.Bind(metadataRequest);
        using HttpResponseMessage response = await client.SendAsync(metadataRequest, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = (await response.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken))!;
        var domain = metadata.Domains.ShouldHaveSingleItem();
        domain.Domain.ShouldBe("folders");
        domain.NamedProjectionTypes.ShouldBe([OperationalStatusKeys.ProjectionType]);
        domain.SharedEpochProjectionTypes.ShouldBe([OperationalStatusKeys.ProjectionType]);
        domain.AggregateTypes.ShouldBeEmpty();
        metadata.CatalogFingerprint.ShouldNotBeNullOrWhiteSpace();
        using var orphanRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/operational-index-metadata") { Content = JsonContent.Create(request with { Domains = ["orphan"] }) };
        projectionCatalog.Bind(orphanRequest);
        using HttpResponseMessage orphanResponse = await client.SendAsync(orphanRequest, TestContext.Current.CancellationToken);
        orphanResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var unserved = AdminOperationalIndexMetadata.Create(
            app.Services.GetRequiredService<Hexalith.EventStore.Client.Discovery.DiscoveryResult>(), ["orphan"],
            app.Services.GetServices<IDomainQueryHandler>(), app.Services.GetServices<IAsyncDomainProjectionHandler>(),
            "folders", "v1", app.Services.GetRequiredService<IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions>>().Value,
            ["folders"], app.Services.GetServices<IDomainProjectionHandler>());
        unserved.Domains.ShouldBeEmpty();
        unserved.DispatchCapability.ShouldBeNull();
        unserved.CatalogFingerprint.ShouldBeNull();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Runs shared endpoint security after route selection in every supported API mode.</summary>
    /// <param name="mode">The existing API routing mode.</param>
    /// <param name="route">An actual protected route in that mode.</param>
    [Theory]
    [InlineData(FoldersApiRoutingMode.V1Only, "/api/v1/folders/folder-0123456789abcdef/acl")]
    [InlineData(FoldersApiRoutingMode.Coexistence, "/api/v2/folders/folder-0123456789abcdef/acl")]
    [InlineData(FoldersApiRoutingMode.V2Only, "/api/v2/folders/folder-0123456789abcdef/acl")]
    public async Task ProductionHostRoutesBeforeSharedSecurityInEveryApiMode(FoldersApiRoutingMode mode, string route)
    {
        const string tenant = "tenant-mode";
        const string principal = "user-mode-0123456789abcdef";
        const string folder = "folder-0123456789abcdef";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using RSA rsa = RSA.Create(2048); RsaSecurityKey key = new(rsa) { KeyId = "mode-caller" };
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        builder.Configuration[FoldersApiRouting.ModeConfigurationKey] = mode.ToString();
        ConfigureSigning(builder, key, JwtBearerDefaults.AuthenticationScheme);
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = tenant, Enabled = true, Watermark = 1, ProjectionWatermark = "mode:1", LastEventTimestamp = now,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal) { [principal] = new(principal, "Member") },
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        InMemoryEffectivePermissionsReadModel permissions = new();
        permissions.Save(new(tenant, tenant, folder, EffectivePermissionsFolderLifecycleState.Active,
            [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User(principal), "read_metadata", 1, now),
             new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant, EffectivePermissionPrincipal.User(principal), "manage_folder_access", 1, now)],
            new("read_your_writes", now, "mode:1", false, null), true, null));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>(); builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenants);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>(); builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IFolderAclReadModel>();
        builder.Services.AddSingleton<HostModeFolderAclReadModel>(services => new(new(tenant, tenant, folder, 1, true, false, now, [], []),
            services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()));
        builder.Services.AddSingleton<IFolderAclReadModel>(services => services.GetRequiredService<HostModeFolderAclReadModel>());
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            using HttpResponseMessage alive = await client.GetAsync("/alive", TestContext.Current.CancellationToken).ConfigureAwait(true);
            alive.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage denied = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            HostModeFolderAclReadModel observed = app.Services.GetRequiredService<HostModeFolderAclReadModel>();
            observed.ReadCount.ShouldBe(0);
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "https://identity.example.test/realms/folders", Audience = "folders", Expires = DateTime.UtcNow.AddMinutes(2),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = principal, [TenantContextOptions.EventStoreTenantClaimType] = tenant,
                    ["eventstore:permission"] = new[] { "read_metadata", "manage_folder_access" },
                    [RouteCatalogSurfacePolicy.ClaimType] = "ui",
                },
                SigningCredentials = new(key, SecurityAlgorithms.RsaSha256),
            }));
            using HttpResponseMessage allowed = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            observed.ReadCount.ShouldBe(1);
            observed.EndpointName.ShouldBe("ListFolderAclEntries");
            observed.RequestPath.ShouldBe("/api/v1/folders/" + folder + "/acl");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Starts configured SDK-owned OTLP providers and observes module subscriptions independently of OTLP delivery.</summary>
    [Fact]
    public async Task ConfiguredOtlpStartsOneProviderPerSignalAndSubscribesFoldersSignals()
    {
        WebApplicationBuilder collectorBuilder = WebApplication.CreateSlimBuilder();
        collectorBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication collector = collectorBuilder.Build();
        collector.MapPost("/{**signal}", () => Microsoft.AspNetCore.Http.Results.Bytes([], "application/x-protobuf"));
        await collector.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.Urls.Single(),
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            });
            builder.Services.ConfigureAll<OpenTelemetry.Exporter.OtlpExporterOptions>(options =>
            {
                options.Endpoint = new Uri(collector.Urls.Single());
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                options.TimeoutMilliseconds = 5000;
            });
            HostModuleMetricExporter observedMetrics = new();
            PeriodicExportingMetricReader moduleReader = new(observedMetrics);
            builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddReader(moduleReader));
            await using (WebApplication app = builder.Build())
            {
                app.UseFoldersServerPipeline();
                await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                app.Services.GetServices<OpenTelemetry.Trace.TracerProvider>().ShouldHaveSingleItem();
                app.Services.GetServices<OpenTelemetry.Metrics.MeterProvider>().ShouldHaveSingleItem();
                app.Services.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>()
                    .OfType<OpenTelemetry.Logs.OpenTelemetryLoggerProvider>().ShouldHaveSingleItem();
                using (System.Diagnostics.ActivitySource source = new(Hexalith.Folders.Observability.FolderTelemetryNames.ActivitySourceName))
                using (System.Diagnostics.Activity? activity = source.StartActivity("folders-source-export"))
                    activity.ShouldNotBeNull();
                using System.Diagnostics.Metrics.Meter meter = new(Hexalith.Folders.Observability.FolderTelemetryNames.MeterName);
                meter.CreateCounter<long>("folders.source.export").Add(1);
                app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger("Folders.Source.Export").LogInformation("Source fixture exporter validation.");
                // This local reader checks subscription only. OTLP delivery has a separate SDK transport limitation.
                moduleReader.Collect(5000).ShouldBeTrue();
                observedMetrics.Names.ShouldContain("folders.source.export");
                await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            }

        }
        finally
        {
            await collector.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Uses one actual resilience handler and keeps Dapr targets on independent circuit breakers.</summary>
    [Fact]
    public async Task SdkDefaultsInstallOneResilienceHandlerAndPartitionDaprTargets()
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        var calls = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        builder.Services.AddHttpClient("host-defaults")
            .ConfigurePrimaryHttpMessageHandler(() => new HostDefaultsInvocationHandler(calls));
        builder.Services.Configure<RouteCatalogHostOptions>(options =>
        {
            options.ControlPlaneAppIds = ["target-a", "target-b"];
            options.InvocationTransportFactory = _ => new HostDefaultsInvocationHandler(calls);
        });
        builder.Services.ConfigureAll<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 1;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
        });
        await using WebApplication app = builder.Build();
        HttpMessageHandler handler = app.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("host-defaults");
        int resilienceHandlers = 0;
        while (handler is DelegatingHandler delegating)
        {
            if (handler is Microsoft.Extensions.Http.Resilience.ResilienceHandler) resilienceHandlers++;
            handler = delegating.InnerHandler!;
        }
        resilienceHandlers.ShouldBe(1);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("host-defaults");
        const string prefix = "http://127.0.0.1:3500/v1.0/invoke/";
        string suffix = "/method/" + RouteCatalogEndpointExtensions.ReadbackMethod;
        using HttpResponseMessage failed = await client.GetAsync(prefix + "target-a" + suffix, TestContext.Current.CancellationToken).ConfigureAwait(true);
        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await Should.ThrowAsync<Polly.CircuitBreaker.BrokenCircuitException>(() =>
            client.GetAsync(prefix + "target-a" + suffix, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        using HttpResponseMessage independent = await client.GetAsync(prefix + "target-b" + suffix, TestContext.Current.CancellationToken).ConfigureAwait(true);
        independent.StatusCode.ShouldBe(HttpStatusCode.OK);
        calls["target-a"].ShouldBe(2);
        calls["target-b"].ShouldBe(1);
    }

    /// <summary>Evaluates monitored dependency states through the actual anonymous readiness route.</summary>
    /// <param name="daprHealthy">Whether the monitored sidecar is available.</param>
    /// <param name="tenantsDegraded">Whether the monitored tenant service is degraded.</param>
    /// <param name="lag">The monitored projection lag.</param>
    /// <param name="status">The required HTTP status.</param>
    /// <param name="body">The aggregate readiness classification.</param>
    [Theory]
    [InlineData(true, false, 500L, HttpStatusCode.OK, "Healthy")]
    [InlineData(true, false, 501L, HttpStatusCode.OK, "Degraded")]
    [InlineData(true, true, 0L, HttpStatusCode.OK, "Degraded")]
    [InlineData(false, false, 0L, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task ProductionReadinessReportsHealthyDegradedAndUnavailableDapr(bool daprHealthy, bool tenantsDegraded,
        long lag, HttpStatusCode status, string body)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        builder.Services.RemoveAll<Hexalith.Folders.ServiceDefaults.IReadinessSnapshotSource>();
        builder.Services.AddSingleton<Hexalith.Folders.ServiceDefaults.IReadinessSnapshotSource>(new HostReadinessSnapshotSource(new()
        {
            DaprSidecarHealthy = daprHealthy, TenantsAvailabilityDegradedModeActive = tenantsDegraded, ProjectionLagMilliseconds = lag,
        }));
        await using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpResponseMessage response = await app.GetTestClient().GetAsync("/ready", TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(status);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(body);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Maps each service's declared probes to the actual Server host paths.</summary>
    [Fact]
    public async Task DeclaredServerProbesMatchTheActualProductionHost()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.Folders.slnx"))) root = root.Parent;
        using StreamReader reader = File.OpenText(Path.Combine(root!.FullName, "deploy/observability/production/observability.yaml"));
        YamlDotNet.RepresentationModel.YamlStream yaml = new(); yaml.Load(reader);
        var document = (YamlDotNet.RepresentationModel.YamlMappingNode)yaml.Documents[0].RootNode;
        var spec = (YamlDotNet.RepresentationModel.YamlMappingNode)document.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("spec")];
        var probes = ((YamlDotNet.RepresentationModel.YamlSequenceNode)spec.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("healthProbes")])
            .Children.Cast<YamlDotNet.RepresentationModel.YamlMappingNode>().ToArray();
        static string Field(YamlDotNet.RepresentationModel.YamlMappingNode node, string key)
            => ((YamlDotNet.RepresentationModel.YamlScalarNode)node.Children[new YamlDotNet.RepresentationModel.YamlScalarNode(key)]).Value!;
        string[] paths = probes.Where(node => Field(node, "owningComponent") == "folders-server").Select(node => Field(node, "path")).ToArray();
        paths.ShouldBe(["/alive", "/ready"], ignoreOrder: true);
        probes.Where(node => Field(node, "owningComponent") == "folders-workers").Select(node => Field(node, "path"))
            .ShouldBe(["/alive", "/ready"], ignoreOrder: true);
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        await using WebApplication app = builder.Build(); app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            foreach (string path in paths)
            {
                using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
            }
        }
        finally { await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true); }
    }

    /// <summary>Requires canonical named workload policies on every SDK catalog lifecycle endpoint.</summary>
    /// <param name="route">The canonical control route.</param>
    /// <param name="readback">Whether the route is readback rather than activation.</param>
    [Theory]
    [InlineData("/internal/routing/catalog-generation", true)]
    [InlineData("/internal/routing/catalog/prepare", false)]
    [InlineData("/internal/routing/catalog/ready", false)]
    [InlineData("/internal/routing/catalog/commit", false)]
    [InlineData("/internal/routing/catalog/rollback", false)]
    [InlineData("/internal/routing/catalog/reconcile", false)]
    public async Task CatalogLifecycleEndpointsRetainNamedPoliciesAndRefuseMissingOrWrongAuthority(string route, bool readback)
    {
        using RSA rsa = RSA.Create(2048); RsaSecurityKey key = new(rsa) { KeyId = "catalog-management" };
        WebApplicationBuilder builder = CatalogBuilder(key);
        await using WebApplication app = builder.Build(); app.UseFoldersServerPipeline();
        string expectedOperation = readback ? Hexalith.EventStore.Contracts.Authorization.EventStoreOperationIdentifiers.RouteCatalogRead
            : Hexalith.EventStore.Contracts.Authorization.EventStoreOperationIdentifiers.RouteCatalogActivate;
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText == route);
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldHaveSingleItem().Policy.ShouldBe(
            EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, expectedOperation));
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpClient client = app.GetTestClient();
            foreach (string fault in new[] { "absent", "wrong-operation", "wrong-caller" })
            {
                using HttpRequestMessage request = new(readback ? HttpMethod.Get : HttpMethod.Post, route);
                if (!readback) request.Content = JsonContent.Create(new { });
                if (fault != "absent")
                {
                    request.Headers.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
                    request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, CatalogAssertion(key,
                        fault == "wrong-operation" ? readback ? "eventstore:route-catalog-activate" : "eventstore:route-catalog-read" : expectedOperation,
                        fault == "wrong-caller" ? "other-controller" : "catalog-controller"));
                }
                using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(fault == "wrong-operation" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
            }
            if (readback)
            {
                using HttpRequestMessage request = new(HttpMethod.Get, route);
                request.Headers.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, CatalogAssertion(key, expectedOperation, "catalog-controller"));
                using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await response.Content.ReadFromJsonAsync<Hexalith.EventStore.Contracts.Routing.RouteCatalogGenerationReadback>(TestContext.Current.CancellationToken).ConfigureAwait(true))!
                    .Serving.ShouldBeTrue();
            }
        }
        finally { await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true); }
    }

    /// <summary>Does not turn authenticated catalog activation permission into candidate trust.</summary>
    [Fact]
    public async Task CatalogActivationStillRefusesAnUntrustedCandidate()
    {
        using RSA rsa = RSA.Create(2048); RsaSecurityKey key = new(rsa) { KeyId = "catalog-activation" };
        WebApplicationBuilder builder = CatalogBuilder(key);
        await using WebApplication app = builder.Build(); app.UseFoldersServerPipeline();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            var authority = app.Services.GetRequiredService<FoldersCallbackRouteCatalog>();
            string root = authority.Snapshot.RootDigest;
            var candidate = new Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation("untrusted-attempt", authority.Snapshot.GetCanonicalBytes(),
                root, null, [app.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity!], "untrusted", "untrusted");
            using HttpRequestMessage request = new(HttpMethod.Post, "/internal/routing/catalog/prepare") { Content = JsonContent.Create(candidate) };
            request.Headers.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, CatalogAssertion(key, "eventstore:route-catalog-activate", "catalog-controller"));
            using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            authority.Snapshot.RootDigest.ShouldBe(root);
        }
        finally { await app.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true); }
    }

    private static WebApplicationBuilder CatalogBuilder(RsaSecurityKey key)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:RouteCatalogWorkload:Audience"] = "folders",
            ["Authentication:RouteCatalogWorkload:AllowedCallers:0"] = "catalog-controller",
        });
        ConfigureSigning(builder, key, RouteCatalogEndpointExtensions.WorkloadScheme);
        return builder;
    }

    private static void ConfigureSigning(WebApplicationBuilder builder, RsaSecurityKey key, string scheme)
    {
        OpenIdConnectConfiguration oidc = new() { Issuer = "https://identity.example.test/realms/folders" };
        oidc.SigningKeys.Add(new RsaSecurityKey(key.Rsa!.ExportParameters(false)) { KeyId = key.KeyId });
        builder.Services.PostConfigure<JwtBearerOptions>(scheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
    }

    private static string CatalogAssertion(RsaSecurityKey key, string operation, string caller)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://identity.example.test/realms/folders", Audience = "folders", Expires = DateTime.UtcNow.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = caller,
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operation,
                [RouteCatalogSurfacePolicy.ClaimType] = "service",
            },
            SigningCredentials = new(key, SecurityAlgorithms.RsaSha256),
        });

    /// <summary>Checks that each actual callback requires its own workload operation.</summary>
    /// <param name="route">The callback route.</param>
    /// <param name="operation">The only operation accepted by the route's policy.</param>
    [Theory]
    [InlineData(FoldersServerModule.ProcessRoute, EventStoreWorkloadOperations.DomainServiceProcess)]
    [InlineData(FoldersServerModule.ProjectRoute, EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("/query", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("/admin/operational-index-metadata", EventStoreWorkloadOperations.DomainServiceMetadata)]
    public void ProductionCallbackUsesItsOperationPolicy(string route, string operation)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        using WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();

        string policyName = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, operation);
        AuthorizationPolicy? policy = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value.GetPolicy(policyName);
        policy.ShouldNotBeNull();
        policy.AuthenticationSchemes.ShouldBe([EventStoreWorkloadAuthenticationDefaults.WorkloadScheme]);
        policy.Requirements.ShouldContain(static requirement => requirement is DenyAnonymousAuthorizationRequirement);
        ClaimsAuthorizationRequirement operationRequirement = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Single(static requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.OperationClaimType);
        operationRequirement.AllowedValues.ShouldBe([operation]);
        policy.Requirements.OfType<ClaimsAuthorizationRequirement>()
            .ShouldContain(static requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType);

        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == route);
        endpoint.Metadata.OfType<IAuthorizeData>()
            .ShouldContain(authorize => authorize.Policy == policyName);
    }
}
