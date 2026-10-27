#if CHATBOT_NATIVE_FOLDERS_OWNER_ALIAS
extern alias FoldersOwner;
using FoldersOwner::Hexalith.Folders.Server.Authentication;
#else
using Hexalith.Folders.Server.Authentication;
#endif

using System.Security.Claims;
using Hexalith.Folders.Authorization;
using Microsoft.AspNetCore.Http;
namespace Hexalith.Folders.NativeContent.Tests;
internal sealed class NativeContentAuthority(IHttpContextAccessor http) : ITenantContextAccessor, IEventStoreClaimTransformEvidenceAccessor,
    IFolderPermissionEvidenceProvider, IEventStoreAuthorizationValidator, IDaprPolicyEvidenceProvider
{
    internal readonly HashSet<string> Denied = new(StringComparer.Ordinal);
    internal Func<FolderPermissionEvidenceRequest, Task>? BeforePermission = null;
    internal List<string> ObservedIdentities { get; } = [];
    internal string Folder = "folder-a";
    public string? AuthoritativeTenantId => http.HttpContext?.User.FindFirstValue("eventstore:tenant");
    public string? PrincipalId => http.HttpContext?.User.FindFirstValue("sub");
    public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
    {
        lock (ObservedIdentities) ObservedIdentities.Add(http.HttpContext?.User.Identity?.AuthenticationType + ":" + PrincipalId);
        return EventStoreClaimTransformEvidence.Allowed(AuthoritativeTenantId, PrincipalId, Denied.Contains(actionToken) ? [] : [actionToken]);
    }
    public async Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (BeforePermission is { } before) await before(request).ConfigureAwait(true);
        return request.ManagedTenantId == "tenant-a" && request.OperationScope == Folder && !Denied.Contains(request.ActionToken)
            ? FolderPermissionEvidenceResult.Allowed("permission_watermark_v1", organizationId: "tenant-a")
            : FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, null);
    }
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(EventStoreAuthorizationValidationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(EventStoreAuthorizationValidationResult.Allowed(null));
    public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(DaprPolicyEvidenceRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(DaprPolicyEvidenceResult.Allowed("folders", "dapr_policy_v1"));
}
