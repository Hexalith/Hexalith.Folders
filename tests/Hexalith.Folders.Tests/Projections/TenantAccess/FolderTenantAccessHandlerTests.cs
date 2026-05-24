using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.TenantAccess;

public sealed class FolderTenantAccessHandlerTests
{
    private static readonly DateTimeOffset EventTimestamp = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static FolderTenantAccessHandler CreateHandler(IFolderTenantAccessProjectionStore store, DateTimeOffset now)
        => new(store, new FixedUtcClock(now), new TenantAccessOptions());

    [Fact]
    public async Task UserAddedToTenantShouldProjectMetadataOnlyAccessEvidence()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000001", 1),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.UserAddedToTenant, "tenant-a", "01J00000000000000000000002", 2, principalId: "user-a", role: "TenantOwner"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.TenantId.ShouldBe("tenant-a");
        projection.Enabled.ShouldBeTrue();
        projection.Watermark.ShouldBe(2);
        projection.LastEventTimestamp.ShouldBe(EventTimestamp);
        projection.Principals.Keys.ShouldContain("user-a");
        projection.Principals["user-a"].Role.ShouldBe("TenantOwner");
    }

    [Fact]
    public async Task DuplicateMessageWithDivergentMetadataShouldRecordReplayConflictWithoutAdvancingWatermark()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        string messageId = "01J00000000000000000000010";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", messageId, 1, payloadFingerprint: "created"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantDisabled, "tenant-a", messageId, 2, payloadFingerprint: "disabled"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.Watermark.ShouldBe(1);
        projection.ReplayConflict.ShouldBeTrue();
        projection.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task NonFoldersConfigurationShouldBeIgnoredAndRemovedFoldersConfigurationShouldBeTombstoned()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000020", 1),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationSet, "tenant-a", "01J00000000000000000000021", 2, configurationKey: "billing.plan"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationSet, "tenant-a", "01J00000000000000000000022", 3, configurationKey: "folders.create.enabled"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantConfigurationRemoved, "tenant-a", "01J00000000000000000000023", 4, configurationKey: "folders.create.enabled"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.ConfigurationKeys.ShouldNotContain("billing.plan");
        projection.ConfigurationKeys.ShouldNotContain("folders.create.enabled");
        projection.RemovedConfigurationKeys.ShouldContain("folders.create.enabled");
    }

    [Fact]
    public async Task FutureTimestampShouldMarkProjectionMalformedAndFailClosed()
    {
        DateTimeOffset now = EventTimestamp;
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, now);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000030", 1, timestamp: now.AddMinutes(5)),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.MalformedEvidence.ShouldBeTrue();
        projection.Watermark.ShouldBe(0);
    }

    [Fact]
    public async Task DuplicateMessageWithRotatedCorrelationIdShouldBeNoOp()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000040", 1, payloadFingerprint: "same", correlationId: "corr-a"),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000040", 1, payloadFingerprint: "same", correlationId: "corr-b"),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.Watermark.ShouldBe(1);
        projection.ReplayConflict.ShouldBeFalse();
        projection.ProcessedMessages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OutOfOrderDeliveryShouldNotAdvanceProjectionOrMarkMalformed()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000050", 2),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantDisabled, "tenant-a", "01J00000000000000000000049", 1),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);

        projection.ShouldNotBeNull();
        projection.Enabled.ShouldBeTrue();
        projection.Watermark.ShouldBe(2);
        projection.MalformedEvidence.ShouldBeFalse();
        projection.ReplayConflict.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingMessageIdOrMembershipPrincipalShouldMarkProjectionMalformed()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        FolderTenantAccessHandler handler = CreateHandler(store, EventTimestamp.AddMinutes(1));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", string.Empty, 1),
            cancellationToken);
        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.UserAddedToTenant, "tenant-b", "01J00000000000000000000060", 1, role: "TenantReader"),
            cancellationToken);

        (await store.GetAsync("tenant-a", cancellationToken)).ShouldNotBeNull().MalformedEvidence.ShouldBeTrue();
        (await store.GetAsync("tenant-b", cancellationToken)).ShouldNotBeNull().MalformedEvidence.ShouldBeTrue();
    }

    [Fact]
    public async Task UserRemovalRoleChangeAndTenantDisabledShouldFailClosedThroughAuthorizer()
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        TenantAccessOptions options = new();
        FolderTenantAccessHandler handler = new(store, new FixedUtcClock(EventTimestamp.AddMinutes(1)), options);
        TenantAccessAuthorizer authorizer = new(store, new FixedUtcClock(EventTimestamp.AddMinutes(1)), options);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000070", 1), cancellationToken);
        await handler.HandleAsync(Event(FolderTenantAccessEventKind.UserAddedToTenant, "tenant-a", "01J00000000000000000000071", 2, principalId: "user-a", role: "TenantReader"), cancellationToken);
        await handler.HandleAsync(Event(FolderTenantAccessEventKind.UserRoleChanged, "tenant-a", "01J00000000000000000000072", 3, principalId: "user-a", role: "TenantOwner"), cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);
        projection.ShouldNotBeNull();
        projection.Principals["user-a"].Role.ShouldBe("TenantOwner");
        (await authorizer.AuthorizeMutationAsync(Context("tenant-a", "user-a"), cancellationToken)).Outcome.ShouldBe(TenantAccessOutcome.Allowed);

        await handler.HandleAsync(Event(FolderTenantAccessEventKind.UserRemovedFromTenant, "tenant-a", "01J00000000000000000000073", 4, principalId: "user-a"), cancellationToken);
        (await authorizer.AuthorizeMutationAsync(Context("tenant-a", "user-a"), cancellationToken)).Outcome.ShouldBe(TenantAccessOutcome.Denied);

        await handler.HandleAsync(Event(FolderTenantAccessEventKind.TenantDisabled, "tenant-a", "01J00000000000000000000074", 5), cancellationToken);
        (await authorizer.AuthorizeMutationAsync(Context("tenant-a", "user-a"), cancellationToken)).Outcome.ShouldBe(TenantAccessOutcome.DisabledTenant);
    }

    [Fact]
    public async Task ConcurrencyConflictShouldRetryWithinConfiguredAttempts()
    {
        FlakySaveStore store = new(new TenantAccessConcurrencyException("tenant-a", 0, 1));
        FolderTenantAccessHandler handler = new(
            store,
            new FixedUtcClock(EventTimestamp.AddMinutes(1)),
            new TenantAccessOptions { ConcurrencyRetryAttempts = 2 });
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000080", 1),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);
        projection.ShouldNotBeNull();
        projection.Enabled.ShouldBeTrue();
        store.SaveAttempts.ShouldBe(2);
    }

    [Fact]
    public async Task TransientPersistenceFailureShouldRetryWithinConfiguredAttempts()
    {
        FlakySaveStore store = new(new TenantAccessTransientPersistenceException("tenant-a"));
        FolderTenantAccessHandler handler = new(
            store,
            new FixedUtcClock(EventTimestamp.AddMinutes(1)),
            new TenantAccessOptions { ConcurrencyRetryAttempts = 2 });
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(
            Event(FolderTenantAccessEventKind.TenantCreated, "tenant-a", "01J00000000000000000000090", 1),
            cancellationToken);

        FolderTenantAccessProjection? projection = await store.GetAsync("tenant-a", cancellationToken);
        projection.ShouldNotBeNull();
        projection.Enabled.ShouldBeTrue();
        store.SaveAttempts.ShouldBe(2);
    }

    private static FolderTenantAccessEvent Event(
        FolderTenantAccessEventKind kind,
        string tenantId,
        string messageId,
        long sequenceNumber,
        string? principalId = null,
        string? role = null,
        string? configurationKey = null,
        string? payloadFingerprint = null,
        DateTimeOffset? timestamp = null,
        string correlationId = "correlation-a")
        => new(
            kind,
            tenantId,
            messageId,
            sequenceNumber,
            timestamp ?? EventTimestamp,
            correlationId,
            principalId,
            role,
            ConfigurationKey: configurationKey,
            PayloadFingerprint: payloadFingerprint ?? tenantId);

    private static TenantAccessAuthorizationContext Context(string tenantId, string principalId)
        => new(tenantId, principalId, tenantId);

    private sealed class FlakySaveStore(Exception firstSaveException) : IFolderTenantAccessProjectionStore
    {
        private readonly InMemoryFolderTenantAccessProjectionStore _inner = new();
        private bool _failed;

        public int SaveAttempts { get; private set; }

        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => _inner.GetAsync(tenantId, cancellationToken);

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (!_failed)
            {
                _failed = true;
                throw firstSaveException;
            }

            return _inner.SaveAsync(projection, cancellationToken);
        }
    }
}
