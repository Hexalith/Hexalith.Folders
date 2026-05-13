using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class TenantFolderProviderContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ExtensionVocabularyPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "extensions", "hexalith-extension-vocabulary.yaml");

    private static readonly string[] OperationAllowList =
    [
        "CreateFolder",
        "GetFolderLifecycleStatus",
        "ArchiveFolder",
        "ListFolderAclEntries",
        "UpdateFolderAclEntry",
        "GetEffectivePermissions",
        "ConfigureProviderBinding",
        "GetProviderBinding",
        "ValidateProviderReadiness",
        "GetProviderSupportEvidence",
        "CreateRepositoryBackedFolder",
        "BindRepository",
        "GetRepositoryBinding",
        "ConfigureBranchRefPolicy",
        "GetBranchRefPolicy",
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "GetWorkspaceLock",
        "GetWorkspaceRetryEligibility",
        "GetWorkspaceTransitionEvidence",
    ];

    // POST/PUT/PATCH/DELETE operations are mutating by default. Anything in this allow-list
    // is a documented exception (e.g. POST-as-query for ValidateProviderReadiness — see
    // docs/contract/tenant-folder-provider-repository-contract-groups.md#POST-as-query-Exception).
    private static readonly string[] NonMutatingMethodPostAllowList =
    [
        "ValidateProviderReadiness",
    ];

    [Fact]
    public void ContractGroupOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).ToArray();

        operations.Select(o => o.OperationId).Order(StringComparer.Ordinal).ShouldBe(OperationAllowList.Order(StringComparer.Ordinal));
        operations.Select(o => o.Path).Distinct(StringComparer.Ordinal).All(p => p.StartsWith("/api/v1/", StringComparison.Ordinal)).ShouldBeTrue();
        operations.Select(o => o.OperationId).ShouldBeUnique();

        string[] forbiddenPathFragments =
        [
            "/locks",
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
        ResolveExtensionVocabularyRefs();
    }

    [Fact]
    public void ContractGroupOperations_DeclareRequiredMetadataAndIdempotencyRules()
    {
        Operation[] operations = EnumerateOperations(LoadYamlMapping(OpenApiPath)).ToArray();
        HashSet<string> nonMutatingPostAllowList = NonMutatingMethodPostAllowList.ToHashSet(StringComparer.Ordinal);

        foreach (Operation operation in operations)
        {
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-correlation")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-parity-dimensions")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-audit-metadata-keys")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-authorization")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-canonical-error-categories")).ShouldBeTrue(operation.OperationId);

            bool hasIdempotencyHeader = EnumerateParameters(operation.PathItem, operation.Node)
                .Any(parameter => GetScalar(parameter, "name") == "Idempotency-Key" || GetScalar(parameter, "$ref")?.EndsWith("/IdempotencyKey", StringComparison.Ordinal) == true);

            bool isMutatingByMethod = operation.Method is "post" or "put" or "patch" or "delete";
            bool isMutating = isMutatingByMethod && !nonMutatingPostAllowList.Contains(operation.OperationId);

            if (isMutating)
            {
                hasIdempotencyHeader.ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-ttl-tier")).ShouldBeTrue(operation.OperationId);

                string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
                    .OfType<YamlScalarNode>()
                    .Select(value => value.Value ?? string.Empty)
                    .ToArray();

                equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray(), $"{operation.OperationId} idempotency equivalence fields must be lexicographically ordered.");
            }
            else
            {
                hasIdempotencyHeader.ShouldBeFalse($"{operation.OperationId} is non-mutating and must not accept Idempotency-Key.");
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);
            }
        }
    }

    [Fact]
    public void ContractGroupOperations_DoNotExposeTenantAuthorityOrSecretMaterial()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        string serialized = File.ReadAllText(OpenApiPath);

        foreach (string named in EnumerateNamedFields(root))
        {
            named.Equals("tenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("tenant_id", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("managedTenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
        }

        string[] forbiddenFieldTerms =
        [
            "token",
            "secret",
            "password",
            "privateKey",
            "accessToken",
            "credential",
            "apiKey",
        ];

        HashSet<string> credentialReferenceWhitelist = new(StringComparer.Ordinal)
        {
            "nonSecretCredentialReference",
            "non_secret_credential_reference",
        };

        foreach (string named in EnumerateNamedFields(root))
        {
            if (credentialReferenceWhitelist.Contains(named))
            {
                continue;
            }

            foreach (string forbidden in forbiddenFieldTerms)
            {
                named.ShouldNotContain(forbidden, Case.Insensitive, $"secret-shaped field name '{named}' is forbidden.");
            }
        }

        EnumerateNamedFields(root).ShouldContain("nonSecretCredentialReference", "credential references must be explicit non-secret opaque references.");

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
            Regex.IsMatch(serialized, pattern, RegexOptions.IgnoreCase).ShouldBeFalse($"contract examples must not contain token-shaped strings matching {pattern}.");
        }
    }

    [Fact]
    public void ContractGroupOperations_SafeDenialExamplesMatchTheirHttpStatusAndAreAudiencePartitioned()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        // Each canonical safe-denial example must declare a `status:` that matches the HTTP status
        // its response is wired to. The 401/403/404 envelopes use distinct categories, but within
        // each status the response shape is identical across every operation (no per-resource fields
        // that would leak protected resource existence).
        (string ExampleName, int ExpectedStatus)[] safeDenialStatusCases =
        [
            ("SafeDenial401Unauthorized", 401),
            ("SafeDenial403Forbidden", 403),
            ("SafeDenial404NotFound", 404),
        ];

        foreach ((string exampleName, int expectedStatus) in safeDenialStatusCases)
        {
            YamlMappingNode value = RequiredMapping(RequiredMapping(examples, exampleName), "value");
            GetScalar(value, "status").ShouldBe(expectedStatus.ToString(), $"{exampleName} status must match its HTTP response code.");

            // Resource-identity leak check: the safe-denial body must not include fields that
            // would let an unauthorized caller infer which protected resource was queried.
            string[] leakyFieldNames = ["folderId", "providerBindingRef", "repositoryBindingId", "branchRefPolicyRef", "aclEntryId"];
            foreach (string leaky in leakyFieldNames)
            {
                value.Children.ContainsKey(new YamlScalarNode(leaky)).ShouldBeFalse($"{exampleName} must not include resource-identifying field '{leaky}' which would leak existence.");
            }
        }

        // Provider readiness audience partitioning. The schema is a oneOf discriminated by `audience`:
        // consumer-audience callers receive only safe status + retry hint; operator-audience callers
        // receive full sanitized evidence. Both audience examples must be free of credential state,
        // raw provider payloads, and provider installation identity.
        YamlMappingNode consumerExample = RequiredMapping(RequiredMapping(examples, "ProviderReadinessConsumer"), "value");
        GetScalar(consumerExample, "audience").ShouldBe("consumer");
        consumerExample.Children.ContainsKey(new YamlScalarNode("evidence")).ShouldBeFalse("Consumer-audience example must not expose per-capability evidence.");
        consumerExample.Children.ContainsKey(new YamlScalarNode("providerBindingRef")).ShouldBeFalse("Consumer-audience example must not expose providerBindingRef.");
        consumerExample.Children.ContainsKey(new YamlScalarNode("capabilityProfileRef")).ShouldBeFalse("Consumer-audience example must not expose capabilityProfileRef.");
        string consumerSerialized = SerializeYaml(consumerExample);
        consumerSerialized.ShouldNotContain("token", Case.Insensitive);
        consumerSerialized.ShouldNotContain("secret", Case.Insensitive);
        consumerSerialized.ShouldNotContain("installation", Case.Insensitive);

        YamlMappingNode operatorExample = RequiredMapping(RequiredMapping(examples, "ProviderReadinessOperatorDiagnostic"), "value");
        GetScalar(operatorExample, "audience").ShouldBe("authorized_operator");
        string operatorSerialized = SerializeYaml(operatorExample);
        operatorSerialized.ShouldNotContain("token", Case.Insensitive);
        operatorSerialized.ShouldNotContain("secret", Case.Insensitive);
        operatorSerialized.ShouldNotContain("installation", Case.Insensitive);
    }

    [Fact]
    public void ContractGroupOperations_PreserveNegativeScope()
    {
        string[] forbiddenRoots =
        [
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Cli", "Commands"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Mcp", "Tools"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.UI", "Pages"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Workers", "Providers"),
        ];

        foreach (string forbiddenRoot in forbiddenRoots)
        {
            Directory.Exists(forbiddenRoot).ShouldBeFalse($"Story 1.7 must not add downstream artifacts at {forbiddenRoot}.");
        }

        string githubRoot = Path.Combine(RepositoryRoot, ".github");
        if (Directory.Exists(githubRoot))
        {
            string[] workflowFiles = Directory.EnumerateFiles(githubRoot, "*.yml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(githubRoot, "*.yaml", SearchOption.AllDirectories))
                .ToArray();
            workflowFiles.ShouldBeEmpty("Story 1.7 must not add CI gates.");
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
                    yield return new Operation(path, method, GetScalar(operation, "operationId") ?? string.Empty, operation, pathItem);
                }
            }
        }
    }

    private static IEnumerable<YamlMappingNode> EnumerateParameters(YamlMappingNode pathItem, YamlMappingNode operation)
    {
        // Path-level parameters apply to every method under the path-item.
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

    private static void ResolveRefs(YamlMappingNode root)
    {
        foreach (string reference in EnumerateRefs(root))
        {
            // Intra-file references must be JSON-Pointer fragments rooted at the document.
            reference.StartsWith("#/", StringComparison.Ordinal).ShouldBeTrue(reference);
            ResolvePointer(root, reference[2..].Split('/')).ShouldNotBeNull(reference);
        }
    }

    private static void ResolveExtensionVocabularyRefs()
    {
        // The extension vocabulary file holds vocabulary-level $refs back to the spine schemas.
        // We load it separately and confirm every external pointer of the form
        // "../hexalith.folders.v1.yaml#/components/schemas/<X>" actually resolves in the spine,
        // while intra-file pointers resolve within the extension file itself.
        if (!File.Exists(ExtensionVocabularyPath))
        {
            return;
        }

        YamlMappingNode spine = LoadYamlMapping(OpenApiPath);
        YamlMappingNode extensions = LoadYamlMapping(ExtensionVocabularyPath);

        foreach (string reference in EnumerateRefs(extensions))
        {
            if (reference.StartsWith("#/", StringComparison.Ordinal))
            {
                ResolvePointer(extensions, reference[2..].Split('/')).ShouldNotBeNull(reference);
                continue;
            }

            const string spinePrefix = "../hexalith.folders.v1.yaml#/";
            reference.StartsWith(spinePrefix, StringComparison.Ordinal).ShouldBeTrue($"Unsupported $ref shape in extension vocabulary: '{reference}'.");
            ResolvePointer(spine, reference[spinePrefix.Length..].Split('/')).ShouldNotBeNull(reference);
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

    private static string? GetScalar(YamlMappingNode mapping, string key)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
            ? value.ShouldBeOfType<YamlScalarNode>().Value
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
