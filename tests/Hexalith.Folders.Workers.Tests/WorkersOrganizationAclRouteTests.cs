using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
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
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class WorkersOrganizationAclRouteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDeliveryReevaluatesUntilPrecedingWriteIsPersisted(bool writeConflict)
    {
        WorkerRouteReadModelStore store = new();
        WebApplication app = await StartMaintenanceHostAsync(store, store).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        TaskCompletionSource observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.ReadObserved = key =>
        {
            if (key == "projection:folders:organization-permissions:tenant-a") observed.TrySetResult();
        };
        Task<HttpResponseMessage> grant = client.PostAsJsonAsync("/folders/events", Envelope("grant", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                "user-a", "create_folder", "corr", "task", "grant-key", "grant-fingerprint")),
            TestContext.Current.CancellationToken);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        store.ReadObserved = null;
        store.ForcedWriteConflicts = writeConflict ? 1 : 0;
        using HttpResponseMessage baseline = await client.PostAsJsonAsync("/folders/events", Envelope("baseline", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        baseline.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage granted = await grant.ConfigureAwait(true);
        granted.StatusCode.ShouldBe(HttpStatusCode.OK);
        OrganizationPermissionProjection persisted = (await store.GetAsync<OrganizationPermissionProjection>(
            "statestore", "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).Value!;
        persisted.LastSequence.ShouldBe(2);
        persisted.Grants.ShouldHaveSingleItem().PrincipalId.ShouldBe("user-a");
        persisted.RecentMessages!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FolderCallbackRetriesOneWriteConflictAndPersistsSequenceOne()
    {
        WorkerRouteReadModelStore store = new() { ForcedWriteConflicts = 1 };
        WebApplication app = await StartMaintenanceHostAsync(store, store).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        byte[] retainedCreate = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "event-evolution", "folder-created-valid-unstamped-v1.json"));
        EventStoreDomainEventEnvelope created = new("folder-conflict-message", "folder-a", "tenant-a",
            typeof(FolderCreated).FullName!, 1, DateTimeOffset.UtcNow, "correlation-a", "json", retainedCreate)
        {
            Domain = "folders",
            MetadataVersion = 1,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/folders/events", created,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        store.ForcedWriteConflicts.ShouldBe(0);
        FolderPermissionProjection persisted = (await store.GetAsync<FolderPermissionProjection>("statestore",
            "projection:folders:folder-permissions:tenant-a:folder-a", TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).Value!;
        persisted.IsCreated.ShouldBeTrue();
        persisted.LastSequence.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvedConcurrentDeliveryFailsWithinBudgetWithoutAdvancing(bool writeConflict)
    {
        WorkerRouteReadModelStore store = new() { ForcedWriteConflicts = writeConflict ? int.MaxValue : 0 };
        WebApplication app = await StartMaintenanceHostAsync(store, store).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        using HttpResponseMessage response = await client.PostAsJsonAsync("/folders/events", Envelope("baseline",
            writeConflict ? 1 : 2, new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")),
            bound.Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:tenant-a", TestContext.Current.CancellationToken)
            .ConfigureAwait(true)).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "-1")]
    [InlineData(true, "-1")]
    [InlineData(false, "4")]
    [InlineData(true, "4")]
    public async Task MalformedRetainedPrincipalKindsLeaveRowsUnchangedAndExplicitV1UserStillReplays(
        bool revoke, string malformedKind)
    {
        const string retainedV1 = """{"ManagedTenantId":"tenant-a","OrganizationId":"tenant-a","PrincipalKind":0,"PrincipalId":"retained-user","Action":"create_folder","CorrelationId":"correlation-a","TaskId":"task-a","IdempotencyKey":"retained-key","IdempotencyFingerprint":"retained-fingerprint"}""";
        byte[] retainedBytes = Encoding.UTF8.GetBytes(retainedV1);
        byte[] retainedDigest = SHA256.HashData(retainedBytes);
        WorkerRouteReadModelStore store = new();
        WebApplication app = await StartMaintenanceHostAsync(store, store).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        (await client.PostAsJsonAsync("/folders/events", Envelope("baseline", 1,
            new OrganizationAclBaselineInitialized("tenant-a", "tenant-a", "corr", "task", "key", "fingerprint")),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/folders/events", Envelope("seed-grant", 2,
            new OrganizationAclPrincipalGranted("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
                revoke ? "retained-user" : "initial-user", "create_folder", "corr", "task", "seed-key", "seed-fingerprint")),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        const string key = "projection:folders:organization-permissions:tenant-a";
        ReadModelEntry<OrganizationPermissionProjection> before = await store
            .GetAsync<OrganizationPermissionProjection>("statestore", key, TestContext.Current.CancellationToken);
        JsonObject malformed = JsonNode.Parse(retainedV1)!.AsObject();
        if (malformedKind == "missing") malformed.Remove("PrincipalKind").ShouldBeTrue();
        else malformed["PrincipalKind"] = int.Parse(malformedKind, System.Globalization.CultureInfo.InvariantCulture);
        EventStoreDomainEventEnvelope envelope = new("retained-event", "tenant-a", "tenant-a",
            (revoke ? typeof(OrganizationAclPrincipalRevoked) : typeof(OrganizationAclPrincipalGranted)).FullName!,
            3, DateTimeOffset.UtcNow, "correlation-a", "json", Encoding.UTF8.GetBytes(malformed.ToJsonString()))
        { Domain = "folders", MetadataVersion = 1 };

        (await client.PostAsJsonAsync("/folders/events", envelope, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        ReadModelEntry<OrganizationPermissionProjection> unchanged = await store
            .GetAsync<OrganizationPermissionProjection>("statestore", key, TestContext.Current.CancellationToken);
        unchanged.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(unchanged.Value).ShouldBe(JsonSerializer.Serialize(before.Value));

        (await client.PostAsJsonAsync("/folders/events", envelope with { Payload = retainedBytes },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        OrganizationPermissionProjection recovered = (await store.GetAsync<OrganizationPermissionProjection>(
            "statestore", key, TestContext.Current.CancellationToken)).Value!;
        recovered.LastSequence.ShouldBe(3);
        if (revoke) recovered.Grants.ShouldBeEmpty();
        else
        {
            recovered.Grants.Count.ShouldBe(2);
            recovered.Grants.Single(item => item.PrincipalId == "retained-user").PrincipalKind
                .ShouldBe(OrganizationAclPrincipalKind.User);
        }
        Encoding.UTF8.GetString(retainedBytes).ShouldBe(retainedV1);
        SHA256.HashData(retainedBytes).ShouldBe(retainedDigest);
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("revoke")]
    [InlineData("repeated-baseline")]
    public async Task MalformedOrganizationStreamLeavesPersistedProjectionUnchanged(string scenario)
    {
        WorkerRouteReadModelStore store = new();
        WebApplication app = await StartMaintenanceHostAsync(store, store).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        const string key = "projection:folders:organization-permissions:tenant-a";
        OrganizationAclBaselineInitialized baseline = new("tenant-a", "tenant-a", "correlation-a",
            "task-a", "baseline-key", "baseline-fingerprint");
        OrganizationAclPrincipalGranted grant = new("tenant-a", "tenant-a", OrganizationAclPrincipalKind.User,
            "user-a", "read_metadata", "correlation-a", "task-a", "grant-key", "grant-fingerprint");
        if (scenario == "repeated-baseline")
        {
            (await client.PostAsJsonAsync("/folders/events", Envelope("baseline", 1, baseline),
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.PostAsJsonAsync("/folders/events", Envelope("grant", 2, grant),
                TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        ReadModelEntry<OrganizationPermissionProjection> before = await store
            .GetAsync<OrganizationPermissionProjection>("statestore", key, TestContext.Current.CancellationToken);
        EventStoreDomainEventEnvelope malformed = scenario switch
        {
            "grant" => Envelope("grant-without-baseline", 1, grant),
            "revoke" => Envelope("revoke-without-baseline", 1, new OrganizationAclPrincipalRevoked(
                "tenant-a", "tenant-a", OrganizationAclPrincipalKind.User, "user-a", "read_metadata",
                "correlation-a", "task-a", "revoke-key", "revoke-fingerprint")),
            _ => Envelope("second-baseline", 3, baseline with { IdempotencyKey = "other-baseline-key" }),
        };
        (await client.PostAsJsonAsync("/folders/events", malformed, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        ReadModelEntry<OrganizationPermissionProjection> after = await store
            .GetAsync<OrganizationPermissionProjection>("statestore", key, TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value).ShouldBe(JsonSerializer.Serialize(before.Value));
    }

    [Fact]
    public async Task OrganizationResetErasesOnlyOrganizationRowAndAuditsMetadata()
    {
        WorkerRouteReadModelStore store = new();
        PermissionProjectionFailureLoggerProvider logs = new();
        WebApplication app = await StartMaintenanceHostAsync(store, store, logs).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        const string organizationKey = "projection:folders:organization-permissions:tenant-a";
        const string folderKey = "projection:folders:folder-permissions:tenant-a:folder-a";
        OrganizationAclBaselineInitialized baseline = new("tenant-a", "tenant-a", "correlation-a",
            "task-a", "baseline-key", "baseline-fingerprint");
        (await client.PostAsJsonAsync("/folders/events", Envelope("baseline", 1, baseline),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await store.TrySaveAsync("statestore", folderKey, new { retained = true }, string.Empty,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        ReadModelEntry<OrganizationPermissionProjection> before = await store
            .GetAsync<OrganizationPermissionProjection>("statestore", organizationKey, TestContext.Current.CancellationToken);
        FolderPermissionResetRequest request = new("tenant-a", "tenant-a", before.ETag);
        (await client.PostAsJsonAsync("/maintenance/folder-permissions/reset", request,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await store.GetAsync<OrganizationPermissionProjection>("statestore", organizationKey,
            TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        (await store.GetAsync<object>("statestore", folderKey,
            TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(1202);
        entry.Fields["TenantId"].ShouldBe("tenant-a");
        entry.Fields["AggregateId"].ShouldBe("tenant-a");
        entry.Fields["Outcome"].ShouldBe("erased");
        entry.Exception.ShouldBeNull();
        entry.Fields.Keys.Order(StringComparer.Ordinal).ShouldBe(new[]
            { "TenantId", "AggregateId", "Outcome", "{OriginalFormat}" }.Order(StringComparer.Ordinal));
        entry.Message.ShouldNotContain(before.ETag!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tenant:invalid")]
    [InlineData("system")]
    [InlineData("provider-binding-0123456789abcdef")]
    public async Task InvalidMaintenanceTenantReturnsBadRequestWithoutCallingEraser(string tenant)
    {
        IReadModelConditionalEraser eraser = Substitute.For<IReadModelConditionalEraser>();
        WebApplication app = await StartMaintenanceHostAsync(new WorkerRouteReadModelStore(), eraser).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        (await client.PostAsJsonAsync("/maintenance/folder-permissions/reset",
            new FolderPermissionResetRequest(tenant, tenant, "etag"), TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        eraser.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("row-remaining", HttpStatusCode.Conflict, "row_remaining")]
    [InlineData("etag-conflict", HttpStatusCode.Conflict, "etag_conflict")]
    [InlineData("inspect-failure", HttpStatusCode.ServiceUnavailable, "unavailable")]
    [InlineData("erase-failure", HttpStatusCode.ServiceUnavailable, "unavailable")]
    [InlineData("read-back-failure", HttpStatusCode.ServiceUnavailable, "unavailable")]
    public async Task MaintenanceConflictsAndStoreFailuresRemainClosedAndAudited(
        string scenario, HttpStatusCode expected, string outcome)
    {
        const string key = "projection:folders:folder-permissions:tenant-a:folder-a";
        const string protectedEtag = "protected-etag-value";
        const string protectedFailure = "protected-backend-credential-and-message";
        IReadModelConditionalEraser eraser = Substitute.For<IReadModelConditionalEraser>();
        eraser.TryReadEtagAsync("statestore", key, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((true, protectedEtag)));
        eraser.TryEraseAsync("statestore", key, protectedEtag, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(scenario != "etag-conflict"));
        if (scenario is "inspect-failure" or "read-back-failure")
        {
            eraser.TryReadEtagAsync("statestore", key, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<(bool, string)>(new InvalidOperationException(protectedFailure)));
        }
        if (scenario == "erase-failure")
        {
            eraser.TryEraseAsync("statestore", key, protectedEtag, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<bool>(new InvalidOperationException(protectedFailure)));
        }

        PermissionProjectionFailureLoggerProvider logs = new();
        WebApplication app = await StartMaintenanceHostAsync(new WorkerRouteReadModelStore(), eraser, logs).ConfigureAwait(true);
        await using var cleanup = app.ConfigureAwait(false);
        using HttpClient client = AuthenticatedClient(app);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/maintenance/folder-permissions/reset",
            new FolderPermissionResetRequest("tenant-a", "folder-a", scenario == "inspect-failure" ? null : protectedEtag),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(expected == HttpStatusCode.ServiceUnavailable ? 1203 : 1202);
        entry.Fields["TenantId"].ShouldBe("tenant-a");
        entry.Fields["AggregateId"].ShouldBe("folder-a");
        entry.Fields["Outcome"].ShouldBe(outcome);
        entry.Exception.ShouldBeNull();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (string secret in new[] { protectedEtag, protectedFailure })
        {
            entry.Message.ShouldNotContain(secret);
            body.ShouldNotContain(secret);
        }
        if (scenario != "inspect-failure")
        {
            await eraser.Received(1).TryEraseAsync("statestore", key, protectedEtag, Arg.Any<CancellationToken>());
        }
    }

    private static async Task<WebApplication> StartMaintenanceHostAsync(IReadModelStore store,
        IReadModelConditionalEraser eraser, ILoggerProvider? logs = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        if (logs is not null) builder.Logging.AddProvider(logs);
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.AddSingleton(store);
        builder.Services.RemoveAll<IReadModelConditionalEraser>();
        builder.Services.AddSingleton(eraser);
        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapFoldersTenantEventWorkerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return app;
    }

    private static HttpClient AuthenticatedClient(WebApplication app)
    {
        HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
        return client;
    }

    [Fact]
    public async Task ProjectionFailureLogsOnlyFixedStageCategoryAndRuntimeTypeBeforeRetryableResponse()
    {
        const string protectedTenant = "protected-tenant-identifier";
        const string protectedMessage = "protected-message-identifier";
        const string protectedPayload = "protected-payload-correlation";
        const string protectedFailure = "protected-exception-message-and-credential";
        IReadModelStore store = Substitute.For<IReadModelStore>();
        store.GetAsync<OrganizationPermissionProjection>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ReadModelEntry<OrganizationPermissionProjection>>(
                new InvalidOperationException(protectedFailure)));
        PermissionProjectionFailureLoggerProvider logs = new();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "test-sidecar-token";
        builder.Logging.AddProvider(logs);
        builder.Services.AddFoldersTenantEventWorkers();
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.AddSingleton(store);
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapFoldersTenantEventWorkerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            OrganizationAclBaselineInitialized baseline = new(protectedTenant, protectedTenant,
                protectedPayload, "protected-task", "protected-key", "protected-fingerprint");
            EventStoreDomainEventEnvelope envelope = Envelope(protectedMessage, 1, baseline) with
            {
                TenantId = protectedTenant,
                AggregateId = protectedTenant,
            };
            using HttpResponseMessage response = await client.PostAsJsonAsync("/folders/events", envelope,
                TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

            var entry = logs.Entries.ShouldHaveSingleItem();
            entry.Level.ShouldBe(LogLevel.Warning);
            entry.EventId.Id.ShouldBe(1201);
            entry.Exception.ShouldBeNull();
            entry.Fields.Keys.Order(StringComparer.Ordinal).ShouldBe(new[]
                { "Category", "Stage", "FailureType", "{OriginalFormat}" }.Order(StringComparer.Ordinal));
            entry.Fields["Category"].ShouldBe("permission_projection_failed");
            entry.Fields["Stage"].ShouldBe("permission_projection");
            entry.Fields["FailureType"].ShouldBe(typeof(InvalidOperationException).FullName);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            foreach (string protectedValue in new[] { protectedTenant, protectedMessage, protectedPayload,
                protectedFailure, "protected-task", "protected-key", "protected-fingerprint",
                Convert.ToBase64String(envelope.Payload) })
            {
                entry.Message.ShouldNotContain(protectedValue);
                entry.Fields.Values.Select(value => value?.ToString() ?? string.Empty)
                    .ShouldNotContain(value => value.Contains(protectedValue, StringComparison.Ordinal));
                body.ShouldNotContain(protectedValue);
            }
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

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
        builder.Services.RemoveAll<IReadModelConditionalEraser>();
        builder.Services.AddSingleton<IReadModelConditionalEraser>(store);
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
            const string resetRoute = "/maintenance/folder-permissions/reset";
            FolderPermissionResetRequest reset = new("tenant-a", "folder-a", currentFolder.ETag);
            (await client.PostAsJsonAsync(resetRoute, reset, TestContext.Current.CancellationToken))
                .StatusCode.ShouldBe(HttpStatusCode.Conflict);
            ReadModelEntry<FolderPermissionProjection> broken = await store
                .GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                    TestContext.Current.CancellationToken);
            reset = reset with { ExpectedEtag = broken.ETag };
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            (await client.PostAsJsonAsync(resetRoute, reset, TestContext.Current.CancellationToken))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "wrong-sidecar-token");
            (await client.PostAsJsonAsync(resetRoute, reset, TestContext.Current.CancellationToken))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await store.GetAsync<FolderPermissionProjection>("statestore", folderProjectionKey,
                TestContext.Current.CancellationToken)).Value!.Overrides.ShouldHaveSingleItem().IsGranted.ShouldBeTrue();
            client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            using (HttpResponseMessage etagResponse = await client.PostAsJsonAsync(resetRoute,
                reset with { ExpectedEtag = null }, TestContext.Current.CancellationToken))
            {
                etagResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
                using JsonDocument etagBody = JsonDocument.Parse(await etagResponse.Content
                    .ReadAsStringAsync(TestContext.Current.CancellationToken));
                etagBody.RootElement.GetProperty("etag").GetString().ShouldBe(broken.ETag);
            }
            (await client.PostAsJsonAsync(resetRoute, reset, TestContext.Current.CancellationToken))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            foreach (string invalidActor in new[] { "organization_baseline", ProviderBindingActorId.Create("binding-a") })
            {
                (await client.PostAsJsonAsync(resetRoute, reset with { AggregateId = invalidActor },
                    TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            }
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
