using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

public interface ILayeredFolderAuthorizationResultAccessor
{
    LayeredFolderAuthorizationResult? Current { get; set; }
}
