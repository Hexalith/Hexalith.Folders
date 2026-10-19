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

internal static class OperationalStatusRebuildFixture
{
    public static async Task<ReadModelBatchResult> RebuildAsync(OperationalStatusTestStore store, string tenantId,
        string folderId, string operationId, IReadOnlyList<OperationalStatusEvent> events, long sourceHead, CancellationToken token)
    {
        ServiceCollection services = new();
        services.AddSingleton<IReadModelStore>(store);
        services.AddSingleton<IReadModelBatchStore>(store);
        services.AddSingleton<IReadModelBatchStagingStore>(store);
        services.AddSingleton<IReadModelPendingBatchRecoveryStore>(store);
        services.AddSingleton(new SharedProjectionEpochCoordinator(store, store));
        services.AddSingleton<IOrganizationStreamSequenceReader>(new OperationalStatusSequenceReader { Head = sourceHead });
        services.AddSingleton<OperationalStatusProjectionWriter>();
        services.AddSingleton<IAsyncDomainProjectionHandler, OperationalStatusProjectionHandler>();
        services.AddSingleton(Options.Create(new ProjectionDispatchOptions { MaxRebuildEventCount = 20_000 }));
        services.AddSingleton(Options.Create(new DomainProjectionIdentityOptions { AppId = "folders", ServiceVersion = "test" }));
        using var provider = services.BuildServiceProvider();
        var result = await OperationalStatusRebuild.ExecuteAsync(provider,
            new ProjectionRequest(tenantId, "folders", folderId, events.Select(static item => item.Source!).ToArray()),
            operationId, token).ConfigureAwait(false);
        if (!result.IsSuccess) throw new InvalidOperationException(result.RecoveryReason);
        return result;
    }
}
