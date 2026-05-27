using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the audit-trail and operation-timeline read views. All are metadata-only queries wrapping
/// the matching <see cref="IClient"/> operation 1:1.
/// </summary>
[McpServerToolType]
internal static class AuditTools
{
    [McpServerTool(Name = "list-audit-trail")]
    [Description("List metadata-only audit-trail records for a folder (query).")]
    public static Task<string> ListAuditTrail(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Opaque pagination cursor echoed from a previous page.")] string? cursor = null,
        [Description("Maximum number of items to return.")] int? limit = null,
        [Description("Metadata-only filter expression.")] string? filter = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ListAuditTrailAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), cursor, limit, filter, ct)), cancellationToken);

    [McpServerTool(Name = "get-audit-record")]
    [Description("Inspect a specific metadata-only audit record (query).")]
    public static Task<string> GetAuditRecord(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque audit record identifier.")] string auditRecordId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetAuditRecordAsync(folderId, auditRecordId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "list-operation-timeline")]
    [Description("List the metadata-only operation timeline for a folder (query).")]
    public static Task<string> ListOperationTimeline(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Opaque pagination cursor echoed from a previous page.")] string? cursor = null,
        [Description("Maximum number of items to return.")] int? limit = null,
        [Description("Metadata-only filter expression.")] string? filter = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ListOperationTimelineAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), cursor, limit, filter, ct)), cancellationToken);

    [McpServerTool(Name = "get-operation-timeline-entry")]
    [Description("Inspect a specific metadata-only operation timeline entry (query).")]
    public static Task<string> GetOperationTimelineEntry(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque timeline entry identifier.")] string timelineEntryId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetOperationTimelineEntryAsync(folderId, timelineEntryId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
