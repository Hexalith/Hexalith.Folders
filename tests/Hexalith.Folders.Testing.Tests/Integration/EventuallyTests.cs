using System.Diagnostics;
using Hexalith.Folders.Testing.Polling;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Integration;

public sealed class EventuallyTests
{
    [Fact]
    public async Task UntilAsyncPollsUntilTheConditionIsReady()
    {
        int attempts = 0;

        int result = await Eventually.UntilAsync(
            _ => Task.FromResult(++attempts),
            value => value >= 3,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(10),
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
            TimeSpan.FromMilliseconds(10),
            TestContext.Current.CancellationToken)).ConfigureAwait(true);

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
}
