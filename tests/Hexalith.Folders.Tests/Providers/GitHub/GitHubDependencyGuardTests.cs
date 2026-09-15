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

        // Allowlisted OQ4 approval wording must actually carry approval wording, and every other OQ4 line
        // must carry none. Diagnostics report the offending line index only, never the line content.
        int[] missingClaims = EvaluateAllowlistedLinesWithoutApprovalWording(catalog);
        missingClaims.ShouldBeEmpty($"Allowlisted OQ4 lines without approval wording at indexes: {string.Join(", ", missingClaims)}");

        int[] unauthorizedClaims = EvaluateUnauthorizedOq4ApprovalClaims(catalog);
        unauthorizedClaims.ShouldBeEmpty($"Unauthorized OQ4 approval claims at line indexes: {string.Join(", ", unauthorizedClaims)}");
    }

    [Fact]
    public void UnauthorizedOq4ApprovalClaimsFailClosedReportingOnlyTheLineIndex()
    {
        // Each allowlisted shape stays authorized...
        foreach (string authorized in new[]
                 {
                     "- OQ4 status: approved",
                     "- OQ4 approval record: 2026-09-05 by jpiquot; the profile is approved.",
                     "- OQ4 authority approval: Provider, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the Provider authority.",
                     "- OQ4 authority approval: Architecture, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the Architecture authority.",
                     "- OQ4 authority approval: PM, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the PM authority.",
                     CatalogFooterApprovalLine,
                 })
        {
            EvaluateUnauthorizedOq4ApprovalClaims(authorized).ShouldBeEmpty(authorized);
            EvaluateAllowlistedLinesWithoutApprovalWording(authorized).ShouldBeEmpty(authorized);
        }

        // ...while any other OQ4 line that claims approval or acceptance fails closed, including a line that
        // opens with an allowlisted prefix and then smuggles an extra claim onto the rest of the line, and a
        // truncated footer that keeps only the approving half.
        foreach (string unauthorized in new[]
                 {
                     "- OQ4 summary: the Forgejo live evidence lane is approved.",
                     "OQ4 is accepted for release.",
                     "- OQ4 note: accepted by the provider team.",
                     "- OQ4 authority approval: Provider, signer Administrator, dated 2026-09-15. Also the Forgejo live evidence lane is accepted.",
                     "- OQ4 authority approval: Provider, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the PM authority.",
                     "- OQ4 authority approval: Legal, signer Administrator, dated 2026-09-15, bound to catalog version `1.0.0` and its SHA-256 digest. Catalog version `1.0.0` is approved for the Legal authority.",
                     "The GitHub OQ4 profile in this catalog is approved; nothing else is.",
                 })
        {
            int[] diagnostics = EvaluateUnauthorizedOq4ApprovalClaims($"# Catalog\n\n{unauthorized}\n");
            diagnostics.ShouldBe([2], unauthorized);
        }

        // An allowlisted shape that silently loses its approval wording also fails closed.
        EvaluateAllowlistedLinesWithoutApprovalWording("- OQ4 approval record: 2026-09-05 by jpiquot; no wording here.")
            .ShouldBe([0]);

        // The diagnostic surface is line indexes only; no catalog content can ride along.
        EvaluateUnauthorizedOq4ApprovalClaims("- OQ4 note: accepted by the provider team.")
            .ShouldAllBe(static index => index >= 0);
    }

    private static int[] EvaluateUnauthorizedOq4ApprovalClaims(string catalog) =>
        Oq4Lines(catalog)
            .Where(entry => !IsAllowlistedOq4ApprovalLine(entry.Line) && ApprovalClaim.IsMatch(entry.Line))
            .Select(entry => entry.Index)
            .ToArray();

    private static int[] EvaluateAllowlistedLinesWithoutApprovalWording(string catalog) =>
        Oq4Lines(catalog)
            .Where(entry => IsAllowlistedOq4ApprovalLine(entry.Line) && !ApprovalClaim.IsMatch(entry.Line))
            .Select(entry => entry.Index)
            .ToArray();

    private static IEnumerable<(int Index, string Line)> Oq4Lines(string catalog) =>
        catalog.Split('\n')
            .Select(static (line, index) => (Index: index, Line: line.TrimEnd('\r')))
            .Where(static entry => entry.Line.Contains("OQ4", StringComparison.OrdinalIgnoreCase));

    // Allow-list rationale: exactly four OQ4 wordings may pair OQ4 with an approval claim — the single status
    // line, the dated per-record approval lines, the three per-authority approval lines introduced by the
    // versioned OQ4 catalog package, and the catalog footer. The authority lines and the footer are matched
    // whole-line and exactly, so an allowlisted prefix cannot smuggle an extra approval claim onto the rest
    // of the line. Broadening this set weakens the guard.
    private static bool IsAllowlistedOq4ApprovalLine(string line)
    {
        string trimmed = line.Trim();
        return string.Equals(trimmed, "- OQ4 status: approved", StringComparison.Ordinal)
            || string.Equals(trimmed, CatalogFooterApprovalLine, StringComparison.Ordinal)
            || line.TrimStart().StartsWith("- OQ4 approval record:", StringComparison.Ordinal)
            || AuthorityApprovalLine.IsMatch(trimmed);
    }

    private const string CatalogFooterApprovalLine =
        "Full GitHub provider-ready status requires OQ8 acceptance plus completion evidence from Stories 3.3, 3.10, 3.11, and 3.14. "
        + "The GitHub OQ4 profile in this catalog is approved; this catalog must not be interpreted as complete provider-ready or release acceptance.";

    // Pins the authority set, the named signer, the approval date shape, and the catalog version, and requires
    // the approval sentence to close the line for the same authority it opened with.
    private static readonly Regex AuthorityApprovalLine = new(
        @"^- OQ4 authority approval: (?<authority>Provider|Architecture|PM), signer Administrator, dated \d{4}-\d{2}-\d{2}, "
        + @"bound to catalog version `\d+\.\d+\.\d+` and its SHA-256 digest\. "
        + @"Catalog version `\d+\.\d+\.\d+` is approved for the \k<authority> authority\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ApprovalClaim = new(
        @"\b(approved|accepted)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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
