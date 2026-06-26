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
    public async Task PaginationShouldAdvanceByRawSourceRowsWhenAllRowsAreDiscarded()
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

        public Task<FolderSearchSourceResult> SearchAsync(
            FolderSearchSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
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

    private sealed class StubBridgeReadModel(IReadOnlyList<SemanticIndexingBridgeEntry> entries) : ISemanticIndexingBridgeReadModel
    {
        public bool IsAvailable => true;

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
            SemanticIndexingFileVersionIdentity identity,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries.FirstOrDefault(e => e.Identity.ReadModelKey == identity.ReadModelKey));

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
            string managedTenantId,
            string folderId,
            string fileVersionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries.FirstOrDefault(e =>
                string.Equals(e.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FolderId, folderId, StringComparison.Ordinal)
                && string.Equals(e.Identity.FileVersionId, fileVersionId, StringComparison.Ordinal)));

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
            string managedTenantId,
            string folderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(entries);
    }
}
