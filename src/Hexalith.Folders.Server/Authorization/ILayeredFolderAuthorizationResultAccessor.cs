using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

// Scoped accessor for the layered-auth result of the current /process request. The
// request handler owns the scope lifecycle via BeginScope/EndScope; consumers see only
// the read-only Current accessor so a downstream processor or evidence provider cannot
// mutate the scope's authorization context mid-request.
public interface ILayeredFolderAuthorizationResultAccessor
{
    LayeredFolderAuthorizationResult? Current { get; }

    void BeginScope(LayeredFolderAuthorizationResult result);

    void EndScope();
}
