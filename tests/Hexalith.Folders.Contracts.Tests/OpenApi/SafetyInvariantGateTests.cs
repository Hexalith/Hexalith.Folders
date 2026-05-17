using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class SafetyInvariantGateTests
{
    private const string GateName = "safety-invariant";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CorpusPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "audit-leakage-corpus.json");
    private static readonly string InventoryPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "safety-channel-inventory.json");
    private static readonly string QuarantinePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "quarantine", "safety-negative-controls.json");
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string WorkflowPath = Path.Combine(RepositoryRoot, ".github", "workflows", "contract-spine.yml");
    private static readonly string GateScriptPath = Path.Combine(RepositoryRoot, "tests", "tools", "run-safety-invariant-gates.ps1");
    private static readonly string GateDocumentationPath = Path.Combine(RepositoryRoot, "docs", "contract", "safety-invariant-ci-gates.md");

    private static readonly string[] AllowedClassifications =
    [
        "synthetic-sentinel",
        "tenant-sensitive",
        "confidential",
        "metadata-placeholder",
        "safe-provenance",
        "unauthorized-resource-hint",
        "generated-context-sensitive",
    ];

    private static readonly string[] AllowedSurfaces =
    [
        "logs",
        "traces",
        "metric-labels",
        "telemetry-attributes",
        "events",
        "audit-records",
        "projections",
        "provider-diagnostics",
        "console-payloads",
        "generated-sdk",
        "parity-artifacts",
        "openapi-examples",
        "problem-details-examples",
        "developer-diagnostics",
        "ci-logs",
        "assertion-messages",
    ];

    [Fact]
    public void SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(CorpusPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "schema_version").ShouldBe("1.0.0");
        RequiredBoolean(RequiredObject(root, "ownership"), "synthetic_data_only").ShouldBeTrue();
        RequiredArray(root, "classification_vocabulary").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(AllowedClassifications.Order(StringComparer.Ordinal));
        RequiredArray(root, "forbidden_output_surfaces").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(AllowedSurfaces.Order(StringComparer.Ordinal));

        JsonElement samples = RequiredArray(root, "sentinel_samples");
        samples.GetArrayLength().ShouldBeGreaterThanOrEqualTo(14);
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement sample in samples.EnumerateArray())
        {
            string id = RequiredString(sample, "id");
            ids.Add(id).ShouldBeTrue($"Duplicate sentinel sample id '{id}'.");
            RequiredBoolean(sample, "synthetic_sentinel").ShouldBeTrue(id);
            RequiredBoolean(sample, "synthetic_data_only").ShouldBeTrue(id);
            RequiredString(sample, "value").ShouldNotBeNullOrWhiteSpace(id);
            RequiredString(sample, "safe_notes").ShouldContain("synthetic", Case.Insensitive, id);
            AllowedClassifications.ShouldContain(RequiredString(sample, "classification"), id);
            RequiredString(sample, "category").ShouldNotBeNullOrWhiteSpace(id);
            RequiredArray(sample, "forbidden_output_surfaces").SelectText().ShouldNotBeEmpty(id);
            RequiredArray(sample, "allowed_provenance_representations").SelectText().ShouldNotBeEmpty(id);
            RequiredArray(sample, "participates_in").SelectText().ShouldContain("positive", id);
        }
    }

    [Fact]
    public void SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined()
    {
        string corpus = ReadRequiredFile(CorpusPath);
        AssertMetadataOnly(corpus);

        using JsonDocument quarantine = JsonDocument.Parse(ReadRequiredFile(QuarantinePath));
        RequiredBoolean(RequiredObject(quarantine.RootElement, "ownership"), "quarantined_negative_controls").ShouldBeTrue();

        foreach (JsonElement control in RequiredArray(quarantine.RootElement, "negative_controls").EnumerateArray())
        {
            RequiredString(control, "id").ShouldStartWith("negative-control-");
            RequiredBoolean(control, "synthetic_data_only").ShouldBeTrue();
            RequiredBoolean(control, "normative_example").ShouldBeFalse();
            RequiredString(control, "contaminated_payload").ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ChannelInventoryResolvesCoveredSourcesAndBoundsMissingChannels()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
        JsonElement root = document.RootElement;

        string[] includeRoots = RequiredArray(root, "include_roots").SelectText();
        includeRoots.ShouldNotBeEmpty();
        foreach (string includeRoot in includeRoots)
        {
            AssertRepositoryRelativePath(includeRoot, "include_roots");
            PathExists(includeRoot).ShouldBeTrue($"include_roots entry '{includeRoot}' does not resolve to a repository artifact.");
        }

        string[] exclusions = RequiredArray(root, "structured_exclusions").SelectText();
        exclusions.ShouldContain("tests/fixtures/quarantine/**");
        exclusions.ShouldContain(".git/**");
        foreach (string mandatoryExclusion in new[] { "**/bin/**", "**/obj/**" })
        {
            exclusions.ShouldContain(mandatoryExclusion);
        }

        foreach (JsonElement channel in RequiredArray(root, "channels").EnumerateArray())
        {
            string name = RequiredString(channel, "channel");
            string status = RequiredString(channel, "prerequisite_status");
            string owner = RequiredString(channel, "owning_story");
            string diagnostic = RequiredString(channel, "diagnostic");
            string safeAbsence = RequiredString(channel, "safe_absence_diagnostic");

            owner.ShouldNotBeNullOrWhiteSpace(name);
            owner.ShouldNotContain("-x-", Case.Sensitive, $"{name} owning_story must not use wildcard placeholders like '2-x-...' (AC 15).");
            owner.ShouldNotEndWith("-x", $"{name} owning_story must be a concrete story ID, not a wildcard (AC 15).");
            diagnostic.ShouldBeOneOf("covered", "SAFETY-CHANNEL-MISSING", "SAFETY-PREREQUISITE-DRIFT");
            safeAbsence.ShouldBeOneOf("covered", "SAFETY-CHANNEL-MISSING", "SAFETY-PREREQUISITE-DRIFT");
            AssertMetadataOnly(diagnostic);
            AssertMetadataOnly(safeAbsence);

            JsonElement sources = RequiredArray(channel, "artifact_sources");
            if (status == "covered")
            {
                sources.GetArrayLength().ShouldBeGreaterThan(0, $"{name} claims coverage but declares no source.");
                foreach (string source in sources.SelectText())
                {
                    AssertRepositoryRelativePath(source, name);
                    PathExists(source).ShouldBeTrue($"{name} points to stale source '{source}'.");
                }
            }
            else
            {
                status.ShouldBeOneOf("reference-pending", "prerequisite-drift");
                sources.GetArrayLength().ShouldBe(0, $"{name} is {status} and must not claim artifact coverage.");
                safeAbsence.ShouldNotBe("covered", $"{name} is {status}; safe_absence_diagnostic must not be 'covered'.");
            }
        }
    }

    [Fact]
    public void SafetyScansDetectQuarantinedControlsWithoutScanningQuarantineAsNormalArtifacts()
    {
        SentinelSample[] samples = LoadSentinelSamples();
        SafetyScanDiagnostic[] negativeControlFindings = ScanNegativeControls(samples);

        negativeControlFindings.ShouldNotBeEmpty();
        foreach (SafetyScanDiagnostic diagnostic in negativeControlFindings)
        {
            diagnostic.Gate.ShouldBe(GateName);
            diagnostic.RuleId.ShouldBe("SAFETY-FORBIDDEN-VALUE");
            diagnostic.RepositoryPath.ShouldBe("tests/fixtures/quarantine/safety-negative-controls.json");
            diagnostic.ToString().ShouldNotContain(samples.Single(s => s.Id == diagnostic.SampleId).Value, Case.Sensitive);
            AssertMetadataOnly(diagnostic.ToString());
        }

        SafetyScanDiagnostic[] normalFindings = ScanManifestCoveredArtifacts(samples);
        normalFindings.ShouldBeEmpty(string.Join(Environment.NewLine, normalFindings.Select(d => d.ToString())));
    }

    [Fact]
    public void OpenApiExamplesAndContextQueriesRemainMetadataOnly()
    {
        string openApi = ReadRequiredFile(OpenApiPath);
        foreach (SentinelSample sample in LoadSentinelSamples().Where(sample => sample.ForbiddenOutputSurfaces.Contains("openapi-examples", StringComparer.Ordinal) || sample.ForbiddenOutputSurfaces.Contains("problem-details-examples", StringComparer.Ordinal)))
        {
            openApi.ShouldNotContain(sample.Value, Case.Sensitive, sample.Id);
        }

        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        foreach (Operation operation in EnumerateOperations(root).Where(o => o.OperationId is "SearchFolderFiles" or "GlobFolderFiles" or "ReadFileRange" or "GetFolderFileMetadata"))
        {
            YamlSequenceNode order = RequiredSequence(RequiredMapping(operation.Node, "x-hexalith-authorization"), "order");
            string[] orderSteps = order.Children
                .Select(node => RequiredScalar(node, "authorization order"))
                .ToArray();
            orderSteps.Length.ShouldBeGreaterThanOrEqualTo(4, $"{operation.OperationId}: AC 9 requires tenant_access, folder_acl, path_policy at the front and query_execution at the end.");
            orderSteps.Take(3).ToArray().ShouldBe(["tenant_access", "folder_acl", "path_policy"], $"{operation.OperationId}: AC 9 authorization-before-observation prefix.");
            orderSteps[^1].ShouldBe("query_execution", $"{operation.OperationId}: AC 9 requires execution to be the final authorization step.");

            string serialized = SerializeYaml(operation.Node);
            serialized.ShouldContain("authorization", Case.Insensitive, operation.OperationId);
            serialized.ShouldNotContain("search-first", Case.Insensitive, operation.OperationId);
            serialized.ShouldNotContain("filter-later", Case.Insensitive, operation.OperationId);
        }
    }

    [Fact]
    public void WorkflowAndDocumentationExposeSameOfflineSafetyGate()
    {
        string workflow = ReadRequiredFile(WorkflowPath);
        string script = ReadRequiredFile(GateScriptPath);
        string documentation = ReadRequiredFile(GateDocumentationPath);

        workflow.ShouldContain("./tests/tools/run-safety-invariant-gates.ps1 -NoRestore");
        workflow.ShouldContain("dotnet restore Hexalith.Folders.slnx");
        workflow.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore");
        workflow.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj");
        script.ShouldContain("FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldNotContain("--recursive", Case.Insensitive);

        documentation.ShouldContain(".\\tests\\tools\\run-safety-invariant-gates.ps1 -NoRestore");
        documentation.ShouldContain("SAFETY-PREREQUISITE-DRIFT");
        documentation.ShouldContain("reference-pending");
        documentation.ShouldContain("Story 1.16");
        AssertMetadataOnly(documentation);
    }

    private static SafetyScanDiagnostic[] ScanManifestCoveredArtifacts(IReadOnlyList<SentinelSample> samples)
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
        List<SafetyScanDiagnostic> diagnostics = [];

        foreach (JsonElement channel in RequiredArray(inventory.RootElement, "channels").EnumerateArray())
        {
            if (RequiredString(channel, "prerequisite_status") != "covered" || !RequiredBoolean(channel, "scan_forbidden_values"))
            {
                continue;
            }

            string channelName = RequiredString(channel, "channel");
            foreach (string source in RequiredArray(channel, "artifact_sources").SelectText())
            {
                foreach (string file in EnumerateSourceFiles(source))
                {
                    if (IsExcludedByInventory(file))
                    {
                        continue;
                    }

                    string text = File.ReadAllText(Path.Combine(RepositoryRoot, NormalizeForFileSystem(file)));
                    diagnostics.AddRange(ScanText(file, channelName, text, samples));
                }
            }
        }

        return diagnostics.ToArray();
    }

    private static SafetyScanDiagnostic[] ScanNegativeControls(IReadOnlyList<SentinelSample> samples)
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(QuarantinePath));
        List<SafetyScanDiagnostic> diagnostics = [];
        foreach (JsonElement control in RequiredArray(document.RootElement, "negative_controls").EnumerateArray())
        {
            string payload = RequiredString(control, "contaminated_payload");
            string channel = RequiredString(control, "output_channel");
            diagnostics.AddRange(ScanText("tests/fixtures/quarantine/safety-negative-controls.json", channel, payload, samples));
        }

        return diagnostics.ToArray();
    }

    private static IEnumerable<SafetyScanDiagnostic> ScanText(string repositoryPath, string channel, string text, IEnumerable<SentinelSample> samples)
    {
        foreach (SentinelSample sample in samples)
        {
            if (!sample.ForbiddenOutputSurfaces.Contains(channel, StringComparer.Ordinal))
            {
                continue;
            }

            if (text.Contains(sample.Value, StringComparison.OrdinalIgnoreCase))
            {
                yield return new(GateName, "SAFETY-FORBIDDEN-VALUE", repositoryPath, channel, sample.Id, sample.Classification, sample.Category, "Replace the raw value with an allowed provenance-safe representation.");
            }
        }
    }

    private static SentinelSample[] LoadSentinelSamples()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(CorpusPath));
        return RequiredArray(document.RootElement, "sentinel_samples")
            .EnumerateArray()
            .Select(sample => new SentinelSample(
                RequiredString(sample, "id"),
                RequiredString(sample, "value"),
                RequiredString(sample, "classification"),
                RequiredString(sample, "category"),
                RequiredArray(sample, "forbidden_output_surfaces").SelectText().ToArray()))
            .ToArray();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string repositoryPath)
    {
        string absolute = Path.Combine(RepositoryRoot, NormalizeForFileSystem(repositoryPath));
        if (File.Exists(absolute))
        {
            yield return repositoryPath.Replace("\\", "/", StringComparison.Ordinal);
            yield break;
        }

        if (!Directory.Exists(absolute))
        {
            yield break;
        }

        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
        };

        foreach (string file in Directory.EnumerateFiles(absolute, "*", enumerationOptions))
        {
            string relative = ToRepositoryPath(file);
            if (!IsExcludedByInventory(relative) && !IsBinaryFile(relative))
            {
                yield return relative;
            }
        }
    }

    private static bool IsExcludedByInventory(string repositoryPath)
    {
        string normalized = repositoryPath.Replace("\\", "/", StringComparison.Ordinal);
        string[] excludedPrefixes =
        [
            ".git/",
            "bin/",
            "obj/",
            "node_modules/",
            "tests/fixtures/quarantine/",
        ];

        string[] excludedSegments =
        [
            "/bin/",
            "/obj/",
            "/node_modules/",
        ];

        return excludedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal))
            || excludedSegments.Any(segment => normalized.Contains(segment, StringComparison.Ordinal));
    }

    private static bool IsBinaryFile(string repositoryPath)
    {
        string extension = Path.GetExtension(repositoryPath).ToLowerInvariant();
        return extension is ".dll" or ".exe" or ".pdb" or ".nupkg" or ".snupkg" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".zip"
            or ".so" or ".dylib" or ".lib" or ".bin" or ".tar" or ".gz" or ".7z" or ".rar"
            or ".ico" or ".bmp" or ".tiff" or ".mp4" or ".mov" or ".pdf" or ".docx" or ".xlsx" or ".pptx"
            or ".binlog";
    }

    private static bool PathExists(string repositoryPath)
    {
        string absolute = Path.Combine(RepositoryRoot, NormalizeForFileSystem(repositoryPath));
        return File.Exists(absolute) || Directory.Exists(absolute);
    }

    private static void AssertRepositoryRelativePath(string repositoryPath, string because)
    {
        repositoryPath.ShouldNotBeNullOrWhiteSpace(because);
        Path.IsPathFullyQualified(repositoryPath).ShouldBeFalse(because);
        repositoryPath.ShouldNotContain("\\", Case.Sensitive, because);
        repositoryPath.StartsWith("../", StringComparison.Ordinal).ShouldBeFalse(because);
        repositoryPath.Split('/').ShouldNotContain("..", because);
    }

    private static void AssertMetadataOnly(string value)
    {
        string[] forbidden =
        [
            "diff --git",
            "-----BEGIN",
            "DefaultEndpointsProtocol=",
            "AccountKey=",
            "client_secret=",
            "clientSecret",
            "password=",
            "pwd=",
            "passwd=",
            "api_key=",
            "apikey=",
            "https://github.com/",
            "https://api.github.com",
            "https://prod.",
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

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static IEnumerable<Operation> EnumerateOperations(YamlMappingNode root)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = RequiredScalar(methodEntry.Key, "method").ToLowerInvariant();
                if (method is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                YamlMappingNode operation = methodEntry.Value.ShouldBeOfType<YamlMappingNode>();
                yield return new Operation(RequiredScalar(operation, "operationId"), operation);
            }
        }
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

    private static JsonElement RequiredObject(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        value.ValueKind.ShouldBe(JsonValueKind.Object, property);
        return value;
    }

    private static JsonElement RequiredArray(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        value.ValueKind.ShouldBe(JsonValueKind.Array, property);
        return value;
    }

    private static string RequiredString(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        value.ValueKind.ShouldBe(JsonValueKind.String, property);
        return value.GetString() ?? string.Empty;
    }

    private static bool RequiredBoolean(JsonElement element, string property)
    {
        element.TryGetProperty(property, out JsonElement value).ShouldBeTrue(property);
        (value.ValueKind is JsonValueKind.True or JsonValueKind.False).ShouldBeTrue(property);
        return value.GetBoolean();
    }

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return RequiredScalar(value, key);
    }

    private static string RequiredScalar(YamlNode node, string name)
    {
        string? value = node.ShouldBeOfType<YamlScalarNode>().Value;
        value.ShouldNotBeNullOrWhiteSpace(name);
        return value!;
    }

    private static string SerializeYaml(YamlNode node)
    {
        YamlStream stream = new(new YamlDocument(node));
        using StringWriter writer = new();
        stream.Save(writer, false);
        return writer.ToString();
    }

    private static string ReadRequiredFile(string path)
    {
        File.Exists(path).ShouldBeTrue(ToRepositoryPath(path));
        return File.ReadAllText(path);
    }

    private static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace("\\", "/", StringComparison.Ordinal);

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static string FindRepositoryRoot()
    {
        string? githubWorkspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrEmpty(githubWorkspace)
            && File.Exists(Path.Combine(githubWorkspace, "Hexalith.Folders.slnx")))
        {
            return githubWorkspace;
        }

        foreach (string seed in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? current = new(seed);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("SAFETY-PREREQUISITE-DRIFT: Could not locate Hexalith.Folders.slnx from AppContext.BaseDirectory, current directory, or GITHUB_WORKSPACE.");
    }

    private sealed record Operation(string OperationId, YamlMappingNode Node);

    private sealed record SentinelSample(string Id, string Value, string Classification, string Category, string[] ForbiddenOutputSurfaces);

    private sealed record SafetyScanDiagnostic(string Gate, string RuleId, string RepositoryPath, string OutputChannel, string SampleId, string Classification, string Category, string Remediation)
    {
        public override string ToString()
        {
            string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RepositoryPath))).ToLowerInvariant()[..12];
            return $"{Gate}:{RuleId}: path={RepositoryPath}; path_hash={pathHash}; channel={OutputChannel}; sample_id={SampleId}; classification={Classification}; category={Category}; remediation={Remediation}";
        }
    }
}

internal static class JsonElementSafetyExtensions
{
    public static string[] SelectText(this JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
}
