using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Exposes an actual hosted stop wait for lifecycle observation qualification.</summary>
internal sealed class LifecycleDiagnosticTestService : IHostedService
{
    /// <summary>Gets the point where the normal host stop calls this service.</summary>
    internal TaskCompletionSource Stopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the test-owned release of the normal hosted stop.</summary>
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Stopping.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
