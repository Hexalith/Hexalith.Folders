using System.Diagnostics;

namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Tracks bounded native smart-Git execution without exposing native diagnostics.
/// </summary>
internal sealed class ForgejoNativeOperationState(
    string repositoryPath,
    CancellationToken cancellationToken,
    TimeSpan deadline,
    long maximumTransferBytes,
    long maximumDiskBytes)
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private int _failureCondition;
    private long _maximumObservedTransferBytes;

    public ForgejoApiFailureCondition FailureCondition => (ForgejoApiFailureCondition)Volatile.Read(ref _failureCondition);

    public bool Check(long receivedBytes)
    {
        if (FailureCondition != ForgejoApiFailureCondition.None)
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Fail(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }

        if (_elapsed.Elapsed >= deadline)
        {
            return Fail(ForgejoApiFailureCondition.OperationTimedOut);
        }

        _maximumObservedTransferBytes = Math.Max(_maximumObservedTransferBytes, receivedBytes);
        if (_maximumObservedTransferBytes > maximumTransferBytes)
        {
            return Fail(ForgejoApiFailureCondition.TransferLimitExceeded);
        }

        try
        {
            long size = 0;
            foreach (string file in Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories))
            {
                size += new FileInfo(file).Length;
                if (size > maximumDiskBytes)
                {
                    return Fail(ForgejoApiFailureCondition.TemporaryDiskLimitExceeded);
                }
            }
        }
        catch (IOException)
        {
            return Fail(ForgejoApiFailureCondition.ServerUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return Fail(ForgejoApiFailureCondition.ServerUnavailable);
        }

        return true;
    }

    private bool Fail(ForgejoApiFailureCondition condition)
    {
        _ = Interlocked.CompareExchange(
            ref _failureCondition,
            (int)condition,
            (int)ForgejoApiFailureCondition.None);
        return false;
    }
}
