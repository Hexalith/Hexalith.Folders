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

            string[] expectedCategories = operation.OperationId == "GetTaskStatus"
                ? new[] { "tenant_access_denied", "read_model_unavailable", "not_found", "redacted" }
                : new[] { "tenant_access_denied", "folder_acl_denied", "read_model_unavailable", "not_found", "redacted" };

            foreach (string expected in expectedCategories)
            {
                categories.ShouldContain(expected, operation.OperationId);
            }

            if (operation.OperationId == "GetTaskStatus")
            {
                categories.ShouldNotContain("folder_acl_denied", "GetTaskStatus path is tenant+task scoped and has no folder ACL layer; declaring folder_acl_denied would leak the task-belongs-to-some-folder relation.");
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

        string[] lifecycleStates = RequiredEnumValues(schemas, "LifecycleState");
        foreach (string c6State in new[] { "requested", "preparing", "ready", "locked", "changes_staged", "dirty", "committed", "failed", "inaccessible", "unknown_provider_outcome", "reconciliation_required" })
        {
            lifecycleStates.ShouldContain(c6State, $"C6 transition matrix requires state '{c6State}'.");
        }
        RequiredEnumValues(schemas, "ProviderOutcomeState").ShouldBe(["pending", "known_success", "known_failure", "unknown_provider_outcome", "reconciliation_required"]);
        RequiredEnumValues(schemas, "ReconciliationState").ShouldBe(["not_required", "required", "in_progress", "completed_clean", "completed_dirty", "failed"]);

        string[] story110Examples =
        [
            "CommitWorkspaceRequest",
            "CommitWorkspaceAccepted",
            "WorkspaceStatusCommitted",
            "WorkspaceStatusUnknownProviderOutcome",
            "TaskStatusFailed",
            "CommitEvidenceRedacted",
            "ProviderOutcomeKnownFailure",
            "ProviderOutcomeUnknown",
            "ReconciliationStatusRequired",
            "ReconciliationStatusCompletedClean",
            "ReconciliationStatusCompletedDirty",
            "UnknownProviderOutcomeProblem",
            "UnknownProviderOutcomeConflictProblem",
            "ProviderUnavailableProblem",
            "ProviderKnownFailureProblem",
            "CommitFailedProblem",
            "DirtyWorkspaceProblem",
            "WorkspaceTransitionInvalidProblem",
            "ReconciliationRequiredProblem",
            "IdempotencyConflictGeneric",
        ];

        foreach (string exampleName in story110Examples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        HashSet<string> story110ExampleSet = new(story110Examples, StringComparer.Ordinal);
        string combinedExamples = string.Join("\n", examples.Children
            .Where(entry => entry.Key is YamlScalarNode scalar && scalar.Value is not null && story110ExampleSet.Contains(scalar.Value))
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
    public void CommitStatusOperations_RejectClientControlledTenantAuthority()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode components = RequiredMapping(root, "components");
        YamlMappingNode schemas = RequiredMapping(components, "schemas");
        Operation[] operations = EnumerateOperations(root).Where(o => CommitStatusOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        operations.Length.ShouldBe(CommitStatusOperationIds.Length);

        foreach (Operation operation in operations)
        {
            operation.Path.ShouldNotContain("{tenantId}", Case.Insensitive, operation.OperationId);
            operation.Path.ShouldNotContain("/tenants/", Case.Insensitive, operation.OperationId);

            foreach (YamlMappingNode parameter in EnumerateParameters(operation.PathItem, operation.Node))
            {
                string? name = GetOptionalScalar(parameter, "name");
                string? referenced = GetOptionalScalar(parameter, "$ref");

                if (name is not null)
                {
                    name.ShouldNotBe("tenantId", $"{operation.OperationId}: client-controlled tenantId parameter is forbidden.");
                    name.ShouldNotBe("X-Hexalith-Tenant-Id", $"{operation.OperationId}: client-controlled tenant header is forbidden.");
                    name.Contains("tenant", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"{operation.OperationId}: parameter '{name}' looks tenant-controlled.");
                }

                if (referenced is not null)
                {
                    referenced.ShouldNotEndWith("/TenantId", $"{operation.OperationId}: $ref to TenantId parameter is forbidden.");
                }
            }

            if (operation.OperationId == "CommitWorkspace")
            {
                YamlMappingNode requestSchema = RequiredMapping(schemas, "CommitWorkspaceRequest");
                YamlMappingNode requestProperties = RequiredMapping(requestSchema, "properties");

                foreach (KeyValuePair<YamlNode, YamlNode> property in requestProperties.Children)
                {
                    string propertyName = property.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                    propertyName.Contains("tenant", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"CommitWorkspaceRequest: property '{propertyName}' looks tenant-controlled; tenant authority is envelope-derived.");
                }
            }
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

        // Per Dev Notes (story 1.10 line 170): "assert forbidden artifact categories and paths, not incidental current filenames, so the check remains stable as the scaffold grows."
        // Each entry is (project, relativePathPattern, description). Patterns target capability-bearing source under conceptual subfolders, not project-root scaffold files.
        (string Project, string Pattern, string Description)[] forbiddenCategories =
        [
            ("Hexalith.Folders.Server", "Endpoints/**/*.cs", "REST handlers/endpoints"),
            ("Hexalith.Folders.Server", "Commands/**/*.cs", "server-side commands"),
            ("Hexalith.Folders", "Aggregates/**/*.cs", "domain aggregate behavior"),
            ("Hexalith.Folders", "Providers/**/*.cs", "provider adapters"),
            ("Hexalith.Folders.Cli", "Commands/**/*.cs", "CLI commands"),
            ("Hexalith.Folders.Mcp", "Tools/**/*.cs", "MCP tools"),
            ("Hexalith.Folders.Workers", "CommitWorkflows/**/*.cs", "commit-workflow workers"),
            ("Hexalith.Folders.Workers", "Reconciliation/**/*.cs", "reconciliation workers"),
            ("Hexalith.Folders.Workers", "Handlers/**/*.cs", "worker event handlers"),
            ("Hexalith.Folders.UI", "Pages/**/*.razor", "UI pages"),
        ];

        string srcRoot = Path.Combine(RepositoryRoot, "src");
        foreach ((string project, string pattern, string description) in forbiddenCategories)
        {
            string projectRoot = Path.Combine(srcRoot, project);
            if (!Directory.Exists(projectRoot))
            {
                continue;
            }

            string[] matches = Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    return !relative.StartsWith("obj/", StringComparison.Ordinal)
                        && !relative.StartsWith("bin/", StringComparison.Ordinal)
                        && MatchesGlob(relative, pattern);
                })
                .ToArray();

            matches.ShouldBeEmpty($"Story 1.10 negative scope: {description} ({project}/{pattern}) must not be added.");
        }

        string combined = File.ReadAllText(OpenApiPath) + "\n" + notes;
        Regex.IsMatch(combined, @"git submodule update --init\s+--recursive", RegexOptions.IgnoreCase).ShouldBeFalse();
    }

    private static bool MatchesGlob(string relativePath, string pattern)
    {
        string regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*/", "(?:.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]") + "$";
        return Regex.IsMatch(relativePath, regexPattern);
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
