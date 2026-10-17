using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Workers;

/// <summary>Allows concurrent callbacks to finish their preceding contiguous projection writes.</summary>
internal static class PermissionProjectionDelivery
{
    internal static async Task ApplyAsync(Func<CancellationToken, Task> apply, CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                try
                {
                    await apply(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    return;
                }
                catch (PermissionProjectionConcurrencyException)
                {
                    // Reread and validate the persisted row on every attempt. Invalid identity,
                    // payload, order and replay conflicts never enter this retry path.
                    await Task.Delay(25, deadline.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PermissionProjectionConcurrencyException("Permission projection delivery did not become contiguous within its deadline.");
        }
    }
}
