using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class FixtureContractTests
{
    private static readonly string[] JsonFixturePaths =
    [
        "tests/fixtures/audit-leakage-corpus.json",
        "tests/fixtures/idempotency-encoding-corpus.json",
        "tests/fixtures/parity-contract.schema.json"
    ];

    private static readonly string[] OwnershipFields =
    [
        "owner_workstream",
        "future_test_use",
        "known_omissions",
        "mutation_rules",
        "non_policy_placeholder",
        "synthetic_data_only"
    ];

    private static readonly Regex SecretShapedValue = new(
        @"(AKIA[0-9A-Z]{16})|(ASIA[0-9A-Z]{16})|(gh[pousr]_[A-Za-z0-9_]{30,})|(-----BEGIN [A-Z ]*PRIVATE KEY-----)|(AccountKey=)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void NormativeFixturesAreParseableAndCarryOwnershipMetadata()
    {
        string root = RepositoryRoot();

        foreach (string relativePath in JsonFixturePaths)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, NormalizeForFileSystem(relativePath))));

            JsonElement ownership = document.RootElement.GetProperty("ownership");
            AssertOwnershipMetadata(relativePath, ownership);
        }

        string previousSpinePath = Path.Combine(root, "tests", "fixtures", "previous-spine.yaml");
        Dictionary<string, string> previousSpine = ParseTopLevelYamlScalarMap(File.ReadAllLines(previousSpinePath));

        previousSpine.ShouldContainKey("version");
        previousSpine.ShouldContainKey("source_marker");
        previousSpine.ShouldContainKey("operations");
        previousSpine.ShouldNotContainKey("openapi");
        previousSpine["operations"].ShouldBe("[]");
    }

    [Fact]
    public void DeferredArtifactAreasCarryMachineCheckableOwnershipNotes()
    {
        string root = RepositoryRoot();
        string[] notePaths =
        [
            "tests/load/README.md",
            "tests/tools/parity-oracle-generator/README.md",
            "docs/exit-criteria/_template.md",
            "docs/adrs/0000-template.md"
        ];

        foreach (string relativePath in notePaths)
        {
            string content = File.ReadAllText(Path.Combine(root, NormalizeForFileSystem(relativePath)));

            foreach (string field in OwnershipFields)
            {
                content.ShouldContain(field, Case.Sensitive, $"{relativePath} should expose {field}.");
            }
        }
    }

    [Fact]
    public void SecretShapedFixtureValuesRequireSyntheticSentinelTags()
    {
        string root = RepositoryRoot();
        string auditCorpusPath = Path.Combine(root, "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(auditCorpusPath));

        foreach (JsonElement sample in document.RootElement.GetProperty("sentinel_samples").EnumerateArray())
        {
            sample.GetProperty("synthetic_sentinel").GetBoolean().ShouldBeTrue();
            sample.GetProperty("synthetic_data_only").GetBoolean().ShouldBeTrue();

            string value = sample.GetProperty("value").GetString() ?? string.Empty;
            if (SecretShapedValue.IsMatch(value))
            {
                sample.GetProperty("classification").GetString().ShouldBe("synthetic-sentinel");
            }
        }
    }

    [Fact]
    public void SeededFixturesAvoidRealDataAndProductionMaterial()
    {
        string root = RepositoryRoot();
        string[] relativePaths =
        [
            .. JsonFixturePaths,
            "tests/fixtures/previous-spine.yaml",
            "tests/load/README.md",
            "tests/tools/parity-oracle-generator/README.md",
            "docs/exit-criteria/_template.md",
            "docs/adrs/0000-template.md"
        ];

        foreach (string relativePath in relativePaths)
        {
            string content = File.ReadAllText(Path.Combine(root, NormalizeForFileSystem(relativePath)));

            content.ShouldNotContain("BEGIN PRIVATE KEY", Case.Insensitive, $"{relativePath} must not contain private key material.");
            content.ShouldNotContain("DefaultEndpointsProtocol=", Case.Insensitive, $"{relativePath} must not contain storage connection strings.");
            content.ShouldNotContain("password=", Case.Insensitive, $"{relativePath} must not contain credential material.");
            content.ShouldNotContain("https://api.github.com", Case.Insensitive, $"{relativePath} must not contain production provider URLs.");
            content.ShouldNotContain("https://github.com/Hexalith", Case.Insensitive, $"{relativePath} must not contain production repository URLs.");
            content.ShouldNotContain("diff --git", Case.Insensitive, $"{relativePath} must not contain diffs or file contents.");
        }
    }

    private static void AssertOwnershipMetadata(string relativePath, JsonElement ownership)
    {
        foreach (string field in OwnershipFields)
        {
            ownership.TryGetProperty(field, out JsonElement value).ShouldBeTrue($"{relativePath} should expose ownership.{field}.");
            value.ValueKind.ShouldNotBe(JsonValueKind.Null, $"{relativePath} ownership.{field} should be populated.");
        }

        ownership.GetProperty("non_policy_placeholder").GetBoolean().ShouldBeTrue();
        ownership.GetProperty("synthetic_data_only").GetBoolean().ShouldBeTrue();
    }

    private static Dictionary<string, string> ParseTopLevelYamlScalarMap(string[] lines)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (string line in lines)
        {
            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                values[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return values;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
