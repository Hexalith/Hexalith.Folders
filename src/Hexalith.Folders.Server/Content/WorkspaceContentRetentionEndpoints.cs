using Hexalith.EventStore.DomainService.Content;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.Folders.Server.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server.Content;

/// <summary>Maps the approved content-specific administrative transport, independently authorized by the SDK.</summary>
internal static class WorkspaceContentRetentionEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { AllowDuplicateProperties = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static bool Matches(HttpContext context)
    {
        string[] parts = (context.Request.Path.Value ?? string.Empty).Trim('/').Split('/');
        return HttpMethods.IsPost(context.Request.Method) && parts.Length == 8
            && parts[0] == "api" && parts[1] == "v1" && parts[2] == "folders"
            && parts[4] == "workspaces" && parts[6] == "content" && parts[7] == "disposition";
    }
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/folders/{folderId}/workspaces/{workspaceId}/content/disposition",
            async (string folderId, string workspaceId,
                HttpContext context, ITenantContextAccessor tenant, IAuthoritativeContentStore store, CancellationToken token) =>
            {
                string? correlation = FolderHttpHeaderReader.ReadHeader(context, "X-Correlation-Id");
                string? task = FolderHttpHeaderReader.ReadHeader(context, "X-Hexalith-Task-Id");
                WorkspaceContentDispositionRequest? request;
                try
                {
                    byte[] payload = await FoldersDomainServiceEndpoints.ReadBoundedContentAsync(context.Request.Body, 16384, token).ConfigureAwait(false);
                    request = JsonSerializer.Deserialize<WorkspaceContentDispositionRequest>(payload, Json);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception) when (exception is JsonException or WorkspaceContentPolicyException or IOException)
                { return FoldersDomainServiceEndpoints.ContentProblem("content_evidence_invalid", correlation, task); }
                if (tenant.AuthoritativeTenantId is null || !FolderCanonicalSegmentIdentifier.IsValid(folderId)
                    || !FolderCanonicalSegmentIdentifier.IsValid(workspaceId) || request?.ReceiptId is not { Length: 72 }
                    || !request.ReceiptId.StartsWith("content_", StringComparison.Ordinal))
                    return FoldersDomainServiceEndpoints.ContentProblem("content_evidence_invalid", correlation, task);
                var scope = new ContentScope(tenant.AuthoritativeTenantId, "folders", folderId, workspaceId);
                context.Items[WorkspaceContentAuthority.ActionItem] = "manage_folder_access";
                try
                {
                    bool erased = false;
                    switch (request.Operation)
                    {
                        case "hold": await store.SetHoldAsync(context.User, scope, request.ReceiptId, true, token).ConfigureAwait(false); break;
                        case "release": await store.SetHoldAsync(context.User, scope, request.ReceiptId, false, token).ConfigureAwait(false); break;
                        case "abandon": await store.AbandonUploadAsync(context.User, scope, request.ReceiptId, token).ConfigureAwait(false); break;
                        case "apply": erased = await store.ApplyDispositionAsync(context.User, scope, request.ReceiptId, token).ConfigureAwait(false); break;
                        default: return FoldersDomainServiceEndpoints.ContentProblem("content_evidence_invalid", correlation, task);
                    }
                    return Results.Ok(new { accepted = true, erased });
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (UnauthorizedAccessException) { return FoldersDomainServiceEndpoints.ContentProblem("resource_unavailable", correlation, task); }
                catch (ContentConflictException) { return FoldersDomainServiceEndpoints.ContentProblem("concurrency_conflict", correlation, task); }
                catch (Exception) { return FoldersDomainServiceEndpoints.ContentProblem("file_policy_unavailable", correlation, task); }
            }).RequireAuthorization();
    }
}
