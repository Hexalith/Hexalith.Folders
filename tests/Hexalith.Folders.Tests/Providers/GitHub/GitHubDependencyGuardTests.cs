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

        references.ShouldAllBe(path =>
            path.StartsWith("src/Hexalith.Folders/Providers/GitHub/", StringComparison.Ordinal)
            || string.Equals(path, "src/Hexalith.Folders/Hexalith.Folders.csproj", StringComparison.Ordinal)
            || string.Equals(path, "src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs", StringComparison.Ordinal)
            || string.Equals(path, "tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs", StringComparison.Ordinal)
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/GitHub/", StringComparison.Ordinal)
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/Abstractions/", StringComparison.Ordinal));
    }

    [Fact]
    public void CentralPackageManagementPinsOctokitVersion()
    {
        string root = FindRepositoryRoot();
        string packagesProps = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        packagesProps.ShouldContain("PackageVersion Include=\"Octokit\" Version=\"14.0.0\"", Case.Sensitive);
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
