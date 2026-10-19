using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;

using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server;

/// <summary>Consumes the SDK bounded shared-rebuild transport for the existing protected folder route.</summary>
public static class OperationalStatusRebuild
{
    /// <summary>Rebuilds one authoritative complete owner prefix with SDK-owned source identity handoff.</summary>
    public static async Task<ReadModelBatchResult> ExecuteAsync(IServiceProvider services, ProjectionRequest source,
        string operationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Events is not { Length: > 0 }) throw new InvalidOperationException("Complete operational source is required.");
        var options = services.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value;
        var binding = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        var handlers = services.GetServices<IAsyncDomainProjectionHandler>().Where(handler => handler.Domain == source.Domain).ToArray();
        string catalog = ProjectionRouteCatalogFingerprint.Compute(binding.AppId, binding.ServiceVersion,
            handlers.Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)),
            handlers.Where(static handler => handler.OrdinaryDeliveryEnabled).Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)));
        DomainSharedProjectionRebuildIdentity identity = new(source.TenantId, source.Domain,
            OperationalStatusKeys.ProjectionType, operationId, catalog);
        await services.GetRequiredService<SharedProjectionEpochCoordinator>().RegisterWriterAsync(
            OperationalStatusGeneration.Scope(source.TenantId), OperationalStatusGeneration.WriterId, cancellationToken).ConfigureAwait(false);
        long position = source.Events[^1].GlobalPosition;
        if (position <= 0) throw new InvalidOperationException("Original operational source position is missing.");
        string capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            0, DomainSharedProjectionRebuildFingerprint.ComputeHistory(source.AggregateId, false, source.Events));
        DomainSharedProjectionRebuildResponse begin = await Dispatch(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Begin, identity, CaptureInventoryFingerprint: capture,
            SourceHighWatermarks: new Dictionary<string, long> { [source.AggregateId] = position })).ConfigureAwait(false);
        if (!Success(begin)) return Incomplete(begin);
        DomainSharedProjectionRebuildResponse accumulated = await Dispatch(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0,
            AggregateId: source.AggregateId, Events: source.Events)).ConfigureAwait(false);
        if (!Success(accumulated)) return Incomplete(accumulated);
        DomainSharedProjectionRebuildResponse finalized = await Dispatch(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize, identity, ExpectedAggregateCount: 1,
            ExpectedInventoryFingerprint: accumulated.InventoryFingerprint)).ConfigureAwait(false);
        if (!Success(finalized)) return Incomplete(finalized);
        DomainSharedProjectionRebuildResponse staged = await Dispatch(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Stage, identity)).ConfigureAwait(false);
        if (!Success(staged)) return Incomplete(staged);
        DomainSharedProjectionRebuildResponse committed = await Dispatch(new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Commit, identity)).ConfigureAwait(false);
        if (!Success(committed)) return Incomplete(committed);
        if (await services.GetRequiredService<IOrganizationStreamSequenceReader>().GetCurrentSequenceAsync(
            source.TenantId, source.AggregateId, cancellationToken).ConfigureAwait(false) != source.Events.LongLength)
            return ReadModelBatchResult.Indeterminate(string.Empty, "operational_source_advanced");
        return ReadModelBatchResult.Completed(committed.BatchFingerprint ?? operationId);

        Task<DomainSharedProjectionRebuildResponse> Dispatch(DomainSharedProjectionRebuildRequest request)
            => DomainSharedProjectionRebuildDispatcher.DispatchAsync(services, request, options, binding, cancellationToken);
    }

    private static ReadModelBatchResult Incomplete(DomainSharedProjectionRebuildResponse response)
        => ReadModelBatchResult.Indeterminate(response.BatchFingerprint ?? string.Empty,
            $"{response.Phase}:{response.Status}:{response.ReasonCode}");

    private static bool Success(DomainSharedProjectionRebuildResponse response)
        => response.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted;

}
