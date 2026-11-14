using System.Diagnostics;

namespace Hexalith.Folders.Authorization;

/// <summary>Waits briefly for committed events to reach permission projections before mutation denial.</summary>
internal static class PermissionProjectionCatchUp
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Bounds enabled reads and retries; exhausted deadlines never produce available authority.</summary>
    internal static async Task<EffectivePermissionsReadModelResult> ReadAsync(
        Func<CancellationToken, Task<EffectivePermissionsReadModelResult>> read, bool waitForCatchUp,
        DateTimeOffset observedAt, CancellationToken cancellationToken)
    {
        if (!waitForCatchUp)
        {
            return await read(cancellationToken).ConfigureAwait(false);
        }

        long started = Stopwatch.GetTimestamp();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var notificationCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Cancellation callbacks run in reverse registration order. This first registration
        // completes after the source registrations, including when the timer already owns cancellation.
        using CancellationTokenRegistration notification = deadline.Token.UnsafeRegister(
            static state => ((TaskCompletionSource)state!).TrySetResult(), notificationCompleted);
        deadline.CancelAfter(Budget);
        EffectivePermissionsReadModelResult? stale = null;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TimeSpan remaining = Budget - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                {
                    return await ExhaustedAsync().ConfigureAwait(false);
                }

                EffectivePermissionsReadModelResult result = await read(deadline.Token)
                    .WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                remaining = Budget - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                {
                    return await ExhaustedAsync().ConfigureAwait(false);
                }

                if (result.Status != EffectivePermissionsReadModelStatus.Stale)
                {
                    return result;
                }

                stale = result;
                await Task.Delay(remaining < PollInterval ? remaining : PollInterval,
                    deadline.Token).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            return await ExhaustedAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await ExhaustedAsync().ConfigureAwait(false);
        }

        // Preserve a completed stale classification, which denies mutations. A read that never
        // completed supplies no evidence; in particular, late available authority cannot escape.
        async Task<EffectivePermissionsReadModelResult> ExhaustedAsync()
        {
            // WaitAsync can win before the owned timer. Disposing that timer alone would
            // strand the actual source calls with an uncanceled token after exhaustion.
            try { await deadline.CancelAsync().ConfigureAwait(false); }
            catch (AggregateException)
            {
                // CancelAsync has observed every callback. A callback failure cannot
                // replace the already selected safe result or caller cancellation.
            }
            // CancelAsync does not join a notification that the timer already started.
            // Keep the registrations alive until that notification reaches our last callback.
            await notificationCompleted.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return stale ?? EffectivePermissionsReadModelResult.Unavailable("permission_projection_catch_up_timeout", observedAt);
        }
    }
}
