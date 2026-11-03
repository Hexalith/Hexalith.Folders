using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Provides a real background execution whose pending stop can be observed without changing the host.</summary>
internal sealed class LifecycleDiagnosticBackgroundService : BackgroundService
{
    /// <summary>Gets the point where the real background task starts.</summary>
    internal TaskCompletionSource Running { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the point where the host cancels the original stopping token.</summary>
    internal TaskCompletionSource Stopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the test's release of the existing background task.</summary>
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var registration = stoppingToken.Register(() => Stopping.TrySetResult());
        Running.TrySetResult();
        await Release.Task.ConfigureAwait(false);
    }
}
