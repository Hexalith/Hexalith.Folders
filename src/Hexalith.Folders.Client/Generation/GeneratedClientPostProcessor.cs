using System.Text;
using System.Text.RegularExpressions;

using Hexalith.Folders.Client.Generation.Shared;

using YamlDotNet.RepresentationModel;

using static Hexalith.Folders.Client.Generation.Shared.YamlContractLoader;

namespace Hexalith.Folders.Client.Generation;

/// <summary>
/// Applies the deterministic SDK-shape corrections that NSwag cannot express from OpenAPI alone.
/// </summary>
internal static class GeneratedClientPostProcessor
{
    /// <summary>
    /// Makes both declared successful range responses flow through the common generated result abstraction.
    /// </summary>
    /// <param name="clientPath">The generated NSwag client source path.</param>
    public static void Process(string clientPath, string contractPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractPath);

        string source = File.ReadAllText(clientPath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        const string concreteRangeReturn = "System.Threading.Tasks.Task<FileRangeReadCompleteResult> ReadFileRangeAsync";
        const string commonRangeReturn = "System.Threading.Tasks.Task<FileRangeReadResult> ReadFileRangeAsync";
        int concreteRangeCount = CountOccurrences(source, concreteRangeReturn);
        if (concreteRangeCount == 4)
        {
            source = source.Replace(concreteRangeReturn, commonRangeReturn, StringComparison.Ordinal);
        }
        else if (concreteRangeCount != 0 || CountOccurrences(source, commonRangeReturn) != 4)
        {
            throw new InvalidOperationException("Generated-client range return shape was neither raw nor already post-processed.");
        }

        const string exceptionalPartial = "                            throw new HexalithFoldersApiException<FileRangeReadPartialResult>(\"Partial authorized byte range when end-of-file is reached before the requested exclusive end offset.\", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);";
        const string successfulPartial = "                            return objectResponse_.Object;";
        int exceptionalPartialCount = CountOccurrences(source, exceptionalPartial);
        if (exceptionalPartialCount == 1)
        {
            source = source.Replace(exceptionalPartial, successfulPartial, StringComparison.Ordinal);
        }
        else if (exceptionalPartialCount != 0)
        {
            throw new InvalidOperationException("Generated-client partial-range branch occurred an unexpected number of times.");
        }

        AssertPartialRangeIsSuccessful(source);

        string[] strictWireTypes =
        [
            "PathMetadata",
            "VisiblePathMetadata",
            "ContentAllowedPathMetadata",
            "FileMutationRequest",
            "AddFileRequest",
            "ChangeFileRequest",
            "RemoveFileRequest",
            "FileRangeReadCompleteResult",
            "FileRangeReadPartialResult",
            "FileSearchResult",
            "FileSafeResourceUnavailableProblem",
            "FileRangeUnsatisfiableProblem",
            "FilePolicyUnavailableProblem",
            "FileContentEvidenceInvalidProblem",
            "FileContentEvidenceInvalidOrValidationProblem",
            "FileInlineTransportRequiredProblem",
            "FileContentLimitExceededProblem",
            "FileContentLimitExceededOrWorkspaceTransitionProblem",
            "FileMutationUnavailableProblem",
            "FileContextUnavailableProblem",
        ];
        foreach (string typeName in strictWireTypes)
        {
            string declaration = $"    public partial class {typeName}";
            string decorated = $"    [Newtonsoft.Json.JsonConverter(typeof(Hexalith.Folders.Client.Serialization.Oq2WireObjectConverter))]\n{declaration}";
            if (!source.Contains(decorated, StringComparison.Ordinal))
            {
                source = ReplaceExactly(source, declaration, decorated, expectedCount: 1);
            }
        }

        source = RelaxGeneratedRequiredFields(source);
        source = RequireClosedProblemFields(source);
        source = RequireClosedSuccessFields(source, contractPath);
        source = RestoreOperationUnavailableEnumUnions(source, contractPath);

        WriteAtomically(clientPath, source);
    }

    private static string RelaxGeneratedRequiredFields(string source)
        => source.Replace(
            "Required = Newtonsoft.Json.Required.Always)]",
            "Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]",
            StringComparison.Ordinal);

    private static string RestoreOperationUnavailableEnumUnions(
        string source,
        string contractPath)
    {
        // NSwag 14 selects the first exact oneOf branch when it emits the public DTO even though the
        // wrapper's declared properties contain the scalar union. Restore every declared string value
        // deterministically, but only on the operation wrappers derived from the candidate contract.
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(LoadYaml(contractPath), "components"), "schemas");
        Dictionary<string, YamlMappingNode> wrappers = schemas.Children
            .Select(static entry => new
            {
                Name = entry.Key.ShouldBeScalar("schema name").Value ?? string.Empty,
                Schema = entry.Value.ShouldBeMapping("schema"),
            })
            .Where(static entry => entry.Name.EndsWith("UnavailableProblem", StringComparison.Ordinal)
                && entry.Schema.Children.ContainsKey(new YamlScalarNode("oneOf"))
                && entry.Schema.Children.ContainsKey(new YamlScalarNode("properties")))
            .ToDictionary(static entry => entry.Name, static entry => entry.Schema, StringComparer.Ordinal);
        if (wrappers.Count != 49)
        {
            throw new InvalidOperationException($"Expected 49 generated operation-unavailable wrappers, found {wrappers.Count}.");
        }

        foreach ((string typeName, YamlMappingNode wrapper) in wrappers.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            string declaration = $"    public partial class {typeName}";
            string decorated = $"    [Newtonsoft.Json.JsonConverter(typeof(Hexalith.Folders.Client.Serialization.Oq2WireObjectConverter))]\n{declaration}";
            if (!source.Contains(decorated, StringComparison.Ordinal))
            {
                source = ReplaceExactly(source, declaration, decorated, expectedCount: 1);
            }

            string block = ClassBlock(source, typeName);
            YamlMappingNode properties = RequiredMapping(wrapper, "properties");
            foreach (string propertyName in new[] { "Type", "Title", "Category", "Code", "Message", "ClientAction" })
            {
                string wireName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
                foreach (string wireValue in EnumValues(RequiredMapping(properties, wireName), $"{typeName}.{wireName}"))
                {
                    source = AddEnumValue(source, PropertyType(block, propertyName), wireValue, EnumMemberName(wireValue));
                }
            }

            string detailsType = PropertyType(block, "Details");
            YamlMappingNode detailProperties = RequiredMapping(RequiredMapping(properties, "details"), "properties");
            foreach (string visibility in EnumValues(RequiredMapping(detailProperties, "visibility"), $"{typeName}.details.visibility"))
            {
                source = AddEnumValue(
                    source,
                    PropertyType(ClassBlock(source, detailsType), "Visibility"),
                    visibility,
                    EnumMemberName(visibility));
            }
        }

        return source;
    }

    private static IEnumerable<string> EnumValues(YamlMappingNode schema, string location)
    {
        if (!schema.Children.TryGetValue(new YamlScalarNode("enum"), out YamlNode? valuesNode))
        {
            throw new InvalidOperationException($"Generated-client union property '{location}' has no enum.");
        }

        return valuesNode.ShouldBeSequence($"{location}.enum").Children
            .Select(value => value.ShouldBeScalar(location).Value
                ?? throw new InvalidOperationException($"Generated-client union value '{location}' must not be null."));
    }

    private static string EnumMemberName(string wireValue)
    {
        string member = Regex.Replace(wireValue, @"[^A-Za-z0-9_]", "_", RegexOptions.CultureInvariant);
        if (member.Length == 0)
        {
            return "Value";
        }

        if (char.IsDigit(member[0]))
        {
            member = "_" + member;
        }

        return char.ToUpperInvariant(member[0]) + member[1..];
    }

    private static string PropertyType(string classBlock, string propertyName)
    {
        Match match = Regex.Match(
            classBlock,
            $@"public (?<type>[A-Za-z0-9_]+) {Regex.Escape(propertyName)} \{{ get; set; \}}",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Generated-client property '{propertyName}' was not found for unavailable-union restoration.");
        }

        return match.Groups["type"].Value;
    }

    private static string AddEnumValue(string source, string enumType, string wireValue, string memberName)
    {
        string declaration = $"    public enum {enumType}";
        int start = source.IndexOf(declaration, StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException($"Generated-client enum '{enumType}' was not found.");
        }

        string block = source[start..end];
        string attribute = $"[System.Runtime.Serialization.EnumMember(Value = @\"{wireValue}\")]";
        if (block.Contains(attribute, StringComparison.Ordinal))
        {
            return source;
        }

        int nextValue = Regex.Matches(block, @"= (?<value>[0-9]+),", RegexOptions.CultureInvariant)
            .Select(static match => int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(-1)
            .Max() + 1;
        string member = $"\n        {attribute}\n        {memberName} = {nextValue},\n";
        return source.Insert(end, member);
    }

    private static string RequireClosedProblemFields(string source)
    {
        string[] requiredProblemProperties =
        [
            "type", "title", "status", "category", "code", "message", "correlationId",
            "retryable", "clientAction", "details",
        ];
        string[] problemTypeNames = Regex.Matches(
                source,
                @"^    public partial class (?<name>[A-Za-z0-9_]*Problem(?:Details)?)\b",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> detailsTypeNames = new(StringComparer.Ordinal);
        foreach (string typeName in problemTypeNames)
        {
            string block = ClassBlock(source, typeName);
            Match detailsType = Regex.Match(
                block,
                @"public (?<name>Details[0-9]*) Details \{ get; set; \}",
                RegexOptions.CultureInvariant);
            if (detailsType.Success)
            {
                detailsTypeNames.Add(detailsType.Groups["name"].Value);
            }

            source = RequireJsonProperties(source, typeName, requiredProblemProperties);
            source = RemoveJsonExtensionData(source, typeName);
        }

        foreach (string detailsTypeName in detailsTypeNames)
        {
            source = RequireJsonProperties(source, detailsTypeName, ["visibility"]);
            source = RemoveJsonExtensionData(source, detailsTypeName);
        }

        return source;
    }

    private static string RequireClosedSuccessFields(string source, string contractPath)
    {
        YamlMappingNode root = LoadYaml(contractPath);
        YamlMappingNode components = RequiredMapping(root, "components");
        YamlMappingNode schemas = RequiredMapping(components, "schemas");
        HashSet<string> successSchemas = CollectSuccessSchemas(root, components, schemas);
        foreach ((YamlNode nameNode, YamlNode schemaNode) in schemas.Children)
        {
            string typeName = nameNode.ShouldBeScalar("schema name").Value ?? string.Empty;
            if (!successSchemas.Contains(typeName)
                || typeName.Contains("Problem", StringComparison.Ordinal)
                || !source.Contains($"    public partial class {typeName}", StringComparison.Ordinal))
            {
                continue;
            }

            YamlMappingNode schema = schemaNode.ShouldBeMapping($"schema {typeName}");
            HashSet<string> required = ReadStringSequence(schema, "required").ToHashSet(StringComparer.Ordinal);
            bool closed = IsFalse(schema, "additionalProperties") || IsFalse(schema, "unevaluatedProperties");
            if (schema.Children.TryGetValue(new YamlScalarNode("allOf"), out YamlNode? allOfNode))
            {
                closed = true;
                foreach (YamlNode branchNode in allOfNode.ShouldBeSequence($"{typeName}.allOf"))
                {
                    if (branchNode is not YamlMappingNode branch)
                    {
                        continue;
                    }

                    required.UnionWith(ReadStringSequence(branch, "required"));
                    closed = closed || IsFalse(branch, "additionalProperties") || IsFalse(branch, "unevaluatedProperties");
                }
            }

            if (required.Count > 0)
            {
                source = RequireJsonProperties(source, typeName, required.Order(StringComparer.Ordinal).ToArray());
            }

            if (closed)
            {
                source = RemoveJsonExtensionData(source, typeName);
            }
        }

        return source;
    }

    private static HashSet<string> CollectSuccessSchemas(
        YamlMappingNode root,
        YamlMappingNode components,
        YamlMappingNode schemas)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (YamlNode pathItemNode in RequiredMapping(root, "paths").Children.Values)
        {
            foreach ((YamlNode methodNode, YamlNode operationNode) in pathItemNode.ShouldBeMapping("path item").Children)
            {
                string method = methodNode.ShouldBeScalar("method").Value ?? string.Empty;
                if (method is not ("get" or "post" or "put" or "patch" or "delete")
                    || operationNode is not YamlMappingNode operation)
                {
                    continue;
                }

                foreach ((YamlNode statusNode, YamlNode responseNode) in RequiredMapping(operation, "responses").Children)
                {
                    string status = statusNode.ShouldBeScalar("response status").Value ?? string.Empty;
                    if (!status.StartsWith('2'))
                    {
                        continue;
                    }

                    YamlMappingNode response = responseNode.ShouldBeMapping("success response");
                    if (response.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? responseReference))
                    {
                        string reference = responseReference.ShouldBeScalar("response $ref").Value ?? string.Empty;
                        const string prefix = "#/components/responses/";
                        response = RequiredMapping(components, "responses").Children[
                            new YamlScalarNode(reference[prefix.Length..])].ShouldBeMapping("referenced response");
                    }

                    CollectSchemaReferences(response, schemas, names);
                }
            }
        }

        return names;
    }

    private static void CollectSchemaReferences(
        YamlNode node,
        YamlMappingNode schemas,
        HashSet<string> names)
    {
        if (node is YamlMappingNode mapping)
        {
            if (mapping.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? referenceNode))
            {
                string reference = referenceNode.ShouldBeScalar("schema $ref").Value ?? string.Empty;
                const string prefix = "#/components/schemas/";
                if (reference.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string name = reference[prefix.Length..];
                    if (names.Add(name)
                        && schemas.Children.TryGetValue(new YamlScalarNode(name), out YamlNode? schema))
                    {
                        CollectSchemaReferences(schema, schemas, names);
                    }
                }
            }

            foreach (YamlNode child in mapping.Children.Values)
            {
                CollectSchemaReferences(child, schemas, names);
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (YamlNode child in sequence.Children)
            {
                CollectSchemaReferences(child, schemas, names);
            }
        }
    }

    private static bool IsFalse(YamlMappingNode mapping, string name)
        => mapping.Children.TryGetValue(new YamlScalarNode(name), out YamlNode? value)
            && value is YamlScalarNode scalar
            && bool.TryParse(scalar.Value, out bool parsed)
            && !parsed;

    private static string RemoveJsonExtensionData(string source, string typeName)
    {
        int start = FindClassDeclaration(source, typeName);
        int end = start < 0 ? -1 : source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException(
                $"Generated-client problem type '{typeName}' was not found for closed-shape enforcement.");
        }

        string block = source[start..end];
        block = Regex.Replace(
            block,
            "\\n        private System\\.Collections\\.Generic\\.IDictionary<string, object> _additionalProperties;\\n\\n"
                + "        \\[Newtonsoft\\.Json\\.JsonExtensionData\\]\\n"
                + "        public System\\.Collections\\.Generic\\.IDictionary<string, object> AdditionalProperties\\n"
                + "        \\{\\n"
                + "            get \\{ return _additionalProperties \\?\\? \\(_additionalProperties = new System\\.Collections\\.Generic\\.Dictionary<string, object>\\(\\)\\); \\}\\n"
                + "            set \\{ _additionalProperties = value; \\}\\n"
                + "        \\}\\n",
            string.Empty,
            RegexOptions.CultureInvariant);
        return source[..start] + block + source[end..];
    }

    private static string RequireJsonProperties(string source, string typeName, IReadOnlyList<string> propertyNames)
    {
        int start = FindClassDeclaration(source, typeName);
        if (start < 0)
        {
            throw new InvalidOperationException($"Generated-client problem type '{typeName}' was not found for required-field enforcement.");
        }

        int end = source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"Generated-client problem type '{typeName}' has no deterministic class terminator.");
        }

        string block = source[start..end];
        foreach (string propertyName in propertyNames)
        {
            string optional = $"[Newtonsoft.Json.JsonProperty(\"{propertyName}\", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]";
            string required = $"[Newtonsoft.Json.JsonProperty(\"{propertyName}\", Required = Newtonsoft.Json.Required.Always)]";
            if (block.Contains(optional, StringComparison.Ordinal))
            {
                block = block.Replace(optional, required, StringComparison.Ordinal);
            }
        }

        return source[..start] + block + source[end..];
    }

    private static string ClassBlock(string source, string typeName)
    {
        int start = FindClassDeclaration(source, typeName);
        int end = start < 0 ? -1 : source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException($"Generated-client class block '{typeName}' was not found.");
        }

        return source[start..end];
    }

    private static int FindClassDeclaration(string source, string typeName)
    {
        string declaration = $"    public partial class {typeName}";
        int offset = 0;
        while ((offset = source.IndexOf(declaration, offset, StringComparison.Ordinal)) >= 0)
        {
            int suffix = offset + declaration.Length;
            if (suffix == source.Length || source[suffix] is '\r' or '\n' or ' ')
            {
                return offset;
            }

            offset = suffix;
        }

        return -1;
    }

    private static void AssertPartialRangeIsSuccessful(string source)
    {
        const string partialReader = "var objectResponse_ = await ReadObjectResponseAsync<FileRangeReadPartialResult>(response_, headers_, cancellationToken).ConfigureAwait(false);";
        int partialReaderOffset = source.IndexOf(partialReader, StringComparison.Ordinal);
        if (partialReaderOffset < 0
            || source.IndexOf(partialReader, partialReaderOffset + partialReader.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException("Generated-client partial-range response reader occurred an unexpected number of times.");
        }

        int nextBranchOffset = source.IndexOf("                        else", partialReaderOffset, StringComparison.Ordinal);
        string partialBranch = source[partialReaderOffset..(nextBranchOffset < 0 ? source.Length : nextBranchOffset)];
        if (CountOccurrences(partialBranch, "return objectResponse_.Object;") != 1
            || partialBranch.Contains("throw new HexalithFoldersApiException<FileRangeReadPartialResult>", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated-client HTTP 206 branch is not a single successful partial-range return.");
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static string ReplaceExactly(string source, string oldValue, string newValue, int expectedCount)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(oldValue, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += oldValue.Length;
        }

        if (count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Generated-client post-processing expected {expectedCount} occurrence(s) but found {count}: {oldValue}");
        }

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
