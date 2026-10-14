using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
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
            [EventStoreWorkloadOperations.DomainServiceProcess, EventStoreWorkloadOperations.DomainServiceProject]);
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

            ProjectionEventDto retained = new(typeof(FolderCreated).FullName!,
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "event-evolution",
                    "folder-created-valid-unstamped-v1.json")), "json", 1, now, "correlation-a")
            { MetadataVersion = 1 };
            ProjectionRequest project = new("tenant-a", "folders", "folder-a", [retained]);
            client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            (await client.PostAsJsonAsync(FoldersServerModule.ProjectRoute, project,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            (await client.PostAsJsonAsync(FoldersServerModule.ProjectRoute, project,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            string projectAssertion = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
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
                        [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProject,
                    },
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(workloadKey)), SecurityAlgorithms.HmacSha256),
                });
            client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, projectAssertion);
            using HttpResponseMessage projected = await client.PostAsJsonAsync(FoldersServerModule.ProjectRoute,
                project, TestContext.Current.CancellationToken);
            projected.StatusCode.ShouldBe(HttpStatusCode.OK);
            ProjectionResponse rebuilt = (await projected.Content.ReadFromJsonAsync<ProjectionResponse>(
                TestContext.Current.CancellationToken))!;
            rebuilt.State.GetProperty("isCreated").GetBoolean().ShouldBeTrue();
            rebuilt.State.GetProperty("displayName").GetString().ShouldBe("Folder A");
            foreach (string unknown in new[] { "Unregistered.Namespace.FolderCreated", "FolderCreated" })
            {
                using HttpResponseMessage rejected = await client.PostAsJsonAsync(FoldersServerModule.ProjectRoute,
                    project with { Events = [retained with { EventTypeName = unknown }] }, TestContext.Current.CancellationToken);
                rejected.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
                (await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain(unknown);
            }
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(ProviderFailureCategory.ProviderUnavailable, FolderResultCode.ProviderUnavailable, "provider_unavailable")]
    [InlineData(ProviderFailureCategory.ProviderRateLimited, FolderResultCode.ProviderRateLimited, "provider_rate_limited")]
    [InlineData(ProviderFailureCategory.UnknownProviderOutcome, FolderResultCode.UnknownProviderOutcome, "unknown_provider_outcome")]
    [InlineData(ProviderFailureCategory.ReconciliationRequired, FolderResultCode.ReconciliationRequired, "reconciliation_required")]
    public async Task ProcessRouteEmitsActorFoldedFolderOrganizationAndBindingEventsWithZeroEventReplay(
        ProviderFailureCategory failureCategory, FolderResultCode expectedFailure, string expectedAuditCategory)
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
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<ScopeCheckingFolderPermissionEvidenceProvider>(service => new(
            new EffectivePermissionsFolderPermissionEvidenceProvider(permissions, service.GetRequiredService<IUtcClock>())));
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(service =>
            service.GetRequiredService<ScopeCheckingFolderPermissionEvidenceProvider>());
        builder.Services.RemoveAll<IRepositoryBindingReadinessValidator>();
        builder.Services.AddSingleton<IRepositoryBindingReadinessValidator, ProcessBindingReadinessValidator>();
        builder.Services.RemoveAll<IWorkspacePreparationReadinessValidator>();
        builder.Services.AddSingleton<IWorkspacePreparationReadinessValidator>(new ProcessWorkspaceReadinessValidator(
            failureCategory == ProviderFailureCategory.ReconciliationRequired
                ? failureCategory : ProviderFailureCategory.UnknownProviderOutcome));
        builder.Services.RemoveAll<IProviderReadinessBindingReader>();
        builder.Services.AddSingleton<IProviderReadinessBindingReader, ProcessProviderBindingReader>();
        InMemoryFolderAuditObserver audit = new();
        builder.Services.AddSingleton<IFolderAuditObserver>(audit);
        RecordingProviderCapabilityResolver provider = new(FakeGitProvider.Failing(failureCategory));
        builder.Services.RemoveAll<IProviderCapabilityResolver>();
        builder.Services.AddSingleton<IProviderCapabilityResolver>(provider);

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
            foreach ((string Actor, string Type) invalid in new[]
            {
                ("folder-a", FoldersServerModule.ConfigureProviderBindingCommandType),
                ("tenant-a", FoldersServerModule.CreateFolderCommandType),
                ("organization_baseline", FoldersServerModule.CreateFolderCommandType),
                (ProviderBindingActorId.Create("binding-a"), FoldersServerModule.CreateFolderCommandType),
                ("folder-a", FoldersServerModule.GrantOrganizationAclPrincipalCommandType),
                ("folder-a", FoldersServerModule.RevokeOrganizationAclPrincipalCommandType),
            })
            {
                CommandEnvelope misrouted = Command(invalid.Actor, invalid.Type, "misrouted-message", new { });
                using HttpResponseMessage denied = await client.PostAsJsonAsync(FoldersServerModule.ProcessRoute,
                    new DomainServiceRequest(misrouted, null), TestContext.Current.CancellationToken);
                denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                using JsonDocument problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                problem.RootElement.GetProperty("retryable").GetBoolean().ShouldBeFalse();
                problem.RootElement.TryGetProperty("events", out _).ShouldBeFalse();
                denied.Headers.Contains("X-Hexalith-Authorization-Denial").ShouldBeTrue();
            }
            DomainServiceWireResult folder = await ProcessAsync(client, folderCommand, null);
            AssertEvent(folder, typeof(FolderCreated));
            DomainServiceCurrentState folderState = Retain(folderCommand, folder);
            AssertReplay(await ProcessAsync(client, folderCommand, folderState));

            permissions.Save(new EffectivePermissionsReadModelSnapshot("tenant-a", "tenant-a", "folder-a",
                EffectivePermissionsFolderLifecycleState.Active,
                [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "manage_folder_access", 1, now)],
                new("read_your_writes", now, "1", false, null), true, null));
            object[] aclOperations =
            [
                new { principalKind = "user", principalId = "user-b", action = "read_metadata" },
                new { principalKind = "group", principalId = "group-a", action = "mutate_files" },
                new { principalKind = "user", principalId = "user-c", action = "configure_branch_ref_policy" },
            ];
            (FolderAccessPrincipalKind Kind, string PrincipalId, string Action)[] expectedTuples =
            [
                (FolderAccessPrincipalKind.User, "user-b", "read_metadata"),
                (FolderAccessPrincipalKind.Group, "group-a", "mutate_files"),
                (FolderAccessPrincipalKind.User, "user-c", "configure_branch_ref_policy"),
            ];
            CommandEnvelope folderGrantCommand = Command("folder-a", FoldersServerModule.GrantFolderAccessCommandType,
                "message-folder-grant", new { operations = aclOperations });
            DomainServiceWireResult folderGrant = await ProcessAsync(client, folderGrantCommand, folderState);
            folderGrant.IsRejection.ShouldBeFalse(string.Join("; ", folderGrant.Events.Select(item => Encoding.UTF8.GetString(item.Payload))));
            folderGrant.Events.Count.ShouldBe(3);
            FolderAccessGranted[] grants = folderGrant.Events.Select(item =>
            {
                item.EventTypeName.ShouldBe(typeof(FolderAccessGranted).FullName);
                item.PayloadVersion.ShouldBeNull();
                return JsonSerializer.Deserialize<FolderAccessGranted>(item.Payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            }).ToArray();
            grants.Select(item => (item.PrincipalKind, item.PrincipalId, item.Action))
                .ShouldBe(expectedTuples, ignoreOrder: true);
            grants.Select(item => item.AccessSequence).ShouldBe([1L, 2L, 3L]);
            foreach (FolderAccessGranted item in grants)
            {
                AssertFolderAclPayload(item, folderGrantCommand);
                item.ActorPrincipalId.ShouldBe("user-a");
            }
            DomainServiceCurrentState grantedState = Retain(folderGrantCommand, folderGrant, folderState);
            FolderEventReplay actorReplay = app.Services.GetRequiredService<FolderEventReplay>();
            FolderState granted = actorReplay.FoldFolder(grantedState, "tenant-a", "folder-a");
            granted.AccessOverrides.Count.ShouldBe(3);
            granted.AccessOverrides.Values.ShouldAllBe(item => item.IsGranted);
            granted.AccessOverrides.Keys.Select(key => (key.PrincipalKind, key.PrincipalId, key.Action))
                .ShouldBe(expectedTuples, ignoreOrder: true);
            DomainServiceWireResult grantReplay = await ProcessAsync(client, folderGrantCommand with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { operations = aclOperations.Reverse().ToArray() }),
            }, grantedState);
            AssertFolderAclBatchReplay(grantReplay, folderGrantCommand.MessageId);
            AssertFoldedAclUnchanged(granted,
                actorReplay.FoldFolder(Retain(folderGrantCommand, grantReplay, grantedState), "tenant-a", "folder-a"));

            CommandEnvelope folderRevokeCommand = Command("folder-a", FoldersServerModule.RevokeFolderAccessCommandType,
                "message-folder-revoke", new { operations = aclOperations });
            DomainServiceWireResult folderRevoke = await ProcessAsync(client, folderRevokeCommand, grantedState);
            folderRevoke.IsRejection.ShouldBeFalse();
            folderRevoke.Events.Count.ShouldBe(3);
            FolderAccessRevoked[] revokes = folderRevoke.Events.Select(item =>
            {
                item.EventTypeName.ShouldBe(typeof(FolderAccessRevoked).FullName);
                item.PayloadVersion.ShouldBeNull();
                return JsonSerializer.Deserialize<FolderAccessRevoked>(item.Payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            }).ToArray();
            revokes.Select(item => (item.PrincipalKind, item.PrincipalId, item.Action))
                .ShouldBe(expectedTuples, ignoreOrder: true);
            revokes.Select(item => item.AccessSequence).ShouldBe([4L, 5L, 6L]);
            foreach (FolderAccessRevoked item in revokes)
            {
                AssertFolderAclPayload(item, folderRevokeCommand);
                item.ActorPrincipalId.ShouldBe("user-a");
            }
            DomainServiceCurrentState revokedState = Retain(folderRevokeCommand, folderRevoke, grantedState);
            FolderState revoked = actorReplay.FoldFolder(revokedState, "tenant-a", "folder-a");
            revoked.AccessOverrides.Count.ShouldBe(3);
            revoked.AccessOverrides.Values.ShouldAllBe(item => !item.IsGranted);
            revoked.AccessOverrides.Keys.Select(key => (key.PrincipalKind, key.PrincipalId, key.Action))
                .ShouldBe(expectedTuples, ignoreOrder: true);
            DomainServiceWireResult revokeReplay = await ProcessAsync(client, folderRevokeCommand with
            {
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { operations = aclOperations.Reverse().ToArray() }),
            }, revokedState);
            AssertFolderAclBatchReplay(revokeReplay, folderRevokeCommand.MessageId);
            AssertFoldedAclUnchanged(revoked,
                actorReplay.FoldFolder(Retain(folderRevokeCommand, revokeReplay, revokedState), "tenant-a", "folder-a"));

            CommandEnvelope baselineCommand = Command("tenant-a",
                FoldersServerModule.InitializeOrganizationAclBaselineCommandType, "message-baseline",
                new
                {
                    requestSchemaVersion = "v1",
                    operations = new[]
                    { new { principalKind = "User", principalId = "user-a", action = "create_folder" } }
                });
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
            DomainServiceWireResult duplicateBaseline = await ProcessAsync(client, baselineCommand with
            {
                MessageId = "message-baseline-new-key",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    operations = new[]
                    { new { principalKind = "User", principalId = "attacker-a", action = "manage_organization_acl" } }
                }),
            }, baselineState);
            duplicateBaseline.IsRejection.ShouldBeTrue();
            JsonSerializer.Deserialize<JsonElement>(duplicateBaseline.Events.ShouldHaveSingleItem().Payload)
                .GetProperty("Code").GetString().ShouldBe(nameof(FolderResultCode.DuplicateEntry));
            app.Services.GetRequiredService<FolderEventReplay>()
                .FoldOrganization(Retain(baselineCommand, duplicateBaseline, baselineState), "tenant-a", "tenant-a")
                .Grants.ShouldNotContain(grant => grant.PrincipalId == "attacker-a");

            CommandEnvelope grantCommand = Command("tenant-a",
                FoldersServerModule.GrantOrganizationAclPrincipalCommandType, "message-grant",
                new
                {
                    requestSchemaVersion = "v1",
                    operations = new[]
                    { new { principalKind = "User", principalId = "user-a", action = "configure_provider_binding" } }
                });
            DomainServiceWireResult grant = await ProcessAsync(client, grantCommand, baselineState);
            AssertEvent(grant, typeof(OrganizationAclPrincipalGranted));
            foreach (string action in new[] { "create_repository_backed_folder", "bind_repository", "configure_branch_ref_policy" })
            {
                CommandEnvelope publishedAction = Command("tenant-a", FoldersServerModule.GrantOrganizationAclPrincipalCommandType,
                    "message-" + action, new
                    {
                        operations = new[]
                        { new { principalKind = "User", principalId = "user-a", action } }
                    });
                DomainServiceWireResult acceptedAction = await ProcessAsync(client, publishedAction, baselineState);
                AssertEvent(acceptedAction, typeof(OrganizationAclPrincipalGranted));
                JsonSerializer.Deserialize<OrganizationAclPrincipalGranted>(acceptedAction.Events[0].Payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Action.ShouldBe(action);
            }
            DomainServiceCurrentState organizationGrantState = Retain(grantCommand, grant, baselineState);
            AssertReplay(await ProcessAsync(client, grantCommand, organizationGrantState));
            CommandEnvelope organizationRevoke = grantCommand with
            {
                CommandType = FoldersServerModule.RevokeOrganizationAclPrincipalCommandType,
                MessageId = "message-organization-revoke",
            };
            AssertEvent(await ProcessAsync(client, organizationRevoke, organizationGrantState),
                typeof(OrganizationAclPrincipalRevoked));
            foreach ((string Message, string Classification) expected in new[]
            {
                ("message-baseline", "initialize_organization_acl_baseline"),
                ("message-grant", "grant_organization_acl_principal"),
                ("message-organization-revoke", "revoke_organization_acl_principal"),
            })
            {
                FolderAuditObservation observed = audit.Observations.Single(item => item.OperationId == expected.Message);
                observed.Classifications["command.type"].ShouldBe(expected.Classification);
                observed.Result.ShouldBe(FolderAuditResult.Success);
                observed.SanitizedCategory.ShouldBe("accepted");
            }

            CommandEnvelope delegated = Command("tenant-a", FoldersServerModule.GrantOrganizationAclPrincipalCommandType,
                "message-delegated", new
                {
                    operations = new[]
                    { new { principalKind = "delegated_service_agent", principalId = "agent-a", action = "read_metadata" } }
                });
            DomainServiceWireResult delegatedResult = await ProcessAsync(client, delegated, baselineState);
            AssertEvent(delegatedResult, typeof(OrganizationAclPrincipalGranted));
            JsonSerializer.Deserialize<OrganizationAclPrincipalGranted>(delegatedResult.Events[0].Payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!
                .PrincipalKind.ShouldBe(OrganizationAclPrincipalKind.DelegatedServiceAgent);
            DomainServiceWireResult numeric = await ProcessAsync(client, delegated with
            {
                MessageId = "message-numeric",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    operations = new[]
                    { new { principalKind = "3", principalId = "agent-a", action = "read_metadata" } }
                }),
            }, baselineState);
            numeric.IsRejection.ShouldBeTrue();
            numeric.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(FolderCommandRejected).FullName);

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

            permissions.Save(new EffectivePermissionsReadModelSnapshot("tenant-a", "tenant-a", "folder-a",
                EffectivePermissionsFolderLifecycleState.Active,
                [new(EffectivePermissionEvidenceSource.OrganizationBaselineGrant,
                    EffectivePermissionPrincipal.User("user-a"), "bind_repository", 1, now)],
                new("read_your_writes", now, "1", false, null), true, null));
            CommandEnvelope bindCommand = Command("folder-a", FoldersServerModule.BindRepositoryCommandType,
                "message-bind", new
                {
                    requestSchemaVersion = "v1",
                    providerBindingRef = "binding-a",
                    externalRepositoryRef = "repository-a",
                    branchRefPolicy = new { requestSchemaVersion = "v1", repositoryBindingId = "repository-binding-a", policyRef = "policy-a" },
                });
            DomainServiceWireResult failedBinding = await ProcessAsync(client, bindCommand, folderState);
            failedBinding.IsRejection.ShouldBeFalse(string.Join("; ", failedBinding.Events.Select(item => Encoding.UTF8.GetString(item.Payload))));
            failedBinding.Events.Select(item => item.EventTypeName).ShouldBe([
                typeof(ExistingRepositoryBindingRequested).FullName,
                failureCategory is ProviderFailureCategory.UnknownProviderOutcome or ProviderFailureCategory.ReconciliationRequired
                    ? typeof(ProviderOutcomeUnknown).FullName : typeof(RepositoryBindingFailed).FullName,
            ]);
            using JsonDocument failedPayload = JsonDocument.Parse(failedBinding.ResultPayload!);
            failedPayload.RootElement.GetProperty("failureCode").GetString().ShouldBe(expectedFailure.ToString());
            FolderAuditObservation outcomeAudit = audit.Observations.Single(item => item.OperationId == bindCommand.MessageId);
            outcomeAudit.Result.ShouldBe(FolderAuditResult.Failed);
            outcomeAudit.SanitizedCategory.ShouldBe(expectedAuditCategory);
            outcomeAudit.RedactionState.ShouldBe(FolderAuditRedactionState.MetadataOnly);
            DomainServiceCurrentState failedRetained = Retain(bindCommand, failedBinding, folderState);
            FolderState failedState = actorReplay.FoldFolder(failedRetained, "tenant-a", "folder-a");
            failedState.RepositoryBindingState.ShouldBe(
                failureCategory == ProviderFailureCategory.ReconciliationRequired ? FolderRepositoryBindingState.ReconciliationRequired
                    : failureCategory == ProviderFailureCategory.UnknownProviderOutcome ? FolderRepositoryBindingState.UnknownProviderOutcome
                    : FolderRepositoryBindingState.Failed);
            AssertReplay(await ProcessAsync(client, bindCommand, failedRetained));
            provider.ProviderCalls.ShouldBe(1);
            // PrepareWorkspace must persist its outcome fact even though the business result is a failure.
            FolderState preparationState = FolderState.Empty with
            {
                IsCreated = true,
                ManagedTenantId = "tenant-a",
                OrganizationId = "tenant-a",
                FolderId = "folder-a",
                LifecycleState = FolderLifecycleState.Active,
                RepositoryBindingState = FolderRepositoryBindingState.Bound,
                WorkspaceLifecycleState = FolderWorkspaceLifecycleState.Preparing,
                RepositoryBindingId = "repository-binding-a",
                ProviderBindingRef = "binding-a",
                BranchRefPolicyRef = "policy-a",
                BranchRefPolicy = new("repository-binding-a", "policy-a", "main", [], [], "user-a", "corr", "task-a",
                    "policy-key", "policy-fingerprint", now),
            };
            DomainServiceCurrentState preparationSnapshot = new(JsonSerializer.SerializeToElement(preparationState), [], 1, 1);
            permissions.Save(new("tenant-a", "tenant-a", "folder-a", EffectivePermissionsFolderLifecycleState.Active,
                [new(EffectivePermissionEvidenceSource.FolderOverrideGrant, EffectivePermissionPrincipal.User("user-a"),
                    "prepare_workspace", 1, now)], new("read_your_writes", now, "1", false, null), true, null));
            CommandEnvelope prepareCommand = Command("folder-a", FoldersServerModule.PrepareWorkspaceCommandType,
                "message-prepare", new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = "workspace-a",
                    repositoryBindingId = "repository-binding-a",
                    branchRefPolicyRef = "policy-a",
                    workspacePolicyRef = "workspace-policy-a"
                });
            DomainServiceWireResult preparation = await ProcessAsync(client, prepareCommand, preparationSnapshot);
            preparation.IsRejection.ShouldBeFalse(string.Join("; ", preparation.Events.Select(item => Encoding.UTF8.GetString(item.Payload))));
            AssertEvent(preparation, typeof(FolderWorkspaceLifecycleEventRecorded));
            using JsonDocument preparationPayload = JsonDocument.Parse(preparation.ResultPayload!);
            preparationPayload.RootElement.GetProperty("failureCode").GetString().ShouldBe(
                (failureCategory == ProviderFailureCategory.ReconciliationRequired ? FolderResultCode.ReconciliationRequired
                    : FolderResultCode.UnknownProviderOutcome).ToString());
            FolderWorkspaceLifecycleEventRecorded unknown = JsonSerializer.Deserialize<FolderWorkspaceLifecycleEventRecorded>(
                preparation.Events[0].Payload, EventStorePayloadSerialization.Options)!;
            unknown.WorkspaceLifecycleEvent.ShouldBe(FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown);
            FolderState retainedPreparation = preparationState.Apply([unknown], FolderStreamName.Create("tenant-a", "folder-a"));
            retainedPreparation.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.UnknownProviderOutcome);
            DomainServiceCurrentState preparationReplayState = new(JsonSerializer.SerializeToElement(retainedPreparation), [], 2, 2);
            AssertReplay(await ProcessAsync(client, prepareCommand, preparationReplayState));
            provider.ProviderCalls.ShouldBe(1);

            ScopeCheckingFolderPermissionEvidenceProvider sequenceEvidence = app.Services
                .GetRequiredService<ScopeCheckingFolderPermissionEvidenceProvider>();
            sequenceEvidence.ObservedHeads.ShouldContain(1L);
            sequenceEvidence.ObservedHeads.ShouldContain(2L);
            sequenceEvidence.ObservedHeads.ShouldContain(4L);
            FolderActorSequenceScope.TryGet("tenant-a", "folder-a", out _).ShouldBeFalse();
            OrganizationActorSequenceScope.TryGet("tenant-a", "tenant-a", out _).ShouldBeFalse();
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

    private static void AssertFolderAclPayload(IFolderEvent item, CommandEnvelope command)
    {
        item.ManagedTenantId.ShouldBe(command.TenantId);
        item.OrganizationId.ShouldBe(command.TenantId);
        item.FolderId.ShouldBe(command.AggregateId);
        item.CorrelationId.ShouldBe(command.CorrelationId);
        item.TaskId.ShouldBe(command.Extensions!["taskId"]);
        item.IdempotencyKey.ShouldBe(command.MessageId);
        item.IdempotencyFingerprint.ShouldNotBeNullOrWhiteSpace();
        item.OccurredAt.ShouldNotBe(default);
    }

    private static void AssertReplay(DomainServiceWireResult result)
    {
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        using JsonDocument payload = JsonDocument.Parse(result.ResultPayload.ShouldNotBeNull());
        payload.RootElement.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
    }

    private static void AssertFolderAclBatchReplay(DomainServiceWireResult result, string idempotencyKey)
    {
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        using JsonDocument payload = JsonDocument.Parse(result.ResultPayload.ShouldNotBeNull());
        payload.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
        payload.RootElement.GetProperty("idempotencyKey").GetString().ShouldBe(idempotencyKey);
    }

    private static void AssertFoldedAclUnchanged(FolderState expected, FolderState actual)
    {
        actual.AccessSequence.ShouldBe(expected.AccessSequence);
        actual.IdempotencyFingerprints.ShouldBe(expected.IdempotencyFingerprints);
        // Compare retained values structurally, including each revocation history list.
        JsonSerializer.SerializeToUtf8Bytes(actual.AccessOverrides.OrderBy(item => item.Key.CanonicalValue))
            .ShouldBe(JsonSerializer.SerializeToUtf8Bytes(expected.AccessOverrides.OrderBy(item => item.Key.CanonicalValue)));
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
