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
    public void Oq2CanonicalPolicyPinsPathVocabularyPrecedenceContentAndSafeDenial()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string policy = File.ReadAllText(ContractNotesPath);

        string[] requiredStatements =
        [
            "Policy version: `1.0.0`",
            "Approved by: `Administrator` for PM, Architecture, and Security",
            "at most 500 characters",
            "ASCII `A-Z a-z 0-9 . _ - /`",
            "caller's exact accepted spelling",
            "ordinal-ignore-case semantics",
            "touched entry and each existing ancestor",
            "no-follow",
            "`content_allowed`",
            "`metadata_only`",
            "`excluded`",
            "`restricted`",
            "non-empty include allowlist",
            "Exclusions are then evaluated and always win",
            "Re-inclusion after",
            "an exclusion is unsupported",
            "invalid, empty,",
            "unbounded, stale, unreadable, or unavailable policy fails closed",
            "1,048,576 bytes",
            "at most 100 changes",
            "10,485,760 aggregate add/change bytes",
            "strict UTF-8 validation",
            "one UTF-8 BOM at byte zero",
            "HTTP 404",
            "`category: tenant_access_denied`",
            "`code: resource_unavailable`",
            "HTTP 416 is reserved for a caller that is already authorized",
            "Stories 12.1, 12.3, and 4.20 and FR32-FR35 runtime proof remain incomplete",
        ];

        foreach (string statement in requiredStatements)
        {
            policy.ShouldContain(statement, Case.Sensitive);
        }

        policy.ShouldNotContain("safe-denial-matrix follow-up", Case.Insensitive);
        policy.ShouldNotContain("path-policy-class definition story", Case.Insensitive);
        policy.ShouldNotContain("parser-policy story", Case.Insensitive);
        policy.ShouldNotContain("tenant_sensitive_document", Case.Sensitive);
    }

    [Fact]
    public void Oq2OpenApiPinsCanonicalPathClassesMutationBoundsAndRouting()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode policy = RequiredMapping(root, "x-hexalith-file-policy");
        GetScalar(policy, "version").ShouldBe("1.0.0");
        GetScalar(policy, "canonicalArtifact").ShouldBe("docs/contract/file-context-contract-groups.md");

        YamlMappingNode pathProfile = RequiredMapping(policy, "pathProfile");
        GetScalar(pathProfile, "characterProfile").ShouldBe("ASCII A-Z a-z 0-9 . _ - /");
        GetScalar(pathProfile, "maximumCharacters").ShouldBe("500");
        GetScalar(pathProfile, "unicodeNormalization").ShouldBe("NFC");
        GetScalar(pathProfile, "collisionComparison").ShouldBe("ordinal-ignore-case");
        GetScalar(pathProfile, "callerSpelling").ShouldBe("preserve-without-retargeting");
        GetScalar(pathProfile, "linkHandling").ShouldBe("reject-touched-entry-or-ancestor-without-following");

        RequiredSequence(policy, "policyClasses").Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["content_allowed", "metadata_only", "excluded", "restricted"]);

        YamlMappingNode precedence = RequiredMapping(policy, "policyPrecedence");
        GetScalar(precedence, "includeAllowlistRequired").ShouldBe("true");
        GetScalar(precedence, "exclusionsAlwaysWin").ShouldBe("true");
        GetScalar(precedence, "reInclusionSupported").ShouldBe("false");
        GetScalar(precedence, "invalidOrUnavailablePolicy").ShouldBe("fail-closed");

        YamlMappingNode limits = RequiredMapping(policy, "mutationLimits");
        GetScalar(limits, "inlineTransportBytes").ShouldBe("262144");
        GetScalar(limits, "perFileBytes").ShouldBe("1048576");
        GetScalar(limits, "maximumChanges").ShouldBe("100");
        GetScalar(limits, "aggregateBytes").ShouldBe("10485760");
        GetScalar(limits, "validation").ShouldBe("atomic-apply-none-on-any-failure");

        YamlMappingNode readability = RequiredMapping(policy, "contentReadability");
        GetScalar(readability, "encoding").ShouldBe("strict-utf-8-with-optional-leading-bom");
        GetScalar(readability, "binaryAndOtherEncodings").ShouldBe("metadata-only");
        GetScalar(readability, "truncation").ShouldBe("forbidden");

        YamlMappingNode hidden = RequiredMapping(policy, "hiddenPathOutcome");
        GetScalar(hidden, "status").ShouldBe("404");
        GetScalar(hidden, "category").ShouldBe("tenant_access_denied");
        GetScalar(hidden, "code").ShouldBe("resource_unavailable");
        GetScalar(policy, "authorizedUnsatisfiableRangeStatus").ShouldBe("416");

        foreach (Operation operation in EnumerateOperations(root).Where(operation => FileContextOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)))
        {
            GetScalar(RequiredMapping(RequiredMapping(operation.Node, "responses"), "404"), "$ref")
                .ShouldBe("#/components/responses/SafeAuthorizationDenial404", operation.OperationId);
        }

        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode pathMetadata = RequiredMapping(schemas, "PathMetadata");
        YamlMappingNode pathProperties = RequiredMapping(pathMetadata, "properties");
        YamlMappingNode normalizedPath = RequiredMapping(pathProperties, "normalizedPath");
        GetScalar(normalizedPath, "maxLength").ShouldBe("500");
        string pathPattern = GetScalar(normalizedPath, "pattern");
        Regex.IsMatch("Docs/A-1_b.c", pathPattern, RegexOptions.CultureInvariant).ShouldBeTrue();
        foreach (string rejected in new[] { "/docs/a.md", "docs/a.md/", "docs//a.md", "docs/./a.md", "docs/../a.md", "docs/con.txt", "docs\\a.md", "docs/é.md" })
        {
            Regex.IsMatch(rejected, pathPattern, RegexOptions.CultureInvariant).ShouldBeFalse(rejected);
        }

        YamlMappingNode pathPolicyClass = RequiredMapping(pathProperties, "pathPolicyClass");
        RequiredSequence(pathPolicyClass, "enum").Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["content_allowed", "metadata_only", "excluded", "restricted"]);

        YamlMappingNode mutation = RequiredMapping(schemas, "FileMutationRequest");
        YamlMappingNode mutationLimits = RequiredMapping(mutation, "x-hexalith-change-set-limits");
        GetScalar(mutationLimits, "maximumChanges").ShouldBe("100");
        GetScalar(mutationLimits, "aggregateBytes").ShouldBe("10485760");
        GetScalar(mutationLimits, "perFileBytes").ShouldBe("1048576");
        GetScalar(RequiredMapping(RequiredMapping(mutation, "properties"), "byteLength"), "maximum").ShouldBe("1048576");
        GetScalar(RequiredMapping(RequiredMapping(schemas, "PutFileStream"), "properties").Children[new YamlScalarNode("declaredLength")].ShouldBeOfType<YamlMappingNode>(), "maximum").ShouldBe("1048576");
        GetScalar(RequiredMapping(RequiredMapping(schemas, "PutFileStream"), "properties").Children[new YamlScalarNode("observedLength")].ShouldBeOfType<YamlMappingNode>(), "maximum").ShouldBe("1048576");

        Operation rangeRead = EnumerateOperations(root).Single(operation => operation.OperationId == "ReadFileRange");
        YamlMappingNode responses = RequiredMapping(rangeRead.Node, "responses");
        GetScalar(RequiredMapping(responses, "404"), "$ref").ShouldBe("#/components/responses/SafeAuthorizationDenial404");
        YamlMappingNode rangeExamples = RequiredMapping(RequiredMapping(RequiredMapping(responses, "416"), "content"), "application/problem+json");
        string rangeResponse = SerializeYaml(rangeExamples);
        rangeResponse.ShouldContain("ReadFileRangeUnsatisfiableProblem", Case.Sensitive);
        rangeResponse.ShouldNotContain("redacted", Case.Insensitive);
        rangeResponse.ShouldNotContain("sensitivity-denied", Case.Insensitive);

        string[] rangeCategories = RequiredSequence(rangeRead.Node, "x-hexalith-canonical-error-categories")
            .Children.Cast<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray();
        rangeCategories.ShouldContain("range_unsatisfiable");
        rangeCategories.ShouldNotContain("redacted");

        YamlMappingNode safe404 = RequiredMapping(RequiredMapping(RequiredMapping(root, "components"), "examples"), "SafeDenial404NotFound");
        YamlMappingNode safe404Value = RequiredMapping(safe404, "value");
        GetScalar(safe404Value, "status").ShouldBe("404");
        GetScalar(safe404Value, "category").ShouldBe("tenant_access_denied");
        GetScalar(safe404Value, "code").ShouldBe("resource_unavailable");

        string openApi = File.ReadAllText(OpenApiPath);
        openApi.ShouldNotContain("ReadFileRangeRedactedProblem", Case.Sensitive);
        openApi.ShouldNotContain("safe-denial-matrix follow-up", Case.Insensitive);
        openApi.ShouldNotContain("path-policy-class definition story", Case.Insensitive);
        openApi.ShouldNotContain("tenant_sensitive_document", Case.Sensitive);
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

            string[] expectedEquivalence = operation.OperationId switch
            {
                "AddFile" or "ChangeFile" => new[] { "content_hash_reference", "file_operation_kind", "operation_id", "path_metadata", "path_policy_class", "task_id", "workspace_id" },
                "RemoveFile" => new[] { "file_operation_kind", "operation_id", "path_metadata", "path_policy_class", "task_id", "workspace_id" },
                _ => throw new InvalidOperationException($"Unexpected mutating operation '{operation.OperationId}' — extend the equivalence membership matrix when adding mutations."),
            };
            equivalence.ShouldBe(expectedEquivalence, $"{operation.OperationId} equivalence membership must match the Story 1.5 field list exactly.");
            equivalence.ShouldNotContain("tenant_id", "tenant authority is envelope-derived and must not be client-controlled OpenAPI equivalence.");

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("held workspace lock", Case.Insensitive, operation.OperationId);
            if (operation.OperationId is "AddFile" or "ChangeFile")
            {
                serializedOperation.ShouldContain("PutFileInline", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("PutFileStream", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("262144", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("X-Hexalith-Retry-Transport", Case.Sensitive, operation.OperationId);
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

            foreach (string expected in new[] { "tenant_access_denied", "folder_acl_denied", "path_validation_failed", "input_limit_exceeded", "response_limit_exceeded", "query_timeout", "read_model_unavailable" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }

            if (operation.OperationId == "ReadFileRange")
            {
                categories.ShouldContain("range_unsatisfiable", operation.OperationId);
                categories.ShouldNotContain("redacted", operation.OperationId);
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

        // Targeted schema assertions (P14): bound specific schema nodes to specific C4 values
        // so substring drift in unrelated schemas does not silently shield this story's bounds.
        YamlMappingNode fileMetadataRequest = RequiredMapping(schemas, "FileMetadataRequest");
        YamlMappingNode paths = RequiredMapping(RequiredMapping(fileMetadataRequest, "properties"), "paths");
        GetScalar(paths, "maxItems").ShouldBe("100");

        YamlMappingNode fileTreeResultSchema = RequiredMapping(schemas, "FileTreeResult");
        YamlMappingNode treeItems = RequiredMapping(RequiredMapping(fileTreeResultSchema, "properties"), "items");
        GetScalar(treeItems, "maxItems").ShouldBe("2000");

        YamlMappingNode fileSearchRequest = RequiredMapping(schemas, "FileSearchRequest");
        YamlMappingNode searchLimit = RequiredMapping(RequiredMapping(fileSearchRequest, "properties"), "limit");
        GetScalar(searchLimit, "maximum").ShouldBe("500");

        YamlMappingNode fileGlobRequest = RequiredMapping(schemas, "FileGlobRequest");
        YamlMappingNode globLimit = RequiredMapping(RequiredMapping(fileGlobRequest, "properties"), "limit");
        GetScalar(globLimit, "maximum").ShouldBe("500");

        YamlMappingNode contextLimits = RequiredMapping(schemas, "ContextQueryLimitMetadata");
        YamlMappingNode actualBytes = RequiredMapping(RequiredMapping(contextLimits, "properties"), "actualBytes");
        GetScalar(actualBytes, "maximum").ShouldBe("1048576");

        YamlMappingNode contentHashReference = RequiredMapping(schemas, "ContentHashReference");
        GetScalar(contentHashReference, "pattern").ShouldStartWith("^hashref_");

        string[] requiredExamples =
        [
            "AddFileInlineRequest",
            "ChangeFileStreamRequest",
            "RemoveFileRequest",
            "FileTreeResult",
            "FileTreeResultTruncated",
            "FileMetadataResult",
            "SearchFolderFilesRequest",
            "GlobFolderFilesRequest",
            "ReadFileRangeMinimumRequest",
            "ReadFileRangeMaximumRequest",
            "ReadFileRangeInvalidReversedProblem",
            "ReadFileRangeOverBoundProblem",
            "ReadFileRangeUnsatisfiableProblem",
            "SafeDenial404NotFound",
            "ContextInputLimitExceededProblem",
        ];

        foreach (string exampleName in requiredExamples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string combinedExamples = string.Join("\n", requiredExamples.Select(name => SerializeYaml(RequiredMapping(examples, name))));
        string[] forbiddenLeakPatterns =
        [
            "diff --git",
            "BEGIN ",
            "-----BEGIN",
            "PRIVATE KEY",
            "ssh-rsa ",
            "xoxb-",
            "xoxp-",
            "ghp_",
            "ghs_",
            "github_pat_",
            "AKIA",
            "AIza",
            "eyJ",
            "BEGIN_PGP",
            "github.com/",
            "gitlab.com/",
            "C:\\",
            "D:\\",
            "/home/",
            "/Users/",
            "matchedLine",
            "snippet",
            "rawSearchText",
            "generatedContext",
            "providerPayload",
        ];
        foreach (string forbidden in forbiddenLeakPatterns)
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
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "FileEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "ContextQueryEndpoints.cs"),
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
