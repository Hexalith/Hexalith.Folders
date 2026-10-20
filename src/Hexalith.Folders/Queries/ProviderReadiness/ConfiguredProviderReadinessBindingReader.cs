using System.Collections.Frozen;
using Hexalith.Folders.Aggregates.Organization;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>Resolves private target context only after durable source identity and current-head validation.</summary>
internal sealed class ConfiguredProviderReadinessBindingReader(
    EventStoreProviderReadinessBindingReadModel source,
    IOptions<FoldersProviderBindingOptions> options) : IProviderReadinessBindingReader
{
    public async Task<OrganizationProviderBinding?> GetAsync(ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
    {
        OrganizationProviderBinding? binding = await source.GetAsync(request, cancellationToken).ConfigureAwait(false);
        if (binding is null) return null;
        if (!binding.NamingPolicy.Metadata.TryGetValue(ProviderBindingProfileFingerprint.ProfileRefKey, out string? profileRef))
            return binding;
        if (!binding.NamingPolicy.Metadata.TryGetValue(ProviderBindingProfileFingerprint.FingerprintKey, out string? fingerprint)
            || !options.Value.Profiles.TryGetValue(profileRef, out ProviderBindingProfile? configured))
            throw new ProviderBindingProfileDriftException();
        ProviderBindingProfile profile = new()
        {
            ManagedTenantId = configured.ManagedTenantId,
            ProviderKind = configured.ProviderKind,
            NamingPolicy = configured.NamingPolicy with
            { Metadata = configured.NamingPolicy.Metadata.ToFrozenDictionary(StringComparer.Ordinal) },
            BranchPolicy = configured.BranchPolicy with
            { Metadata = configured.BranchPolicy.Metadata.ToFrozenDictionary(StringComparer.Ordinal) },
        };
        if (profile.ManagedTenantId != binding.ManagedTenantId || profile.ProviderKind != binding.ProviderKind
            || profile.NamingPolicy.PolicyRef != binding.NamingPolicy.PolicyRef
            || profile.BranchPolicy.PolicyRef != binding.BranchPolicy.PolicyRef
            || ProviderBindingProfileFingerprint.Compute(profileRef, profile) != fingerprint)
            throw new ProviderBindingProfileDriftException();
        // This enriched context is never written to the source projection or returned by the public query.
        return binding with { NamingPolicy = profile.NamingPolicy, BranchPolicy = profile.BranchPolicy };
    }
}
