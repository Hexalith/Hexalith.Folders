using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

/// <summary>
/// OQ4 content gate for the canonical provider compatibility catalog. It proves that every required
/// profile section, governed provider, published ceiling, and recorded gap appears exactly once, and
/// that every published ceiling either cites an enforcing constant or is recorded as a numbered gap.
/// Diagnostics stay metadata-only: they report the section, ceiling, or gap identifier, never content.
/// </summary>
public sealed class ProviderCompatibilityCatalogContractTests
{
    private const string CatalogPath = "docs/contract/provider-compatibility-catalog.md";
    private const string NoEnforcingConstant = "none";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // Sections the approved catalog version 1.0.0 publishes. Dropping one is a governance regression,
    // not a cleanup: the OQ4 approval is bound to this shape.
    private static readonly string[] RequiredSections =
    [
        "## Catalog identity and OQ4 governance",
        "## Product and instance identity profile",
        "## Call-ceiling profile",
        "## Readiness-outcome profile",
        "## Recorded catalog gaps",
        "## GitHub hermetic drift lane",
        "## GitHub profile",
        "## Forgejo profile",
        "## Ownership and readiness limits",
    ];

    private static readonly string[] GovernedProviders = ["github", "forgejo"];

    private static readonly string[] RequiredCeilingIds =
    [
        "CC1",
        "CC2",
        "CC3",
        "CC4",
        "CC5",
        "CC6",
        "CC7",
        "CC8",
        "CC9",
        "CC10",
        "CC11",
        "CC12",
    ];

    private static readonly string[] RequiredGapIds = ["PG1", "PG2", "PG3"];

    private static readonly string[] RequiredReadinessRows =
    [
        "readiness_validation",
        "provider_support_evidence",
        "repository_creation",
        "repository_binding",
        "branch_ref_inspection",
        "file_mutation_support",
        "commit_support",
        "status_query",
        "cleanup_expiration",
    ];

    // Governance statements that keep the catalog honest about what approval does and does not buy.
    private static readonly string[] RequiredGovernanceStatements =
    [
        "- Catalog version: `1.0.0`",
        "- OQ4 authority approval: Provider,",
        "- OQ4 authority approval: Architecture,",
        "- OQ4 authority approval: PM,",
        "- OQ4 reopen policy:",
        "- C12 evidence standard:",
        "docs/contract/oq4-provider-compatibility-evidence.yaml",
        "Credentialed live provider runs against GitHub and Forgejo are reported as explicitly not run",
        "Stories 3.3, 3.14, 12.1, and 12.3 remain",
    ];

    [Fact]
    public void CatalogPublishesEverySectionCeilingGapAndReadinessRowExactlyOnce()
    {
        string catalog = ReadCatalog();
        CatalogDiagnostic[] diagnostics = EvaluateCatalog(ParseCatalog(catalog));

        foreach (CatalogDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        foreach (string statement in RequiredGovernanceStatements)
        {
            catalog.ShouldContain(statement, Case.Sensitive);
        }

        CatalogModel model = ParseCatalog(catalog);

        // Set equality, not just missing/duplicate: an unapproved new `## ` section is catalog drift against
        // a digest-bound approval, so it must redden here rather than pass silently.
        model.Sections.ShouldBe(RequiredSections, ignoreOrder: true);
        model.Ceilings.Select(ceiling => ceiling.Id).ShouldBe(RequiredCeilingIds, ignoreOrder: true);
        model.GapIds.ShouldBe(RequiredGapIds, ignoreOrder: true);
        model.ReadinessRows.ShouldBe(RequiredReadinessRows, ignoreOrder: true);

        // Every recorded gap must be referenced by the ceiling it explains; an unreferenced gap is a
        // dangling claim rather than published evidence.
        model.GapIds.ShouldBeSubsetOf(model.Ceilings.Select(ceiling => ceiling.GapId).Where(gap => gap != NoEnforcingConstant));
    }

    [Fact]
    public void CatalogContentGateFailsClosedForMissingDuplicateAndUnpinnedEvidence()
    {
        CatalogModel model = ParseCatalog(ReadCatalog());

        CatalogDiagnostic[] missingSection = EvaluateCatalog(
            model with { Sections = model.Sections.Where(section => section != "## Call-ceiling profile").ToArray() });
        missingSection.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_catalog_section_missing" && diagnostic.Identifier == "## Call-ceiling profile");

        CatalogDiagnostic[] duplicateSection = EvaluateCatalog(
            model with { Sections = [.. model.Sections, "## Recorded catalog gaps"] });
        duplicateSection.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_catalog_section_duplicate" && diagnostic.Identifier == "## Recorded catalog gaps");

        CeilingRow droppedCeiling = model.Ceilings.Single(ceiling => ceiling.Id == "CC1");
        CatalogDiagnostic[] missingCeiling = EvaluateCatalog(
            model with { Ceilings = model.Ceilings.Where(ceiling => ceiling.Id != "CC1").ToArray() });
        missingCeiling.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_ceiling_missing" && diagnostic.Identifier == "CC1");

        CatalogDiagnostic[] duplicateCeiling = EvaluateCatalog(
            model with { Ceilings = [.. model.Ceilings, droppedCeiling] });
        duplicateCeiling.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_ceiling_duplicate" && diagnostic.Identifier == "CC1");

        // A published ceiling with no enforcing constant and no recorded gap is an unpinned claim.
        CatalogDiagnostic[] unpinnedCeiling = EvaluateCatalog(
            model with
            {
                Ceilings = [.. model.Ceilings.Where(ceiling => ceiling.Id != "CC1"), droppedCeiling with { EnforcingConstant = NoEnforcingConstant, GapId = NoEnforcingConstant }],
            });
        unpinnedCeiling.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_ceiling_unpinned" && diagnostic.Identifier == "CC1");

        // A ceiling that cites a gap the catalog never records fails closed too.
        CatalogDiagnostic[] unknownGapReference = EvaluateCatalog(
            model with
            {
                Ceilings = [.. model.Ceilings.Where(ceiling => ceiling.Id != "CC1"), droppedCeiling with { EnforcingConstant = NoEnforcingConstant, GapId = "PG9" }],
            });
        unknownGapReference.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_ceiling_unpinned" && diagnostic.Identifier == "CC1");

        CatalogDiagnostic[] unknownProviderScope = EvaluateCatalog(
            model with
            {
                Ceilings = [.. model.Ceilings.Where(ceiling => ceiling.Id != "CC1"), droppedCeiling with { ProviderScope = "gitlab" }],
            });
        unknownProviderScope.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_ceiling_provider_unknown" && diagnostic.Identifier == "CC1");

        CatalogDiagnostic[] missingGap = EvaluateCatalog(
            model with { GapIds = model.GapIds.Where(gap => gap != "PG2").ToArray() });
        missingGap.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_gap_missing" && diagnostic.Identifier == "PG2");

        CatalogDiagnostic[] duplicateGap = EvaluateCatalog(
            model with { GapIds = [.. model.GapIds, "PG2"] });
        duplicateGap.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_gap_duplicate" && diagnostic.Identifier == "PG2");

        CatalogDiagnostic[] missingReadiness = EvaluateCatalog(
            model with { ReadinessRows = model.ReadinessRows.Where(row => row != "status_query").ToArray() });
        missingReadiness.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_readiness_row_missing" && diagnostic.Identifier == "status_query");

        CatalogDiagnostic[] duplicateReadiness = EvaluateCatalog(
            model with { ReadinessRows = [.. model.ReadinessRows, "status_query"] });
        duplicateReadiness.ShouldContain(diagnostic =>
            diagnostic.Category == "oq4_readiness_row_duplicate" && diagnostic.Identifier == "status_query");

        foreach (CatalogDiagnostic diagnostic in missingSection
            .Concat(duplicateSection)
            .Concat(missingCeiling)
            .Concat(duplicateCeiling)
            .Concat(unpinnedCeiling)
            .Concat(unknownGapReference)
            .Concat(unknownProviderScope)
            .Concat(missingGap)
            .Concat(duplicateGap)
            .Concat(missingReadiness)
            .Concat(duplicateReadiness))
        {
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void CatalogMakesNoCredentialedLiveProviderCompletionClaim()
    {
        string catalog = ReadCatalog();

        foreach (string forbidden in new[]
                 {
                     "a credentialed live GitHub mutation run occurred",
                     "credentialed live provider run completed",
                     "live provider evidence is complete",
                     "provider-ready status is complete",
                 })
        {
            catalog.ShouldNotContain(forbidden, Case.Insensitive);
        }

        catalog.ShouldContain("this catalog does not claim a live GitHub mutation run occurred", Case.Sensitive);
        catalog.ShouldContain("remain residual provider-ready debt", Case.Sensitive);
    }

    private static CatalogDiagnostic[] EvaluateCatalog(CatalogModel model)
    {
        List<CatalogDiagnostic> diagnostics = [];

        AppendInventoryDiagnostics(diagnostics, model.Sections, RequiredSections, "oq4_catalog_section");
        AppendInventoryDiagnostics(diagnostics, model.Ceilings.Select(ceiling => ceiling.Id).ToArray(), RequiredCeilingIds, "oq4_ceiling");
        AppendInventoryDiagnostics(diagnostics, model.GapIds, RequiredGapIds, "oq4_gap");
        AppendInventoryDiagnostics(diagnostics, model.ReadinessRows, RequiredReadinessRows, "oq4_readiness_row");

        HashSet<string> recordedGaps = model.GapIds.ToHashSet(StringComparer.Ordinal);

        foreach (CeilingRow ceiling in model.Ceilings)
        {
            bool citesConstant = !string.Equals(ceiling.EnforcingConstant, NoEnforcingConstant, StringComparison.Ordinal)
                && ceiling.EnforcingConstant.Length > 0;
            bool recordsGap = !string.Equals(ceiling.GapId, NoEnforcingConstant, StringComparison.Ordinal)
                && recordedGaps.Contains(ceiling.GapId);

            if (!citesConstant && !recordsGap)
            {
                diagnostics.Add(new("oq4_ceiling_unpinned", ceiling.Id));
            }

            string[] scopes = ceiling.ProviderScope
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (scopes.Length == 0 || scopes.Any(scope => !GovernedProviders.Contains(scope, StringComparer.Ordinal)))
            {
                diagnostics.Add(new("oq4_ceiling_provider_unknown", ceiling.Id));
            }
        }

        return [.. diagnostics];
    }

    private static void AppendInventoryDiagnostics(
        List<CatalogDiagnostic> diagnostics,
        IReadOnlyList<string> observed,
        IReadOnlyList<string> required,
        string categoryPrefix)
    {
        Dictionary<string, int> counts = observed
            .GroupBy(item => item, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (string expected in required)
        {
            if (!counts.TryGetValue(expected, out int count))
            {
                diagnostics.Add(new($"{categoryPrefix}_missing", expected));
            }
            else if (count > 1)
            {
                diagnostics.Add(new($"{categoryPrefix}_duplicate", expected));
            }
        }
    }

    private static CatalogModel ParseCatalog(string catalog)
    {
        string[] lines = catalog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        string[] sections = lines
            .Select(line => line.TrimEnd())
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .ToArray();

        CeilingRow[] ceilings = lines
            .Select(line => CeilingRowPattern.Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => new CeilingRow(
                match.Groups["id"].Value,
                match.Groups["scope"].Value.Trim(),
                match.Groups["constant"].Value.Trim(),
                match.Groups["gap"].Value.Trim().Trim('`')))
            .ToArray();

        string[] gapIds = lines
            .Select(line => GapRowPattern.Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToArray();

        string[] readinessRows = lines
            .Select(line => ReadinessRowPattern.Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToArray();

        return new CatalogModel(sections, ceilings, gapIds, readinessRows);
    }

    private static string ReadCatalog() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, CatalogPath.Replace('/', Path.DirectorySeparatorChar)));

    private static void AssertMetadataOnly(string value)
    {
        foreach (string forbidden in new[] { "https://", "http://", "provider_token", "credential_material", "/home/", "/Users/" })
        {
            value.ShouldNotContain(forbidden, Case.Insensitive);
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

        throw new InvalidOperationException("Unable to locate the repository root from the test base directory.");
    }

    private static readonly Regex CeilingRowPattern = new(
        @"^\|\s*`(?<id>CC\d+)`\s*\|\s*(?<scope>[^|]+)\|\s*(?<value>[^|]+)\|\s*(?<constant>[^|]+)\|\s*(?<gap>[^|]+)\|$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GapRowPattern = new(
        @"^\|\s*`(?<id>PG\d+)`\s*\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReadinessRowPattern = new(
        @"^\|\s*`(?<id>[a-z][a-z_]*)`\s*\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record CeilingRow(string Id, string ProviderScope, string EnforcingConstant, string GapId);

    private sealed record CatalogModel(
        IReadOnlyList<string> Sections,
        IReadOnlyList<CeilingRow> Ceilings,
        IReadOnlyList<string> GapIds,
        IReadOnlyList<string> ReadinessRows);

    private sealed record CatalogDiagnostic(string Category, string Identifier)
    {
        public override string ToString() =>
            $"oq4-provider-compatibility-catalog:{Category}: id={Identifier}; path={CatalogPath}";
    }
}
