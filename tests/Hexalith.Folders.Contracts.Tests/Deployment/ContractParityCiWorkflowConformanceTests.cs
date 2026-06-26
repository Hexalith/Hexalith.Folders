using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ContractParityCiWorkflowConformanceTests
{
    private const string WorkflowPath = ".github/workflows/ci.yml";
    private const string GateScriptPath = "tests/tools/run-contract-parity-ci-gates.ps1";
    private const string OperatorDocPath = "docs/contract/contract-parity-ci-gates.md";
    private const string ReportPath = "_bmad-output/gates/contract-parity-ci/latest.json";

    private static readonly string[] _categories =
    [
        "server-vs-spine",
        "previous-spine",
        "generated-client",
        "idempotency-helpers",
        "parity-oracle-schema",
        "parity-oracle-determinism",
        "sdk-transport-parity",
        "rest-sdk-golden-parity",
        "cli-behavioral-parity",
        "mcp-behavioral-parity",
        "mixed-surface-handoff",
    ];

    private static readonly string[] _testProjects =
    [
        "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj",
        "tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj",
        "tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj",
        "tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj",
        "tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj",
    ];

    private static readonly string[] _rootBuildSubmodules =
    [
        "references/Hexalith.AI.Tools",
        "references/Hexalith.Builds",
        "references/Hexalith.Commons",
        "references/Hexalith.EventStore",
        "references/Hexalith.FrontComposer",
        "references/Hexalith.Memories",
        "references/Hexalith.Tenants",
    ];

    private static readonly string[] _excludedLanes =
    [
        "run-safety-invariant-gates.ps1",
        "run-governance-completeness-gates.ps1",
        "run-dapr-policy-conformance-gates.ps1",
        "run-container-image-gates.ps1",
        "capacity",
        "scheduled drift",
        "semantic-release",
        "dotnet publish",
        "upload-artifact",
    ];

    [Fact]
    public void ContractParityWorkflowShouldExposeStableBlockingJobWithBaselineSetupPosture()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);

        GetScalar(workflow, "name").ShouldBe("baseline-ci");
        GetScalar(GetMapping(workflow, "permissions"), "contents").ShouldBe("read");

        YamlMappingNode jobs = GetMapping(workflow, "jobs");
        YamlMappingNode baselineJob = GetMapping(jobs, "baseline-build-and-unit-gates");
        YamlMappingNode contractJob = GetMapping(jobs, "contract-and-parity-gates");

        GetScalar(baselineJob, "name").ShouldBe("baseline-build-and-unit-gates");
        GetScalar(contractJob, "name").ShouldBe("contract-and-parity-gates");
        GetScalar(contractJob, "runs-on").ShouldBe("ubuntu-latest");

        YamlMappingNode checkout = FindStep(contractJob, "actions/checkout@v6");
        GetScalar(GetMapping(checkout, "with"), "fetch-depth").ShouldBe("1");
        GetScalar(GetMapping(checkout, "with"), "submodules").ShouldBe("false");

        YamlMappingNode submodules = GetSequence(contractJob, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? value)
                && string.Equals(value.ToString(), "Initialize root-level build submodules", StringComparison.Ordinal));
        string submoduleCommand = GetScalar(submodules, "run");
        submoduleCommand.ShouldStartWith("git submodule update --init ", Case.Sensitive);
        submoduleCommand.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        foreach (string module in _rootBuildSubmodules)
        {
            submoduleCommand.ShouldContain(module, Case.Sensitive);
        }

        YamlMappingNode setupDotnet = FindStep(contractJob, "actions/setup-dotnet@v5");
        YamlMappingNode setupWith = GetMapping(setupDotnet, "with");
        GetScalar(setupWith, "global-json-file").ShouldBe("global.json");
        GetScalar(setupWith, "cache").ShouldBe("true");
        string cacheDependencyPath = GetScalar(setupWith, "cache-dependency-path");
        foreach (string path in new[] { "Directory.Packages.props", "global.json", "nuget.config", "**/*.csproj" })
        {
            cacheDependencyPath.ShouldContain(path, Case.Sensitive);
        }

        string workflowText = ReadText(WorkflowPath);
        workflowText.ShouldContain("./tests/tools/run-contract-parity-ci-gates.ps1");
        workflowText.ShouldNotContain("secrets.", Case.Insensitive);
        workflowText.ShouldNotContain("services:", Case.Insensitive);
        workflowText.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflowText.ShouldNotContain("playwright install", Case.Insensitive);
        workflowText.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void ContractParityGateScriptShouldRunOnlyExactContractAndParityAllowList()
    {
        string script = ReadText(GateScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("Set-StrictMode -Version Latest");
        script.ShouldContain("$ErrorActionPreference = 'Stop'");
        script.ShouldContain(ReportPath);
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain("@('test', $gate.project_path, '--no-restore', '--no-build', '--filter', $gate.filter)", Case.Sensitive);
        script.ShouldNotContain("@('test', 'Hexalith.Folders.slnx'", Case.Sensitive);
        script.ShouldNotContain("@('test', 'tests'", Case.Sensitive);
        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);

        foreach (string category in _categories)
        {
            script.ShouldContain($"category = '{category}'", Case.Sensitive);
        }

        ProjectPathAssignmentPattern().Matches(script)
            .Cast<Match>()
            .Select(static match => match.Groups["value"].Value)
            .Distinct(StringComparer.Ordinal)
            .ShouldBe(_testProjects);

        string[] requiredFilterFragments =
        [
            "ContractSpineCiGateTests",
            "ContractSpineFoundationTests",
            "ParityOracleGeneratorTests",
            "ClientGenerationTests",
            "TransportParityConformanceTests",
            "ArchiveFolderClientConformanceTests",
            "LifecycleStatusClientConformanceTests",
            "Hexalith.Folders.Cli.Tests.ParityOracleConformanceTests",
            "Hexalith.Folders.Cli.Tests.BehavioralParityTests",
            "Hexalith.Folders.Mcp.Tests.ParityOracleConformanceTests",
            "PreSdkFailureTests",
            "PostSdkMappingTests",
            "SourcingTests",
            "EndToEnd.GoldenLifecycleParityTests",
            "AdapterParity.CrossAdapterBehavioralParityTests",
            "MixedSurfaceHandoff.MixedSurfaceHandoffTests",
        ];

        foreach (string fragment in requiredFilterFragments)
        {
            script.ShouldContain(fragment, Case.Sensitive);
        }

        foreach (string excluded in _excludedLanes)
        {
            script.ShouldNotContain(excluded, Case.Insensitive);
        }
    }

    [Fact]
    public void ContractParityGateReportShouldStayMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("contract-parity-ci");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        ReadStringArray(root, "categories").ShouldBe(_categories);

        JsonElement gates = root.GetProperty("test_gates");
        gates.ValueKind.ShouldBe(JsonValueKind.Array);
        gates.EnumerateArray()
            .Select(static element => RequiredString(element, "project_path"))
            .Distinct(StringComparer.Ordinal)
            .ShouldBe(_testProjects);

        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void ContractParityOperatorDocumentationShouldRecordHandoffAndTransitionalOwnership()
    {
        string documentation = ReadText(OperatorDocPath);

        documentation.ShouldContain("contract-and-parity-gates");
        documentation.ShouldContain("branch protection");
        documentation.ShouldContain(ReportPath);
        documentation.ShouldContain("metadata-only");
        documentation.ShouldContain("contract-spine.yml");
        documentation.ShouldContain("Stories 7.6 and 7.8");
        documentation.ShouldContain("git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants");

        foreach (string category in _categories)
        {
            documentation.ShouldContain(category, Case.Sensitive);
        }

        foreach (string project in _testProjects)
        {
            documentation.ShouldContain(project, Case.Sensitive);
        }

        foreach (string excluded in _excludedLanes.Take(4))
        {
            documentation.ShouldContain(excluded, Case.Sensitive);
        }
    }

    [Fact]
    public void ContractParityArtifactsShouldNotIntroduceRecursiveSubmoduleSetup()
    {
        Regex recursiveSubmodule = RecursiveSubmodulePattern();
        foreach (string path in EnumerateScannedFiles())
        {
            recursiveSubmodule.IsMatch(ReadText(path)).ShouldBeFalse($"{path} must not initialize nested submodules recursively.");
        }
    }

    private static YamlMappingNode FindStep(YamlMappingNode job, string uses)
        => GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("uses"), out YamlNode? value)
                && string.Equals(value.ToString(), uses, StringComparison.Ordinal));

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string[] EnumerateScannedFiles()
        => new[] { ".github", "tests/tools", "docs", "deploy", "src" }
            .Select(RepositoryPath)
            .Where(Directory.Exists)
            .SelectMany(static path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}TestResults{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(ToRepositoryPath)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.Array, $"JSON property '{propertyName}' must be an array.");
        return property.EnumerateArray().Select(static item => item.GetString().ShouldNotBeNull()).ToArray();
    }

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }

                break;

            case JsonValueKind.String:
                string value = element.GetString().ShouldNotBeNull();
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Contract parity CI report value must not contain an absolute path: {value}");
                ForbiddenDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Contract parity CI report value must stay metadata-only: {value}");
                break;
        }
    }

    private static string RepositoryPath(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory, "Hexalith.Folders.slnx")))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private static string ToRepositoryPath(string fullPath)
    {
        string root = RepositoryPath(".");
        return Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string GetScalar(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode GetSequence(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    [GeneratedRegex(@"git\s+submodule\s+update\s+--init\s+(?:[^\r\n]*\s)?--recursive|--recursive", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecursiveSubmodulePattern();

    [GeneratedRegex(@"project_path\s*=\s*'(?<value>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectPathAssignmentPattern();

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump|local absolute path", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDiagnosticPattern();
}
