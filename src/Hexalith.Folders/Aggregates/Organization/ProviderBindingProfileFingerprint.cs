using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>Binds retained opaque policy references to the exact private host configuration.</summary>
internal static class ProviderBindingProfileFingerprint
{
    internal const string ProfileRefKey = "host_profile_ref";
    internal const string FingerprintKey = "host_profile_fingerprint";

    internal static string Compute(string profileRef, ProviderBindingProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileRef);
        ArgumentNullException.ThrowIfNull(profile);
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Schema = "private-provider-profile-v1",
            ProfileRef = profileRef,
            profile.ManagedTenantId,
            profile.ProviderKind,
            NamingPolicyRef = profile.NamingPolicy.PolicyRef,
            NamingMetadata = profile.NamingPolicy.Metadata.OrderBy(static item => item.Key, StringComparer.Ordinal).ToArray(),
            BranchPolicyRef = profile.BranchPolicy.PolicyRef,
            BranchMetadata = profile.BranchPolicy.Metadata.OrderBy(static item => item.Key, StringComparer.Ordinal).ToArray(),
        })));
    }
}
