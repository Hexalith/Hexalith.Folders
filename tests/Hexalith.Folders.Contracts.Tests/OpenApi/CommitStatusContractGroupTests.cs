using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class CommitStatusContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ContractNotesPath = Path.Combine(RepositoryRoot, "docs", "contract", "commit-status-contract-groups.md");

    private static readonly string[] CommitStatusOperationIds =
    [
        "CommitWorkspace",
        "GetWorkspaceStatus",
        "GetTaskStatus",
        "GetCommitEvidence",
        "GetProviderOutcome",
        "GetReconciliationStatus",
    ];

    private static readonly string[] QueryOperationIds =
    [
        "GetWorkspaceStatus",
        "GetTaskStatus",
        "GetCommitEvidence",
        "GetProviderOutcome",
        "GetReconciliationStatus",
    ];

    [Fact]
    public void CommitStatusOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => CommitStatusOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        operations.Select(o => o.OperationId).Order(StringComparer.Ordinal).ShouldBe(CommitStatusOperationIds.Order(StringComparer.Ordinal));
        operations.Select(o => o.OperationId).ShouldBeUnique();

        Dictionary<string, (string Method, string Path)> expected = new(StringComparer.Ordinal)
        {
            ["CommitWorkspace"] = ("post", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits"),
            ["GetWorkspaceStatus"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/status"),
            ["GetTaskStatus"] = ("get", "/api/v1/tasks/{taskId}/status"),
            ["GetCommitEvidence"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence"),
            ["GetProviderOutcome"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome"),
            ["GetReconciliationStatus"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status"),
        };

        foreach (Operation operation in operations)
        {
            (string method, string path) = expected[operation.OperationId];
            operation.Method.ShouldBe(method, operation.OperationId);
            operation.Path.ShouldBe(path, operation.OperationId);
        }

        ResolveRefs(root);
    }

    [Fact]
    public void CommitWorkspace_DeclaresCommitIdempotencyAndMetadataOnlyScope()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation operation = EnumerateOperations(root).Single(o => o.OperationId == "CommitWorkspace");
        YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();

        parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeTrue();
        parameters.Any(p =>
            GetOptionalScalar(p, "$ref") == "#/components/parameters/TaskId"
            || (GetOptionalScalar(p, "name") == "X-Hexalith-Task-Id" && GetOptionalScalar(p, "required") == "true")).ShouldBeTrue();
        parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/WorkspaceId").ShouldBeTrue();
        parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/FolderId").ShouldBeTrue();

        operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue();
        GetScalar(operation.Node, "x-hexalith-idempotency-ttl-tier").ShouldBe("commit");

        string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();

        equivalence.ShouldBe(
        [
            "author_metadata_reference",
            "branch_ref_target",
            "changed_path_metadata_digest",
            "commit_message_classification",
            "operation_id",
            "task_id",
            "workspace_id",
        ]);
        equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray());
        equivalence.ShouldNotContain("tenant_id", "tenant authority is envelope-derived and must not be client-controlled OpenAPI equivalence.");

        string serializedOperation = SerializeYaml(operation.Node);
        serializedOperation.ShouldContain("operationId", Case.Sensitive);
        serializedOperation.ShouldContain("TODO(reference-pending): docs/exit-criteria/c3-retention.md Legal and PM approval for commit tier", Case.Sensitive);
        serializedOperation.ShouldContain("prepared workspace", Case.Insensitive);
        serializedOperation.ShouldContain("staged changes", Case.Insensitive);
        serializedOperation.ShouldContain("task-scoped lock", Case.Insensitive);
        serializedOperation.ShouldContain("metadata-only changed-path evidence", Case.Insensitive);

        string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();

        foreach (string expected in new[] { "commit_failed", "provider_failure_known", "unknown_provider_outcome", "reconciliation_required", "state_transition_invalid", "idempotency_conflict", "dirty_workspace", "workspace_not_ready", "lock_expired", "lock_not_owned" })
        {
            categories.ShouldContain(expected);
        }
    }

    [Fact]
    public void CommitStatusQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => QueryOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations)
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("authorization-before-observation", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("safe-denial", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("freshness", Case.Insensitive, operation.OperationId);

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "tenant_access_denied", "folder_acl_denied", "read_model_unavailable", "not_found", "redacted" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }
        }

        YamlMappingNode workspaceConsistency = RequiredMapping(operations.Single(o => o.OperationId == "GetWorkspaceStatus").Node, "x-hexalith-read-consistency");
        GetScalar(workspaceConsistency, "class").ShouldBe("read_your_writes");
    }

    [Fact]
    public void CommitStatusSchemas_DeclareOutcomeRetryReconciliationAndMetadataOnlyExamples()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        foreach (string schemaName in new[] { "CommitWorkspaceRequest", "CommitWorkspaceAccepted", "WorkspaceStatus", "TaskStatus", "CommitEvidence", "ProviderOutcome", "RetryEligibility", "ReconciliationStatus" })
        {
            RequiredMapping(schemas, schemaName);
        }

        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("committed");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("failed");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("unknown_provider_outcome");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("reconciliation_required");
        RequiredEnumValues(schemas, "ProviderOutcomeState").ShouldBe(["pending", "known_success", "known_failure", "unknown_provider_outcome", "reconciliation_required"]);
        RequiredEnumValues(schemas, "ReconciliationState").ShouldBe(["not_required", "required", "in_progress", "completed_clean", "completed_dirty", "failed"]);

        foreach (string exampleName in new[] { "CommitWorkspaceRequest", "CommitWorkspaceAccepted", "WorkspaceStatusCommitted", "WorkspaceStatusUnknownProviderOutcome", "TaskStatusFailed", "CommitEvidenceRedacted", "ProviderOutcomeKnownFailure", "ProviderOutcomeUnknown", "ReconciliationStatusRequired", "ReconciliationStatusCompletedDirty" })
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string combinedExamples = string.Join("\n", examples.Children
            .Where(entry => entry.Key is YamlScalarNode scalar && (scalar.Value?.Contains("Commit", StringComparison.Ordinal) == true || scalar.Value?.Contains("Status", StringComparison.Ordinal) == true || scalar.Value?.Contains("ProviderOutcome", StringComparison.Ordinal) == true || scalar.Value?.Contains("Reconciliation", StringComparison.Ordinal) == true))
            .Select(entry => SerializeYaml(entry.Value)));

        string[] forbiddenLeakPatterns =
        [
            "diff --git",
            "rawCommitMessage",
            "raw_commit_message",
            "changedPaths",
            "providerPayload",
            "generatedContext",
            "github.com/",
            "C:\\",
            "D:\\",
            "/home/",
            "/Users/",
            "-----BEGIN",
            "ghp_",
            "eyJ",
        ];

        foreach (string forbidden in forbiddenLeakPatterns)
        {
            combinedExamples.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void CommitStatusContractNotes_RecordDeferredOwnersAndNegativeScope()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string notes = File.ReadAllText(ContractNotesPath);

        foreach (string required in new[] { "Story 1.11", "Story 1.12", "Story 1.13", "Epic 4", "Epic 5", "Story 6.6", "unknown-outcome reconciliation", "commit tier", "contract-only" })
        {
            notes.ShouldContain(required, Case.Insensitive);
        }

        string[] forbiddenRoots =
        [
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "CommitEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Cli", "Commands"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Mcp", "Tools"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Workers", "CommitWorkflows"),
        ];

        foreach (string forbiddenRoot in forbiddenRoots)
        {
            File.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
            Directory.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
        }

        string combined = File.ReadAllText(OpenApiPath) + "\n" + notes;
        Regex.IsMatch(combined, @"git submodule update --init\s+--recursive", RegexOptions.IgnoreCase).ShouldBeFalse();
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

    private static string[] RequiredEnumValues(YamlMappingNode schemas, string schemaName)
    {
        YamlSequenceNode values = RequiredSequence(RequiredMapping(schemas, schemaName), "enum");

        return values
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();
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
