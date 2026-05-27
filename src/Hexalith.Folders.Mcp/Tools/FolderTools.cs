using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the folder lifecycle, repository binding, ACL, effective-permission, and branch-ref-policy
/// canonical operations. Each tool wraps the matching <see cref="IClient"/> operation 1:1 (tool name =
/// kebab-case of the operation id); the shared <see cref="ToolPipeline"/> owns sourcing and failure mapping.
/// </summary>
[McpServerToolType]
internal static class FolderTools
{
    [McpServerTool(Name = "create-folder")]
    [Description("Create a tenant-scoped folder identity (mutating).")]
    public static Task<string> CreateFolder(
        ToolPipeline pipeline,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the CreateFolderRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.CreateFolderAsync(s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<CreateFolderRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "create-repository-backed-folder")]
    [Description("Request repository creation for a new folder where provider capabilities permit it (mutating).")]
    public static Task<string> CreateRepositoryBackedFolder(
        ToolPipeline pipeline,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the CreateRepositoryBackedFolderRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.CreateRepositoryBackedFolderAsync(s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<CreateRepositoryBackedFolderRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "bind-repository")]
    [Description("Bind an existing repository reference to a folder where supported (mutating).")]
    public static Task<string> BindRepository(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the BindRepositoryRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.BindRepositoryAsync(folderId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<BindRepositoryRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-repository-binding")]
    [Description("Inspect repository binding metadata without exposing protected repository existence (query).")]
    public static Task<string> GetRepositoryBinding(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque repository binding identifier.")] string repositoryBindingId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetRepositoryBindingAsync(folderId, repositoryBindingId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "get-folder-lifecycle-status")]
    [Description("Inspect folder lifecycle and repository binding status (query).")]
    public static Task<string> GetFolderLifecycleStatus(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetFolderLifecycleStatusAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "archive-folder")]
    [Description("Archive a folder without exposing cross-tenant existence (mutating).")]
    public static Task<string> ArchiveFolder(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the ArchiveFolderRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ArchiveFolderAsync(folderId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<ArchiveFolderRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "list-folder-acl-entries")]
    [Description("List metadata-only folder ACL entries (query).")]
    public static Task<string> ListFolderAclEntries(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Opaque pagination cursor echoed from a previous page.")] string? cursor = null,
        [Description("Maximum number of items to return.")] int? limit = null,
        [Description("Metadata-only filter expression.")] string? filter = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ListFolderAclEntriesAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), cursor, limit, filter, ct)), cancellationToken);

    [McpServerTool(Name = "update-folder-acl-entry")]
    [Description("Add, update, or revoke a metadata-only folder ACL entry (mutating).")]
    public static Task<string> UpdateFolderAclEntry(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Opaque ACL entry identifier.")] string aclEntryId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the UpdateFolderAclEntryRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.UpdateFolderAclEntryAsync(folderId, aclEntryId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<UpdateFolderAclEntryRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-effective-permissions")]
    [Description("Inspect caller effective permissions without exposing tenant authority fields (query).")]
    public static Task<string> GetEffectivePermissions(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetEffectivePermissionsAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "configure-branch-ref-policy")]
    [Description("Configure tenant-scoped branch and ref policy metadata (mutating).")]
    public static Task<string> ConfigureBranchRefPolicy(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the BranchRefPolicyRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ConfigureBranchRefPolicyAsync(folderId, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<BranchRefPolicyRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-branch-ref-policy")]
    [Description("Inspect branch and ref policy metadata (query).")]
    public static Task<string> GetBranchRefPolicy(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetBranchRefPolicyAsync(folderId, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);
}
