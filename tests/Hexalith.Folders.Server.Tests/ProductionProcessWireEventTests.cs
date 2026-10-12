using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class ProductionProcessWireEventTests
{
    [Fact]
    public async Task SignedWorkloadAndHumanReceiptAreAcceptedByProductionProcessRegistration()
    {
        const string workloadKey = "development-workload-signing-key-32-bytes-minimum";
        string receiptKey = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "TenantOwner"),
            },
            Watermark = 1,
            LastEventTimestamp = now,
            ProjectionWatermark = "tenant-a:1",
        }, TestContext.Current.CancellationToken);
        InMemoryEffectivePermissionsReadModel permissions = new();
        permissions.Save(new EffectivePermissionsReadModelSnapshot(
            "tenant-a", "tenant-a", "organization_baseline",
            EffectivePermissionsFolderLifecycleState.Active,
            [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                EffectivePermissionPrincipal.User("user-a"), "create_folder", 1, now)],
            new EffectivePermissionsFreshness("read_your_writes", now, "1", false, null),
            RevocationFreshnessEstablished: true, TaskScope: null));

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Configuration["Authentication:JwtBearer:Audience"] = "folders";
        builder.Configuration["Authentication:JwtBearer:Issuer"] = "hexalith-dev";
        builder.Configuration["Authentication:JwtBearer:SigningKey"] = workloadKey;
        builder.Configuration["Authentication:HumanAuthorityReceipt:SigningKey"] = receiptKey;
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, "Authentication:Workload",
            static (options, _) =>
            {
                options.Audience = "folders";
                options.AllowedCallers.Add("eventstore");
            });
        builder.Services.AddEventStoreWorkloadPolicies(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            [EventStoreWorkloadOperations.DomainServiceProcess]);
        builder.Services.AddSingleton<HumanAuthorityReceiptVerifier>();
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenants);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();

        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            DateTime issued = DateTime.UtcNow;
            string assertion = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
                new SecurityTokenDescriptor
                {
                    Issuer = "hexalith-dev",
                    Audience = "folders",
                    IssuedAt = issued,
                    NotBefore = issued,
                    Expires = issued.AddMinutes(2),
                    Claims = new Dictionary<string, object>
                    {
                        [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
                        [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProcess,
                    },
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(workloadKey)), SecurityAlgorithms.HmacSha256),
                });
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "eventstore");
            CommandEnvelope command = Command("folder-signed", FoldersServerModule.CreateFolderCommandType,
                "message-signed", new
                {
                    requestSchemaVersion = "v1",
                    folderId = "folder-signed",
                    folderMetadata = new { displayName = "Signed Folder" },
                });
            string receipt = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "user-a",
                ["create_folder"], receiptKey, now);

            AssertEvent(await ProcessAsync(client, command, null, receipt), typeof(FolderCreated));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ProcessRouteEmitsActorFoldedFolderOrganizationAndBindingEventsWithZeroEventReplay()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "TenantOwner"),
            },
            Watermark = 1,
            LastEventTimestamp = now,
            ProjectionWatermark = "tenant-a:1",
        }, TestContext.Current.CancellationToken);
        InMemoryEffectivePermissionsReadModel permissions = new();
        permissions.Save(new EffectivePermissionsReadModelSnapshot(
            "tenant-a", "tenant-a", "organization_baseline",
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "create_folder", 1, now),
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "manage_organization_acl", 1, now),
                new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "configure_provider_binding", 1, now),
            ],
            new EffectivePermissionsFreshness("read_your_writes", now, "1", false, null),
            RevocationFreshnessEstablished: true, TaskScope: null));

        ITenantContextAccessor tenantContext = new ProductionProcessTenantContextAccessor();
        IEventStoreClaimTransformEvidenceAccessor claims = new ProductionProcessClaimTransformEvidenceAccessor();

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton(tenantContext);
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton(claims);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenants);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddScoped<IFolderRepository, ActorFoldedFolderRepository>();
        builder.Services.RemoveAll<IOrganizationProviderBindingRepository>();
        builder.Services.RemoveAll<IOrganizationAclRepository>();
        builder.Services.AddScoped<ActorFoldedOrganizationProviderBindingRepository>();
        builder.Services.AddScoped<IOrganizationProviderBindingRepository>(service =>
            service.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());
        builder.Services.AddScoped<IOrganizationAclRepository>(service =>
            service.GetRequiredService<ActorFoldedOrganizationProviderBindingRepository>());

        await using WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            CommandEnvelope folderCommand = Command("folder-a", FoldersServerModule.CreateFolderCommandType,
                "message-folder", new
                {
                    requestSchemaVersion = "v1",
                    folderId = "folder-a",
                    folderMetadata = new { displayName = "Folder A" },
                });
            DomainServiceWireResult folder = await ProcessAsync(client, folderCommand, null);
            AssertEvent(folder, typeof(FolderCreated));
            DomainServiceCurrentState folderState = Retain(folderCommand, folder);
            AssertReplay(await ProcessAsync(client, folderCommand, folderState));

            CommandEnvelope baselineCommand = Command("tenant-a",
                FoldersServerModule.InitializeOrganizationAclBaselineCommandType, "message-baseline",
                new { requestSchemaVersion = "v1", operations = new[]
                    { new { principalKind = "User", principalId = "user-a", action = "create_folder" } } });
            DomainServiceWireResult baseline = await ProcessAsync(client, baselineCommand, null);
            baseline.IsRejection.ShouldBeFalse();
            baseline.Events.Select(item => item.EventTypeName).ShouldBe([
                typeof(OrganizationAclBaselineInitialized).FullName,
                typeof(OrganizationAclPrincipalGranted).FullName,
            ]);
            foreach (DomainServiceWireEvent item in baseline.Events)
            {
                item.PayloadVersion.ShouldBeNull();
            }
            DomainServiceCurrentState baselineState = Retain(baselineCommand, baseline);
            AssertReplay(await ProcessAsync(client, baselineCommand, baselineState));

            CommandEnvelope grantCommand = Command("tenant-a",
                FoldersServerModule.GrantOrganizationAclPrincipalCommandType, "message-grant",
                new { requestSchemaVersion = "v1", operations = new[]
                    { new { principalKind = "User", principalId = "user-a", action = "configure_provider_binding" } } });
            DomainServiceWireResult grant = await ProcessAsync(client, grantCommand, baselineState);
            AssertEvent(grant, typeof(OrganizationAclPrincipalGranted));
            AssertReplay(await ProcessAsync(client, grantCommand, Retain(grantCommand, grant, baselineState)));

            CommandEnvelope bindingCommand = Command(ProviderBindingActorId.Create("binding-a"),
                FoldersServerModule.ConfigureProviderBindingCommandType, "message-binding",
                new
                {
                    requestSchemaVersion = "v1",
                    providerBindingRef = "binding-a",
                    providerFamilyRef = "github",
                    nonSecretCredentialReference = "credential-ref-a",
                });
            DomainServiceWireResult binding = await ProcessAsync(client, bindingCommand, null);
            AssertEvent(binding, typeof(ProviderBindingConfigured));
            AssertReplay(await ProcessAsync(client, bindingCommand, Retain(bindingCommand, binding)));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static CommandEnvelope Command(string actorId, string type, string messageId, object payload)
        => new(messageId, "tenant-a", "folders", actorId, type,
            JsonSerializer.SerializeToUtf8Bytes(payload), "correlation-a", null, "user-a",
            new Dictionary<string, string> { ["taskId"] = "task-a" });

    private static async Task<DomainServiceWireResult> ProcessAsync(HttpClient client, CommandEnvelope command,
        DomainServiceCurrentState? state, string? humanAuthorityReceipt = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(command, state) { HumanAuthorityReceipt = humanAuthorityReceipt },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        return (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>(
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
    }

    private static void AssertEvent(DomainServiceWireResult result, Type eventType)
    {
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(eventType.FullName);
        result.Events[0].PayloadVersion.ShouldBeNull();
    }

    private static void AssertReplay(DomainServiceWireResult result)
    {
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        using JsonDocument payload = JsonDocument.Parse(result.ResultPayload.ShouldNotBeNull());
        payload.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
    }

    private static DomainServiceCurrentState Retain(CommandEnvelope command, DomainServiceWireResult result,
        DomainServiceCurrentState? prior = null)
    {
        List<EventEnvelope> events = [.. prior?.Events ?? []];
        foreach (DomainServiceWireEvent item in result.Events)
        {
            long sequence = events.Count + 1;
            EventMetadata metadata = new(command.MessageId, command.AggregateId, "folders",
                command.TenantId, command.Domain, sequence, sequence, DateTimeOffset.UtcNow,
                command.CorrelationId, command.MessageId, command.UserId, "test", item.EventTypeName,
                item.MetadataVersion ?? 1, item.SerializationFormat)
            {
                PayloadVersion = item.PayloadVersion,
                EventContractType = item.EventContractType,
            };
            events.Add(new EventEnvelope(metadata, item.Payload, null));
        }

        return new DomainServiceCurrentState(null, events, 0, events.Count);
    }
}
