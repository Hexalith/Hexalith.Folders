using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the diagnostic read views (readiness, provider status, sync status, lock, dirty-state,
/// failed-operation, projection freshness). All are metadata-only queries wrapping the matching
/// <see cref="IClient"/> operation 1:1.
/// </summary>
[McpServerToolType]
internal static class DiagnosticsTools
{
    [McpServerTool(Name = "get-readiness-diagnostics")]
    [Description("Inspect tenant/provider readiness diagnostics (query).")]
    public static Task<string> GetReadinessDiagnostics(
        ToolPipeline pipeline,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetReadinessDiagnosticsAsync(s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-provider-status-diagnostics")]
    [Description("Inspect provider status diagnostics for a folder (query).")]
    public static Task<string> GetProviderStatusDiagnostics(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetProviderStatusDiagnosticsAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-sync-status-diagnostics")]
    [Description("Inspect sync status diagnostics for a workspace (query).")]
    public static Task<string> GetSyncStatusDiagnostics(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetSyncStatusDiagnosticsAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-lock-diagnostics")]
    [Description("Inspect lock diagnostics for a workspace (query).")]
    public static Task<string> GetLockDiagnostics(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetLockDiagnosticsAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-dirty-state-diagnostics")]
    [Description("Inspect dirty-state diagnostics for a workspace (query).")]
    public static Task<string> GetDirtyStateDiagnostics(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetDirtyStateDiagnosticsAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-failed-operation-diagnostics")]
    [Description("Inspect failed-operation diagnostics for a workspace (query).")]
    public static Task<string> GetFailedOperationDiagnostics(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetFailedOperationDiagnosticsAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-projection-freshness")]
    [Description("Inspect projection freshness diagnostics (query).")]
    public static Task<string> GetProjectionFreshness(
        ToolPipeline pipeline,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetProjectionFreshnessAsync(s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
