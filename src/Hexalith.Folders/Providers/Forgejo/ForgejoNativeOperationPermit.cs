namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Transfers one native-operation gate permit from its caller to the native worker.
/// </summary>
internal sealed class ForgejoNativeOperationPermit(SemaphoreSlim gate)
{
    private const int CallerOwned = 0;
    private const int NativeTaskOwned = 1;
    private const int Released = 2;
    private int _state = CallerOwned;

    /// <summary>
    /// Transfers ownership to a native task before that task is scheduled.
    /// </summary>
    public void TransferToNativeTask()
    {
        if (Interlocked.CompareExchange(ref _state, NativeTaskOwned, CallerOwned) != CallerOwned)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Releases a permit that was not transferred to a native task.
    /// </summary>
    public void ReleaseByCaller()
    {
        if (Interlocked.CompareExchange(ref _state, Released, CallerOwned) == CallerOwned)
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Releases a transferred permit after the native task exits.
    /// </summary>
    public void ReleaseByNativeTask()
    {
        if (Interlocked.CompareExchange(ref _state, Released, NativeTaskOwned) == NativeTaskOwned)
        {
            gate.Release();
        }
    }
}
