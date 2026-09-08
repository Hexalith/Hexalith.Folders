using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.IntegrationTests.ContextSearch;

/// <summary>
/// Story 10.5 AC11 — no-mock-gateway integration: drives the real REST endpoint -> layered authorization ->
/// <see cref="IFolderSearchSource"/> egress seam end-to-end (the handler is never called directly and the egress is
/// never stubbed at the gateway boundary). A controllable <see cref="IFolderSearchSource"/> fake flows through the
/// real endpoint + auth so cross-tenant isolation and safe-denial indistinguishability are asserted on the real path.
/// </summary>
public sealed class ContextSearchFacadeWiringTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchShouldReturnOnlyTheCallersTenantHitsThroughTheRealEndpointAndAuth()
    {
        // The shared folders-index can echo a foreign hit; the Folders-side trim is the authoritative control.
        FakeFolderSearchSource source = new()
        {
            Hits =
            [
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-a", 2.0),
                new FolderSearchSourceHit("tenant-b", "org-b", "folder-a", "workspace-a", "fv-b", 1.5),
            ],
            TotalCount = 2,
        };
        SeededBridgeReadModel bridge = new();
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-a"));
        bridge.Add(Entry("tenant-b", "folder-a", "workspace-a", "fv-b"));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage response = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-1"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
            source.Calls.ShouldBe(1, "the request must flow through the real endpoint and auth to the egress seam");

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement items = document.RootElement.GetProperty("items");
            items.GetArrayLength().ShouldBe(1, "the cross-tenant (tenant-b) hit must be trimmed Folders-side");
            items[0].GetProperty("fileVersionReference").GetString().ShouldBe("fv-a");
            json.ShouldNotContain("fv-b", Case.Sensitive);
            json.ShouldNotContain("folders://", Case.Sensitive);
            json.ShouldNotContain("rawCount", Case.Insensitive);
            json.ShouldNotContain("totalCount", Case.Insensitive);
            AssertNoLeakageCorpusValue(json);
            document.RootElement.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe("eventually_consistent");
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SearchShouldDropStaleAndTombstonedHitsWhileStatusMayListThem()
    {
        FakeFolderSearchSource source = new()
        {
            Hits =
            [
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-live", 2.0),
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-stale", 1.5),
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-removed", 1.0),
            ],
            TotalCount = 3,
        };
        SeededBridgeReadModel bridge = new();
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-live"));
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-stale", SemanticIndexingBridgeStatus.Stale));
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-removed", SemanticIndexingBridgeStatus.Tombstoned));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage searchResponse = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-lifecycle"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string searchJson = await searchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, searchJson);
            using JsonDocument searchDocument = JsonDocument.Parse(searchJson);
            JsonElement items = searchDocument.RootElement.GetProperty("items");
            items.GetArrayLength().ShouldBe(1);
            items[0].GetProperty("fileVersionReference").GetString().ShouldBe("fv-live");
            searchJson.ShouldNotContain("fv-stale", Case.Sensitive);
            searchJson.ShouldNotContain("fv-removed", Case.Sensitive);
            searchJson.ShouldNotContain("rawCount", Case.Insensitive);
            AssertNoLeakageCorpusValue(searchJson);

            HttpResponseMessage statusResponse = await host.Client
                .SendAsync(StatusRequest("folder-a", "corr-status"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string statusJson = await statusResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK, statusJson);
            statusJson.ShouldContain("fv-stale", Case.Sensitive);
            statusJson.ShouldContain("fv-removed", Case.Sensitive);
            AssertNoLeakageCorpusValue(statusJson);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ArchivedDocumentShouldBeAbsentFromSearchWhileStatusRemainsIndexed()
    {
        // Folder archive is not a bridge enum: Memories re-sends folders.status=archived and active search
        // filters it out. The facade only hydrates hits the source returns.
        FakeFolderSearchSource source = new();
        SeededBridgeReadModel bridge = new();
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-archived"));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage searchResponse = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-archive"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string searchJson = await searchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, searchJson);
            using JsonDocument searchDocument = JsonDocument.Parse(searchJson);
            searchDocument.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
            searchJson.ShouldNotContain("fv-archived", Case.Sensitive);
            AssertNoLeakageCorpusValue(searchJson);

            HttpResponseMessage statusResponse = await host.Client
                .SendAsync(StatusRequest("folder-a", "corr-archive-status"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string statusJson = await statusResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK, statusJson);
            statusJson.ShouldContain("fv-archived", Case.Sensitive);
            statusJson.ShouldContain("indexed", Case.Sensitive);
            AssertNoLeakageCorpusValue(statusJson);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnavailableBridgeAfterAuthorizationShouldReturn503WithoutMemoriesEgress()
    {
        FakeFolderSearchSource source = new()
        {
            Hits = [new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-a", 2.0)],
            TotalCount = 1,
        };
        SeededBridgeReadModel bridge = new() { IsAvailable = false };
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-a"));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage response = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-unavail"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, json);
            source.Calls.ShouldBe(0);
            json.ShouldContain("read_model_unavailable");
            json.ShouldNotContain("fv-a", Case.Sensitive);
            json.ShouldNotContain("folders://", Case.Sensitive);
            AssertNoLeakageCorpusValue(json);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnauthorizedCallerShouldNotDistinguishUnavailableBridgeFromDenial()
    {
        FakeFolderSearchSource source = new();
        SeededBridgeReadModel unavailable = new() { IsAvailable = false };
        TestHost host = await StartHostAsync(source, unavailable).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            string denied = await DenialBodyAsync(host, "folder-b", "corr-denied-unavail").ConfigureAwait(true);
            source.Calls.ShouldBe(0);
            denied.ShouldNotContain("read_model_unavailable", Case.Sensitive);
            denied.ShouldNotContain("unavailable", Case.Insensitive);
            AssertNoLeakageCorpusValue(denied);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task DuplicateSourceHitsForOneIdentityShouldNotDiscloseRawCandidateCounts()
    {
        FakeFolderSearchSource source = new()
        {
            Hits =
            [
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-a", 2.0),
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-a", 1.9),
            ],
            TotalCount = 99,
        };
        SeededBridgeReadModel bridge = new();
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-a"));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage first = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-dup-1"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            HttpResponseMessage second = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-dup-2"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string firstJson = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string secondJson = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            first.StatusCode.ShouldBe(HttpStatusCode.OK, firstJson);
            second.StatusCode.ShouldBe(HttpStatusCode.OK, secondJson);
            firstJson.ShouldNotContain("99", Case.Sensitive);
            secondJson.ShouldNotContain("99", Case.Sensitive);
            firstJson.ShouldNotContain("rawCount", Case.Insensitive);
            firstJson.ShouldNotContain("totalCount", Case.Insensitive);
            AssertNoLeakageCorpusValue(firstJson);
            AssertNoLeakageCorpusValue(secondJson);

            using JsonDocument firstDocument = JsonDocument.Parse(firstJson);
            using JsonDocument secondDocument = JsonDocument.Parse(secondJson);
            firstDocument.RootElement.GetProperty("items")[0].GetProperty("fileVersionReference").GetString()
                .ShouldBe(secondDocument.RootElement.GetProperty("items")[0].GetProperty("fileVersionReference").GetString());
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task WrongWorkspaceHitShouldBeTrimmedWithoutLeakingExistence()
    {
        FakeFolderSearchSource source = new()
        {
            Hits =
            [
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-b", "fv-b", 2.0),
                new FolderSearchSourceHit("tenant-a", "org-a", "folder-a", "workspace-a", "fv-a", 1.5),
            ],
            TotalCount = 2,
        };
        SeededBridgeReadModel bridge = new();
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-a", "fv-a"));
        bridge.Add(Entry("tenant-a", "folder-a", "workspace-b", "fv-b"));

        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpResponseMessage response = await host.Client
                .SendAsync(SearchRequest("folder-a", "workspace-a", "needle", "corr-ws"), TestContext.Current.CancellationToken)
                .ConfigureAwait(true);
            string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
            json.ShouldNotContain("fv-b", Case.Sensitive);
            AssertNoLeakageCorpusValue(json);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnauthorizedCrossTenantAndNonexistentTargetsShouldReturnByteIdenticalSafeDenial()
    {
        FakeFolderSearchSource source = new();
        SeededBridgeReadModel bridge = new();
        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            // user-a is authorized in tenant-a for folder-a only. Then the caller context is switched to tenant-b
            // for the cross-tenant probe. Unauthorized, cross-tenant, and nonexistent targets all resolve to the
            // SAME safe-denial via layered authorization, externally indistinguishable.
            SeedTenant(host, "tenant-a", "user-a");
            SeedTenant(host, "tenant-b", "user-b");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            string unauthorized = await DenialBodyAsync(host, "folder-b", "corr-unauth").ConfigureAwait(true);
            host.Context.Set("tenant-b", "user-b");
            string crossTenant = await DenialBodyAsync(host, "folder-a", "corr-cross").ConfigureAwait(true);
            host.Context.Set("tenant-a", "user-a");
            string nonexistent = await DenialBodyAsync(host, "folder-x", "corr-absent").ConfigureAwait(true);

            // The egress seam must never be reached for a denied caller (deny before observation).
            source.Calls.ShouldBe(0);

            // Normalize the correlation id so the comparison proves the bodies are otherwise byte-identical.
            string a = NormalizeCorrelation(unauthorized);
            string b = NormalizeCorrelation(crossTenant);
            string c = NormalizeCorrelation(nonexistent);
            a.ShouldBe(b);
            b.ShouldBe(c);
            a.ShouldContain("metadata_only");
            AssertNoLeakageCorpusValue(unauthorized);
            AssertNoLeakageCorpusValue(crossTenant);
            AssertNoLeakageCorpusValue(nonexistent);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SearchWithIdempotencyKeyShouldBeRejectedBeforeTheEgressSeam()
    {
        FakeFolderSearchSource source = new();
        SeededBridgeReadModel bridge = new();
        TestHost host = await StartHostAsync(source, bridge).ConfigureAwait(true);
        try
        {
            SeedTenant(host, "tenant-a", "user-a");
            SeedContextSearchPermission(host, "tenant-a", "org-a", "folder-a", "user-a");

            HttpRequestMessage request = SearchRequest("folder-a", "workspace-a", "needle", "corr-idem");
            request.Headers.Add("Idempotency-Key", "should-not-be-allowed");

            HttpResponseMessage response = await host.Client
                .SendAsync(request, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            source.Calls.ShouldBe(0);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<string> DenialBodyAsync(TestHost host, string folderId, string correlationId)
    {
        HttpResponseMessage response = await host.Client
            .SendAsync(SearchRequest(folderId, "workspace-a", "needle", correlationId), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static string NormalizeCorrelation(string body)
        => Regex.Replace(body, "corr-[a-z]+", "corr-NORMALIZED", RegexOptions.CultureInvariant);

    private static void AssertNoLeakageCorpusValue(string body)
    {
        foreach (string sentinel in LeakageCorpusValues())
        {
            body.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    private static string[] LeakageCorpusValues()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(static sample => sample.GetProperty("value").GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tests", "fixtures", "audit-leakage-corpus.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("audit-leakage-corpus.json not found relative to test base directory.");
    }

    private static HttpRequestMessage SearchRequest(string folderId, string workspaceId, string queryText, string correlationId)
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/index-search")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                queryFamily = "semantic_reference_pending",
                queryText,
            }),
        };
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        return request;
    }

    private static HttpRequestMessage StatusRequest(string folderId, string correlationId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/folders/{folderId}/indexing-status");
        request.Headers.Add("X-Correlation-Id", correlationId);
        return request;
    }

    private static SemanticIndexingBridgeEntry Entry(
        string tenantId,
        string folderId,
        string workspaceId,
        string fileVersionId,
        SemanticIndexingBridgeStatus status = SemanticIndexingBridgeStatus.Indexed)
        => new(
            new SemanticIndexingFileVersionIdentity(
                tenantId,
                "org-a",
                folderId,
                workspaceId,
                "op-a",
                "digest-" + fileVersionId,
                fileVersionId,
                "hash-" + fileVersionId,
                $"folders://{tenantId}/organizations/org-a/folders/{folderId}/workspaces/{workspaceId}/file-versions/{fileVersionId}"),
            status,
            "memories_accepted",
            retryable: false,
            "seed-correlation",
            "seed-task",
            Now,
            new SemanticIndexingEvidence(pathPolicyClass: "tenant_sensitive_document"));

    private static void SeedTenant(TestHost host, string tenantId, string principalId)
        => host.TenantStore.SaveAsync(
            new FolderTenantAccessProjection
            {
                TenantId = tenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    [principalId] = new(principalId, "Owner"),
                },
                Watermark = 1,
                LastEventTimestamp = Now.AddMinutes(-1),
                ProjectionWatermark = $"{tenantId}:1",
            },
            TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    private static void SeedContextSearchPermission(
        TestHost host,
        string tenantId,
        string organizationId,
        string folderId,
        string principalId)
        => host.Permissions.Save(new EffectivePermissionsReadModelSnapshot(
            tenantId,
            organizationId,
            folderId,
            EffectivePermissionsFolderLifecycleState.Active,
            [
                new(
                    EffectivePermissionEvidenceSource.FolderOverrideGrant,
                    EffectivePermissionPrincipal.User(principalId),
                    ContextSearchQueryHandler.ActionToken,
                    Sequence: 1,
                    EffectiveAt: Now.AddMinutes(-1)),
            ],
            new EffectivePermissionsFreshness("read_your_writes", Now, "permission-watermark-a", Stale: false, ReasonCode: null),
            RevocationFreshnessEstablished: true,
            TaskScope: null));

    private static async Task<TestHost> StartHostAsync(IFolderSearchSource source, ISemanticIndexingBridgeReadModel bridge)
    {
        MutableTenantAndClaimContext context = new("tenant-a", "user-a");
        InMemoryFolderTenantAccessProjectionStore tenantStore = new();
        InMemoryEffectivePermissionsReadModel permissions = new();
        InMemoryFolderLifecycleStatusReadModel lifecycleReadModel = new(new FixedUtcClock(Now));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InMemoryFolderRepository repository = new(lifecycleReadModel, timeProvider: timeProvider);
        Uri? hostUri = null;
        InProcessRejectionPropagatingGatewayClient gateway = new(() => new HttpClient { BaseAddress = hostUri! }, () => context.PrincipalId);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.RemoveAll<IEventStoreGatewayClient>();
        builder.Services.AddSingleton<IEventStoreGatewayClient>(gateway);
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(context);
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(context);
        builder.Services.RemoveAll<IFolderRepository>();
        builder.Services.AddSingleton<IFolderRepository>(repository);
        builder.Services.RemoveAll<IFolderLifecycleStatusReadModel>();
        builder.Services.AddSingleton<IFolderLifecycleStatusReadModel>(lifecycleReadModel);
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenantStore);
        builder.Services.RemoveAll<IEffectivePermissionsReadModel>();
        builder.Services.AddSingleton<IEffectivePermissionsReadModel>(permissions);
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<TimeProvider>();
        builder.Services.AddSingleton(timeProvider);

        // The Story 10.5 facade seam: override the live Memories gateway with a controllable fake, and the bridge
        // read model with a seeded stub. The request still flows through the REAL endpoint, REAL layered auth, and
        // the REAL handler — only the egress + authoritative hydration are controllable (no boundary stubbing).
        builder.Services.RemoveAll<IFolderSearchSource>();
        builder.Services.AddSingleton(source);
        builder.Services.RemoveAll<ISemanticIndexingBridgeReadModel>();
        builder.Services.AddSingleton(bridge);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        hostUri = new Uri(app.Urls.First());
        return new TestHost(app, new HttpClient { BaseAddress = hostUri }, context, tenantStore, permissions);
    }

    private sealed record TestHost(
        WebApplication App,
        HttpClient Client,
        MutableTenantAndClaimContext Context,
        InMemoryFolderTenantAccessProjectionStore TenantStore,
        InMemoryEffectivePermissionsReadModel Permissions) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await App.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class MutableTenantAndClaimContext(string tenantId, string principalId)
        : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor
    {
        public string? AuthoritativeTenantId { get; private set; } = tenantId;

        public string? PrincipalId { get; private set; } = principalId;

        public void Set(string tenantId, string principalId)
        {
            AuthoritativeTenantId = tenantId;
            PrincipalId = principalId;
        }

        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
            => EventStoreClaimTransformEvidence.Allowed(
                AuthoritativeTenantId ?? string.Empty,
                PrincipalId ?? string.Empty,
                [actionToken]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeFolderSearchSource : IFolderSearchSource
    {
        private int _calls;

        public IReadOnlyList<FolderSearchSourceHit> Hits { get; init; } = [];

        public long TotalCount { get; init; }

        public FolderSearchSourceStatus Status { get; init; } = FolderSearchSourceStatus.Available;

        public int Calls => _calls;

        public Task<FolderSearchSourceResult> SearchAsync(
            FolderSearchSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new FolderSearchSourceResult(
                Status,
                Hits,
                TotalCount == 0 ? Hits.Count : TotalCount,
                Hits.Count));
        }
    }

    private sealed class SeededBridgeReadModel : ISemanticIndexingBridgeReadModel
    {
        private readonly List<SemanticIndexingBridgeEntry> _entries = [];

        public bool IsAvailable { get; init; } = true;

        public void Add(SemanticIndexingBridgeEntry entry) => _entries.Add(entry);

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
            SemanticIndexingFileVersionIdentity identity,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(e => e.Identity.ReadModelKey == identity.ReadModelKey));

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
            string managedTenantId,
            string folderId,
            string fileVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.FirstOrDefault(e =>
                string.Equals(e.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FolderId, folderId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FileVersionId, fileVersionId, StringComparison.Ordinal)));

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
            string managedTenantId,
            string folderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SemanticIndexingBridgeEntry>>(
                [.. _entries.Where(e =>
                    string.Equals(e.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                    && string.Equals(e.Identity.FolderId, folderId, StringComparison.Ordinal))]);
    }
}
