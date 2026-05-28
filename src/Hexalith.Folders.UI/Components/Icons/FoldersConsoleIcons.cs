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

    // Story 6.6 — hand-authored 16px status glyphs for the Trust Matrix (UX-DR9 cells need an icon)
    // and the safe-error panel. Authored as core Fluent UI Icon paths for the same reason as
    // LockClosed16: the …Components.Icons package is deliberately off the reference graph.
    private const string CheckmarkCirclePath = "<path d=\"M8 1.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13Zm3.1 4.6-3.7 3.7a.75.75 0 0 1-1.06 0L4.9 8.4a.75.75 0 1 1 1.06-1.06l.9.9 3.17-3.17a.75.75 0 1 1 1.07 1.06Z\"/>";
    private const string WarningPath = "<path d=\"M7.1 2.3 1.7 12a1 1 0 0 0 .9 1.5h10.8a1 1 0 0 0 .9-1.5L8.9 2.3a1 1 0 0 0-1.8 0ZM8 6a.75.75 0 0 1 .75.75v2.5a.75.75 0 0 1-1.5 0v-2.5A.75.75 0 0 1 8 6Zm0 5.5a.9.9 0 1 1 0-1.8.9.9 0 0 1 0 1.8Z\"/>";
    private const string ErrorCirclePath = "<path d=\"M8 1.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13ZM8 4.5a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 8 4.5Zm0 6.2a.9.9 0 1 1 0-1.8.9.9 0 0 1 0 1.8Z\"/>";
    private const string InfoPath = "<path d=\"M8 1.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13ZM8 5.3a.9.9 0 1 1 0-1.8.9.9 0 0 1 0 1.8Zm.75 5.95a.75.75 0 0 1-1.5 0v-3.5a.75.75 0 0 1 1.5 0Z\"/>";
    private const string QuestionPath = "<path d=\"M8 1.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13Zm0 9.6a.9.9 0 1 1 0 1.8.9.9 0 0 1 0-1.8Zm0-6.6a2.1 2.1 0 0 1 1 3.95c-.34.2-.5.34-.5.6v.2a.75.75 0 0 1-1.5 0v-.2c0-.95.6-1.45 1.05-1.7a.6.6 0 1 0-.9-.52.75.75 0 0 1-1.43-.45A2.1 2.1 0 0 1 8 4.5Z\"/>";
    private const string ClockPath = "<path d=\"M8 1.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13Zm0 2.5a.75.75 0 0 1 .75.75V8h2a.75.75 0 0 1 0 1.5H8A.75.75 0 0 1 7.25 8.75v-4A.75.75 0 0 1 8 4Z\"/>";

    /// <summary>Creates the F-5 closed-padlock affordance icon at 16px.</summary>
    public static Icon LockClosed16() => Create("LockClosed", IconSize.Size16, LockClosedPath);

    /// <summary>Creates the success/ready status glyph at 16px.</summary>
    public static Icon CheckmarkCircle16() => Create("CheckmarkCircle", IconSize.Size16, CheckmarkCirclePath);

    /// <summary>Creates the warning/degraded status glyph at 16px.</summary>
    public static Icon Warning16() => Create("Warning", IconSize.Size16, WarningPath);

    /// <summary>Creates the error/failed status glyph at 16px.</summary>
    public static Icon ErrorCircle16() => Create("ErrorCircle", IconSize.Size16, ErrorCirclePath);

    /// <summary>Creates the informational status glyph at 16px.</summary>
    public static Icon Info16() => Create("Info", IconSize.Size16, InfoPath);

    /// <summary>Creates the unknown status glyph at 16px.</summary>
    public static Icon Question16() => Create("Question", IconSize.Size16, QuestionPath);

    /// <summary>Creates the delayed/freshness-lag status glyph at 16px.</summary>
    public static Icon Clock16() => Create("Clock", IconSize.Size16, ClockPath);

    private static Icon Create(string name, IconSize size, string content)
        => new(name, IconVariant.Regular, size, content);
}
