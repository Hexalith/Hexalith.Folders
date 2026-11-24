using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>
/// Waits for server-side cancellation during the first policy evaluation.
/// </summary>
internal sealed class CancelMidFlightFolderArchivePolicyEvidenceProvider
    : IFolderArchivePolicyEvidenceProvider
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _serverCancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _requestCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    /// <summary>Gets the stable policy version this fake reports, kept constant across attempts.</summary>
    private static string PolicyVersion => "v1-test-cancel-mid-flight";

    /// <summary>Gets the number of policy evaluations.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets a task that completes when policy evaluation is entered.</summary>
    public Task Entered => _entered.Task;

    /// <summary>Gets a task that completes when server middleware observes cancellation from <c>/process</c>.</summary>
    public Task ServerCancellationObserved => _serverCancellationObserved.Task;

    /// <summary>Gets a task that completes after the server request has fully unwound.</summary>
    public Task Completion => _requestCompleted.Task;

    /// <summary>Records server-side propagation of an <see cref="OperationCanceledException"/>.</summary>
    public void ObserveServerCancellation() => _serverCancellationObserved.TrySetResult();

    /// <summary>Signals that the hosting pipeline has completed request cleanup.</summary>
    public void CompleteRequest() => _requestCompleted.TrySetResult();

    /// <inheritdoc/>
    public async Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        int call = Interlocked.Increment(ref _calls);
        if (call == 1)
        {
            _entered.TrySetResult();
            if (!cancellationToken.CanBeCanceled)
            {
                throw new InvalidOperationException("Archive policy evaluation did not receive a request cancellation token.");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        return FolderArchivePolicyEvidence.Allowed(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            PolicyVersion);
    }
}
