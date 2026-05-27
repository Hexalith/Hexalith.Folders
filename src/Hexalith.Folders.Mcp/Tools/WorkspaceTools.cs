using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the task-scoped workspace lifecycle (prepare, lock, release) and its read views. The three
/// mutations and the task-scoped queries require <c>taskId</c> (their <see cref="IClient"/> signatures carry
/// <c>x_Hexalith_Task_Id</c>); <c>get-workspace-lock</c> and <c>get-workspace-status</c> do not.
/// </summary>
[McpServerToolType]
internal static class WorkspaceTools
{
    [McpServerTool(Name = "prepare-workspace")]
    [Description("Accept workspace preparation for a repository-backed folder task (mutating, task-scoped).")]
    public static Task<string> PrepareWorkspace(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the PrepareWorkspaceRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.PrepareWorkspaceAsync(folderId, workspaceId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<PrepareWorkspaceRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "lock-workspace")]
    [Description("Accept task-scoped workspace lock acquisition (mutating, task-scoped).")]
    public static Task<string> LockWorkspace(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the LockWorkspaceRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.LockWorkspaceAsync(folderId, workspaceId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<LockWorkspaceRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-workspace-lock")]
    [Description("Inspect metadata-only workspace lock state (query).")]
    public static Task<string> GetWorkspaceLock(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetWorkspaceLockAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "release-workspace-lock")]
    [Description("Release a task-scoped workspace lock (mutating, task-scoped).")]
    public static Task<string> ReleaseWorkspaceLock(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the ReleaseWorkspaceLockRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ReleaseWorkspaceLockAsync(folderId, workspaceId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<ReleaseWorkspaceLockRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-workspace-retry-eligibility")]
    [Description("Inspect workspace retry eligibility (query, task-scoped).")]
    public static Task<string> GetWorkspaceRetryEligibility(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetWorkspaceRetryEligibilityAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-workspace-transition-evidence")]
    [Description("Inspect workspace transition evidence (query, task-scoped).")]
    public static Task<string> GetWorkspaceTransitionEvidence(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetWorkspaceTransitionEvidenceAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-workspace-status")]
    [Description("Inspect workspace lifecycle status (query).")]
    public static Task<string> GetWorkspaceStatus(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetWorkspaceStatusAsync(folderId, workspaceId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-workspace-cleanup-status")]
    [Description("Inspect workspace cleanup status (query, task-scoped).")]
    public static Task<string> GetWorkspaceCleanupStatus(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque workspace identifier.")] string workspaceId,
        [Description("Caller-provided task ID (required for this task-scoped query).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId, taskIdRequired: true, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetWorkspaceCleanupStatusAsync(folderId, workspaceId, s.CorrelationId, s.TaskId!, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
