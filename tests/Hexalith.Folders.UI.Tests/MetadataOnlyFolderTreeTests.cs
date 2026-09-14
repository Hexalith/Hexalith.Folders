using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #4 — the metadata-only folder tree renders permitted path metadata as evidence with
/// redacted/excluded/unknown entries visibly + semantically distinct, exposes no file content/diff/
/// download affordance, and never fabricates a workspace/task id.
/// </summary>
public sealed class MetadataOnlyFolderTreeTests
{
    [Fact]
    public void WithoutWorkspaceContext_RendersNoWorkspaceBoundContent()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<MetadataOnlyFolderTree> rendered = ctx.Render<MetadataOnlyFolderTree>(p => p
            .Add(t => t.FolderId, "folder-1"));

        rendered.Find("[data-testid=\"metadata-only-folder-tree-no-context\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"metadata-only-folder-tree-row\"]").ShouldBeEmpty();
    }

    [Fact]
    public void RedactedExcludedAndUnknownEntries_AreVisiblyDistinct()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        List<FileMetadataItem> items =
        [
            Item("src/visible.cs", FileMetadataItemKind.File, FileMetadataItemRedaction.Not_redacted),
            Item("src/secret.cs", FileMetadataItemKind.File, HistoricalRedaction(1)),
            Item("src/excluded.bin", FileMetadataItemKind.File, HistoricalRedaction(2)),
            Item("src/blob.bin", FileMetadataItemKind.File, HistoricalRedaction(3)),
        ];

        IRenderedComponent<MetadataOnlyFolderTree> rendered = ctx.Render<MetadataOnlyFolderTree>(p => p
            .Add(t => t.FolderId, "folder-1")
            .Add(t => t.WorkspaceId, "workspace-1")
            .Add(t => t.TaskId, "task-1")
            .Add(t => t.Items, items));

        rendered.FindAll("[data-testid=\"metadata-only-folder-tree-row\"]").Count.ShouldBe(4);
        rendered.FindAll("[data-fc-disclosure=\"redacted\"]").Count.ShouldBe(2);
        rendered.Markup.ShouldNotContain("secret.cs");
        rendered.FindAll("[data-fc-disclosure=\"unknown\"]").Count.ShouldBe(8);
        rendered.FindAll("[data-fc-disclosure=\"missing\"]").Count.ShouldBe(3);
        rendered.Markup.ShouldContain("Permitted");
        rendered.Markup.ShouldContain("Redacted");
        rendered.Markup.ShouldContain("Excluded by policy");
        rendered.Markup.ShouldContain("Binary");
    }

    [Fact]
    public void ExcludedAndBinaryEntries_DoNotFabricateSize_AndWithholdExcludedPath()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        List<FileMetadataItem> items =
        [
            Item("src/excluded.bin", FileMetadataItemKind.File, HistoricalRedaction(2)),
            Item("src/blob.bin", FileMetadataItemKind.File, HistoricalRedaction(3)),
        ];

        IRenderedComponent<MetadataOnlyFolderTree> rendered = ctx.Render<MetadataOnlyFolderTree>(p => p
            .Add(t => t.FolderId, "folder-1")
            .Add(t => t.WorkspaceId, "workspace-1")
            .Add(t => t.TaskId, "task-1")
            .Add(t => t.Items, items));

        // A withheld byte length is never presented as a real "empty" size (fabricated metadata).
        rendered.Markup.ShouldNotContain("empty");

        rendered.Markup.ShouldNotContain("excluded.bin");
    }

    [Fact]
    public void RendersNoFileContentDiffOrDownloadAffordance()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        List<FileMetadataItem> items =
        [
            Item("src/a.cs", FileMetadataItemKind.File, FileMetadataItemRedaction.Not_redacted),
        ];

        IRenderedComponent<MetadataOnlyFolderTree> rendered = ctx.Render<MetadataOnlyFolderTree>(p => p
            .Add(t => t.FolderId, "folder-1")
            .Add(t => t.WorkspaceId, "workspace-1")
            .Add(t => t.TaskId, "task-1")
            .Add(t => t.Items, items));

        // No open/diff/download control: no links or buttons inside the metadata-only tree.
        rendered.FindAll("a").ShouldBeEmpty();
        rendered.FindAll("button").ShouldBeEmpty();
        rendered.FindAll("[download]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void WithContextButNoItems_RendersNoMatchesEmptyState()
    {
        using BunitContext ctx = BadgeRenderingFixture.Create();

        IRenderedComponent<MetadataOnlyFolderTree> rendered = ctx.Render<MetadataOnlyFolderTree>(p => p
            .Add(t => t.FolderId, "folder-1")
            .Add(t => t.WorkspaceId, "workspace-1")
            .Add(t => t.TaskId, "task-1")
            .Add(t => t.Items, new List<FileMetadataItem>()));

        rendered.Find("[data-fc-empty-reason=\"no_matches\"]").ShouldNotBeNull();
    }

    private static FileMetadataItem Item(string path, FileMetadataItemKind kind, FileMetadataItemRedaction redaction)
    {
        var pathMetadata = new VisiblePathMetadata
        {
            NormalizedPath = path,
            DisplayName = path[(path.LastIndexOf('/') + 1)..],
            PathPolicyClass = VisiblePathMetadataPathPolicyClass.Metadata_only,
            UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
        };
        ((PathMetadata)pathMetadata).PathPolicyClass = PathMetadataPathPolicyClass.Metadata_only;

        return new FileMetadataItem
        {
            Path = pathMetadata,
            Kind = kind,
            ByteLength = 256,
            Sensitivity = SensitiveMetadataTier.Public_metadata,
            Redaction = redaction,
        };
    }

    private static FileMetadataItemRedaction HistoricalRedaction(int ordinal) => (FileMetadataItemRedaction)ordinal;
}
