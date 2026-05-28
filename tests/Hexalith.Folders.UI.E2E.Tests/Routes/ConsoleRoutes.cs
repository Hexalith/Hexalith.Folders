namespace Hexalith.Folders.UI.E2E.Tests.Routes;

/// <summary>
/// Path constants for the operations console. Per the project README's Route Contract,
/// tests must not hardcode route strings outside this file. Stories 6.6-6.8 add the
/// diagnostic routes; Story 6.2 ships only Home and Tenants.
/// </summary>
public static class ConsoleRoutes
{
    public const string Home = "/";

    public const string Tenants = "/tenants";
}
