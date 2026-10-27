using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public static partial class FoldersDomainServiceEndpoints
{
    internal static async Task<IResult> NativeAddFileAsync(string folderId, string workspaceId, HttpContext httpContext,
        IEventStoreGatewayClient gateway, ITenantContextAccessor tenantContext, TimeProvider timeProvider,
        WorkspaceOperationEvidence evidence, CancellationToken cancellationToken)
    {
        folderId = WorkspaceNativeAddress.Resolve(folderId, "folder"); workspaceId = WorkspaceNativeAddress.Resolve(workspaceId, "workspace");
        string? task = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Task-Id");
        if (!FolderCanonicalSegmentIdentifier.IsValid(folderId) || !FolderCanonicalSegmentIdentifier.IsValid(workspaceId)
            || !FolderCanonicalSegmentIdentifier.IsValid(task) || tenantContext.AuthoritativeTenantId is null)
            return ContentProblem("content_evidence_invalid", null, null);
        try
        {
            await evidence.AuthorizeAsync(new(tenantContext.AuthoritativeTenantId, "folders", folderId, workspaceId), task!, cancellationToken).ConfigureAwait(false);
            httpContext.Items["Folders:NativeRequest"] = true;
            return await FileMutationAsync(folderId, workspaceId, "add", httpContext, gateway, tenantContext, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException) { return ContentProblem("resource_unavailable", null, task); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return ContentProblem("file_policy_unavailable", null, task); }
    }
}
