using Hexalith.Folders.Workers;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class PermissionProjectionDeliveryTests
{
    [Fact]
    public async Task AlreadyCancelledCallerNeverEntersProjection()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync().ConfigureAwait(true);
        bool entered = false;
        await Should.ThrowAsync<OperationCanceledException>(() => PermissionProjectionDelivery.ApplyAsync(_ =>
        {
            entered = true;
            return Task.CompletedTask;
        }, cancellation.Token)).ConfigureAwait(true);
        entered.ShouldBeFalse();
    }

    [Fact]
    public async Task CallerCancellationStopsInFlightDelivery()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task delivery = PermissionProjectionDelivery.ApplyAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(true);
        }, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await cancellation.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => delivery).ConfigureAwait(true);
    }
}
