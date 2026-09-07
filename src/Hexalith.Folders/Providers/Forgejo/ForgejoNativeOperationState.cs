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
    private long _maximumObservedTransferBytes;

    public ForgejoApiFailureCondition FailureCondition { get; private set; }

    public bool Check(long receivedBytes)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            FailureCondition = ForgejoApiFailureCondition.CancellationBeforeDispatch;
            return false;
        }

        if (_elapsed.Elapsed >= deadline)
        {
            FailureCondition = ForgejoApiFailureCondition.OperationTimedOut;
            return false;
        }

        _maximumObservedTransferBytes = Math.Max(_maximumObservedTransferBytes, receivedBytes);
        if (_maximumObservedTransferBytes > maximumTransferBytes)
        {
            FailureCondition = ForgejoApiFailureCondition.TransferLimitExceeded;
            return false;
        }

        try
        {
            long size = 0;
            foreach (string file in Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories))
            {
                size += new FileInfo(file).Length;
                if (size > maximumDiskBytes)
                {
                    FailureCondition = ForgejoApiFailureCondition.TemporaryDiskLimitExceeded;
                    return false;
                }
            }
        }
        catch (IOException)
        {
            FailureCondition = ForgejoApiFailureCondition.ServerUnavailable;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            FailureCondition = ForgejoApiFailureCondition.ServerUnavailable;
            return false;
        }

        return true;
    }
}
