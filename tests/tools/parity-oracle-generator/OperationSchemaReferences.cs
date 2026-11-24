using System.Globalization;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

/// <summary>Checks local OpenAPI document pointers reachable from an operation's schemas.</summary>
internal static class OperationSchemaReferences
{
    private static readonly string[] SchemaMaps = ["properties", "patternProperties", "dependentSchemas", "$defs"];
    private static readonly string[] SchemaLists = ["allOf", "anyOf", "oneOf", "prefixItems"];
    private static readonly string[] SingleSchemas =
    [
        "items", "additionalProperties", "unevaluatedProperties", "unevaluatedItems", "contains",
        "propertyNames", "not", "if", "then", "else", "contentSchema",
    ];

    /// <summary>Resolves local request/response schema references without changing the emitted parity row.</summary>
    internal static void Validate(YamlMappingNode root, YamlMappingNode path, YamlMappingNode operation, string operationId)
    {
        HashSet<YamlNode> seenSchemas = new(ReferenceEqualityComparer.Instance);
        VisitParameters(path);
        VisitParameters(operation);

        if (Get(operation, "requestBody") is { } requestBody)
        {
            YamlMappingNode? container = ResolveContainer(requestBody, "request body");
            if (requestBody is YamlMappingNode requestMapping && Get(requestMapping, "$ref") is not null
                && (container is null || Get(container, "content") is not YamlMappingNode))
            {
                throw Unresolved("request body");
            }

            VisitContainer(container, "request body");
        }

        if (Get(operation, "responses") is YamlMappingNode responses)
        {
            foreach (YamlNode response in responses.Children.Values)
            {
                YamlMappingNode? container = ResolveContainer(response, "response");
                VisitContainer(container, "response");
                if (container is not null && Get(container, "headers") is YamlMappingNode headers)
                {
                    foreach (YamlNode header in headers.Children.Values)
                    {
                        VisitContainer(ResolveContainer(header, "response header"), "response header");
                    }
                }
            }
        }

        void VisitParameters(YamlMappingNode parent)
        {
            if (Get(parent, "parameters") is YamlSequenceNode parameters)
            {
                foreach (YamlNode parameter in parameters.Children)
                {
                    VisitContainer(ResolveContainer(parameter, "request parameter"), "request parameter");
                }
            }
        }

        YamlMappingNode? ResolveContainer(YamlNode node, string location)
        {
            HashSet<YamlNode> seenContainers = new(ReferenceEqualityComparer.Instance);
            while (node is YamlMappingNode mapping && Get(mapping, "$ref") is { } reference)
            {
                if (!seenContainers.Add(node))
                {
                    throw Unresolved(location);
                }

                node = Resolve(root, reference) as YamlMappingNode ?? throw Unresolved(location);
            }

            return node as YamlMappingNode;
        }

        void VisitContainer(YamlMappingNode? container, string location)
        {
            if (container is null)
            {
                return;
            }

            VisitSchema(Get(container, "schema"), location);
            if (Get(container, "content") is YamlMappingNode content)
            {
                foreach (YamlMappingNode mediaType in content.Children.Values.OfType<YamlMappingNode>())
                {
                    VisitSchema(Get(mediaType, "schema"), location);
                }
            }
        }

        void VisitSchema(YamlNode? node, string location)
        {
            if (node is not YamlMappingNode schema || !seenSchemas.Add(schema))
            {
                return;
            }

            // Resource identifiers and dynamic anchors would change the reference resolution scope.
            if (Get(schema, "$id") is not null || Get(schema, "$dynamicRef") is not null)
            {
                throw new InvalidOperationException(
                    $"prerequisite-drift: operation {operationId} uses unsupported schema reference scope in its {location}.");
            }

            if (Get(schema, "$ref") is { } reference)
            {
                YamlNode? target = Resolve(root, reference);
                if (target is not YamlMappingNode && !IsBooleanSchema(target))
                {
                    throw Unresolved(location);
                }

                VisitSchema(target, location);
            }

            foreach (string key in SchemaMaps)
            {
                if (Get(schema, key) is YamlMappingNode children)
                {
                    foreach (YamlNode child in children.Children.Values)
                    {
                        VisitSchema(child, location);
                    }
                }
            }

            foreach (string key in SchemaLists)
            {
                if (Get(schema, key) is YamlSequenceNode children)
                {
                    foreach (YamlNode child in children.Children)
                    {
                        VisitSchema(child, location);
                    }
                }
            }

            foreach (string key in SingleSchemas)
            {
                VisitSchema(Get(schema, key), location);
            }
        }

        InvalidOperationException Unresolved(string location)
            => new($"prerequisite-drift: operation {operationId} has an unresolved OpenAPI {location} schema reference.");
    }

    private static YamlNode? Resolve(YamlMappingNode root, YamlNode reference)
    {
        if (reference is not YamlScalarNode { Value: { } pointer }
            || !pointer.StartsWith('#'))
        {
            return null;
        }

        for (int index = pointer.IndexOf('%'); index >= 0; index = pointer.IndexOf('%', index + 3))
        {
            if (index + 2 >= pointer.Length || !Uri.IsHexDigit(pointer[index + 1]) || !Uri.IsHexDigit(pointer[index + 2]))
            {
                return null;
            }
        }

        string fragment = Uri.UnescapeDataString(pointer[1..]);
        if (!fragment.StartsWith('/'))
        {
            return null;
        }

        YamlNode? node = root;
        foreach (string token in fragment[1..].Split('/'))
        {
            for (int index = token.IndexOf('~'); index >= 0; index = token.IndexOf('~', index + 1))
            {
                if (index + 1 == token.Length || token[index + 1] is not ('0' or '1'))
                {
                    return null;
                }
            }

            string part = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node switch
            {
                YamlMappingNode mapping => Get(mapping, part),
                YamlSequenceNode sequence when (part.Length == 1 || !part.StartsWith('0'))
                    && int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    && index < sequence.Children.Count => sequence.Children[index],
                _ => null,
            };
        }

        return node;
    }

    private static bool IsBooleanSchema(YamlNode? node)
        => node is YamlScalarNode { Value: "true" or "false" or "True" or "False" or "TRUE" or "FALSE" } scalar
            && (scalar.Tag == new TagName("tag:yaml.org,2002:bool")
                || (scalar.Tag.IsEmpty && scalar.Style == ScalarStyle.Plain));

    private static YamlNode? Get(YamlMappingNode mapping, string key)
        => mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
            || mapping.Children.TryGetValue(new YamlScalarNode(key) { Tag = new TagName("tag:yaml.org,2002:str") }, out value)
                ? value
                : null;
}
