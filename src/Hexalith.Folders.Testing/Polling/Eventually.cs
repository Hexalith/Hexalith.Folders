namespace Hexalith.Folders.Testing.Polling;

public static class Eventually
{
    /// <summary>
    /// Repeatedly invokes the probe until the predicate succeeds.
    /// </summary>
    /// <remarks>
    /// If both caller cancellation and timeout have been requested when polling cancellation is observed, timeout takes precedence.
    /// Probe cancellation reported with a different token propagates to the caller.
    /// </remarks>
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
        return await PollAsync(probe, isReady, interval, timeoutSource.Token, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls until the predicate succeeds, with independently supplied timeout and caller cancellation tokens.
    /// </summary>
    internal static async Task<T> PollAsync<T>(
        Func<CancellationToken, Task<T>> probe,
        Predicate<T> isReady,
        TimeSpan interval,
        CancellationToken timeoutToken,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);
        CancellationToken pollingToken = linkedSource.Token;

        try
        {
            T lastValue = await InvokeProbeAsync(probe, pollingToken).ConfigureAwait(false);
            while (!isReady(lastValue))
            {
                await Task.Delay(interval, pollingToken).ConfigureAwait(false);
                lastValue = await InvokeProbeAsync(probe, pollingToken).ConfigureAwait(false);
            }

            return lastValue;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == pollingToken && timeoutToken.IsCancellationRequested)
        {
            throw new TimeoutException("The condition was not ready before the timeout elapsed.", ex);
        }
    }

    private static async Task<T> InvokeProbeAsync<T>(Func<CancellationToken, Task<T>> probe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await probe(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
