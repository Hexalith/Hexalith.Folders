using System.Text;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Tests.OperationalStatus;

using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

public sealed class OperationalStatusClockRegistrationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ConfiguredClockPreservesCurrentViewsThroughExpiryRebuildAndFurtherDeliveryAsync(int registration, bool inherited)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset observed = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        OperationalStatusClock clock = new() { UtcNow = observed.AddHours(1) };
        OperationalStatusTestStore store = new();
        store.BatchOptions.TimeProvider = clock;
        ServiceCollection services = new();
        services.AddSingleton<IReadModelStore>(store);
        services.AddSingleton<IReadModelBatchStore>(store);
        services.AddSingleton<IReadModelBatchStagingStore>(store);
        services.AddSingleton<IReadModelPendingBatchRecoveryStore>(store);
        services.AddSingleton<IReadModelConditionalEraser>(store);
        services.AddSingleton<IUtcClock>(clock);
        services.AddEventStoreReadModelStore(options => options.TimeProvider = clock);
        if (registration == 1) services.AddFoldersServer();
        services.AddFoldersOperationalStatusPersistence();
        OperationalStatusSequenceReader sequence = new() { Head = 8 };
        services.AddSingleton<IOrganizationStreamSequenceReader>(sequence);
        using ServiceProvider provider = services.BuildServiceProvider();
        OperationalStatusProjectionWriter writer = registration == 2
            ? new(store, store, store, clock)
            : provider.GetRequiredService<OperationalStatusProjectionWriter>();
        SharedProjectionEpochCoordinator coordinator = provider.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = OperationalStatusGeneration.Scope("tenant-a");
        OperationalStatusEvent[] source = OperationalStatusTestData.Sources(OperationalStatusTestData.CleanReleasedPrefix(observed));
        (await writer.ApplyAsync("tenant-a", "folder-a", "initial", source, token)).IsSuccess.ShouldBeTrue();
        string lifecycleKey = OperationalStatusKeys.Lifecycle("tenant-a", "folder-a");
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "clock-selected", source, source.Length, token, clock: clock))
            .IsSuccess.ShouldBeTrue();
        if (inherited)
            await PromoteAsync("inherited", ReadModelBatchOperation.Write("clock-test-unrelated", new JsonObject { ["value"] = 1 }, ReadModelBatchConcurrency.LastWrite));

        clock.UtcNow = observed + OperationalStatusKeys.Retention - TimeSpan.FromTicks(1);
        (await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope, lifecycleKey, token)).Value.ShouldNotBeNull();
        (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-before-deadline", [source[^1]], token)).IsSuccess.ShouldBeTrue();

        clock.UtcNow = observed + OperationalStatusKeys.Retention;
        SharedProjectionReadResult<FolderLifecycleStatusReadModelSnapshot> expired = await coordinator.ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope, lifecycleKey, token);
        expired.IsAvailable.ShouldBeTrue();
        expired.Value.ShouldBeNull();
        IFolderLifecycleStatusReadModel reader = provider.GetRequiredService<IFolderLifecycleStatusReadModel>();
        (await reader.GetAsync(new FolderLifecycleStatusReadModelRequest("tenant-a", "folder-a", "reader", "read_metadata", null, "corr", "authority", "eventually_consistent"), token))
            .Status.ShouldBe(FolderLifecycleStatusReadModelStatus.NotFound);
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "clock-expired-rebuild", source, source.Length, token, clock: clock))
            .IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "expired-duplicate-after-rebuild", [source[^1]], token)).IsSuccess.ShouldBeTrue();
        OperationalStatusEvent ninth = Rejection(9);
        sequence.Head = 9;
        (await writer.ApplyAsync("tenant-a", "folder-a", "after-deadline", [ninth], token)).IsSuccess.ShouldBeTrue();
        (await NamedOperationalStatusRebuildFixture.RebuildAsync(store, "tenant-a", "folder-a", "clock-rebuild", [.. source, ninth], 9, token, clock: clock))
            .IsSuccess.ShouldBeTrue();
        (await writer.ApplyAsync("tenant-a", "folder-a", "duplicate-after-rebuild", [ninth], token)).IsSuccess.ShouldBeTrue();
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        sequence.Head = 10;
        (await writer.ApplyAsync("tenant-a", "folder-a", "further-growth", [Rejection(10)], token)).IsSuccess.ShouldBeTrue();
        (await coordinator.ReadAsync<OperationalStatusCheckpoint>(scope, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"), token)).Value!.Sequence.ShouldBe(10);

        async Task PromoteAsync(string operationId, ReadModelBatchOperation operation)
        {
            _ = await coordinator.BeginAsync(scope, operationId, operationId + "-inventory", new Dictionary<string, long>(), token).ConfigureAwait(false);
            _ = await coordinator.StageStreamingAsync(scope, operationId, OneAsync(operation), token).ConfigureAwait(false);
            await coordinator.CommitAsync(scope, operationId, cancellationToken: token).ConfigureAwait(false);
        }

        OperationalStatusEvent Rejection(long position) => OperationalStatusEventReader.ReadStored(source[0].Source! with
        {
            SequenceNumber = position,
            GlobalPosition = position,
            MessageId = "clock-rejection-" + position,
            Timestamp = clock.UtcNow,
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = Encoding.UTF8.GetBytes("{\"Code\":\"UnsupportedCommandType\",\"CommandType\":\"UnknownButLegitimate\"}"),
        });
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> OneAsync(ReadModelBatchOperation operation)
    {
        yield return operation;
        await Task.Yield();
    }
}
