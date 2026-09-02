namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Converts folder lifecycle authorization outcomes to their public compatibility tokens.
/// </summary>
internal static class FolderLifecycleAuthorizationOutcomeExtensions
{
    /// <summary>
    /// Converts an authorization outcome to its canonical token, failing closed for unknown values.
    /// </summary>
    /// <param name="outcome">The authorization outcome.</param>
    /// <returns>The canonical public token.</returns>
    internal static string ToToken(this FolderLifecycleAuthorizationOutcome outcome)
        => outcome == FolderLifecycleAuthorizationOutcome.Allowed
            ? "allowed"
            : "denied_safe";
}
