using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

// Serialize generator invocations across the test class: each test shells out to `dotnet run`
// against the same generator project, and concurrent invocations can race on the project's
// obj/ build locks even with --no-build (NETSDK metadata is touched on each call). Disabling
// per-class parallelism removes that race deterministically.
[Collection("ParityOracleGenerator")]
public sealed class ParityOracleGeneratorTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string OraclePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml");
    private static readonly string SchemaPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.schema.json");
    private static readonly string PreviousSpinePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "previous-spine.yaml");
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
        string[] outcomeRequired = ReadRequired(schema.RootElement.GetProperty("properties").GetProperty("outcome_mapping").GetProperty("items")).ToArray();
        Dictionary<string, HashSet<string>> enums = LoadSchemaEnums(schema.RootElement);
        HashSet<int> cliExitCodes = ReadIntEnum(schema.RootElement.GetProperty("$defs").GetProperty("cli_exit_code"));
        Regex auditKeyPattern = new("^[a-z][a-z0-9_]*$");
        Regex correlationPattern = new(@"^(headers|problem|result|metadata)\.[A-Za-z0-9_.-]+$");
        Regex operationIdPattern = new("^[A-Z][A-Za-z0-9]*$");

        foreach (YamlMappingNode row in rows)
        {
            AssertRequired(row, rootRequired);
            string operationId = RequiredScalar(row, "operation_id");
            operationIdPattern.IsMatch(operationId).ShouldBeTrue($"operation_id '{operationId}' must match shape regex");

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
            RequiredSequence(transport, "audit_metadata_keys").Children
                .Select(item => item.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty)
                .ShouldAllBe(key => auditKeyPattern.IsMatch(key));
            correlationPattern.IsMatch(RequiredScalar(transport, "correlation_field_path")).ShouldBeTrue();

            YamlMappingNode behavioral = RequiredMapping(row, "behavioral_parity");
            AssertRequired(behavioral, behavioralRequired);
            enums["pre_sdk_error_class"].ShouldContain(RequiredScalar(behavioral, "pre_sdk_error_class"));
            enums["idempotency_key_sourcing"].ShouldContain(RequiredScalar(behavioral, "idempotency_key_sourcing"));
            enums["correlation_id_sourcing"].ShouldContain(RequiredScalar(behavioral, "correlation_id_sourcing"));
            enums["task_id_sourcing"].ShouldContain(RequiredScalar(behavioral, "task_id_sourcing"));
            enums["credential_sourcing"].ShouldContain(RequiredScalar(behavioral, "credential_sourcing"));
            enums["mcp_failure_kind"].ShouldContain(RequiredScalar(behavioral, "mcp_failure_kind"));
            cliExitCodes.ShouldContain(int.Parse(RequiredScalar(behavioral, "cli_exit_code")));

            YamlSequenceNode outcomeMapping = RequiredSequence(row, "outcome_mapping");
            outcomeMapping.Children.Count.ShouldBeGreaterThan(0);
            foreach (YamlMappingNode mapping in outcomeMapping.Children.Cast<YamlMappingNode>())
            {
                AssertRequired(mapping, outcomeRequired);
                enums["canonical_error_category"].ShouldContain(RequiredScalar(mapping, "canonical_error_category"));
                enums["mcp_failure_kind"].ShouldContain(RequiredScalar(mapping, "mcp_failure_kind"));
                enums["pre_sdk_error_class"].ShouldContain(RequiredScalar(mapping, "pre_sdk_error_class"));
                cliExitCodes.ShouldContain(int.Parse(RequiredScalar(mapping, "cli_exit_code")));
            }
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
    public void GeneratedOutcomeMappingPopulatesEveryDeclaredErrorCategory()
    {
        YamlMappingNode[] rows = LoadRows(OraclePath);

        foreach (YamlMappingNode row in rows)
        {
            HashSet<string> errorCategories = RequiredSequence(RequiredMapping(row, "transport_parity"), "error_code_set")
                .Children
                .Select(item => item.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> outcomeCategories = RequiredSequence(row, "outcome_mapping")
                .Children
                .Cast<YamlMappingNode>()
                .Select(mapping => RequiredScalar(mapping, "canonical_error_category"))
                .ToHashSet(StringComparer.Ordinal);

            outcomeCategories.ShouldBe(errorCategories);
        }
    }

    [Fact]
    public void GeneratorOutputIsByteStableAndMetadataOnly()
    {
        string temp = NewTempDirectory("hexalith-parity");
        string first = Path.Combine(temp, "first.yaml");
        string second = Path.Combine(temp, "second.yaml");

        RunGenerator(OpenApiPath, first).ShouldBe(0);
        RunGenerator(OpenApiPath, second).ShouldBe(0);
        File.ReadAllBytes(second).ShouldBe(File.ReadAllBytes(first));

        string output = File.ReadAllText(first);
        AssertNoLeakage(output);

        // Committed parity-contract.yaml must match what the generator produces today. Normalize
        // line endings on both sides so a Windows checkout with git autocrlf=true (which rewrites
        // the committed LF to CRLF on disk) does not break this assertion; the contract is "the
        // generator output text is byte-stable", not "git's on-disk encoding is byte-stable".
        string committed = NormalizeLineEndings(File.ReadAllText(OraclePath));
        string generated = NormalizeLineEndings(File.ReadAllText(first));
        committed.ShouldBe(generated, "Committed parity-contract.yaml is out of sync with generator output. Regenerate the oracle via `dotnet run --project tests/tools/parity-oracle-generator`.");
    }

    [Fact]
    public void GeneratorFailsClosedWhenMutatingIdempotencyMetadataIsMissing()
    {
        string temp = NewTempDirectory("hexalith-parity-negative");
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
        string temp = NewTempDirectory("hexalith-parity-duplicate");
        string mutatedContract = Path.Combine(temp, "hexalith.folders.v1.yaml");
        string contract = NormalizeLineEndings(File.ReadAllText(OpenApiPath));
        string before = "        - parent_folder_id\n        - request_schema_version";
        string after = "        - parent_folder_id\n        - parent_folder_id\n        - request_schema_version";
        int firstIndex = contract.IndexOf(before, StringComparison.Ordinal);
        firstIndex.ShouldBeGreaterThanOrEqualTo(0, customMessage: "Test fixture anchor not found in OpenAPI; adjust the anchor.");
        contract.IndexOf(before, firstIndex + 1, StringComparison.Ordinal).ShouldBe(-1, "Test fixture anchor matches more than one operation; adjust the anchor so the mutation is targeted.");
        string mutated = contract.Replace(before, after, StringComparison.Ordinal);
        File.WriteAllText(mutatedContract, mutated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        GeneratorResult result = RunGeneratorDetailed(mutatedContract, Path.Combine(temp, "parity-contract.yaml"));

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("duplicate idempotency fields", Case.Insensitive);
    }

    [Fact]
    public void GeneratorFailsClosedForRemovedPreviousSpineOperationWithoutDeprecation()
    {
        string temp = NewTempDirectory("hexalith-parity-removed");
        string previousSpine = Path.Combine(temp, "previous-spine.yaml");
        File.WriteAllText(previousSpine, """
version: test
operations:
  - operation_id: RemovedSyntheticOperation
    method: get
    path: /api/v1/synthetic/removed
""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        GeneratorResult result = RunGeneratorDetailed(OpenApiPath, Path.Combine(temp, "parity-contract.yaml"), previousSpine);

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("removed without approved deprecation", Case.Insensitive);
    }

    [Fact]
    public void GeneratorFailsClosedForEmptyBaselineWithoutOverride()
    {
        string temp = NewTempDirectory("hexalith-parity-empty-baseline");
        string previousSpine = Path.Combine(temp, "previous-spine.yaml");
        File.WriteAllText(previousSpine, "version: synthetic\noperations: []\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        GeneratorResult result = RunGeneratorDetailed(OpenApiPath, Path.Combine(temp, "parity-contract.yaml"), previousSpine);

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).ShouldContain("empty operations", Case.Insensitive);
    }

    [Fact]
    public void GeneratorAcceptsApprovedDeprecationWithYamlBooleanLiteral()
    {
        string temp = NewTempDirectory("hexalith-parity-yaml-bool");
        string previousSpine = Path.Combine(temp, "previous-spine.yaml");
        File.WriteAllText(previousSpine, """
version: test
operations:
  - operation_id: RemovedSyntheticOperation
    method: get
    path: /api/v1/synthetic/removed
    deprecation:
      approved: yes
""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        GeneratorResult result = RunGeneratorDetailed(OpenApiPath, Path.Combine(temp, "parity-contract.yaml"), previousSpine);

        result.ExitCode.ShouldBe(0, customMessage: result.Output + result.Error);
    }

    [Fact]
    public void PreviousSpineBaselineCoversEveryCurrentOperation()
    {
        string[] currentOps = LoadOperationIds(OpenApiPath);
        YamlMappingNode baseline = LoadYamlMapping(PreviousSpinePath);

        baseline.Children.TryGetValue(new YamlScalarNode("operations"), out YamlNode? operationsNode).ShouldBeTrue();
        YamlSequenceNode seq = operationsNode.ShouldBeOfType<YamlSequenceNode>();
        HashSet<string> baselineOps = seq.Children
            .Cast<YamlMappingNode>()
            .Select(op => RequiredScalar(op, "operation_id"))
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> approvedAdditions = new(StringComparer.Ordinal);
        if (baseline.Children.TryGetValue(new YamlScalarNode("approved_additions"), out YamlNode? additionsNode) && additionsNode is YamlSequenceNode additionsSeq)
        {
            foreach (YamlNode entry in additionsSeq.Children)
            {
                if (entry is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
                {
                    approvedAdditions.Add(scalar.Value!);
                }
            }
        }

        string[] unaccountedAdditions = currentOps
            .Where(op => !baselineOps.Contains(op) && !approvedAdditions.Contains(op))
            .ToArray();

        unaccountedAdditions.ShouldBeEmpty(
            "These operationIds are present in the OpenAPI Contract Spine but absent from both the previous-spine baseline and the approved_additions list. "
            + "Either add them to `approved_additions:` in tests/fixtures/previous-spine.yaml or rerun the generator with `--initialize-baseline` after an intentional sweep.");
    }

    [Fact]
    public void ParitySchemaCanonicalEnumDoesNotDuplicateProviderOutcomeUnknown()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        HashSet<string> categoryEnum = ReadEnum(schema.RootElement.GetProperty("$defs").GetProperty("canonical_error_category"));
        HashSet<string> mcpEnum = ReadEnum(schema.RootElement.GetProperty("$defs").GetProperty("mcp_failure_kind"));

        categoryEnum.ShouldContain("unknown_provider_outcome");
        categoryEnum.ShouldNotContain("provider_outcome_unknown");
        mcpEnum.ShouldContain("unknown_provider_outcome");
        mcpEnum.ShouldNotContain("provider_outcome_unknown");
    }

    [Fact]
    public void ParitySchemaOutcomeMappingShapeIsBounded()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        JsonElement outcomeMapping = schema.RootElement.GetProperty("properties").GetProperty("outcome_mapping");

        outcomeMapping.GetProperty("type").GetString().ShouldBe("array");
        outcomeMapping.GetProperty("uniqueItems").GetBoolean().ShouldBeTrue();
        outcomeMapping.GetProperty("minItems").GetInt32().ShouldBe(1);
        JsonElement item = outcomeMapping.GetProperty("items");
        item.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        string[] required = item.GetProperty("required").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        required.ShouldBe(["canonical_error_category", "cli_exit_code", "mcp_failure_kind", "pre_sdk_error_class"], ignoreOrder: true);
    }

    private static string NewTempDirectory(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void AssertNoLeakage(string output)
    {
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "contentBytes",
            "raw provider payload",
            "https://",
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            RepositoryRoot.Replace("\\", "\\\\", StringComparison.Ordinal),
        ];
        foreach (string value in forbidden)
        {
            output.ShouldNotContain(value, Case.Insensitive);
        }
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    private static int RunGenerator(string contractPath, string outputPath) =>
        RunGeneratorDetailed(contractPath, outputPath).ExitCode;

    private static GeneratorResult RunGeneratorDetailed(string contractPath, string outputPath, string? previousSpinePath = null)
    {
        string previousArgument = previousSpinePath is null ? string.Empty : $" --previous-spine \"{previousSpinePath}\"";
        ProcessStartInfo info = new()
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{GeneratorProject}\" --no-build -- --repository-root \"{RepositoryRoot}\" --contract \"{contractPath}\" --output \"{outputPath}\"{previousArgument}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(info)!;

        // Drain stdout/stderr concurrently so a full pipe buffer cannot deadlock the child.
        StringBuilder stdout = new();
        StringBuilder stderr = new();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            if (!process.WaitForExit(180_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw new TimeoutException("Generator did not exit within 180 seconds.");
            }

            // After a successful timed wait, call the parameterless overload to guarantee the
            // async output/error readers have flushed their final buffered lines. Without this,
            // assertions that grep for trailing `prerequisite_drift` messages can be flaky.
            process.WaitForExit();
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }

            throw;
        }

        return new(process.ExitCode, stdout.ToString(), stderr.ToString());
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
                string method = (methodEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty).ToLowerInvariant();
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

    private static HashSet<int> ReadIntEnum(JsonElement element) =>
        element.GetProperty("enum").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();

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
