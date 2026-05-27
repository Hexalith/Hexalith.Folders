using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the commit canonical operations: the task-scoped commit mutation plus the commit-evidence,
/// provider-outcome, reconciliation-status, and task-status read views. Unknown/reconciliation outcomes are
/// surfaced truthfully (the kinds <c>unknown_provider_outcome</c>/<c>reconciliation_required</c> are never
/// papered over or retry-looped).
/// </summary>
[McpServerToolType]
internal static class CommitTools
{
    [McpServerTool(Name = "commit-workspace")]
    [Description("Commit a locked workspace (mutating, task-scoped).")]
    public static Task<string> CommitWorkspace(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the CommitWorkspaceRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.CommitWorkspaceAsync(folderId, workspaceId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<CommitWorkspaceRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-commit-evidence")]
    [Description("Inspect commit evidence for an operation (query).")]
    public static Task<string> GetCommitEvidence(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Opaque per-file operation identifier.")] string operationId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetCommitEvidenceAsync(folderId, workspaceId, operationId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-provider-outcome")]
    [Description("Inspect the provider outcome for an operation (query). Unknown outcomes are surfaced truthfully.")]
    public static Task<string> GetProviderOutcome(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Opaque per-file operation identifier.")] string operationId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetProviderOutcomeAsync(folderId, workspaceId, operationId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-reconciliation-status")]
    [Description("Inspect reconciliation status (query). Reconciliation-required outcomes are surfaced truthfully.")]
    public static Task<string> GetReconciliationStatus(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Opaque reconciliation identifier.")] string reconciliationId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetReconciliationStatusAsync(folderId, workspaceId, reconciliationId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-task-status")]
    [Description("Inspect the status of a task by its identifier (query).")]
    public static Task<string> GetTaskStatus(
        ToolPipeline pipeline,
        [Description("Opaque identifier of the task to inspect (resource path identifier).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetTaskStatusAsync(taskId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
