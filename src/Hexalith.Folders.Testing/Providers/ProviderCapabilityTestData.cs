using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Testing.Providers;

public static class ProviderCapabilityTestData
{
    public static ProviderCapabilityDiscoveryRequest Request(
        string providerFamily = "github",
        string providerKey = "github",
        string profileSchemaVersion = "v1",
        string correlationId = "correlation-a",
        ProviderTargetEvidence? targetEvidence = null)
        => new(
            "tenant-a",
            "organization-a",
            "binding-a",
            "credential-ref-a",
            providerFamily,
            providerKey,
            profileSchemaVersion,
            targetEvidence ?? TargetEvidence(),
            [ProviderCredentialMode.AppInstallationReference],
            new ProviderAuthorizationEvidenceSnapshot(
                "authz-snapshot-default",
                DateTimeOffset.Parse("2026-05-24T06:00:00+00:00"),
                "fresh"),
            correlationId);

    public static ProviderTargetEvidence TargetEvidence(
        string productVersion = "3.13.0",
        bool isStale = false,
        DateTimeOffset? observedAt = null)
        => new(
            "provider-product",
            productVersion,
            "rest-v1",
            "target-evidence-v1",
            isStale,
            observedAt ?? DateTimeOffset.Parse("2026-05-24T06:00:00+00:00"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "enterprise",
            });
}
