using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class GitHubDependencyGuardTests
{
    [Fact]
    public void OctokitReferencesStayInsideGitHubProviderBoundary()
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
            .Where(path => File.ReadAllText(path).Contains("Octokit", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Allow-list rationale (architecture A-6): Octokit is confined to the GitHub provider adapter. The only
        // legitimate concrete-adapter reference outside src/.../Providers/GitHub/ is the composition-root DI
        // registration in FoldersServiceCollectionExtensions.cs (the GitHub adapter is wired there) — this entry is a
        // deliberate, architecture-sanctioned exception, NOT a weakened guard. Do not broaden it to relax the boundary.
        references.ShouldAllBe(path =>
            path.StartsWith("src/Hexalith.Folders/Providers/GitHub/", StringComparison.Ordinal)
            || string.Equals(path, "src/Hexalith.Folders/Hexalith.Folders.csproj", StringComparison.Ordinal)
            || string.Equals(path, "src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs", StringComparison.Ordinal)
            || string.Equals(path, "tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs", StringComparison.Ordinal)
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/GitHub/", StringComparison.Ordinal)
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/Abstractions/", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedBuildPackageManagementPinsOctokitVersion()
    {
        string root = FindRepositoryRoot();
        string packagesProps = File.ReadAllText(Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"));

        packagesProps.ShouldContain("PackageVersion Include=\"Octokit\" Version=\"14.0.0\"", Case.Sensitive);
    }

    [Fact]
    public void CompatibilityCatalogPinsStory310AssumptionsWithoutApprovingOq4()
    {
        string root = FindRepositoryRoot();
        string catalogPath = Path.Combine(root, "docs", "contract", "provider-compatibility-catalog.md");

        File.Exists(catalogPath).ShouldBeTrue("Story 3.10 requires explicit, reviewable GitHub compatibility assumptions.");
        string catalog = File.ReadAllText(catalogPath);

        string[] requiredEvidence =
        [
            "Octokit `14.0.0`",
            "`X-GitHub-Api-Version: 2022-11-28`",
            "AppInstallationReference",
            "UserDelegatedReference",
            "`auto_init=false`",
            "canonical repository ID",
            "primary rate limit",
            "secondary rate limit",
            "unknown_provider_outcome",
            "no blind retry",
            "OQ4 status: pending-human-acceptance",
            "Story 3.3",
            "Story 3.11",
            "Story 3.14",
        ];

        foreach (string evidence in requiredEvidence)
        {
            catalog.ShouldContain(evidence, Case.Sensitive);
        }

        catalog.ShouldNotContain("OQ4 status: approved", Case.Sensitive);
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
