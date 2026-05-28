namespace Hexalith.Folders.UI.E2E.Tests.Routes;

/// <summary>
/// Path constants for the operations console. Per the project README's Route Contract,
/// tests must not hardcode route strings outside this file. Story 6.3 adds the development
/// state-label gallery; Stories 6.6-6.8 add the diagnostic routes.
/// </summary>
public static class ConsoleRoutes
{
    public const string Home = "/";

    public const string Tenants = "/tenants";

    public const string StateLabelGallery = "/dev/state-label-gallery";
}
