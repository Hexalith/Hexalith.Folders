using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Resources;

/// <summary>
/// The read-only <c>folder-tree</c> MCP resource: a metadata-only view over the canonical
/// <see cref="IClient.ListFolderFilesAsync(string, string, string, string, ReadConsistencyClass?, string, int?, CancellationToken)"/>
/// query for a prepared workspace. It introduces no new query semantics — it reuses the same shared
/// <see cref="ToolPipeline"/> as the tools (correlation echo, task-id sourcing, canonical failure-kind
/// projection, metadata-only serialization). The companion glob view is reachable via the
/// <c>glob-folder-files</c> tool.
/// </summary>
/// <remarks>
/// ModelContextProtocol 1.3.0 exposes a stable resource attribute surface
/// (<c>[McpServerResource]</c>/<c>[McpServerResourceType]</c> + <c>WithResourcesFromAssembly</c>), verified
/// against the pinned package — so the AC #3 primary path (resources) is used, not the read-tool fallback.
/// The URI template carries the task-scoped identifiers because <c>ListFolderFiles</c> requires a task ID.
/// </remarks>
[McpServerResourceType]
internal static class FolderTreeResource
{
    [McpServerResource(
        Name = "folder-tree",
        UriTemplate = "folders://folder-tree/{folderId}/{workspaceId}/{taskId}",
        MimeType = "application/json")]
    [Description("Read-only metadata-only file tree for a prepared workspace, over the ListFolderFiles query.")]
    public static Task<string> Read(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required; ListFolderFiles is task-scoped).")] string taskId,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId: null, (client, s, ct) => ToolPipeline.AsObject(
            client.ListFolderFilesAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, x_Hexalith_Freshness: null, cursor: null, limit: null, ct)), cancellationToken);
}
