using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;

using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class DaprBackedProviderCredentialResolverTests
{
    [Fact]
    public async Task GitHubResolverShouldResolveCredentialLeaseFromExplicitReferenceOnly()
    {
        RecordingSecretStoreClient store = new(ProviderCredentialSecretLookupResult.Found(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["access_token"] = "synthetic-access-token",
            }));
        DaprBackedGitHubCredentialResolver resolver = new(ReferenceResolver(store));

        GitHubCredentialResolutionResult result = await resolver.ResolveAsync(
            GitHubRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        store.LastSecretStoreName.ShouldBe("folders-provider-credentials");
        store.LastCredentialReferenceId.ShouldBe("credential-ref-a");
        store.LastCredentialReferenceId.ShouldNotBe("binding-a");

        GitHubCredentialLease lease = result.Credential.ShouldNotBeNull();
        lease.AccessToken.ShouldBe("synthetic-access-token");
        await lease.DisposeAsync();
        lease.AccessToken.ShouldBeEmpty();
    }

    [Fact]
    public async Task ForgejoResolverShouldResolveCredentialLeaseFromExplicitReferenceOnly()
    {
        RecordingSecretStoreClient store = new(ProviderCredentialSecretLookupResult.Found(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["access_token"] = "synthetic-forgejo-token",
            }));
        DaprBackedForgejoCredentialResolver resolver = new(ReferenceResolver(store));

        ForgejoCredentialResolutionResult result = await resolver.ResolveAsync(
            ForgejoRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        store.LastCredentialReferenceId.ShouldBe("credential-ref-a");
        ForgejoCredentialLease lease = result.Credential.ShouldNotBeNull();
        lease.AccessToken.ShouldBe("synthetic-forgejo-token");
        await lease.DisposeAsync();
        lease.AccessToken.ShouldBeEmpty();
    }

    [Fact]
    public Task ReferenceResolverShouldMapMissingSecretWithoutExposingReferenceValue()
        => AssertFailureAsync(
            ProviderCredentialSecretLookupResult.Missing(),
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_credential_reference_missing");

    [Fact]
    public Task ReferenceResolverShouldMapDeniedSecretWithoutExposingReferenceValue()
        => AssertFailureAsync(
            ProviderCredentialSecretLookupResult.Denied(),
            ProviderFailureCategory.ProviderPermissionInsufficient,
            "provider_credential_reference_denied");

    [Fact]
    public Task ReferenceResolverShouldMapMalformedMultiValueSecretWithoutExposingReferenceValue()
        => AssertFailureAsync(
            ProviderCredentialSecretLookupResult.Found(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["access_token"] = "synthetic-access-token",
                ["extra"] = "unexpected",
            }),
            ProviderFailureCategory.ProviderAuthenticationRequired,
            "provider_credential_secret_malformed");

    [Fact]
    public Task ReferenceResolverShouldMapUnavailableSecretStoreWithoutExposingReferenceValue()
        => AssertFailureAsync(
            ProviderCredentialSecretLookupResult.Unavailable(TimeSpan.FromSeconds(30)),
            ProviderFailureCategory.ProviderUnavailable,
            "provider_credential_store_unavailable");

    [Fact]
    public async Task ReferenceResolverShouldRejectBlankCredentialReferenceBeforeSecretLookup()
    {
        RecordingSecretStoreClient store = new(ProviderCredentialSecretLookupResult.Found(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["access_token"] = "synthetic-access-token",
            }));
        DaprProviderCredentialReferenceResolver resolver = ReferenceResolver(store);

        ProviderCredentialReferenceResolutionResult result = await resolver.ResolveAsync(
            ReferenceRequest() with { CredentialReferenceId = " " },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("provider_credential_reference_missing");
        store.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ReferenceResolverShouldPropagateCancellationBeforeSecretLookup()
    {
        RecordingSecretStoreClient store = new(ProviderCredentialSecretLookupResult.Missing());
        DaprProviderCredentialReferenceResolver resolver = ReferenceResolver(store);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(ReferenceRequest(), cts.Token).AsTask());
        store.Calls.ShouldBe(0);
    }

    private static async Task AssertFailureAsync(
        ProviderCredentialSecretLookupResult lookup,
        ProviderFailureCategory expectedCategory,
        string expectedReason)
    {
        RecordingSecretStoreClient store = new(lookup);
        DaprProviderCredentialReferenceResolver resolver = ReferenceResolver(store);

        ProviderCredentialReferenceResolutionResult result = await resolver.ResolveAsync(
            ReferenceRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(expectedCategory);
        result.ReasonCode.ShouldBe(expectedReason);
        result.ReasonCode.ShouldNotContain("credential-ref-a", Case.Sensitive);
        result.AccessToken.ShouldBeNull();
    }

    private static DaprProviderCredentialReferenceResolver ReferenceResolver(RecordingSecretStoreClient store)
        => new(store, Options.Create(new FoldersProviderCredentialOptions()));

    private static ProviderCredentialReferenceResolutionRequest ReferenceRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            ProviderFamily: "github",
            ProviderKey: "github",
            CredentialMode: ProviderCredentialMode.AppInstallationReference,
            AuthorizationEvidenceFingerprint: "authz-fingerprint-a",
            CorrelationId: "correlation-a");

    private static GitHubCredentialResolutionRequest GitHubRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            CredentialMode: ProviderCredentialMode.AppInstallationReference,
            AuthorizationEvidenceFingerprint: "authz-fingerprint-a",
            CorrelationId: "correlation-a");

    private static ForgejoCredentialResolutionRequest ForgejoRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-ref-a",
            CredentialMode: ProviderCredentialMode.UserDelegatedReference,
            AuthorizationEvidenceFingerprint: "authz-fingerprint-a",
            CorrelationId: "correlation-a");

    private sealed class RecordingSecretStoreClient(ProviderCredentialSecretLookupResult result) : IProviderCredentialSecretStoreClient
    {
        public int Calls { get; private set; }

        public string? LastSecretStoreName { get; private set; }

        public string? LastCredentialReferenceId { get; private set; }

        public ValueTask<ProviderCredentialSecretLookupResult> GetSecretAsync(
            string secretStoreName,
            string credentialReferenceId,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastSecretStoreName = secretStoreName;
            LastCredentialReferenceId = credentialReferenceId;
            metadata.Keys.ShouldContain("provider_family");
            metadata.Keys.ShouldContain("correlation_id");
            return ValueTask.FromResult(result);
        }
    }
}
