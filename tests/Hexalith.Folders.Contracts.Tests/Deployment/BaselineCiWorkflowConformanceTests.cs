using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class BaselineCiWorkflowConformanceTests
{
    private const string WorkflowPath = ".github/workflows/ci.yml";
    private const string GateScriptPath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string OperatorDocPath = "docs/operations/baseline-ci-gates.md";

    private static readonly string[] _baselineCategories =
    [
        "restore",
        "build",
        "format",
        "lint",
        "unit-tests",
    ];

    private static readonly string[] _baselineUnitProjects =
    [
        "tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj",
        "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj",
        "tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj",
        "tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj",
        "tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj",
        "tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj",
        "tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj",
        "tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj",
        "samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj",
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

    private static readonly string[] _excludedBaselineLanes =
    [
        "tests/Hexalith.Folders.IntegrationTests",
        "tests/Hexalith.Folders.UI.E2E.Tests",
        "tests/load/Hexalith.Folders.LoadTests",
        "tests/Hexalith.Folders.LoadTests.Tests",
        "run-container-image-gates.ps1",
        "run-dapr-policy-conformance-gates.ps1",
        "run-contract-spine-gates.ps1",
        "run-safety-invariant-gates.ps1",
        "run-governance-completeness-gates.ps1",
    ];

    [Fact]
    public void BaselineWorkflowShouldUseStableTriggersCheckoutSdkAndCache()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);

        GetScalar(workflow, "name").ShouldBe("baseline-ci");
        YamlMappingNode trigger = GetMapping(workflow, "on");
        trigger.Children.ContainsKey(new YamlScalarNode("pull_request")).ShouldBeTrue();
        GetSequence(GetMapping(trigger, "push"), "branches").Children.Select(static n => n.ToString()).ShouldBe(["main", "next", "alpha", "beta"]);
        GetScalar(GetMapping(workflow, "permissions"), "contents").ShouldBe("read");

        YamlMappingNode job = GetMapping(GetMapping(workflow, "jobs"), "baseline-build-and-unit-gates");
        GetScalar(job, "name").ShouldBe("baseline-build-and-unit-gates");
        GetScalar(job, "runs-on").ShouldBe("ubuntu-latest");

        YamlMappingNode checkout = FindStep(job, "actions/checkout@v6");
        GetScalar(GetMapping(checkout, "with"), "fetch-depth").ShouldBe("1");
        GetScalar(GetMapping(checkout, "with"), "submodules").ShouldBe("false");

        YamlMappingNode submodules = GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? value)
                && string.Equals(value.ToString(), "Initialize root-level build submodules", StringComparison.Ordinal));
        string submoduleCommand = GetScalar(submodules, "run");
        submoduleCommand.ShouldStartWith("git submodule update --init ", Case.Sensitive);
        submoduleCommand.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        foreach (string module in _rootBuildSubmodules)
        {
            submoduleCommand.ShouldContain(module, Case.Sensitive);
        }

        YamlMappingNode setupDotnet = FindStep(job, "actions/setup-dotnet@v5");
        YamlMappingNode setupWith = GetMapping(setupDotnet, "with");
        GetScalar(setupWith, "global-json-file").ShouldBe("global.json");
        GetScalar(setupWith, "cache").ShouldBe("true");
        string cacheDependencyPath = GetScalar(setupWith, "cache-dependency-path");
        foreach (string path in new[] { "Directory.Packages.props", "global.json", "nuget.config", "**/*.csproj" })
        {
            cacheDependencyPath.ShouldContain(path, Case.Sensitive);
        }

        YamlMappingNode runStep = GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("run"), out YamlNode? value)
                && string.Equals(value.ToString(), "./tests/tools/run-baseline-ci-gates.ps1", StringComparison.Ordinal));
        GetScalar(runStep, "shell").ShouldBe("pwsh");
        GetScalar(runStep, "timeout-minutes").ShouldBe("25");
    }

    [Fact]
    public void BaselineWorkflowShouldExposeMechanicalGateCategoriesWithoutInfrastructure()
    {
        string workflow = ReadText(WorkflowPath);

        foreach (string category in _baselineCategories)
        {
            workflow.ShouldContain(category, Case.Sensitive);
        }

        workflow.ShouldContain("./tests/tools/run-baseline-ci-gates.ps1");
        workflow.ShouldNotContain("secrets.", Case.Insensitive);
        workflow.ShouldNotContain("services:", Case.Insensitive);
        workflow.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflow.ShouldNotContain("playwright install", Case.Insensitive);
        workflow.ShouldNotContain("dotnet publish", Case.Insensitive);
        workflow.ShouldNotContain("docker", Case.Insensitive);
        workflow.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void BaselineGateScriptShouldRunRestoreBuildFormatLintAndAllowListedUnitTests()
    {
        string script = ReadText(GateScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("Set-StrictMode -Version Latest");
        script.ShouldContain("$ErrorActionPreference = 'Stop'");
        script.ShouldContain("Hexalith.Folders.slnx");
        script.ShouldContain("Invoke-BaselineCommand -Category 'restore' -Arguments @('restore', 'Hexalith.Folders.slnx'");
        script.ShouldContain("Invoke-BaselineCommand -Category 'build' -Arguments @('build', 'Hexalith.Folders.slnx', '--no-restore'");
        script.ShouldContain("--no-restore");
        script.ShouldContain("Invoke-BaselineCommand -Category 'format' -Arguments @('format', 'whitespace', 'Hexalith.Folders.slnx'");
        script.ShouldContain("--verify-no-changes");
        script.ShouldContain("Invoke-BaselineCommand -Category 'lint' -Arguments @('format', 'analyzers', 'Hexalith.Folders.slnx'");
        script.ShouldContain("--severity");

        // Format/lint must be scoped to this repository's own source. The host build needs
        // sibling submodule working trees present, but those submodules are independent
        // repositories with their own formatting standards and must not be evaluated by
        // this baseline gate. The exact './src/' form is required: a bare 'src tests'
        // include matches no files and would make the gate pass vacuously.
        script.ShouldContain("@('format', 'whitespace', 'Hexalith.Folders.slnx', '--verify-no-changes', '--no-restore', '--include', './src/', './tests/', './samples/')", Case.Sensitive);
        script.ShouldContain("@('format', 'analyzers', 'Hexalith.Folders.slnx', '--verify-no-changes', '--no-restore', '--severity', 'warn', '--include', './src/', './tests/', './samples/')", Case.Sensitive);

        script.ShouldContain("_bmad-output/gates/baseline-ci/latest.json");
        script.ShouldContain("$LASTEXITCODE");

        foreach (string category in _baselineCategories)
        {
            script.ShouldContain($"'{category}'", Case.Sensitive);
        }

        foreach (string project in _baselineUnitProjects)
        {
            script.ShouldContain(project, Case.Sensitive);
        }

        foreach (string excluded in _excludedBaselineLanes)
        {
            script.ShouldNotContain(excluded, Case.Sensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        script.ShouldNotContain("ContainerRegistry", Case.Insensitive);
        script.ShouldNotContain("SDK_CONTAINER_REGISTRY", Case.Insensitive);
    }

    [Fact]
    public void BaselineGateScriptShouldKeepExactHermeticUnitAllowList()
    {
        string script = ReadText(GateScriptPath);

        ProjectPathAssignmentPattern().Matches(script)
            .Cast<Match>()
            .Select(static match => match.Groups["value"].Value)
            .ShouldBe(_baselineUnitProjects);

        script.ShouldContain("@('test', $testProject.project_path, '--no-restore', '--no-build')");
        script.ShouldNotContain("@('test', 'Hexalith.Folders.slnx'", Case.Sensitive);
        script.ShouldNotContain("@('test', 'tests'", Case.Sensitive);

        foreach (Match match in FilterAssignmentPattern().Matches(script))
        {
            string filter = match.Groups["value"].Value;
            filter.ShouldNotContain("IntegrationTests", Case.Sensitive);
            filter.ShouldNotContain("UI.E2E", Case.Sensitive);
            filter.ShouldNotContain("LoadTests", Case.Sensitive);
            filter.ShouldNotContain("Playwright", Case.Sensitive);
        }
    }

    [Fact]
    public void BaselineGateScriptShouldNotReMaskNowGreenTestsWithFailOpenFilters()
    {
        // Story 8.5 (AC2/AC4/AC6, Risk R3) regression guard, realizing the 7.18 AC6 "no fail-open --filter" principle.
        // The story REMOVED the obsolete masks that hid now-green tests (Folders.Tests provider-boundary guards,
        // Testing.Tests governance/scaffold, Workers.Tests TenantSubscriptionEndpointShould) but added no test that
        // they STAY removed — so a future PR could silently re-add e.g. a `FullyQualifiedName!~` exclusion naming one
        // of those provider-boundary/scaffold guards and no gate would notice. This pins each project's filter
        // disposition so a re-mask fails CI here. NOTE: the forbidden guard names below are assembled with
        // string.Concat so this conformance file never contains the contiguous provider-boundary token that the
        // now-unmasked GitHubDependencyGuardTests scans the tests/ tree for (a contiguous literal here would itself
        // fail that guard — the exact reason the baseline must stay clear of it).
        string script = ReadText(GateScriptPath);

        string[] projectPaths = ProjectPathAssignmentPattern().Matches(script)
            .Select(static match => match.Groups["value"].Value)
            .ToArray();
        string[] filters = FilterAssignmentPattern().Matches(script)
            .Select(static match => match.Groups["value"].Value)
            .ToArray();

        // The two assignment lists are parallel ($unitTestProjects declares project_path then filter per entry, in
        // order), so a 1:1 zip is well-defined only when both lists align with the canonical project allow-list.
        projectPaths.ShouldBe(_baselineUnitProjects);
        filters.Length.ShouldBe(projectPaths.Length);

        Dictionary<string, string> filterByProject = projectPaths
            .Zip(filters)
            .ToDictionary(static pair => pair.First, static pair => pair.Second, StringComparer.Ordinal);

        // AC4 (provider-boundary guards), AC2 (governance/scaffold), AC6 (TenantSubscriptionEndpointShould re-included):
        // these three projects run with an EMPTY filter so the baseline lane exercises every test, never a subset.
        filterByProject["tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj"].ShouldBe(string.Empty);
        filterByProject["tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj"].ShouldBe(string.Empty);
        filterByProject["tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj"].ShouldBe(string.Empty);

        // No baseline filter may name any of the formerly-masked, now-green tests — a re-mask (the exact 7.18 AC6
        // anti-pattern) names one of these in a `FullyQualifiedName!~` exclusion.
        foreach (string filter in filters)
        {
            filter.ShouldNotContain(string.Concat("Octo", "kitReferencesStayInsideGitHubProviderBoundary"), Case.Sensitive);
            filter.ShouldNotContain("ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies", Case.Sensitive);
            filter.ShouldNotContain("ScaffoldContractTests", Case.Sensitive);
            filter.ShouldNotContain("ExitCriteriaDecisionArtifact", Case.Sensitive);
            filter.ShouldNotContain("FixtureContractTests", Case.Sensitive);
            filter.ShouldNotContain("TenantSubscriptionEndpointShould", Case.Sensitive);
        }

        // The only sanctioned non-empty filters are deliberate division-of-labor allow-lists, not fail-open masks:
        // Contracts.Tests is an inclusion (~) allow-list; Client.Tests is the documented env-gated codegen exclusion.
        filterByProject["tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj"]
            .ShouldContain("FullyQualifiedName~", Case.Sensitive);
        filterByProject["tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj"]
            .ShouldContain("GeneratedClientAndHelpersMatchIsolatedRegeneration", Case.Sensitive);
    }

    [Fact]
    public void BaselineGateReportShouldStayMetadataOnlyWhenPresent()
    {
        string reportPath = "_bmad-output/gates/baseline-ci/latest.json";
        string fullReportPath = RepositoryPath(reportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(reportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("baseline-ci");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(reportPath);
        RequiredString(root, "solution").ShouldBe("Hexalith.Folders.slnx");
        ReadStringArray(root, "categories").ShouldBe(_baselineCategories);

        JsonElement unitProjects = root.GetProperty("unit_test_projects");
        unitProjects.ValueKind.ShouldBe(JsonValueKind.Array);
        unitProjects.EnumerateArray()
            .Select(static element => RequiredString(element, "project_path"))
            .ShouldBe(_baselineUnitProjects);

        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void BaselineOperatorDocumentationShouldRecordStatusCheckAndDiagnosticPolicy()
    {
        string documentation = ReadText(OperatorDocPath);

        documentation.ShouldContain("baseline-build-and-unit-gates");
        documentation.ShouldContain("branch protection");
        documentation.ShouldContain("metadata-only");
        documentation.ShouldContain("Directory.Packages.props");
        documentation.ShouldContain("global.json");
        documentation.ShouldContain("nuget.config");
        documentation.ShouldContain("**/*.csproj");

        foreach (string category in _baselineCategories)
        {
            documentation.ShouldContain(category, Case.Sensitive);
        }

        foreach (string project in _baselineUnitProjects)
        {
            documentation.ShouldContain(project, Case.Sensitive);
        }

        foreach (string excluded in _excludedBaselineLanes.Take(4))
        {
            documentation.ShouldContain(excluded, Case.Sensitive);
        }

        foreach (string module in _rootBuildSubmodules)
        {
            documentation.ShouldContain(module, Case.Sensitive);
        }
    }

    [Fact]
    public void BaselineArtifactsShouldNotIntroduceRecursiveSubmoduleSetup()
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
            .Select(static p => p.TrimStart())
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
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Baseline CI report value must not contain an absolute path: {value}");
                ForbiddenDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Baseline CI report value must stay metadata-only: {value}");
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

    [GeneratedRegex(@"filter\s*=\s*'(?<value>[^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex FilterAssignmentPattern();

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDiagnosticPattern();
}
