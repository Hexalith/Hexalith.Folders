using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class TenantFolderProviderContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");

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
    ];

    private static readonly string[] MutatingOperationIds =
    [
        "CreateFolder",
        "ArchiveFolder",
        "UpdateFolderAclEntry",
        "ConfigureProviderBinding",
        "CreateRepositoryBackedFolder",
        "BindRepository",
        "ConfigureBranchRefPolicy",
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
            "/workspaces",
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
    }

    [Fact]
    public void ContractGroupOperations_DeclareRequiredMetadataAndIdempotencyRules()
    {
        Operation[] operations = EnumerateOperations(LoadYamlMapping(OpenApiPath)).ToArray();
        HashSet<string> mutating = MutatingOperationIds.ToHashSet(StringComparer.Ordinal);

        foreach (Operation operation in operations)
        {
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-correlation")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-parity-dimensions")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-audit-metadata-keys")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-authorization")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-canonical-error-categories")).ShouldBeTrue(operation.OperationId);

            bool hasIdempotencyHeader = EnumerateParameters(operation.Node)
                .Any(parameter => GetScalar(parameter, "name") == "Idempotency-Key" || GetScalar(parameter, "$ref")?.EndsWith("/IdempotencyKey", StringComparison.Ordinal) == true);

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
        ];

        foreach (string named in EnumerateNamedFields(root))
        {
            if (named.Equals("nonSecretCredentialReference", StringComparison.Ordinal))
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
    public void ContractGroupOperations_SafeDenialExamplesAreIndistinguishableAndDiagnosticsArePartitioned()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        string[] safeDenialNames =
        [
            "SafeDenialUnauthorizedTenant",
            "SafeDenialMissingFolder",
            "SafeDenialMissingProviderBinding",
            "SafeDenialMissingRepositoryBinding",
            "SafeDenialMissingBranchRefPolicy",
        ];

        string first = SerializeYaml(RequiredMapping(RequiredMapping(examples, safeDenialNames[0]), "value"));
        foreach (string exampleName in safeDenialNames.Skip(1))
        {
            SerializeYaml(RequiredMapping(RequiredMapping(examples, exampleName), "value")).ShouldBe(first, exampleName);
        }

        YamlMappingNode operatorExample = RequiredMapping(RequiredMapping(examples, "ProviderReadinessOperatorDiagnostic"), "value");
        GetScalar(operatorExample, "audience").ShouldBe("authorized_operator");
        SerializeYaml(operatorExample).ShouldNotContain("token", Case.Insensitive);
        SerializeYaml(operatorExample).ShouldNotContain("secret", Case.Insensitive);
        SerializeYaml(operatorExample).ShouldNotContain("installation", Case.Insensitive);
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
            Directory.EnumerateFiles(githubRoot, "*.yml", SearchOption.AllDirectories).ShouldBeEmpty("Story 1.7 must not add CI gates.");
        }
    }

    private sealed record Operation(string Path, string Method, string OperationId, YamlMappingNode Node);

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
                    yield return new Operation(path, method, GetScalar(operation, "operationId") ?? string.Empty, operation);
                }
            }
        }
    }

    private static IEnumerable<YamlMappingNode> EnumerateParameters(YamlMappingNode operation)
    {
        if (!operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
        {
            yield break;
        }

        foreach (YamlNode parameter in parametersNode.ShouldBeOfType<YamlSequenceNode>())
        {
            yield return parameter.ShouldBeOfType<YamlMappingNode>();
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
