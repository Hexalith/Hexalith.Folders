namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityProfile(
    string ManagedTenantId,
    string OrganizationId,
    string ProviderBindingRef,
    string ProviderFamily,
    string ProviderKey,
    ProviderCapabilityProfileVersion Version,
    ProviderTargetEvidence TargetEvidence,
    IReadOnlyList<ProviderOperationCapability> Operations,
    IReadOnlyList<ProviderCredentialMode> CredentialModeRequirements,
    ProviderRateLimitPosture RateLimit,
    IReadOnlyDictionary<string, string> KnownFailureMappings,
    string AuthorizationEvidenceFingerprint,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Evidence,
    string Fingerprint);
