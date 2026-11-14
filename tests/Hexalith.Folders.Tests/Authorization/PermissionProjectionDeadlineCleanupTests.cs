using Hexalith.Folders.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

/// <summary>Checks owned cancellation when a permission read exhausts through a timeout.</summary>
public sealed class PermissionProjectionDeadlineCleanupTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CleanupCallbackFailurePreservesTheSelectedOutcomeAndCallerPriorityAsync(bool stale, bool cancelCaller)
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellationTokenRegistration registration = default;
        bool callbackFinished = false;
        int calls = 0;
        var freshness = new EffectivePermissionsFreshness("read_your_writes", DateTimeOffset.UtcNow, "1", true, "projection_stale");
        var prior = EffectivePermissionsReadModelResult.Stale(new("tenant-a", "tenant-a", "folder-a",
            EffectivePermissionsFolderLifecycleState.Stale, [], freshness, false, null));
        try
        {
            Task<EffectivePermissionsReadModelResult> pending = PermissionProjectionCatchUp.ReadAsync(token =>
            {
                if (calls++ == 0)
                    registration = token.Register(() =>
                    {
                        try
                        {
                            if (cancelCaller) caller.Cancel();
                            throw new InvalidOperationException("Controlled cleanup callback failure");
                        }
                        finally { callbackFinished = true; }
                    });
                return stale && calls == 1 ? Task.FromResult(prior)
                    : Task.FromException<EffectivePermissionsReadModelResult>(new TimeoutException());
            }, true, freshness.ObservedAt, caller.Token);
            if (cancelCaller)
                (await Should.ThrowAsync<OperationCanceledException>(() => pending)).CancellationToken.ShouldBe(caller.Token);
            else
            {
                var result = await pending.ConfigureAwait(true);
                if (stale) result.ShouldBe(prior);
                else result.Freshness.ReasonCode.ShouldBe("permission_projection_catch_up_timeout");
            }
            callbackFinished.ShouldBeTrue();
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExhaustionWaitsForAnAlreadyRunningDeadlineNotificationAsync()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualRead = new TaskCompletionSource<EffectivePermissionsReadModelResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource? source = null;
        CancellationTokenRegistration sourceRegistration = default;
        CancellationTokenRegistration blocker = default;
        Task<EffectivePermissionsReadModelResult>? pending = null;
        bool returnedBeforeNotification = false;
        try
        {
            pending = PermissionProjectionCatchUp.ReadAsync(token =>
            {
                source = CancellationTokenSource.CreateLinkedTokenSource(token);
                sourceRegistration = source.Token.Register(() => actualRead.TrySetCanceled(source.Token));
                blocker = token.Register(() => { entered.TrySetResult(); release.Wait(); });
                return actualRead.Task;
            }, true, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            returnedBeforeNotification = await Task.WhenAny(pending, Task.Delay(100, TestContext.Current.CancellationToken)) == pending;
        }
        finally
        {
            release.Set();
            if (pending is not null)
            {
                var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
            }
            await blocker.DisposeAsync();
            await sourceRegistration.DisposeAsync();
            source?.Dispose();
        }
        returnedBeforeNotification.ShouldBeFalse();
        source.ShouldNotBeNull().IsCancellationRequested.ShouldBeTrue();
        actualRead.Task.IsCanceled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationBeforeItsDeadlineLinkStillCancelsTheActualReadAsync(bool callbackThrows)
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualRead = new TaskCompletionSource<EffectivePermissionsReadModelResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource? source = null;
        CancellationTokenRegistration sourceRegistration = default;
        CancellationTokenRegistration blocker = default;
        Task? cancellation = null;
        try
        {
            Task<EffectivePermissionsReadModelResult> pending = PermissionProjectionCatchUp.ReadAsync(token =>
            {
                source = CancellationTokenSource.CreateLinkedTokenSource(token);
                sourceRegistration = source.Token.Register(() =>
                {
                    actualRead.TrySetCanceled(source.Token);
                    if (callbackThrows) throw new InvalidOperationException("Controlled linked callback failure");
                });
                // The later WaitAsync registration runs first; the older deadline link stays pending.
                blocker = caller.Token.Register(() => { entered.TrySetResult(); release.Wait(); });
                return actualRead.Task;
            }, true, DateTimeOffset.UtcNow, caller.Token);
            cancellation = Task.Run(caller.Cancel, TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var failure = await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            failure.CancellationToken.ShouldBe(caller.Token);
            source.ShouldNotBeNull().IsCancellationRequested.ShouldBeTrue();
            actualRead.Task.IsCanceled.ShouldBeTrue();
        }
        finally
        {
            release.Set();
            if (cancellation is not null) await cancellation.ConfigureAwait(true);
            try { source?.Cancel(); }
            catch (AggregateException) { }
            await blocker.DisposeAsync();
            await sourceRegistration.DisposeAsync();
            source?.Dispose();
        }
    }

    [Fact]
    public async Task CallerCancellationDuringOwnedDeadlineCleanupKeepsItsOriginalTokenAsync()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellationTokenRegistration registration = default;
        try
        {
            var failure = await Should.ThrowAsync<OperationCanceledException>(() => PermissionProjectionCatchUp.ReadAsync(token =>
            {
                registration = token.Register(caller.Cancel);
                return Task.FromException<EffectivePermissionsReadModelResult>(new TimeoutException());
            }, true, DateTimeOffset.UtcNow, caller.Token));
            failure.CancellationToken.ShouldBe(caller.Token);
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }

    [Fact]
    public async Task TimeoutExhaustionCancelsTheActualReadTokenBeforeReturningUnavailableAsync()
    {
        CancellationToken observed = default;
        EffectivePermissionsReadModelResult result = await PermissionProjectionCatchUp.ReadAsync(token =>
        {
            observed = token;
            return Task.FromException<EffectivePermissionsReadModelResult>(new TimeoutException());
        }, true, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(EffectivePermissionsReadModelStatus.Unavailable);
        result.Freshness.ReasonCode.ShouldBe("permission_projection_catch_up_timeout");
        observed.CanBeCanceled.ShouldBeTrue();
        observed.IsCancellationRequested.ShouldBeTrue();
    }
}
