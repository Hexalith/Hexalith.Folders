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

    // Patch 5: added github_pat_ (fine-grained PATs) and ghe_ (Enterprise tokens)
    private static readonly Regex SecretShapedValue = new(
        @"(AKIA[0-9A-Z]{16})|(ASIA[0-9A-Z]{16})|(gh[pousr]_[A-Za-z0-9_]{30,})|(github_pat_[A-Za-z0-9_]{30,})|(ghe_[A-Za-z0-9_]{30,})|(-----BEGIN [A-Z ]*PRIVATE KEY-----)|(AccountKey=)|(\bclient_secret\b[""']?\s*[:=])|(\bclientSecret\b[""']?\s*[:=])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Patch 8: added pwd=, passwd=, apikey=, api_key= variants
    private static readonly string[] CredentialMaterialMarkers =
    [
        "BEGIN PRIVATE KEY",
        "DefaultEndpointsProtocol=",
        "password=",
        "pwd=",
        "passwd=",
        "apikey=",
        "api_key=",
        "client_secret=",
        "client_secret:",
        "clientSecret=",
        "clientSecret\":",
        "https://api.github.com",
        "https://github.com/Hexalith",
        "diff --git"
    ];

    [Fact]
    public void NormativeFixturesAreParseableAndCarryOwnershipMetadata()
    {
        string root = RepositoryRoot();

        foreach (string relativePath in JsonFixturePaths)
        {
            string absolutePath = Path.Combine(root, NormalizeForFileSystem(relativePath));
            using JsonDocument document = JsonDocument.Parse(ReadFixtureFile(absolutePath));

            // Patch 4: explicit existence check before property access
            document.RootElement.TryGetProperty("ownership", out JsonElement ownership)
                .ShouldBeTrue($"{relativePath} must contain a top-level 'ownership' object.");
            AssertOwnershipMetadata(relativePath, ownership);
        }

        // Patch 4: structural guards so deleted arrays throw assertion failures, not KeyNotFoundException
        string auditAbsPath = Path.Combine(root, "tests", "fixtures", "audit-leakage-corpus.json");
        using (JsonDocument auditDoc = JsonDocument.Parse(ReadFixtureFile(auditAbsPath)))
        {
            auditDoc.RootElement.TryGetProperty("sentinel_samples", out _)
                .ShouldBeTrue("audit-leakage-corpus.json must contain a 'sentinel_samples' array.");
        }

        string idempotencyAbsPath = Path.Combine(root, "tests", "fixtures", "idempotency-encoding-corpus.json");
        using (JsonDocument idempotencyDoc = JsonDocument.Parse(ReadFixtureFile(idempotencyAbsPath)))
        {
            idempotencyDoc.RootElement.TryGetProperty("cases", out _)
                .ShouldBeTrue("idempotency-encoding-corpus.json must contain a 'cases' array.");
        }

        string parityAbsPath = Path.Combine(root, "tests", "fixtures", "parity-contract.schema.json");
        using (JsonDocument parityDoc = JsonDocument.Parse(ReadFixtureFile(parityAbsPath)))
        {
            parityDoc.RootElement.TryGetProperty("required", out _)
                .ShouldBeTrue("parity-contract.schema.json must contain a 'required' array.");
            parityDoc.RootElement.TryGetProperty("properties", out _)
                .ShouldBeTrue("parity-contract.schema.json must contain a 'properties' object.");
        }

        string previousSpinePath = Path.Combine(root, "tests", "fixtures", "previous-spine.yaml");
        string rawPreviousSpine = ReadFixtureFile(previousSpinePath);
        Dictionary<string, string> previousSpine = ParseTopLevelYamlScalarMap(File.ReadAllLines(previousSpinePath));

        previousSpine.ShouldContainKey("version");
        previousSpine.ShouldContainKey("source_marker");
        previousSpine.ShouldContainKey("operations");
        previousSpine.ShouldNotContainKey("openapi");

        // Patch 3: direct line assertion guards against multi-line YAML block sequences that the hand-rolled parser cannot detect
        rawPreviousSpine.ShouldContain("operations: []", Case.Sensitive,
            "previous-spine.yaml must declare operations as an empty flow sequence on a single line.");

        // Patch 6: verify all ownership fields are present and boolean flags are explicitly true (AC6 gap)
        foreach (string field in OwnershipFields)
        {
            rawPreviousSpine.ShouldContain(field, Case.Sensitive,
                $"previous-spine.yaml should contain ownership field '{field}'.");
        }
        rawPreviousSpine.ShouldContain("non_policy_placeholder: true", Case.Sensitive,
            "previous-spine.yaml ownership.non_policy_placeholder must be true.");
        rawPreviousSpine.ShouldContain("synthetic_data_only: true", Case.Sensitive,
            "previous-spine.yaml ownership.synthetic_data_only must be true.");
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
            string absolutePath = Path.Combine(root, NormalizeForFileSystem(relativePath));
            string content = ReadFixtureFile(absolutePath);

            foreach (string field in OwnershipFields)
            {
                content.ShouldContain(field, Case.Sensitive, $"{relativePath} should expose {field}.");
            }

            // Patch 9: verify boolean flags are explicitly true, not merely present as field names
            content.ShouldContain("non_policy_placeholder: true", Case.Sensitive,
                $"{relativePath} non_policy_placeholder must be explicitly set to true.");
            content.ShouldContain("synthetic_data_only: true", Case.Sensitive,
                $"{relativePath} synthetic_data_only must be explicitly set to true.");
        }
    }

    [Fact]
    public void SecretShapedFixtureValuesRequireSyntheticSentinelTags()
    {
        string root = RepositoryRoot();
        string auditCorpusPath = Path.Combine(root, "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(ReadFixtureFile(auditCorpusPath));

        // Patch 4: explicit existence check before enumeration
        document.RootElement.TryGetProperty("sentinel_samples", out JsonElement sentinelSamples)
            .ShouldBeTrue("audit-leakage-corpus.json must contain a 'sentinel_samples' array.");

        foreach (JsonElement sample in sentinelSamples.EnumerateArray())
        {
            string sampleId = sample.TryGetProperty("id", out JsonElement idEl)
                ? idEl.GetString() ?? "unknown"
                : "unknown";

            sample.GetProperty("synthetic_sentinel").GetBoolean()
                .ShouldBeTrue($"Sample '{sampleId}' must have synthetic_sentinel: true.");
            sample.GetProperty("synthetic_data_only").GetBoolean()
                .ShouldBeTrue($"Sample '{sampleId}' must have synthetic_data_only: true.");

            string value = sample.GetProperty("value").GetString() ?? string.Empty;

            // Patch 2: enforce classification for all samples, not only regex-matched ones
            string expectedClassification = SecretShapedValue.IsMatch(value)
                ? "synthetic-sentinel"
                : "metadata-placeholder";
            sample.GetProperty("classification").GetString()
                .ShouldBe(expectedClassification,
                    $"Sample '{sampleId}' must have classification '{expectedClassification}'.");
        }
    }

    [Fact]
    public void SecretShapedValueRegexCoversOAuthClientSecretMarkers()
    {
        SecretShapedValue.IsMatch("client_secret=SYNTHETIC-VALUE").ShouldBeTrue();
        SecretShapedValue.IsMatch("\"clientSecret\":\"SYNTHETIC-VALUE\"").ShouldBeTrue();
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
            string absolutePath = Path.Combine(root, NormalizeForFileSystem(relativePath));
            string content = ReadFixtureFile(absolutePath);

            foreach (string marker in CredentialMaterialMarkers)
            {
                content.ShouldNotContain(marker, Case.Insensitive,
                    $"{relativePath} must not contain credential material, production endpoints, diffs, or file contents.");
            }
        }
    }

    private static void AssertOwnershipMetadata(string relativePath, JsonElement ownership)
    {
        foreach (string field in OwnershipFields)
        {
            ownership.TryGetProperty(field, out JsonElement value)
                .ShouldBeTrue($"{relativePath} should expose ownership.{field}.");
            value.ValueKind.ShouldNotBe(JsonValueKind.Null,
                $"{relativePath} ownership.{field} should be populated.");
        }

        ownership.GetProperty("non_policy_placeholder").GetBoolean().ShouldBeTrue();
        ownership.GetProperty("synthetic_data_only").GetBoolean().ShouldBeTrue();
    }

    // Patch 7: helper that asserts file existence before reading, giving a clear failure message
    private static string ReadFixtureFile(string absolutePath)
    {
        File.Exists(absolutePath).ShouldBeTrue($"Fixture file not found: '{absolutePath}'");
        return File.ReadAllText(absolutePath);
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

    // Patch 1: bounded walk with descriptive error; prevents silent wrong-root on monorepo or flat-publish CI layouts
    private static string RepositoryRoot()
    {
        const int MaxAncestors = 20;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        string searchStart = directory.FullName;
        int ancestors = 0;
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            if (++ancestors > MaxAncestors)
            {
                throw new InvalidOperationException(
                    $"Repository root not found within {MaxAncestors} ancestor directories of '{searchStart}'.");
            }

            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            $"Repository root not found searching from '{searchStart}'.");
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
