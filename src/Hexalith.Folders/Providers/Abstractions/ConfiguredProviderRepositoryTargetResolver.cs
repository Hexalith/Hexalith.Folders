using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>Resolves an existing repository only from current tenant-admin configured binding policy.</summary>
internal sealed class ConfiguredProviderRepositoryTargetResolver(IProviderReadinessBindingReader reader)
    : IProviderRepositoryTargetResolver
{
    /// <inheritdoc/>
    public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
        ProviderRepositoryCreationTargetResolutionRequest request, CancellationToken cancellationToken = default)
        => new UnconfiguredProviderRepositoryTargetResolver().ResolveCreationAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
        ProviderRepositoryBindingTargetResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AuthorizationEvidenceFingerprint)) return Missing();
        OrganizationProviderBinding? binding;
        try
        {
            binding = await reader.GetAsync(new(request.ManagedTenantId, request.ProviderBindingRef,
                request.CorrelationId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return ProviderRepositoryTargetResolutionResult.Failure(ProviderFailureCategory.ProviderUnavailable,
                "provider_binding_authority_unavailable");
        }
        if (binding is null || binding.ManagedTenantId != request.ManagedTenantId
            || binding.OrganizationId != request.OrganizationId || binding.ProviderBindingRef != request.ProviderBindingRef
            || binding.ConfiguredStatus != "configured"
            || binding.BranchPolicy.PolicyRef != request.BranchRefPolicyRef
            || Value(binding.NamingPolicy, "external_repository_ref") != request.ExternalRepositoryRef
            || string.IsNullOrWhiteSpace(request.AuthorizationEvidenceFingerprint))
            return Missing();
        string? owner = Value(binding.NamingPolicy, "repository_owner");
        string? name = Value(binding.NamingPolicy, "repository_name");
        string? defaultBranch = Value(binding.BranchPolicy, "default_branch");
        string? selected = Value(binding.BranchPolicy, "selected_ref");
        string? visibility = Value(binding.NamingPolicy, "repository_visibility");
        string? protection = Value(binding.BranchPolicy, "require_protected_ref");
        if (owner is null || name is null || defaultBranch is null || selected is null
            || visibility is not ("private" or "public") || protection is not ("true" or "false")) return Missing();
        ProviderRepositoryResolvedTarget target = new(owner, name,
            visibility == "private" ? ProviderRepositoryVisibility.Private : ProviderRepositoryVisibility.Public,
            defaultBranch, selected, RequireProtectedRef: protection == "true", RequireContentsPermission: true,
            RequireAdministrationPermission: false, ExpectedCanonicalRepositoryId: null,
            EquivalentExistingAuthorized: false);
        return target.TryValidate(out _) ? ProviderRepositoryTargetResolutionResult.Success(target) : Missing();
    }

    private static string? Value(OrganizationProviderBindingPolicy policy, string key)
        => policy.Metadata.TryGetValue(key, out string? value) ? value : null;
    private static ProviderRepositoryTargetResolutionResult Missing()
        => ProviderRepositoryTargetResolutionResult.Failure(ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_repository_binding_target_unconfigured");
}
