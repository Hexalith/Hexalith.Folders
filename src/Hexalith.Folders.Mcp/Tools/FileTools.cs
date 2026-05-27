using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for file mutations in a locked workspace. <c>add-file</c>/<c>change-file</c> go through the
/// Story 5.1 upload convenience (<see cref="FoldersFileUploadExtensions.UploadFileAsync(IClient, FileUploadDescriptor, ReadOnlyMemory{byte}, string, string, string, CancellationToken)"/>),
/// which selects the inline transport internally and signals <see cref="FileUploadStreamingRequiredException"/>
/// for over-boundary content (mapped to the content-safe <c>input_limit_exceeded</c>). File content is
/// supplied inline as base64 and is never echoed to any output channel (metadata-only).
/// </summary>
[McpServerToolType]
internal static class FileTools
{
    [McpServerTool(Name = "add-file")]
    [Description("Add a file to a locked workspace (mutating, task-scoped). Content is supplied as base64 and never echoed.")]
    public static Task<string> AddFile(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Opaque per-file operation identifier.")] string operationId,
        [Description("Workspace-root-relative normalized path.")] string path,
        [Description("Human-readable file name.")] string displayName,
        [Description("RFC 6838 media type (type/subtype).")] string mediaType,
        [Description("Base64-encoded file content (inline upload; never echoed in output).")] string contentBase64,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional original media type when it differs from mediaType.")] string? contentMediaType = null,
        [Description("Path policy classification.")] string pathPolicyClass = "metadata_only",
        CancellationToken cancellationToken = default)
        => Upload(pipeline, FileMutationRequestFileOperationKind.Add, folderId, workspaceId, operationId, path, displayName, mediaType, contentBase64, idempotencyKey, taskId, correlationId, contentMediaType, pathPolicyClass, cancellationToken);

    [McpServerTool(Name = "change-file")]
    [Description("Change a file in a locked workspace (mutating, task-scoped). Content is supplied as base64 and never echoed.")]
    public static Task<string> ChangeFile(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Opaque per-file operation identifier.")] string operationId,
        [Description("Workspace-root-relative normalized path.")] string path,
        [Description("Human-readable file name.")] string displayName,
        [Description("RFC 6838 media type (type/subtype).")] string mediaType,
        [Description("Base64-encoded file content (inline upload; never echoed in output).")] string contentBase64,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional original media type when it differs from mediaType.")] string? contentMediaType = null,
        [Description("Path policy classification.")] string pathPolicyClass = "metadata_only",
        CancellationToken cancellationToken = default)
        => Upload(pipeline, FileMutationRequestFileOperationKind.Change, folderId, workspaceId, operationId, path, displayName, mediaType, contentBase64, idempotencyKey, taskId, correlationId, contentMediaType, pathPolicyClass, cancellationToken);

    [McpServerTool(Name = "remove-file")]
    [Description("Remove a file from a locked workspace (mutating, task-scoped). Supply the metadata-only removal body as JSON.")]
    public static Task<string> RemoveFile(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the FileMutationRequest (removal) contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.RemoveFileAsync(folderId, workspaceId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<FileMutationRequest>(requestJson), ct)), cancellationToken);

    private static Task<string> Upload(
        ToolPipeline pipeline,
        FileMutationRequestFileOperationKind kind,
        string folderId,
        string workspaceId,
        string operationId,
        string path,
        string displayName,
        string mediaType,
        string contentBase64,
        string idempotencyKey,
        string taskId,
        string? correlationId,
        string? contentMediaType,
        string pathPolicyClass,
        CancellationToken cancellationToken)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) =>
        {
            ReadOnlyMemory<byte> content = DecodeContent(contentBase64);
            FileUploadDescriptor descriptor = new()
            {
                FolderId = folderId,
                WorkspaceId = workspaceId,
                OperationId = operationId,
                MediaType = mediaType,
                ContentMediaType = contentMediaType,
                FileOperationKind = kind,
                PathMetadata = new PathMetadata
                {
                    NormalizedPath = path,
                    DisplayName = displayName,
                    PathPolicyClass = pathPolicyClass,
                    UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
                },
            };

            return ToolPipeline.AsObject(client.UploadFileAsync(descriptor, content, s.IdempotencyKey, s.CorrelationId, s.TaskId, ct));
        }, cancellationToken);

    private static ReadOnlyMemory<byte> DecodeContent(string contentBase64)
    {
        try
        {
            return Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            // Pre-SDK usage error: content was not valid base64. Never echo the content itself.
            throw new McpUsageException("The contentBase64 input is not valid base64.");
        }
    }
}
