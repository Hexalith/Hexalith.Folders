using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Workers;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class WorkersOrganizationAclRouteTests
{
    [Fact]
    public async Task AuthenticatedBaselineAndGrantShouldReachSharedPermissionState()
    {
        WorkerRouteReadModelStore store = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.AddSingleton<IReadModelStore>(store);
        await using WebApplication app = builder.Build();
        app.UseCloudEvents();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSubscribeHandler();
        app.MapFoldersTenantEventWorkerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            OrganizationAclBaselineInitialized baseline = new("tenant-a", "tenant-a", "correlation-a",
                "task-a", "key-a", "fingerprint-a");
            EventStoreDomainEventEnvelope first = Envelope("message-a", 1, baseline);
            (await client.PostAsJsonAsync("/folders/events", first,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "wrong-sidecar-token");
            (await client.PostAsJsonAsync("/folders/events", first,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await store.GetAsync<OrganizationPermissionProjection>("statestore",
                "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            (await client.PostAsJsonAsync("/folders/events", first,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            OrganizationAclPrincipalGranted grant = new("tenant-a", "tenant-a",
                OrganizationAclPrincipalKind.User, "user-a", "create_folder", "correlation-a",
                "task-a", "key-b", "fingerprint-b");
            (await client.PostAsJsonAsync("/folders/events", Envelope("message-b", 2, grant),
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

            ReadModelEntry<OrganizationPermissionProjection> persisted = await store
                .GetAsync<OrganizationPermissionProjection>("statestore",
                    "projection:folders:organization-permissions:tenant-a",
                    TestContext.Current.CancellationToken);
            persisted.Value.ShouldNotBeNull();
            persisted.Value.LastSequence.ShouldBe(2);
            persisted.Value.Grants.ShouldHaveSingleItem().Action.ShouldBe("create_folder");

            byte[] retainedCreate = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
                "event-evolution", "folder-created-valid-unstamped-v1.json"));
            byte[] retainedDigest = SHA256.HashData(retainedCreate);
            FolderCreated created = JsonSerializer.Deserialize<FolderCreated>(retainedCreate,
                EventStorePayloadSerialization.Options)!;
            EventStoreDomainEventEnvelope folderEnvelope = new("message-folder-a", "folder-a", "tenant-a",
                typeof(FolderCreated).FullName!, 1, DateTimeOffset.UtcNow, "correlation-a", "json",
                retainedCreate)
            {
                Domain = "folders",
                MetadataVersion = 1,
            };
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            ReadModelEntry<FolderPermissionProjection> folder = await store
                .GetAsync<FolderPermissionProjection>("statestore",
                    "projection:folders:folder-permissions:tenant-a:folder-a",
                    TestContext.Current.CancellationToken);
            folder.Value.ShouldNotBeNull();
            folder.Value.IsCreated.ShouldBeTrue();
            folder.Value.LastSequence.ShouldBe(1);

            // Repair an acknowledged but missing row by replaying the identical retained
            // EventStore envelope through the authenticated subscription path.
            const string folderProjectionKey = "projection:folders:folder-permissions:tenant-a:folder-a";
            store.Remove(folderProjectionKey);
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await store.GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await store.GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                TestContext.Current.CancellationToken)).Value!.LastSequence.ShouldBe(1);

            // A present, incomplete derived row must be conditionally erased before replay from one.
            // Replaying against it cannot silently inherit a stale override.
            ReadModelEntry<FolderPermissionProjection> currentFolder = await store
                .GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                    TestContext.Current.CancellationToken);
            FolderPermissionProjection incomplete = currentFolder.Value! with
            {
                LastSequence = 2,
                RecentMessages = [],
                Overrides = [new FolderPermissionOverrideEntry(FolderAccessPrincipalKind.User,
                    "user-a", "read_metadata", true, 1, DateTimeOffset.UtcNow)],
            };
            (await store.TrySaveAsync("statestore", folderProjectionKey, incomplete,
                currentFolder.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            store.TryRemove(folderProjectionKey, currentFolder.ETag!).ShouldBeFalse();
            ReadModelEntry<FolderPermissionProjection> broken = await store
                .GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                    TestContext.Current.CancellationToken);
            store.TryRemove(folderProjectionKey, broken.ETag!).ShouldBeTrue();
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "wrong-sidecar-token");
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await store.GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            FolderPermissionProjection repaired = (await store.GetAsync<FolderPermissionProjection>("statestore",
                folderProjectionKey, TestContext.Current.CancellationToken)).Value!;
            repaired.LastSequence.ShouldBe(1);
            repaired.Overrides.ShouldBeEmpty();

            ProviderBindingConfigured binding = new("tenant-a", "tenant-a", "binding-a", "github",
                "credential-a", OrganizationProviderBindingPolicy.Empty,
                OrganizationProviderBindingPolicy.Empty, "correlation-a", "task-a", "key-binding",
                "fingerprint-binding", "configured", DateTimeOffset.UtcNow);
            EventStoreDomainEventEnvelope bindingEnvelope = new("message-binding-a", ProviderBindingActorId.Create("binding-a"),
                "tenant-a", typeof(ProviderBindingConfigured).FullName!, 1, DateTimeOffset.UtcNow,
                "correlation-a", "json", JsonSerializer.SerializeToUtf8Bytes(binding,
                    EventStorePayloadSerialization.Options))
            {
                Domain = "folders",
                MetadataVersion = 1,
            };
            (await client.PostAsJsonAsync("/folders/events", bindingEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            ProviderBindingConfigured tenantReference = binding with { ProviderBindingRef = "tenant-a" };
            EventStoreDomainEventEnvelope tenantReferenceEnvelope = bindingEnvelope with
            {
                MessageId = "message-binding-tenant",
                AggregateId = ProviderBindingActorId.Create("tenant-a"),
                Payload = JsonSerializer.SerializeToUtf8Bytes(tenantReference,
                    EventStorePayloadSerialization.Options),
            };
            (await client.PostAsJsonAsync("/folders/events", tenantReferenceEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.PostAsJsonAsync("/folders/events", folderEnvelope with
            {
                AggregateId = ProviderBindingActorId.Create("binding-a"),
                Payload = JsonSerializer.SerializeToUtf8Bytes(created with
                {
                    FolderId = ProviderBindingActorId.Create("binding-a"),
                }, EventStorePayloadSerialization.Options),
            }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            foreach (EventStoreDomainEventEnvelope invalid in new[]
            {
                bindingEnvelope with { MetadataVersion = 2 },
                bindingEnvelope with { PayloadVersion = 2 },
                bindingEnvelope with { TenantId = "tenant-b" },
                bindingEnvelope with { AggregateId = "binding-b" },
                bindingEnvelope with
                {
                    Payload = JsonSerializer.SerializeToUtf8Bytes(binding with
                    {
                        OrganizationId = "other-organization",
                    }, EventStorePayloadSerialization.Options),
                },
            })
            {
                (await client.PostAsJsonAsync("/folders/events", invalid,
                    TestContext.Current.CancellationToken)).StatusCode
                    .ShouldBe(HttpStatusCode.InternalServerError);
            }

            (await store.GetAsync<FolderPermissionProjection>("statestore",
                "projection:folders:folder-permissions:tenant-a:binding-a",
                TestContext.Current.CancellationToken)).Value.ShouldBeNull();
            (await store.GetAsync<OrganizationPermissionProjection>("statestore",
                "projection:folders:organization-permissions:tenant-a",
                TestContext.Current.CancellationToken)).Value!.LastSequence.ShouldBe(2);

            EventStoreDomainEventEnvelope unsupported = Envelope("message-c", 3, grant) with
            {
                PayloadVersion = 2,
            };
            (await client.PostAsJsonAsync("/folders/events", unsupported,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await store.GetAsync<OrganizationPermissionProjection>("statestore",
                "projection:folders:organization-permissions:tenant-a",
                TestContext.Current.CancellationToken)).Value!.LastSequence.ShouldBe(2);

            OrganizationAclPrincipalRevoked organizationRevoke = new("tenant-a", "tenant-a",
                OrganizationAclPrincipalKind.User, "user-a", "create_folder", "correlation-a",
                "task-a", "key-revoke", "fingerprint-revoke");
            (await client.PostAsJsonAsync("/folders/events", Envelope("message-org-revoke", 3, organizationRevoke),
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            OrganizationPermissionProjection revokedOrganization = (await store
                .GetAsync<OrganizationPermissionProjection>("statestore",
                    "projection:folders:organization-permissions:tenant-a",
                    TestContext.Current.CancellationToken)).Value!;
            revokedOrganization.LastSequence.ShouldBe(3);
            revokedOrganization.Grants.ShouldBeEmpty();

            FolderAccessGranted folderGrant = new("tenant-a", "tenant-a", "folder-a",
                FolderAccessPrincipalKind.User, "user-a", "read_metadata", "user-a", "correlation-a",
                "task-a", "key-folder-grant", "fingerprint-folder-grant", 1, DateTimeOffset.UtcNow);
            EventStoreDomainEventEnvelope grantEnvelope = folderEnvelope with
            {
                MessageId = "message-folder-grant",
                EventTypeName = typeof(FolderAccessGranted).FullName!,
                SequenceNumber = 2,
                Payload = JsonSerializer.SerializeToUtf8Bytes(folderGrant, EventStorePayloadSerialization.Options),
            };
            (await client.PostAsJsonAsync("/folders/events", grantEnvelope,
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            FolderAccessRevoked folderRevoke = new("tenant-a", "tenant-a", "folder-a",
                FolderAccessPrincipalKind.User, "user-a", "read_metadata", "user-a", "correlation-a",
                "task-a", "key-folder-revoke", "fingerprint-folder-revoke", 2, DateTimeOffset.UtcNow);
            (await client.PostAsJsonAsync("/folders/events", grantEnvelope with
            {
                MessageId = "message-folder-revoke",
                EventTypeName = typeof(FolderAccessRevoked).FullName!,
                SequenceNumber = 3,
                Payload = JsonSerializer.SerializeToUtf8Bytes(folderRevoke, EventStorePayloadSerialization.Options),
            }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            FolderPermissionProjection revokedFolder = (await store.GetAsync<FolderPermissionProjection>(
                "statestore", folderProjectionKey, TestContext.Current.CancellationToken)).Value!;
            revokedFolder.LastSequence.ShouldBe(3);
            revokedFolder.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeFalse();

            string[] retainedRejections = ["folder-command-rejected", "duplicate-workspace-lock-rejected",
                "workspace-transition-invalid-rejected"];
            string[] rejectionNames = ["Hexalith.Folders.Server.FolderCommandRejected",
                "Hexalith.Folders.Server.DuplicateWorkspaceLockRejected",
                "Hexalith.Folders.Server.WorkspaceTransitionInvalidRejected"];
            for (int i = 0; i < retainedRejections.Length; i++)
            {
                string path = Path.Combine(AppContext.BaseDirectory, "event-evolution",
                    retainedRejections[i] + "-unstamped-v1.json");
                byte[] bytes = File.ReadAllBytes(path);
                (await client.PostAsJsonAsync("/folders/events", folderEnvelope with
                {
                    MessageId = "message-rejection-" + i,
                    EventTypeName = rejectionNames[i],
                    SequenceNumber = i + 4,
                    Payload = bytes,
                }, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
                File.ReadAllBytes(path).ShouldBe(bytes);
            }
            FolderPermissionProjection final = (await store.GetAsync<FolderPermissionProjection>(
                "statestore", folderProjectionKey, TestContext.Current.CancellationToken)).Value!;
            final.LastSequence.ShouldBe(6);
            final.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeFalse();
            SHA256.HashData(retainedCreate).ShouldBe(retainedDigest);
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "event-evolution",
                "folder-created-valid-unstamped-v1.json")).ShouldBe(retainedCreate);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static EventStoreDomainEventEnvelope Envelope<T>(string messageId, long sequence, T payload)
        => new(messageId, "tenant-a", "tenant-a", typeof(T).FullName!, sequence,
            DateTimeOffset.UtcNow, "correlation-a", "json", JsonSerializer.SerializeToUtf8Bytes(payload))
        {
            Domain = "folders",
            MetadataVersion = 1,
        };
}
