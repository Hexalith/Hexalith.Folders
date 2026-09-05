using System.Text.RegularExpressions;
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
    public void CompatibilityCatalogPinsGitHubProviderAssumptionsWithApprovedOq4()
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
            "OQ4 status: approved",
            "OQ4 approval record: 2026-09-05 by jpiquot",
            "Story 3.3",
            "Story 3.11",
            "Story 3.14",
            "`git/blobs`",
            "`git/trees`",
            "`force=false`",
            "five read-only checks",
            "15-minute window",
            "provider_file_mutation_source_unconfigured",
        ];

        foreach (string evidence in requiredEvidence)
        {
            catalog.ShouldContain(evidence, Case.Sensitive);
        }

        catalog.Split('\n').Count(static line => string.Equals(
            line.TrimEnd('\r'),
            "- OQ4 status: approved",
            StringComparison.Ordinal)).ShouldBe(1);

        catalog.ShouldNotContain("OQ4 status: pending-operator-approval", Case.Sensitive);

        Regex approvalClaim = new(
            @"\b(approved|accepted)\b",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
        foreach (string line in catalog.Split('\n'))
        {
            if (!line.Contains("OQ4", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string trimmed = line.TrimEnd('\r');
            bool isStatusLine = string.Equals(trimmed.Trim(), "- OQ4 status: approved", StringComparison.Ordinal);
            bool isApprovalRecord = trimmed.TrimStart().StartsWith("- OQ4 approval record:", StringComparison.Ordinal);
            bool isFooter = trimmed.Contains("The GitHub OQ4 profile in this catalog is approved", StringComparison.Ordinal);
            if (isStatusLine || isApprovalRecord || isFooter)
            {
                approvalClaim.IsMatch(line).ShouldBeTrue($"Expected OQ4 approval wording on: '{trimmed.Trim()}'");
                continue;
            }

            approvalClaim.IsMatch(line).ShouldBeFalse($"Unexpected OQ4 approval claim: '{trimmed.Trim()}'");
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
