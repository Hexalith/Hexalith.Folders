using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

public sealed class ScopedLayeredFolderAuthorizationResultAccessor : ILayeredFolderAuthorizationResultAccessor
{
    public LayeredFolderAuthorizationResult? Current { get; set; }
}
