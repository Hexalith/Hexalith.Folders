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
            || path.StartsWith("tests/Hexalith.Folders.Tests/Providers/Forgejo/", StringComparison.Ordinal));
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
