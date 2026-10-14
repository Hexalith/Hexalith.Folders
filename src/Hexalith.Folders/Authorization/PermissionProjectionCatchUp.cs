using System.Diagnostics;

namespace Hexalith.Folders.Authorization;

/// <summary>Waits briefly for committed events to reach permission projections before mutation denial.</summary>
internal static class PermissionProjectionCatchUp
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);
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
                    return Exhausted();
                }

                EffectivePermissionsReadModelResult result = await read(deadline.Token)
                    .WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                remaining = Budget - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                {
                    return Exhausted();
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
            cancellationToken.ThrowIfCancellationRequested();
            return Exhausted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            return Exhausted();
        }

        // Preserve a completed stale classification, which denies mutations. A read that never
        // completed supplies no evidence; in particular, late available authority cannot escape.
        EffectivePermissionsReadModelResult Exhausted()
            => stale ?? EffectivePermissionsReadModelResult.Unavailable("permission_projection_catch_up_timeout", observedAt);
    }
}
