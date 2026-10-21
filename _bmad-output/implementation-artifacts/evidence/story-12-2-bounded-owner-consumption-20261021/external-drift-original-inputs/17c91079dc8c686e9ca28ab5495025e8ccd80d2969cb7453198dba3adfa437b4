#if HEXALITH_SOURCE_FIXTURE
namespace Hexalith.Projects.Server.Tests.Queries;

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Projects.Contracts.Queries;
using Hexalith.Projects.Projections.TenantAccess;
using Hexalith.Projects.Server.Queries;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

/// <summary>Local bound-signing source lane: real gateway/SDK/owner HTTP and Dapr persistence; loopback replaces service discovery only.</summary>
public sealed class SupportedProjectGatewayHttpTests
{
    private const string Issuer = "story61-bound-signing";
    private const string Audience = "configured-read-audience";
    private const string Channel = "story61-owned-local-channel";
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task GatewayCallerAuthorityAndCurrentFolderAclControlListOpenAndSafeFailures()
    {
        string? http = Environment.GetEnvironmentVariable("STORY61_DAPR_HTTP_ENDPOINT"), grpc = Environment.GetEnvironmentVariable("STORY61_DAPR_GRPC_ENDPOINT");
        Assert.SkipUnless(http is not null && grpc is not null, "Requires the isolated Story 6.1 Dapr source lane.");
        string tenant = "gw-" + Guid.NewGuid().ToString("N");
        using RSA ownerKey = RSA.Create(2048);
        var transport = new GatewayFixtureLocalRoutes();
        var heads = new GatewayFixtureOwnerHeads();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WebApplication folders = await StartFoldersAsync(http!, grpc!, transport, heads, now).ConfigureAwait(true);
        await using var foldersLifetime = folders.ConfigureAwait(true);
        transport.Add("folders", folders);
        WebApplication projects = await StartProjectsAsync(http!, grpc!, transport, ownerKey, now).ConfigureAwait(true);
        await using var projectsLifetime = projects.ConfigureAwait(true);
        transport.Add("projects", projects);
        WebApplication gateway = await StartGatewayAsync(http!, grpc!, transport, ownerKey).ConfigureAwait(true);
        await using var gatewayLifetime = gateway.ConfigureAwait(true);
        transport.Add("eventstore", gateway);
        var tenantProjection = new ProjectTenantAccessProjection { TenantId = tenant, Enabled = true, Watermark = 1,
            ProjectionWatermark = tenant + ":1", LastEventTimestamp = now };
        tenantProjection.Principals["actor-1"] = new("actor-1", "TenantOwner");
        await projects.Services.GetRequiredService<IProjectTenantAccessProjectionStore>().SaveAsync(tenantProjection, TestContext.Current.CancellationToken);
        var folderTenant = new FolderTenantAccessProjection { TenantId = tenant, Enabled = true, Watermark = 1,
            ProjectionWatermark = tenant + ":1", LastEventTimestamp = now };
        folderTenant.Principals["actor-1"] = new("actor-1", "TenantOwner");
        await folders.Services.GetRequiredService<IFolderTenantAccessProjectionStore>().SaveAsync(folderTenant, TestContext.Current.CancellationToken);
        IWorkloadAssertionIssuer producer = gateway.Services.GetRequiredService<IWorkloadAssertionIssuer>();
        using HttpClient platform = gateway.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        using DaprClient dapr = new DaprClientBuilder().UseHttpEndpoint(http!).UseGrpcEndpoint(grpc!).Build();
        foreach (string domain in new[] { "projects", "folders" })
        {
            using HttpRequestMessage metadataRequest = dapr.CreateInvokeMethodRequest(domain, "admin/operational-index-metadata",
                new AdminOperationalIndexMetadata.Request([domain]) { AppId = domain, ServiceVersion = "v1" });
            using HttpResponseMessage metadataResponse = await platform.SendAsync(metadataRequest, TestContext.Current.CancellationToken);
            metadataResponse.EnsureSuccessStatusCode();
            AdminOperationalIndexMetadata.Response metadata = (await metadataResponse.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken))!;
            var declared = metadata.Domains.Single(item => item.Domain == domain);
            await dapr.SaveStateAsync("statestore", "admin:query-types:" + domain, declared.QueryTypes, cancellationToken: TestContext.Current.CancellationToken);
            if (domain == "folders") declared.QueryTypes.ShouldContain(FolderOwnerReadQuery.QueryType);
            if (domain == "projects")
            {
                foreach (string id in new[] { "project-a", "project-b" })
                {
                    ProjectionRequest seed = SupportedProjectReadFixture.Request(id);
                    seed = seed with { TenantId = tenant, Events = seed.Events.Select(item => item with
                        { Payload = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(item.Payload).Replace("tenant-a", tenant, StringComparison.Ordinal)) }).ToArray() };
                    using HttpRequestMessage dispatch = dapr.CreateInvokeMethodRequest("projects", "project/v2", new ProjectionDispatchRequest(seed,
                        ["project-reads"], "gateway-seed-" + id, metadata.CatalogFingerprint!));
                    using HttpResponseMessage projected = await platform.SendAsync(dispatch, TestContext.Current.CancellationToken);
                    projected.EnsureSuccessStatusCode();
                    (await projected.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(TestContext.Current.CancellationToken))!.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
                }
            }
        }
        EventStoreFolderEffectivePermissionsReadModel owner = folders.Services.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>();
        foreach (string id in new[] { "folder-project-a", "folder-project-b" })
        {
            await owner.ApplyAsync(tenant, id, "created", 1, "created", new FolderCreated(tenant, tenant, id, "Owner label", null, null, [],
                FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "actor-1", "corr", "task", "create", "digest", now), TestContext.Current.CancellationToken);
            await owner.ApplyAsync(tenant, id, "granted", 2, "granted", new FolderAccessGranted(tenant, tenant, id, FolderAccessPrincipalKind.User,
                "actor-1", "read_metadata", "actor-1", "corr", "task", "grant", "digest", 2, now), TestContext.Current.CancellationToken);
            heads.Set(tenant, id, 2);
        }
        using HttpClient client = new() { BaseAddress = new Uri(gateway.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", HumanToken(tenant, inspection: false));
        SubmitQueryRequest Open(object payload, string id = "project-a") => new(tenant, "projects", id, GetProjectQuery.QueryType, Payload: JsonSerializer.SerializeToElement(payload, JsonSerializerOptions.Web));
        SubmitQueryRequest List(ListProjectsQuery payload) => new(tenant, "projects", "projects", ListProjectsQuery.QueryType, Payload: JsonSerializer.SerializeToElement(payload, JsonSerializerOptions.Web));
        GetProjectResponse opened = await ReadAsync<GetProjectResponse>(client, Open(new GetProjectQuery()));
        opened.Name.ShouldBeNull();
        ProjectListPage first = await ReadAsync<ProjectListPage>(client, List(new(PageSize: 1)));
        first.Items.Single().ProjectId.ShouldBe("project-a"); first.Cursor.ShouldNotBeNull();
        (await ReadAsync<ProjectListPage>(client, List(new(Cursor: first.Cursor, PageSize: 1)))).Items.Single().ProjectId.ShouldBe("project-b");
        await StatusAsync(client, List(new(Cursor: "invalid")), HttpStatusCode.BadRequest);
        await StatusAsync(client, Open(new[] { 1 }), HttpStatusCode.BadRequest);
        await StatusAsync(client, Open(new GetProjectQuery(true)), HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Authorization = new("Bearer", HumanToken(tenant, inspection: true));
        (await ReadAsync<GetProjectResponse>(client, Open(new GetProjectQuery(true)))).Name.ShouldBe("Project project-a");
        (await ReadAsync<ProjectListPage>(client, List(new(InspectDescriptiveMetadata: true)))).Items.ShouldAllBe(item => item.Name != null);
        // Real owner ACL revocation, then current stream-head lag/outage: neither can be hidden by inspection.
        await owner.ApplyAsync(tenant, "folder-project-a", "revoked", 3, "revoked", new FolderAccessRevoked(tenant, tenant, "folder-project-a",
            FolderAccessPrincipalKind.User, "actor-1", "read_metadata", "actor-1", "corr", "task", "revoke", "digest", 3, now), TestContext.Current.CancellationToken);
        heads.Set(tenant, "folder-project-a", 3);
        client.DefaultRequestHeaders.Authorization = new("Bearer", HumanToken(tenant, inspection: false, projectUser: true));
        string denied = await StatusAsync(client, Open(new GetProjectQuery()), HttpStatusCode.NotFound);
        string absent = await StatusAsync(client, Open(new GetProjectQuery(), "absent"), HttpStatusCode.NotFound);
        JsonDocument.Parse(denied).RootElement.GetProperty("detail").GetString().ShouldBe(JsonDocument.Parse(absent).RootElement.GetProperty("detail").GetString());
        (await ReadAsync<ProjectListPage>(client, List(new()))).Items.Single().ProjectId.ShouldBe("project-b");
        heads.Outage = true;
        ProjectListPage outage = await ReadAsync<ProjectListPage>(client, List(new()));
        outage.Items.ShouldBeEmpty(); outage.Snapshot.ResponseState.ShouldBe(AdmissionResponseState.Unavailable);
        heads.Outage = false;
        foreach (string malformed in new[] { "{}", "null", "[]", "{\"outcome\":null}", "{\"outcome\":999}", "{\"outcome\":0,\"Outcome\":0}", "{\"outcome\":\"Allowed\"}" })
        {
            transport.OwnerResponseOverride = malformed;
            ProjectListPage unverifiable = await ReadAsync<ProjectListPage>(client, List(new()));
            unverifiable.Items.ShouldBeEmpty(); unverifiable.Snapshot.ResponseState.ShouldBe(AdmissionResponseState.Unavailable);
            await StatusAsync(client, Open(new GetProjectQuery(), "project-b"), HttpStatusCode.NotFound);
        }
        transport.OwnerResponseOverride = null;
        foreach (HttpStatusCode status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound })
        {
            transport.OwnerStatusOverride = status;
            ProjectListPage ownerFailure = await ReadAsync<ProjectListPage>(client, List(new()));
            ownerFailure.Items.ShouldBeEmpty();
            ownerFailure.Snapshot.ResponseState.ShouldBe(status == HttpStatusCode.NotFound ? AdmissionResponseState.Complete : AdmissionResponseState.Unavailable);
        }
        transport.OwnerStatusOverride = null;
        IConfiguration signing = projects.Services.GetRequiredService<IConfiguration>();
        string? originalKey = signing["Authentication:BoundStreamRead:PrivateKey"];
        signing["Authentication:BoundStreamRead:PrivateKey"] = null;
        (await ReadAsync<ProjectListPage>(client, List(new()))).Snapshot.ResponseState.ShouldBe(AdmissionResponseState.Unavailable);
        signing["Authentication:BoundStreamRead:PrivateKey"] = originalKey;
        transport.TamperNextProjectQuery = true;
        await StatusAsync(client, Open(new GetProjectQuery(), "project-b"), HttpStatusCode.NotFound);
        transport.HumanBearerForwarded.ShouldBeFalse();
        transport.OwnerQueries.ShouldBeGreaterThan(0);
        await gateway.StopAsync(TestContext.Current.CancellationToken); await projects.StopAsync(TestContext.Current.CancellationToken); await folders.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WebApplicationBuilder Builder(string workload, string http, string grpc, GatewayFixtureLocalRoutes routes)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APP_API_TOKEN"] = Channel, ["Authentication:JwtBearer:Issuer"] = Issuer,
            ["Authentication:JwtBearer:Audience"] = Audience, ["Authentication:JwtBearer:SigningKey"] = SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256", ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:WorkloadIssuer:Workload"] = workload,
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_ => new DaprClientBuilder().UseHttpEndpoint(http).UseGrpcEndpoint(grpc).Build());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddAuthorization(); builder.Services.AddEventStoreWorkloadAssertionIssuer();
        builder.Services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new GatewayFixtureLoopbackHandler(routes));
        return builder;
    }

    private static async Task<WebApplication> StartProjectsAsync(string http, string grpc, GatewayFixtureLocalRoutes routes, RSA key, DateTimeOffset now)
    {
        var builder = Builder("projects", http, grpc, routes);
        builder.Configuration["Authentication:BoundStreamRead:PrivateKey"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        builder.Services.AddProjectsServer(); builder.Services.AddProjectsServerRuntimeInfrastructure();
        builder.Services.AddSingleton<Hexalith.Projects.Projections.TenantAccess.IUtcClock>(new ProjectReadTestClock(now.AddSeconds(1)));
        builder.Services.AddSingleton<TimeProvider>(new ProjectReadTestClock(now.AddSeconds(1)));
        WebApplication app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapProjectsServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false); return app;
    }

    private static async Task<WebApplication> StartFoldersAsync(string http, string grpc, GatewayFixtureLocalRoutes routes, GatewayFixtureOwnerHeads heads, DateTimeOffset now)
    {
        var builder = Builder("folders", http, grpc, routes);
        builder.Services.AddFoldersServer(); builder.Services.AddEventStoreReadModelStore();
        // Unused command services require a repository to compose; owner reads below use only persisted ACL projections.
        builder.Services.AddSingleton<IFolderRepository, InMemoryFolderRepository>();
        builder.Services.AddSingleton<Hexalith.Folders.Projections.TenantAccess.IUtcClock>(new Hexalith.Folders.Projections.TenantAccess.FixedUtcClock(now.AddSeconds(1)));
        builder.Services.AddSingleton<IOrganizationStreamSequenceReader>(heads);
        builder.Services.AddSingleton<EventStoreOrganizationEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<EventStoreFolderEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(sp => sp.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>());
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore, EventStoreFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, "Authentication:Workload",
            (options, _) => { options.Audience = "folders"; options.AllowedCallers.Add("eventstore"); });
        builder.Services.AddEventStoreWorkloadPolicies(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            [EventStoreWorkloadOperations.DomainServiceQuery, EventStoreWorkloadOperations.DomainServiceMetadata]);
        WebApplication app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapFoldersDomainServiceEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false); return app;
    }

    private static async Task<WebApplication> StartGatewayAsync(string http, string grpc, GatewayFixtureLocalRoutes routes, RSA key)
    {
        var builder = Builder("eventstore", http, grpc, routes);
        builder.Configuration["Authentication:BoundStreamRead:PublicKeys:projects"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        builder.Configuration["Authentication:BoundOwnerQuery:AllowedQueries:projects"] = FolderOwnerReadQuery.QueryType;
        builder.Configuration["Authentication:QueryAuthority:RequiredQueries:projects:0"] = ListProjectsQuery.QueryType;
        builder.Configuration["Authentication:QueryAuthority:RequiredQueries:projects:1"] = GetProjectQuery.QueryType;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = Audience,
                ValidateLifetime = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)) };
        }).AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal",
            (options, _) => { options.Audience = "eventstore"; options.AllowedCallers.Add("projects"); });
        builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName, [EventStoreWorkloadOperations.GatewayOwnerAuthorityQuery]);
        builder.Services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            options.DefaultPolicy = Hexalith.EventStore.Extensions.EventStoreServiceCollectionExtensions.CreateHumanDefaultPolicy());
        builder.Services.AddControllers().AddApplicationPart(typeof(QueriesController).Assembly);
        builder.Services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(QueryEnvelope).Assembly));
        builder.Services.AddTransient<MediatR.IRequestHandler<Hexalith.EventStore.Server.Pipeline.Queries.SubmitQuery, Hexalith.EventStore.Server.Pipeline.Queries.SubmitQueryResult>, SubmitQueryHandler>();
        builder.Services.AddSingleton<ITenantValidator, ClaimsTenantValidator>(); builder.Services.AddSingleton<IRbacValidator, ClaimsRbacValidator>();
        builder.Services.AddSingleton<IETagService, GatewayFixtureNoProjectionETag>();
        builder.Services.AddSingleton<IDomainServiceResolver, GatewayFixtureRoutesResolver>();
        builder.Services.AddSingleton<IDomainQueryHandlerRegistry, DaprDomainQueryHandlerRegistry>();
        builder.Services.AddSingleton<IDomainQueryInvoker, DaprDomainQueryInvoker>();
        builder.Services.AddSingleton<IQueryRouter>(sp => new HandlerAwareQueryRouter(new GatewayFixtureUnsupportedActorRouter(), sp.GetRequiredService<IDomainQueryHandlerRegistry>(),
            sp.GetRequiredService<IDomainQueryInvoker>(), sp.GetRequiredService<ILogger<HandlerAwareQueryRouter>>()));
        builder.Services.AddTransient<DomainServiceWorkloadAssertionHandler>();
        builder.Services.AddHttpClient(Options.DefaultName).AddHttpMessageHandler<DomainServiceWorkloadAssertionHandler>();
        builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<QueryNotFoundExceptionHandler>(); builder.Services.AddExceptionHandler<QueryExecutionFailedExceptionHandler>();
        WebApplication app = builder.Build(); app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false); return app;
    }

    private static string HumanToken(string tenant, bool inspection, bool projectUser = false)
    {
        List<Claim> claims = [new("sub", "actor-1"), new("tenant_id", tenant), new("eventstore:tenant", tenant), new("azp", projectUser ? "chatbot" : "operator-web"),
            new("client_id", "issuing-client"), new("act", "{\"sub\":\"delegation-1\"}"), new("scope", "projects.read projects.list"),
            new("projects:role", projectUser ? "ProjectUser" : "TenantOperator"), new("eventstore:permission", "query:read"),
            new("eventstore:permission", "read_metadata"), new("eventstore:permission", Hexalith.Projects.Server.ProjectAuthorizationGate.ReadProjectAction),
            new("eventstore:permission", Hexalith.Projects.Server.ProjectAuthorizationGate.ListProjectsAction)];
        if (inspection) claims.Add(new("eventstore:permission", ProjectReadQueryExecutor.InspectionPermission));
        var handler = new JwtSecurityTokenHandler(); handler.OutboundClaimTypeMap.Clear();
        return handler.CreateEncodedJwt(new SecurityTokenDescriptor { Issuer = Issuer, Audience = Audience,
            NotBefore = DateTime.UtcNow.AddSeconds(-1), Expires = DateTime.UtcNow.AddMinutes(5), Subject = new ClaimsIdentity(claims),
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256) });
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, SubmitQueryRequest query)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", query, TestContext.Current.CancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<SubmitQueryResponse>(body, JsonSerializerOptions.Web)!.Payload.Deserialize<T>(JsonSerializerOptions.Web)!;
    }
    private static async Task<string> StatusAsync(HttpClient client, SubmitQueryRequest query, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", query, TestContext.Current.CancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false); response.StatusCode.ShouldBe(expected, body); return body;
    }
}
#endif
