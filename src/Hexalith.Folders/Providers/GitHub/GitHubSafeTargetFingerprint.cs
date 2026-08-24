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

    public static bool TryCreate(
        ProviderFileChangeSetRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
        => TryCreateOperationEvidence(
            request.TargetEvidence,
            ComputeFingerprint(request, credentialMode),
            "file_mutation",
            out safeTargetEvidence,
            out failureReason);

    public static bool TryCreate(
        ProviderCommitRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
        => TryCreateOperationEvidence(
            request.TargetEvidence,
            ComputeFingerprint(request, credentialMode),
            "commit",
            out safeTargetEvidence,
            out failureReason);

    public static bool TryCreate(
        ProviderMutationStatusRequest request,
        ProviderCredentialMode credentialMode,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
        => TryCreateOperationEvidence(
            request.TargetEvidence,
            ComputeFingerprint(request, credentialMode),
            "status",
            out safeTargetEvidence,
            out failureReason);

    public static string ComputeProviderObjectFingerprint(
        string repositoryBindingFingerprint,
        string operationReference,
        string providerObjectSha)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "github-provider-object-v2");
        AppendField(hash, repositoryBindingFingerprint);
        AppendField(hash, operationReference);
        AppendField(hash, providerObjectSha);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string ComputeExpectedHeadFingerprint(
        string safeTargetFingerprint,
        string operationReference,
        string providerObjectSha)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "github-expected-head-v1");
        AppendField(hash, safeTargetFingerprint);
        AppendField(hash, operationReference);
        AppendField(hash, providerObjectSha);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string ComputeReconciliationReference(
        string safeTargetFingerprint,
        string operationReference,
        string intentFingerprint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "github-reconciliation-v1");
        AppendField(hash, safeTargetFingerprint);
        AppendField(hash, operationReference);
        AppendField(hash, intentFingerprint);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool TryCreateOperationEvidence(
        ProviderTargetEvidence targetEvidence,
        string safeTargetFingerprint,
        string defaultOperationScope,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        safeTargetEvidence = targetEvidence;
        failureReason = null;
        if (targetEvidence is null
            || !string.Equals(targetEvidence.Product, "github", StringComparison.Ordinal)
            || !string.Equals(targetEvidence.ProductVersion, "github-rest", StringComparison.Ordinal)
            || !string.Equals(
                targetEvidence.ApiSurfaceVersion,
                $"github-rest-{GitHubProviderConstants.RestApiVersion}",
                StringComparison.Ordinal)
            || !string.Equals(targetEvidence.EvidenceVersion, "github-target-evidence-v1", StringComparison.Ordinal)
            || targetEvidence.Metadata is null
            || !targetEvidence.Metadata.TryGetValue("api_version", out string? apiVersion)
            || !string.Equals(apiVersion, GitHubProviderConstants.RestApiVersion, StringComparison.Ordinal)
            || !targetEvidence.Metadata.TryGetValue("operation_scope", out string? scope)
            || !string.Equals(scope, defaultOperationScope, StringComparison.Ordinal)
            || targetEvidence.Metadata.Any(static pair => !IsSafeMetadataValue(pair.Key) || !IsSafeMetadataValue(pair.Value))
            || targetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "github_target_evidence_profile_invalid";
            return false;
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["safe_target_fingerprint"] = safeTargetFingerprint,
            ["target_fingerprint_version"] = "github-target-v1",
            ["operation_scope"] = defaultOperationScope,
            ["api_version"] = GitHubProviderConstants.RestApiVersion,
        };
        safeTargetEvidence = new ProviderTargetEvidence(
            "github",
            "github-rest",
            $"github-rest-{GitHubProviderConstants.RestApiVersion}",
            "github-target-evidence-v1",
            targetEvidence.IsStale,
            targetEvidence.ObservedAt,
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

    private static string ComputeFingerprint(
        ProviderFileChangeSetRequest request,
        ProviderCredentialMode credentialMode)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendOperationFields(
            hash,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.ProviderFamily,
            request.ProviderKey,
            credentialMode,
            request.AuthorizationEvidence,
            request.OperationEvidence);
        AppendField(hash, request.IdempotencyKey);
        AppendField(hash, request.IdempotencyAdmission.IntentFingerprint);
        AppendField(hash, request.ChangeSetReference);
        foreach (ProviderFileChange change in request.Changes)
        {
            AppendField(hash, change.OperationReference);
            AppendField(hash, change.PathReference);
            AppendField(hash, change.Kind.ToString());
            AppendField(hash, change.ContentReference);
            AppendField(hash, change.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendField(hash, change.MediaType);
        }

        AppendSafeTargetMetadata(hash, request.TargetEvidence);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeFingerprint(
        ProviderCommitRequest request,
        ProviderCredentialMode credentialMode)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendOperationFields(
            hash,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.ProviderFamily,
            request.ProviderKey,
            credentialMode,
            request.AuthorizationEvidence,
            request.OperationEvidence);
        AppendField(hash, request.IdempotencyKey);
        AppendField(hash, request.IdempotencyAdmission.IntentFingerprint);
        AppendField(hash, request.StagedChangeSetReference);
        AppendField(hash, request.CommitMessageReference);
        AppendSafeTargetMetadata(hash, request.TargetEvidence);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeFingerprint(
        ProviderMutationStatusRequest request,
        ProviderCredentialMode credentialMode)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendOperationFields(
            hash,
            request.ManagedTenantId,
            request.FolderId,
            request.OrganizationId,
            request.CredentialReferenceId,
            request.ProviderBindingRef,
            request.RepositoryBindingId,
            request.ProviderFamily,
            request.ProviderKey,
            credentialMode,
            request.AuthorizationEvidence,
            request.OperationEvidence);
        AppendField(hash, request.OperationReference);
        AppendField(hash, request.ReconciliationReference);
        AppendSafeTargetMetadata(hash, request.TargetEvidence);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendOperationFields(
        IncrementalHash hash,
        string managedTenantId,
        string folderId,
        string organizationId,
        string credentialReferenceId,
        string providerBindingRef,
        string repositoryBindingId,
        string providerFamily,
        string providerKey,
        ProviderCredentialMode credentialMode,
        ProviderAuthorizationEvidenceSnapshot authorizationEvidence,
        ProviderOperationEvidenceSnapshot operationEvidence)
    {
        AppendField(hash, managedTenantId);
        AppendField(hash, folderId);
        AppendField(hash, organizationId);
        AppendField(hash, credentialReferenceId);
        AppendField(hash, providerBindingRef);
        AppendField(hash, repositoryBindingId);
        AppendField(hash, providerFamily);
        AppendField(hash, providerKey);
        AppendField(hash, GitHubProviderConstants.RestApiVersion);
        AppendField(hash, credentialMode.ToString());
        AppendField(hash, authorizationEvidence.Fingerprint);
        AppendField(hash, authorizationEvidence.FreshnessClass);
        AppendField(hash, operationEvidence.AuthorizedManagedTenantId);
        AppendField(hash, operationEvidence.AuthorizedFolderId);
        AppendField(hash, operationEvidence.AuthorizedOrganizationId);
        AppendField(hash, operationEvidence.AuthorizedCredentialReferenceId);
        AppendField(hash, operationEvidence.AuthorizedRepositoryBindingId);
        AppendField(hash, operationEvidence.DelegatedTaskFingerprint);
        AppendField(hash, operationEvidence.RepositoryBindingFingerprint);
        AppendField(hash, operationEvidence.RefPolicyFingerprint);
        AppendField(hash, operationEvidence.CanonicalLockFingerprint);
        AppendField(hash, operationEvidence.FreshnessClass);
    }

    private static void AppendSafeTargetMetadata(IncrementalHash hash, ProviderTargetEvidence targetEvidence)
    {
        foreach (KeyValuePair<string, string> pair in targetEvidence.Metadata.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            if (IsSafeMetadataValue(pair.Key) && IsSafeMetadataValue(pair.Value))
            {
                AppendField(hash, pair.Key);
                AppendField(hash, pair.Value);
            }
        }
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
