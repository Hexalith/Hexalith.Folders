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

        if (request.TargetEvidence?.Metadata is null
            || request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key))
            || request.TargetEvidence.Metadata.Any(static pair => !IsSafeMetadataKey(pair.Key) || !IsSafeMetadataValue(pair.Value)))
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

        if (request.TargetEvidence?.Metadata is null
            || request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key))
            || request.TargetEvidence.Metadata.Any(static pair => !IsSafeMetadataKey(pair.Key) || !IsSafeMetadataValue(pair.Value)))
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

        if (request.TargetEvidence?.Metadata is null
            || request.TargetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key))
            || request.TargetEvidence.Metadata.Any(static pair => !IsSafeMetadataKey(pair.Key) || !IsSafeMetadataValue(pair.Value)))
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

    public static bool TryCreate(
        ProviderFileMutationRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            ProviderOperationCatalog.FileMutationSupport,
            [
                request.ManagedTenantId,
                request.OrganizationId,
                request.FolderId,
                request.DelegatedTaskId,
                request.ProviderBindingRef,
                request.RepositoryBindingId,
                request.AuthorizationEvidence.Fingerprint,
                request.LockEvidence.Fingerprint,
                request.RefPolicyEvidence.Fingerprint,
                request.FilePolicyEvidence.Fingerprint,
                request.SafeChangeSetFingerprint,
                request.IdempotencyKey,
                request.IdempotencyAdmission.IntentFingerprint,
            ],
            out safeTargetEvidence,
            out failureReason);
    }

    public static bool TryCreate(
        ProviderCommitRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            ProviderOperationCatalog.CommitSupport,
            [
                request.ManagedTenantId,
                request.OrganizationId,
                request.FolderId,
                request.DelegatedTaskId,
                request.ProviderBindingRef,
                request.RepositoryBindingId,
                request.AuthorizationEvidence.Fingerprint,
                request.LockEvidence.Fingerprint,
                request.RefPolicyEvidence.Fingerprint,
                request.SafeStagedChangeSetFingerprint,
                request.IdempotencyKey,
                request.IdempotencyAdmission.IntentFingerprint,
            ],
            out safeTargetEvidence,
            out failureReason);
    }

    public static bool TryCreate(
        ProviderOperationStatusRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            ProviderOperationCatalog.StatusQuery,
            [
                request.ManagedTenantId,
                request.OrganizationId,
                request.FolderId,
                request.DelegatedTaskId,
                request.ProviderBindingRef,
                request.RepositoryBindingId,
                request.AuthorizationEvidence.Fingerprint,
                request.LockEvidence.Fingerprint,
                request.RefPolicyEvidence.Fingerprint,
                request.OperationReference,
                request.SafeExpectedHeadFingerprint,
                request.SafeIntendedCommitFingerprint,
                request.CheckNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            out safeTargetEvidence,
            out failureReason);
    }

    private static bool TryCreateOperationTarget(
        ProviderTargetEvidence targetEvidence,
        ProviderCredentialMode credentialMode,
        string operationScope,
        IReadOnlyList<string> fingerprintFields,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        safeTargetEvidence = targetEvidence;
        failureReason = null;
        if (targetEvidence is null
            || targetEvidence.Metadata is null
            || !string.Equals(targetEvidence.Product, "github", StringComparison.Ordinal)
            || !string.Equals(targetEvidence.ProductVersion, "github-rest", StringComparison.Ordinal)
            || !string.Equals(
                targetEvidence.ApiSurfaceVersion,
                $"github-rest-{GitHubProviderConstants.RestApiVersion}",
                StringComparison.Ordinal)
            || targetEvidence.EvidenceVersion is not ("target-v1" or "github-target-evidence-v1")
            || !targetEvidence.Metadata.TryGetValue("operation_scope", out string? declaredScope)
            || !string.Equals(declaredScope, operationScope, StringComparison.Ordinal)
            || targetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_github_target_metadata";
            return false;
        }

        List<string?> fields = [.. fingerprintFields, credentialMode.ToString(), GitHubProviderConstants.RestApiVersion];
        foreach (KeyValuePair<string, string> pair in targetEvidence.Metadata.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsSafeMetadataKey(pair.Key) || !IsSafeMetadataValue(pair.Value))
            {
                failureReason = "unsafe_github_target_metadata";
                return false;
            }

            fields.Add(pair.Key);
            fields.Add(pair.Value);
        }

        string safeTargetFingerprint = GitHubProviderSafeOperationEvidence.Create([.. fields]);
        safeTargetEvidence = new ProviderTargetEvidence(
            "github",
            "github-rest",
            $"github-rest-{GitHubProviderConstants.RestApiVersion}",
            "github-target-evidence-v1",
            targetEvidence.IsStale,
            targetEvidence.ObservedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["safe_target_fingerprint"] = safeTargetFingerprint,
                ["target_fingerprint_version"] = "github-target-v1",
                ["operation_scope"] = operationScope,
                ["api_version"] = GitHubProviderConstants.RestApiVersion,
            });
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
        AppendField(hash, request.RepositoryProfileRef);
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
        => value is { Length: > 0 and <= 512 }
            && !string.IsNullOrWhiteSpace(value)
            && !value.Contains("://", StringComparison.Ordinal)
            && !value.Contains('@', StringComparison.Ordinal)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("diff --git", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeMetadataKey(string? value)
        => value is { Length: > 0 and <= 128 }
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static void AppendField(IncrementalHash hash, string? value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
