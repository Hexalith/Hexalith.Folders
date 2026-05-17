using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using Hexalith.Folders.Client.Generation.Shared;
using static Hexalith.Folders.Client.Generation.Shared.YamlContractLoader;

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
const string PlaceholderToken = "__GENERATED_HELPERS_SHA256__";
const string ConstDeclarationPrefix = "    public const string GeneratedHelpersSha256 = \"";
string constDeclarationSentinel = ConstDeclarationPrefix + PlaceholderToken + "\";";
int constLineIndex = output.IndexOf(constDeclarationSentinel, StringComparison.Ordinal);
if (constLineIndex < 0)
{
    throw new InvalidOperationException("Generated helper hash placeholder const declaration was not emitted.");
}

if (output.IndexOf(constDeclarationSentinel, constLineIndex + constDeclarationSentinel.Length, StringComparison.Ordinal) >= 0)
{
    throw new InvalidOperationException("Generated helper hash placeholder const declaration was emitted more than once.");
}

int placeholderOffset = constLineIndex + ConstDeclarationPrefix.Length;
output = output.Remove(placeholderOffset, PlaceholderToken.Length).Insert(placeholderOffset, helperHash);
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

        string? outOfOrderPrevious = null;
        string? outOfOrderCurrent = null;
        string? previousField = null;
        foreach (string field in operation.IdempotencyFields)
        {
            if (previousField is not null && string.CompareOrdinal(previousField, field) > 0)
            {
                outOfOrderPrevious = previousField;
                outOfOrderCurrent = field;
                break;
            }

            previousField = field;
        }

        if (outOfOrderPrevious is not null)
        {
            throw new InvalidOperationException($"Operation {operation.OperationId} idempotency fields are not in lexicographic order: '{outOfOrderPrevious}' must come after '{outOfOrderCurrent}'.");
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
        string parameter = EnsureParameter(helperParameters, field, parameterName, operation.OperationId);
        return new FieldModel(field, "true", parameter);
    }

    if (SpecialFields.Registry.TryGetValue((schemaName, field), out FieldModel? specialField))
    {
        return specialField;
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

static bool SchemaMatchesLogicalPrefix(string schemaName, string prefix)
{
    string normalizedSchema = NormalizeName(schemaName);
    string normalizedPrefix = NormalizeName(prefix);
    return normalizedSchema == normalizedPrefix + "_request";
}

static string EnsureParameter(List<ParameterModel> parameters, string field, string spineParameterName, string operationId)
{
    string name = ToParameterName(field);
    // Validate that the spine-declared parameter name and the emitted helper parameter name resolve
    // to the same logical identifier under NormalizeName. The spine may declare either camelCase
    // (`folderId`) or PascalCase (`FolderId`, when sourced via $ref to a components/parameters entry);
    // either form must normalize to the same snake_case as the idempotency-equivalence field. A
    // mismatch here is Contract Spine drift — fail closed rather than silently emitting two
    // different parameter slots. Round 4 review (P5).
    if (!string.Equals(NormalizeName(name), NormalizeName(spineParameterName), StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Operation {operationId} idempotency field '{field}' resolves to helper parameter '{name}' " +
            $"but the spine-declared parameter normalizes differently from '{spineParameterName}'. " +
            "Fix the spine parameter name or update the snake-to-camel rule in YamlContractLoader.ToParameterName.");
    }

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

// TryReadRequestSchema is provided by YamlContractLoader (imported via `using static`).

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
    // ChangedPathEvidence2 NSwag-duplicate-name shim lives in
    // src/Hexalith.Folders.Client/Compat/ChangedPathEvidenceShim.cs as a hand-written companion
    // partial. Generated/ stays purely generated.
    code.AppendLine("public sealed record HexalithFoldersGeneratedArtifactsVerification(bool IsCurrent, string Diagnostic);");
    code.AppendLine();
    code.AppendLine("public static class HexalithFoldersGeneratedArtifacts");
    code.AppendLine("{");
    code.AppendLine($"    public const string ContractSpineSha256 = \"{contractHash}\";");
    code.AppendLine($"    public const string GenerationConfigurationSha256 = \"{configurationHash}\";");
    code.AppendLine("    public const string GeneratedHelpersSha256 = \"__GENERATED_HELPERS_SHA256__\";");
    code.AppendLine();
    code.AppendLine("    // HelperSchemaVersion is a deterministic SHA-256 prefix of the canonical helper-signature");
    code.AppendLine("    // shape (schema names, parameter names in declared order, idempotency field paths per");
    code.AppendLine("    // variant). It changes whenever helper parameter shapes change, and only then. Downstream");
    code.AppendLine("    // consumers can branch on it to detect positional-parameter renames. Not included in the");
    code.AppendLine("    // canonical hash. Round 4 review (P4) replaced the hardcoded date literal with this");
    code.AppendLine("    // signature-derived constant so a stable spine produces a stable version.");
    code.AppendLine($"    public const string HelperSchemaVersion = \"{ComputeHelperSchemaVersion(helpers)}\";");
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
    code.AppendLine("        string spinePath = Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Contracts\", \"openapi\", \"hexalith.folders.v1.yaml\");");
    code.AppendLine("        string configurationPath = Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Client\", \"nswag.json\");");
    code.AppendLine("        string helpersPath = Path.Combine(repositoryRoot, \"src\", \"Hexalith.Folders.Client\", \"Generated\", \"HexalithFoldersIdempotencyHelpers.g.cs\");");
    code.AppendLine("        try");
    code.AppendLine("        {");
    code.AppendLine("            string spine = File.ReadAllText(spinePath);");
    code.AppendLine("            string configuration = File.ReadAllText(configurationPath);");
    code.AppendLine("            string helpers = File.ReadAllText(helpersPath);");
    code.AppendLine("            string? malformed = DetectMalformedHelperConstant(helpers);");
    code.AppendLine("            if (malformed is not null)");
    code.AppendLine("            {");
    code.AppendLine("                return new(false, malformed);");
    code.AppendLine("            }");
    code.AppendLine();
    code.AppendLine("            return IsCurrent(spine, configuration, helpers)");
    code.AppendLine("                ? new(true, \"generated artifacts match Contract Spine inputs\")");
    code.AppendLine("                : new(false, \"generated artifact content hashes do not match Contract Spine inputs\");");
    code.AppendLine("        }");
    code.AppendLine("        catch (FileNotFoundException notFound)");
    code.AppendLine("        {");
    code.AppendLine("            return new(false, $\"{nameof(FileNotFoundException)}: {Relativize(repositoryRoot, notFound.FileName ?? notFound.Message)}\");");
    code.AppendLine("        }");
    code.AppendLine("        catch (DirectoryNotFoundException directoryMissing)");
    code.AppendLine("        {");
    code.AppendLine("            return new(false, $\"{nameof(DirectoryNotFoundException)}: {Relativize(repositoryRoot, directoryMissing.Message)}\");");
    code.AppendLine("        }");
    code.AppendLine("        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)");
    code.AppendLine("        {");
    code.AppendLine("            return new(false, $\"{exception.GetType().Name}: {Relativize(repositoryRoot, exception.Message)}\");");
    code.AppendLine("        }");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    // Strips repositoryRoot+separator from diagnostic strings so the emitted message");
    code.AppendLine("    // does not leak machine-local absolute paths. AC 16 / Round 4 P11 / Round 4 (external) P2.");
    code.AppendLine("    // Case comparison follows the host filesystem (case-insensitive on Windows, case-sensitive");
    code.AppendLine("    // elsewhere). Only the root-plus-separator forms are stripped to avoid corrupting unrelated");
    code.AppendLine("    // substrings that happen to share the bare root prefix (e.g. \"C:\\\\Work\" inside \"C:\\\\Workspace\\\\\").");
    code.AppendLine("    private static string Relativize(string repositoryRoot, string message)");
    code.AppendLine("    {");
    code.AppendLine("        if (string.IsNullOrEmpty(message))");
    code.AppendLine("        {");
    code.AppendLine("            return message;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        StringComparison comparison = OperatingSystem.IsWindows()");
    code.AppendLine("            ? StringComparison.OrdinalIgnoreCase");
    code.AppendLine("            : StringComparison.Ordinal;");
    code.AppendLine("        string trimmedRoot = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);");
    code.AppendLine("        string primaryPrefix = trimmedRoot + Path.DirectorySeparatorChar;");
    code.AppendLine("        string altPrefix = trimmedRoot + Path.AltDirectorySeparatorChar;");
    code.AppendLine();
    code.AppendLine("        string result = message.Replace(primaryPrefix, string.Empty, comparison);");
    code.AppendLine("        if (!string.Equals(primaryPrefix, altPrefix, StringComparison.Ordinal))");
    code.AppendLine("        {");
    code.AppendLine("            result = result.Replace(altPrefix, string.Empty, comparison);");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        return result;");
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
    code.AppendLine("        Regex.Replace(NormalizeText(value), \"^(?<indent>[ \\\\t]*)public const string GeneratedHelpersSha256 = \\\"(?:[0-9a-fA-F]{64}|__GENERATED_HELPERS_SHA256__)\\\";[ \\\\t]*$\", \"${indent}public const string GeneratedHelpersSha256 = \\\"__GENERATED_HELPERS_SHA256__\\\";\", RegexOptions.CultureInvariant | RegexOptions.Multiline);");
    code.AppendLine();
    code.AppendLine("    // Detects a malformed `public const string GeneratedHelpersSha256 = \"...\";` constant so");
    code.AppendLine("    // VerifyCurrentDetailed can emit a distinct \"corrupted local file\" diagnostic instead of");
    code.AppendLine("    // the generic \"content hashes do not match\" message that misdirects operators to spine");
    code.AppendLine("    // drift. Returns null when the constant is well-formed (64 hex chars or the placeholder).");
    code.AppendLine("    // Round 4 (external) P8.");
    code.AppendLine("    private static string? DetectMalformedHelperConstant(string helpers)");
    code.AppendLine("    {");
    code.AppendLine("        Match permissive = Regex.Match(NormalizeText(helpers), \"^[ \\\\t]*public const string GeneratedHelpersSha256 = \\\"(?<value>[^\\\"]*)\\\";\", RegexOptions.CultureInvariant | RegexOptions.Multiline);");
    code.AppendLine("        if (!permissive.Success)");
    code.AppendLine("        {");
    code.AppendLine("            return \"generated helpers file is missing the GeneratedHelpersSha256 constant declaration (regenerate via dotnet msbuild /t:GenerateHexalithFoldersClient)\";");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        string captured = permissive.Groups[\"value\"].Value;");
    code.AppendLine("        if (captured == \"__GENERATED_HELPERS_SHA256__\")");
    code.AppendLine("        {");
    code.AppendLine("            return null;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        if (captured.Length != 64 || !captured.All(static c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))");
    code.AppendLine("        {");
    code.AppendLine("            return $\"generated helpers file has a malformed GeneratedHelpersSha256 constant (expected 64 hex characters, got {captured.Length}); regenerate via dotnet msbuild /t:GenerateHexalithFoldersClient\";");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        return null;");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    private static string NormalizeText(string value) => value.Replace(\"\\r\\n\", \"\\n\", StringComparison.Ordinal).Replace(\"\\r\", \"\\n\", StringComparison.Ordinal);");
    code.AppendLine();
    code.AppendLine("    private static string ComputeSha256(string value) =>");
    code.AppendLine("        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();");
    code.AppendLine("}");
    code.AppendLine();
    code.AppendLine("public partial class HexalithFoldersApiException");
    code.AppendLine("{");
    code.AppendLine("    // Thread-safe lazy cache for the parsed Problem Details. Reference-typed wrapper plus");
    code.AppendLine("    // Interlocked.CompareExchange gives lock-free single-VISIBLE-value across concurrent readers.");
    code.AppendLine("    // Note (Round 4 external P11): the actual ParseProblemDetails call may run more than once");
    code.AppendLine("    // under contention — two threads can each enter ParseProblemDetails, both produce a fresh");
    code.AppendLine("    // ParsedProblemDetailsCache, and the loser's value is discarded by CompareExchange. Both");
    code.AppendLine("    // readers then observe the winner's cache, so the visible value is single-init even though");
    code.AppendLine("    // the parse work is not strictly de-duplicated. This is acceptable because parsing is pure;");
    code.AppendLine("    // if parse cost or side-effect concerns surface, switch to LazyInitializer.EnsureInitialized");
    code.AppendLine("    // or System.Threading.Lazy<T> for a true single-parse guarantee.");
    code.AppendLine("    private sealed class ParsedProblemDetailsCache");
    code.AppendLine("    {");
    code.AppendLine("        public ParsedProblemDetailsCache(ProblemDetails? problem, string? diagnostic)");
    code.AppendLine("        {");
    code.AppendLine("            Problem = problem;");
    code.AppendLine("            Diagnostic = diagnostic;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        public ProblemDetails? Problem { get; }");
    code.AppendLine("        public string? Diagnostic { get; }");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    private ParsedProblemDetailsCache? _parsedProblemDetails;");
    code.AppendLine();
    code.AppendLine("    public ProblemDetails? ProblemDetails => GetParsedProblemDetails().Problem;");
    code.AppendLine();
    code.AppendLine("    public string? ProblemDetailsParseDiagnostic => GetParsedProblemDetails().Diagnostic;");
    code.AppendLine();
    code.AppendLine("    private ParsedProblemDetailsCache GetParsedProblemDetails()");
    code.AppendLine("    {");
    code.AppendLine("        ParsedProblemDetailsCache? cached = _parsedProblemDetails;");
    code.AppendLine("        if (cached is not null)");
    code.AppendLine("        {");
    code.AppendLine("            return cached;");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        (ProblemDetails? Problem, string? Diagnostic) parsed = ParseProblemDetails();");
    code.AppendLine("        ParsedProblemDetailsCache fresh = new(parsed.Problem, parsed.Diagnostic);");
    code.AppendLine("        return System.Threading.Interlocked.CompareExchange(ref _parsedProblemDetails, fresh, null) ?? fresh;");
    code.AppendLine("    }");
    code.AppendLine();
    code.AppendLine("    private (ProblemDetails? Problem, string? Diagnostic) ParseProblemDetails()");
    code.AppendLine("    {");
    code.AppendLine("        if (this is HexalithFoldersApiException<ProblemDetails> typed)");
    code.AppendLine("        {");
    code.AppendLine("            return (typed.Result, null);");
    code.AppendLine("        }");
    code.AppendLine();
    code.AppendLine("        if (string.IsNullOrWhiteSpace(Response))");
    code.AppendLine("        {");
    code.AppendLine("            return (null, null);");
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
    code.AppendLine("            return (serializer.Deserialize<ProblemDetails>(jsonReader), null);");
    code.AppendLine("        }");
    code.AppendLine("        catch (Exception exception) when (exception is JsonException or JsonSerializationException)");
    code.AppendLine("        {");
    code.AppendLine("            return (null, exception.GetType().Name);");
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
        code.AppendLine("    // NSwag emits FileMutationRequestFileOperationKind with Add=0 and Change=1 only; the spine's");
        code.AppendLine("    // 'remove' const lives on a oneOf branch NSwag cannot surface, so we synthesize an unnamed value");
        code.AppendLine("    // at ordinal 2. The const declaration is required so the value can be used in switch patterns;");
        code.AppendLine("    // the static constructor below verifies at type-init time that no named member collides with");
        code.AppendLine("    // ordinal 2, failing fast on Contract Spine or NSwag generator drift.");
        code.AppendLine("    private const FileMutationRequestFileOperationKind RemoveFileOperationKind = (FileMutationRequestFileOperationKind)2;");
        code.AppendLine();
        code.AppendLine("    static FileMutationRequest()");
        code.AppendLine("    {");
        code.AppendLine("        foreach (FileMutationRequestFileOperationKind member in Enum.GetValues<FileMutationRequestFileOperationKind>())");
        code.AppendLine("        {");
        code.AppendLine("            if ((int)member == (int)RemoveFileOperationKind && Enum.GetName(member) is { } memberName && !string.Equals(memberName, \"Remove\", StringComparison.Ordinal))");
        code.AppendLine("            {");
        code.AppendLine("                throw new InvalidOperationException(\"FileMutationRequestFileOperationKind member '\" + memberName + \"' collides with the synthesized Remove ordinal; Contract Spine or NSwag generator drift.\");");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine("    }");
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

// YAML loading helpers (LoadYaml, RequiredMapping, RequiredScalar, ReadStringSequence) are
// supplied by the shared Hexalith.Folders.Client.Generation.Shared library via `using static`.

static string ToJsonPropertyName(string snake) => ToParameterName(snake);

// ToParameterName, ToPropertyName, and NormalizeName live in YamlContractLoader (shared library)
// so the generator and the focused tests consume one canonical implementation. Round 4 review (P8).

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

        if (values[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Argument {values[i]} at position {i} is missing a value (next token '{values[i + 1]}' looks like a flag).");
        }

        parsed[values[i]] = values[i + 1];
    }

    return parsed;
}

static string RequiredArgument(IReadOnlyDictionary<string, string> values, string key) =>
    values.TryGetValue(key, out string? value) ? value : throw new ArgumentException($"Missing argument {key}.");

static string NormalizeText(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

static string NormalizeGeneratedHelperHash(string value) =>
    Regex.Replace(NormalizeText(value), "^(?<indent>[ \\t]*)public const string GeneratedHelpersSha256 = \"(?:[0-9a-fA-F]{64}|__GENERATED_HELPERS_SHA256__)\";[ \\t]*$", "${indent}public const string GeneratedHelpersSha256 = \"__GENERATED_HELPERS_SHA256__\";", RegexOptions.CultureInvariant | RegexOptions.Multiline);

static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

// Canonical helper-signature representation: one record per helper, fields delimited by `;`, lines
// delimited by `\n`. The hash digests this representation and returns the first 16 hex characters
// (64 bits) as a compact version label. Any change to schema name, parameter name list, parameter
// order, variant operation IDs, or declared field list per variant changes the digest; spine edits
// that leave the helper shapes alone leave the digest unchanged.
static string ComputeHelperSchemaVersion(IReadOnlyList<HelperModel> helpers)
{
    // Use ASCII unit separator (U+001F) between tokens so any spine-derived name
    // (parameters, fields, operation IDs) cannot collide with the separator. Helpers
    // are sorted by SchemaName so the constant is invariant to BuildHelpers iteration
    // order across .NET runtimes. Round 4 (external) P4.
    const char tokenSeparator = '';
    StringBuilder shape = new();
    foreach (HelperModel helper in helpers.OrderBy(h => h.SchemaName, StringComparer.Ordinal))
    {
        shape.Append("schema=").Append(helper.SchemaName).Append(";params=[");
        shape.AppendJoin(tokenSeparator, helper.Parameters.Select(p => p.Name));
        shape.Append("];variants=[");
        IEnumerable<string> variantTokens = helper.Variants
            .OrderBy(v => v.OperationId, StringComparer.Ordinal)
            .Select(v => v.OperationId + "(" + string.Join(tokenSeparator, v.DeclaredFields) + ")");
        shape.AppendJoin('|', variantTokens);
        shape.Append("]\n");
    }

    return Sha256(shape.ToString())[..16];
}

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

// YamlNodeExtensions (ShouldBeMapping / ShouldBeSequence / ShouldBeScalar) live in the shared
// library Hexalith.Folders.Client.Generation.Shared so both the generator and the client tests
// share a single canonical set of YAML-shape diagnostics.

// Special-case field registry for schemas whose canonical hash references projected or composed
// properties (e.g., fields declared via x-hexalith-idempotency-equivalence that traverse a child
// schema). Per-entry comments name the spine operationId that declares the equivalence; the actual
// line numbers vary as the spine grows, so grep `x-hexalith-idempotency-equivalence:` under the
// named operationId to locate the source.
internal static class SpecialFields
{
    public static readonly Dictionary<(string SchemaName, string Field), FieldModel> Registry = new()
    {
        // FileMutationRequest is the union schema for AddFile / ChangeFile / RemoveFile operations.
        // Each entry below appears under one or more of those operationId equivalence lists in
        // src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml.
        //
        // Entry: content_hash_reference (AddFile, ChangeFile equivalence; absent for RemoveFile).
        [("FileMutationRequest", "content_hash_reference")] = new FieldModel("content_hash_reference", "ContentHashReference is not null", "ContentHashReference"),
        // Entry: file_operation_kind (AddFile, ChangeFile, RemoveFile equivalence; typed enum, always present).
        [("FileMutationRequest", "file_operation_kind")] = new FieldModel("file_operation_kind", "true", "ResolveFileMutationOperationKindWireValue()"),
        // Entry: operation_id (NSwag-emitted companion property; preserves null-versus-omitted presence semantics for the shared FileMutationRequest schema).
        [("FileMutationRequest", "operation_id")] = new FieldModel("operation_id", "OperationId is not null", "OperationId"),
        // Entry: path_metadata (AddFile, ChangeFile, RemoveFile equivalence; typed PathMetadata object).
        [("FileMutationRequest", "path_metadata")] = new FieldModel("path_metadata", "PathMetadata is not null", "PathMetadata"),
        // Entry: path_policy_class (derives from PathMetadata.PathPolicyClass; declared spine-side as a top-level equivalence entry on all three file-mutation operations).
        [("FileMutationRequest", "path_policy_class")] = new FieldModel("path_policy_class", "PathMetadata is not null && PathMetadata.PathPolicyClass is not null", "PathMetadata?.PathPolicyClass"),
    };
}
