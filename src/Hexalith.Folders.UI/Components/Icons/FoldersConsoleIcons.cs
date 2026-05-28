using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Icons;

/// <summary>
/// Story 6.4 / F-5 — Folders-owned icon factory built on Fluent UI v5's core <see cref="Icon"/>
/// abstraction, mirroring FrontComposer's <c>FcFluentIcons</c>.
/// </summary>
/// <remarks>
/// The icons are hand-authored SVG <c>&lt;path&gt;</c> data deliberately: <c>Hexalith.Folders.UI</c>
/// references only the <em>core</em> <c>Microsoft.FluentUI.AspNetCore.Components</c> package
/// (transitively via <c>Hexalith.FrontComposer.Shell</c>); the <c>…Components.Icons</c> package is
/// <b>not</b> on its reference graph. Pulling it in to get <c>Icons.Regular.Size16.LockClosed</c> would
/// add a <c>&lt;PackageReference&gt;</c>, which Story 6.4 AC #7 forbids. A <c>FluentIcon</c>-rendered vector
/// inherits Fluent UI sizing/theming and screen-reader semantics with no static-asset pipeline.
/// </remarks>
public static class FoldersConsoleIcons
{
    private const string LockClosedPath = "<path d=\"M8 1a3 3 0 0 0-3 3v2H4.5A1.5 1.5 0 0 0 3 7.5v6A1.5 1.5 0 0 0 4.5 15h7a1.5 1.5 0 0 0 1.5-1.5v-6A1.5 1.5 0 0 0 11.5 6H11V4a3 3 0 0 0-3-3Zm2 5H6V4a2 2 0 1 1 4 0v2Z\"/>";

    /// <summary>Creates the F-5 closed-padlock affordance icon at 16px.</summary>
    public static Icon LockClosed16() => Create("LockClosed", IconSize.Size16, LockClosedPath);

    private static Icon Create(string name, IconSize size, string content)
        => new(name, IconVariant.Regular, size, content);
}
