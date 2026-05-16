using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.RepresentationModel;

Dictionary<string, string> arguments = ParseArguments(args);
string repositoryRoot = Path.GetFullPath(arguments.GetValueOrDefault("--repository-root", LocateRepositoryRoot()));
string contractPath = Path.GetFullPath(arguments.GetValueOrDefault("--contract", Path.Combine(repositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml")));
string schemaPath = Path.GetFullPath(arguments.GetValueOrDefault("--schema", Path.Combine(repositoryRoot, "tests", "fixtures", "parity-contract.schema.json")));
string previousSpinePath = Path.GetFullPath(arguments.GetValueOrDefault("--previous-spine", Path.Combine(repositoryRoot, "tests", "fixtures", "previous-spine.yaml")));
string outputPath = Path.GetFullPath(arguments.GetValueOrDefault("--output", Path.Combine(repositoryRoot, "tests", "fixtures", "parity-contract.yaml")));

YamlMappingNode root = LoadYaml(contractPath);
IReadOnlyList<OperationModel> operations = EnumerateOperations(root).OrderBy(o => o.OperationId, StringComparer.Ordinal).ToArray();
ValidateOperationInventory(operations);
ValidatePreviousSpine(previousSpinePath, operations);

string output = RenderRows(operations, contractPath, schemaPath);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
File.WriteAllText(outputPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

static IReadOnlyList<OperationModel> EnumerateOperations(YamlMappingNode root)
{
    List<OperationModel> operations = [];
    foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
    {
        string path = pathEntry.Key.AsScalar("path").Value ?? string.Empty;
        YamlMappingNode pathItem = pathEntry.Value.AsMapping("path item");
        IReadOnlyList<ParameterModel> pathParameters = EnumerateParameters(pathItem).ToArray();

        foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
        {
            string method = (methodEntry.Key.AsScalar("method").Value ?? string.Empty).ToLowerInvariant();
            if (method is not ("get" or "post" or "put" or "patch" or "delete"))
            {
                continue;
            }

            YamlMappingNode operation = methodEntry.Value.AsMapping("operation");
            string operationId = RequiredScalar(operation, "operationId");
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
            IReadOnlyList<ParameterModel> parameters = [.. pathParameters, .. EnumerateParameters(operation)];
            string[] tags = ReadStringSequence(operation, "tags").ToArray();
            string[] idempotencyFields = ReadStringSequence(operation, "x-hexalith-idempotency-equivalence").ToArray();
            string[] errorCategories = ReadStringSequence(operation, "x-hexalith-canonical-error-categories").Select(NormalizeErrorCategory).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] auditKeys = ReadAuditMetadataKeys(operation).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string? readConsistency = ReadConsistencyClass(operation);
            bool hasIdempotencyKey = HasIdempotencyKey(parameters, operation);
            string correlationHeader = ReadNestedScalar(operation, "x-hexalith-correlation", "correlationHeader") ?? "X-Correlation-Id";

            operations.Add(new OperationModel(
                Method: method,
                Path: NormalizePath(path),
                OperationId: operationId,
                Tags: tags,
                Parameters: parameters,
                HasIdempotencyKey: hasIdempotencyKey,
                IdempotencyFields: idempotencyFields,
                ReadConsistencyClass: readConsistency,
                ErrorCategories: errorCategories,
                AuditMetadataKeys: auditKeys,
                CorrelationHeader: correlationHeader));
        }
    }

    return operations;
}

static void ValidateOperationInventory(IReadOnlyList<OperationModel> operations)
{
    foreach (IGrouping<string, OperationModel> duplicate in operations.GroupBy(o => o.OperationId, StringComparer.Ordinal).Where(g => g.Count() > 1))
    {
        throw new InvalidOperationException($"prerequisite_drift: duplicate operationId '{duplicate.Key}'.");
    }

    foreach (OperationModel operation in operations)
    {
        if (operation.ErrorCategories.Count == 0)
        {
            throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} lacks x-hexalith-canonical-error-categories.");
        }

        if (operation.AuditMetadataKeys.Count == 0)
        {
            throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} lacks x-hexalith-audit-metadata-keys.");
        }

        if (operation.IsMutatingCommand)
        {
            if (!operation.HasIdempotencyKey || operation.IdempotencyFields.Count == 0)
            {
                throw new InvalidOperationException($"prerequisite_drift: mutating operation {operation.OperationId} lacks required idempotency metadata.");
            }

            if (operation.IdempotencyFields.Distinct(StringComparer.Ordinal).Count() != operation.IdempotencyFields.Count)
            {
                throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} declares duplicate idempotency fields.");
            }

            if (!operation.IdempotencyFields.SequenceEqual(operation.IdempotencyFields.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} idempotency fields are not ordinal-sorted.");
            }
        }
        else
        {
            if (operation.HasIdempotencyKey || operation.IdempotencyFields.Count > 0)
            {
                throw new InvalidOperationException($"prerequisite_drift: non-mutating operation {operation.OperationId} accepts idempotency metadata.");
            }

            if (string.IsNullOrWhiteSpace(operation.ReadConsistencyClass))
            {
                throw new InvalidOperationException($"prerequisite_drift: non-mutating operation {operation.OperationId} lacks x-hexalith-read-consistency.");
            }
        }
    }
}

static void ValidatePreviousSpine(string previousSpinePath, IReadOnlyList<OperationModel> currentOperations)
{
    if (!File.Exists(previousSpinePath))
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml is missing.");
    }

    YamlMappingNode previous = LoadYaml(previousSpinePath);
    if (!previous.Children.TryGetValue(new YamlScalarNode("operations"), out YamlNode? operationsNode))
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml lacks operations.");
    }

    HashSet<string> currentIdentities = currentOperations.Select(o => o.Identity).ToHashSet(StringComparer.Ordinal);
    foreach (YamlNode node in operationsNode.AsSequence("previous operations"))
    {
        YamlMappingNode operation = node.AsMapping("previous operation");
        string operationId = ReadFlexibleScalar(operation, "operation_id", "operationId");
        string method = ReadFlexibleScalar(operation, "method", "http_method").ToLowerInvariant();
        string path = NormalizePath(ReadFlexibleScalar(operation, "path", "normalized_path"));
        string identity = method + " " + path + " " + operationId;
        if (!currentIdentities.Contains(identity) && !HasApprovedDeprecation(operation))
        {
            throw new InvalidOperationException($"prerequisite_drift: previous operation removed without approved deprecation '{identity}'.");
        }
    }
}

static string RenderRows(IReadOnlyList<OperationModel> operations, string contractPath, string schemaPath)
{
    StringBuilder builder = new();
    builder.Append("# generated_by: tests/tools/parity-oracle-generator\n");
    builder.Append("# contract_spine_sha256: ").Append(Sha256(File.ReadAllText(contractPath).NormalizeLineEndings())).Append('\n');
    builder.Append("# parity_schema_sha256: ").Append(Sha256(File.ReadAllText(schemaPath).NormalizeLineEndings())).Append('\n');
    builder.Append("# source_authority: openapi-operation-metadata + idempotency-and-parity-rules + architecture-adapter-parity-contract\n");

    foreach (OperationModel operation in operations)
    {
        RenderRow(builder, operation);
    }

    return builder.ToString();
}

static void RenderRow(StringBuilder builder, OperationModel operation)
{
    string family = operation.OperationFamily;
    builder.Append("- operation_id: ").Append(Quote(operation.OperationId)).Append('\n');
    builder.Append("  operation_family: ").Append(Quote(family)).Append('\n');
    builder.Append("  read_consistency_class: ").Append(Quote(operation.IsMutatingCommand ? "not_applicable" : operation.ReadConsistencyClass!)).Append('\n');
    builder.Append("  transport_parity:\n");
    builder.Append("    auth_outcome_class: ").Append(Quote(AuthOutcomeClass(operation))).Append('\n');
    builder.Append("    error_code_set:\n");
    foreach (string category in operation.ErrorCategories)
    {
        builder.Append("      - ").Append(Quote(category)).Append('\n');
    }

    builder.Append("    idempotency_key_rule: ").Append(Quote(IdempotencyRule(operation))).Append('\n');
    builder.Append("    audit_metadata_keys:\n");
    foreach (string key in operation.AuditMetadataKeys)
    {
        builder.Append("      - ").Append(Quote(key)).Append('\n');
    }

    builder.Append("    correlation_field_path: ").Append(Quote("headers." + operation.CorrelationHeader)).Append('\n');
    builder.Append("    terminal_states:\n");
    foreach (string state in TerminalStates(operation))
    {
        builder.Append("      - ").Append(Quote(state)).Append('\n');
    }

    builder.Append("  behavioral_parity:\n");
    builder.Append("    pre_sdk_error_class: ").Append(Quote("none")).Append('\n');
    builder.Append("    idempotency_key_sourcing: ").Append(Quote(operation.IsMutatingCommand ? "caller_provided" : "not_accepted")).Append('\n');
    builder.Append("    correlation_id_sourcing: ").Append(Quote("caller_provided")).Append('\n');
    builder.Append("    task_id_sourcing: ").Append(Quote(operation.Parameters.Any(p => p.Field == "task_id") ? "caller_provided" : "not_task_scoped")).Append('\n');
    builder.Append("    credential_sourcing: ").Append(Quote("sdk_configuration")).Append('\n');
    builder.Append("    cli_exit_code: 0\n");
    builder.Append("    mcp_failure_kind: ").Append(Quote("none")).Append('\n');
    builder.Append("  adapter_expectations:\n");
    foreach (string adapter in AdapterExpectations(operation))
    {
        builder.Append("    - ").Append(Quote(adapter)).Append('\n');
    }

    builder.Append("  ownership:\n");
    builder.Append("    owner_workstream: ").Append(Quote("C13 parity oracle generation")).Append('\n');
    builder.Append("    future_test_use: ").Append(Quote("SDK, REST, CLI, and MCP parity tests consume this generated row.")).Append('\n');
    builder.Append("    known_omissions:\n");
    builder.Append("      - ").Append(Quote("Runtime adapter behavior is implemented by later Epic 5 stories.")).Append('\n');
    builder.Append("    mutation_rules:\n");
    builder.Append("      - ").Append(Quote("Regenerate from the Contract Spine; do not hand-edit operation rows.")).Append('\n');
    builder.Append("    non_policy_placeholder: true\n");
    builder.Append("    synthetic_data_only: true\n");
}

static string AuthOutcomeClass(OperationModel operation)
{
    if (operation.ErrorCategories.Contains("audit_access_denied", StringComparer.Ordinal))
    {
        return "audit_access_denied";
    }

    if (operation.ErrorCategories.Contains("credential_missing", StringComparer.Ordinal))
    {
        return "credential_missing";
    }

    if (operation.ErrorCategories.Contains("folder_acl_denied", StringComparer.Ordinal))
    {
        return "folder_acl_denied";
    }

    if (operation.ErrorCategories.Contains("tenant_access_denied", StringComparer.Ordinal))
    {
        return "tenant_access_denied";
    }

    return operation.ErrorCategories.Contains("not_found", StringComparer.Ordinal) ? "safe_not_found" : "tenant_authorized";
}

static string IdempotencyRule(OperationModel operation)
{
    if (!operation.IsMutatingCommand)
    {
        return "not_accepted_for_non_mutating_operation";
    }

    return operation.IdempotencyFields.Contains("operation_id", StringComparer.Ordinal)
        ? "required_with_operation_id"
        : "required_for_mutating_command";
}

static string[] TerminalStates(OperationModel operation)
{
    if (operation.IsMutatingCommand)
    {
        return ["accepted"];
    }

    return operation.OperationFamily switch
    {
        "context_query" => ["context_returned"],
        "audit" => ["audit_returned"],
        "operations_console_projection" => ["projection_returned"],
        _ => ["projected"],
    };
}

static string[] AdapterExpectations(OperationModel operation)
{
    List<string> adapters = ["rest", "sdk", "cli", "mcp"];
    if (operation.OperationFamily == "operations_console_projection")
    {
        adapters.Add("ui");
    }

    return adapters.ToArray();
}

static IEnumerable<ParameterModel> EnumerateParameters(YamlMappingNode mapping)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
    {
        yield break;
    }

    foreach (YamlNode node in parametersNode.AsSequence("parameters"))
    {
        YamlMappingNode parameter = node.AsMapping("parameter");
        string name;
        if (parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? referenceNode))
        {
            name = (referenceNode.AsScalar("$ref").Value ?? string.Empty).Split('/').Last();
        }
        else
        {
            name = RequiredScalar(parameter, "name");
        }

        yield return new ParameterModel(NormalizeName(name), name);
    }
}

static bool HasIdempotencyKey(IReadOnlyList<ParameterModel> parameters, YamlMappingNode operation) =>
    parameters.Any(p => p.Field == "idempotency_key") ||
    operation.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key"));

static string? ReadConsistencyClass(YamlMappingNode operation)
{
    string? raw = ReadNestedScalar(operation, "x-hexalith-read-consistency", "class");
    return raw?.Replace("_", "-", StringComparison.Ordinal);
}

static IEnumerable<string> ReadAuditMetadataKeys(YamlMappingNode operation)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode("x-hexalith-audit-metadata-keys"), out YamlNode? node))
    {
        yield break;
    }

    foreach (YamlNode item in node.AsSequence("x-hexalith-audit-metadata-keys"))
    {
        yield return RequiredScalar(item.AsMapping("audit metadata key"), "name");
    }
}

static IReadOnlyList<string> ReadStringSequence(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        return [];
    }

    return node.AsSequence(key).Children.Select(c => c.AsScalar(key).Value ?? string.Empty).ToArray();
}

static string? ReadNestedScalar(YamlMappingNode mapping, string parent, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(parent), out YamlNode? node))
    {
        return null;
    }

    YamlMappingNode parentMapping = node.AsMapping(parent);
    return parentMapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
        ? value.AsScalar(key).Value
        : null;
}

static string ReadFlexibleScalar(YamlMappingNode mapping, params string[] keys)
{
    foreach (string key in keys)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            return value.AsScalar(key).Value ?? string.Empty;
        }
    }

    throw new InvalidOperationException($"Missing scalar '{string.Join("|", keys)}'.");
}

static bool HasApprovedDeprecation(YamlMappingNode operation) =>
    operation.Children.TryGetValue(new YamlScalarNode("deprecation"), out YamlNode? deprecationNode) &&
    deprecationNode.AsMapping("deprecation").Children.TryGetValue(new YamlScalarNode("approved"), out YamlNode? approvedNode) &&
    string.Equals(approvedNode.AsScalar("approved").Value, "true", StringComparison.OrdinalIgnoreCase);

static string NormalizeErrorCategory(string category) => category switch
{
    "provider_outcome_unknown" => "unknown_provider_outcome",
    _ => category,
};

static string NormalizePath(string path)
{
    string trimmed = path.Trim();
    while (trimmed.Contains("//", StringComparison.Ordinal))
    {
        trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
    }

    return trimmed;
}

static string NormalizeName(string value)
{
    StringBuilder builder = new(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
        char c = value[i];
        if (c is '-' or '_')
        {
            if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }
        else if (char.IsLetterOrDigit(c))
        {
            if (char.IsUpper(c) && i > 0 && builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }
    }

    return builder.ToString() switch
    {
        "x_hexalith_task_id" => "task_id",
        "idempotency_key" => "idempotency_key",
        "correlation_id" => "correlation_id",
        string normalized => normalized,
    };
}

static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

static Dictionary<string, string> ParseArguments(string[] values)
{
    Dictionary<string, string> parsed = new(StringComparer.Ordinal);
    for (int i = 0; i < values.Length; i += 2)
    {
        if (i + 1 >= values.Length || !values[i].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Arguments must be supplied as --key value pairs. Invalid token at position {i}.");
        }

        parsed[values[i]] = values[i + 1];
    }

    return parsed;
}

static YamlMappingNode LoadYaml(string path)
{
    using StreamReader reader = File.OpenText(path);
    YamlStream yaml = new();
    yaml.Load(reader);
    return yaml.Documents[0].RootNode.AsMapping("root");
}

static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing mapping '{key}'.");
    }

    return node.AsMapping(key);
}

static string RequiredScalar(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing scalar '{key}'.");
    }

    return node.AsScalar(key).Value ?? string.Empty;
}

static string Sha256(string value) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

static string LocateRepositoryRoot()
{
    string current = AppContext.BaseDirectory;
    while (!string.IsNullOrWhiteSpace(current))
    {
        if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
        {
            return current;
        }

        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }

    return Directory.GetCurrentDirectory();
}

internal sealed record OperationModel(
    string Method,
    string Path,
    string OperationId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ParameterModel> Parameters,
    bool HasIdempotencyKey,
    IReadOnlyList<string> IdempotencyFields,
    string? ReadConsistencyClass,
    IReadOnlyList<string> ErrorCategories,
    IReadOnlyList<string> AuditMetadataKeys,
    string CorrelationHeader)
{
    public string Identity => Method + " " + Path + " " + OperationId;

    public bool IsMutatingCommand => IdempotencyFields.Count > 0;

    public string OperationFamily
    {
        get
        {
            if (IsMutatingCommand)
            {
                return "mutating_command";
            }

            if (Tags.Contains("context-queries", StringComparer.Ordinal))
            {
                return "context_query";
            }

            if (Tags.Contains("audit", StringComparer.Ordinal))
            {
                return "audit";
            }

            if (Tags.Contains("ops-console", StringComparer.Ordinal))
            {
                return "operations_console_projection";
            }

            return "query_status";
        }
    }
}

internal sealed record ParameterModel(string Field, string Name);

internal static class TextExtensions
{
    public static string NormalizeLineEndings(this string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}

internal static class YamlExtensions
{
    public static YamlMappingNode AsMapping(this YamlNode node, string name) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{name} must be a mapping.");

    public static YamlSequenceNode AsSequence(this YamlNode node, string name) =>
        node as YamlSequenceNode ?? throw new InvalidOperationException($"{name} must be a sequence.");

    public static YamlScalarNode AsScalar(this YamlNode node, string name) =>
        node as YamlScalarNode ?? throw new InvalidOperationException($"{name} must be a scalar.");
}
