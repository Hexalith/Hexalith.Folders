using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

public sealed class ScopedLayeredFolderAuthorizationResultAccessor : ILayeredFolderAuthorizationResultAccessor
{
    public LayeredFolderAuthorizationResult? Current { get; private set; }

    public void BeginScope(LayeredFolderAuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Current = result;
    }

    public void EndScope() => Current = null;
}
