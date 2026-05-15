using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class ContractSpineFoundationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ExtensionVocabularyPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "extensions", "hexalith-extension-vocabulary.yaml");

    private static readonly string[] RequiredExtensions =
    [
        "x-hexalith-audit-metadata-keys",
        "x-hexalith-authorization",
        "x-hexalith-canonical-error-categories",
        "x-hexalith-correlation",
        "x-hexalith-idempotency-equivalence",
        "x-hexalith-idempotency-key",
        "x-hexalith-idempotency-ttl-tier",
        "x-hexalith-lifecycle-states",
        "x-hexalith-parity-dimensions",
        "x-hexalith-read-consistency",
        "x-hexalith-sensitive-metadata-tier",
    ];

    [Fact]
    public void ContractSpineFoundation_IsOpenApi31WithSharedSurface()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);

        GetScalar(root, "openapi").ShouldBe("3.1.0");
        GetScalar(RequiredMapping(root, "info"), "title").ShouldBe("Hexalith.Folders API");
        GetScalar(RequiredMapping(root, "info"), "version").ShouldBe("v1");
        RequiredMapping(root, "paths").Children.ShouldNotBeEmpty();

        string[] tags = RequiredSequence(root, "tags")
            .OfType<YamlMappingNode>()
            .Select(tag => GetScalar(tag, "name") ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        tags.ShouldBe(
        [
            "audit",
            "commits",
            "context-queries",
            "files",
            "folders",
            "ops-console",
            "provider-readiness",
            "query-status",
            "workspaces",
        ]);

        YamlMappingNode components = RequiredMapping(root, "components");
        RequiredMapping(RequiredMapping(components, "securitySchemes"), "oidcBearer");
        YamlMappingNode schemas = RequiredMapping(components, "schemas");
        RequiredMapping(schemas, "ProblemDetails");
        RequiredMapping(schemas, "AcceptedCommand");
        RequiredMapping(schemas, "ProjectedStatus");
        RequiredMapping(schemas, "PagedResult");
        RequiredMapping(schemas, "PagedItem");
        RequiredMapping(schemas, "FreshnessMetadata");
        RequiredMapping(schemas, "SafeAuthorizationDenial");
        RequiredMapping(schemas, "ValidationFailure");
        RequiredMapping(schemas, "IdempotencyConflict");
        RequiredMapping(schemas, "ReconciliationRequired");

        YamlMappingNode parameters = RequiredMapping(components, "parameters");
        RequiredMapping(parameters, "IdempotencyKey");
        RequiredMapping(parameters, "CorrelationId");
        RequiredMapping(parameters, "TaskId");
        RequiredMapping(parameters, "Freshness");
        RequiredMapping(parameters, "RetryAs");

        ResolveRefs(root);
    }

    [Fact]
    public void ContractSpineFoundation_DeclaresRequiredVocabularyOnly()
    {
        YamlMappingNode openApi = LoadYamlMapping(OpenApiPath);
        YamlMappingNode vocabulary = LoadYamlMapping(ExtensionVocabularyPath);

        EnumerateExtensionKeys(openApi).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ShouldBe(RequiredExtensions);
        EnumerateExtensionKeys(vocabulary).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ShouldBe(RequiredExtensions);

        foreach (string extension in RequiredExtensions)
        {
            YamlMappingNode definition = RequiredMapping(vocabulary, extension);

            RequiredSequence(definition, "allowedLocations").Children.Count.ShouldBeGreaterThan(0);
            RequiredMapping(definition, "valueSchema");
            RequiredMapping(definition, "foundationSchema");
            GetScalar(definition, "requirement").ShouldNotBeNullOrWhiteSpace();
            definition.Children.ContainsKey(new YamlScalarNode("example")).ShouldBeTrue(extension);
            GetScalar(definition, "referencePendingPolicy").ShouldNotBeNullOrWhiteSpace();
        }

        YamlMappingNode parityValueSchema = RequiredMapping(RequiredMapping(vocabulary, "x-hexalith-parity-dimensions"), "valueSchema");
        (GetScalar(parityValueSchema, "description") ?? string.Empty).ShouldContain("vocabulary only");
    }

    [Fact]
    public void ContractSpineFoundation_FoundationDeclarationsDoNotRedefineValueSchema()
    {
        YamlMappingNode openApi = LoadYamlMapping(OpenApiPath);
        YamlMappingNode vocabulary = LoadYamlMapping(ExtensionVocabularyPath);

        foreach (string extension in RequiredExtensions)
        {
            YamlMappingNode spineEntry = RequiredMapping(openApi, extension);

            spineEntry.Children.Keys
                .OfType<YamlScalarNode>()
                .Select(key => key.Value ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToArray()
                .ShouldBe(["foundationUse", "vocabularyRef"], $"Spine entry {extension} must declare only vocabularyRef and foundationUse; valueSchema redefinition is forbidden.");

            YamlMappingNode foundationSchema = RequiredMapping(RequiredMapping(vocabulary, extension), "foundationSchema");
            foundationSchema.Children.TryGetValue(new YamlScalarNode("required"), out YamlNode? requiredNode).ShouldBeTrue(extension);
            string[] requiredFields = requiredNode!.ShouldBeOfType<YamlSequenceNode>()
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();
            requiredFields.ShouldContain("vocabularyRef");
            requiredFields.ShouldContain("foundationUse");
        }
    }

    [Fact]
    public void ContractSpineFoundation_VocabularyRefsResolveAcrossFiles()
    {
        YamlMappingNode openApi = LoadYamlMapping(OpenApiPath);
        YamlMappingNode vocabulary = LoadYamlMapping(ExtensionVocabularyPath);

        foreach (string extension in RequiredExtensions)
        {
            YamlMappingNode spineEntry = RequiredMapping(openApi, extension);
            string reference = GetScalar(spineEntry, "vocabularyRef") ?? string.Empty;
            reference.ShouldStartWith("./extensions/hexalith-extension-vocabulary.yaml#/", Case.Sensitive, extension);

            int fragmentIndex = reference.IndexOf('#');
            string fragment = reference[(fragmentIndex + 1)..];
            fragment.ShouldStartWith("/", Case.Sensitive, extension);

            ResolvePointer(vocabulary, fragment[1..].Split('/')).ShouldNotBeNull(reference);
        }
    }

    [Fact]
    public void ContractSpineFoundation_ContainsRequiredSharedSemantics()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");

        string[] problemFields = RequiredMapping(RequiredMapping(schemas, "ProblemDetails"), "properties").Children.Keys
            .OfType<YamlScalarNode>()
            .Select(key => key.Value ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        problemFields.ShouldContain("type");
        problemFields.ShouldContain("title");
        problemFields.ShouldContain("status");
        problemFields.ShouldContain("detail");
        problemFields.ShouldContain("instance");
        problemFields.ShouldContain("category");
        problemFields.ShouldContain("code");
        problemFields.ShouldContain("message");
        problemFields.ShouldContain("correlationId");
        problemFields.ShouldContain("retryable");
        problemFields.ShouldContain("clientAction");
        problemFields.ShouldContain("details");

        RequiredEnumValues(schemas, "IdempotencyTtlTier").ShouldBe(["mutation", "commit"]);
        RequiredEnumValues(schemas, "ReadConsistencyClass").ShouldBe(["snapshot_per_task", "read_your_writes", "eventually_consistent"]);
        RequiredEnumValues(schemas, "CliExitCode").ShouldBe(["0", "1", "64", "65", "66", "67", "68", "69", "70", "71", "72", "73", "74", "75"]);

        string[] canonicalCategories = RequiredEnumValues(schemas, "CanonicalErrorCategory");
        string[] mcpFailureKinds = RequiredEnumValues(schemas, "McpFailureKind");
        foreach (string category in canonicalCategories.Where(c => c is not "success"))
        {
            mcpFailureKinds.ShouldContain(category, $"McpFailureKind must carry {category} to satisfy the 1:1 mapping rule.");
        }

        string serialized = File.ReadAllText(OpenApiPath);
        serialized.ShouldContain("Tenant authority comes from authenticated principal claims and EventStore envelopes");
        serialized.ShouldContain("tenant_sensitive");

        AssertNoTenantAuthorityField(root);
    }

    [Fact]
    public void ContractSpineFoundation_ExamplesAreSyntheticAndScopeIsNegative()
    {
        string openApi = File.ReadAllText(OpenApiPath);
        string vocabulary = File.ReadAllText(ExtensionVocabularyPath);
        string notes = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "contract", "contract-spine-foundation.md"));
        string combined = string.Concat(openApi, "\n", vocabulary, "\n", notes);

        string[] caseInsensitiveBans =
        [
            "github.com/",
            "forgejo",
            "-----BEGIN",
            "diff --git",
        ];
        foreach (string banned in caseInsensitiveBans)
        {
            combined.ShouldNotContain(banned, Case.Insensitive);
        }

        string[] pathPrefixes =
        [
            "C:\\",
            "D:\\",
            "E:\\",
            "/home/",
            "/Users/",
            "/root/",
            "/var/",
        ];
        foreach (string prefix in pathPrefixes)
        {
            combined.ShouldNotContain(prefix, Case.Sensitive, $"contract artifacts must not embed host-specific path prefix {prefix}.");
        }

        string[] tokenShapePatterns =
        [
            @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
            @"sk_(live|test)_[A-Za-z0-9]{16,}",
            @"ghp_[A-Za-z0-9]{20,}",
            @"gho_[A-Za-z0-9]{20,}",
            @"xox[abprs]-[A-Za-z0-9-]{10,}",
            @"AKIA[A-Z0-9]{16}",
        ];
        foreach (string pattern in tokenShapePatterns)
        {
            Regex.IsMatch(combined, pattern).ShouldBeFalse($"contract artifacts must not contain token-shaped strings matching {pattern}.");
        }

        File.Exists(Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml")).ShouldBeFalse();
        AssertNoOperationHandlersUnderContracts();
        AssertNoSdkGeneratedOutput();

        combined.ShouldNotContain("git submodule update --init --recursive");
    }

    private static void AssertNoTenantAuthorityField(YamlMappingNode root)
    {
        foreach (string named in EnumerateNamedFields(root))
        {
            named.Equals("tenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("tenant_id", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("managedTenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
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

            foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
            {
                foreach (string nested in EnumerateNamedFields(child.Value))
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

    private static void AssertNoOperationHandlersUnderContracts()
    {
        string contractsRoot = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts");
        if (!Directory.Exists(contractsRoot))
        {
            return;
        }

        Regex handlerPattern = new(@"\b(MapPost|MapGet|MapPut|MapPatch|MapDelete|HttpPost|HttpGet|HttpPut|HttpPatch|HttpDelete)\b", RegexOptions.Compiled);
        foreach (string file in Directory.EnumerateFiles(contractsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);
            handlerPattern.IsMatch(content).ShouldBeFalse($"Contracts must remain behavior-free; found operation handler pattern in {file}.");
        }
    }

    private static void AssertNoSdkGeneratedOutput()
    {
        string[] forbiddenArtifactNames =
        [
            "nswag.json",
            ".nswag",
        ];
        string contractsRoot = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts");
        if (Directory.Exists(contractsRoot))
        {
            foreach (string file in Directory.EnumerateFiles(contractsRoot, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                foreach (string forbidden in forbiddenArtifactNames)
                {
                    fileName.Equals(forbidden, StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"NSwag configuration must not live in Contracts; found {file}.");
                }
            }
        }

        string[] forbiddenGeneratedRoots =
        [
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "Generated"),
        ];
        foreach (string generatedRoot in forbiddenGeneratedRoots)
        {
            Directory.Exists(generatedRoot).ShouldBeFalse($"SDK generated output must not exist before Story 1.12; found {generatedRoot}.");
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

    private static IEnumerable<string> EnumerateExtensionKeys(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
            {
                if (child.Key is YamlScalarNode key && key.Value is { } value && value.StartsWith("x-hexalith-", StringComparison.Ordinal))
                {
                    yield return value;
                }

                foreach (string nested in EnumerateExtensionKeys(child.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (YamlNode child in sequence.Children)
            {
                foreach (string nested in EnumerateExtensionKeys(child))
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
        path.ShouldSatisfyAllConditions(
            () => File.Exists(path).ShouldBeTrue(path));

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
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlScalarNode>().Value;
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
