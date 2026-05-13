using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class WorkspaceLockContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ContractNotesPath = Path.Combine(RepositoryRoot, "docs", "contract", "workspace-lock-contract-groups.md");

    private static readonly string[] WorkspaceLockOperationIds =
    [
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "GetWorkspaceLock",
        "GetWorkspaceRetryEligibility",
        "GetWorkspaceTransitionEvidence",
    ];

    private static readonly string[] MutatingOperationIds =
    [
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
    ];

    [Fact]
    public void WorkspaceLockOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => WorkspaceLockOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        HashSet<string> actual = operations.Select(o => o.OperationId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> expected = WorkspaceLockOperationIds.ToHashSet(StringComparer.Ordinal);
        actual.SetEquals(expected).ShouldBeTrue($"workspace/lock operation allow-list drift. Expected {string.Join(", ", expected)}, found {string.Join(", ", actual)}.");
        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.Select(o => o.Path).All(p => p.StartsWith("/api/v1/folders/{folderId}/workspaces", StringComparison.Ordinal)).ShouldBeTrue();

        string[] forbiddenPathFragments =
        [
            "/files",
            "/context",
            "/commits",
            "/audit",
            "/ops-console",
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
    public void WorkspaceLockOperations_DeclareRequiredMetadataAndIdempotencyRules()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => WorkspaceLockOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();
        HashSet<string> mutating = MutatingOperationIds.ToHashSet(StringComparer.Ordinal);

        foreach (Operation operation in operations)
        {
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-correlation")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-parity-dimensions")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-audit-metadata-keys")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-authorization")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-canonical-error-categories")).ShouldBeTrue(operation.OperationId);

            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            bool hasIdempotencyHeader = parameters.Any(parameter =>
                (GetOptionalScalar(parameter, "name") == "Idempotency-Key" && GetOptionalScalar(parameter, "in") == "header")
                || GetOptionalScalar(parameter, "$ref") == "#/components/parameters/IdempotencyKey");

            if (mutating.Contains(operation.OperationId))
            {
                hasIdempotencyHeader.ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-ttl-tier")).ShouldBeTrue(operation.OperationId);

                string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
                    .OfType<YamlScalarNode>()
                    .Select(value => value.Value ?? string.Empty)
                    .ToArray();

                equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray(), $"{operation.OperationId} idempotency equivalence fields must be lexicographically ordered.");
                equivalence.ShouldBeUnique();
                equivalence.ShouldContain("task_id", $"{operation.OperationId} must include task_id in idempotency equivalence.");
                equivalence.ShouldNotContain("request_schema_version", $"{operation.OperationId} should not gate equivalence on a single-value schema version constant.");

                bool hasRequiredTaskIdHeader = parameters.Any(parameter =>
                    GetOptionalScalar(parameter, "name") == "X-Hexalith-Task-Id"
                    && GetOptionalScalar(parameter, "required") == "true");
                hasRequiredTaskIdHeader.ShouldBeTrue($"{operation.OperationId} must declare X-Hexalith-Task-Id as a required header because task_id participates in idempotency equivalence.");
            }
            else
            {
                hasIdempotencyHeader.ShouldBeFalse($"{operation.OperationId} is non-mutating and must not accept Idempotency-Key.");
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);
            }
        }
    }

    [Fact]
    public void WorkspaceLockOperations_ExposeC6LeaseRetryAndSafeProblemDetails()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("locked");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("unknown_provider_outcome");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("reconciliation_required");
        RequiredEnumValues(schemas, "LockLeaseStatus").ShouldBe(["active", "stale", "expired", "released", "revoked"]);
        RequiredEnumValues(schemas, "LockState").ShouldBe(["unlocked", "locked", "expired", "stale", "revoked"]);
        RequiredEnumValues(schemas, "WorkspaceTransitionResult").ShouldContain("state_transition_invalid");
        RequiredEnumValues(schemas, "WorkspaceTransitionResult").ShouldContain("authorization_revoked");

        string[] requiredExamples =
        [
            "WorkspaceLockStatus",
            "WorkspaceLockStatusUnlocked",
            "WorkspaceLockStatusExpired",
            "WorkspaceTransitionEvidenceValid",
            "WorkspaceTransitionEvidenceInvalid",
            "WorkspaceTransitionEvidenceAuthorizationRevoked",
            "WorkspaceTransitionInvalidProblem",
            "WorkspaceAuthorizationRevokedProblem",
            "WorkspaceLockExpiredProblem",
            "WorkspaceLockConflictProblem",
            "WorkspaceRetryEligible",
            "WorkspaceRetryBlocked",
            "IdempotencyConflictGeneric",
            "ReconciliationRequiredProblem",
            "ProviderReadinessFailedProblem",
            "WorkspacePreparationFailedProblem",
            "UnknownProviderOutcomeProblem",
            "ProviderUnavailableProblem",
        ];

        foreach (string exampleName in requiredExamples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        Operation[] operations = EnumerateOperations(root).Where(o => WorkspaceLockOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();
        HashSet<string> mutating = MutatingOperationIds.ToHashSet(StringComparer.Ordinal);
        foreach (Operation operation in operations)
        {
            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            categories.ShouldContain("tenant_access_denied", operation.OperationId);
            categories.ShouldContain("folder_acl_denied", operation.OperationId);

            if (mutating.Contains(operation.OperationId))
            {
                categories.ShouldContain("state_transition_invalid", $"{operation.OperationId} is mutating and must surface state_transition_invalid as a canonical category.");
            }
            else
            {
                categories.ShouldNotContain("state_transition_invalid", $"{operation.OperationId} is a query and cannot cause a state transition; state_transition_invalid is body-encoded in WorkspaceTransitionEvidence.result instead.");
            }
        }

        string[] preparationCategories = RequiredSequence(operations.Single(o => o.OperationId == "PrepareWorkspace").Node, "x-hexalith-canonical-error-categories")
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();
        preparationCategories.ShouldContain("provider_readiness_failed");
        preparationCategories.ShouldContain("workspace_preparation_failed");
        preparationCategories.ShouldContain("unknown_provider_outcome");
        preparationCategories.ShouldContain("reconciliation_required");
        preparationCategories.ShouldContain("idempotency_conflict");
        preparationCategories.ShouldContain("lock_conflict");
    }

    [Fact]
    public void WorkspaceLockOperations_DoNotExposeTenantAuthoritySecretMaterialOrForbiddenScope()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        string serialized = File.ReadAllText(OpenApiPath);
        string notes = File.ReadAllText(ContractNotesPath);
        string combined = string.Concat(serialized, "\n", notes);

        foreach (string named in EnumerateNamedFields(root))
        {
            named.Equals("tenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("tenant_id", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("managedTenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
        }

        foreach (string named in EnumerateNamedFields(root))
        {
            bool allowedNonSecretReference = named is "nonSecretCredentialReference" or "lockOwnershipProof";
            if (allowedNonSecretReference)
            {
                continue;
            }

            string[] forbiddenFieldTerms = ["token", "secret", "password", "privateKey", "accessToken", "credential", "apiKey"];
            foreach (string forbidden in forbiddenFieldTerms)
            {
                named.ShouldNotContain(forbidden, Case.Insensitive, $"secret-shaped field name '{named}' is forbidden.");
            }
        }

        string lockProofDescription = SerializeYaml(RequiredMapping(RequiredMapping(RequiredMapping(schemas, "ReleaseWorkspaceLockRequest"), "properties"), "lockOwnershipProof"));
        lockProofDescription.ShouldContain("non-secret opaque lock proof", Case.Insensitive);
        lockProofDescription.ShouldNotContain("bearer", Case.Insensitive);
        lockProofDescription.ShouldNotContain("provider credential", Case.Insensitive);

        string[] tokenShapePatterns =
        [
            @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
            @"sk_(live|test)_[A-Za-z0-9]{16,}",
            @"ghp_[A-Za-z0-9]{20,}",
            @"gho_[A-Za-z0-9]{20,}",
            @"-----BEGIN",
        ];

        foreach (string pattern in tokenShapePatterns)
        {
            Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase).ShouldBeFalse($"contract artifacts must not contain token-shaped strings matching {pattern}.");
        }

        string[] forbiddenTexts =
        [
            "src/Hexalith.Folders.Server",
            "src/Hexalith.Folders.Client/Generated",
            "src/Hexalith.Folders.Cli",
            "src/Hexalith.Folders.Mcp",
            "src/Hexalith.Folders.Workers",
            string.Concat("git submodule update --init ", "--recursive"),
        ];

        foreach (string forbidden in forbiddenTexts)
        {
            combined.ShouldNotContain(forbidden, Case.Sensitive);
        }
    }

    [Fact]
    public void WorkspaceLockSafeDenialEnvelopes_AreExternallyIndistinguishable()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        // 403 and 404 must be byte-identical so callers cannot distinguish absent-from-forbidden,
        // cross-tenant-from-missing, or missing-resource-from-stale-read. 401 carries a different
        // legitimate signal (no token) and is intentionally distinguishable from authenticated denials.
        string[] indistinguishableExamples = ["SafeDenial403Forbidden", "SafeDenial404NotFound"];
        Dictionary<string, YamlMappingNode> envelopes = indistinguishableExamples
            .ToDictionary(name => name, name => RequiredMapping(RequiredMapping(examples, name), "value"));

        string[] requiredKeys = ["type", "title", "category", "code", "message", "retryable", "clientAction", "details"];
        foreach (string key in requiredKeys)
        {
            HashSet<string> values = envelopes.Values
                .Select(envelope => GetOptionalScalar(envelope, key) ?? SerializeYaml(envelope.Children[new YamlScalarNode(key)]).Trim())
                .ToHashSet(StringComparer.Ordinal);
            values.Count.ShouldBe(1, $"safe-denial envelope key '{key}' must carry the same value across 403/404 examples; found {values.Count} distinct values.");
        }

        // 401 must still redact details and carry no resource-existence hint, even though its title/category differ.
        YamlMappingNode envelope401 = RequiredMapping(RequiredMapping(examples, "SafeDenial401Unauthorized"), "value");
        YamlMappingNode details401 = RequiredMapping(envelope401, "details");
        GetOptionalScalar(details401, "visibility").ShouldBe("redacted");
        details401.Children.Count.ShouldBe(1, "SafeDenial401Unauthorized.details must contain only the redaction marker.");

        foreach ((string name, YamlMappingNode envelope) in envelopes)
        {
            YamlMappingNode details = RequiredMapping(envelope, "details");
            GetOptionalScalar(details, "visibility").ShouldBe("redacted", name);
            details.Children.Count.ShouldBe(1, $"{name}.details must contain only the redaction marker; extra keys leak case-specific information.");
        }
    }

    [Fact]
    public void WorkspaceLockOwnershipProof_NeverAppearsInUnauthorizedOrLeakyProblemDetails()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        YamlMappingNode releaseRequest = RequiredMapping(examples, "ReleaseWorkspaceLockRequest");
        YamlMappingNode releaseValue = RequiredMapping(releaseRequest, "value");
        string proofValue = GetOptionalScalar(releaseValue, "lockOwnershipProof")
            ?? throw new InvalidOperationException("ReleaseWorkspaceLockRequest example must include a synthetic lockOwnershipProof value.");

        string[] unauthorizedOrLeakyExamples =
        [
            "SafeDenial401Unauthorized",
            "SafeDenial403Forbidden",
            "SafeDenial404NotFound",
            "WorkspaceLockExpiredProblem",
            "WorkspaceLockConflictProblem",
            "WorkspaceAuthorizationRevokedProblem",
            "WorkspaceTransitionInvalidProblem",
            "ProviderReadinessFailedProblem",
            "WorkspacePreparationFailedProblem",
            "UnknownProviderOutcomeProblem",
            "ProviderUnavailableProblem",
            "IdempotencyConflictGeneric",
            "ReconciliationRequiredProblem",
        ];

        foreach (string exampleName in unauthorizedOrLeakyExamples)
        {
            string serializedExample = SerializeYaml(RequiredMapping(examples, exampleName));
            serializedExample.ShouldNotContain(proofValue, Case.Sensitive, $"Problem Details example '{exampleName}' must not echo the lockOwnershipProof value '{proofValue}'.");
            serializedExample.ShouldNotContain("lockOwnershipProof", Case.Sensitive, $"Problem Details example '{exampleName}' must not include the lockOwnershipProof field at all.");
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

    private static IEnumerable<string> EnumerateNamedFields(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            if (mapping.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? nameNode) && nameNode is YamlScalarNode nameScalar && nameScalar.Value is { } nameValue)
            {
                yield return nameValue;
            }

            if (mapping.Children.TryGetValue(new YamlScalarNode("properties"), out YamlNode? propsNode) && propsNode is YamlMappingNode propsMapping)
            {
                foreach (YamlNode propKey in propsMapping.Children.Keys)
                {
                    if (propKey is YamlScalarNode propScalar && propScalar.Value is { } propValue)
                    {
                        yield return propValue;
                    }
                }
            }

            foreach (YamlNode child in mapping.Children.Values)
            {
                foreach (string nested in EnumerateNamedFields(child))
                {
                    yield return nested;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (YamlNode child in sequence.Children)
            {
                foreach (string nested in EnumerateNamedFields(child))
                {
                    yield return nested;
                }
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
