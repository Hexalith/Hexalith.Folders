namespace Hexalith.Folders.Testing.Polling;

public static class Eventually
{
    public static async Task<T> UntilAsync<T>(
        Func<CancellationToken, Task<T>> probe,
        Predicate<T> isReady,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(isReady);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be greater than zero.");
        }

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            T lastValue = await InvokeProbeAsync(probe, linkedSource.Token).ConfigureAwait(false);
            while (!isReady(lastValue))
            {
                await Task.Delay(interval, linkedSource.Token).ConfigureAwait(false);
                lastValue = await InvokeProbeAsync(probe, linkedSource.Token).ConfigureAwait(false);
            }

            return lastValue;
        }
        catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The condition was not ready before the timeout elapsed.", ex);
        }
    }

    private static async Task<T> InvokeProbeAsync<T>(Func<CancellationToken, Task<T>> probe, CancellationToken cancellationToken) =>
        await probe(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
}
