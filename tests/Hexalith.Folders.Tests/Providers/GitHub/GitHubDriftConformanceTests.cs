using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Providers.Forgejo;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

/// <summary>
/// Hermetic C12 drift gate for the GitHub profile. GitHub REST is one dated API behind a pinned SDK
/// package, so the lane is a pinned-profile manifest plus the catalog's fixture-to-failure-mode
/// coverage matrix rather than a schema-snapshot diff. Nothing here performs a network call.
/// </summary>
public sealed class GitHubDriftConformanceTests
{
    private const string ManifestRelativePath = "tests/contracts/github/pinned-profile.json";
    private const string CatalogRelativePath = "docs/contract/provider-compatibility-catalog.md";
    private const string PackagePinRelativePath = "references/Hexalith.Builds/Props/Directory.Packages.props";
    private const string LibGit2SharpPinRelativePath = "Directory.Packages.props";
    private const string NightlyDriftScriptRelativePath = "tests/tools/run-nightly-drift-gates.ps1";
    private const string ExpectedSchemaVersion = "github-pinned-profile-v1";
    private const string ExpectedDriftLane = "pinned-profile-manifest-plus-failure-mode-coverage-matrix";
    private const string UnmappedReasonCode = "github_unmapped_outcome";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // The single assembly the manifest's matrix rows are resolved against. The manifest must name it.
    private static readonly Assembly ResolvingAssembly = typeof(GitHubDriftConformanceTests).Assembly;

    [Fact]
    public void PinnedProfileManifestMatchesTheCatalogThePackagePinAndEveryProvingFixture()
    {
        PinnedProfile manifest = LoadManifest();
        CatalogClaims claims = LoadCatalogClaims();

        DriftDiagnostic[] diagnostics = EvaluatePinnedProfile(
            manifest,
            claims,
            ReadPinnedPackageVersion(PackagePinRelativePath, "Octokit"),
            ReadPinnedPackageVersion(LibGit2SharpPinRelativePath, "LibGit2Sharp"),
            GitHubProviderConstants.RestApiVersion,
            GitHubProviderConstants.ProductHeader);

        foreach (DriftDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        manifest.Provider.ShouldBe(GitHubProviderConstants.ProviderFamily);
        manifest.CatalogPath.ShouldBe(CatalogRelativePath);
        manifest.PackagePinPath.ShouldBe(PackagePinRelativePath);
        manifest.DriftLane.ShouldBe(ExpectedDriftLane);
        manifest.NetworkCallsPermitted.ShouldBeFalse();
        manifest.Coverage.Length.ShouldBe(claims.FailureCategories.Count);

        // The manifest names the assembly whose fixtures prove the matrix; resolution must actually use it.
        manifest.ProvingAssembly.ShouldBe(ResolvingAssembly.GetName().Name);
    }

    [Fact]
    public void PinnedProfileDriftFailsClosedForUnmappedOrphanedAndMismatchedEvidence()
    {
        PinnedProfile manifest = LoadManifest();
        CatalogClaims claims = LoadCatalogClaims();
        string pinnedOctokit = ReadPinnedPackageVersion(PackagePinRelativePath, "Octokit");
        string pinnedLibGit2Sharp = ReadPinnedPackageVersion(LibGit2SharpPinRelativePath, "LibGit2Sharp");

        DriftDiagnostic[] Evaluate(PinnedProfile candidate, CatalogClaims candidateClaims) => EvaluatePinnedProfile(
            candidate,
            candidateClaims,
            pinnedOctokit,
            pinnedLibGit2Sharp,
            GitHubProviderConstants.RestApiVersion,
            GitHubProviderConstants.ProductHeader);

        // An unmapped category: the catalog claims a provider-neutral category the matrix does not prove.
        CoverageRow droppedRow = manifest.Coverage[0];
        DriftDiagnostic[] unmapped = Evaluate(manifest with { Coverage = manifest.Coverage.Skip(1).ToArray() }, claims);
        unmapped.ShouldContain(diagnostic =>
            diagnostic.Category == "github_failure_category_unmapped" && diagnostic.Identifier == droppedRow.ProviderNeutralCategory);

        // An orphaned row: the matrix proves a category the catalog does not claim.
        DriftDiagnostic[] orphaned = Evaluate(
            manifest,
            claims with { FailureCategories = claims.FailureCategories.Skip(1).ToArray() });
        orphaned.ShouldContain(diagnostic =>
            diagnostic.Category == "github_failure_category_orphaned" && diagnostic.Identifier == claims.FailureCategories[0]);

        // A duplicate row cannot silently stand in for coverage of two categories.
        DriftDiagnostic[] duplicated = Evaluate(
            manifest with { Coverage = manifest.Coverage.Concat([droppedRow]).ToArray() },
            claims);
        duplicated.ShouldContain(diagnostic =>
            diagnostic.Category == "github_failure_category_duplicate" && diagnostic.Identifier == droppedRow.ProviderNeutralCategory);

        // A category that is not part of the provider-neutral vocabulary at all.
        DriftDiagnostic[] unknownCategory = Evaluate(
            manifest with
            {
                Coverage = [.. manifest.Coverage.Skip(1), droppedRow with { ProviderNeutralCategory = "NotAProviderFailureCategory" }],
            },
            claims);
        unknownCategory.ShouldContain(diagnostic => diagnostic.Category == "github_failure_category_orphaned");

        // A matrix row that names a proving fixture which does not exist.
        DriftDiagnostic[] missingFixture = Evaluate(
            manifest with
            {
                Coverage = [.. manifest.Coverage.Skip(1), droppedRow with { ProvingFixture = "Hexalith.Folders.Tests.Providers.GitHub.NoSuchTests.NoSuchMethod" }],
            },
            claims);
        missingFixture.ShouldContain(diagnostic =>
            diagnostic.Category == "github_proving_fixture_missing" && diagnostic.Identifier == droppedRow.ProviderNeutralCategory);

        // A matrix row whose proving fixture exists but does not actually prove the claimed condition/category pair.
        DriftDiagnostic[] unprovenCondition = Evaluate(
            manifest with
            {
                Coverage = [.. manifest.Coverage.Skip(1), droppedRow with { ProvingCondition = "ReservationInvalidated" }],
            },
            claims);
        unprovenCondition.ShouldContain(diagnostic =>
            diagnostic.Category == "github_proving_condition_unproven" && diagnostic.Identifier == droppedRow.ProviderNeutralCategory);

        // Package, API-version, product-header, catalog-version, schema, and network posture drift.
        Evaluate(manifest with { OctokitPackageVersion = "13.0.0" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_package_mismatch");
        Evaluate(manifest with { LibGit2SharpPackageVersion = "0.31.0" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_package_mismatch"
                && diagnostic.Identifier == "libgit2sharp_package_version");
        Evaluate(manifest with { ProvingAssembly = "Some.Other.Tests" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_proving_assembly_mismatch");
        Evaluate(manifest with { RestApiVersion = "2021-01-01" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_api_version_mismatch");
        Evaluate(manifest with { ProductHeader = "Other-Product" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_product_header_mismatch");
        Evaluate(manifest with { CatalogVersion = "9.9.9" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_catalog_mismatch");
        Evaluate(manifest with { SchemaVersion = "github-pinned-profile-v0" }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_schema_mismatch");
        Evaluate(manifest with { NetworkCallsPermitted = true }, claims)
            .ShouldContain(diagnostic => diagnostic.Category == "github_profile_network_call_permitted");

        foreach (DriftDiagnostic diagnostic in unmapped
            .Concat(orphaned)
            .Concat(duplicated)
            .Concat(unknownCategory)
            .Concat(missingFixture)
            .Concat(unprovenCondition))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void GitHubDriftLaneMakesNoNetworkCall()
    {
        string manifest = File.ReadAllText(RepositoryPath(ManifestRelativePath));
        string script = File.ReadAllText(RepositoryPath(NightlyDriftScriptRelativePath));

        foreach (string host in new[] { "api.github.com", "github.com", "https://", "http://" })
        {
            manifest.ShouldNotContain(host, Case.Insensitive);
        }

        script.ShouldNotContain("api.github.com", Case.Insensitive);
        script.ShouldContain("tests/contracts/github/pinned-profile.json", Case.Sensitive);
        script.ShouldContain("github-pinned-profile-integrity", Case.Sensitive);
        script.ShouldContain("github-failure-mode-coverage", Case.Sensitive);
    }

    [Fact]
    public void CatalogClaimedCategoriesEqualTheFailureMapperCodomainAndEveryConditionIsHandled()
    {
        CatalogClaims claims = LoadCatalogClaims();
        ProviderCapabilityDiscoveryRequest request = ProviderCapabilityTestData.Request();

        GitHubApiFailureCondition[] conditions =
        [
            .. Enum.GetValues<GitHubApiFailureCondition>().Where(condition => condition != GitHubApiFailureCondition.None),
        ];

        HashSet<string> codomain = new(StringComparer.Ordinal);
        List<string> unhandled = [];

        foreach (GitHubApiFailureCondition condition in conditions)
        {
            (ProviderFailureCategory operationCategory, string operationReason) =
                GitHubFailureMapper.ToProviderOperationFailure(condition);
            ProviderCapabilityDiscoveryResult readiness = GitHubFailureMapper.ToProviderFailure(
                GitHubReadinessResult.Failure(condition),
                request);

            bool handled = false;
            if (!string.Equals(operationReason, UnmappedReasonCode, StringComparison.Ordinal))
            {
                codomain.Add(operationCategory.ToString());
                handled = true;
            }

            if (!string.Equals(readiness.ReasonCode, UnmappedReasonCode, StringComparison.Ordinal))
            {
                codomain.Add(readiness.FailureCategory.ToString());
                handled = true;
            }

            if (!handled && condition != GitHubApiFailureCondition.ExistingEquivalent)
            {
                unhandled.Add(condition.ToString());
            }
        }

        // `ExistingEquivalent` is the one non-None condition the mapper handles as a success short-circuit
        // before any failure switch, so it is proven through the declared success mapping instead.
        GitHubFailureMapper.KnownFailureMappings["existing_equivalent"]
            .ShouldBe(ProviderFailureCategory.None.ToCategoryCode());

        unhandled.ShouldBeEmpty(
            $"GitHubApiFailureCondition members the failure mapper does not handle: {string.Join(", ", unhandled)}");

        // The catalog's claimed provider-neutral vocabulary must be exactly what the mapper can produce.
        // A new mapper category, or a category the catalog claims but the mapper cannot emit, reddens here.
        codomain.Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(claims.FailureCategories.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void NightlyDriftExpectedTestCountsMatchTheRealCaseCounts()
    {
        string script = File.ReadAllText(RepositoryPath(NightlyDriftScriptRelativePath));

        CountExactly(script, @"(?<![A-Za-z_])expected_test_count = (?<count>\d+)")
            .ShouldBe(CountXunitCases(typeof(ForgejoManifestAndDriftTests)), "Forgejo drift lane test count");
        CountExactly(script, @"github_expected_test_count = (?<count>\d+)")
            .ShouldBe(CountXunitCases(typeof(GitHubDriftConformanceTests)), "GitHub drift lane test count");
    }

    private static int CountExactly(string script, string pattern)
    {
        MatchCollection matches = Regex.Matches(script, pattern, RegexOptions.CultureInvariant);
        matches.Count.ShouldBe(1, $"the nightly drift script must declare '{pattern}' exactly once.");
        return int.Parse(matches[0].Groups["count"].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Counts the real xUnit cases on a test class: one per <c>[Fact]</c> and one per <c>[InlineData]</c> row
    /// on each <c>[Theory]</c>. Adding a case without moving the nightly script's pinned count reddens here
    /// instead of silently breaking the scheduled lane.
    /// </summary>
    private static int CountXunitCases(Type testClass)
    {
        int total = 0;
        foreach (MethodInfo method in testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            CustomAttributeData[] attributes = [.. method.GetCustomAttributesData()];
            if (attributes.Any(attribute => attribute.AttributeType.Name == "FactAttribute"))
            {
                total++;
                continue;
            }

            if (attributes.Any(attribute => attribute.AttributeType.Name == "TheoryAttribute"))
            {
                total += attributes.Count(attribute => attribute.AttributeType.Name == "InlineDataAttribute");
            }
        }

        return total;
    }

    private static DriftDiagnostic[] EvaluatePinnedProfile(
        PinnedProfile manifest,
        CatalogClaims claims,
        string pinnedOctokitVersion,
        string pinnedLibGit2SharpVersion,
        string pinnedApiVersion,
        string pinnedProductHeader)
    {
        List<DriftDiagnostic> diagnostics = [];

        if (!string.Equals(manifest.SchemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(new("github_profile_schema_mismatch", "schema_version"));
        }

        if (!string.Equals(manifest.CatalogVersion, claims.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.CatalogPath, CatalogRelativePath, StringComparison.Ordinal))
        {
            diagnostics.Add(new("github_profile_catalog_mismatch", "catalog_version"));
        }

        if (!string.Equals(manifest.OctokitPackageVersion, pinnedOctokitVersion, StringComparison.Ordinal)
            || !claims.PublishesSdkVersion("Octokit", manifest.OctokitPackageVersion))
        {
            diagnostics.Add(new("github_profile_package_mismatch", "octokit_package_version"));
        }

        // The catalog also publishes LibGit2Sharp as governed Forgejo identity; a silent bump would falsify
        // a digest-bound approved catalog just as an Octokit bump would.
        if (!string.Equals(manifest.LibGit2SharpPackageVersion, pinnedLibGit2SharpVersion, StringComparison.Ordinal)
            || !claims.PublishesSdkVersion("LibGit2Sharp", manifest.LibGit2SharpPackageVersion))
        {
            diagnostics.Add(new("github_profile_package_mismatch", "libgit2sharp_package_version"));
        }

        if (!string.Equals(manifest.ProvingAssembly, ResolvingAssembly.GetName().Name, StringComparison.Ordinal))
        {
            diagnostics.Add(new("github_profile_proving_assembly_mismatch", "proving_assembly"));
        }

        if (!string.Equals(manifest.RestApiVersion, pinnedApiVersion, StringComparison.Ordinal)
            || !claims.PublishesApiVersion(manifest.RestApiVersion))
        {
            diagnostics.Add(new("github_profile_api_version_mismatch", "rest_api_version"));
        }

        if (!string.Equals(manifest.ProductHeader, pinnedProductHeader, StringComparison.Ordinal)
            || !claims.PublishesProductHeader(manifest.ProductHeader))
        {
            diagnostics.Add(new("github_profile_product_header_mismatch", "product_header"));
        }

        if (manifest.NetworkCallsPermitted)
        {
            diagnostics.Add(new("github_profile_network_call_permitted", "network_calls_permitted"));
        }

        foreach (IGrouping<string, CoverageRow> group in manifest.Coverage
            .GroupBy(row => row.ProviderNeutralCategory, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            diagnostics.Add(new("github_failure_category_duplicate", group.Key));
        }

        HashSet<string> mapped = manifest.Coverage
            .Select(row => row.ProviderNeutralCategory)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string claimed in claims.FailureCategories.Where(category => !mapped.Contains(category)))
        {
            diagnostics.Add(new("github_failure_category_unmapped", claimed));
        }

        foreach (CoverageRow row in manifest.Coverage)
        {
            if (!claims.FailureCategories.Contains(row.ProviderNeutralCategory, StringComparer.Ordinal)
                || !Enum.IsDefined(typeof(ProviderFailureCategory), row.ProviderNeutralCategory))
            {
                diagnostics.Add(new("github_failure_category_orphaned", row.ProviderNeutralCategory));
                continue;
            }

            MethodInfo? provingMethod = ResolveProvingMethod(row.ProvingFixture);
            if (provingMethod is null || !IsTestMethod(provingMethod))
            {
                diagnostics.Add(new("github_proving_fixture_missing", row.ProviderNeutralCategory));
                continue;
            }

            if (!Enum.IsDefined(typeof(GitHubApiFailureCondition), row.ProvingCondition)
                || !ProvesConditionToCategory(provingMethod, row.ProvingCondition, row.ProviderNeutralCategory))
            {
                diagnostics.Add(new("github_proving_condition_unproven", row.ProviderNeutralCategory));
            }
        }

        return [.. diagnostics];
    }

    private static MethodInfo? ResolveProvingMethod(string provingFixture)
    {
        int separator = provingFixture.LastIndexOf('.');
        if (separator <= 0 || separator == provingFixture.Length - 1)
        {
            return null;
        }

        Type? type = ResolvingAssembly.GetType(provingFixture[..separator], throwOnError: false);
        return type?.GetMethod(provingFixture[(separator + 1)..], BindingFlags.Public | BindingFlags.Instance);
    }

    private static bool IsTestMethod(MethodInfo method) =>
        method.GetCustomAttributesData().Any(attribute =>
            attribute.AttributeType.Name is "FactAttribute" or "TheoryAttribute");

    private static bool ProvesConditionToCategory(MethodInfo method, string condition, string category)
    {
        long expected = Convert.ToInt64(Enum.Parse<ProviderFailureCategory>(category), CultureInfo.InvariantCulture);

        foreach (CustomAttributeData attribute in method.GetCustomAttributesData()
            .Where(attribute => string.Equals(attribute.AttributeType.Name, "InlineDataAttribute", StringComparison.Ordinal)))
        {
            if (attribute.ConstructorArguments.Count == 0
                || Unwrap(attribute.ConstructorArguments[0]) is not IReadOnlyList<CustomAttributeTypedArgument> row
                || row.Count < 2)
            {
                continue;
            }

            if (Unwrap(row[0]) is string observedCondition
                && string.Equals(observedCondition, condition, StringComparison.Ordinal)
                && Unwrap(row[1]) is object observedCategory
                && Convert.ToInt64(observedCategory, CultureInfo.InvariantCulture) == expected)
            {
                return true;
            }
        }

        return false;
    }

    private static object? Unwrap(CustomAttributeTypedArgument argument) =>
        argument.Value is CustomAttributeTypedArgument nested ? Unwrap(nested) : argument.Value;

    private static PinnedProfile LoadManifest()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RepositoryPath(ManifestRelativePath)));
        JsonElement root = document.RootElement;

        CoverageRow[] coverage = root.GetProperty("failureModeCoverage").EnumerateArray()
            .Select(item => new CoverageRow(
                RequiredString(item, "providerNeutralCategory"),
                RequiredString(item, "provingFixture"),
                RequiredString(item, "provingCondition")))
            .ToArray();

        return new PinnedProfile(
            RequiredString(root, "schemaVersion"),
            RequiredString(root, "provider"),
            RequiredString(root, "catalogPath"),
            RequiredString(root, "catalogVersion"),
            RequiredString(root, "packagePinPath"),
            RequiredString(root, "octokitPackageVersion"),
            RequiredString(root, "libGit2SharpPackageVersion"),
            RequiredString(root, "restApiVersion"),
            RequiredString(root, "productHeader"),
            RequiredString(root, "driftLane"),
            RequiredString(root, "provingAssembly"),
            root.GetProperty("networkCallsPermitted").GetBoolean(),
            coverage);
    }

    private static CatalogClaims LoadCatalogClaims()
    {
        string[] lines = File.ReadAllLines(RepositoryPath(CatalogRelativePath));
        string catalogVersion = lines
            .Select(line => Regex.Match(line.Trim(), @"^- Catalog version: `(?<version>[0-9]+\.[0-9]+\.[0-9]+)`$", RegexOptions.CultureInvariant))
            .Where(match => match.Success)
            .Select(match => match.Groups["version"].Value)
            .Single();

        int heading = Array.FindIndex(lines, line => string.Equals(line.Trim(), "Provider-neutral failure categories claimed by the GitHub profile:", StringComparison.Ordinal));
        heading.ShouldBeGreaterThan(-1, "the catalog must publish the claimed provider-neutral failure categories.");

        List<string> categories = [];
        for (int index = heading + 1; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Match match = Regex.Match(line, @"^- `(?<category>[A-Za-z]+)`$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                break;
            }

            categories.Add(match.Groups["category"].Value);
        }

        return new CatalogClaims(catalogVersion, categories, File.ReadAllText(RepositoryPath(CatalogRelativePath)));
    }

    private static string ReadPinnedPackageVersion(string propsRelativePath, string packageId)
    {
        string packagesProps = File.ReadAllText(RepositoryPath(propsRelativePath));
        Match match = Regex.Match(
            packagesProps,
            $@"PackageVersion Include=""{Regex.Escape(packageId)}"" Version=""(?<version>[^""]+)""",
            RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue($"the build must centrally pin the {packageId} package version.");
        return match.Groups["version"].Value;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing manifest property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, propertyName);
        return property.GetString().ShouldNotBeNull();
    }

    private static void AssertMetadataOnly(string value)
    {
        foreach (string forbidden in new[] { "api.github.com", "https://", "http://", "provider_token", "credential_material", "/home/", "/Users/" })
        {
            value.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    private static string RepositoryPath(string relativePath) =>
        Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

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

        throw new InvalidOperationException("Unable to locate the repository root from the test base directory.");
    }

    private sealed record CoverageRow(string ProviderNeutralCategory, string ProvingFixture, string ProvingCondition);

    private sealed record PinnedProfile(
        string SchemaVersion,
        string Provider,
        string CatalogPath,
        string CatalogVersion,
        string PackagePinPath,
        string OctokitPackageVersion,
        string LibGit2SharpPackageVersion,
        string RestApiVersion,
        string ProductHeader,
        string DriftLane,
        string ProvingAssembly,
        bool NetworkCallsPermitted,
        CoverageRow[] Coverage);

    private sealed record CatalogClaims(string CatalogVersion, IReadOnlyList<string> FailureCategories, string CatalogText)
    {
        public bool PublishesSdkVersion(string packageId, string version) =>
            CatalogText.Contains($"{packageId} `{version}`", StringComparison.Ordinal);

        public bool PublishesApiVersion(string apiVersion) =>
            CatalogText.Contains($"X-GitHub-Api-Version: {apiVersion}", StringComparison.Ordinal);

        public bool PublishesProductHeader(string productHeader) =>
            CatalogText.Contains($"`{productHeader}`", StringComparison.Ordinal);
    }

    private sealed record DriftDiagnostic(string Category, string Identifier)
    {
        public override string ToString() =>
            $"github-pinned-profile:{Category}: id={Identifier}; path={ManifestRelativePath}";
    }
}
