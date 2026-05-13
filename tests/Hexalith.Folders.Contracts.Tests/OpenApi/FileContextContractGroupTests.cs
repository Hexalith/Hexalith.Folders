using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class FileContextContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ContractNotesPath = Path.Combine(RepositoryRoot, "docs", "contract", "file-context-contract-groups.md");

    private static readonly string[] FileContextOperationIds =
    [
        "AddFile",
        "ChangeFile",
        "RemoveFile",
        "ListFolderFiles",
        "GetFolderFileMetadata",
        "SearchFolderFiles",
        "GlobFolderFiles",
        "ReadFileRange",
    ];

    private static readonly string[] MutatingOperationIds =
    [
        "AddFile",
        "ChangeFile",
        "RemoveFile",
    ];

    [Fact]
    public void FileContextOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        operations.Select(o => o.OperationId).Order(StringComparer.Ordinal).ShouldBe(FileContextOperationIds.Order(StringComparer.Ordinal));
        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.Select(o => o.Path).All(p => p.StartsWith("/api/v1/folders/{folderId}/workspaces/{workspaceId}/", StringComparison.Ordinal)).ShouldBeTrue();

        string[] forbiddenPathFragments =
        [
            "/commits",
            "/audit",
            "/ops-console",
            "/provider-bindings",
            "/repository-bindings",
        ];

        foreach (Operation operation in operations)
        {
            foreach (string forbidden in forbiddenPathFragments)
            {
                operation.Path.ShouldNotContain(forbidden, Case.Sensitive, $"{operation.OperationId} belongs to a downstream story.");
            }
        }

        ResolveRefs(root);
    }

    [Fact]
    public void FileMutations_DeclareIdempotencyLockScopeAndD9Transport()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations.Where(o => MutatingOperationIds.Contains(o.OperationId, StringComparer.Ordinal)))
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeTrue(operation.OperationId);
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/FolderId").ShouldBeTrue(operation.OperationId);
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/WorkspaceId").ShouldBeTrue(operation.OperationId);
            parameters.Any(p =>
                GetOptionalScalar(p, "$ref") == "#/components/parameters/TaskId"
                || (GetOptionalScalar(p, "name") == "X-Hexalith-Task-Id" && GetOptionalScalar(p, "required") == "true")).ShouldBeTrue(operation.OperationId);

            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-ttl-tier")).ShouldBeTrue(operation.OperationId);

            string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray(), $"{operation.OperationId} equivalence fields must be lexicographically ordered.");
            equivalence.ShouldContain("operation_id", operation.OperationId);
            equivalence.ShouldContain("path_metadata", operation.OperationId);
            equivalence.ShouldContain("path_policy_class", operation.OperationId);
            equivalence.ShouldContain("task_id", operation.OperationId);
            equivalence.ShouldContain("workspace_id", operation.OperationId);
            equivalence.ShouldNotContain("tenant_id", "tenant authority is envelope-derived and must not be client-controlled OpenAPI equivalence.");

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("held workspace lock", Case.Insensitive, operation.OperationId);
            if (operation.OperationId is "AddFile" or "ChangeFile")
            {
                serializedOperation.ShouldContain("PutFileInline", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("PutFileStream", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("262144", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("X-Hexalith-Retry-As", Case.Sensitive, operation.OperationId);
            }

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "workspace_locked", "lock_expired", "lock_not_owned", "authorization_revocation_detected", "path_validation_failed", "file_operation_failed", "state_transition_invalid", "unknown_provider_outcome", "reconciliation_required", "idempotency_conflict" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }
        }
    }

    [Fact]
    public void ContextQueries_OmitIdempotencyAndRequireAuthorizationBeforeObservation()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations.Where(o => !MutatingOperationIds.Contains(o.OperationId, StringComparer.Ordinal)))
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("tenant access, folder ACL, path policy, sensitivity classification, C4 bounds, then query execution", Case.Sensitive, operation.OperationId);
            serializedOperation.ShouldContain("snapshot_per_task", Case.Sensitive, operation.OperationId);
            serializedOperation.ShouldContain("safe-denial", Case.Insensitive, operation.OperationId);

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "tenant_access_denied", "folder_acl_denied", "path_validation_failed", "input_limit_exceeded", "response_limit_exceeded", "query_timeout", "read_model_unavailable", "redacted" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }
        }
    }

    [Fact]
    public void FileContextSchemas_DeclareC4BoundsMetadataOnlyExamplesAndRangeSemantics()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        RequiredMapping(schemas, "PathMetadata");
        RequiredMapping(schemas, "FileMutationRequest");
        RequiredMapping(schemas, "FileMutationAccepted");
        RequiredMapping(schemas, "FileTreeResult");
        RequiredMapping(schemas, "FileMetadataResult");
        RequiredMapping(schemas, "FileSearchRequest");
        RequiredMapping(schemas, "FileGlobRequest");
        RequiredMapping(schemas, "FileRangeReadRequest");
        RequiredMapping(schemas, "FileRangeReadResult");
        RequiredMapping(schemas, "ContextQueryLimitMetadata");

        string serialized = File.ReadAllText(OpenApiPath);
        serialized.ShouldContain("maxItems: 100", Case.Sensitive);
        serialized.ShouldContain("maxResultCount: 2000", Case.Sensitive);
        serialized.ShouldContain("maxResultCount: 500", Case.Sensitive);
        serialized.ShouldContain("maxBytes: 262144", Case.Sensitive);
        serialized.ShouldContain("x-hexalith-response-budget-bytes: 1048576", Case.Sensitive);
        serialized.ShouldContain("x-hexalith-query-timeout-ms: 2000", Case.Sensitive);
        serialized.ShouldContain("TODO(reference-pending): docs/exit-criteria/c4-input-limits.md PM approval state is proposed", Case.Sensitive);

        string[] requiredExamples =
        [
            "AddFileInlineRequest",
            "ChangeFileStreamRequest",
            "RemoveFileRequest",
            "FileTreeResult",
            "FileMetadataResult",
            "SearchFolderFilesRequest",
            "GlobFolderFilesRequest",
            "ReadFileRangeMinimumRequest",
            "ReadFileRangeMaximumRequest",
            "ReadFileRangeInvalidReversedProblem",
            "ReadFileRangeOverBoundProblem",
            "ReadFileRangeRedactedProblem",
        ];

        foreach (string exampleName in requiredExamples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string combinedExamples = string.Join("\n", requiredExamples.Select(name => SerializeYaml(RequiredMapping(examples, name))));
        foreach (string forbidden in new[] { "diff --git", "BEGIN ", "github.com/", "C:\\", "D:\\", "/home/", "matchedLine", "snippet", "rawSearchText", "generatedContext", "providerPayload" })
        {
            combinedExamples.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void FileContextContractNotes_RecordDeferredOwnersAndNegativeScope()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string notes = File.ReadAllText(ContractNotesPath);

        notes.ShouldContain("Story 1.10", Case.Sensitive);
        notes.ShouldContain("Story 1.11", Case.Sensitive);
        notes.ShouldContain("Epic 4", Case.Sensitive);
        notes.ShouldContain("Epic 5", Case.Sensitive);
        notes.ShouldContain("Story 6.6", Case.Sensitive);
        notes.ShouldContain("semantic indexing", Case.Insensitive);
        notes.ShouldContain("deferred", Case.Insensitive);
        notes.ShouldContain("contract-only", Case.Insensitive);

        string[] forbiddenRoots =
        [
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "FileEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "ContextQueryEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Cli", "Commands"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Mcp", "Tools"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Workers", "Providers"),
        ];

        foreach (string forbiddenRoot in forbiddenRoots)
        {
            File.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
            Directory.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
        }
    }

    private sealed record Operation(string Path, string Method, string OperationId, YamlMappingNode Node, YamlMappingNode PathItem);

    private static IEnumerable<Operation> EnumerateOperations(YamlMappingNode root)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            string path = pathEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = methodEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                if (method is "get" or "put" or "post" or "patch" or "delete")
                {
                    YamlMappingNode operation = methodEntry.Value.ShouldBeOfType<YamlMappingNode>();
                    yield return new Operation(path, method, GetScalar(operation, "operationId"), operation, pathItem);
                }
            }
        }
    }

    private static IEnumerable<YamlMappingNode> EnumerateParameters(YamlMappingNode pathItem, YamlMappingNode operation)
    {
        if (pathItem.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? pathParametersNode))
        {
            foreach (YamlNode parameter in pathParametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }

        if (operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
        {
            foreach (YamlNode parameter in parametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }
    }

    private static void ResolveRefs(YamlMappingNode root)
    {
        foreach (string reference in EnumerateRefs(root))
        {
            reference.StartsWith("#/", StringComparison.Ordinal).ShouldBeTrue(reference);
            ResolvePointer(root, reference[2..].Split('/')).ShouldNotBeNull(reference);
        }
    }

    private static IEnumerable<string> EnumerateRefs(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
            {
                if (child.Key is YamlScalarNode { Value: "$ref" } && child.Value is YamlScalarNode reference)
                {
                    yield return reference.Value ?? string.Empty;
                }

                foreach (string nestedReference in EnumerateRefs(child.Value))
                {
                    yield return nestedReference;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (YamlNode child in sequence.Children)
            {
                foreach (string reference in EnumerateRefs(child))
                {
                    yield return reference;
                }
            }
        }
    }

    private static YamlNode? ResolvePointer(YamlNode root, IReadOnlyList<string> segments)
    {
        YamlNode current = root;

        foreach (string segment in segments)
        {
            string key = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current is not YamlMappingNode mapping || !mapping.Children.TryGetValue(new YamlScalarNode(key), out current!))
            {
                return null;
            }
        }

        return current;
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        path.ShouldSatisfyAllConditions(() => File.Exists(path).ShouldBeTrue(path));

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

    private static string GetScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
    }

    private static string? GetOptionalScalar(YamlMappingNode mapping, string key)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static string SerializeYaml(YamlNode node)
    {
        YamlStream stream = new(new YamlDocument(node));
        using StringWriter writer = new();
        stream.Save(writer, false);

        return writer.ToString();
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

            DirectoryInfo? parent = Directory.GetParent(current);
            current = parent?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
