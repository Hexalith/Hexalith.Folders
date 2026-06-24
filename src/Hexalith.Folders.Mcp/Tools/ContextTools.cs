using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for file-context queries over a prepared workspace (tree, metadata, search, glob, range-read).
/// Every operation's <see cref="IClient"/> signature carries <c>x_Hexalith_Task_Id</c>, so each requires
/// <c>taskId</c>; none accepts an idempotency key. The <c>read-file-range</c> result carries authorized
/// content, which the metadata-only serializer drops from all output.
/// </summary>
[McpServerToolType]
internal static class ContextTools
{
    [McpServerTool(Name = "list-folder-files")]
    [Description("List metadata-only folder files (query, task-scoped).")]
    public static Task<string> ListFolderFiles(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Opaque pagination cursor echoed from a previous page.")] string? cursor = null,
        [Description("Maximum number of items to return.")] int? limit = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ListFolderFilesAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), cursor, limit, ct)), cancellationToken);

    [McpServerTool(Name = "get-folder-file-metadata")]
    [Description("Get metadata for specific folder files (query, task-scoped).")]
    public static Task<string> GetFolderFileMetadata(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Request body as inline JSON matching the FileMetadataRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetFolderFileMetadataAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), RequestBody.Read<FileMetadataRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "search-folder-files")]
    [Description("Search folder files (query, task-scoped).")]
    public static Task<string> SearchFolderFiles(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Request body as inline JSON matching the FileSearchRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.SearchFolderFilesAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), RequestBody.Read<FileSearchRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "glob-folder-files")]
    [Description("Glob folder files by pattern (query, task-scoped).")]
    public static Task<string> GlobFolderFiles(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Request body as inline JSON matching the FileGlobRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GlobFolderFilesAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), RequestBody.Read<FileGlobRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "read-file-range")]
    [Description("Read a file byte range (query, task-scoped). Authorized content is dropped from output (metadata-only).")]
    public static Task<string> ReadFileRange(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Request body as inline JSON matching the FileRangeReadRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ReadFileRangeAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), RequestBody.Read<FileRangeReadRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "search-folder-indexed-files")]
    [Description("Search the authorized Folders semantic search index (query, task-scoped). Metadata-only results.")]
    public static Task<string> SearchFolderIndexedFiles(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: eventually_consistent (the search index is async pub/sub-fed).")] string? freshness = null,
        [Description("Request body as inline JSON matching the ContextIndexSearchRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.SearchFolderIndexedFilesAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), RequestBody.Read<ContextIndexSearchRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-folder-indexing-status")]
    [Description("Inspect the metadata-only semantic-indexing status of a folder's file versions (query). Not task-scoped.")]
    public static Task<string> GetFolderIndexingStatus(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: eventually_consistent (the bridge projection is async pub/sub-fed).")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetFolderIndexingStatusAsync(folderId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
