using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class ContractSpineCiGateTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string PreviousSpinePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "previous-spine.yaml");
    private static readonly string WorkflowPath = Path.Combine(RepositoryRoot, ".github", "workflows", "contract-spine.yml");
    private static readonly string GateScriptPath = Path.Combine(RepositoryRoot, "tests", "tools", "run-contract-spine-gates.ps1");
    private static readonly string GateDocumentationPath = Path.Combine(RepositoryRoot, "docs", "contract", "contract-spine-ci-gates.md");

    [Fact]
    public void WorkflowWiresFocusedContractGeneratedArtifactGatesOnly()
    {
        string workflow = File.ReadAllText(WorkflowPath);
        string script = File.ReadAllText(GateScriptPath);

        workflow.ShouldContain("actions/checkout@v6");
        workflow.ShouldContain("submodules: false");
        workflow.ShouldContain("actions/setup-dotnet@v5");
        workflow.ShouldContain("global-json-file: global.json");
        workflow.ShouldContain("dotnet restore Hexalith.Folders.slnx");
        workflow.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore");
        workflow.ShouldContain("./tests/tools/run-contract-spine-gates.ps1 -NoRestore");
        workflow.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflow.ShouldNotContain("dotnet publish", Case.Insensitive);
        workflow.ShouldNotContain("semantic-release", Case.Insensitive);
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj");
        script.ShouldContain("tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldNotContain("--recursive", Case.Insensitive);
    }

    [Fact]
    public void ServerVsSpineGateFailsClosedUntilSingleNonSelfServerSourceExists()
    {
        string[] candidates = DiscoverServerOpenApiSources();

        GateDiagnostic diagnostic = EvaluateServerSource(candidates);

        diagnostic.Gate.ShouldBe("server-vs-spine");
        diagnostic.Category.ShouldBe("prerequisite-drift");
        diagnostic.RepositoryPath.ShouldBe("src/Hexalith.Folders.Server");
        diagnostic.Message.ShouldContain("No repository-local server OpenAPI source");
        AssertMetadataOnly(diagnostic.ToString());
    }

    [Fact]
    public void ServerVsSpineSourceResolutionRejectsSelfReferenceAndAmbiguity()
    {
        GateDiagnostic selfReference = EvaluateServerSource(["src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml"]);
        GateDiagnostic ambiguous = EvaluateServerSource(["src/Hexalith.Folders.Server/openapi/a.yaml", "src/Hexalith.Folders.Server/openapi/b.yaml"]);

        selfReference.Category.ShouldBe("prerequisite-drift");
        selfReference.Message.ShouldContain("Contract Spine itself");
        ambiguous.Category.ShouldBe("prerequisite-drift");
        ambiguous.Message.ShouldContain("Multiple repository-local server OpenAPI sources");
        AssertMetadataOnly(selfReference.ToString());
        AssertMetadataOnly(ambiguous.ToString());
    }

    [Fact]
    public void ServerVsSpineComparisonDetectsPublicContractDrift()
    {
        string original = File.ReadAllText(OpenApiPath);
        (string Name, string Content)[] mutations =
        [
            ("operation id", ReplaceOnce(original, "operationId: CreateFolder", "operationId: CreateFolderRenamed")),
            ("path", ReplaceOnce(original, "  /api/v1/folders:", "  /api/v1/folders-renamed:")),
            ("required header", ReplaceOnce(original, "#/components/parameters/IdempotencyKey", "#/components/parameters/CorrelationId")),
            ("Problem Details category", ReplaceOnce(original, "validation_error", "validation_error_changed")),
            ("extension metadata", ReplaceOnce(original, "x-hexalith-read-consistency:", "x-hexalith-read-consistency-disabled:")),
        ];

        foreach ((string name, string content) in mutations)
        {
            GateDiagnostic diagnostic = CompareServerOpenApiToSpine(content, original, "src/Hexalith.Folders.Server/openapi/hexalith.folders.server.yaml");

            diagnostic.Gate.ShouldBe("server-vs-spine", name);
            diagnostic.Category.ShouldBe("server-spine-mismatch", name);
            diagnostic.Message.ShouldContain("normalized_hash", Case.Sensitive, name);
            AssertMetadataOnly(diagnostic.ToString());
        }
    }

    [Fact]
    public void PreviousSpineBaselinePinsCurrentOperationInventory()
    {
        OperationIdentity[] current = LoadOpenApiOperations(OpenApiPath).OrderBy(o => o.OperationId, StringComparer.Ordinal).ToArray();
        OperationIdentity[] baseline = LoadPreviousSpineOperations(PreviousSpinePath).OrderBy(o => o.OperationId, StringComparer.Ordinal).ToArray();

        current.ShouldNotBeEmpty();
        baseline.ShouldNotBeEmpty();
        baseline.ShouldBe(current);

        string baselineText = File.ReadAllText(PreviousSpinePath);
        baselineText.ShouldNotContain("operations: []");
        baselineText.ShouldContain("approved_additions:");
        baselineText.ShouldContain("synthetic_data_only: true");
    }

    [Fact]
    public void ContractSpineGateDocumentationRecordsPrerequisitesAndBoundedDiagnostics()
    {
        string documentation = File.ReadAllText(GateDocumentationPath);
        string[] categories =
        [
            "contract-spine-drift",
            "server-spine-mismatch",
            "previous-spine-drift",
            "generated-client-drift",
            "parity-oracle-mismatch",
            "generation-nondeterminism",
            "prerequisite-drift",
        ];

        documentation.ShouldContain("dotnet restore Hexalith.Folders.slnx");
        documentation.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore");
        documentation.ShouldContain(".\\tests\\tools\\run-contract-spine-gates.ps1 -NoRestore");
        documentation.ShouldContain("Server OpenAPI emission");
        documentation.ShouldContain("Story 1.15");
        documentation.ShouldContain("Story 1.16");
        foreach (string category in categories)
        {
            documentation.ShouldContain(category);
        }

        AssertMetadataOnly(documentation);
    }

    private static readonly string[] ExcludedSubtreeSegments =
    [
        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Generated{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
    ];

    private static string[] DiscoverServerOpenApiSources()
    {
        string src = Path.Combine(RepositoryRoot, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        return Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Hexalith.Folders.Server{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => Path.GetExtension(path) is ".yaml" or ".yml" or ".json")
            .Where(path => !ExcludedSubtreeSegments.Any(segment => path.Contains(segment, StringComparison.Ordinal)))
            .Where(path =>
            {
                string relative = ToRepositoryPath(path);
                if (string.Equals(relative, "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml", StringComparison.Ordinal))
                {
                    return false;
                }

                return ContainsTopLevelOpenApiKey(path);
            })
            .Select(ToRepositoryPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ContainsTopLevelOpenApiKey(string path)
    {
        using StreamReader reader = File.OpenText(path);
        for (int read = 0; read < 64; read++)
        {
            string? line = reader.ReadLine();
            if (line is null)
            {
                return false;
            }

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("openapi:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("\"openapi\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static GateDiagnostic EvaluateServerSource(IReadOnlyList<string> repositoryRelativeCandidates)
    {
        if (repositoryRelativeCandidates.Count == 0)
        {
            return new("server-vs-spine", "prerequisite-drift", "src/Hexalith.Folders.Server", "No repository-local server OpenAPI source exists yet; add offline server emission before comparing server output with the Contract Spine.");
        }

        if (repositoryRelativeCandidates.Any(candidate => string.Equals(candidate, "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml", StringComparison.Ordinal)))
        {
            return new("server-vs-spine", "prerequisite-drift", "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml", "Server OpenAPI source resolution pointed at the Contract Spine itself.");
        }

        if (repositoryRelativeCandidates.Count > 1)
        {
            return new("server-vs-spine", "prerequisite-drift", "src/Hexalith.Folders.Server", "Multiple repository-local server OpenAPI sources were found; configure one explicit source before comparing.");
        }

        return new("server-vs-spine", "server-spine-mismatch", repositoryRelativeCandidates[0], "Server source is configured; normalized comparison belongs to the server emission implementation story.");
    }

    private static GateDiagnostic CompareServerOpenApiToSpine(string serverOpenApi, string contractSpine, string serverRepositoryPath)
    {
        string serverNormalized = NormalizeOpenApiForComparison(serverOpenApi);
        string spineNormalized = NormalizeOpenApiForComparison(contractSpine);
        if (string.Equals(serverNormalized, spineNormalized, StringComparison.Ordinal))
        {
            return new("server-vs-spine", "contract-spine-drift", serverRepositoryPath, "Server OpenAPI source matches the Contract Spine.");
        }

        return new(
            "server-vs-spine",
            "server-spine-mismatch",
            serverRepositoryPath,
            $"Server OpenAPI normalized_hash={StableHash(serverNormalized)} does not match Contract Spine normalized_hash={StableHash(spineNormalized)}.");
    }

    private static string ReplaceOnce(string text, string oldValue, string newValue)
    {
        int index = text.IndexOf(oldValue, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Mutation anchor not found: {oldValue}");
        return text.Remove(index, oldValue.Length).Insert(index, newValue);
    }

    private static string NormalizeOpenApiForComparison(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Trim();

    private static string StableHash(string value)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static OperationIdentity[] LoadOpenApiOperations(string path)
    {
        YamlMappingNode root = LoadYamlMapping(path);
        List<OperationIdentity> operations = [];
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            string route = RequiredScalar(pathEntry.Key, "path");
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = RequiredScalar(methodEntry.Key, "method").ToLowerInvariant();
                if (method is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                string operationId = RequiredScalar(methodEntry.Value.ShouldBeOfType<YamlMappingNode>(), "operationId");
                operations.Add(new(operationId, method, route));
            }
        }

        return operations.ToArray();
    }

    private static OperationIdentity[] LoadPreviousSpineOperations(string path)
    {
        YamlMappingNode root = LoadYamlMapping(path);
        return RequiredSequence(root, "operations")
            .Children
            .Select(node => node.ShouldBeOfType<YamlMappingNode>())
            .Select(operation => new OperationIdentity(
                RequiredScalar(operation, "operation_id"),
                RequiredScalar(operation, "method"),
                RequiredScalar(operation, "path")))
            .ToArray();
    }

    private static void AssertMetadataOnly(string value)
    {
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "contentBytes",
            "raw provider payload",
            "https://",
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            RepositoryRoot.Replace("\\", "\\\\", StringComparison.Ordinal),
            "C:\\",
            "D:\\",
            "/home/",
            "/Users/",
        ];

        foreach (string forbiddenValue in forbidden)
        {
            value.ShouldNotContain(forbiddenValue, Case.Insensitive);
        }
    }

    private static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace("\\", "/", StringComparison.Ordinal);

    private static YamlMappingNode LoadYamlMapping(string path)
    {
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

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return RequiredScalar(value, key);
    }

    private static string RequiredScalar(YamlNode node, string name) =>
        RequiredScalarValue(node, name);

    private static string RequiredScalarValue(YamlNode node, string name)
    {
        string? value = node.ShouldBeOfType<YamlScalarNode>().Value;
        value.ShouldNotBeNullOrWhiteSpace(name);
        return value!;
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

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record GateDiagnostic(string Gate, string Category, string RepositoryPath, string Message);

    private sealed record OperationIdentity(string OperationId, string Method, string Path);
}
