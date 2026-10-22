using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Qualifies a path against immutable current policy and the SDK's authoritative virtual namespace.</summary>
internal sealed class WorkspaceContentPathEvidenceProvider(IAuthoritativeContentStore store,
    IWorkspaceFilePolicySource policies, IHttpContextAccessor http, IWorkspaceFileSensitivityClassifier sensitivities) : IWorkspacePathPolicyEvidenceProvider
{
    public async Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(WorkspacePathPolicyEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = policies.Capture();
            var path = request.PathMetadata;
            if (path is null || policy.Classify(path) is "excluded" or "restricted")
                return new(WorkspacePathPolicyEvidenceDecision.SymlinkEscape);
            if (policy.ProtectionMode != "non_confidential") return new(WorkspacePathPolicyEvidenceDecision.Unavailable);
            var sensitivity = await sensitivities.ClassifyAsync(path, cancellationToken).ConfigureAwait(false);
            if (sensitivity.Decision == WorkspaceFileSensitivityDecision.Redacted) return new(WorkspacePathPolicyEvidenceDecision.SymlinkEscape);
            if (sensitivity.Decision != WorkspaceFileSensitivityDecision.Allowed || !policies.IsCurrent(policy))
                return new(WorkspacePathPolicyEvidenceDecision.Unavailable);
            var bindings = await store.ListAsync(http.HttpContext!.User,
                new(request.ManagedTenantId, "folders", request.FolderId, request.WorkspaceId), cancellationToken).ConfigureAwait(false);
            foreach (var binding in bindings)
            {
                var other = WorkspaceContentAuthority.MetadataPath(binding.Change.ResourceMetadata ?? new Dictionary<string, string>());
                string[] requested = path.NormalizedPath.Split('/'), existing = other.NormalizedPath.Split('/');
                for (int index = 0; index < Math.Min(requested.Length, existing.Length); index++)
                {
                    if (!requested[index].Equals(existing[index], StringComparison.OrdinalIgnoreCase)) break;
                    if (requested[index] != existing[index]) return new(WorkspacePathPolicyEvidenceDecision.SymlinkEscape);
                }
                // This namespace admits regular files only; it has no links, mounts, reparse entries or filesystem aliases.
                if (path.NormalizedPath.StartsWith(other.NormalizedPath + "/", StringComparison.OrdinalIgnoreCase)
                    || other.NormalizedPath.StartsWith(path.NormalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                    return new(WorkspacePathPolicyEvidenceDecision.SymlinkEscape);
            }
            return policies.IsCurrent(policy)
                ? new(WorkspacePathPolicyEvidenceDecision.NoEscape) { PolicyVersion = policy.Version, PolicyDigest = policy.Digest }
                : new(WorkspacePathPolicyEvidenceDecision.Unavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(WorkspacePathPolicyEvidenceDecision.Unavailable); }
    }
}
