using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoSafeTargetFingerprint
{
    private static readonly HashSet<string> UnsafeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "owner",
        "repository",
        "repo",
        "branch",
        "ref",
        "clone_url",
        "html_url",
        "email",
        "display_name",
        "raw_payload",
        "token",
        "access_token",
        "credential_label",
        "credential_reference_label",
    };

    public static bool TryCreate(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(canonicalBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotVersion);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (!TryValidateMetadata(request.TargetEvidence, out failureReason))
        {
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(
            request,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
            declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "forgejo-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "readiness",
            ["api_surface_version"] = ForgejoProviderConstants.ApiSurfaceVersion,
            ["snapshot_version"] = snapshotVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "forgejo",
            snapshotVersion,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "forgejo-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryCreate(
        ProviderRepositoryCreationRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(canonicalBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotVersion);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (!TryValidateMetadata(request.TargetEvidence, out failureReason))
        {
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(
            request,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
            declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "forgejo-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "repository_creation",
            ["api_surface_version"] = ForgejoProviderConstants.ApiSurfaceVersion,
            ["snapshot_version"] = snapshotVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "forgejo",
            snapshotVersion,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "forgejo-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryCreate(
        ProviderRepositoryBindingRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(canonicalBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotVersion);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (!TryValidateMetadata(request.TargetEvidence, out failureReason))
        {
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(
            request,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
            declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "forgejo-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "existing_repository_binding",
            ["api_surface_version"] = ForgejoProviderConstants.ApiSurfaceVersion,
            ["snapshot_version"] = snapshotVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "forgejo",
            snapshotVersion,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "forgejo-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryValidateMetadata(ProviderTargetEvidence targetEvidence, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(targetEvidence);
        failureReason = null;

        if (targetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_forgejo_target_metadata";
            return false;
        }

        return true;
    }

    private static string ComputeFingerprint(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        string? declaredFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, request.ProviderFamily);
        AppendField(hash, request.ProviderKey);
        AppendField(hash, ForgejoProviderConstants.ApiSurfaceVersion);
        AppendField(hash, snapshotVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
        AppendField(hash, CanonicalOrigin(canonicalBaseUri));
        AppendField(hash, declaredFingerprint);
        foreach (KeyValuePair<string, string> pair in request.TargetEvidence.Metadata.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            if (IsSafeMetadataValue(pair.Key) && IsSafeMetadataValue(pair.Value))
            {
                AppendField(hash, pair.Key);
                AppendField(hash, pair.Value);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeFingerprint(
        ProviderRepositoryCreationRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        string? declaredFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, request.RepositoryBindingId);
        AppendField(hash, request.ProviderFamily);
        AppendField(hash, request.ProviderKey);
        AppendField(hash, ForgejoProviderConstants.ApiSurfaceVersion);
        AppendField(hash, snapshotVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
        AppendField(hash, CanonicalOrigin(canonicalBaseUri));
        AppendField(hash, request.IdempotencyKey);
        AppendField(hash, declaredFingerprint);
        foreach (KeyValuePair<string, string> pair in request.TargetEvidence.Metadata.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            if (IsSafeMetadataValue(pair.Key) && IsSafeMetadataValue(pair.Value))
            {
                AppendField(hash, pair.Key);
                AppendField(hash, pair.Value);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeFingerprint(
        ProviderRepositoryBindingRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        string? declaredFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, request.RepositoryBindingId);
        AppendField(hash, request.ExternalRepositoryRefFingerprint);
        AppendField(hash, request.BranchRefPolicyRef);
        AppendField(hash, request.ProviderFamily);
        AppendField(hash, request.ProviderKey);
        AppendField(hash, ForgejoProviderConstants.ApiSurfaceVersion);
        AppendField(hash, snapshotVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
        AppendField(hash, CanonicalOrigin(canonicalBaseUri));
        AppendField(hash, request.IdempotencyKey);
        AppendField(hash, declaredFingerprint);
        foreach (KeyValuePair<string, string> pair in request.TargetEvidence.Metadata.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            if (IsSafeMetadataValue(pair.Key) && IsSafeMetadataValue(pair.Value))
            {
                AppendField(hash, pair.Key);
                AppendField(hash, pair.Value);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string CanonicalOrigin(Uri uri)
        => $"{uri.Scheme.ToLowerInvariant()}://{uri.IdnHost.ToLowerInvariant()}:{uri.Port}";

    private static bool IsSafeMetadataValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Contains('@', StringComparison.Ordinal)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("diff --git", StringComparison.OrdinalIgnoreCase);

    private static void AppendField(IncrementalHash hash, string? value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
