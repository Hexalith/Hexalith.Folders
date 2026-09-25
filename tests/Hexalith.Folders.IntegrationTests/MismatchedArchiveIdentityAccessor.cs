using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

using Shouldly;

namespace Hexalith.Folders.IntegrationTests;

internal sealed class MismatchedArchiveIdentityAccessor : ILayeredFolderAuthorizationResultAccessor
{
    private readonly ScopedLayeredFolderAuthorizationResultAccessor _inner = new();

    public LayeredFolderAuthorizationResult? Current => _inner.Current;

    public int BeginCalls { get; private set; }

    public int EndCalls { get; private set; }

    public void BeginScope(LayeredFolderAuthorizationResult result)
    {
        result.IsAllowed.ShouldBeTrue();
        result.AllowedContext.ShouldNotBeNull();
        result.AllowedContext.PrincipalId.ShouldBe("user-a");
        result.AllowedContext.ActorSafeIdentifier.ShouldBe("user-a");
        BeginCalls++;
        _inner.BeginScope(result with
        {
            AllowedContext = result.AllowedContext with { ActorSafeIdentifier = "different-actor" },
        });
    }

    public void EndScope()
    {
        EndCalls++;
        _inner.EndScope();
    }
}
