using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Abstractions;

public sealed class ProviderCapabilityProfileTests
{
    [Fact]
    public async Task FakeProvidersShouldExposeNProviderCapabilityProfiles()
    {
        ProviderCapabilityDiscoveryRequest request = ProviderCapabilityTestData.Request(providerFamily: " GitHub ", providerKey: "GITHUB-Enterprise");

        ProviderCapabilityDiscoveryResult github = await FakeGitProvider.GitHubLike().DiscoverCapabilitiesAsync(request, TestContext.Current.CancellationToken);
        ProviderCapabilityDiscoveryResult forgejo = await FakeGitProvider.ForgejoLike().DiscoverCapabilitiesAsync(request with { ProviderFamily = "forgejo", ProviderKey = "Forgejo" }, TestContext.Current.CancellationToken);
        ProviderCapabilityDiscoveryResult custom = await FakeGitProvider.CustomFamily().DiscoverCapabilitiesAsync(request with { ProviderFamily = "acme custom", ProviderKey = "Acme_Custom" }, TestContext.Current.CancellationToken);

        github.IsSuccess.ShouldBeTrue(github.ReasonCode);
        forgejo.IsSuccess.ShouldBeTrue(forgejo.ReasonCode);
        custom.IsSuccess.ShouldBeTrue(custom.ReasonCode);

        github.Profile.ShouldNotBeNull().ProviderFamily.ShouldBe("github");
        forgejo.Profile.ShouldNotBeNull().ProviderFamily.ShouldBe("forgejo");
        custom.Profile.ShouldNotBeNull().ProviderFamily.ShouldBe("acme_custom");

        github.Profile.Operations.Select(o => o.OperationId).ShouldContain(ProviderOperationCatalog.ReadinessValidation);
        github.Profile.Operations.Select(o => o.Support).ShouldContain(ProviderOperationSupport.Supported);
        forgejo.Profile.Operations.Select(o => o.Support).ShouldContain(ProviderOperationSupport.Partial);
        custom.Profile.Operations.Select(o => o.Support).ShouldContain(ProviderOperationSupport.Emulated);
        github.Profile.CredentialModeRequirements.ShouldContain(ProviderCredentialMode.AppInstallationReference);
        github.Profile.RateLimit.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task EquivalentProfilesShouldHaveStableFingerprintsAcrossOrderingAndVolatileEvidence()
    {
        ProviderCapabilityDiscoveryRequest first = ProviderCapabilityTestData.Request(correlationId: "correlation-a");
        ProviderCapabilityDiscoveryRequest second = ProviderCapabilityTestData.Request(correlationId: "correlation-b") with
        {
            TargetEvidence = ProviderCapabilityTestData.TargetEvidence(productVersion: " 3.13.0 ", observedAt: DateTimeOffset.Parse("2026-05-24T07:00:00+00:00")),
        };

        ProviderCapabilityDiscoveryResult resultA = await FakeGitProvider.GitHubLike(reversedOperations: false).DiscoverCapabilitiesAsync(first, TestContext.Current.CancellationToken);
        ProviderCapabilityDiscoveryResult resultB = await FakeGitProvider.GitHubLike(reversedOperations: true).DiscoverCapabilitiesAsync(second, TestContext.Current.CancellationToken);

        resultA.Profile.ShouldNotBeNull().Fingerprint.ShouldBe(resultB.Profile.ShouldNotBeNull().Fingerprint);
        resultA.Profile.Operations.Select(o => o.OperationId).ShouldBe(resultB.Profile.Operations.Select(o => o.OperationId));
    }

    [Fact]
    public async Task ChangedSafeEvidenceShouldChangeProfileVersionAndFingerprint()
    {
        ProviderCapabilityDiscoveryRequest request = ProviderCapabilityTestData.Request();
        FakeGitProvider provider = FakeGitProvider.GitHubLike();

        ProviderCapabilityDiscoveryResult first = await provider.DiscoverCapabilitiesAsync(request, TestContext.Current.CancellationToken);
        ProviderCapabilityDiscoveryResult changed = await provider.DiscoverCapabilitiesAsync(
            request with { TargetEvidence = ProviderCapabilityTestData.TargetEvidence(productVersion: "3.14.0") },
            TestContext.Current.CancellationToken);

        first.Profile.ShouldNotBeNull().Fingerprint.ShouldNotBe(changed.Profile.ShouldNotBeNull().Fingerprint);
        first.Profile.Version.ProfileFingerprint.ShouldNotBe(changed.Profile.Version.ProfileFingerprint);
    }

    [Fact]
    public void OperationCatalogShouldMapToExistingProviderReadinessAndSupportVocabulary()
    {
        ProviderOperationCatalog.CanonicalOperationIds.ShouldContain(ProviderOperationCatalog.ReadinessValidation);
        ProviderOperationCatalog.CanonicalOperationIds.ShouldContain(ProviderOperationCatalog.ProviderSupportEvidence);
        ProviderOperationCatalog.CanonicalOperationIds.ShouldContain(ProviderOperationCatalog.RepositoryBinding);
        ProviderOperationIdentifier.Normalize(" provider-readiness ").ShouldBe(ProviderOperationCatalog.ReadinessValidation);
        ProviderOperationIdentifier.Normalize("Provider Support Evidence").ShouldBe(ProviderOperationCatalog.ProviderSupportEvidence);
    }
}
