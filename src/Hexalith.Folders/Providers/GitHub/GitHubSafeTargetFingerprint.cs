using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubSafeTargetFingerprint
{
    private static readonly HashSet<string> UnsafeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "owner",
        "repository",
        "repo",
        "branch",
        "ref",
        "installation",
        "installation_id",
        "clone_url",
        "html_url",
        "email",
        "display_name",
        "raw_payload",
    };

    public static bool TryCreate(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_github_target_metadata";
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(request, credentialMode, declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "github-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "readiness",
            ["api_version"] = GitHubProviderConstants.RestApiVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "github",
            "github-rest",
            $"github-rest-{GitHubProviderConstants.RestApiVersion}",
            "github-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryCreate(
        ProviderRepositoryCreationRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_github_target_metadata";
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(request, credentialMode, declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "github-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "repository_creation",
            ["api_version"] = GitHubProviderConstants.RestApiVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "github",
            "github-rest",
            $"github-rest-{GitHubProviderConstants.RestApiVersion}",
            "github-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryCreate(
        ProviderRepositoryBindingRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);

        safeTargetEvidence = request.TargetEvidence;
        failureReason = null;

        if (request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_github_target_metadata";
            return false;
        }

        string? declaredFingerprint = request.TargetEvidence.Metadata.TryGetValue("safe_target_fingerprint", out string? candidate)
            && IsSafeMetadataValue(candidate)
                ? candidate
                : null;

        string safeTargetFingerprint = ComputeFingerprint(request, credentialMode, declaredFingerprint);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "github-target-v1",
            ["operation_scope"] = request.TargetEvidence.Metadata.TryGetValue("operation_scope", out string? scope) && IsSafeMetadataValue(scope)
                ? scope
                : "existing_repository_binding",
            ["api_version"] = GitHubProviderConstants.RestApiVersion,
        };

        safeTargetEvidence = new ProviderTargetEvidence(
            "github",
            "github-rest",
            $"github-rest-{GitHubProviderConstants.RestApiVersion}",
            "github-target-evidence-v1",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    private static string ComputeFingerprint(
        ProviderCapabilityDiscoveryRequest request,
        ProviderCredentialMode credentialMode,
        string? declaredFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, request.ProviderFamily);
        AppendField(hash, request.ProviderKey);
        AppendField(hash, GitHubProviderConstants.RestApiVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
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
        string? declaredFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, request.RepositoryBindingId);
        AppendField(hash, request.ProviderFamily);
        AppendField(hash, request.ProviderKey);
        AppendField(hash, GitHubProviderConstants.RestApiVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
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
        AppendField(hash, GitHubProviderConstants.RestApiVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendField(hash, request.AuthorizationEvidence.FreshnessClass);
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
