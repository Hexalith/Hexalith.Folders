using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Tests.Queries.Folders;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.ContextSearch;

public sealed class ContextSearchQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldReturnSafeBeforeAnyEgress()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(tenantId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.AuthenticationRequired);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingFolderShouldReturnNotFoundSafeBeforeEgress()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(folderId: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.NotFoundSafe);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task BlankQueryTextShouldReturnValidationBeforeEgress()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(queryText: "  "),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ValidationFailed);
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(257, 10)]
    [InlineData(10, 501)]
    public async Task OverC4BoundsShouldReturnInputLimitBeforeEgress(int queryTextLength, int limit)
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(queryText: new string('a', queryTextLength), limit: limit),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.InputLimitExceeded);
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("bad-cursor")]
    [InlineData("memories-search:-1")]
    [InlineData("")]
    public async Task MalformedCursorShouldReturnValidationBeforeEgress(string cursor)
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(cursor: cursor),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ValidationFailed);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidNegativeLimitShouldNotEchoInvalidLimitInSafeMetadata()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(limit: -1),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ValidationFailed);
        result.Limits.ConfiguredLimit.ShouldBe(500);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TenantDenialShouldReturnBeforeSourceObservation()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([]),
            tenantStore: new CountingTenantAccessProjectionStore(TenantProjection(principals: ["someone-else"])));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.AuthorizationDenied);
        result.Items.ShouldBeEmpty();
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TenantDenialShouldWinOverInputLimitBeforeSourceObservation()
    {
        RecordingFolderSearchSource source = new();
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([]),
            tenantStore: new CountingTenantAccessProjectionStore(TenantProjection(principals: ["someone-else"])));

        ContextSearchQueryResult result = await handler.HandleAsync(
            Query(limit: 501),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.AuthorizationDenied);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AuthorizedSearchShouldReachSourceWithAuthoritativeScopedRequest()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1")],
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        FolderSearchSourceRequest request = source.Requests.ShouldHaveSingleItem();
        request.ManagedTenantId.ShouldBe("tenant-a");
        request.OrganizationId.ShouldBe("org-a");
        request.FolderId.ShouldBe("folder-a");
        request.WorkspaceId.ShouldBe("workspace-a");
        request.PrincipalId.ShouldBe("user-a");
        request.ActionToken.ShouldBe(ContextSearchQueryHandler.ActionToken);
        request.QueryText.ShouldBe("needle");

        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-1");
        item.IndexingStatus.ShouldBe("indexed");
        result.Limits.QueryFamily.ShouldBe(ContextSearchQueryHandler.QueryFamily);
        result.Freshness.ReadConsistency.ShouldBe("eventually_consistent");
        result.Freshness.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizedSearchShouldDropStaleBridgeEntries()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1")],
        };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-1", SemanticIndexingBridgeStatus.Stale)]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldBeEmpty();
        source.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UnavailableBridgeAfterAuthorizationShouldFailClosedBeforeMemoriesEgress()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-1")], isAvailable: false));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ReadModelUnavailable);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
        source.Requests.ShouldBeEmpty();

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("folders://", Case.Sensitive);
        serialized.ShouldNotContain("snippet", Case.Insensitive);
        serialized.ShouldNotContain("sourceUri", Case.Insensitive);
        serialized.ShouldNotContain("normalizedPath", Case.Insensitive);
        serialized.ShouldNotContain("memories unreachable", Case.Insensitive);
    }

    [Fact]
    public async Task AuthorizationDenialShouldWinOverUnavailableBridgeAndSkipMemories()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-1")], isAvailable: false),
            tenantStore: new CountingTenantAccessProjectionStore(TenantProjection(principals: ["someone-else"])));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.AuthorizationDenied);
        result.Items.ShouldBeEmpty();
        source.Requests.ShouldBeEmpty();

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("folders://", Case.Sensitive);
        serialized.ShouldNotContain("unavailable", Case.Insensitive);
        serialized.ShouldNotContain("IsAvailable", Case.Sensitive);
    }

    [Fact]
    public async Task ThrowingBridgeShouldDegradeToReadModelUnavailable()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new ThrowingBridgeReadModel());

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ReadModelUnavailable);
        result.Items.ShouldBeEmpty();
        source.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AuthorizedSearchWithoutOrganizationShouldFailClosedBeforeSourceObservation()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1")]), organizationId: null);

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ReadModelUnavailable);
        result.Items.ShouldBeEmpty();
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTenantHitShouldBeTrimmedEvenWhenTheIndexEchoesIt()
    {
        // The shared folders-index could echo a foreign hit despite the server-side filter; the Folders-side
        // trim is the authoritative control. Only the tenant-a hit may survive.
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-b", tenantId: "tenant-b"), Hit("fv-a")],
            TotalCount = 2,
        };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-a"), Entry("fv-b", tenantId: "tenant-b")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-a");
    }

    [Fact]
    public async Task CrossWorkspaceHitShouldBeTrimmedEvenWhenTheIndexEchoesIt()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-b", workspaceId: "workspace-b"), Hit("fv-a")],
            TotalCount = 2,
        };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-a"), Entry("fv-b", workspaceId: "workspace-b")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-a");
    }

    [Fact]
    public async Task CrossOrganizationHitShouldBeTrimmedEvenWhenTheIndexEchoesIt()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-b", organizationId: "org-b"), Hit("fv-a")],
            TotalCount = 2,
        };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-a"), Entry("fv-b", organizationId: "org-b")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-a");
    }

    [Fact]
    public async Task DuplicateHitsForOneFileVersionShouldYieldOneItemWithTheFirstScore()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1", score: 2.0), Hit("fv-1", score: 1.5), Hit("fv-2", score: 1.0), Hit("fv-1", score: 0.5)],
            RawCount = 4,
            TotalCount = 4,
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1"), Entry("fv-2")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.Select(item => item.FileVersionReference).ShouldBe(["fv-1", "fv-2"]);
        result.Items[0].Score.ShouldBe(2.0);
        result.Items[1].Score.ShouldBe(1.0);
        result.Limits.IsTruncated.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task DuplicateRowsShouldCountTowardAFullPageSoLaterRowsStayReachable()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1", score: 2.0), Hit("fv-1", score: 1.5)],
            RawCount = 2,
            TotalCount = 3,
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1"), Entry("fv-2")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(limit: 2), TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem().FileVersionReference.ShouldBe("fv-1");
        result.Limits.IsTruncated.ShouldBeTrue();
        result.NextCursor.ShouldBe("memories-search:2");
    }

    [Fact]
    public async Task RejectedFileVersionShouldBeHydratedOnceAndNotCountTowardAFullPage()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-ghost", score: 2.0), Hit("fv-ghost", score: 1.5)],
            RawCount = 2,
            TotalCount = 3,
        };
        StubBridgeReadModel bridge = new([]);
        ContextSearchQueryHandler handler = Handler(source, bridge);

        ContextSearchQueryResult result = await handler.HandleAsync(Query(limit: 2), TestContext.Current.CancellationToken);

        result.Items.ShouldBeEmpty();
        bridge.ByIdReads.ShouldBe(1);
        result.Limits.IsTruncated.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task DroppedDuplicateShouldNotSuppressALaterValidRowForTheSameFileVersion()
    {
        // The first row carries a foreign workspace and is trimmed before hydration, so it must not mark the file
        // version as emitted. The later in-scope row still represents it.
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1", workspaceId: "workspace-b", score: 2.0), Hit("fv-1", score: 1.0)],
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-1");
        item.Score.ShouldBe(1.0);
    }

    [Fact]
    public async Task HitWithoutAuthoritativeBridgeEntryShouldBeDropped()
    {
        // The index is non-authoritative: a hit with no current bridge entry must not be returned.
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-ghost")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task HydratedEntryOutsideAuthorizedWorkspaceShouldBeDropped()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1", workspaceId: "workspace-b")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task HydratedEntryOutsideAuthorizedOrganizationShouldBeDropped()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1", organizationId: "org-b")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SemanticIndexingBridgeStatus.Stale)]
    [InlineData(SemanticIndexingBridgeStatus.Tombstoned)]
    [InlineData(SemanticIndexingBridgeStatus.Skipped)]
    [InlineData(SemanticIndexingBridgeStatus.Failed)]
    [InlineData(SemanticIndexingBridgeStatus.ReconciliationRequired)]
    [InlineData(SemanticIndexingBridgeStatus.Unknown)]
    public async Task HitWithNonLiveBridgeStatusShouldBeDropped(SemanticIndexingBridgeStatus status)
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1", status)]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SensitivePathPolicyClassShouldYieldDistinctRedactedMarker()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-secret")] };
        ContextSearchQueryHandler handler = Handler(
            source,
            new StubBridgeReadModel([Entry("fv-secret", pathPolicyClass: "tenant_secret_document")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.Redaction.ShouldBe("redacted");
        item.Sensitivity.ShouldBe("restricted");
    }

    [Theory]
    [InlineData(FolderSearchSourceStatus.Unavailable, ContextSearchResultCode.ReadModelUnavailable)]
    [InlineData(FolderSearchSourceStatus.Degraded, ContextSearchResultCode.ReadModelUnavailable)]
    [InlineData(FolderSearchSourceStatus.Timeout, ContextSearchResultCode.QueryTimeout)]
    public async Task SourceFailuresShouldMapToSafeCodes(
        FolderSearchSourceStatus sourceStatus,
        ContextSearchResultCode expected)
    {
        RecordingFolderSearchSource source = new() { Status = sourceStatus };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expected);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThrowingSourceShouldDegradeToReadModelUnavailable()
    {
        ContextSearchQueryHandler handler = Handler(new ThrowingFolderSearchSource(), new StubBridgeReadModel([]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ReadModelUnavailable);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task TruncatedResultsShouldEmitNextCursorThatRoundTripsToOffset()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1")],
            TotalCount = 5,
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1")]));

        ContextSearchQueryResult first = await handler.HandleAsync(Query(limit: 1), TestContext.Current.CancellationToken);

        first.Limits.IsTruncated.ShouldBeTrue();
        first.NextCursor.ShouldNotBeNull();

        ContextSearchQueryResult second = await handler.HandleAsync(
            Query(limit: 1, cursor: first.NextCursor),
            TestContext.Current.CancellationToken);

        second.Code.ShouldBe(ContextSearchResultCode.Allowed);
        source.Requests.Count.ShouldBe(2);
        source.Requests[1].Offset.ShouldBe(1);
    }

    [Fact]
    public async Task AllDiscardedRowsShouldNotExposeRawSourcePagination()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [],
            RawCount = 1,
            TotalCount = 5,
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([]));

        ContextSearchQueryResult first = await handler.HandleAsync(Query(limit: 1), TestContext.Current.CancellationToken);

        first.Code.ShouldBe(ContextSearchResultCode.Allowed);
        first.Items.ShouldBeEmpty();
        first.Limits.IsTruncated.ShouldBeFalse();
        first.NextCursor.ShouldBeNull();
        source.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SourceReturningMoreRowsThanRequestedShouldNotExceedLimit()
    {
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-1"), Hit("fv-2")],
            TotalCount = 10,
            RawCount = 2,
        };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1"), Entry("fv-2")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(limit: 1), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        ContextSearchItem item = result.Items.ShouldHaveSingleItem();
        item.FileVersionReference.ShouldBe("fv-1");
        result.Limits.ConfiguredLimit.ShouldBe(1);
        result.Limits.IsTruncated.ShouldBeTrue();
        result.NextCursor.ShouldBe("memories-search:1");
    }

    [Fact]
    public async Task ResultShouldBeMetadataOnlyWithNoPathOrSnippetOrSourceUri()
    {
        RecordingFolderSearchSource source = new() { Hits = [Hit("fv-1")] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry("fv-1")]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("folders://", Case.Sensitive);
        serialized.ShouldNotContain("snippet", Case.Insensitive);
        serialized.ShouldNotContain("sourceUri", Case.Insensitive);
        serialized.ShouldNotContain("normalizedPath", Case.Insensitive);
    }

    [Fact]
    public async Task OversizedSerializedResponseShouldReturnResponseLimitExceeded()
    {
        string oversizedFileVersionId = new('a', 1_100_000);
        RecordingFolderSearchSource source = new() { Hits = [Hit(oversizedFileVersionId)] };
        ContextSearchQueryHandler handler = Handler(source, new StubBridgeReadModel([Entry(oversizedFileVersionId)]));

        ContextSearchQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.ResponseLimitExceeded);
        result.Items.ShouldBeEmpty();
        result.Limits.ActualBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("missing", 1, false, 2)]
    [InlineData("stale", 1, false, 2)]
    [InlineData("duplicate", 1, false, 1)]
    [InlineData("foreign-hydration", 1, false, 2)]
    [InlineData("miskeyed-tenant", 1, false, 2)]
    [InlineData("miskeyed-folder", 1, false, 2)]
    [InlineData("miskeyed-version", 1, false, 2)]
    [InlineData("visible", 1, true, 2)]
    [InlineData("visible", 2, true, 2)]
    [InlineData("partial", 2, false, 1)]
    public async Task GatewayContinuationRequiresAFullWindowAndADistinctVisibleTail(
        string scenario, int limit, bool hasMore, int bridgeReads)
    {
        string tail = scenario == "duplicate" ? "fv-first" : "fv-tail";
        FolderSearchSourceHit[] first = scenario == "partial"
            ? [Hit("fv-first"), Hit("foreign", tenantId: "tenant-b")]
            : [.. Enumerable.Repeat(Hit("fv-first"), limit)];
        RecordingFolderSearchSource source = new()
        {
            Hits = first,
            TotalCount = limit + 1,
            Continuation = (_, _) => Task.FromResult(new FolderSearchSourceResult(
                FolderSearchSourceStatus.Available, [Hit(tail)], limit + 1, 1)),
        };
        List<SemanticIndexingBridgeEntry> entries = [Entry("fv-first")];
        if (scenario == "stale") entries.Add(Entry(tail, SemanticIndexingBridgeStatus.Stale));
        if (scenario == "foreign-hydration") entries.Add(Entry(tail, workspaceId: "workspace-b"));
        if (scenario == "visible") entries.Add(Entry(tail));
        SemanticIndexingBridgeEntry? miskeyed = scenario switch
        {
            "miskeyed-tenant" => Entry(tail, tenantId: "tenant-b"),
            "miskeyed-folder" => Entry(tail, folderId: "folder-b"),
            "miskeyed-version" => Entry("fv-other"),
            _ => null,
        };
        StubBridgeReadModel bridge = new(entries)
        {
            HydrateById = fileVersionId => fileVersionId == tail && miskeyed is not null
                ? Task.FromResult<SemanticIndexingBridgeEntry?>(miskeyed) : null,
        };

        ContextSearchQueryResult result = await Handler(source, bridge).HandleGatewayAsync(Query(limit: limit),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ContextSearchResultCode.Allowed);
        result.Items.ShouldHaveSingleItem();
        result.NextCursor.ShouldBe(hasMore ? $"memories-search:{limit}" : null);
        result.Limits.IsTruncated.ShouldBe(hasMore);
        source.Requests.Select(request => request.Offset).ShouldBe(scenario == "partial" ? [0] : [0, limit]);
        bridge.ByIdReads.ShouldBe(bridgeReads);
    }

    [Theory]
    [InlineData("status-unavailable", ContextSearchResultCode.ReadModelUnavailable)]
    [InlineData("status-degraded", ContextSearchResultCode.ReadModelUnavailable)]
    [InlineData("status-timeout", ContextSearchResultCode.QueryTimeout)]
    [InlineData("timeout-exception", ContextSearchResultCode.QueryTimeout)]
    [InlineData("deadline", ContextSearchResultCode.QueryTimeout)]
    [InlineData("hydration-deadline", ContextSearchResultCode.QueryTimeout)]
    [InlineData("exception", ContextSearchResultCode.ReadModelUnavailable)]
    public async Task GatewayContinuationFailureDoesNotReturnAPartialSuccess(string scenario, ContextSearchResultCode expected)
    {
        TaskCompletionSource<FolderSearchSourceResult> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<SemanticIndexingBridgeEntry?> pendingHydration = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingFolderSearchSource source = new()
        {
            Hits = [Hit("fv-first")],
            TotalCount = 2,
            Continuation = (_, _) => scenario switch
            {
                "status-unavailable" => Task.FromResult(FolderSearchSourceResult.Unavailable()),
                "status-degraded" => Task.FromResult(new FolderSearchSourceResult(FolderSearchSourceStatus.Degraded, [], 0, 0)),
                "status-timeout" => Task.FromResult(new FolderSearchSourceResult(FolderSearchSourceStatus.Timeout, [], 0, 0)),
                "timeout-exception" => throw new TimeoutException("private timeout details"),
                "deadline" => pending.Task,
                "hydration-deadline" => Task.FromResult(new FolderSearchSourceResult(
                    FolderSearchSourceStatus.Available, [Hit("fv-tail")], 2, 1)),
                _ => throw new InvalidOperationException("private source details"),
            },
        };
        StubBridgeReadModel bridge = new([Entry("fv-first")])
        {
            HydrateById = fileVersionId => fileVersionId == "fv-tail" ? pendingHydration.Task : null,
        };
        CancellationToken token = TestContext.Current.CancellationToken;
        try
        {
            ContextSearchQueryResult result = await Handler(source, bridge).HandleGatewayAsync(Query(limit: 1), token)
                .WaitAsync(TimeSpan.FromSeconds(10), token);

            result.Code.ShouldBe(expected);
            result.Items.ShouldBeEmpty();
            result.NextCursor.ShouldBeNull();
            source.Requests.Select(request => request.Offset).ShouldBe([0, 1]);
            bridge.ByIdReads.ShouldBe(scenario == "hydration-deadline" ? 2 : 1);
        }
        finally
        {
            pending.TrySetResult(FolderSearchSourceResult.Unavailable());
            pendingHydration.TrySetResult(null);
        }
    }

    private static ContextSearchQueryHandler Handler(
        IFolderSearchSource source,
        ISemanticIndexingBridgeReadModel bridge,
        IFolderTenantAccessProjectionStore? tenantStore = null,
        string? organizationId = "org-a")
    {
        FixedUtcClock clock = new(Now);
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(
                tenantStore ?? new CountingTenantAccessProjectionStore(TenantProjection(principals: ["user-a"])),
                clock,
                new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(
                FolderPermissionEvidenceResult.Allowed("permission_watermark_v1", organizationId: organizationId)),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            clock);

        return new ContextSearchQueryHandler(authorization, source, bridge, clock);
    }

    private static ContextSearchQuery Query(
        string? folderId = "folder-a",
        string? workspaceId = "workspace-a",
        string? tenantId = "tenant-a",
        string? principalId = "user-a",
        string? queryText = "needle",
        int? limit = 50,
        string? cursor = null)
        => new(
            folderId,
            workspaceId,
            tenantId,
            principalId,
            EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [ContextSearchQueryHandler.ActionToken]),
            "correlation-a",
            "task-a",
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null,
            queryText,
            limit,
            cursor);

    private static FolderSearchSourceHit Hit(
        string fileVersionId,
        string tenantId = "tenant-a",
        string organizationId = "org-a",
        string folderId = "folder-a",
        string workspaceId = "workspace-a",
        double score = 1.0)
        => new(tenantId, organizationId, folderId, workspaceId, fileVersionId, score);

    private static SemanticIndexingBridgeEntry Entry(
        string fileVersionId,
        SemanticIndexingBridgeStatus status = SemanticIndexingBridgeStatus.Indexed,
        string tenantId = "tenant-a",
        string organizationId = "org-a",
        string folderId = "folder-a",
        string workspaceId = "workspace-a",
        string? pathPolicyClass = "tenant_sensitive_document")
        => new(
            new SemanticIndexingFileVersionIdentity(
                tenantId,
                organizationId,
                folderId,
                workspaceId,
                "op-a",
                "digest-" + fileVersionId,
                fileVersionId,
                "hash-" + fileVersionId,
                $"folders://{tenantId}/organizations/{organizationId}/folders/{folderId}/workspaces/{workspaceId}/file-versions/{fileVersionId}"),
            status,
            "reason_ok",
            retryable: false,
            "correlation-a",
            "task-a",
            Now,
            new SemanticIndexingEvidence(pathPolicyClass: pathPolicyClass));

    private static FolderTenantAccessProjection TenantProjection(string tenantId = "tenant-a", params string[] principals)
        => new()
        {
            TenantId = tenantId,
            Enabled = true,
            Principals = principals.ToDictionary(
                static principal => principal,
                static principal => new FolderTenantPrincipalEvidence(principal, "Member"),
                StringComparer.Ordinal),
            Watermark = 7,
            LastEventTimestamp = Now.AddMinutes(-1),
            ProjectionWatermark = "tenant_watermark_v1",
        };

    private sealed class RecordingFolderSearchSource : IFolderSearchSource
    {
        public List<FolderSearchSourceRequest> Requests { get; } = [];

        public FolderSearchSourceStatus Status { get; init; } = FolderSearchSourceStatus.Available;

        public IReadOnlyList<FolderSearchSourceHit> Hits { get; init; } = [];

        public long TotalCount { get; init; }

        public int? RawCount { get; init; }

        public Func<FolderSearchSourceRequest, CancellationToken, Task<FolderSearchSourceResult>>? Continuation { get; init; }

        public Task<FolderSearchSourceResult> SearchAsync(
            FolderSearchSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Offset > 0 && Continuation is not null)
            {
                return Continuation(request, cancellationToken);
            }

            return Task.FromResult(new FolderSearchSourceResult(
                Status,
                Hits,
                TotalCount == 0 ? Hits.Count : TotalCount,
                RawCount ?? Hits.Count));
        }
    }

    private sealed class ThrowingFolderSearchSource : IFolderSearchSource
    {
        public Task<FolderSearchSourceResult> SearchAsync(
            FolderSearchSourceRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("memories unreachable");
    }

    private sealed class ThrowingBridgeReadModel : ISemanticIndexingBridgeReadModel
    {
        public bool IsAvailable => true;

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
            SemanticIndexingFileVersionIdentity identity,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("bridge unavailable");

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
            string managedTenantId,
            string folderId,
            string fileVersionId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("bridge unavailable");

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
            string managedTenantId,
            string folderId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("bridge unavailable");
    }

    private sealed class StubBridgeReadModel(IReadOnlyList<SemanticIndexingBridgeEntry> entries, bool isAvailable = true) : ISemanticIndexingBridgeReadModel
    {
        public bool IsAvailable => isAvailable;

        public int ByIdReads { get; private set; }

        public Func<string, Task<SemanticIndexingBridgeEntry?>?>? HydrateById { get; init; }

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
            SemanticIndexingFileVersionIdentity identity,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries.FirstOrDefault(e => e.Identity.ReadModelKey == identity.ReadModelKey));

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
            string managedTenantId,
            string folderId,
            string fileVersionId,
            CancellationToken cancellationToken = default)
        {
            ByIdReads++;
            return HydrateById?.Invoke(fileVersionId) ?? Task.FromResult(entries.FirstOrDefault(e =>
                string.Equals(e.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FolderId, folderId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FileVersionId, fileVersionId, StringComparison.Ordinal)));
        }

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
            string managedTenantId,
            string folderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries);
    }
}
