using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

internal static class NamedOperationalStatusRebuildFixture
{
    public static async Task<ReadModelBatchResult> RebuildAsync(OperationalStatusTestStore store, string tenantId,
        string folderId, string operationId, IReadOnlyList<OperationalStatusEvent> events, long sourceHead, CancellationToken token, Action? afterBegin = null, bool retryPartial = true, IUtcClock? clock = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IReadModelStore>(store);
        services.AddSingleton<IReadModelBatchStore>(store);
        services.AddSingleton<IReadModelBatchStagingStore>(store);
        services.AddSingleton<IReadModelPendingBatchRecoveryStore>(store);
        services.AddSingleton(new SharedProjectionEpochCoordinator(store, store, store.BatchOptions.TimeProvider));
        if (clock is not null) services.AddSingleton<IUtcClock>(clock);
        services.AddSingleton<IOrganizationStreamSequenceReader>(new OperationalStatusSequenceReader { Head = sourceHead });
        services.AddSingleton<OperationalStatusProjectionWriter>();
        services.AddSingleton<IAsyncDomainProjectionHandler, OperationalStatusProjectionHandler>();
        services.AddSingleton(Options.Create(new ProjectionDispatchOptions { MaxRebuildEventCount = 20_000 }));
        services.AddSingleton(Options.Create(new DomainProjectionIdentityOptions { AppId = "folders", ServiceVersion = "test" }));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value;
        var binding = provider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        string catalog = ProjectionRouteCatalogFingerprint.Compute(binding.AppId, binding.ServiceVersion,
            [new("folders", OperationalStatusKeys.ProjectionType)], [new("folders", OperationalStatusKeys.ProjectionType)]);
        DomainSharedProjectionRebuildIdentity identity = new(tenantId, "folders", OperationalStatusKeys.ProjectionType,
            operationId, catalog);
        var source = events.Select(static item => item.Source!).ToArray();
        string capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            0, DomainSharedProjectionRebuildFingerprint.ComputeHistory(folderId, false, source));
        var coordinator = provider.GetRequiredService<SharedProjectionEpochCoordinator>();
        await coordinator.RegisterWriterAsync(OperationalStatusGeneration.Scope(tenantId),
            OperationalStatusGeneration.WriterId, token).ConfigureAwait(false);
        try
        {
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                identity, CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: new Dictionary<string, long> { [folderId] = sourceHead })).ConfigureAwait(false);
            afterBegin?.Invoke();
            var accumulated = await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0,
                AggregateId: folderId, Events: source)).ConfigureAwait(false);
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
                identity, ExpectedAggregateCount: 1, ExpectedInventoryFingerprint: accumulated.InventoryFingerprint)).ConfigureAwait(false);
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Stage, identity)).ConfigureAwait(false);
            var committed = await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Commit, identity)).ConfigureAwait(false);
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Verify, identity)).ConfigureAwait(false);
            return ReadModelBatchResult.Completed(committed.BatchFingerprint ?? operationId);
        }
        catch (Exception failure)
        {
            var aborted = await DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider,
                new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity),
                options, binding, token).ConfigureAwait(false);
            throw new InvalidOperationException($"{failure.Message}; Abort={aborted.Phase}:{aborted.Status}:{aborted.ReasonCode}", failure);
        }

        async Task<DomainSharedProjectionRebuildResponse> RequireAsync(DomainSharedProjectionRebuildRequest request)
        {
            for (int attempt = 0; ; attempt++)
            {
                var response = await DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request, options, binding, token).ConfigureAwait(false);
                if (response.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)
                    return response;
                if (retryPartial && attempt + 1 < options.MaxRetryAttempts
                    && (response.Status == ProjectionDispatchStatus.Retryable
                        || response.Status == ProjectionDispatchStatus.Indeterminate && response.ReasonCode == ProjectionDispatchReasonCodes.HandlerFailure))
                    continue; // Resume the unchanged SDK action, including its retained private batch.
                throw new InvalidOperationException($"{request.Action}:{response.Phase}:{response.Status}:{response.ReasonCode}");
            }
        }
    }
}
