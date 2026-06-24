using System.Text.Json;

using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Server.ContextSearch;

using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Story 10.5 — the Option-B Memories gateway: identity is recovered from <c>ScoredResult.SourceUri</c> only, the
/// content snippet is dropped, the query is constrained by the security-trim attribute filters (incl.
/// <c>folders.status=active</c>), malformed hits are skipped, and remote/in-band degradation maps to a safe status.
/// </summary>
public sealed class MemoriesFolderSearchSourceTests
{
    private static readonly FolderSearchSourceRequest Request = new(
        "tenant-a",
        "org-a",
        "folder-a",
        "workspace-a",
        "user-a",
        ContextSearchQueryHandler.ActionToken,
        "task-a",
        "corr-a",
        AuthorizationWatermark: "watermark-a",
        "needle",
        Limit: 50,
        Offset: 0);

    [Fact]
    public async Task ShouldRecoverIdentityFromSourceUriDropSnippetAndApplySecurityTrimFilters()
    {
        const string sentinelSecret = "SENTINEL-ghp_supersecretleakcanary0123456789";
        FakeMemoriesClient client = new(Result(
            Scored("folders://tenant-a/organizations/org-a/folders/folder-a/workspaces/workspace-a/file-versions/fv-a", 2.5, sentinelSecret)));
        MemoriesFolderSearchSource source = new(client);

        FolderSearchSourceResult result = await source.SearchAsync(Request, TestContext.Current.CancellationToken);

        // AC3 sentinel: the dropped ContentSnippet (a secret-shaped canary) must not survive into the result.
        JsonSerializer.Serialize(result).ShouldNotContain(sentinelSecret, Shouldly.Case.Sensitive);

        result.Status.ShouldBe(FolderSearchSourceStatus.Available);
        FolderSearchSourceHit hit = result.Hits.ShouldHaveSingleItem();
        hit.ManagedTenantId.ShouldBe("tenant-a");
        hit.OrganizationId.ShouldBe("org-a");
        hit.FolderId.ShouldBe("folder-a");
        hit.WorkspaceId.ShouldBe("workspace-a");
        hit.FileVersionId.ShouldBe("fv-a");
        hit.Score.ShouldBe(2.5);

        // The query was constrained to the shared index with the authoritative security-trim filters + status=active.
        client.LastRequest.ShouldNotBeNull();
        client.LastRequest.TenantId.ShouldBe(FoldersSemanticIndexingAttributes.IndexTenant);
        client.LastRequest.Axis.ShouldBe(FoldersSemanticIndexingAttributes.SearchAxis);
        IReadOnlyDictionary<string, string> filters = client.LastRequest.AttributeFilters.ShouldNotBeNull();
        filters[FoldersSemanticIndexingAttributes.ManagedTenantIdAttribute].ShouldBe("tenant-a");
        filters[FoldersSemanticIndexingAttributes.OrganizationIdAttribute].ShouldBe("org-a");
        filters[FoldersSemanticIndexingAttributes.FolderIdAttribute].ShouldBe("folder-a");
        filters[FoldersSemanticIndexingAttributes.StatusAttribute].ShouldBe(FoldersSemanticIndexingAttributes.StatusActive);
    }

    [Fact]
    public async Task ShouldDropHitsWithMalformedSourceUri()
    {
        FakeMemoriesClient client = new(Result(
            Scored("file:///etc/passwd", 1.0, "x"),
            Scored("folders://tenant-a/organizations/org-a/folders/folder-a/workspaces/workspace-a", 1.0, "x"),
            Scored("folders://tenant-a/organizations/org-a/folders/folder-a/workspaces/workspace-a/file-versions/fv-ok", 1.0, "x")));
        MemoriesFolderSearchSource source = new(client);

        FolderSearchSourceResult result = await source.SearchAsync(Request, TestContext.Current.CancellationToken);

        FolderSearchSourceHit hit = result.Hits.ShouldHaveSingleItem();
        hit.FileVersionId.ShouldBe("fv-ok");
    }

    [Fact]
    public async Task DegradedSearchResultShouldMapToDegradedStatus()
    {
        FakeMemoriesClient client = new(new SearchResult
        {
            Results = [],
            TotalCount = 0,
            HasIndexedMemoryUnits = true,
            Query = "needle",
            Degraded = true,
        });
        MemoriesFolderSearchSource source = new(client);

        FolderSearchSourceResult result = await source.SearchAsync(Request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderSearchSourceStatus.Degraded);
        result.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnavailableSyntacticAxisShouldMapToDegradedStatus()
    {
        FakeMemoriesClient client = new(new SearchResult
        {
            Results = [],
            TotalCount = 0,
            HasIndexedMemoryUnits = true,
            Query = "needle",
            UnavailableAxes = ["syntactic"],
        });
        MemoriesFolderSearchSource source = new(client);

        FolderSearchSourceResult result = await source.SearchAsync(Request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderSearchSourceStatus.Degraded);
    }

    [Fact]
    public async Task TransportFailureShouldMapToUnavailableNeverThrow()
    {
        FakeMemoriesClient client = new(new HttpRequestException("memories unreachable"));
        MemoriesFolderSearchSource source = new(client);

        FolderSearchSourceResult result = await source.SearchAsync(Request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(FolderSearchSourceStatus.Unavailable);
        result.Hits.ShouldBeEmpty();
    }

    private static SearchResult Result(params ScoredResult[] results)
        => new()
        {
            Results = results,
            TotalCount = results.Length,
            HasIndexedMemoryUnits = true,
            Query = "needle",
        };

    private static ScoredResult Scored(string sourceUri, double score, string snippet)
        => new()
        {
            MemoryUnitId = "mu-" + sourceUri.GetHashCode(System.StringComparison.Ordinal),
            Score = score,
            ContentSnippet = snippet,
            SourceUri = sourceUri,
            SourceType = SourceType.File,
        };

    private sealed class FakeMemoriesClient : MemoriesClient
    {
        private readonly SearchResult? _result;
        private readonly Exception? _throw;

        public FakeMemoriesClient(SearchResult result)
            : base(new HttpClient(), Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance)
            => _result = result;

        public FakeMemoriesClient(Exception toThrow)
            : base(new HttpClient(), Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance)
            => _throw = toThrow;

        public SearchRequest? LastRequest { get; private set; }

        public override Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return _throw is not null ? throw _throw : Task.FromResult(_result!);
        }
    }
}
