using System.Diagnostics;
using Hexalith.Folders.Testing.Polling;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Integration;

public sealed class EventuallyTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task UntilAsyncPollsUntilTheConditionIsReady()
    {
        int attempts = 0;

        int result = await Eventually.UntilAsync(
            _ => Task.FromResult(++attempts),
            value => value >= 3,
            TimeSpan.FromSeconds(1),
            _interval,
            TestContext.Current.CancellationToken);

        result.ShouldBe(3);
    }

    [Fact]
    public async Task UntilAsyncTimesOutWhenProbeIgnoresCancellation()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        await Should.ThrowAsync<TimeoutException>(() => Eventually.UntilAsync(
            async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                return 0;
            },
            _ => false,
            TimeSpan.FromMilliseconds(50),
            _interval,
            TestContext.Current.CancellationToken)).ConfigureAwait(true);

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UntilAsyncDoesNotInvokeProbeWhenCallerAlreadyCanceledAsync()
    {
        bool probeInvoked = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Eventually.UntilAsync(
            _ =>
            {
                probeInvoked = true;
                return Task.FromResult(1);
            },
            _ => true,
            TimeSpan.FromSeconds(1),
            _interval,
            new CancellationToken(canceled: true)));

        probeInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task PollAsyncTimesOutWhenBothTimeoutAndCallerCancelAsync()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using CancellationTokenSource timeout = new();

        await Should.ThrowAsync<TimeoutException>(() => Eventually.PollAsync(
            pollingToken =>
            {
                caller.Cancel();
                timeout.Cancel();
                return Task.FromCanceled<int>(pollingToken);
            },
            _ => false,
            _interval,
            timeout.Token,
            caller.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PollAsyncPreservesProbeCancellationWithADifferentTokenAsync(bool linkProbeToken)
    {
        using CancellationTokenSource timeout = new();
        CancellationToken probeToken = default;

        OperationCanceledException cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Eventually.PollAsync(
            pollingToken =>
            {
                using CancellationTokenSource probe = linkProbeToken
                    ? CancellationTokenSource.CreateLinkedTokenSource(pollingToken)
                    : new CancellationTokenSource();
                probeToken = probe.Token;
                if (!linkProbeToken)
                {
                    probe.Cancel();
                }

                timeout.Cancel();
                return Task.FromCanceled<int>(probeToken);
            },
            _ => false,
            _interval,
            timeout.Token,
            TestContext.Current.CancellationToken));

        cancellation.CancellationToken.ShouldBe(probeToken);
    }

    [Fact]
    public async Task PollAsyncPropagatesCallerCancellationAsync()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<OperationCanceledException>(() => Eventually.PollAsync(
            pollingToken =>
            {
                caller.Cancel();
                return Task.FromCanceled<int>(pollingToken);
            },
            _ => false,
            _interval,
            CancellationToken.None,
            caller.Token));
    }
}
