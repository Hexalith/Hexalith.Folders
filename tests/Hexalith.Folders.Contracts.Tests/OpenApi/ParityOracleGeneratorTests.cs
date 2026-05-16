using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class ParityOracleGeneratorTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string OraclePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml");
    private static readonly string SchemaPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.schema.json");
    private static readonly string GeneratorProject = Path.Combine(RepositoryRoot, "tests", "tools", "parity-oracle-generator", "Hexalith.Folders.ParityOracleGenerator.csproj");

    [Fact]
    public void GeneratedParityOracleContainsEveryCurrentOperationExactlyOnce()
    {
        string[] openApiOperations = LoadOperationIds(OpenApiPath);
        YamlMappingNode[] rows = LoadRows(OraclePath);

        rows.Select(row => RequiredScalar(row, "operation_id")).Order(StringComparer.Ordinal).ToArray().ShouldBe(openApiOperations);
        rows.Select(row => RequiredScalar(row, "operation_id")).Distinct(StringComparer.Ordinal).Count().ShouldBe(rows.Length);
    }

    [Fact]
    public void GeneratedParityRowsValidateAgainstSeedSchemaEnumsAndRequiredColumns()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        YamlMappingNode[] rows = LoadRows(OraclePath);

        string[] rootRequired = ReadRequired(schema.RootElement).ToArray();
        string[] transportRequired = ReadRequired(schema.RootElement.GetProperty("properties").GetProperty("transport_parity")).ToArray();
        string[] behavioralRequired = ReadRequired(schema.RootElement.GetProperty("properties").GetProperty("behavioral_parity")).ToArray();
        Dictionary<string, HashSet<string>> enums = LoadSchemaEnums(schema.RootElement);

        foreach (YamlMappingNode row in rows)
        {
            AssertRequired(row, rootRequired);
            RequiredSequence(row, "adapter_expectations").Children
                .Select(item => item.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty)
                .ShouldAllBe(adapter => enums["adapter_name"].Contains(adapter));

            enums["operation_family"].ShouldContain(RequiredScalar(row, "operation_family"));
            enums["read_consistency_class"].ShouldContain(RequiredScalar(row, "read_consistency_class"));

            YamlMappingNode transport = RequiredMapping(row, "transport_parity");
            AssertRequired(transport, transportRequired);
            enums["auth_outcome_class"].ShouldContain(RequiredScalar(transport, "auth_outcome_class"));
            enums["idempotency_key_rule"].ShouldContain(RequiredScalar(transport, "idempotency_key_rule"));
            RequiredSequence(transport, "error_code_set").Children
                .Select(item => item.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty)
                .ShouldAllBe(category => enums["canonical_error_category"].Contains(category));

            YamlMappingNode behavioral = RequiredMapping(row, "behavioral_parity");
            AssertRequired(behavioral, behavioralRequired);
            enums["pre_sdk_error_class"].ShouldContain(RequiredScalar(behavioral, "pre_sdk_error_class"));
            enums["idempotency_key_sourcing"].ShouldContain(RequiredScalar(behavioral, "idempotency_key_sourcing"));
            enums["correlation_id_sourcing"].ShouldContain(RequiredScalar(behavioral, "correlation_id_sourcing"));
            enums["task_id_sourcing"].ShouldContain(RequiredScalar(behavioral, "task_id_sourcing"));
            enums["credential_sourcing"].ShouldContain(RequiredScalar(behavioral, "credential_sourcing"));
            enums["mcp_failure_kind"].ShouldContain(RequiredScalar(behavioral, "mcp_failure_kind"));
        }
    }

    [Fact]
    public void GeneratedParityRowsClassifyMutatingAndNonMutatingIdempotencyRules()
    {
        YamlMappingNode[] rows = LoadRows(OraclePath);

        foreach (YamlMappingNode row in rows)
        {
            string family = RequiredScalar(row, "operation_family");
            string readConsistency = RequiredScalar(row, "read_consistency_class");
            string idempotencyRule = RequiredScalar(RequiredMapping(row, "transport_parity"), "idempotency_key_rule");

            if (family == "mutating_command")
            {
                readConsistency.ShouldBe("not_applicable");
                idempotencyRule.ShouldBeOneOf("required_for_mutating_command", "required_with_operation_id");
            }
            else
            {
                readConsistency.ShouldNotBe("not_applicable");
                idempotencyRule.ShouldBe("not_accepted_for_non_mutating_operation");
            }
        }
    }

    [Fact]
    public void GeneratorOutputIsByteStableAndMetadataOnly()
    {
        string temp = Path.Combine(Path.GetTempPath(), "hexalith-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string first = Path.Combine(temp, "first.yaml");
        string second = Path.Combine(temp, "second.yaml");

        RunGenerator(OpenApiPath, first).ShouldBe(0);
        RunGenerator(OpenApiPath, second).ShouldBe(0);
        File.ReadAllBytes(second).ShouldBe(File.ReadAllBytes(first));

        string output = File.ReadAllText(first);
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "contentBytes",
            "raw provider payload",
            "https://",
            RepositoryRoot,
        ];
        foreach (string value in forbidden)
        {
            output.ShouldNotContain(value, Case.Insensitive);
        }
    }

    [Fact]
    public void GeneratorFailsClosedWhenMutatingIdempotencyMetadataIsMissing()
    {
        string temp = Path.Combine(Path.GetTempPath(), "hexalith-parity-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string mutatedContract = Path.Combine(temp, "hexalith.folders.v1.yaml");
        string contract = File.ReadAllText(OpenApiPath);
        File.WriteAllText(mutatedContract, contract.Replace("x-hexalith-idempotency-equivalence:", "x-disabled-idempotency-equivalence:", StringComparison.Ordinal), Encoding.UTF8);

        GeneratorResult result = RunGeneratorDetailed(mutatedContract, Path.Combine(temp, "parity-contract.yaml"));

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("prerequisite_drift", Case.Insensitive);
    }

    [Fact]
    public void GeneratorFailsClosedForDuplicateIdempotencyFields()
    {
        string temp = Path.Combine(Path.GetTempPath(), "hexalith-parity-duplicate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string mutatedContract = Path.Combine(temp, "hexalith.folders.v1.yaml");
        string contract = File.ReadAllText(OpenApiPath);
        File.WriteAllText(mutatedContract, contract.Replace("        - parent_folder_id\n        - request_schema_version", "        - parent_folder_id\n        - parent_folder_id\n        - request_schema_version", StringComparison.Ordinal), Encoding.UTF8);

        GeneratorResult result = RunGeneratorDetailed(mutatedContract, Path.Combine(temp, "parity-contract.yaml"));

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("duplicate idempotency fields", Case.Insensitive);
    }

    [Fact]
    public void GeneratorFailsClosedForRemovedPreviousSpineOperationWithoutDeprecation()
    {
        string temp = Path.Combine(Path.GetTempPath(), "hexalith-parity-removed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string previousSpine = Path.Combine(temp, "previous-spine.yaml");
        File.WriteAllText(previousSpine, """
version: test
operations:
  - operation_id: RemovedSyntheticOperation
    method: get
    path: /api/v1/synthetic/removed
""", Encoding.UTF8);

        GeneratorResult result = RunGeneratorDetailed(OpenApiPath, Path.Combine(temp, "parity-contract.yaml"), previousSpine);

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("removed without approved deprecation", Case.Insensitive);
    }

    private static int RunGenerator(string contractPath, string outputPath) =>
        RunGeneratorDetailed(contractPath, outputPath).ExitCode;

    private static GeneratorResult RunGeneratorDetailed(string contractPath, string outputPath, string? previousSpinePath = null)
    {
        string previousArgument = previousSpinePath is null ? string.Empty : $" --previous-spine \"{previousSpinePath}\"";
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{GeneratorProject}\" -- --repository-root \"{RepositoryRoot}\" --contract \"{contractPath}\" --output \"{outputPath}\"{previousArgument}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;

        process.WaitForExit(120_000).ShouldBeTrue();
        return new(process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
    }

    private static string[] LoadOperationIds(string path)
    {
        YamlMappingNode root = LoadYamlMapping(path);
        List<string> operations = [];
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = methodEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                if (method is "get" or "post" or "put" or "patch" or "delete")
                {
                    operations.Add(RequiredScalar(methodEntry.Value.ShouldBeOfType<YamlMappingNode>(), "operationId"));
                }
            }
        }

        return operations.Order(StringComparer.Ordinal).ToArray();
    }

    private static YamlMappingNode[] LoadRows(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlSequenceNode>()
            .Children
            .Select(row => row.ShouldBeOfType<YamlMappingNode>())
            .ToArray();
    }

    private static Dictionary<string, HashSet<string>> LoadSchemaEnums(JsonElement root)
    {
        Dictionary<string, HashSet<string>> values = new(StringComparer.Ordinal)
        {
            ["operation_family"] = ReadEnum(root.GetProperty("properties").GetProperty("operation_family")),
            ["read_consistency_class"] = ReadEnum(root.GetProperty("properties").GetProperty("read_consistency_class")),
            ["auth_outcome_class"] = ReadEnum(root.GetProperty("properties").GetProperty("transport_parity").GetProperty("properties").GetProperty("auth_outcome_class")),
            ["idempotency_key_rule"] = ReadEnum(root.GetProperty("properties").GetProperty("transport_parity").GetProperty("properties").GetProperty("idempotency_key_rule")),
            ["pre_sdk_error_class"] = ReadEnum(root.GetProperty("properties").GetProperty("behavioral_parity").GetProperty("properties").GetProperty("pre_sdk_error_class")),
            ["idempotency_key_sourcing"] = ReadEnum(root.GetProperty("properties").GetProperty("behavioral_parity").GetProperty("properties").GetProperty("idempotency_key_sourcing")),
            ["correlation_id_sourcing"] = ReadEnum(root.GetProperty("properties").GetProperty("behavioral_parity").GetProperty("properties").GetProperty("correlation_id_sourcing")),
            ["task_id_sourcing"] = ReadEnum(root.GetProperty("properties").GetProperty("behavioral_parity").GetProperty("properties").GetProperty("task_id_sourcing")),
            ["credential_sourcing"] = ReadEnum(root.GetProperty("properties").GetProperty("behavioral_parity").GetProperty("properties").GetProperty("credential_sourcing")),
            ["adapter_name"] = ReadEnum(root.GetProperty("$defs").GetProperty("adapter_name")),
            ["canonical_error_category"] = ReadEnum(root.GetProperty("$defs").GetProperty("canonical_error_category")),
            ["mcp_failure_kind"] = ReadEnum(root.GetProperty("$defs").GetProperty("mcp_failure_kind")),
        };

        return values;
    }

    private static HashSet<string> ReadEnum(JsonElement element) =>
        element.GetProperty("enum").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> ReadRequired(JsonElement element) =>
        element.GetProperty("required").EnumerateArray().Select(item => item.GetString() ?? string.Empty);

    private static void AssertRequired(YamlMappingNode mapping, IEnumerable<string> required)
    {
        foreach (string key in required)
        {
            mapping.Children.ContainsKey(new YamlScalarNode(key)).ShouldBeTrue(key);
        }
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
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

    private sealed record GeneratorResult(int ExitCode, string Output, string Error);
}
