using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Testing.Providers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Abstractions;

public sealed class ProviderCapabilityBoundaryTests
{
    [Fact]
    public async Task DeniedCapabilityDiscoveryShouldNotObserveProvidersOrEvidenceStores()
    {
        RecordingProviderCapabilityAuthorizer authorizer = RecordingProviderCapabilityAuthorizer.Denied();
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore evidenceStore = new();
        ProviderCapabilityDiscoveryService service = new(authorizer, resolver, evidenceStore);

        ProviderCapabilityDiscoveryResult result = await service.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderPermissionInsufficient);
        authorizer.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(0);
        evidenceStore.Calls.ShouldBe(0);
        resolver.ProviderCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AllowedCapabilityDiscoveryShouldCaptureOneAuthorizationSnapshotBeforeObservation()
    {
        RecordingProviderCapabilityAuthorizer authorizer = RecordingProviderCapabilityAuthorizer.Allowed("authz-snapshot-a");
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RecordingProviderCapabilityEvidenceStore evidenceStore = new();
        ProviderCapabilityDiscoveryService service = new(authorizer, resolver, evidenceStore);

        ProviderCapabilityDiscoveryResult result = await service.DiscoverCapabilitiesAsync(
            ProviderCapabilityTestData.Request(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.Profile.ShouldNotBeNull().AuthorizationEvidenceFingerprint.ShouldBe("authz-snapshot-a");
        authorizer.Calls.ShouldBe(1);
        resolver.Calls.ShouldBe(1);
        evidenceStore.Calls.ShouldBe(1);
        resolver.ProviderCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RequestsAndResultsShouldNotSerializeCredentialOrProviderPayloadSentinels()
    {
        string[] forbidden =
        [
            "ghp_123456789012345678901234567890123456",
            "-----BEGIN PRIVATE KEY-----",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature",
            "https://user:password@example.invalid/repository.git",
            "branch-secret-prod",
            "diff --git a/secret b/secret",
            "providerPayload",
            "display-name@example.invalid",
            "unauthorized-resource-name",
        ];

        ProviderCapabilityDiscoveryRequest request = ProviderCapabilityTestData.Request();
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.GitHubLike()
            .DiscoverCapabilitiesAsync(request, TestContext.Current.CancellationToken);

        string serialized = JsonSerializer.Serialize(new { request, result });
        foreach (string value in forbidden)
        {
            serialized.ShouldNotContain(value, Case.Sensitive);
        }
    }

    [Fact]
    public async Task ProviderPayloadShapedEvidenceShouldBeRejectedAndNeverSerializedIntoResults()
    {
        // Each credential/provider-payload-shaped sentinel injected as provider evidence metadata must be
        // rejected at the port boundary and must never survive into the serialized failure result.
        string[] forbidden =
        [
            "ghp_123456789012345678901234567890123456",
            "-----BEGIN PRIVATE KEY-----",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature",
            "https://user:password@example.invalid/repository.git",
            "branch-secret-prod",
            "diff --git a/secret b/secret",
            "providerPayload",
            "display-name@example.invalid",
        ];

        foreach (string sentinel in forbidden)
        {
            ProviderCapabilityDiscoveryResult result = await FakeGitProvider
                .WithEvidenceMetadata(("diagnostic", sentinel))
                .DiscoverCapabilitiesAsync(ProviderCapabilityTestData.Request(), TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse(sentinel);
            result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed, sentinel);
            result.Profile.ShouldBeNull(sentinel);
            JsonSerializer.Serialize(result).ShouldNotContain(sentinel, Case.Sensitive);
        }
    }

    [Fact]
    public async Task SensitiveMetadataShouldBeRejectedInsteadOfReturnedForDiagnostics()
    {
        ProviderCapabilityDiscoveryResult result = await FakeGitProvider.WithEvidenceMetadata(("diagnostic", "ghp_123456789012345678901234567890123456"))
            .DiscoverCapabilitiesAsync(ProviderCapabilityTestData.Request(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
        JsonSerializer.Serialize(result).ShouldNotContain("ghp_123456789012345678901234567890123456", Case.Sensitive);
    }

    [Fact]
    public void ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies()
    {
        string root = FindRepositoryRoot();
        string abstractionRoot = Path.Combine(root, "src", "Hexalith.Folders", "Providers", "Abstractions");
        string combined = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(abstractionRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        string[] forbiddenTerms =
        [
            "Octokit",
            "Dapr",
            "EventStore",
            "HttpClient",
            "System.Net.Http",
            "Hexalith.Folders.Client",
            "Hexalith.Folders.Cli",
            "Hexalith.Folders.Mcp",
            "Hexalith.Folders.UI",
            "Hexalith.Folders.Workers",
            "Aspire",
            "Redis",
            "Keycloak",
            "Directory.",
            "File.",
            "Process",
        ];

        foreach (string forbidden in forbiddenTerms)
        {
            combined.ShouldNotContain(forbidden, Case.Sensitive);
        }
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
