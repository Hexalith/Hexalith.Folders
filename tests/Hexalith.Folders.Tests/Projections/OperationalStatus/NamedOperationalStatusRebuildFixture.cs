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

/// <summary>Exercises the supported SDK named/shared protocol with the production Folders handler.</summary>
internal static class NamedOperationalStatusRebuildFixture
{
    /// <summary>Rebuilds complete original owner histories through bounded transport parts and the SDK generation lifecycle.</summary>
    public static async Task<ReadModelBatchResult> RebuildAsync(OperationalStatusTestStore store, string tenantId,
        string folderId, string operationId, IReadOnlyList<OperationalStatusEvent> events, long sourceHead, CancellationToken token, Action? afterBegin = null, bool retryPartial = true, IUtcClock? clock = null,
        IReadOnlyList<(string FolderId, IReadOnlyList<OperationalStatusEvent> Events)>? additionalOwners = null)
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
        services.AddSingleton(Options.Create(new ProjectionDispatchOptions { MaxRebuildEventCount = 40_000 }));
        services.AddSingleton(Options.Create(new DomainProjectionIdentityOptions { AppId = "folders", ServiceVersion = "test" }));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value;
        var binding = provider.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        string catalog = ProjectionRouteCatalogFingerprint.Compute(binding.AppId, binding.ServiceVersion,
            [new("folders", OperationalStatusKeys.ProjectionType)], [new("folders", OperationalStatusKeys.ProjectionType)]);
        DomainSharedProjectionRebuildIdentity identity = new(tenantId, "folders", OperationalStatusKeys.ProjectionType,
            operationId, catalog);
        List<(string FolderId, ProjectionEventDto[] Events)> owners = [(folderId, events.Select(static item => item.Source!).ToArray())];
        if (additionalOwners is not null)
            owners.AddRange(additionalOwners.Select(static owner => (owner.FolderId, owner.Events.Select(static item => item.Source!).ToArray())));
        string capture = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < owners.Count; ordinal++)
            capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(capture, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(owners[ordinal].FolderId, false, owners[ordinal].Events));
        Dictionary<string, long> watermarks = owners.ToDictionary(static owner => owner.FolderId, static owner => owner.Events[^1].GlobalPosition);
        watermarks[folderId] = sourceHead;
        var coordinator = provider.GetRequiredService<SharedProjectionEpochCoordinator>();
        await coordinator.RegisterWriterAsync(OperationalStatusGeneration.Scope(tenantId),
            OperationalStatusGeneration.WriterId, token).ConfigureAwait(false);
        try
        {
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                identity, CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: watermarks)).ConfigureAwait(false);
            afterBegin?.Invoke();
            DomainSharedProjectionRebuildResponse? accumulated = null;
            for (int ordinal = 0; ordinal < owners.Count; ordinal++)
            {
                var owner = owners[ordinal];
                int part = 0;
                foreach (ProjectionEventDto[] page in owner.Events.Chunk(256))
                    await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version,
                        DomainSharedProjectionRebuildAction.AccumulatePart, identity, AggregateOrdinal: ordinal,
                        AggregateId: owner.FolderId, Events: page, PartOrdinal: part++)).ConfigureAwait(false);
                accumulated = await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version,
                    DomainSharedProjectionRebuildAction.AccumulateComplete, identity, AggregateOrdinal: ordinal,
                    AggregateId: owner.FolderId, PartCount: part,
                    ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory(owner.FolderId, false, owner.Events))).ConfigureAwait(false);
            }
            await RequireAsync(new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize,
                identity, ExpectedAggregateCount: owners.Count, ExpectedInventoryFingerprint: accumulated!.InventoryFingerprint)).ConfigureAwait(false);
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
