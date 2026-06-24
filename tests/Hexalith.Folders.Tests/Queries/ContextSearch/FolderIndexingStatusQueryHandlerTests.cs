using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Tests.Queries.Folders;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.ContextSearch;

public sealed class FolderIndexingStatusQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissingAuthenticationShouldReturnSafe()
    {
        FolderIndexingStatusQueryHandler handler = Handler(new StubBridge([]));

        FolderIndexingStatusQueryResult result = await handler.HandleAsync(Query(tenantId: null), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderIndexingStatusResultCode.AuthenticationRequired);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingFolderShouldReturnNotFoundSafe()
    {
        FolderIndexingStatusQueryHandler handler = Handler(new StubBridge([]));

        FolderIndexingStatusQueryResult result = await handler.HandleAsync(Query(folderId: null), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderIndexingStatusResultCode.NotFoundSafe);
    }

    [Fact]
    public async Task TenantDenialShouldReturnAuthorizationDenied()
    {
        FolderIndexingStatusQueryHandler handler = Handler(
            new StubBridge([]),
            tenantStore: new CountingTenantAccessProjectionStore(TenantProjection(principals: ["other"])));

        FolderIndexingStatusQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderIndexingStatusResultCode.AuthorizationDenied);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task AuthorizedReadShouldMapEveryFolderEntryIncludingNonLiveStatuses()
    {
        // Unlike search, the status projection surfaces ALL statuses (the operator must see failed/tombstoned too).
        StubBridge bridge = new(
        [
            Entry("fv-1", SemanticIndexingBridgeStatus.Indexed),
            Entry("fv-2", SemanticIndexingBridgeStatus.Failed),
            Entry("fv-3", SemanticIndexingBridgeStatus.Tombstoned, pathPolicyClass: "tenant_secret_document"),
        ]);
        FolderIndexingStatusQueryHandler handler = Handler(bridge);

        FolderIndexingStatusQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderIndexingStatusResultCode.Allowed);
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(i => i.FileVersionReference == "fv-2" && i.IndexingStatus == "failed");
        result.Items.ShouldContain(i => i.FileVersionReference == "fv-3" && i.IndexingStatus == "tombstoned" && i.Redaction == "redacted");
        bridge.Requests.ShouldHaveSingleItem().ShouldBe(("tenant-a", "folder-a"));
        result.Freshness.ReadConsistency.ShouldBe("eventually_consistent");
    }

    private static FolderIndexingStatusQueryHandler Handler(
        ISemanticIndexingBridgeReadModel bridge,
        IFolderTenantAccessProjectionStore? tenantStore = null)
    {
        FixedUtcClock clock = new(Now);
        LayeredFolderAuthorizationService authorization = new(
            new TenantAccessAuthorizer(
                tenantStore ?? new CountingTenantAccessProjectionStore(TenantProjection(principals: ["user-a"])),
                clock,
                new TenantAccessOptions()),
            new RecordingFolderPermissionEvidenceProvider(
                FolderPermissionEvidenceResult.Allowed("permission_watermark_v1", organizationId: "org-a")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1")),
            clock);

        return new FolderIndexingStatusQueryHandler(authorization, bridge, clock);
    }

    private static FolderIndexingStatusQuery Query(
        string? folderId = "folder-a",
        string? tenantId = "tenant-a",
        string? principalId = "user-a")
        => new(
            folderId,
            tenantId,
            principalId,
            EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, [ContextSearchQueryHandler.ActionToken]),
            "correlation-a",
            "task-a",
            ClientControlledTenantValues: null,
            ClientControlledPrincipalValues: null);

    private static SemanticIndexingBridgeEntry Entry(
        string fileVersionId,
        SemanticIndexingBridgeStatus status,
        string? pathPolicyClass = "tenant_sensitive_document")
        => new(
            new SemanticIndexingFileVersionIdentity(
                "tenant-a",
                "org-a",
                "folder-a",
                "workspace-a",
                "op-a",
                "digest-" + fileVersionId,
                fileVersionId,
                "hash-" + fileVersionId,
                $"folders://tenant-a/organizations/org-a/folders/folder-a/workspaces/workspace-a/file-versions/{fileVersionId}"),
            status,
            "memories_accepted",
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

    private sealed class StubBridge(IReadOnlyList<SemanticIndexingBridgeEntry> entries) : ISemanticIndexingBridgeReadModel
    {
        public List<(string Tenant, string Folder)> Requests { get; } = [];

        public Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
            SemanticIndexingFileVersionIdentity identity,
            CancellationToken cancellationToken = default)
            => Task.FromResult<SemanticIndexingBridgeEntry?>(null);

        public Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
            string managedTenantId,
            string folderId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((managedTenantId, folderId));
            return Task.FromResult(entries);
        }
    }
}
