using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>
/// Cancels a test-owned client token after archive processing reaches policy evaluation.
/// </summary>
internal sealed class CancelMidFlightFolderArchivePolicyEvidenceProvider(CancellationTokenSource requestCancellation)
    : IFolderArchivePolicyEvidenceProvider
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _serverCancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _requestCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _requestCancellation = requestCancellation
        ?? throw new ArgumentNullException(nameof(requestCancellation));
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
    /// <remarks>
    /// <para><c>IDomainProcessor.ProcessAsync</c> carries no token, so <c>FolderDomainProcessor</c> hands
    /// evidence providers <see cref="CancellationToken.None"/>. The <paramref name="cancellationToken"/>
    /// guard below is therefore defensive only: this fake manufactures the cancellation itself rather than
    /// observing the caller's. Nothing here proves that a public request token reaches the processor.</para>
    /// <para>The retry returns the same <c>policyVersion</c> as a normal allow would. That version is bound
    /// into the archive decision fingerprint by <c>FolderArchiveTenantGate</c>, so a drifting value would
    /// make a same-key retry look like an idempotency conflict for a reason unrelated to the unwind under
    /// test.</para>
    /// </remarks>
    public async Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
        ArchiveFolder command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        int call = Interlocked.Increment(ref _calls);
        if (call > 1)
        {
            return FolderArchivePolicyEvidence.Allowed(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                PolicyVersion);
        }

        _entered.TrySetResult();
        await _requestCancellation.CancelAsync().ConfigureAwait(false);

        throw new OperationCanceledException(_requestCancellation.Token);
    }
}
