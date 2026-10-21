namespace Hexalith.Projects.Server.Tests.Queries;

using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Projects.Authorization;
using Hexalith.Projects.Contracts.Events;
using Hexalith.Projects.Contracts.Models;
using Hexalith.Projects.Contracts.Queries;
using Hexalith.Projects.Contracts.Ui;
using Hexalith.Projects.Projections.TenantAccess;
using Hexalith.Projects.Server.Projections.ProjectReads;
using Hexalith.Projects.Server.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

internal sealed class SupportedProjectReadFixture
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly DateTimeOffset EventTime = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    public InMemoryReadModelStore Store { get; } = new();
    public ProjectReadTestClock Clock { get; } = new(EventTime.AddSeconds(1));
    public ProjectReadTestFolders Folders { get; } = new();
    public ProjectReadTestAuthorization Authorization { get; } = new();
    public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();
    public DefaultHttpContext Http { get; } = new();
    public InMemoryProjectTenantAccessProjectionStore Tenants { get; } = new();
    public ProjectReadTestAuditStore AuditStore { get; private set; } = null!;
    public ProjectReadQueryExecutor Executor { get; private set; } = null!;
    public ProjectReadProjectionHandler Projection => new(Store, Store);
    public string Workload { get; private set; } = "operator-web";
    public ClaimsIdentity Identity => (ClaimsIdentity)Http.User.Identity!;

    public static async Task<SupportedProjectReadFixture> CreateAsync(bool chatbot = false, bool inspection = false)
    {
        SupportedProjectReadFixture fixture = new() { Workload = chatbot ? "chatbot" : "operator-web" };
        List<Claim> claims =
        [
            new("sub", "actor-1"), new("tenantId", "tenant-a"), new("azp", fixture.Workload),
            new("client_id", "issuing-client"), new("act", "{\"sub\":\"delegation-1\"}"),
            new("scope", "projects.read projects.list"), new("aud", "configured-read-audience"),
            new("eventstore:permission", ProjectAuthorizationGate.ReadProjectAction),
            new("eventstore:permission", ProjectAuthorizationGate.ListProjectsAction),
            new("projects:role", chatbot ? "ProjectUser" : "TenantOperator"),
        ];
        if (inspection) { claims.Add(new("eventstore:permission", ProjectReadQueryExecutor.InspectionPermission)); }
        fixture.Http.User = new(new ClaimsIdentity(claims, "authenticated-test"));
        FixedProjectQueryHttpContextAccessor http = new(fixture.Http);
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Audience"] = "configured-read-audience",
        }).Build();
        IHostEnvironment environment = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production }).Environment;
        ProjectQueryEnvelopePrincipalBinding binding = new(http, new HttpContextProjectTenantContextAccessor(http), config, environment);
        ProjectTenantAccessProjection tenant = new()
        {
            TenantId = "tenant-a", Enabled = true, Watermark = 1,
            ProjectionWatermark = "tenant-a:1", LastEventTimestamp = EventTime,
        };
        tenant.Principals["actor-1"] = new("actor-1", "TenantOwner");
        await fixture.Tenants.SaveAsync(tenant, TestContext.Current.CancellationToken).ConfigureAwait(false);
        ProjectAuthorizationGate gate = new(new TenantAccessAuthorizer(fixture.Tenants, fixture.Clock, new TenantAccessOptions()),
            fixture.Authorization, new AllowingProjectDaprPolicyEvidenceProvider(), new InMemoryProjectDetailReadModel());
        fixture.AuditStore = new(fixture.Store);
        fixture.Executor = new(fixture.Store, gate, binding, fixture.Folders,
            new QueryCursorCodec(fixture.DataProtection, "story61-tests"), new ProjectReadAudit(fixture.AuditStore, fixture.Clock), fixture.Clock);
        return fixture;
    }

    public QueryEnvelope Query(object input, string projectId = "projects", string correlation = "read-request")
        => new("tenant-a", "projects", projectId, input is ListProjectsQuery ? ListProjectsQuery.QueryType : GetProjectQuery.QueryType,
            JsonSerializer.SerializeToUtf8Bytes(input, Json), correlation, "actor-1")
        {
            OriginalActorId = "actor-1", AuthenticatedWorkloadId = Workload, IsDelegated = true, DelegationId = "delegation-1",
            Scopes = ["projects.read", "projects.list"], Audience = ["configured-read-audience"],
        };

    public async Task SeedAsync(string id, long position = 100, bool folder = true)
    {
        ProjectionRequest request = Request(id, position, folder);
        await Projection.ProjectAsync(request, $"seed-{id}-{position}", TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public static ProjectionRequest Request(string id, long position = 100, bool folder = true)
    {
        ProjectCreated created = new("tenant-a", id, id.Length > 100 ? "Long identifier project" : $"Project {id}", "Safe description", null, ProjectLifecycle.Active,
            "actor-1", "create", "task", "create", "create", EventTime);
        List<ProjectionEventDto> events = [Event(created, 1, position)];
        if (folder)
        {
            ProjectFolderSet assigned = new("tenant-a", id, id.Length > 100 ? "folder-long-identifier" : $"folder-{id.Replace('.', '-')}", new ProjectFolderMetadata("Folder"),
                "actor-1", "folder", "task", "folder", "folder", EventTime);
            events.Add(Event(assigned, 2, position + 1));
        }

        return new("tenant-a", "projects", id, [.. events]);
    }

    public static ProjectionEventDto Event(object value, long sequence, long position)
        => new(value.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), Json),
            "json", sequence, EventTime, "projection", $"event-{position}", "actor-1", position);

    public static T Payload<T>(QueryResult result) => JsonSerializer.Deserialize<T>(result.PayloadBytes!, Json)!;
}
