using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Hexalith.Folders.Contracts;
using Hexalith.McpCli.Abstractions;
using YamlDotNet.Serialization;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

/// <summary>Loads the independent inventory, spine and approved routing declarations.</summary>
internal static class McpCliContractTestSupport
{
    /// <summary>The repository containing the contract spine.</summary>
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The decorated assembly under test.</summary>
    internal static Assembly ContractsAssembly { get; } = typeof(FoldersContractMetadata).Assembly;

    /// <summary>The exact eligible operation set and routing decisions, independent of reflection.</summary>
    internal static IReadOnlyList<OperationDeclaration> Operations { get; } =
    [
        new("C01", typeof(Contracts.Commands.ArchiveFolder), "archive-folder", "ArchiveFolder", "FolderId", false),
        new("C02", typeof(Contracts.Commands.BindRepository), "bind-repository", "BindRepository", "FolderId", false),
        new("C03", typeof(Contracts.Commands.CommitWorkspace), "commit-workspace", "CommitWorkspace", "FolderId", true),
        new("C04", typeof(Contracts.Commands.ConfigureBranchRefPolicy), "configure-branch-ref-policy", "ConfigureBranchRefPolicy", "FolderId", false),
        new("C05", typeof(Contracts.Commands.ConfigureProviderBinding), "configure-provider-binding", "ConfigureProviderBinding", "ProviderBindingRef", false),
        new("C06", typeof(Contracts.Commands.CreateFolder), "create-folder", "CreateFolder", "FolderId", false),
        new("C07", typeof(Contracts.Commands.CreateRepositoryBackedFolder), "create-repository-backed-folder", "CreateRepositoryBackedFolder", "FolderId", false),
        new("C08", typeof(Contracts.Commands.GrantFolderAccess), "grant-folder-access", "UpdateFolderAclEntry", "FolderId", false),
        new("C09", typeof(Contracts.Commands.LockWorkspace), "lock-workspace", "LockWorkspace", "FolderId", true),
        new("C10", typeof(Contracts.Commands.MutateFiles), "mutate-files", "MutateFilesRequest", "FolderId", true),
        new("C11", typeof(Contracts.Commands.PrepareWorkspace), "prepare-workspace", "PrepareWorkspace", "FolderId", true),
        new("C12", typeof(Contracts.Commands.ReleaseWorkspaceLock), "release-workspace-lock", "ReleaseWorkspaceLock", "FolderId", true),
        new("C13", typeof(Contracts.Commands.RevokeFolderAccess), "revoke-folder-access", "UpdateFolderAclEntry", "FolderId", false),
        new("Q01", typeof(Contracts.Queries.GetAuditRecord), "get-audit-record", "GetAuditRecord", "FolderId", false),
        new("Q02", typeof(Contracts.Queries.GetBranchRefPolicy), "get-branch-ref-policy", "GetBranchRefPolicy", "FolderId", false),
        new("Q03", typeof(Contracts.Queries.GetCommitEvidence), "get-commit-evidence", "GetCommitEvidence", "FolderId", false),
        new("Q05", typeof(Contracts.Queries.GetEffectivePermissions), "get-effective-permissions", "GetEffectivePermissions", "FolderId", false),
        new("Q07", typeof(Contracts.Queries.GetFolderFileMetadata), "get-folder-file-metadata", "GetFolderFileMetadata", "FolderId", false),
        new("Q08", typeof(Contracts.Queries.GetFolderIndexingStatus), "get-folder-indexing-status", "GetFolderIndexingStatus", "FolderId", false),
        new("Q09", typeof(Contracts.Queries.GetFolderLifecycleStatus), "get-folder-lifecycle-status", "GetFolderLifecycleStatus", "FolderId", false),
        new("Q11", typeof(Contracts.Queries.GetOperationTimelineEntry), "get-operation-timeline-entry", "GetOperationTimelineEntry", "FolderId", false),
        new("Q13", typeof(Contracts.Queries.GetProviderBinding), "get-provider-binding", "GetProviderBinding", "ProviderBindingRef", false),
        new("Q14", typeof(Contracts.Queries.GetProviderOutcome), "get-provider-outcome", "GetProviderOutcome", "FolderId", false),
        new("Q16", typeof(Contracts.Queries.GetProviderSupportEvidence), "get-provider-support-evidence", "GetProviderSupportEvidence", null, false),
        new("Q18", typeof(Contracts.Queries.GetReconciliationStatus), "get-reconciliation-status", "GetReconciliationStatus", "FolderId", false),
        new("Q19", typeof(Contracts.Queries.GetRepositoryBinding), "get-repository-binding", "GetRepositoryBinding", "FolderId", false),
        new("Q21", typeof(Contracts.Queries.GetTaskStatus), "get-task-status", "GetTaskStatus", "FolderId", false),
        new("Q22", typeof(Contracts.Queries.GetWorkspaceCleanupStatus), "get-workspace-cleanup-status", "GetWorkspaceCleanupStatus", "FolderId", false),
        new("Q23", typeof(Contracts.Queries.GetWorkspaceLock), "get-workspace-lock", "GetWorkspaceLock", "FolderId", false),
        new("Q24", typeof(Contracts.Queries.GetWorkspaceRetryEligibility), "get-workspace-retry-eligibility", "GetWorkspaceRetryEligibility", "FolderId", false),
        new("Q25", typeof(Contracts.Queries.GetWorkspaceStatus), "get-workspace-status", "GetWorkspaceStatus", "FolderId", false),
        new("Q26", typeof(Contracts.Queries.GetWorkspaceTransitionEvidence), "get-workspace-transition-evidence", "GetWorkspaceTransitionEvidence", "FolderId", false),
        new("Q27", typeof(Contracts.Queries.GlobFolderFiles), "glob-folder-files", "GlobFolderFiles", "FolderId", false),
        new("Q28", typeof(Contracts.Queries.ListAuditTrail), "list-audit-trail", "ListAuditTrail", "FolderId", false),
        new("Q29", typeof(Contracts.Queries.ListFolderAclEntries), "list-folder-acl-entries", "ListFolderAclEntries", "FolderId", false),
        new("Q30", typeof(Contracts.Queries.ListFolderFiles), "list-folder-files", "ListFolderFiles", "FolderId", false),
        new("Q31", typeof(Contracts.Queries.ListOperationTimeline), "list-operation-timeline", "ListOperationTimeline", "FolderId", false),
        new("Q32", typeof(Contracts.Queries.ReadFileRange), "read-file-range", "ReadFileRange", "FolderId", false),
        new("Q33", typeof(Contracts.Queries.SearchFolderFiles), "search-folder-files", "SearchFolderFiles", "FolderId", false),
        new("Q34", typeof(Contracts.Queries.SearchFolderIndexedFiles), "search-folder-indexed-files", "SearchFolderIndexedFiles", "FolderId", false),
        new("Q35", typeof(Contracts.Queries.ValidateProviderReadiness), "validate-provider-readiness", "ValidateProviderReadiness", "ProviderBindingRef", false),
    ];

    /// <summary>The diagnostics that retain their inventory defer disposition.</summary>
    internal static IReadOnlySet<string> ExcludedIds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Q04", "Q06", "Q10", "Q12", "Q15", "Q17", "Q20",
    };

    /// <summary>Gets the immutable migration inventory as row ID, canonical name, CLR type and wire.</summary>
    internal static string[][] InventoryRows() => File.ReadLines(Path.Combine(RepositoryRoot,
        "references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md"))
        .Where(line => Regex.IsMatch(line, @"^\| [CQ][0-9]{2} \| `folders\."))
        .Select(line => line.Split('|', StringSplitOptions.TrimEntries)
            .Select(cell => cell.Trim('`')).ToArray())
        .ToArray();

    /// <summary>Gets all operation types, excluding undecorated helper records and enums.</summary>
    internal static Type[] DecoratedTypes() => ContractsAssembly.GetExportedTypes()
        .Where(type => type.IsDefined(typeof(HexalithCommandAttribute)) || type.IsDefined(typeof(HexalithQueryAttribute)))
        .ToArray();

    /// <summary>Loads YAML as a JSON tree so schema assertions preserve scalar kinds.</summary>
    internal static JsonObject LoadSpine()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryRoot,
            "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml"));
        object yaml = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(text);
        string json = new SerializerBuilder().JsonCompatible().Build().Serialize(yaml);
        return JsonNode.Parse(json)!.AsObject();
    }

    /// <summary>Finds a spine operation by its stable operation ID.</summary>
    internal static JsonObject FindOperation(JsonObject spine, string operationId) => spine["paths"]!.AsObject()
        .SelectMany(path => path.Value!.AsObject())
        .Select(method => method.Value).OfType<JsonObject>()
        .Single(operation => operation["operationId"]?.GetValue<string>() == operationId);

    /// <summary>The pinned conditional file branches implemented by downstream admission.</summary>
    internal static JsonNode FileMutationConditions { get; } = JsonNode.Parse("""
        [{"if":{"required":["transportOperation"],"properties":{"transportOperation":{"enum":["PutFileInline"]}}},"then":{"required":["inlineContent","byteLength","contentHashReference"],"properties":{"fileOperationKind":{"enum":["add","change"]},"byteLength":{"maximum":262144}},"not":{"required":["streamDescriptor"]}}},{"if":{"required":["transportOperation"],"properties":{"transportOperation":{"enum":["PutFileStream"]}}},"then":{"required":["streamDescriptor","byteLength","contentHashReference"],"properties":{"fileOperationKind":{"enum":["add","change"]},"byteLength":{"minimum":262145,"maximum":1048576}},"not":{"required":["inlineContent"]}}},{"if":{"required":["transportOperation"],"properties":{"transportOperation":{"enum":["metadataOnlyRemoval"]}}},"then":{"properties":{"fileOperationKind":{"enum":["remove"]}},"not":{"anyOf":[{"required":["inlineContent"]},{"required":["streamDescriptor"]},{"required":["contentHashReference"]},{"required":["byteLength"]}]}}},{"if":{"required":["fileOperationKind"],"properties":{"fileOperationKind":{"enum":["add","change"]}}},"then":{"properties":{"transportOperation":{"enum":["PutFileInline","PutFileStream"]}}}},{"if":{"required":["fileOperationKind"],"properties":{"fileOperationKind":{"enum":["remove"]}}},"then":{"properties":{"transportOperation":{"enum":["metadataOnlyRemoval"]}}}}]
        """)!;

    /// <summary>Creates the default module serializer with the canonical McpCli resolver.</summary>
    /// <param name="modifier">An optional metadata mutation used by drift-gate self-tests.</param>
    internal static JsonSerializerOptions CreateCanonicalPayloadOptions(Action<JsonTypeInfo>? modifier = null)
    {
        DefaultJsonTypeInfoResolver resolver = CreateResolver();
        if (modifier is not null)
        {
            resolver.Modifiers.Add(modifier);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = resolver,
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }

    // This method intentionally mirrors McpCliJson.CreateResolver. The drift tests compare both
    // method bodies against the root-declared McpCli source; Contracts needs no Core dependency.
    private static DefaultJsonTypeInfoResolver CreateResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            for (int index = typeInfo.Properties.Count - 1; index >= 0; index--)
            {
                JsonPropertyInfo property = typeInfo.Properties[index];

                // A getter-only member is still input when deserialization populates it in place.
                bool populated = (property.ObjectCreationHandling ?? typeInfo.PreferredPropertyObjectCreationHandling) == JsonObjectCreationHandling.Populate;
                if (property.Get is not null && property.Set is null && property.AssociatedParameter is null && !property.IsExtensionData && !populated)
                {
                    typeInfo.Properties.RemoveAt(index);
                }
            }
        });
        return resolver;
    }

    /// <summary>Resolves references and supported allOf intersections without losing field constraints.</summary>
    internal static JsonObject Resolve(JsonObject spine, JsonObject node)
    {
        var result = new JsonObject();
        if (node["$ref"] is JsonValue reference)
        {
            string address = reference.GetValue<string>();
            if (!address.StartsWith("#/", StringComparison.Ordinal))
            {
                throw new NotSupportedException("Only local spine references are supported: " + address);
            }

            JsonNode target = spine;
            foreach (string segment in address[2..].Split('/'))
            {
                target = target[segment]!;
            }

            result = Resolve(spine, target.AsObject());
        }

        if (node["allOf"] is JsonArray intersections)
        {
            if (intersections.OfType<JsonObject>().Any(item => item.ContainsKey("if")))
            {
                if (!JsonNode.DeepEquals(intersections, FileMutationConditions))
                {
                    throw new NotSupportedException("Unsupported conditional allOf; reconcile the pinned FileMutationRequest admission rules.");
                }

                result["allOf"] = intersections.DeepClone();
            }
            else
            {
                foreach (JsonNode? intersection in intersections)
                {
                    MergeIntersection(spine, result, Resolve(spine, intersection!.AsObject()), "allOf");
                }
            }
        }

        var siblings = new JsonObject(node.Where(pair => pair.Key is not ("$ref" or "allOf"))
            .Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())));
        MergeIntersection(spine, result, siblings, "$schema");
        return result;
    }

    private static void MergeIntersection(JsonObject spine, JsonObject target, JsonObject incoming, string path)
    {
        foreach ((string key, JsonNode? value) in incoming)
        {
            if (key is "anyOf" or "oneOf" or "not" or "if" or "then" or "else")
            {
                throw new NotSupportedException("Unsupported spine composition at " + path + "." + key);
            }

            if (!target.TryGetPropertyValue(key, out JsonNode? previous))
            {
                target[key] = value?.DeepClone();
            }
            else if (key == "properties")
            {
                foreach ((string member, JsonNode? schema) in value!.AsObject())
                {
                    JsonObject properties = previous!.AsObject();
                    if (properties[member] is JsonObject original)
                    {
                        JsonObject merged = Resolve(spine, original);
                        MergeIntersection(spine, merged, Resolve(spine, schema!.AsObject()), path + "." + member);
                        properties[member] = merged;
                    }
                    else
                    {
                        properties[member] = schema?.DeepClone();
                    }
                }
            }
            else if (key == "required")
            {
                string[] members = previous!.AsArray().Concat(value!.AsArray())
                    .Select(item => item!.GetValue<string>()).Distinct(StringComparer.Ordinal).ToArray();
                target[key] = new JsonArray(members.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray());
            }
            else if (key is "minimum" or "minLength" or "minItems" or "maximum" or "maxLength" or "maxItems")
            {
                decimal oldBound = decimal.Parse(previous!.ToJsonString(), CultureInfo.InvariantCulture);
                decimal newBound = decimal.Parse(value!.ToJsonString(), CultureInfo.InvariantCulture);
                bool lowerBound = key.StartsWith("min", StringComparison.Ordinal);
                target[key] = (lowerBound ? newBound > oldBound : newBound < oldBound) ? value.DeepClone() : previous.DeepClone();
            }
            else if (key == "items")
            {
                JsonObject items = Resolve(spine, previous!.AsObject());
                MergeIntersection(spine, items, Resolve(spine, value!.AsObject()), path + ".items");
                target[key] = items;
            }
            else if (key == "enum")
            {
                JsonNode?[] common = previous!.AsArray().Where(item => value!.AsArray().Any(candidate => JsonNode.DeepEquals(item, candidate)))
                    .Select(item => item?.DeepClone()).ToArray();
                if (common.Length == 0)
                {
                    throw new NotSupportedException("Contradictory enum intersection at " + path);
                }

                target[key] = new JsonArray(common);
            }
            else if (key is "nullable" or "additionalProperties" or "uniqueItems")
            {
                bool left = previous!.GetValue<bool>();
                bool right = value!.GetValue<bool>();
                target[key] = key == "uniqueItems" ? left || right : left && right;
            }
            else if (key is "description" or "title" or "examples" or "default" || key.StartsWith("x-", StringComparison.Ordinal))
            {
                // Annotation text does not weaken an intersected structural constraint.
                if (!JsonNode.DeepEquals(previous, value) && key is not ("description" or "title" or "examples" or "default"))
                {
                    throw new NotSupportedException("Unsupported extension intersection at " + path + "." + key);
                }

                target[key] = value?.DeepClone();
            }
            else if (!JsonNode.DeepEquals(previous, value))
            {
                throw new NotSupportedException("Unsupported intersection at " + path + "." + key);
            }
        }

        if (target["const"] is JsonNode constant && target["enum"] is JsonArray vocabulary)
        {
            if (!vocabulary.Any(value => JsonNode.DeepEquals(value, constant)))
            {
                throw new NotSupportedException("Contradictory const/enum intersection at " + path);
            }

            target["enum"] = new JsonArray(constant.DeepClone());
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Cannot locate the Folders contract repository.");
    }
}
