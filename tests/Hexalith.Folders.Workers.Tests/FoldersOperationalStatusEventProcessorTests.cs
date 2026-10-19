using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Tests.OperationalStatus;
using Hexalith.Folders.Workers;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class FoldersOperationalStatusEventProcessorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedWorkspaceOrLeaseNeverAdvancesWorkerCompletion(bool malformedLease)
    {
        var history = OperationalStatusTestData.LockedPrefix(OperationalStatusTestData.Now);
        OperationalStatusTestStore store = new();
        var processor = new FoldersOperationalStatusEventProcessor(new(store, store, store));
        foreach (var value in history.Take(history.Count - 1).Select((value, index) => OperationalStatusTestData.Envelope(value, index + 1)))
            await processor.ProcessAsync(value, TestContext.Current.CancellationToken);
        var acquired = history.OfType<Hexalith.Folders.Aggregates.Folder.WorkspaceLockAcquired>().Single();
        acquired = malformedLease ? acquired with { ExpiresAt = acquired.AcquiredAt.AddSeconds(-1) } : acquired with { WorkspaceId = null! };
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(OperationalStatusTestData.Envelope(acquired, history.Count),
            TestContext.Current.CancellationToken));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(history.Count - 1);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("999")]
    public async Task InvalidProviderRejectionNeverCreatesSourceReceipts(string code)
    {
        InMemoryReadModelStore store = new();
        var processor = new FoldersProviderBindingEventProcessor(new(store, new OperationalStatusSequenceReader()));
        var source = OperationalStatusTestData.Envelope(OperationalStatusTestData.Created()) with
        {
            AggregateId = Hexalith.Folders.Aggregates.Organization.ProviderBindingActorId.Create("binding-a"),
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = System.Text.Encoding.UTF8.GetBytes("{\"Code\":\"" + code + "\",\"CommandType\":\"Hexalith.Folders.Commands.ConfigureProviderBinding\"}"),
        };
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(source, TestContext.Current.CancellationToken));
        store.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DurableCheckpointPrecedesSuccessfulProcessingAndDuplicateAcknowledgment()
    {
        OperationalStatusTestStore store = new();
        var processor = new FoldersOperationalStatusEventProcessor(new(store, store, store));
        var source = OperationalStatusTestData.Envelope(OperationalStatusTestData.Created());
        await processor.ProcessAsync(source, TestContext.Current.CancellationToken);
        await processor.ProcessAsync(source, TestContext.Current.CancellationToken);
        var checkpoint = store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"));
        checkpoint!.Sequence.ShouldBe(1);
        checkpoint.EventDigests.ShouldHaveSingleItem();
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(
            source with { SequenceNumber = 3, MessageId = "gap" }, TestContext.Current.CancellationToken));
        store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
    }

    [Fact]
    public async Task IndeterminatePersistenceIsRetryableAndOriginalManifestRecovers()
    {
        OperationalStatusTestStore store = new();
        store.BatchFaultHook = (phase, _, _) => phase is ReadModelBatchPhase.AfterCommit or ReadModelBatchPhase.Reconcile
            ? throw new IOException("Injected crash") : Task.CompletedTask;
        var source = OperationalStatusTestData.Envelope(OperationalStatusTestData.Created());
        await Should.ThrowAsync<InvalidOperationException>(() => new FoldersOperationalStatusEventProcessor(new(store, store, store))
            .ProcessAsync(source, TestContext.Current.CancellationToken));
        store.BatchFaultHook = null;
        await new FoldersOperationalStatusEventProcessor(new(store, store, store)).ProcessAsync(source, TestContext.Current.CancellationToken);
        store.HasPendingEnvelope(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a")).ShouldBeFalse();
        store.Snapshot<OperationalStatusCheckpoint>(OperationalStatusKeys.StoreName, OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkerRejectsReusedMessagePositionsAndUndefinedRejectionCodes(bool reusedMessage)
    {
        OperationalStatusTestStore store = new();
        var processor = new FoldersOperationalStatusEventProcessor(new(store, store, store));
        var source = OperationalStatusTestData.Envelope(OperationalStatusTestData.Created());
        await processor.ProcessAsync(source, TestContext.Current.CancellationToken);
        var rejected = source with
        {
            SequenceNumber = 2,
            MessageId = reusedMessage ? source.MessageId : "new-rejection",
            EventTypeName = "Hexalith.Folders.Server.FolderCommandRejected",
            Payload = System.Text.Encoding.UTF8.GetBytes(reusedMessage
                ? "{\"Code\":\"StateTransitionInvalid\",\"CommandType\":\"LockWorkspace\"}"
                : "{\"Code\":\"999\",\"CommandType\":\"LockWorkspace\"}"),
        };
        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(rejected, TestContext.Current.CancellationToken));
        store.Snapshot<OperationalStatusCheckpoint>("statestore", OperationalStatusKeys.Checkpoint("tenant-a", "folder-a"))!.Sequence.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(1025)]
    public async Task UnsupportedStoredVersionCannotAdvanceCheckpoint(int version)
    {
        OperationalStatusTestStore store = new();
        var source = OperationalStatusTestData.Envelope(OperationalStatusTestData.Created()) with { PayloadVersion = version };
        await Should.ThrowAsync<Exception>(() => new FoldersOperationalStatusEventProcessor(new(store, store, store))
            .ProcessAsync(source, TestContext.Current.CancellationToken));
        store.Count.ShouldBe(0);
    }
}
