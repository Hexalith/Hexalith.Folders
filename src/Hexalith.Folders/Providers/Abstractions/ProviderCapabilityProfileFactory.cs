using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.Providers.Abstractions;

public static partial class ProviderCapabilityProfileFactory
{
    private const string SupportedSchemaVersion = "v1";

    public static ProviderCapabilityDiscoveryResult Create(
        ProviderCapabilityDiscoveryRequest request,
        string declaredProviderFamily,
        string declaredProviderKey,
        IReadOnlyList<ProviderCapabilityOperationRow> operationRows,
        ProviderRateLimitPosture rateLimit,
        IReadOnlyDictionary<string, string> knownFailureMappings,
        IReadOnlyDictionary<string, string> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredProviderFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredProviderKey);
        ArgumentNullException.ThrowIfNull(operationRows);
        ArgumentNullException.ThrowIfNull(rateLimit);
        ArgumentNullException.ThrowIfNull(knownFailureMappings);
        ArgumentNullException.ThrowIfNull(evidence);

        string schemaVersion = NormalizeVersion(request.ProfileSchemaVersion);
        if (schemaVersion.Length == 0)
        {
            return Failure(ProviderFailureCategory.ProviderValidationFailed, "missing_profile_schema_version", request);
        }

        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            return Failure(ProviderFailureCategory.ReconciliationRequired, "profile_schema_version_incompatible", request);
        }

        if (request.TargetEvidence.IsStale)
        {
            return Failure(ProviderFailureCategory.ReconciliationRequired, "target_evidence_stale", request);
        }

        string requestProviderFamily;
        string requestProviderKey;
        string providerFamily;
        string providerKey;
        try
        {
            requestProviderFamily = ProviderIdentityIdentifier.Normalize(request.ProviderFamily);
            requestProviderKey = ProviderIdentityIdentifier.Normalize(request.ProviderKey);
            providerFamily = ProviderIdentityIdentifier.Normalize(declaredProviderFamily);
            providerKey = ProviderIdentityIdentifier.Normalize(declaredProviderKey);
        }
        catch (ArgumentException)
        {
            return Failure(ProviderFailureCategory.ProviderValidationFailed, "provider_identity_malformed", request);
        }

        if (!string.Equals(requestProviderFamily, providerFamily, StringComparison.Ordinal)
            || !string.Equals(requestProviderKey, providerKey, StringComparison.Ordinal))
        {
            return Failure(ProviderFailureCategory.UnsupportedProviderCapability, "unsupported_provider_family", request);
        }

        if (ContainsSensitiveMetadata(request.TargetEvidence.Metadata)
            || ContainsSensitiveMetadata(rateLimit.Metadata)
            || ContainsSensitiveMetadata(knownFailureMappings)
            || ContainsSensitiveMetadata(evidence))
        {
            return Failure(ProviderFailureCategory.ProviderValidationFailed, "sensitive_provider_metadata_rejected", request);
        }

        IReadOnlyList<ProviderOperationCapability>? operations = NormalizeOperations(operationRows, out string? operationFailureReason);
        if (operations is null)
        {
            string reason = operationFailureReason ?? "operation_capability_invalid";
            ProviderFailureCategory category = reason.Contains("conflict", StringComparison.Ordinal)
                || reason.Contains("duplicate", StringComparison.Ordinal)
                    ? ProviderFailureCategory.ProviderConflict
                    : ProviderFailureCategory.ProviderValidationFailed;

            return Failure(category, reason, request);
        }

        string fingerprint = ComputeFingerprint(
            request,
            providerFamily,
            providerKey,
            schemaVersion,
            operations,
            rateLimit,
            knownFailureMappings,
            evidence);

        ProviderCapabilityProfileVersion version = new(schemaVersion, fingerprint);
        ProviderCapabilityProfile profile = new(
            request.ManagedTenantId,
            request.OrganizationId,
            request.ProviderBindingRef,
            providerFamily,
            providerKey,
            version,
            NormalizeTargetEvidence(request.TargetEvidence),
            operations,
            request.CredentialModeRequirements.Distinct().Order().ToArray(),
            NormalizeRateLimit(rateLimit),
            NormalizeDictionary(knownFailureMappings),
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            NormalizeDictionary(evidence),
            fingerprint);

        return ProviderCapabilityDiscoveryResult.Success(profile);
    }

    public static ProviderCapabilityComparisonResult Compare(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);

        List<string> changed = [];
        AddIfChanged(changed, "tenant", current.ManagedTenantId, candidate.ManagedTenantId);
        AddIfChanged(changed, "organization", current.OrganizationId, candidate.OrganizationId);
        AddIfChanged(changed, "provider_binding", current.ProviderBindingRef, candidate.ProviderBindingRef);
        AddIfChanged(changed, "provider_family", current.ProviderFamily, candidate.ProviderFamily);
        AddIfChanged(changed, "provider_key", current.ProviderKey, candidate.ProviderKey);
        AddIfChanged(changed, "schema_version", current.Version.SchemaVersion, candidate.Version.SchemaVersion);
        AddIfChanged(changed, "fingerprint", current.Fingerprint, candidate.Fingerprint);

        return new(
            string.Equals(current.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal),
            current.Fingerprint,
            candidate.Fingerprint,
            changed);
    }

    private static ProviderCapabilityDiscoveryResult Failure(
        ProviderFailureCategory category,
        string reasonCode,
        ProviderCapabilityDiscoveryRequest request)
        => ProviderCapabilityDiscoveryResult.Failure(category, reasonCode, request.CorrelationId);

    private static IReadOnlyList<ProviderOperationCapability>? NormalizeOperations(
        IReadOnlyList<ProviderCapabilityOperationRow> operationRows,
        out string? failureReason)
    {
        failureReason = null;
        Dictionary<string, List<ProviderCapabilityOperationRow>> rowsByOperation = new(StringComparer.Ordinal);

        foreach (ProviderCapabilityOperationRow row in operationRows)
        {
            string operationId;
            try
            {
                operationId = ProviderOperationIdentifier.Normalize(row.OperationId);
            }
            catch (ArgumentException)
            {
                failureReason = "operation_identifier_malformed";
                return null;
            }

            if (ContainsSensitiveMetadata(row.Limits) || ContainsSensitiveMetadata(row.Constraints))
            {
                failureReason = "sensitive_provider_metadata_rejected";
                return null;
            }

            if (!rowsByOperation.TryGetValue(operationId, out List<ProviderCapabilityOperationRow>? rows))
            {
                rows = [];
                rowsByOperation[operationId] = rows;
            }

            rows.Add(row with { OperationId = operationId });
        }

        foreach (KeyValuePair<string, List<ProviderCapabilityOperationRow>> pair in rowsByOperation)
        {
            if (pair.Value.Select(static x => x.Support).Distinct().Count() > 1)
            {
                failureReason = "conflicting_operation_capability";
                return null;
            }

            if (pair.Value.Count > 1)
            {
                failureReason = "duplicate_operation_capability";
                return null;
            }
        }

        return rowsByOperation
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new ProviderOperationCapability(
                pair.Key,
                pair.Value[0].Support,
                NormalizeDictionary(pair.Value[0].Limits),
                NormalizeDictionary(pair.Value[0].Constraints),
                pair.Value[0].Retryable,
                pair.Value[0].FailureCategory))
            .ToArray();
    }

    private static string ComputeFingerprint(
        ProviderCapabilityDiscoveryRequest request,
        string providerFamily,
        string providerKey,
        string schemaVersion,
        IReadOnlyList<ProviderOperationCapability> operations,
        ProviderRateLimitPosture rateLimit,
        IReadOnlyDictionary<string, string> knownFailureMappings,
        IReadOnlyDictionary<string, string> evidence)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendField(hash, request.ManagedTenantId);
        AppendField(hash, request.OrganizationId);
        AppendField(hash, request.ProviderBindingRef);
        AppendField(hash, providerFamily);
        AppendField(hash, providerKey);
        AppendField(hash, schemaVersion);
        AppendField(hash, NormalizeVersion(request.TargetEvidence.Product));
        AppendField(hash, NormalizeVersion(request.TargetEvidence.ProductVersion));
        AppendField(hash, NormalizeVersion(request.TargetEvidence.ApiSurfaceVersion));
        AppendField(hash, NormalizeVersion(request.TargetEvidence.EvidenceVersion));
        AppendDictionary(hash, request.TargetEvidence.Metadata);
        AppendField(hash, request.AuthorizationEvidence.Fingerprint);
        AppendCredentialModes(hash, request.CredentialModeRequirements);
        AppendOperations(hash, operations);
        AppendRateLimit(hash, rateLimit);
        AppendDictionary(hash, knownFailureMappings);
        AppendDictionary(hash, evidence);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static ProviderTargetEvidence NormalizeTargetEvidence(ProviderTargetEvidence target)
        => target with
        {
            Product = NormalizeVersion(target.Product),
            ProductVersion = NormalizeVersion(target.ProductVersion),
            ApiSurfaceVersion = NormalizeVersion(target.ApiSurfaceVersion),
            EvidenceVersion = NormalizeVersion(target.EvidenceVersion),
            Metadata = NormalizeDictionary(target.Metadata),
        };

    private static ProviderRateLimitPosture NormalizeRateLimit(ProviderRateLimitPosture rateLimit)
        => rateLimit with
        {
            Classification = NormalizeVersion(rateLimit.Classification),
            Metadata = NormalizeDictionary(rateLimit.Metadata),
        };

    private static IReadOnlyDictionary<string, string> NormalizeDictionary(IReadOnlyDictionary<string, string> values)
        => values
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                static pair => NormalizeVersion(pair.Key),
                static pair => NormalizeVersion(pair.Value),
                StringComparer.Ordinal);

    private static string NormalizeVersion(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    private static void AppendOperations(IncrementalHash hash, IReadOnlyList<ProviderOperationCapability> operations)
    {
        AppendInt32(hash, operations.Count);
        foreach (ProviderOperationCapability operation in operations.OrderBy(static x => x.OperationId, StringComparer.Ordinal))
        {
            AppendField(hash, operation.OperationId);
            AppendField(hash, operation.Support.ToString());
            AppendField(hash, operation.Retryable.ToString());
            AppendField(hash, operation.FailureCategory?.ToCategoryCode());
            AppendDictionary(hash, operation.Limits);
            AppendDictionary(hash, operation.Constraints);
        }
    }

    private static void AppendRateLimit(IncrementalHash hash, ProviderRateLimitPosture rateLimit)
    {
        AppendField(hash, NormalizeVersion(rateLimit.Classification));
        AppendField(hash, rateLimit.Retryable.ToString());
        AppendField(hash, rateLimit.RetryAfter?.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture));
        AppendDictionary(hash, rateLimit.Metadata);
    }

    private static void AppendCredentialModes(IncrementalHash hash, IReadOnlyList<ProviderCredentialMode> credentialModes)
    {
        ProviderCredentialMode[] modes = credentialModes.Distinct().Order().ToArray();
        AppendInt32(hash, modes.Length);
        foreach (ProviderCredentialMode mode in modes)
        {
            AppendField(hash, mode.ToString());
        }
    }

    private static void AppendDictionary(IncrementalHash hash, IReadOnlyDictionary<string, string> values)
    {
        KeyValuePair<string, string>[] entries = NormalizeDictionary(values).ToArray();
        AppendInt32(hash, entries.Length);
        foreach (KeyValuePair<string, string> pair in entries)
        {
            AppendField(hash, pair.Key);
            AppendField(hash, pair.Value);
        }
    }

    private static void AppendField(IncrementalHash hash, string? value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static bool ContainsSensitiveMetadata(IReadOnlyDictionary<string, string> values)
        => values.Any(pair => IsSensitive(pair.Key) || IsSensitive(pair.Value));

    private static bool IsSensitive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string canonical = value.Trim().ToLowerInvariant();
        return canonical.Contains("token", StringComparison.Ordinal)
            || canonical.Contains("secret", StringComparison.Ordinal)
            || canonical.Contains("password", StringComparison.Ordinal)
            || canonical.Contains("privatekey", StringComparison.Ordinal)
            || canonical.Contains("private key", StringComparison.Ordinal)
            || canonical.Contains("credential_url", StringComparison.Ordinal)
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("diff --git", StringComparison.Ordinal)
            || canonical.Contains("providerpayload", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || ProviderTokenPattern().IsMatch(value)
            || JwtPattern().IsMatch(value);
    }

    private static void AddIfChanged(List<string> changed, string dimension, string current, string candidate)
    {
        if (!string.Equals(current, candidate, StringComparison.Ordinal))
        {
            changed.Add(dimension);
        }
    }

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();
}
