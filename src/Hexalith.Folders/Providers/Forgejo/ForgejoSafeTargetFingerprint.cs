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
            ["target_fingerprint_version"] = "forgejo-target-v2",
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
            "forgejo-target-evidence-v2",
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
            ["target_fingerprint_version"] = "forgejo-target-v2",
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
            "forgejo-target-evidence-v2",
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
            ["target_fingerprint_version"] = "forgejo-target-v2",
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
            "forgejo-target-evidence-v2",
            request.TargetEvidence.IsStale,
            request.TargetEvidence.ObservedAt,
            metadata);

        return true;
    }

    public static bool TryCreate(
        ProviderFileMutationRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
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
                request.SafeResolvedTargetFingerprint,
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
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
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
                request.SafeResolvedTargetFingerprint,
                request.SafeStagedChangeSetFingerprint,
                request.SafeCommitMessageFingerprint,
                request.SafeExpectedHeadFingerprint,
                request.IdempotencyKey,
                request.IdempotencyAdmission.IntentFingerprint,
            ],
            out safeTargetEvidence,
            out failureReason);
    }

    public static bool TryCreate(
        ProviderOperationStatusRequest request,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TryCreateOperationTarget(
            request.TargetEvidence,
            credentialMode,
            canonicalBaseUri,
            snapshotVersion,
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
                request.SafeResolvedTargetFingerprint,
                request.SafeFullRefFingerprint,
                request.SafeExpectedHeadFingerprint,
                request.SafeIntendedCommitFingerprint,
                request.SafeCheckWindowFingerprint,
            ],
            out safeTargetEvidence,
            out failureReason);
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

    private static bool TryCreateOperationTarget(
        ProviderTargetEvidence targetEvidence,
        ProviderCredentialMode credentialMode,
        Uri canonicalBaseUri,
        string snapshotVersion,
        string operationScope,
        IReadOnlyList<string> fingerprintFields,
        out ProviderTargetEvidence safeTargetEvidence,
        out string? failureReason)
    {
        safeTargetEvidence = targetEvidence;
        failureReason = null;
        if (targetEvidence is null
            || targetEvidence.Metadata is null
            || !string.Equals(targetEvidence.Product, "forgejo", StringComparison.Ordinal)
            || !string.Equals(targetEvidence.ProductVersion, snapshotVersion, StringComparison.Ordinal)
            || !string.Equals(targetEvidence.ApiSurfaceVersion, ForgejoProviderConstants.ApiSurfaceVersion, StringComparison.Ordinal)
            || !targetEvidence.Metadata.TryGetValue("operation_scope", out string? declaredScope)
            || !string.Equals(declaredScope, operationScope, StringComparison.Ordinal)
            || targetEvidence.Metadata.Keys.Any(static key => UnsafeKeys.Contains(key)))
        {
            failureReason = "unsafe_forgejo_target_metadata";
            return false;
        }

        List<string?> fields =
        [
            .. fingerprintFields,
            credentialMode.ToString(),
            ForgejoProviderConstants.ApiSurfaceVersion,
            snapshotVersion,
            CanonicalOrigin(canonicalBaseUri),
        ];
        foreach (KeyValuePair<string, string> pair in targetEvidence.Metadata.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsSafeMetadataKey(pair.Key)
                || (!string.Equals(pair.Key, "authorized_base_url", StringComparison.Ordinal)
                    && !IsSafeMetadataValue(pair.Value)))
            {
                failureReason = "unsafe_forgejo_target_metadata";
                return false;
            }

            if (!string.Equals(pair.Key, "authorized_base_url", StringComparison.Ordinal))
            {
                fields.Add(pair.Key);
                fields.Add(pair.Value);
            }
        }

        string safeTargetFingerprint = ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:operation-target",
            [.. fields]);
        safeTargetEvidence = new ProviderTargetEvidence(
            "forgejo",
            snapshotVersion,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "forgejo-target-evidence-v3",
            targetEvidence.IsStale,
            targetEvidence.ObservedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["safe_target_fingerprint"] = safeTargetFingerprint,
                ["target_fingerprint_version"] = "forgejo-target-v3",
                ["operation_scope"] = operationScope,
                ["api_surface_version"] = ForgejoProviderConstants.ApiSurfaceVersion,
                ["snapshot_version"] = snapshotVersion,
            });
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
        AppendField(hash, request.RepositoryProfileRef);
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
        => $"{uri.Scheme.ToLowerInvariant()}://{uri.IdnHost.ToLowerInvariant()}:{uri.Port}{uri.AbsolutePath}";

    private static bool IsSafeMetadataValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
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
