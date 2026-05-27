using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Tests.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.FileContext;

public sealed class WorkspaceFileContextQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TenantDenialShouldReturnBeforeContextSourceObservation()
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(
            source,
            tenantStore: new CountingTenantAccessProjectionStore(TenantProjection(principals: ["user-b"])));

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.AuthorizationDenied);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PathPolicyShouldRunBeforeSensitivityAndContextSource()
    {
        RecordingContextSource source = new();
        RecordingSensitivityClassifier sensitivity = new(WorkspacePathSensitivityResult.Allowed());
        WorkspaceFileContextQueryHandler handler = Handler(source, sensitivityClassifier: sensitivity);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(paths: [Path("../secret.txt")]),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.PathValidationFailed);
        sensitivity.Requests.ShouldBeEmpty();
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("docs\\mixed.txt")]
    [InlineData("docs/%2e%2e/secret.txt")]
    [InlineData("docs%2fsecret.txt")]
    [InlineData("docs/NUL.md")]
    [InlineData("docs/readme\u200d.md")]
    [InlineData("docs/cafe\u0301.txt")]
    public async Task UnsafeContextPathsShouldDenyBeforeSourceAndNeverEchoPath(string normalizedPath)
    {
        ArgumentNullException.ThrowIfNull(normalizedPath);

        RecordingContextSource source = new();
        RecordingSensitivityClassifier sensitivity = new(WorkspacePathSensitivityResult.Allowed());
        WorkspaceFileContextQueryHandler handler = Handler(source, sensitivityClassifier: sensitivity);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(paths: [Path(normalizedPath)]),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.PathValidationFailed);
        sensitivity.Requests.ShouldBeEmpty();
        source.Requests.ShouldBeEmpty();

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain(normalizedPath, Case.Sensitive);
    }

    [Fact]
    public async Task SensitivityRedactionShouldReturnBeforeContextSource()
    {
        RecordingContextSource source = new();
        RecordingSensitivityClassifier sensitivity = new(WorkspacePathSensitivityResult.Redacted());
        WorkspaceFileContextQueryHandler handler = Handler(source, sensitivityClassifier: sensitivity);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(paths: [Path("docs/secret.md", pathPolicyClass: "tenant_secret_document")]),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.Redacted);
        sensitivity.Requests.Count.ShouldBe(1);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InputBoundsShouldReturnBeforeContextSource()
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(
                WorkspaceFileContextQueryKind.Search,
                queryText: new string('a', 257),
                limit: 10),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.InputLimitExceeded);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task MetadataWithoutPathsShouldReturnValidationBeforeContextSource()
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(WorkspaceFileContextQueryKind.Metadata, paths: []),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.ValidationFailed);
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(WorkspaceFileContextQueryKind.Search)]
    [InlineData(WorkspaceFileContextQueryKind.Glob)]
    public async Task SearchAndGlobWithoutRequiredLimitShouldReturnValidationBeforeContextSource(
        WorkspaceFileContextQueryKind kind)
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(kind, limit: null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.ValidationFailed);
        source.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(WorkspaceFileContextQueryKind.Tree)]
    [InlineData(WorkspaceFileContextQueryKind.Metadata)]
    [InlineData(WorkspaceFileContextQueryKind.Search)]
    [InlineData(WorkspaceFileContextQueryKind.Glob)]
    [InlineData(WorkspaceFileContextQueryKind.Range)]
    public async Task SuccessfulQueriesShouldReachSourceWithSafeAuthoritativeRequest(WorkspaceFileContextQueryKind kind)
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(kind, paths: kind is WorkspaceFileContextQueryKind.Metadata or WorkspaceFileContextQueryKind.Range ? [Path("docs/readme.md")] : null),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.Allowed);
        WorkspaceFileContextSourceRequest request = source.Requests.ShouldHaveSingleItem();
        request.ManagedTenantId.ShouldBe("tenant-a");
        request.FolderId.ShouldBe("folder-a");
        request.WorkspaceId.ShouldBe("workspace-a");
        request.PrincipalId.ShouldBe("user-a");
        request.ActionToken.ShouldBe(kind == WorkspaceFileContextQueryKind.Range ? "read_file_content" : "read_metadata");
        request.AuthorizationWatermark.ShouldBe("permission_watermark_v1");

        if (kind == WorkspaceFileContextQueryKind.Range)
        {
            result.ContentBytes.ShouldBe("YQ==");
            result.Items.ShouldBeEmpty();
        }
        else
        {
            result.Items.Count.ShouldBe(1);
            result.ContentBytes.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ReversedRangeShouldReturnValidationBeforeContextSource()
    {
        RecordingContextSource source = new();
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(WorkspaceFileContextQueryKind.Range, paths: [Path("docs/readme.md")], startOffset: 10, endOffset: 2),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.ValidationFailed);
        source.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RangeSourceOverRequestedWindowShouldReturnResponseLimitWithoutBytes()
    {
        RecordingContextSource source = new() { RangeActualBytes = 2 };
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(WorkspaceFileContextQueryKind.Range, paths: [Path("docs/readme.md")], startOffset: 0, endOffset: 1),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.ResponseLimitExceeded);
        result.RangePath.ShouldBeNull();
        result.Range.ShouldBeNull();
        result.ContentBytes.ShouldBeNull();
    }

    [Theory]
    [InlineData(WorkspaceFileContextSourceStatus.Unavailable, WorkspaceFileContextResultCode.ReadModelUnavailable)]
    [InlineData(WorkspaceFileContextSourceStatus.Stale, WorkspaceFileContextResultCode.ProjectionStale)]
    [InlineData(WorkspaceFileContextSourceStatus.Timeout, WorkspaceFileContextResultCode.QueryTimeout)]
    [InlineData(WorkspaceFileContextSourceStatus.InputLimitExceeded, WorkspaceFileContextResultCode.InputLimitExceeded)]
    [InlineData(WorkspaceFileContextSourceStatus.ResponseLimitExceeded, WorkspaceFileContextResultCode.ResponseLimitExceeded)]
    [InlineData(WorkspaceFileContextSourceStatus.Redacted, WorkspaceFileContextResultCode.Redacted)]
    [InlineData(WorkspaceFileContextSourceStatus.BinaryDisallowed, WorkspaceFileContextResultCode.Redacted)]
    [InlineData(WorkspaceFileContextSourceStatus.LargeFileDisallowed, WorkspaceFileContextResultCode.Redacted)]
    [InlineData(WorkspaceFileContextSourceStatus.RangeUnsatisfiable, WorkspaceFileContextResultCode.RangeUnsatisfiable)]
    public async Task SourceDenialsShouldReturnSafeMetadataOnlyResults(
        WorkspaceFileContextSourceStatus sourceStatus,
        WorkspaceFileContextResultCode expectedCode)
    {
        RecordingContextSource source = new() { Status = sourceStatus };
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(WorkspaceFileContextQueryKind.Range, paths: [Path("docs/readme.md")]),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expectedCode);
        result.Items.ShouldBeEmpty();
        result.RangePath.ShouldBeNull();
        result.Range.ShouldBeNull();
        result.ContentBytes.ShouldBeNull();
        source.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(WorkspaceFileContextQueryKind.Search)]
    [InlineData(WorkspaceFileContextQueryKind.Glob)]
    public async Task AvailableSourceOverResponseBudgetShouldReturnResponseLimitWithoutResults(
        WorkspaceFileContextQueryKind kind)
    {
        RecordingContextSource source = new() { ItemCount = 501 };
        WorkspaceFileContextQueryHandler handler = Handler(source);

        WorkspaceFileContextQueryResult result = await handler.HandleAsync(
            Query(kind, limit: 500),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(WorkspaceFileContextResultCode.ResponseLimitExceeded);
        result.Items.ShouldBeEmpty();
        result.ContentBytes.ShouldBeNull();
    }

    private static WorkspaceFileContextQueryHandler Handler(
        RecordingContextSource source,
        IWorkspaceFileSensitivityClassifier? sensitivityClassifier = null,
        IFolderTenantAccessProjectionStore? tenantStore = null)
    {
        FixedUtcClock clock = new(Now);
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(tenantStore ?? new CountingTenantAccessProjectionStore(TenantProjection(principals: ["user-a"])), clock, new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed("permission_watermark_v1")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            clock);

        return new WorkspaceFileContextQueryHandler(
            authorization,
            source,
            sensitivityClassifier ?? new RecordingSensitivityClassifier(WorkspacePathSensitivityResult.Allowed()),
            clock);
    }

    private static WorkspaceFileContextQuery Query(
        WorkspaceFileContextQueryKind kind = WorkspaceFileContextQueryKind.Metadata,
        IReadOnlyList<PathMetadata>? paths = null,
        string? queryText = null,
        string? globPattern = null,
        int? limit = -1,
        long? startOffset = 0,
        long? endOffset = 1)
        => new(
            kind,
            "folder-a",
            "workspace-a",
            "tenant-a",
            "user-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [kind == WorkspaceFileContextQueryKind.Range ? "read_file_content" : "read_metadata"]),
            "correlation-a",
            "task-a",
            null,
            null,
            paths,
            queryText ?? (kind == WorkspaceFileContextQueryKind.Search ? "needle" : null),
            globPattern ?? (kind == WorkspaceFileContextQueryKind.Glob ? "*.md" : null),
            limit == -1
                ? kind is WorkspaceFileContextQueryKind.Search or WorkspaceFileContextQueryKind.Glob ? 10 : null
                : limit,
            null,
            kind == WorkspaceFileContextQueryKind.Range ? startOffset : null,
            kind == WorkspaceFileContextQueryKind.Range ? endOffset : null);

    private static PathMetadata Path(string normalizedPath, string pathPolicyClass = "tenant_sensitive_document")
        => new(normalizedPath, normalizedPath.Split('/').Last(), pathPolicyClass, "NFC");

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

    private sealed class RecordingContextSource : IWorkspaceFileContextSource
    {
        public List<WorkspaceFileContextSourceRequest> Requests { get; } = [];

        public WorkspaceFileContextSourceStatus Status { get; init; } = WorkspaceFileContextSourceStatus.Available;

        public int ItemCount { get; init; } = 1;

        public long RangeActualBytes { get; init; } = 1;

        public Task<WorkspaceFileContextSourceResult> QueryAsync(
            WorkspaceFileContextSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            PathMetadata path = request.Paths.Count == 0 ? Path("docs/readme.md") : request.Paths[0].Path;
            WorkspaceFileContextLimits limits = new(
                QueryFamily(request.Kind),
                request.Limit,
                ItemCount,
                request.Kind == WorkspaceFileContextQueryKind.Range ? 1 : 128,
                1,
                false,
                "not_truncated");
            FolderLifecycleFreshness freshness = new("snapshot_per_task", Now, "source_watermark_v1", Stale: false, null);
            List<WorkspaceFileContextItem> items = Enumerable
                .Range(0, ItemCount)
                .Select(_ => new WorkspaceFileContextItem(path, "file", 1, "tenant_sensitive", "not_redacted"))
                .ToList();

            if (request.Kind == WorkspaceFileContextQueryKind.Range)
            {
                return Task.FromResult(new WorkspaceFileContextSourceResult(
                    Status,
                    items,
                    path,
                    new WorkspaceFileContextRange(request.StartOffset!.Value, request.EndOffset!.Value, RangeActualBytes, Partial: false),
                    Status == WorkspaceFileContextSourceStatus.Available ? "YQ==" : "unsafe",
                    null,
                    limits,
                    freshness));
            }

            return Task.FromResult(new WorkspaceFileContextSourceResult(
                Status,
                items,
                null,
                null,
                null,
                new WorkspaceFileContextPage(null, request.Limit, false, null),
                limits,
                freshness));
        }

        private static string QueryFamily(WorkspaceFileContextQueryKind kind)
            => kind.ToString().ToLowerInvariant();
    }

    private sealed class RecordingSensitivityClassifier(WorkspacePathSensitivityResult result) : IWorkspaceFileSensitivityClassifier
    {
        public List<PathMetadata> Requests { get; } = [];

        public Task<WorkspacePathSensitivityResult> ClassifyAsync(
            PathMetadata path,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(path);
            return Task.FromResult(result);
        }
    }
}
