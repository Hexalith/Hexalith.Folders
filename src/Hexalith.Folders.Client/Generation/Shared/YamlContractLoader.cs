using System.Text;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Client.Generation.Shared;

// Canonical YAML loader and name-normalization helpers for the Hexalith Contract Spine. Used by
// the build-time generator (Generation/Program.cs) and the focused client tests
// (ClientGenerationTests.cs) so both paths consume the same parsing rules, identical failure
// semantics, and the same operation / parameter / request-schema discovery logic. Resolves Round 2
// ResolvedDecision P-shared-1, Round 3 decision D1, and Round 4 findings P15-P18 (guards + scope)
// and P8 (shared name parsing).
public static class YamlContractLoader
{
    public static YamlMappingNode LoadYaml(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        if (yaml.Documents.Count == 0)
        {
            throw new InvalidOperationException($"Contract Spine '{path}' contains no YAML documents.");
        }

        return yaml.Documents[0].RootNode.ShouldBeMapping("root");
    }

    public static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            throw new InvalidOperationException($"Missing mapping '{key}'.");
        }

        return value.ShouldBeMapping(key);
    }

    public static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            throw new InvalidOperationException($"Missing scalar '{key}'.");
        }

        return value.ShouldBeScalar(key).Value ?? string.Empty;
    }

    public static IReadOnlyList<string> ReadStringSequence(YamlMappingNode mapping, string key)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            return [];
        }

        return value.ShouldBeSequence(key).Children.Select(n => n.ShouldBeScalar(key).Value ?? string.Empty).ToArray();
    }

    // Returns the trailing schema name from `#/components/schemas/<Name>` for operations whose
    // request body declares a JSON $ref, or null when the operation declares no requestBody. Throws
    // an InvalidOperationException when the requestBody is present but does not declare a JSON $ref
    // (no inline-schema fallback today) or when the $ref string is empty / ends in a separator —
    // both indicate prerequisite Contract Spine drift rather than soft-fail.
    public static string? ReadRequestSchemaRef(YamlMappingNode operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Children.TryGetValue(new YamlScalarNode("requestBody"), out YamlNode? requestBodyNode))
        {
            return null;
        }

        YamlMappingNode requestBody = requestBodyNode.ShouldBeMapping("requestBody");
        YamlMappingNode content = RequiredMapping(requestBody, "content");
        YamlMappingNode json = RequiredMapping(content, "application/json");
        YamlMappingNode schema = RequiredMapping(json, "schema");
        string reference = RequiredScalar(schema, "$ref");
        // Expected shape: `#/components/schemas/<SchemaName>` where <SchemaName> matches a C# identifier.
        // Round 4 (external) P10: reject `#`, `#/`, and other malformed fragment-only references with a
        // shape-specific diagnostic so operators are not misdirected to a "missing mapping" error.
        if (!reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"requestBody $ref '{reference}' must be of the form '#/components/schemas/<SchemaName>'.");
        }

        string suffix = reference["#/components/schemas/".Length..];
        if (string.IsNullOrEmpty(suffix) || suffix.Contains('/', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"requestBody $ref '{reference}' does not resolve to a single schema name.");
        }

        return suffix;
    }

    // Back-compat alias preserved while in-tree callers migrate. The "Try" prefix is a misnomer —
    // the method throws on prerequisite drift — so new callers should consume ReadRequestSchemaRef.
    public static string? TryReadRequestSchema(YamlMappingNode operation) => ReadRequestSchemaRef(operation);

    // Lowercases the input, collapses `_`/`-` separators, and inserts `_` between camelCase
    // letters so that `folderId`, `folder_id`, and `Folder-Id` all normalize to `folder_id`.
    // Used by the generator and tests as the parameter/field lookup key. Header-named OpenAPI
    // parameters with the `x-hexalith-task-id` prefix collapse to `task_id` for backwards
    // compatibility.
    public static string NormalizeName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
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

        return builder.ToString() switch
        {
            "x_hexalith_task_id" => "task_id",
            "idempotency_key" => "idempotency_key",
            "correlation_id" => "correlation_id",
            string normalized => normalized,
        };
    }

    // Converts a snake_case / kebab-case identifier to lowerCamelCase using a deterministic rule
    // shared between the generator and tests. Single-segment inputs lowercase the first character
    // and preserve the tail; multi-segment inputs concatenate parts with the first letter of each
    // non-leading part uppercased. Empty input yields empty.
    public static string ToParameterName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return value;
        }

        if (!value.Contains('_', StringComparison.Ordinal) && !value.Contains('-', StringComparison.Ordinal))
        {
            return char.ToLowerInvariant(value[0]) + value[1..];
        }

        string[] parts = value.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    // Returns ToParameterName(value) with the first letter uppercased (deterministic PascalCase
    // for property accessors emitted into generated helpers).
    public static string ToPropertyName(string value)
    {
        string camel = ToParameterName(value);
        return camel.Length == 0 ? camel : char.ToUpperInvariant(camel[0]) + camel[1..];
    }

}

internal static class YamlNodeExtensions
{
    public static YamlMappingNode ShouldBeMapping(this YamlNode node, string name) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{name} must be a mapping.");

    public static YamlSequenceNode ShouldBeSequence(this YamlNode node, string name) =>
        node as YamlSequenceNode ?? throw new InvalidOperationException($"{name} must be a sequence.");

    public static YamlScalarNode ShouldBeScalar(this YamlNode node, string name) =>
        node as YamlScalarNode ?? throw new InvalidOperationException($"{name} must be a scalar.");
}
