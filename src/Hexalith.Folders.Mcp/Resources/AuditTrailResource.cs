using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Tooling;

using ModelContextProtocol.Server;

namespace Hexalith.Folders.Mcp.Resources;

/// <summary>
/// The read-only <c>audit-trail</c> MCP resource: a metadata-only view over the canonical
/// <see cref="IClient.ListAuditTrailAsync(string, string, ReadConsistencyClass?, string, int?, string, CancellationToken)"/>
/// query for a folder. It introduces no new query semantics — it reuses the same shared
/// <see cref="ToolPipeline"/> as the tools. The companion operation-timeline view is reachable via the
/// <c>list-operation-timeline</c> tool.
/// </summary>
/// <remarks>
/// Uses the ModelContextProtocol 1.3.0 attribute resource surface (verified against the pinned package).
/// Audit-trail listing is not task-scoped, so the URI template carries only the folder identifier.
/// </remarks>
[McpServerResourceType]
internal static class AuditTrailResource
{
    [McpServerResource(
        Name = "audit-trail",
        UriTemplate = "folders://audit-trail/{folderId}",
        MimeType = "application/json")]
    [Description("Read-only metadata-only audit trail for a folder, over the ListAuditTrail query.")]
    public static Task<string> Read(
        ToolPipeline pipeline,
        [Description("Opaque folder identifier.")] string folderId,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteQueryAsync(taskId: null, taskIdRequired: false, correlationId: null, (client, s, ct) => ToolPipeline.AsObject(
            client.ListAuditTrailAsync(folderId, s.CorrelationId, x_Hexalith_Freshness: null, cursor: null, limit: null, filter: null, ct)), cancellationToken);
}
