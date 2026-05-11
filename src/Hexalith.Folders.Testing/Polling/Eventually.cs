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

        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        T lastValue = await probe(timeoutSource.Token).ConfigureAwait(false);
        while (!isReady(lastValue))
        {
            await Task.Delay(interval, timeoutSource.Token).ConfigureAwait(false);
            lastValue = await probe(timeoutSource.Token).ConfigureAwait(false);
        }

        return lastValue;
    }
}
