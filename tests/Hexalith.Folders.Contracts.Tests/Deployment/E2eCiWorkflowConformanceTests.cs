using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Story 8.5 (AC5, Option A) — conformance gate pinning the full UI end-to-end CI lane: the <c>e2e-gates</c> job
/// in <c>ci.yml</c>, the focused gate script, and the operator doc. Mirrors
/// <see cref="AccessibilityCiWorkflowConformanceTests"/>, but asserts the gate runs the <b>full</b>
/// <c>Hexalith.Folders.UI.E2E.Tests</c> lane (all 63 tests) rather than a single namespace subset — that is the
/// genuine environmental gap this lane closes. A change that drops the job, removes the browser provisioning,
/// narrows the gate to a namespace subset, or reintroduces a forbidden CI substring (AD7) fails here.
/// </summary>
public sealed partial class E2eCiWorkflowConformanceTests
{
    private const string WorkflowPath = ".github/workflows/ci.yml";
    private const string GateScriptPath = "tests/tools/run-e2e-ci-gates.ps1";
    private const string OperatorDocPath = "docs/operations/e2e-ci-gates.md";
    private const string ReportPath = "_bmad-output/gates/e2e/latest.json";
    private const string JobName = "e2e-gates";
    private const string E2eProject = "tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj";
    private const string InstallScriptInvocation = "./tests/install-playwright.ps1";
    private const string GateScriptInvocation = "./tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall";

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

    [Fact]
    public void E2eWorkflowJobUsesStableCheckoutSubmodulesSdkAndGateSteps()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);
        YamlMappingNode job = GetMapping(GetMapping(workflow, "jobs"), JobName);

        GetScalar(job, "name").ShouldBe(JobName);
        GetScalar(job, "runs-on").ShouldBe("ubuntu-latest");

        YamlMappingNode checkout = FindStep(job, "actions/checkout@v6");
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
        GetScalar(GetMapping(setupDotnet, "with"), "global-json-file").ShouldBe("global.json");

        // The job must provision the browser and then run the gate with -SkipBrowserInstall — both via pwsh.
        YamlMappingNode provisionStep = FindRunStep(job, InstallScriptInvocation);
        GetScalar(provisionStep, "shell").ShouldBe("pwsh");

        YamlMappingNode gateStep = FindRunStep(job, GateScriptInvocation);
        GetScalar(gateStep, "shell").ShouldBe("pwsh");
    }

    [Fact]
    public void E2eWorkflowAvoidsForbiddenInfrastructureSubstrings()
    {
        // AD7: the gate must stay a hermetic localhost lane — never these substrings anywhere in ci.yml.
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain(JobName, Case.Sensitive);
        workflow.ShouldContain(GateScriptInvocation, Case.Sensitive);
        workflow.ShouldNotContain("playwright install", Case.Insensitive);
        workflow.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflow.ShouldNotContain("secrets.", Case.Insensitive);
        workflow.ShouldNotContain("services:", Case.Insensitive);
        workflow.ShouldNotContain("dotnet publish", Case.Insensitive);
        workflow.ShouldNotContain("docker", Case.Insensitive);
        workflow.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void E2eGateScriptProvisionsBrowserRunsFullLaneAndFailsClosed()
    {
        string script = ReadText(GateScriptPath);

        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            "Split-Path -Parent $MyInvocation.MyCommand.Path",
            "Resolve-Path",
            "Join-Path $toolsParent",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "diagnostic_policy",
            "metadata-only",
            "Push-Location",
            "Pop-Location",
            "E2E-PREREQUISITE-DRIFT",
            "-SkipBrowserInstall",
            "install-playwright.ps1",
            "-SkipBuild",
            E2eProject,
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        // The defining property of this lane: it runs the FULL UI.E2E project, not a namespace subset. A reintroduced
        // namespace/category narrowing (the gap 8.4 left) must fail here.
        script.ShouldNotContain("--filter", Case.Sensitive);
        script.ShouldNotContain("-namespace", Case.Sensitive);
        script.ShouldNotContain("FullyQualifiedName", Case.Sensitive);

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        script.ShouldNotContain("upload-artifact", Case.Insensitive);
        script.ShouldNotContain("dotnet publish", Case.Insensitive);
        script.ShouldNotContain("docker", Case.Insensitive);
        script.ShouldNotContain("secrets.", Case.Insensitive);
    }

    [Fact]
    public void E2eOperatorDocumentationRecordsJobStatusCheckAndDiagnosticPolicy()
    {
        string documentation = ReadText(OperatorDocPath);

        documentation.ShouldContain(JobName, Case.Sensitive);
        documentation.ShouldContain("branch protection", Case.Sensitive);
        documentation.ShouldContain("metadata-only", Case.Sensitive);
        documentation.ShouldContain(GateScriptPath, Case.Sensitive);
        documentation.ShouldContain(ReportPath, Case.Sensitive);

        foreach (string module in _rootBuildSubmodules)
        {
            documentation.ShouldContain(module, Case.Sensitive);
        }
    }

    [Fact]
    public void E2eGateReportStaysMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("e2e");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);

        // AC5's defining property recorded at the report layer: the lane is the FULL UI.E2E project, not a namespace
        // subset. A report regressing to a scoped run (the gap 8.4 left) is caught here, not just at the script layer.
        RequiredString(root, "validation_class").ShouldBe("Hexalith.Folders.UI.E2E.Tests");
        RequiredString(root, "scope").ShouldBe("full-ui-e2e-lane");
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void E2eArtifactsDoNotIntroduceRecursiveSubmoduleSetup()
    {
        foreach (string path in new[] { WorkflowPath, GateScriptPath, OperatorDocPath })
        {
            ReadText(path).Contains(string.Concat("--", "recursive"), StringComparison.OrdinalIgnoreCase)
                .ShouldBeFalse($"{path} must not initialize nested submodules recursively.");
        }
    }

    private static YamlMappingNode FindStep(YamlMappingNode job, string uses)
        => GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("uses"), out YamlNode? value)
                && string.Equals(value.ToString(), uses, StringComparison.Ordinal));

    private static YamlMappingNode FindRunStep(YamlMappingNode job, string run)
        => GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("run"), out YamlNode? value)
                && string.Equals(value.ToString()?.Trim(), run, StringComparison.Ordinal));

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
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
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"E2E CI report value must not contain an absolute path: {value}");
                ForbiddenDiagnosticPattern().IsMatch(value).ShouldBeFalse($"E2E CI report value must stay metadata-only: {value}");
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

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDiagnosticPattern();
}
