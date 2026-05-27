using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FileContextEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 8, 45, 0, TimeSpan.Zero);

    [Fact]
    public void MapFoldersServerEndpointsShouldRegisterContextQueryRoutes()
    {
        using WebApplication app = BuildApp(new RecordingContextSource());

        string[] routes = ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/tree");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/metadata");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/search");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/glob");
        routes.ShouldContain("/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/range-read");
    }

    [Fact]
    public async Task TreeQueryShouldReturnMetadataOnlyResult()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/workspaces/workspace-a/context/tree?limit=10");
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        response.Headers.GetValues("X-Hexalith-Freshness").ShouldContain("snapshot_per_task");
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
        document.RootElement.GetProperty("items")[0].GetProperty("path").GetProperty("normalizedPath").GetString().ShouldBe("docs/readme.md");
        json.ShouldNotContain("contentBytes", Case.Sensitive);
        source.Requests.ShouldHaveSingleItem().Limit.ShouldBe(10);
    }

    [Fact]
    public async Task MetadataQueryShouldReturnMetadataOnlyResult()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = new[] { Path("docs/readme.md") },
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
        document.RootElement.GetProperty("items")[0].GetProperty("path").GetProperty("normalizedPath").GetString().ShouldBe("docs/readme.md");
        json.ShouldNotContain("contentBytes", Case.Sensitive);
        WorkspaceFileContextSourceRequest sourceRequest = source.Requests.ShouldHaveSingleItem();
        sourceRequest.Kind.ShouldBe(WorkspaceFileContextQueryKind.Metadata);
        sourceRequest.Paths.ShouldHaveSingleItem().Path.NormalizedPath.ShouldBe("docs/readme.md");
    }

    [Fact]
    public async Task MetadataQueryWithoutPathsShouldRejectBeforeSourceObservation()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = Array.Empty<object>(),
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, json);
        json.ShouldContain("\"code\":\"validation_error\"");
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("search")]
    [InlineData("glob")]
    public async Task SearchAndGlobQueriesShouldForwardBodyBoundsAndReturnMetadataOnly(string queryFamily)
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        bool isSearch = string.Equals(queryFamily, "search", StringComparison.Ordinal);
        object body = isSearch
            ? new
            {
                requestSchemaVersion = "v1",
                queryFamily,
                queryText = "needle",
                requestedPaths = new[] { Path("docs/readme.md") },
                limit = 5,
                cursor = "cursor-a",
            }
            : new
            {
                requestSchemaVersion = "v1",
                queryFamily,
                globPattern = "*.md",
                requestedPaths = new[] { Path("docs/readme.md") },
                limit = 5,
                cursor = "cursor-a",
            };
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/folder-a/workspaces/workspace-a/context/{queryFamily}")
        {
            Content = JsonContent.Create(body),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        json.ShouldNotContain("contentBytes", Case.Sensitive);
        WorkspaceFileContextSourceRequest sourceRequest = source.Requests.ShouldHaveSingleItem();
        sourceRequest.Kind.ShouldBe(isSearch ? WorkspaceFileContextQueryKind.Search : WorkspaceFileContextQueryKind.Glob);
        sourceRequest.Limit.ShouldBe(5);
        sourceRequest.Cursor.ShouldBe("cursor-a");
        sourceRequest.QueryText.ShouldBe(isSearch ? "needle" : null);
        sourceRequest.GlobPattern.ShouldBe(isSearch ? null : "*.md");
        sourceRequest.Paths.ShouldHaveSingleItem().Path.NormalizedPath.ShouldBe("docs/readme.md");
    }

    [Theory]
    [InlineData("search")]
    [InlineData("glob")]
    public async Task SearchAndGlobWithoutRequiredLimitShouldRejectBeforeSourceObservation(string queryFamily)
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        bool isSearch = string.Equals(queryFamily, "search", StringComparison.Ordinal);
        object body = isSearch
            ? new
            {
                requestSchemaVersion = "v1",
                queryFamily,
                queryText = "needle",
            }
            : new
            {
                requestSchemaVersion = "v1",
                queryFamily,
                globPattern = "*.md",
            };
        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/folders/folder-a/workspaces/workspace-a/context/{queryFamily}")
        {
            Content = JsonContent.Create(body),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, json);
        json.ShouldContain("\"code\":\"validation_error\"");
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RangeReadShouldReturnOnlyBoundedRangeBytes()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/range-read")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                path = Path("docs/readme.md"),
                startOffset = 0,
                endOffset = 1,
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, json);
        json.ShouldContain("\"contentBytes\":\"YQ==\"");
        source.Requests.ShouldHaveSingleItem().ActionToken.ShouldBe("read_file_content");
    }

    [Theory]
    [InlineData(WorkspaceFileContextSourceStatus.Timeout, HttpStatusCode.RequestTimeout, "query_timeout")]
    [InlineData(WorkspaceFileContextSourceStatus.ResponseLimitExceeded, HttpStatusCode.RequestEntityTooLarge, "response_limit_exceeded")]
    [InlineData(WorkspaceFileContextSourceStatus.Redacted, HttpStatusCode.NotFound, "redacted")]
    [InlineData(WorkspaceFileContextSourceStatus.BinaryDisallowed, HttpStatusCode.NotFound, "redacted")]
    [InlineData(WorkspaceFileContextSourceStatus.LargeFileDisallowed, HttpStatusCode.NotFound, "redacted")]
    [InlineData(WorkspaceFileContextSourceStatus.Unavailable, HttpStatusCode.ServiceUnavailable, "read_model_unavailable")]
    public async Task SourceDenialsShouldReturnSafeProblemDetails(
        WorkspaceFileContextSourceStatus sourceStatus,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        RecordingContextSource source = new() { Status = sourceStatus };
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = new[] { Path("docs/secret.md") },
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus, json);
        json.ShouldContain($"\"code\":\"{expectedCode}\"");
        json.ShouldContain("\"visibility\":\"metadata_only\"");
        json.ShouldNotContain("docs/secret.md", Case.Sensitive);
        json.ShouldNotContain("contentBytes", Case.Sensitive);
        source.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnsatisfiableRangeShouldReturnCanonicalRangeCategory()
    {
        RecordingContextSource source = new() { Status = WorkspaceFileContextSourceStatus.RangeUnsatisfiable };
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/range-read")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                path = Path("docs/readme.md"),
                startOffset = 128,
                endOffset = 256,
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable, json);
        json.ShouldContain("\"category\":\"range_unsatisfiable\"");
        json.ShouldContain("\"code\":\"range_unsatisfiable\"");
        json.ShouldContain("\"visibility\":\"metadata_only\"");
        json.ShouldNotContain("docs/readme.md", Case.Sensitive);
        json.ShouldNotContain("contentBytes", Case.Sensitive);
    }

    [Fact]
    public async Task ReadOnlyContextQueryShouldRejectIdempotencyBeforeSourceObservation()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = new[] { Path("docs/readme.md") },
            }),
        };
        AddQueryHeaders(request);
        request.Headers.Add("Idempotency-Key", "idempotency-a");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.ShouldContain("\"code\":\"idempotency_key_not_allowed\"");
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SafeDenialShouldNotEchoRouteOrPathDetails()
    {
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = new[] { Path("docs/secret.md") },
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        json.ShouldContain("\"visibility\":\"metadata_only\"");
        json.ShouldNotContain("folder-a", Case.Sensitive);
        json.ShouldNotContain("workspace-a", Case.Sensitive);
        json.ShouldNotContain("docs/secret.md", Case.Sensitive);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SafeDenialProblemDetailsShouldNotEchoLeakageCorpusSentinels()
    {
        string[] sentinels = LeakageCorpusSentinels();
        RecordingContextSource source = new();
        await using WebApplication app = BuildApp(source, tenantId: null, principalId: null);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/workspaces/workspace-a/context/metadata")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                paths = sentinels.Select(value => new
                {
                    normalizedPath = "docs/readme.md",
                    displayName = value,
                    pathPolicyClass = "tenant_sensitive_document",
                    unicodeNormalization = "NFC",
                }).ToArray(),
            }),
        };
        AddQueryHeaders(request);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        foreach (string sentinel in sentinels)
        {
            json.ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    private static WebApplication BuildApp(
        RecordingContextSource source,
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        builder.Services.RemoveAll<ITenantContextAccessor>();
        builder.Services.AddSingleton<ITenantContextAccessor>(new StaticTenantContextAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IEventStoreClaimTransformEvidenceAccessor>();
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(new StaticClaimTransformEvidenceAccessor(tenantId, principalId));
        builder.Services.RemoveAll<IUtcClock>();
        builder.Services.AddSingleton<IUtcClock>(new FixedUtcClock(Now));
        builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
        builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(TenantStore());
        builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
        builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(new AllowingFolderPermissionEvidenceProvider());
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, AllowingEventStoreAuthorizationValidator>();
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider>(new AllowingDaprPolicyEvidenceProvider());
        builder.Services.RemoveAll<IWorkspaceFileContextSource>();
        builder.Services.AddSingleton<IWorkspaceFileContextSource>(source);

        WebApplication app = builder.Build();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static void AddQueryHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
    }

    private static PathMetadata Path(string normalizedPath)
        => new(normalizedPath, normalizedPath.Split('/').Last(), "tenant_sensitive_document", "NFC");

    private static string[] LeakageCorpusSentinels()
    {
        string path = System.IO.Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(sample => sample.GetProperty("value").GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static IFolderTenantAccessProjectionStore TenantStore()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "Member"),
            },
            Watermark = 1,
            LastEventTimestamp = Now.AddMinutes(-1),
            ProjectionWatermark = "tenant_watermark_v1",
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class RecordingContextSource : IWorkspaceFileContextSource
    {
        public List<WorkspaceFileContextSourceRequest> Requests { get; } = [];

        public WorkspaceFileContextSourceStatus Status { get; init; } = WorkspaceFileContextSourceStatus.Available;

        public Task<WorkspaceFileContextSourceResult> QueryAsync(
            WorkspaceFileContextSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            WorkspaceFileContextLimits limits = new(
                QueryFamily(request.Kind),
                request.Limit,
                1,
                request.Kind == WorkspaceFileContextQueryKind.Range ? 1 : 128,
                1,
                false,
                "not_truncated");
            FolderLifecycleFreshness freshness = new("snapshot_per_task", Now, "context_watermark_v1", Stale: false, null);

            if (request.Kind == WorkspaceFileContextQueryKind.Range)
            {
                return Task.FromResult(new WorkspaceFileContextSourceResult(
                    Status,
                    [],
                    request.Paths[0].Path,
                    new WorkspaceFileContextRange(request.StartOffset!.Value, request.EndOffset!.Value, 1, Partial: false),
                    Status == WorkspaceFileContextSourceStatus.Available ? "YQ==" : "unsafe",
                    null,
                    limits,
                    freshness));
            }

            return Task.FromResult(new WorkspaceFileContextSourceResult(
                Status,
                [new WorkspaceFileContextItem(Path("docs/readme.md"), "file", 1, "tenant_sensitive", "not_redacted")],
                null,
                null,
                null,
                new WorkspaceFileContextPage(null, request.Limit, false, null),
                limits,
                freshness));
        }

        private static string QueryFamily(WorkspaceFileContextQueryKind kind)
            => kind switch
            {
                WorkspaceFileContextQueryKind.Tree => "tree",
                WorkspaceFileContextQueryKind.Metadata => "metadata",
                WorkspaceFileContextQueryKind.Search => "search",
                WorkspaceFileContextQueryKind.Glob => "glob",
                WorkspaceFileContextQueryKind.Range => "range",
                _ => "metadata",
            };
    }

    private sealed class StaticTenantContextAccessor(string? authoritativeTenantId, string? principalId) : ITenantContextAccessor
    {
        public string? AuthoritativeTenantId => authoritativeTenantId;

        public string? PrincipalId => principalId;
    }

    private sealed class StaticClaimTransformEvidenceAccessor(string? tenantId, string? principalId)
        : IEventStoreClaimTransformEvidenceAccessor
    {
        public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);
            return string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId)
                ? EventStoreClaimTransformEvidence.Missing()
                : EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [actionToken]);
        }
    }

    private sealed class AllowingFolderPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1"));
    }

    private sealed class AllowingDaprPolicyEvidenceProvider : IDaprPolicyEvidenceProvider
    {
        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
    }
}
