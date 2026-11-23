using System.Text.Json;
using System.Text.RegularExpressions;
using Hexalith.Folders.Testing.Tests.Helpers;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

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
        "clientSecret\":"
    ];

    [Fact]
    public void NormativeFixturesAreParseableAndCarryOwnershipMetadata()
    {
        string root = RepositoryArtifacts.FindRoot();

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
        YamlMappingNode previousSpine = ParsePreviousSpine(ReadFixtureFile(previousSpinePath), "previous-spine.yaml");

        previousSpine.Children.ShouldContainKey(new YamlScalarNode("version"));
        previousSpine.Children.ShouldContainKey(new YamlScalarNode("source_marker"));
        YamlSequenceNode operations = previousSpine.Children[new YamlScalarNode("operations")]
            .ShouldBeOfType<YamlSequenceNode>("previous-spine.yaml operations");
        operations.Children.ShouldNotBeEmpty("previous-spine.yaml must include captured operations.");
        foreach (YamlNode operation in operations.Children)
        {
            operation.ShouldBeOfType<YamlMappingNode>("previous-spine.yaml operation")
                .Children.ShouldContainKey(new YamlScalarNode("operation_id"));
        }

        YamlMappingNode baselineOwnership = previousSpine.Children[new YamlScalarNode("ownership")]
            .ShouldBeOfType<YamlMappingNode>("previous-spine.yaml ownership");
        foreach (string field in OwnershipFields)
        {
            baselineOwnership.Children.ShouldContainKey(new YamlScalarNode(field), $"previous-spine.yaml ownership.{field}");
        }

        foreach (string field in new[] { "owner_workstream", "future_test_use" })
        {
            CheckOwnershipText(baselineOwnership.Children[new YamlScalarNode(field)], field);
        }

        foreach (string field in new[] { "known_omissions", "mutation_rules" })
        {
            YamlSequenceNode values = baselineOwnership.Children[new YamlScalarNode(field)].ShouldBeOfType<YamlSequenceNode>(field);
            values.Children.ShouldNotBeEmpty($"previous-spine.yaml ownership.{field}");
            foreach (YamlNode value in values.Children)
            {
                CheckOwnershipText(value, field);
            }
        }

        foreach (string flag in new[] { "non_policy_placeholder", "synthetic_data_only" })
        {
            YamlScalarNode value = baselineOwnership.Children[new YamlScalarNode(flag)].ShouldBeOfType<YamlScalarNode>();
            value.Value.ShouldBe("true", $"previous-spine.yaml ownership.{flag}");
            value.Style.ShouldBe(YamlDotNet.Core.ScalarStyle.Plain, $"previous-spine.yaml ownership.{flag} must be boolean.");
        }
    }

    [Theory]
    [InlineData("openapi: 3.1.0", "openapi")]
    [InlineData("root: [{'openapi_version': 3.1.0}]", "openapi_version")]
    [InlineData("root: {'OpenAPI-Version': 3.1.0}", "OpenAPI-Version")]
    [InlineData("root: {openapiVersion: 3.1.0}", "openapiVersion")]
    public void PreviousSpineRejectsOpenApiVersionKeys(string yaml, string key)
    {
        Should.Throw<InvalidOperationException>(() => ParsePreviousSpine(yaml, "previous-spine.yaml"))
            .Message.ShouldContain($"'{key}'", Case.Sensitive);
    }

    [Fact]
    public void PreviousSpinePermitsOpenApiReferencesInValues()
        => ParsePreviousSpine("source_marker: 'openapi: 3.1.0'", "previous-spine.yaml")
            .Children.ShouldContainKey(new YamlScalarNode("source_marker"));

    [Fact]
    public void DeferredArtifactAreasCarryMachineCheckableOwnershipNotes()
    {
        string root = RepositoryArtifacts.FindRoot();
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
        string root = RepositoryArtifacts.FindRoot();
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

            string classification = sample.GetProperty("classification").GetString() ?? string.Empty;
            if (SecretShapedValue.IsMatch(value))
            {
                classification.ShouldBe("synthetic-sentinel",
                    $"Secret-shaped sample '{sampleId}' must have classification 'synthetic-sentinel'.");
            }

            JsonElement vocabulary = document.RootElement.GetProperty("classification_vocabulary");
            vocabulary.EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ShouldContain(classification, $"Sample '{sampleId}' must use the corpus classification vocabulary.");
        }
    }

    [Fact]
    public void AuditLeakageCorpusUsesReviewerVisibleClassificationVocabulary()
    {
        string root = RepositoryArtifacts.FindRoot();
        string auditCorpusPath = Path.Combine(root, "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(ReadFixtureFile(auditCorpusPath));

        string[] vocabulary = document.RootElement.GetProperty("classification_vocabulary")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

        vocabulary.ShouldNotBeEmpty("Story 1.15 promotes the audit corpus from placeholder-only metadata to the authoritative safety vocabulary.");

        JsonElement samples = document.RootElement.GetProperty("sentinel_samples");
        foreach (JsonElement sample in samples.EnumerateArray())
        {
            string sampleId = sample.GetProperty("id").GetString() ?? "unknown";
            string classification = sample.GetProperty("classification").GetString() ?? string.Empty;
            string[] surfaces = sample.GetProperty("forbidden_output_surfaces")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();

            vocabulary.ShouldContain(classification, $"Sample '{sampleId}' must use the reviewer-visible corpus vocabulary.");
            surfaces.ShouldNotBeEmpty($"Sample '{sampleId}' must declare at least one forbidden output surface.");
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
        string root = RepositoryArtifacts.FindRoot();
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

            AssertFixtureContent(relativePath, content);
        }
    }

    [Fact]
    public void FixtureCheckAcceptsSyntheticPatchContentAndStillDetectsEmbeddedCredentials()
    {
        const string patch = "diff --git a/example b/example\n+synthetic example";
        AssertFixtureContent("example.patch", patch);
        Should.Throw<ShouldAssertException>(() => AssertFixtureContent("example.patch", patch + "\n+password=example"))
            .Message.ShouldContain("marker 'password='");
    }

    private static void AssertFixtureContent(string relativePath, string content)
    {
        foreach (string marker in CredentialMaterialMarkers)
        {
            content.Contains(marker, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"{relativePath} must not contain credential material matching marker '{marker}'.");
        }

        foreach (string endpoint in new[] { "https://api.github.com", "https://github.com/Hexalith" })
        {
            content.Contains(endpoint, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"{relativePath} must not contain a production endpoint.");
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

        bool expectedPlaceholder = !string.Equals(relativePath, "tests/fixtures/audit-leakage-corpus.json", StringComparison.Ordinal);
        ownership.GetProperty("non_policy_placeholder").GetBoolean().ShouldBe(expectedPlaceholder);
        ownership.GetProperty("synthetic_data_only").GetBoolean().ShouldBeTrue();
    }

    // Patch 7: helper that asserts file existence before reading, giving a clear failure message
    private static string ReadFixtureFile(string absolutePath)
    {
        File.Exists(absolutePath).ShouldBeTrue($"Fixture file not found: '{absolutePath}'");
        return File.ReadAllText(absolutePath);
    }

    private static YamlMappingNode ParsePreviousSpine(string content, string name)
    {
        using StringReader reader = new(content);
        YamlStream yaml = new();
        yaml.Load(reader);
        yaml.Documents.Count.ShouldBe(1, $"{name} must contain one YAML document.");
        YamlMappingNode root = yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>(name);
        CheckVersionKeys(root, name, new HashSet<YamlNode>(ReferenceEqualityComparer.Instance));
        return root;
    }

    private static void CheckOwnershipText(YamlNode node, string field)
    {
        string location = $"previous-spine.yaml ownership.{field}";
        YamlScalarNode text = node.ShouldBeOfType<YamlScalarNode>(location);
        string? value = text.Value;
        (string.IsNullOrWhiteSpace(value)
            || (text.Style == YamlDotNet.Core.ScalarStyle.Plain && (value == "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))))
            .ShouldBeFalse($"{location} must contain text.");
    }

    private static void CheckVersionKeys(YamlNode node, string location, HashSet<YamlNode> visited)
    {
        if (!visited.Add(node))
        {
            return;
        }

        if (node is YamlMappingNode mapping)
        {
            foreach ((YamlNode key, YamlNode value) in mapping.Children)
            {
                string field = key.ShouldBeOfType<YamlScalarNode>(location).Value ?? string.Empty;
                if (field.Equals("openapi", StringComparison.OrdinalIgnoreCase)
                    || field.Equals("openapiVersion", StringComparison.OrdinalIgnoreCase)
                    || field.StartsWith("openapi_", StringComparison.OrdinalIgnoreCase)
                    || field.StartsWith("openapi-", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"{location} contains the OpenAPI version key '{field}'.");
                }

                CheckVersionKeys(value, $"{location}.{field}", visited);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                CheckVersionKeys(sequence.Children[index], $"{location}[{index}]", visited);
            }
        }
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
