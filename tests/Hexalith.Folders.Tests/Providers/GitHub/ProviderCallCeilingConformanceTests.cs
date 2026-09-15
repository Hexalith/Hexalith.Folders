using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

/// <summary>
/// Proves that the OQ4 catalog's call-ceiling table publishes ceilings "exactly as the adapters enforce"
/// them. Every `CC*` row whose enforcing-constant cell names resolvable `Type.Member` references is resolved
/// by reflection (including non-public statics) and compared to the value the row publishes. Rows whose cell
/// is prose rather than a resolvable reference are listed explicitly and asserted against the literal they
/// describe, so no row escapes the check by wording.
/// </summary>
public sealed class ProviderCallCeilingConformanceTests
{
    private const string CatalogRelativePath = "docs/contract/provider-compatibility-catalog.md";

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // Adapter types the catalog's ceiling table may cite, by simple name.
    private static readonly Type[] CitableTypes =
    [
        typeof(GitHubProvider),
        typeof(ForgejoProvider),
        typeof(ForgejoSmartHttpGitTransport),
        typeof(OctokitGitHubApiClient),
        typeof(ForgejoHttpApiClient),
        typeof(ForgejoHttpApiClientFactory),
    ];

    // Ceilings the catalog publishes as absent: each must cite no constant and must carry a recorded gap.
    private static readonly string[] UnenforcedCeilings = ["CC3", "CC9"];

    // Rows whose enforcing-constant cell is prose rather than a resolvable `Type.Member` reference. Each is
    // pinned to the literal the prose describes, and probed against the enforcing code where a probe exists,
    // so the row is still value-checked. Adding a row here instead of citing a constant is a deliberate act.
    private static readonly Dictionary<string, ProseCitedCeiling> ProseCitedCeilings = new(StringComparer.Ordinal)
    {
        // "`ForgejoHttpApiClientFactory` HttpClient timeout" - the managed HttpClient's Timeout, set in the
        // factory's parameterless constructor rather than exposed as a named constant. Probed live.
        ["CC5"] = new(TimeSpan.FromSeconds(30), ProbeForgejoHttpClientTimeout),

        // "`OctokitGitHubApiClient.BoundedRetryAfter` and `ForgejoHttpApiClient` retry-after bounding" - the
        // Forgejo half is a private local bound rather than a named constant, so the published clamp is
        // pinned to the literal both adapters apply. The GitHub half is resolved as a cited reference below.
        ["CC8"] = new(TimeSpan.FromHours(24), null),
    };

    [Fact]
    public void EveryPublishedCallCeilingMatchesTheConstantTheCatalogCites()
    {
        CeilingRow[] rows = ParseCeilingRows();
        rows.Select(row => row.Id).ShouldBe(
            ["CC1", "CC2", "CC3", "CC4", "CC5", "CC6", "CC7", "CC8", "CC9", "CC10", "CC11", "CC12"],
            ignoreOrder: true);

        List<string> unresolved = [];
        List<string> unchecked_ = [];

        foreach (CeilingRow row in rows)
        {
            if (UnenforcedCeilings.Contains(row.Id, StringComparer.Ordinal))
            {
                // An absent ceiling must stay absent: no cited constant, and a recorded gap instead.
                row.EnforcingConstantCell.ShouldBe("none", row.Id);
                row.GapId.ShouldNotBe("none", row.Id);
                continue;
            }

            object expected = ExpectedValue(row);
            string[] citedMembers = CitedMemberReferences(row.EnforcingConstantCell);
            bool checkedSomething = false;

            if (ProseCitedCeilings.TryGetValue(row.Id, out ProseCitedCeiling? prose))
            {
                expected.ShouldBe(prose.ExpectedValue, row.Id);
                prose.Probe?.Invoke().ShouldBe(prose.ExpectedValue, row.Id);
                checkedSomething = prose.Probe is not null;
            }
            else if (citedMembers.Length == 0)
            {
                unchecked_.Add(row.Id);
                continue;
            }

            foreach (string member in citedMembers)
            {
                object? actual = ResolveConstantValue(member);
                if (actual is null)
                {
                    unresolved.Add($"{row.Id}:{member}");
                    continue;
                }

                actual.ShouldBe(expected, $"{row.Id} publishes a value the cited constant {member} does not enforce.");
                checkedSomething = true;
            }

            checkedSomething.ShouldBeTrue($"{row.Id} was published without any value being verified.");
        }

        unresolved.ShouldBeEmpty($"cited constants that could not be resolved: {string.Join(", ", unresolved)}");
        unchecked_.ShouldBeEmpty(
            $"ceilings citing neither a resolvable constant nor a recorded prose exception: {string.Join(", ", unchecked_)}");
    }

    /// <summary>
    /// Reads the value the catalog publishes for a ceiling from its published-value cell, so a published
    /// value can never drift from the enforcing constant without this gate noticing.
    /// </summary>
    private static object ExpectedValue(CeilingRow row)
    {
        string value = row.PublishedValue;

        return value switch
        {
            _ when value.Contains("five-second", StringComparison.Ordinal) => TimeSpan.FromSeconds(5),
            _ when value.Contains("thirty-second", StringComparison.Ordinal) => TimeSpan.FromSeconds(30),
            _ when value.Contains("fifteen-minute", StringComparison.Ordinal) => TimeSpan.FromMinutes(15),
            _ when value.Contains("twenty-four-hour", StringComparison.Ordinal) => TimeSpan.FromHours(24),
            _ when value.Contains("one hundred changes", StringComparison.Ordinal) => 100L,
            _ when value.Contains("one MiB", StringComparison.Ordinal) => 1024L * 1024,
            _ when value.Contains("ten MiB", StringComparison.Ordinal) => 10L * 1024 * 1024,
            _ => throw new InvalidOperationException($"{row.Id} publishes a value this gate cannot interpret."),
        };
    }

    private static object? ResolveConstantValue(string memberReference)
    {
        string[] parts = memberReference.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        Type? type = CitableTypes.FirstOrDefault(candidate => string.Equals(candidate.Name, parts[0], StringComparison.Ordinal));
        if (type is null)
        {
            return null;
        }

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        FieldInfo? field = type.GetField(parts[1], Flags);
        if (field is not null)
        {
            return Normalize(field.GetValue(null));
        }

        PropertyInfo? property = type.GetProperty(parts[1], Flags);
        if (property is not null)
        {
            return Normalize(property.GetValue(null));
        }

        // A clamp expressed as a bounding method rather than a constant: invoke it with a value beyond the
        // clamp so the ceiling itself is returned.
        MethodInfo? method = type.GetMethod(parts[1], Flags, [typeof(TimeSpan)]);
        return method is null ? null : Normalize(method.Invoke(null, [TimeSpan.FromDays(365)]));
    }

    // Constants are declared as int, long or TimeSpan; normalize every integral width to long so a published
    // value compares equal regardless of how the constant was declared.
    private static object? Normalize(object? value) => value switch
    {
        null => null,
        int or long or short or byte => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        _ => value,
    };

    /// <summary>
    /// Reads the managed HttpClient deadline the Forgejo factory actually applies, without adding any
    /// production surface: the default factory's client supplier is invoked and its Timeout observed.
    /// </summary>
    private static object? ProbeForgejoHttpClientTimeout()
    {
        ForgejoHttpApiClientFactory factory = new();
        FieldInfo supplier = typeof(ForgejoHttpApiClientFactory)
            .GetField("_httpClientFactory", BindingFlags.Instance | BindingFlags.NonPublic)
            .ShouldNotBeNull();

        using HttpClient client = ((Func<HttpClient>)supplier.GetValue(factory).ShouldNotBeNull())();
        return client.Timeout;
    }

    /// <summary>
    /// Extracts the backticked `Type.Member` references from an enforcing-constant cell. A backticked token
    /// that is not a two-part dotted reference (for example a bare type name inside prose) is ignored here
    /// and covered by the prose-cited list instead.
    /// </summary>
    private static string[] CitedMemberReferences(string cell) =>
        [.. Regex.Matches(cell, "`(?<reference>[A-Za-z0-9_]+\\.[A-Za-z0-9_]+)`", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["reference"].Value)];

    private static CeilingRow[] ParseCeilingRows()
    {
        Regex rowPattern = new(
            @"^\|\s*`(?<id>CC\d+)`\s*\|\s*(?<scope>[^|]+)\|\s*(?<value>[^|]+)\|\s*(?<constant>[^|]+)\|\s*(?<gap>[^|]+)\|$",
            RegexOptions.CultureInvariant);

        return
        [
            .. File.ReadAllLines(Path.Combine(RepositoryRoot, CatalogRelativePath.Replace('/', Path.DirectorySeparatorChar)))
                .Select(line => rowPattern.Match(line.Trim()))
                .Where(match => match.Success)
                .Select(match => new CeilingRow(
                    match.Groups["id"].Value,
                    match.Groups["value"].Value.Trim(),
                    match.Groups["constant"].Value.Trim(),
                    match.Groups["gap"].Value.Trim().Trim('`'))),
        ];
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

    private sealed record CeilingRow(string Id, string PublishedValue, string EnforcingConstantCell, string GapId);

    private sealed record ProseCitedCeiling(object ExpectedValue, Func<object?>? Probe);
}
