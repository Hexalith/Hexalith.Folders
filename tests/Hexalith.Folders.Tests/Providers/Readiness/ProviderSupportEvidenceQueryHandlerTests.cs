using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.ProviderReadiness;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Readiness;

public sealed class ProviderSupportEvidenceQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ShouldReturnTenantScopedProviderRowsWithDeterministicOrdering()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        await store.StoreAsync(Record("tenant-a", "profile_bbbbbbbbbbbbbbbb", "forgejo", EvidenceJson("unsupported", "supported")), TestContext.Current.CancellationToken);
        await store.StoreAsync(Record("tenant-b", "profile_hiddenbbbbbbbb", "github", EvidenceJson("supported", "supported")), TestContext.Current.CancellationToken);
        await store.StoreAsync(Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "temporarily_unavailable")), TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.Allowed);
        result.Items.Count.ShouldBe(14);
        result.Items.Select(static item => item.CapabilityProfileRef).Distinct(StringComparer.Ordinal).ToArray()
            .ShouldBe(["profile_aaaaaaaaaaaaaaaa", "profile_bbbbbbbbbbbbbbbb"]);
        result.Items[0].CapabilityProfileRef.ShouldBe("profile_aaaaaaaaaaaaaaaa");
        result.Items[0].Capability.ShouldBe("repository_creation");
        result.Items[0].SupportState.ShouldBe("supported");
        result.Items[7].CapabilityProfileRef.ShouldBe("profile_bbbbbbbbbbbbbbbb");
        result.Items[7].Capability.ShouldBe("repository_creation");
        result.Items[7].SupportState.ShouldBe("unsupported");
        result.Items.Any(static item => string.Equals(item.CapabilityProfileRef, "profile_hiddenbbbbbbbb", StringComparison.Ordinal)).ShouldBeFalse();
        result.Page.Limit.ShouldBe(50);
        result.Page.IsTruncated.ShouldBeFalse();
        result.Freshness.ReadConsistency.ShouldBe("eventually_consistent");
        result.Freshness.ProjectionWatermark.ShouldBe("tenant-a:42");
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSafeEmptyListWhenAuthorizedTenantHasNoEvidence()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.Allowed);
        result.Items.ShouldBeEmpty();
        result.Page.Limit.ShouldBe(50);
        result.Page.IsTruncated.ShouldBeFalse();
        result.Freshness.ObservedAt.ShouldBe(Now);
        result.Freshness.ProjectionWatermark.ShouldBe("tenant-a:7");
    }

    [Fact]
    public async Task HandleAsync_ShouldApplyBoundedCursorPagination()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        await store.StoreAsync(Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "temporarily_unavailable")), TestContext.Current.CancellationToken);
        await store.StoreAsync(Record("tenant-a", "profile_bbbbbbbbbbbbbbbb", "forgejo", EvidenceJson("unsupported", "supported")), TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult firstPage = await handler.HandleAsync(Query(limit: 5), TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryResult secondPage = await handler.HandleAsync(Query(cursor: firstPage.Page.Cursor, limit: 5), TestContext.Current.CancellationToken);

        firstPage.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.Allowed);
        firstPage.Items.Count.ShouldBe(5);
        firstPage.Page.Cursor.ShouldBe("cursor_5");
        firstPage.Page.IsTruncated.ShouldBeTrue();
        firstPage.Page.TruncatedReason.ShouldBe("result_count_limit");
        secondPage.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.Allowed);
        secondPage.Items.Count.ShouldBe(5);
        secondPage.Items[0].Capability.ShouldBe("provider_errors");
        secondPage.Page.Cursor.ShouldBe("cursor_10");
    }

    [Fact]
    public async Task HandleAsync_ShouldUseLatestEvidencePerCapabilityProfile()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        await store.StoreAsync(
            Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("unsupported", "supported")) with
            {
                ObservedAt = Now.AddMinutes(-10),
                FreshnessWatermark = "tenant-a:41",
            },
            TestContext.Current.CancellationToken);
        await store.StoreAsync(
            Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "supported")) with
            {
                ObservedAt = Now.AddMinutes(-1),
                FreshnessWatermark = "tenant-a:42",
            },
            TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.Allowed);
        result.Items.Count.ShouldBe(7);
        result.Items[0].Capability.ShouldBe("repository_creation");
        result.Items[0].SupportState.ShouldBe("supported");
    }

    [Fact]
    public async Task HandleAsync_ShouldDenyMissingProviderSupportPermissionBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        ProviderSupportEvidenceQueryHandler handler = Handler(readModel);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(
            Query(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", ["read_metadata"])),
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.AuthorizationDenied);
        result.Items.ShouldBeEmpty();
        readModel.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_ShouldDenyMissingAuthenticationSystemTenantAndClientMismatchBeforeReadModelObservation()
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        ProviderSupportEvidenceQueryHandler handler = Handler(readModel);

        ProviderSupportEvidenceQueryResult missingAuthentication = await handler.HandleAsync(
            Query(authoritativeTenantId: null, principalId: null),
            TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryResult systemTenant = await handler.HandleAsync(
            Query(authoritativeTenantId: "system", claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("system", "user-a", [ProviderSupportEvidenceQueryHandler.ReadActionToken])),
            TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryResult tenantMismatch = await handler.HandleAsync(
            Query(clientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal) { ["query_tenant_id"] = "tenant-b" }),
            TestContext.Current.CancellationToken);

        missingAuthentication.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.AuthenticationRequired);
        systemTenant.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.AuthorizationDenied);
        tenantMismatch.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.AuthorizationDenied);
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(TenantAccessOutcome.StaleProjection, ProviderSupportEvidenceQueryResultCode.ProjectionStale)]
    [InlineData(TenantAccessOutcome.UnavailableProjection, ProviderSupportEvidenceQueryResultCode.ProjectionUnavailable)]
    [InlineData(TenantAccessOutcome.MalformedEvidence, ProviderSupportEvidenceQueryResultCode.AuthorizationDenied)]
    [InlineData(TenantAccessOutcome.ReplayConflict, ProviderSupportEvidenceQueryResultCode.AuthorizationDenied)]
    public async Task HandleAsync_ShouldClassifyTenantEvidenceFailuresBeforeReadModelObservation(
        TenantAccessOutcome outcome,
        ProviderSupportEvidenceQueryResultCode expected)
    {
        CountingProviderSupportEvidenceReadModel readModel = new();
        ProviderSupportEvidenceQueryHandler handler = Handler(readModel, TenantStore(outcome));

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expected);
        result.Items.ShouldBeEmpty();
        readModel.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(ProviderSupportEvidenceReadModelStatus.Stale, ProviderSupportEvidenceQueryResultCode.ProjectionStale)]
    [InlineData(ProviderSupportEvidenceReadModelStatus.Unavailable, ProviderSupportEvidenceQueryResultCode.ProviderUnavailable)]
    public async Task HandleAsync_ShouldClassifyReadModelStaleAndUnavailableSafely(
        ProviderSupportEvidenceReadModelStatus status,
        ProviderSupportEvidenceQueryResultCode expected)
    {
        ProviderSupportEvidenceQueryHandler handler = Handler(new StatusProviderSupportEvidenceReadModel(status));

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(expected);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
    }

    [Theory]
    [InlineData("future_observed_at")]
    [InlineData("unsafe_diagnostic_payload")]
    [InlineData("missing_capability_profile_ref")]
    [InlineData("incomplete_capability_evidence")]
    public async Task HandleAsync_ShouldReturnReadModelUnavailableForUnsafeOrIncompatibleEvidence(string caseName)
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        ProviderReadinessEvidenceRecord record = caseName switch
        {
            "future_observed_at" => Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "supported")) with { ObservedAt = Now.AddMinutes(1) },
            "unsafe_diagnostic_payload" => Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", """{"evidence":{"repositoryCreation":"supported"},"raw":"https://provider.example.test/owner/repository-secret"}"""),
            "incomplete_capability_evidence" => Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", """{"evidence":{"repositoryCreation":"supported"}}"""),
            _ => Record("tenant-a", null, "github", EvidenceJson("supported", "supported")),
        };
        await store.StoreAsync(record, TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.ReadModelUnavailable);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ShouldClassifyMixedFreshAndStaleEvidenceAsProjectionStale()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        await store.StoreAsync(
            Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "supported")) with
            {
                ObservedAt = Now.AddMinutes(-1),
            },
            TestContext.Current.CancellationToken);
        await store.StoreAsync(
            Record("tenant-a", "profile_bbbbbbbbbbbbbbbb", "forgejo", EvidenceJson("supported", "supported")) with
            {
                ObservedAt = Now.AddHours(-1),
            },
            TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.ProjectionStale);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ShouldClassifyOldEvidenceAsProjectionStale()
    {
        InMemoryProviderReadinessEvidenceStore store = new(new FixedUtcClock(Now));
        await store.StoreAsync(
            Record("tenant-a", "profile_aaaaaaaaaaaaaaaa", "github", EvidenceJson("supported", "supported")) with
            {
                ObservedAt = Now.AddHours(-1),
            },
            TestContext.Current.CancellationToken);
        ProviderSupportEvidenceQueryHandler handler = Handler(store);

        ProviderSupportEvidenceQueryResult result = await handler.HandleAsync(Query(), TestContext.Current.CancellationToken);

        result.Code.ShouldBe(ProviderSupportEvidenceQueryResultCode.ProjectionStale);
        result.Items.ShouldBeEmpty();
        result.Freshness.Stale.ShouldBeTrue();
    }

    private static ProviderSupportEvidenceQueryHandler Handler(
        IProviderSupportEvidenceReadModel readModel,
        IFolderTenantAccessProjectionStore? tenantStore = null)
    {
        FixedUtcClock clock = new(Now);
        TenantAccessAuthorizer authorizer = new(
            tenantStore ?? TenantStore(TenantAccessOutcome.Allowed),
            clock,
            new TenantAccessOptions
            {
                MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                DiagnosticStalenessBudget = TimeSpan.FromMinutes(30),
            });

        return new ProviderSupportEvidenceQueryHandler(authorizer, readModel, clock);
    }

    private static ProviderSupportEvidenceQuery Query(
        EventStoreClaimTransformEvidence? claimTransformEvidence = null,
        string? authoritativeTenantId = "tenant-a",
        string? principalId = "user-a",
        string? cursor = null,
        int limit = 50,
        IReadOnlyDictionary<string, string?>? clientControlledTenantValues = null)
        => new(
            authoritativeTenantId,
            principalId,
            claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", [ProviderSupportEvidenceQueryHandler.ReadActionToken]),
            "correlation-a",
            cursor,
            limit,
            clientControlledTenantValues ?? new Dictionary<string, string?>(StringComparer.Ordinal));

    private static ProviderReadinessEvidenceRecord Record(
        string tenantId,
        string? capabilityProfileRef,
        string providerKey,
        string diagnosticJson)
        => new(
            tenantId,
            "organization-a",
            $"binding_{providerKey}",
            providerKey,
            providerKey,
            capabilityProfileRef,
            "ready",
            "success",
            Retryable: false,
            "none",
            Now.AddMinutes(-1),
            "tenant-a:42",
            "correlation-evidence",
            diagnosticJson);

    private static string EvidenceJson(string repositoryCreation, string providerErrors)
        => $$"""
        {
          "evidence": {
            "repositoryCreation": "{{repositoryCreation}}",
            "existingRepositoryBinding": "supported",
            "branchRefPolicy": "supported",
            "fileOperations": "supported",
            "commitStatus": "supported",
            "providerErrors": "{{providerErrors}}",
            "failureBehavior": "documented"
          }
        }
        """;

    private static IFolderTenantAccessProjectionStore TenantStore(TenantAccessOutcome outcome)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        if (outcome == TenantAccessOutcome.UnavailableProjection)
        {
            return new ThrowingTenantStore();
        }

        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = outcome != TenantAccessOutcome.DisabledTenant,
            Principals = outcome == TenantAccessOutcome.Denied
                ? new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                : new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    ["user-a"] = new("user-a", "Member"),
                },
            MalformedEvidence = outcome == TenantAccessOutcome.MalformedEvidence,
            ReplayConflict = outcome == TenantAccessOutcome.ReplayConflict,
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = outcome == TenantAccessOutcome.StaleProjection ? Now.AddHours(-1) : Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        return store;
    }

    private sealed class CountingProviderSupportEvidenceReadModel : IProviderSupportEvidenceReadModel
    {
        public int Calls { get; private set; }

        public Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
            ProviderSupportEvidenceReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ProviderSupportEvidenceReadModelResult.Available([], request.EmptyFreshness(), null));
        }
    }

    private sealed class StatusProviderSupportEvidenceReadModel(ProviderSupportEvidenceReadModelStatus status) : IProviderSupportEvidenceReadModel
    {
        public Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
            ProviderSupportEvidenceReadModelRequest request,
            CancellationToken cancellationToken = default)
        {
            ProviderReadinessFreshness freshness = request.EmptyFreshness();
            ProviderSupportEvidenceReadModelResult result = status switch
            {
                ProviderSupportEvidenceReadModelStatus.Stale => ProviderSupportEvidenceReadModelResult.Stale(freshness),
                ProviderSupportEvidenceReadModelStatus.Unavailable => ProviderSupportEvidenceReadModelResult.Unavailable(freshness),
                ProviderSupportEvidenceReadModelStatus.Malformed => ProviderSupportEvidenceReadModelResult.Malformed(freshness),
                _ => ProviderSupportEvidenceReadModelResult.Available([], freshness, null),
            };

            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingTenantStore : IFolderTenantAccessProjectionStore
    {
        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("projection unavailable");

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
