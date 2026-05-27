using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Tools;

/// <summary>
/// MCP tools for the provider-binding canonical operations, each wrapping the matching <see cref="IClient"/>
/// operation 1:1 (tool name = kebab-case of the operation id). The shared <see cref="ToolPipeline"/> owns all
/// sourcing and failure mapping so each tool body is a single typed SDK call.
/// </summary>
[McpServerToolType]
internal static class ProviderTools
{
    [McpServerTool(Name = "configure-provider-binding")]
    [Description("Configure a provider binding reference without credential material (mutating).")]
    public static Task<string> ConfigureProviderBinding(
        ToolPipeline pipeline,
        [Description("Opaque provider binding reference.")] string providerBindingRef,
        [Description("Caller-sourced idempotency key (required; never MCP-generated).")] string idempotencyKey,
        [Description("Caller-provided task ID (required; never MCP-generated).")] string taskId,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Request body as inline JSON matching the ConfigureProviderBindingRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteMutationAsync(idempotencyKey, taskId, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ConfigureProviderBindingAsync(providerBindingRef, s.IdempotencyKey, s.CorrelationId, s.TaskId, RequestBody.Read<ConfigureProviderBindingRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-provider-binding")]
    [Description("Inspect a redacted provider binding reference (query).")]
    public static Task<string> GetProviderBinding(
        ToolPipeline pipeline,
        [Description("Opaque provider binding reference.")] string providerBindingRef,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetProviderBindingAsync(providerBindingRef, s.CorrelationId, ToolInputs.ParseFreshness(freshness), ct)), cancellationToken);

    [McpServerTool(Name = "validate-provider-readiness")]
    [Description("Validate provider readiness through sanitized evidence (query).")]
    public static Task<string> ValidateProviderReadiness(
        ToolPipeline pipeline,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Request body as inline JSON matching the ValidateProviderReadinessRequest contract schema.")] string? requestJson = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.ValidateProviderReadinessAsync(s.CorrelationId, ToolInputs.ParseFreshness(freshness), RequestBody.Read<ValidateProviderReadinessRequest>(requestJson), ct)), cancellationToken);

    [McpServerTool(Name = "get-provider-support-evidence")]
    [Description("Inspect provider-neutral support and capability evidence (query).")]
    public static Task<string> GetProviderSupportEvidence(
        ToolPipeline pipeline,
        [Description("Optional caller-provided correlation ID; a fresh ULID is generated when omitted.")] string? correlationId = null,
        [Description("Optional read-consistency: snapshot_per_task | read_your_writes | eventually_consistent.")] string? freshness = null,
        [Description("Opaque pagination cursor echoed from a previous page.")] string? cursor = null,
        [Description("Maximum number of items to return.")] int? limit = null,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId, (client, s, ct) => ToolPipeline.AsObject(
            client.GetProviderSupportEvidenceAsync(s.CorrelationId, ToolInputs.ParseFreshness(freshness), cursor, limit, ct)), cancellationToken);
}
