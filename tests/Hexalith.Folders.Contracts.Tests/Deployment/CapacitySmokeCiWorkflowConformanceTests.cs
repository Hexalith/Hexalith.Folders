using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class CapacitySmokeCiWorkflowConformanceTests
{
    private const string WorkflowPath = ".github/workflows/ci.yml";
    private const string GateScriptPath = "tests/tools/run-capacity-smoke-ci-gates.ps1";
    private const string OperatorDocPath = "docs/operations/capacity-smoke-ci-gates.md";
    private const string ReportPath = "_bmad-output/gates/capacity-smoke-ci/latest.json";
    private const string LoadProjectPath = "tests/load/Hexalith.Folders.LoadTests.csproj";
    private const string EvidencePath = "_bmad-output/gates/capacity-smoke-ci/reports/lifecycle-capacity-evidence.json";

    private static readonly string[] _categories =
    [
        "harness-self-check",
        "quick-lifecycle-smoke",
        "evidence-shape",
        "non-production-thresholds",
        "metadata-only-report",
    ];

    private static readonly string[] _requiredSteps =
    [
        "prepare_workspace",
        "acquire_workspace_lock",
        "mutate_workspace_file",
        "commit_workspace",
        "read_workspace_status",
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
        "run-baseline-ci-gates.ps1",
        "run-contract-parity-ci-gates.ps1",
        "run-security-redaction-ci-gates.ps1",
        "run-safety-invariant-gates.ps1",
        "run-governance-completeness-gates.ps1",
        "run-dapr-policy-conformance-gates.ps1",
        "run-container-image-gates.ps1",
        "upload-artifact",
        "dotnet publish",
        "semantic-release",
        "playwright install",
        "services:",
        "secrets.",
        "scheduled drift",
    ];

    [Fact]
    public void CapacitySmokeWorkflowShouldExposeStableBlockingJobWithBaselineSetupPosture()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);

        GetScalar(workflow, "name").ShouldBe("baseline-ci");
        GetScalar(GetMapping(workflow, "permissions"), "contents").ShouldBe("read");

        YamlMappingNode jobs = GetMapping(workflow, "jobs");
        GetMapping(jobs, "baseline-build-and-unit-gates");
        GetMapping(jobs, "contract-and-parity-gates");
        GetMapping(jobs, "security-and-redaction-gates");
        YamlMappingNode capacityJob = GetMapping(jobs, "capacity-smoke-gates");

        GetScalar(capacityJob, "name").ShouldBe("capacity-smoke-gates");
        GetScalar(capacityJob, "runs-on").ShouldBe("ubuntu-latest");

        YamlMappingNode checkout = FindStep(capacityJob, "actions/checkout@v6");
        GetScalar(GetMapping(checkout, "with"), "fetch-depth").ShouldBe("1");
        GetScalar(GetMapping(checkout, "with"), "submodules").ShouldBe("false");

        YamlMappingNode submodules = GetSequence(capacityJob, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? value)
                && string.Equals(value.ToString(), "Initialize root-level build submodules", StringComparison.Ordinal));
        string submoduleCommand = GetScalar(submodules, "run");
        submoduleCommand.ShouldStartWith("git submodule update --init ", Case.Sensitive);
        submoduleCommand.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        foreach (string module in _rootBuildSubmodules)
        {
            submoduleCommand.ShouldContain(module, Case.Sensitive);
        }

        YamlMappingNode setupDotnet = FindStep(capacityJob, "actions/setup-dotnet@v5");
        YamlMappingNode setupWith = GetMapping(setupDotnet, "with");
        GetScalar(setupWith, "global-json-file").ShouldBe("global.json");
        GetScalar(setupWith, "cache").ShouldBe("true");
        string cacheDependencyPath = GetScalar(setupWith, "cache-dependency-path");
        foreach (string path in new[] { "Directory.Packages.props", "global.json", "nuget.config", "**/*.csproj" })
        {
            cacheDependencyPath.ShouldContain(path, Case.Sensitive);
        }

        string workflowText = ReadText(WorkflowPath);
        workflowText.ShouldContain("./tests/tools/run-capacity-smoke-ci-gates.ps1");
        workflowText.ShouldContain("dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false");
        workflowText.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore -m:1");
        workflowText.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        workflowText.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflowText.ShouldNotContain("playwright install", Case.Insensitive);
    }

    [Fact]
    public void CapacitySmokeGateScriptShouldRunOnlyTheLoadHarnessAndFailClosed()
    {
        string script = ReadText(GateScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("Set-StrictMode -Version Latest");
        script.ShouldContain("$ErrorActionPreference = 'Stop'");
        script.ShouldContain(ReportPath);
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain(LoadProjectPath);
        script.ShouldContain("'run', '--no-build', '--project', $loadProjectPath");
        script.ShouldContain("--self-check");
        script.ShouldContain("--profile', 'quick'");
        script.ShouldContain("--run-id', 'capacity-smoke-ci'");
        script.ShouldContain(EvidencePath);
        script.ShouldContain("thresholds -ne 'reference_pending'");
        script.ShouldContain("zero-or-partial-step-execution");
        script.ShouldContain("missing-load-harness-assembly");
        script.ShouldContain("malformed-evidence-json");

        foreach (string category in _categories)
        {
            script.ShouldContain($"'{category}'", Case.Sensitive);
        }

        foreach (string step in _requiredSteps)
        {
            script.ShouldContain($"'{step}'", Case.Sensitive);
        }

        foreach (string excluded in _excludedLanes)
        {
            script.ShouldNotContain(excluded, Case.Insensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void CapacitySmokeGateScriptShouldValidateCriticalEvidenceFailureCases()
    {
        string script = ReadText(GateScriptPath);

        script.ShouldContain("function Assert-EvidenceShape", Case.Sensitive);
        script.ShouldContain("foreach ($step in $requiredSteps)", Case.Sensitive);
        script.ShouldContain("@($Evidence.measured_steps) -notcontains $step", Case.Sensitive);
        script.ShouldContain("$Evidence.observed_step_counts.$step", Case.Sensitive);
        script.ShouldContain("$count -lt 1", Case.Sensitive);
        script.ShouldContain("zero-or-partial-step-execution", Case.Sensitive);
        script.ShouldContain("missing-lifecycle-accepted-result-codes", Case.Sensitive);
        script.ShouldContain("missing-status-allowed-result-code", Case.Sensitive);
        script.ShouldContain("function Assert-NonProductionThresholds", Case.Sensitive);
        script.ShouldContain("$Evidence.thresholds -ne 'reference_pending'", Case.Sensitive);

        foreach (string forbidden in new[] { "p95", "throughput", "concurrent tenant", "c1 target", "c2 target", "c5 target", "target hardware" })
        {
            script.ShouldContain($"'{forbidden}'", Case.Sensitive);
        }
    }

    [Fact]
    public void CapacitySmokeReportShouldStayMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("capacity-smoke-ci");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        RequiredString(root, "profile_name").ShouldBe("quick");
        RequiredString(root, "threshold_posture").ShouldBe("reference_pending");
        RequiredString(root, "evidence_path").ShouldBe(EvidencePath);
        ReadStringArray(root, "categories").ShouldBe(_categories);
        ReadStringArray(root, "required_measured_steps").ShouldBe(_requiredSteps);

        if (root.TryGetProperty("measured_steps", out JsonElement measuredSteps))
        {
            measuredSteps.EnumerateArray()
                .Select(static step => step.GetString().ShouldNotBeNull())
                .ToArray()
                .ShouldBe(_requiredSteps, ignoreOrder: true);
        }

        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void CapacitySmokeEvidenceShouldStayMetadataOnlyWhenPresent()
    {
        string fullEvidencePath = RepositoryPath(EvidencePath);
        if (!File.Exists(fullEvidencePath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(EvidencePath));
        JsonElement root = document.RootElement;

        RequiredString(root, "run_id").ShouldBe("capacity-smoke-ci");
        RequiredString(root, "profile_name").ShouldBe("quick");
        RequiredString(root, "thresholds").ShouldBe("reference_pending");
        ReadStringArray(root, "scenario_names").ShouldBe([LifecycleScenarioName()]);
        ReadStringArray(root, "measured_steps").ShouldBe(_requiredSteps, ignoreOrder: true);

        JsonElement observedStepCounts = root.GetProperty("observed_step_counts");
        foreach (string step in _requiredSteps)
        {
            observedStepCounts.GetProperty(step).GetInt32().ShouldBeGreaterThan(0);
        }

        foreach (string path in ReadStringArray(root, "result_artifact_paths"))
        {
            Path.IsPathFullyQualified(path).ShouldBeFalse($"Evidence artifact path must be repository-relative: {path}");
            path.ShouldNotContain("..", Case.Sensitive);
        }

        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void CapacitySmokeDocumentationShouldRecordMaintainerHandoff()
    {
        string documentation = ReadText(OperatorDocPath);

        documentation.ShouldContain("capacity-smoke-gates");
        documentation.ShouldContain("branch protection");
        documentation.ShouldContain("pwsh ./tests/tools/run-capacity-smoke-ci-gates.ps1");
        documentation.ShouldContain("dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj -- --profile quick --run-id capacity-smoke-ci --report-folder _bmad-output/gates/capacity-smoke-ci/reports");
        documentation.ShouldContain(ReportPath);
        documentation.ShouldContain("metadata-only");
        documentation.ShouldContain("reference_pending");
        documentation.ShouldContain("Story 7.10");
        documentation.ShouldContain("C1");
        documentation.ShouldContain("C2");
        documentation.ShouldContain("C5");
        documentation.ShouldContain("git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants");

        foreach (string step in _requiredSteps)
        {
            documentation.ShouldContain(step, Case.Sensitive);
        }
    }

    [Fact]
    public void CapacitySmokeHarnessShouldExposeRequiredMeasuredStepsAndEvidence()
    {
        string scenario = ReadText("tests/load/Scenarios/LifecycleCapacityScenario.cs");
        string evidenceWriter = ReadText("tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs");
        string recorder = ReadText("tests/load/Scenarios/LifecycleCapacityRunRecorder.cs");

        foreach (string step in _requiredSteps)
        {
            scenario.ShouldContain(step, Case.Sensitive);
        }

        scenario.ShouldContain("ReadStatusAsync", Case.Sensitive);
        scenario.ShouldContain("Step.Run<string>(StatusStepName", Case.Sensitive);
        evidenceWriter.ShouldContain("MeasuredSteps", Case.Sensitive);
        evidenceWriter.ShouldContain("ObservedStepCounts", Case.Sensitive);
        recorder.ShouldContain("RecordMeasuredStep", Case.Sensitive);
    }

    [Fact]
    public void CapacitySmokeArtifactsShouldNotIntroduceRecursiveSubmoduleSetup()
    {
        Regex recursiveSubmodule = RecursiveSubmodulePattern();
        foreach (string path in EnumerateScannedFiles())
        {
            if (path.StartsWith("tests/Hexalith.Folders.Contracts.Tests/Deployment/", StringComparison.Ordinal)
                && path.EndsWith("ConformanceTests.cs", StringComparison.Ordinal))
            {
                continue;
            }

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

    private static string LifecycleScenarioName() => "folder_workspace_full_lifecycle";

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
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Capacity smoke CI report value must not contain an absolute path: {value}");
                ForbiddenDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Capacity smoke CI report value must stay metadata-only: {value}");
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

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump|local absolute path|stack trace|cache-key-value|https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDiagnosticPattern();
}
