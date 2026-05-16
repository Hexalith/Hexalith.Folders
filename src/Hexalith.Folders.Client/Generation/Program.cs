using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

Dictionary<string, string> arguments = ParseArguments(args);
string repositoryRoot = RequiredArgument(arguments, "--repository-root");
string contractPath = RequiredArgument(arguments, "--contract");
string configurationPath = RequiredArgument(arguments, "--configuration");
string outputPath = RequiredArgument(arguments, "--output");

string contract = NormalizeText(File.ReadAllText(contractPath));
string configuration = NormalizeText(File.ReadAllText(configurationPath));
YamlMappingNode root = LoadYaml(contractPath);
IReadOnlyList<OperationModel> operations = EnumerateOperations(root).ToArray();
IReadOnlyList<HelperModel> helpers = BuildHelpers(root, operations);
string output = Render(helpers, Sha256(contract), Sha256(configuration));
string helperHash = Sha256(NormalizeGeneratedHelperHash(output));
int helperHashPlaceholder = output.IndexOf("__GENERATED_HELPERS_SHA256__", StringComparison.Ordinal);
if (helperHashPlaceholder < 0)
{
    throw new InvalidOperationException("Generated helper hash placeholder was not emitted.");
}

output = output.Remove(helperHashPlaceholder, "__GENERATED_HELPERS_SHA256__".Length).Insert(helperHashPlaceholder, helperHash);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
File.WriteAllText(outputPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

static IReadOnlyList<HelperModel> BuildHelpers(YamlMappingNode root, IReadOnlyList<OperationModel> operations)
{
    YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
    Dictionary<string, List<HelperVariantModel>> variantsBySchema = new(StringComparer.Ordinal);

    foreach (OperationModel operation in operations.Where(o => o.IdempotencyFields.Count > 0))
    {
        if (operation.Method is not ("post" or "put" or "patch" or "delete"))
        {
            throw new InvalidOperationException($"Operation {operation.OperationId} declares idempotency equivalence on non-mutating method {operation.Method}.");
        }

        if (operation.RequestSchema is null)
        {
            throw new InvalidOperationException($"Operation {operation.OperationId} declares idempotency equivalence without a request schema.");
        }

        if (!operation.IdempotencyFields.SequenceEqual(operation.IdempotencyFields.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Operation {operation.OperationId} idempotency fields are not in lexicographic order.");
        }

        HashSet<string> uniqueFields = new(StringComparer.Ordinal);
        foreach (string field in operation.IdempotencyFields)
        {
            if (!uniqueFields.Add(field))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} declares duplicate idempotency field '{field}'.");
            }
        }

        YamlMappingNode schema = RequiredMapping(schemas, operation.RequestSchema);
        IReadOnlyDictionary<string, SchemaPropertyModel> schemaProperties = ReadProperties(schema);
        List<FieldModel> fields = [];
        List<ParameterModel> helperParameters = [];

        foreach (string field in operation.IdempotencyFields)
        {
            FieldModel fieldModel = ResolveField(operation, field, operation.RequestSchema, schemaProperties, helperParameters);
            fields.Add(fieldModel);
        }

        IReadOnlyList<ParameterModel> orderedParameters = helperParameters
            .OrderBy(p => operation.Parameters.Select((parameter, index) => new { parameter, index })
                .FirstOrDefault(x => NormalizeName(x.parameter.Name) == p.Field)?.index ?? int.MaxValue)
            .ThenBy(p => p.Field, StringComparer.Ordinal)
            .ToArray();

        if (!variantsBySchema.TryGetValue(operation.RequestSchema, out List<HelperVariantModel>? variants))
        {
            variants = [];
            variantsBySchema.Add(operation.RequestSchema, variants);
        }

        variants.Add(new HelperVariantModel(operation.OperationId, operation.IdempotencyFields, fields, orderedParameters));
    }

    List<HelperModel> helpers = variantsBySchema
        .Select(group => new HelperModel(
            group.Key,
            group.Value.OrderBy(v => v.OperationId, StringComparer.Ordinal).ToArray(),
            group.Value
                .SelectMany(v => v.Parameters)
                .GroupBy(p => p.Field, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray()))
        .OrderBy(h => h.SchemaName, StringComparer.Ordinal)
        .ToList();

    if (!helpers.Any(h => h.SchemaName == "FileMutationRequest"))
    {
        throw new InvalidOperationException("FileMutationRequest helper was not generated from the Contract Spine.");
    }

    return helpers;
}

static FieldModel ResolveField(
    OperationModel operation,
    string field,
    string schemaName,
    IReadOnlyDictionary<string, SchemaPropertyModel> schemaProperties,
    List<ParameterModel> helperParameters)
{
    Dictionary<string, string> operationParameters = [];
    foreach (ParameterModel parameter in operation.Parameters)
    {
        string normalized = NormalizeName(parameter.Name);
        if (operationParameters.TryGetValue(normalized, out string? existing))
        {
            if (!string.Equals(existing, parameter.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} has duplicate normalized parameter '{normalized}' from '{existing}' and '{parameter.Name}'.");
            }

            continue;
        }

        operationParameters.Add(normalized, parameter.Name);
    }

    if (operationParameters.TryGetValue(field, out string? parameterName))
    {
        string parameter = EnsureParameter(helperParameters, field, parameterName);
        return new FieldModel(field, "true", parameter);
    }

    if (TryResolveSpecialField(schemaName, field, out FieldModel? specialField))
    {
        return specialField!;
    }

    string[] parts = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
    string[] bodyParts = parts;
    if (parts.Length > 1 && !schemaProperties.ContainsKey(ToJsonPropertyName(parts[0])) && SchemaMatchesLogicalPrefix(schemaName, parts[0]))
    {
        bodyParts = parts[1..];
    }

    string firstJsonName = ToJsonPropertyName(bodyParts[0]);
    if (!schemaProperties.ContainsKey(firstJsonName))
    {
        throw new InvalidOperationException($"Operation {operation.OperationId} field '{field}' does not resolve to a request property or operation parameter.");
    }

    string expression = string.Join(".", bodyParts.Select(ToPropertyName));
    if (bodyParts.Length == 1)
    {
        return new FieldModel(field, "true", expression);
    }

    string rootProperty = ToPropertyName(bodyParts[0]);
    return new FieldModel(field, $"{rootProperty} is not null", $"{rootProperty}?.{string.Join(".", bodyParts.Skip(1).Select(ToPropertyName))}");
}

static bool TryResolveSpecialField(string schemaName, string field, out FieldModel? model)
{
    // FileMutationRequest spine fields are declared at components.schemas.FileMutationRequest.
    model = (schemaName, field) switch
    {
        ("FileMutationRequest", "content_hash_reference") => new FieldModel("content_hash_reference", "ContentHashReference is not null", "ContentHashReference"),
        ("FileMutationRequest", "file_operation_kind") => new FieldModel("file_operation_kind", "true", "ResolveFileMutationOperationKindWireValue()"),
        ("FileMutationRequest", "operation_id") => new FieldModel("operation_id", "OperationId is not null", "OperationId"),
        ("FileMutationRequest", "path_metadata") => new FieldModel("path_metadata", "PathMetadata is not null", "PathMetadata"),
        ("FileMutationRequest", "path_policy_class") => new FieldModel("path_policy_class", "PathMetadata is not null && PathMetadata.PathPolicyClass is not null", "PathMetadata?.PathPolicyClass"),
        _ => null,
    };

    return model is not null;
}

static bool SchemaMatchesLogicalPrefix(string schemaName, string prefix)
{
    string normalizedSchema = NormalizeName(schemaName);
    string normalizedPrefix = NormalizeName(prefix);
    return normalizedSchema == normalizedPrefix + "_request";
}

static string EnsureParameter(List<ParameterModel> parameters, string field, string openApiName)
{
    _ = openApiName;
    string name = ToParameterName(field);
    if (!parameters.Any(p => p.Name == name))
    {
        parameters.Add(new ParameterModel(field, name));
    }

    return name;
}

static IReadOnlyDictionary<string, SchemaPropertyModel> ReadProperties(YamlMappingNode schema)
{
    Dictionary<string, SchemaPropertyModel> properties = new(StringComparer.Ordinal);
    AddProperties(schema, properties);

    if (schema.Children.TryGetValue(new YamlScalarNode("oneOf"), out YamlNode? oneOfNode))
    {
        foreach (YamlNode branchNode in oneOfNode.ShouldBeSequence("oneOf"))
        {
            AddProperties(branchNode.ShouldBeMapping("oneOf branch"), properties);
        }
    }

    return properties;
}

static void AddProperties(YamlMappingNode schema, Dictionary<string, SchemaPropertyModel> properties)
{
    HashSet<string> required = ReadStringSequence(schema, "required").ToHashSet(StringComparer.Ordinal);
    if (!schema.Children.TryGetValue(new YamlScalarNode("properties"), out YamlNode? propertiesNode))
    {
        return;
    }

    foreach (KeyValuePair<YamlNode, YamlNode> property in propertiesNode.ShouldBeMapping("properties").Children)
    {
        string name = property.Key.ShouldBeScalar("property").Value ?? string.Empty;
        properties[name] = new SchemaPropertyModel(property.Value.ShouldBeMapping("property"), required.Contains(name));
    }
}

static IEnumerable<OperationModel> EnumerateOperations(YamlMappingNode root)
{
    foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
    {
        string path = pathEntry.Key.ShouldBeScalar("path").Value ?? string.Empty;
        YamlMappingNode pathItem = pathEntry.Value.ShouldBeMapping("path item");
        IReadOnlyList<ParameterModel> pathParameters = EnumerateParameters(pathItem).ToArray();

        foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
        {
            string method = (methodEntry.Key.ShouldBeScalar("method").Value ?? string.Empty).ToLowerInvariant();
            if (method is not ("get" or "post" or "put" or "patch" or "delete"))
            {
                continue;
            }

            YamlMappingNode operation = methodEntry.Value.ShouldBeMapping("operation");
            string operationId = RequiredScalar(operation, "operationId");
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
            List<ParameterModel> parameters = [.. pathParameters, .. EnumerateParameters(operation)];
            string? requestSchema = TryReadRequestSchema(operation);
            IReadOnlyList<string> fields = ReadStringSequence(operation, "x-hexalith-idempotency-equivalence");
            yield return new OperationModel(path, method, operationId, requestSchema, parameters, fields);
        }
    }
}

static IEnumerable<ParameterModel> EnumerateParameters(YamlMappingNode mapping)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
    {
        yield break;
    }

    foreach (YamlNode parameterNode in parametersNode.ShouldBeSequence("parameters"))
    {
        YamlMappingNode parameter = parameterNode.ShouldBeMapping("parameter");
        if (parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? referenceNode))
        {
            string reference = referenceNode.ShouldBeScalar("$ref").Value ?? string.Empty;
            string name = reference.Split('/').Last();
            yield return new ParameterModel(NormalizeName(name), name);
        }
        else if (parameter.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? nameNode))
        {
            string name = nameNode.ShouldBeScalar("name").Value ?? string.Empty;
            yield return new ParameterModel(NormalizeName(name), name);
        }
    }
}

static string? TryReadRequestSchema(YamlMappingNode operation)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode("requestBody"), out YamlNode? requestBodyNode))
    {
        return null;
    }

    YamlMappingNode requestBody = requestBodyNode.ShouldBeMapping("requestBody");
    YamlMappingNode content = RequiredMapping(requestBody, "content");
    YamlMappingNode json = RequiredMapping(content, "application/json");
    YamlMappingNode schema = RequiredMapping(json, "schema");
    string reference = RequiredScalar(schema, "$ref");
    return reference.Split('/').Last();
}

static string Render(IReadOnlyList<HelperModel> helpers, string contractHash, string configurationHash)
{
    StringBuilder code = new();
    code.AppendLine("// <auto-generated />");
    code.AppendLine("#nullable enable");
    code.AppendLine();
    code.AppendLine("using System.Text.RegularExpressions;");
    code.AppendLine("using Hexalith.Folders.Client.Idempotency;");
    code.AppendLine("using Newtonsoft.Json;");
    code.AppendLine();
    code.AppendLine("namespace Hexalith.Folders.Client.Generated;");
    code.AppendLine();
    code.AppendLine("/// <summary>");
    code.AppendLine("/// NSwag duplicate-name compatibility shim for ChangedPathEvidence references in AuditRecord and WorkspaceDiagnostic.");
    code.AppendLine("/// Remove when the Contract Spine or NSwag configuration emits one stable ChangedPathEvidence type.");
    code.AppendLine("/// </summary>");
    code.AppendLine("public partial class ChangedPathEvidence2 : ChangedPathEvidence");
    code.AppendLine("{");
    code.AppendLine("}");
    code.AppendLine();
    code.AppendLine("public sealed record HexalithFoldersGeneratedArtifactsVerification(bool IsCurrent, string Diagnostic);");
    code.AppendLine();
    code.AppendLine("public static class HexalithFoldersGeneratedArtifacts");
    code.AppendLine("{");
    code.AppendLine($"    public const string ContractSpineSha256 = \"{contractHash}\";");
    code.AppendLine($"    public const string GenerationConfigurationSha256 = \"{configurationHash}\";");
    code.AppendLine("    public const string GeneratedHelpersSha256 = \"__GENERATED_HELPERS_SHA256__\";");
    code.AppendLine();
    code.AppendLine("    public static bool VerifyCurrent(string repositoryRoot) => VerifyCurrentDetailed(repositoryRoot).IsCurrent;");
    code.AppendLine();
    code.AppendLine("    public static HexalithFoldersGeneratedArtifactsVerification VerifyCurrentDetailed(string repositoryRoot)");
    code.AppendLine("    {");
    code.AppendLine("        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot))");
    code.AppendLine("        {");
    code.AppendLine("            return new(false, \"repositoryRoot must be a fully qualified path\");");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        try");
    code.AppendLine("        {");
    code.AppendLine("            string spine = File.ReadAllText(Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Contracts\", \"openapi\", \"hexalith.folders.v1.yaml\"));");
    code.AppendLine("            string configuration = File.ReadAllText(Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Client\", \"nswag.json\"));");
    code.AppendLine("            string helpers = File.ReadAllText(Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Client\", \"Generated\", \"HexalithFoldersIdempotencyHelpers.g.cs\"));");
    code.AppendLine("            return IsCurrent(spine, configuration, helpers)");
    code.AppendLine("                ? new(true, \"generated artifacts match Contract Spine inputs\")");
    code.AppendLine("                : new(false, \"generated artifact content hashes do not match Contract Spine inputs\");");
    code.AppendLine("        }");
    code.AppendLine("        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)");
    code.AppendLine("        {");
    code.AppendLine("            return new(false, exception.GetType().Name);");
    code.AppendLine("        }");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    public static bool IsCurrent(string contractSpine, string generationConfiguration, string generatedHelpers)");
    code.AppendLine("    {");
    code.AppendLine("        ArgumentNullException.ThrowIfNull(contractSpine);");
    code.AppendLine("        ArgumentNullException.ThrowIfNull(generationConfiguration);");
    code.AppendLine("        ArgumentNullException.ThrowIfNull(generatedHelpers);");
    code.AppendLine("        return ComputeSha256(NormalizeText(contractSpine)) == ContractSpineSha256");
    code.AppendLine("            && ComputeSha256(NormalizeText(generationConfiguration)) == GenerationConfigurationSha256");
    code.AppendLine("            && ComputeSha256(NormalizeGeneratedHelperHash(generatedHelpers)) == GeneratedHelpersSha256;");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    private static string NormalizeGeneratedHelperHash(string value) =>");
    code.AppendLine("        Regex.Replace(NormalizeText(value), \"^\\\\s*public const string GeneratedHelpersSha256 = \\\"[0-9a-fA-F_]+\\\";\", \"    public const string GeneratedHelpersSha256 = \\\"__GENERATED_HELPERS_SHA256__\\\";\", RegexOptions.CultureInvariant | RegexOptions.Multiline);");
    code.AppendLine();
    code.AppendLine("    private static string NormalizeText(string value) => value.Replace(\"\\r\\n\", \"\\n\", StringComparison.Ordinal).Replace(\"\\r\", \"\\n\", StringComparison.Ordinal);");
    code.AppendLine();
    code.AppendLine("    private static string ComputeSha256(string value) =>");
    code.AppendLine("        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();");
    code.AppendLine("}");
    code.AppendLine();
    code.AppendLine("public partial class HexalithFoldersApiException");
    code.AppendLine("{");
    code.AppendLine("    public ProblemDetails? ProblemDetails => TryGetProblemDetails();");
    code.AppendLine();
    code.AppendLine("    public string? ProblemDetailsParseDiagnostic => TryReadProblemDetails(out _);");
    code.AppendLine();
    code.AppendLine("    private ProblemDetails? TryGetProblemDetails()");
    code.AppendLine("    {");
    code.AppendLine("        if (this is HexalithFoldersApiException<ProblemDetails> typed)");
    code.AppendLine("        {");
    code.AppendLine("            return typed.Result;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        return TryReadProblemDetails(out ProblemDetails? problem) is null ? problem : null;");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    private string? TryReadProblemDetails(out ProblemDetails? problem)");
    code.AppendLine("    {");
    code.AppendLine("        problem = null;");
    code.AppendLine("        if (string.IsNullOrWhiteSpace(Response))");
    code.AppendLine("        {");
    code.AppendLine("            return null;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        try");
    code.AppendLine("        {");
    code.AppendLine("            using StringReader stringReader = new(Response);");
    code.AppendLine("            using JsonTextReader jsonReader = new(stringReader)");
    code.AppendLine("            {");
    code.AppendLine("                DateParseHandling = DateParseHandling.None,");
    code.AppendLine("                FloatParseHandling = FloatParseHandling.Decimal,");
    code.AppendLine("            };");
    code.AppendLine("            JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings");
    code.AppendLine("            {");
    code.AppendLine("                Culture = System.Globalization.CultureInfo.InvariantCulture,");
    code.AppendLine("            });");
    code.AppendLine("            problem = serializer.Deserialize<ProblemDetails>(jsonReader);");
    code.AppendLine("            return null;");
    code.AppendLine("        }");
    code.AppendLine("        catch (JsonException exception)");
    code.AppendLine("        {");
    code.AppendLine("            return exception.GetType().Name;");
    code.AppendLine("        }");
    code.AppendLine("    }");
    code.AppendLine("}");
    code.AppendLine();

    foreach (HelperModel helper in helpers)
    {
        RenderHelper(code, helper);
    }

    return code.ToString().ReplaceLineEndings("\n");
}

static void RenderHelper(StringBuilder code, HelperModel helper)
{
    code.AppendLine($"public partial class {helper.SchemaName}");
    code.AppendLine("{");

    if (helper.SchemaName == "CreateFolderRequest")
    {
        code.AppendLine("    [JsonIgnore]");
        code.AppendLine("    public bool ParentFolderIdSpecified { get; set; }");
        code.AppendLine();
    }

    if (helper.SchemaName == "FileMutationRequest")
    {
        code.AppendLine("    [JsonProperty(\"requestSchemaVersion\", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]");
        code.AppendLine("    public string? RequestSchemaVersion { get; set; }");
        code.AppendLine();
        code.AppendLine("    [JsonProperty(\"operationId\", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]");
        code.AppendLine("    public string? OperationId { get; set; }");
        code.AppendLine();
        code.AppendLine("    [JsonProperty(\"pathMetadata\", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]");
        code.AppendLine("    public PathMetadata? PathMetadata { get; set; }");
        code.AppendLine();
        code.AppendLine("    [JsonProperty(\"contentHashReference\", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]");
        code.AppendLine("    public string? ContentHashReference { get; set; }");
        code.AppendLine();
        code.AppendLine("    private const FileMutationRequestFileOperationKind RemoveFileOperationKind = (FileMutationRequestFileOperationKind)2;");
        code.AppendLine();
    }

    string parameters = string.Join(", ", helper.Parameters.Select(p => "string " + p.Name));
    if (helper.Variants.Count == 1)
    {
        code.AppendLine($"    public string ComputeIdempotencyHash({parameters}) =>");
        RenderComputeExpression(code, helper.Variants[0], "        ");
        code.AppendLine(";");
    }
    else
    {
        code.AppendLine($"    public string ComputeIdempotencyHash({parameters})");
        code.AppendLine("    {");
        code.AppendLine("        string operationId = ResolveFileMutationOperationId();");
        code.AppendLine("        return operationId switch");
        code.AppendLine("        {");
        foreach (HelperVariantModel variant in helper.Variants)
        {
            code.AppendLine($"            \"{variant.OperationId}\" =>");
            RenderComputeExpression(code, variant, "                ");
            code.AppendLine("            ,");
        }

        code.AppendLine("            _ => throw new InvalidOperationException($\"Unsupported file mutation operation '{operationId}'.\"),");
        code.AppendLine("        };");
        code.AppendLine("    }");
    }

    if (helper.SchemaName == "FileMutationRequest")
    {
        code.AppendLine();
        code.AppendLine("    private string ResolveFileMutationOperationId() => FileOperationKind switch");
        code.AppendLine("    {");
        code.AppendLine("        FileMutationRequestFileOperationKind.Add => \"AddFile\",");
        code.AppendLine("        FileMutationRequestFileOperationKind.Change => \"ChangeFile\",");
        code.AppendLine("        RemoveFileOperationKind => \"RemoveFile\",");
        code.AppendLine("        _ => throw new InvalidOperationException($\"Unsupported file operation kind '{FileOperationKind}'.\"),");
        code.AppendLine("    };");
        code.AppendLine();
        code.AppendLine("    private string ResolveFileMutationOperationKindWireValue() => FileOperationKind switch");
        code.AppendLine("    {");
        code.AppendLine("        FileMutationRequestFileOperationKind.Add => \"add\",");
        code.AppendLine("        FileMutationRequestFileOperationKind.Change => \"change\",");
        code.AppendLine("        RemoveFileOperationKind => \"remove\",");
        code.AppendLine("        _ => throw new InvalidOperationException($\"Unsupported file operation kind '{FileOperationKind}'.\"),");
        code.AppendLine("    };");
    }

    code.AppendLine("}");
    code.AppendLine();
}

static void RenderComputeExpression(StringBuilder code, HelperVariantModel variant, string indent)
{
    code.AppendLine(indent + "HexalithIdempotencyHasher.Compute(");
    code.AppendLine(indent + $"    \"{variant.OperationId}\",");
    code.AppendLine(indent + "    new[]");
    code.AppendLine(indent + "    {");
    foreach (FieldModel field in variant.Fields)
    {
        string present = field.Path == "parent_folder_id" ? "ParentFolderIdSpecified || ParentFolderId is not null" : field.PresentExpression;
        code.AppendLine(indent + $"        new IdempotencyField(\"{field.Path}\", {present}, {field.ValueExpression}),");
    }

    code.AppendLine(indent + "    })");
}

static YamlMappingNode LoadYaml(string path)
{
    using StreamReader reader = File.OpenText(path);
    YamlStream yaml = new();
    yaml.Load(reader);
    return yaml.Documents[0].RootNode.ShouldBeMapping("root");
}

static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
    {
        throw new InvalidOperationException($"Missing mapping '{key}'.");
    }

    return value.ShouldBeMapping(key);
}

static string RequiredScalar(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
    {
        throw new InvalidOperationException($"Missing scalar '{key}'.");
    }

    return value.ShouldBeScalar(key).Value ?? string.Empty;
}

static IReadOnlyList<string> ReadStringSequence(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
    {
        return [];
    }

    return value.ShouldBeSequence(key).Children.Select(n => n.ShouldBeScalar(key).Value ?? string.Empty).ToArray();
}

static string ToJsonPropertyName(string snake) => ToParameterName(snake);

static string ToPropertyName(string snake)
{
    string camel = ToParameterName(snake);
    return char.ToUpperInvariant(camel[0]) + camel[1..];
}

static string ToParameterName(string value)
{
    if (!value.Contains('_', StringComparison.Ordinal) && !value.Contains('-', StringComparison.Ordinal))
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    string[] parts = value.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
    return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}

static string NormalizeName(string value)
{
    StringBuilder builder = new(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
        char character = value[i];
        if (character is '_' or '-')
        {
            if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }
        else if (char.IsLetterOrDigit(character))
        {
            if (char.IsUpper(character) && i > 0 && builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }
    }

    string normalized = builder.ToString();
    return normalized switch
    {
        "x_hexalith_task_id" => "task_id",
        "idempotency_key" => "idempotency_key",
        "correlation_id" => "correlation_id",
        _ => normalized,
    };
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    Dictionary<string, string> parsed = new(StringComparer.Ordinal);
    for (int i = 0; i < values.Length; i += 2)
    {
        if (!values[i].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Expected argument name starting with '--' at position {i}, but found '{values[i]}'.");
        }

        if (i + 1 >= values.Length)
        {
            throw new ArgumentException($"Missing value for {values[i]}.");
        }

        parsed[values[i]] = values[i + 1];
    }

    return parsed;
}

static string RequiredArgument(IReadOnlyDictionary<string, string> values, string key) =>
    values.TryGetValue(key, out string? value) ? value : throw new ArgumentException($"Missing argument {key}.");

static string NormalizeText(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

static string NormalizeGeneratedHelperHash(string value) =>
    Regex.Replace(NormalizeText(value), "^\\s*public const string GeneratedHelpersSha256 = \"[0-9a-fA-F_]+\";", "    public const string GeneratedHelpersSha256 = \"__GENERATED_HELPERS_SHA256__\";", RegexOptions.CultureInvariant | RegexOptions.Multiline);

static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

internal sealed record OperationModel(
    string Path,
    string Method,
    string OperationId,
    string? RequestSchema,
    IReadOnlyList<ParameterModel> Parameters,
    IReadOnlyList<string> IdempotencyFields);

internal sealed record ParameterModel(string Field, string Name);

internal sealed record FieldModel(string Path, string PresentExpression, string ValueExpression);

internal sealed record SchemaPropertyModel(YamlMappingNode Schema, bool Required);

internal sealed record HelperVariantModel(
    string OperationId,
    IReadOnlyList<string> DeclaredFields,
    IReadOnlyList<FieldModel> Fields,
    IReadOnlyList<ParameterModel> Parameters);

internal sealed record HelperModel(
    string SchemaName,
    IReadOnlyList<HelperVariantModel> Variants,
    IReadOnlyList<ParameterModel> Parameters);

internal static class YamlNodeExtensions
{
    public static YamlMappingNode ShouldBeMapping(this YamlNode node, string name) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{name} must be a mapping.");

    public static YamlSequenceNode ShouldBeSequence(this YamlNode node, string name) =>
        node as YamlSequenceNode ?? throw new InvalidOperationException($"{name} must be a sequence.");

    public static YamlScalarNode ShouldBeScalar(this YamlNode node, string name) =>
        node as YamlScalarNode ?? throw new InvalidOperationException($"{name} must be a scalar.");
}
