namespace Hexalith.Folders.Authorization;

public sealed record LayeredFolderAuthorizationResult(
    bool IsAllowed,
    LayeredFolderAuthorizationDecisionSnapshot Decision,
    LayeredFolderAuthorizationAllowedContext? AllowedContext,
    IReadOnlyList<AuthorizationLayer> EvaluatedLayers)
{
    public static LayeredFolderAuthorizationResult Denied(
        LayeredFolderAuthorizationDecisionSnapshot decision,
        IReadOnlyList<AuthorizationLayer>? evaluatedLayers = null)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new(
            IsAllowed: false,
            decision,
            AllowedContext: null,
            evaluatedLayers ?? [decision.TerminalLayer]);
    }
}
