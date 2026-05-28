using System.Globalization;

namespace Hexalith.Folders.UI.E2E.Tests.Routes;

/// <summary>
/// Path constants for the operations console. Per the project README's Route Contract,
/// tests must not hardcode route strings outside this file. Story 6.3 adds the development
/// state-label gallery; Stories 6.6-6.8 add the diagnostic routes.
/// </summary>
public static class ConsoleRoutes
{
    /// <summary>The operations console home.</summary>
    public const string Home = "/";

    /// <summary>The tenant orientation page.</summary>
    public const string Tenants = "/tenants";

    /// <summary>The development-only state-label gallery.</summary>
    public const string StateLabelGallery = "/dev/state-label-gallery";

    /// <summary>The folder list / discovery entry (Story 6.6).</summary>
    public const string Folders = "/folders";

    /// <summary>Builds the folder detail route (Story 6.6 Folder view, §3.1).</summary>
    public static string FolderDetail(string folderId)
        => string.Create(CultureInfo.InvariantCulture, $"/folders/{folderId}");

    /// <summary>Builds the workspace detail route (Story 6.6 Workspace view, §3.2).</summary>
    public static string Workspace(string folderId, string workspaceId)
        => string.Create(CultureInfo.InvariantCulture, $"/folders/{folderId}/workspaces/{workspaceId}");
}
