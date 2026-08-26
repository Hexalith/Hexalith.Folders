using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoDependencyGuardTests
{
    [Fact]
    public void ForgejoProviderImplementationDoesNotReferenceForbiddenIntegrationStacks()
    {
        string root = FindRepositoryRoot();
        string providerRoot = Path.Combine(root, "src", "Hexalith.Folders", "Providers", "Forgejo");
        string[] forbiddenTerms =
        [
            "O" + "ctokit",
            "Aspire.",
            "Dapr.",
            "Keycloak",
            "Redis",
            "ModelContextProtocol",
            "System.CommandLine",
            "Hexalith.Folders.Client",
            "Hexalith.Folders.Contracts",
            "EventStore",
        ];

        string[] violations = Directory
            .EnumerateFiles(providerRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .SelectMany(file => forbiddenTerms
                .Where(term => file.Text.Contains(term, StringComparison.Ordinal))
                .Select(term => $"{Path.GetRelativePath(root, file.Path).Replace('\\', '/')}: {term}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ForgejoInternalSeamAndSnapshotTypesStayInsideForgejoBoundaryAndTests()
    {
        string root = FindRepositoryRoot();
        string[] inspectedRoots =
        [
            Path.Combine(root, "src"),
            Path.Combine(root, "tests"),
        ];

        string[] references = inspectedRoots
            .SelectMany(path => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("IForgejoApiClient", StringComparison.Ordinal)
                    || text.Contains("ForgejoReadinessResult", StringComparison.Ordinal)
                    || text.Contains("ForgejoSupportedVersionEntry", StringComparison.Ordinal)
                    || text.Contains("ForgejoFailureMapper", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        references.ShouldAllBe(path =>
            path.StartsWith("src/Hexalith.Folders/Providers/Forgejo/", StringComparison.Ordinal)
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/Forgejo/", StringComparison.Ordinal)
            || string.Equals(path, "src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderAbstractionsRemainFreeOfForgejoSpecificDetails()
    {
        string root = FindRepositoryRoot();
        string abstractionsRoot = Path.Combine(root, "src", "Hexalith.Folders", "Providers", "Abstractions");

        string[] references = Directory
            .EnumerateFiles(abstractionsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("Forgejo", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        references.ShouldBeEmpty();
    }

    [Fact]
    public void ForgejoUsesCentralPackageManagementWithoutInlineVersionsOrProviderSdk()
    {
        string root = FindRepositoryRoot();
        string projectFile = File.ReadAllText(Path.Combine(root, "src", "Hexalith.Folders", "Hexalith.Folders.csproj"));

        projectFile.ShouldNotContain("Forgejo", Case.Sensitive);
        projectFile.ShouldNotContain("Gitea", Case.Sensitive);
        projectFile.ShouldNotContain("Version=", Case.Sensitive);
    }

    [Fact]
    public void DefaultCompositionRegistersOneForgejoProviderAndManagedHttpFactory()
    {
        ServiceCollection services = new();

        services.AddFoldersProviderReadiness();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ForgejoProvider concrete = serviceProvider.GetServices<ForgejoProvider>().ShouldHaveSingleItem();
        ForgejoProvider providerPort = serviceProvider.GetServices<IGitProvider>().OfType<ForgejoProvider>().ShouldHaveSingleItem();
        providerPort.ShouldBeSameAs(concrete);
        serviceProvider.GetRequiredService<IForgejoApiClientFactory>().ShouldBeOfType<ForgejoHttpApiClientFactory>();
        serviceProvider.GetRequiredService<IHttpClientFactory>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<IProviderRepositoryTargetResolver>()
            .ShouldBeOfType<UnconfiguredProviderRepositoryTargetResolver>();
    }

    [Fact]
    public async Task DefaultCompositionFailsClosedAtTargetResolutionBeforeCredentialOrProviderAccess()
    {
        ServiceCollection services = new();
        CountingForgejoCredentialResolver credentialResolver = new();
        CountingForgejoApiClientFactory apiClientFactory = new();
        services.AddSingleton<IForgejoCredentialResolver>(credentialResolver);
        services.AddSingleton<IForgejoApiClientFactory>(apiClientFactory);
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ForgejoProvider provider = serviceProvider.GetServices<IGitProvider>().OfType<ForgejoProvider>().ShouldHaveSingleItem();

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
        result.ReasonCode.ShouldBe("provider_repository_creation_target_unconfigured");
        credentialResolver.Calls.ShouldBe(0);
        apiClientFactory.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task CompositionUsesPreconfiguredForgejoDependenciesExactlyOnce()
    {
        ServiceCollection services = new();
        CountingForgejoCredentialResolver credentialResolver = new();
        CountingForgejoApiClientFactory apiClientFactory = new();
        CountingForgejoTargetResolver targetResolver = new();
        services.AddSingleton<IForgejoCredentialResolver>(credentialResolver);
        services.AddSingleton<IForgejoApiClientFactory>(apiClientFactory);
        services.AddSingleton<IProviderRepositoryTargetResolver>(targetResolver);
        services.AddFoldersProviderReadiness();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ForgejoProvider provider = serviceProvider.GetServices<IGitProvider>().OfType<ForgejoProvider>().ShouldHaveSingleItem();

        ProviderRepositoryCreationResult result = await provider.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.CanonicalRepositoryId.ShouldBe("42");
        targetResolver.Calls.ShouldBe(1);
        credentialResolver.Calls.ShouldBe(1);
        apiClientFactory.Calls.ShouldBe(1);
        apiClientFactory.ClientCalls.ShouldBe(1);
    }

    private static ProviderRepositoryCreationRequest CreationRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "binding-a",
            CredentialReferenceId: "credential-a",
            RepositoryBindingId: "repository-binding-a",
            ProviderFamily: "forgejo",
            ProviderKey: "forgejo",
            TargetEvidence: new ProviderTargetEvidence(
                "forgejo",
                "16.0.3",
                ForgejoProviderConstants.ApiSurfaceVersion,
                "evidence-v1",
                IsStale: false,
                DateTimeOffset.Parse("2026-08-26T00:00:00+00:00"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authorized_base_url"] = "https://forgejo.example.test",
                    ["safe_target_fingerprint"] = "safe-target-a",
                    ["operation_scope"] = "repository_creation",
                }),
            CredentialModeRequirements: [ProviderCredentialMode.UserDelegatedReference],
            AuthorizationEvidence: new ProviderAuthorizationEvidenceSnapshot(
                "authorization-a",
                DateTimeOffset.Parse("2026-08-26T00:00:00+00:00"),
                "fresh"),
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a",
            IdempotencyAdmission: new ProviderIdempotencyAdmission(
                ProviderIdempotencyDisposition.Fresh,
                "intent-a"),
            RepositoryProfileRef: "profile-a");

    private sealed class CountingForgejoCredentialResolver : IForgejoCredentialResolver
    {
        public int Calls { get; private set; }

        public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
            ForgejoCredentialResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(ForgejoCredentialResolutionResult.Success(
                ForgejoCredentialLease.CreateForTesting("provider-secret")));
        }
    }

    private sealed class CountingForgejoTargetResolver : IProviderRepositoryTargetResolver
    {
        public int Calls { get; private set; }

        public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
            ProviderRepositoryCreationTargetResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Success(new ProviderRepositoryResolvedTarget(
                Owner: "forgejo-owner",
                RepositoryName: "forgejo-repository",
                Visibility: ProviderRepositoryVisibility.Private,
                DefaultBranch: "main",
                SelectedRef: "main",
                RequireProtectedRef: true,
                RequireContentsPermission: true,
                RequireAdministrationPermission: true,
                ExpectedCanonicalRepositoryId: null,
                EquivalentExistingAuthorized: false)));
        }

        public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
            ProviderRepositoryBindingTargetResolutionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_repository_binding_target_unconfigured"));
    }

    private sealed class CountingForgejoApiClientFactory : IForgejoApiClientFactory
    {
        private readonly CountingForgejoApiClient _client = new();

        public int Calls { get; private set; }

        public int ClientCalls => _client.Calls;

        public ValueTask<IForgejoApiClient> CreateAsync(
            ForgejoApiClientRequest request,
            ForgejoCredentialLease credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult<IForgejoApiClient>(_client);
        }
    }

    private sealed class CountingForgejoApiClient : IForgejoApiClient
    {
        public int Calls { get; private set; }

        public Task<ForgejoReadinessResult> GetReadinessAsync(
            ForgejoReadinessRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
            ForgejoRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(ForgejoRepositoryCreationResult.Success(canonicalRepositoryId: "42"));
        }

        public Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(
            ForgejoRepositoryBindingRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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
