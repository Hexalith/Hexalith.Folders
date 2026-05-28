using Hexalith.Folders.Client.Generated;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / UX-DR7 / UX-DR12 — renders permitted path <b>metadata</b> as evidence, never a file
/// manager. Columns per §3.1: <c>path | type | size-class | last op | changed | access | redaction</c>.
/// There is <b>no</b> file open, file content, raw diff, or download affordance. Redaction renders
/// through the shipped <see cref="RedactedField"/> (lock + text), kept distinct from unknown/missing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Table variant (AC #4 / ux-spec:783).</b> The component renders the metadata as a semantic table
/// that preserves hierarchy (the full normalized path plus a depth indent) with accessible column
/// headers and per-cell labels for redacted/excluded entries — the explicitly permitted alternative to
/// a tree widget, chosen for accessibility robustness.
/// </para>
/// <para>
/// <b>Workspace-scoped (AC #4).</b> The data source requires a folder id, a workspace id, and a task id
/// (all three). When the host page cannot supply all three the component renders the "no workspace-bound
/// content" state and never fabricates identifiers.
/// </para>
/// <para>
/// <b>Shape gaps (do not paper over).</b> <see cref="FileMetadataItem"/> carries path/kind/byte-length/
/// sensitivity/redaction only; it has no per-entry "last op" or "changed/clean" signal, so the Last-op and
/// Changed columns render the honest <c>Unknown</c> disclosure rather than a fabricated value. The AC #4
/// §3.1 per-entry vocabulary is therefore consciously partial: <c>permitted</c>/<c>redacted</c>/
/// <c>excluded-by-policy</c>/<c>binary</c> are derived from the redaction tier (Access column);
/// <c>changed</c>/<c>clean</c> are not derivable and render <c>Unknown</c>; and <c>inaccessible</c> is a
/// workspace-level C6 lifecycle state surfaced on the page, not a per-entry file state. If a later
/// projection carries per-entry change/access signals, bind them to the Changed/Access columns then.
/// </para>
/// </remarks>
public partial class MetadataOnlyFolderTree : ComponentBase
{
    private bool _hasWorkspaceContext;
    private IReadOnlyList<FileMetadataItem> _sorted = [];

    /// <summary>Gets or sets the folder identifier (required by the file-context query).</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Gets or sets the workspace identifier (required by the file-context query).</summary>
    [Parameter]
    public string? WorkspaceId { get; set; }

    /// <summary>Gets or sets the task identifier (required by the file-context query).</summary>
    [Parameter]
    public string? TaskId { get; set; }

    /// <summary>
    /// Gets or sets the already-fetched file metadata items (the host page owns the SDK call and the
    /// shared correlation id). <see langword="null"/> or empty renders the no-matches empty state.
    /// </summary>
    [Parameter]
    public IReadOnlyList<FileMetadataItem>? Items { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _hasWorkspaceContext = !string.IsNullOrWhiteSpace(FolderId)
            && !string.IsNullOrWhiteSpace(WorkspaceId)
            && !string.IsNullOrWhiteSpace(TaskId);

        _sorted = Items is null
            ? []
            : [.. Items.OrderBy(static item => item.Path?.NormalizedPath ?? string.Empty, StringComparer.Ordinal)];
    }

    private static int DepthOf(FileMetadataItem item)
    {
        string path = item.Path?.NormalizedPath ?? string.Empty;
        return path.Count(static c => c == '/');
    }
}
