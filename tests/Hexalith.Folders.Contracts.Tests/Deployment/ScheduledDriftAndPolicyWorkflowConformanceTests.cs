using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ScheduledDriftAndPolicyWorkflowConformanceTests
{
    private const string NightlyWorkflowPath = ".github/workflows/nightly-drift.yml";
    private const string PolicyWorkflowPath = ".github/workflows/policy-conformance.yml";
    private const string NightlyScriptPath = "tests/tools/run-nightly-drift-gates.ps1";
    private const string PolicyScriptPath = "tests/tools/run-scheduled-policy-conformance-gates.ps1";
    private const string OperatorDocPath = "docs/operations/scheduled-drift-and-policy-conformance.md";
    private const string NightlyReportPath = "_bmad-output/gates/nightly-drift/latest.json";
    private const string PolicyReportPath = "_bmad-output/gates/policy-conformance/latest.json";

    private static readonly string[] _rootBuildSubmodules =
    [
        "Hexalith.AI.Tools",
        "Hexalith.Commons",
        "Hexalith.EventStore",
        "Hexalith.FrontComposer",
        "Hexalith.Memories",
        "Hexalith.Tenants",
    ];

    private static readonly string[] _nightlyCategories =
    [
        "forgejo-manifest-integrity",
        "forgejo-snapshot-coverage",
        "forgejo-drift-classification",
        "forgejo-sanitized-report",
        "live-provider-drift",
    ];

    private static readonly string[] _policyCategories =
    [
        "static-policy-shape",
        "fixture-provenance",
        "negative-triple-coverage",
        "mtls-and-sidecar-bindings",
        "pubsub-topic-scopes",
        "live-kind-dapr-denial",
    ];

    private static readonly string[] _forbiddenScheduledWorkflowLanes =
    [
        "pull_request:",
        "push:",
        "run-baseline-ci-gates.ps1",
        "run-contract-parity-ci-gates.ps1",
        "run-security-redaction-ci-gates.ps1",
        "run-capacity-smoke-ci-gates.ps1",
        "run-contract-spine-gates.ps1",
        "run-safety-invariant-gates.ps1",
        "run-governance-completeness-gates.ps1",
        "run-container-image-gates.ps1",
        "upload-artifact",
        "dotnet publish",
        "docker build",
        "semantic-release",
        "playwright install",
        "secrets.",
        "id-token:",
        "packages:",
        "deployments:",
        "pull-requests:",
        "checks:",
        "statuses:",
    ];

    [Fact]
    public void ScheduledWorkflowsShouldUseScheduleDispatchAndStableSetupPosture()
    {
        AssertScheduledWorkflow(
            NightlyWorkflowPath,
            "nightly-drift",
            "nightly-drift-gates",
            NightlyScriptPath,
            "17 2 * * *",
            "provider_profile",
            "pinned-snapshots",
            ["pinned-snapshots", "latest-supported"]);

        AssertScheduledWorkflow(
            PolicyWorkflowPath,
            "policy-conformance",
            "policy-conformance-gates",
            PolicyScriptPath,
            "43 2 * * *",
            "policy_mode",
            "static-plus-live-reference",
            ["static-plus-live-reference", "static-only"]);
    }

    [Fact]
    public void NightlyDriftScriptShouldReuseForgejoFixturesAndFailClosed()
    {
        string script = ReadText(NightlyScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("Set-StrictMode -Version Latest");
        script.ShouldContain("$ErrorActionPreference = 'Stop'");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain("Set-Content -Path $latestReportPath -Encoding utf8NoBOM");
        script.ShouldContain("tests/contracts/forgejo/supported-versions.json");
        script.ShouldContain("tests/tools/forgejo-drift/classification-fixtures.json");
        script.ShouldContain("tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1");
        script.ShouldContain("ForgejoManifestAndDriftTests");
        script.ShouldContain("expected_test_count = 7");
        script.ShouldContain("$executedTests -ne 7");
        script.ShouldContain("fallback=xunit-in-process");
        script.ShouldContain("zero-or-partial-test-selection");
        script.ShouldContain("missing-test-assembly");
        script.ShouldContain("stale-integrity-hash");
        script.ShouldContain("missing-snapshot");
        script.ShouldContain("blocking-drift-classification");
        script.ShouldContain("breaking-incompatible-not-failure");
        script.ShouldContain("unknown-unclassified-not-failure");
        script.ShouldContain("raw-schema-diff-retention");
        script.ShouldContain("reference_pending_story_7_8");
        script.ShouldContain("folders-provider-maintainers");
        script.ShouldContain(NightlyReportPath);

        foreach (string category in _nightlyCategories)
        {
            script.ShouldContain($"'{category}'", Case.Sensitive);
        }

        AssertNoForbiddenReleaseLanes(script);
    }

    [Fact]
    public void ScheduledPolicyScriptShouldWrapStaticGateAndCarryLiveKindBoundary()
    {
        string script = ReadText(PolicyScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("Set-StrictMode -Version Latest");
        script.ShouldContain("$ErrorActionPreference = 'Stop'");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldContain("Set-Content -Path $latestReportPath -Encoding utf8NoBOM");
        script.ShouldContain("tests/tools/run-dapr-policy-conformance-gates.ps1");
        script.ShouldContain("_bmad-output/gates/dapr-policy-conformance/latest.json");
        script.ShouldContain("deploy/dapr/production/accesscontrol.yaml");
        script.ShouldContain("deploy/dapr/production/daprsystem.yaml");
        script.ShouldContain("deploy/dapr/production/pubsub.yaml");
        script.ShouldContain("deploy/dapr/production/secretstore.yaml");
        script.ShouldContain("deploy/dapr/production/sidecar-config-bindings.yaml");
        script.ShouldContain("tests/fixtures/dapr-policy-conformance.yaml");
        script.ShouldContain("missing-test-assembly");
        script.ShouldContain("missing-negative-category");
        script.ShouldContain("static-gate-not-passed");
        script.ShouldContain("live-kind-reference-pending-drift");
        script.ShouldContain("reference_pending_story_7_8");
        script.ShouldContain("platform-engineering");
        script.ShouldContain(PolicyReportPath);

        foreach (string category in _policyCategories)
        {
            script.ShouldContain($"'{category}'", Case.Sensitive);
        }

        foreach (string negativeCategory in new[]
                 {
                     "unknown-source-app",
                     "known-unauthorized-source-app",
                     "wrong-target-app",
                     "wrong-operation",
                     "wrong-http-verb",
                     "wrong-namespace",
                     "wrong-trust-domain",
                 })
        {
            script.ShouldContain($"'{negativeCategory}'", Case.Sensitive);
        }

        AssertNoForbiddenReleaseLanes(script);
    }

    [Fact]
    public void ScheduledReportsShouldStayMetadataOnlyWhenPresent()
    {
        AssertScheduledReportWhenPresent(NightlyReportPath, "nightly-drift", _nightlyCategories);
        AssertScheduledReportWhenPresent(PolicyReportPath, "policy-conformance", _policyCategories);
    }

    [Fact]
    public void DocumentationShouldRecordMaintainerAndReleaseReviewerHandoff()
    {
        string documentation = ReadText(OperatorDocPath);

        documentation.ShouldContain("nightly-drift-gates");
        documentation.ShouldContain("policy-conformance-gates");
        documentation.ShouldContain("02:17 UTC");
        documentation.ShouldContain("02:43 UTC");
        documentation.ShouldContain("workflow_dispatch");
        documentation.ShouldContain("provider_profile");
        documentation.ShouldContain("policy_mode");
        documentation.ShouldContain("pwsh ./tests/tools/run-nightly-drift-gates.ps1");
        documentation.ShouldContain("pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1");
        documentation.ShouldContain(NightlyReportPath);
        documentation.ShouldContain(PolicyReportPath);
        documentation.ShouldContain("metadata-only");
        documentation.ShouldContain("additive provider drift");
        documentation.ShouldContain("Breaking provider drift", Case.Insensitive);
        documentation.ShouldContain("Unauthorized Dapr policy changes", Case.Insensitive);
        documentation.ShouldContain("reference_pending_story_7_8");
        documentation.ShouldContain("contract-spine.yml");
        documentation.ShouldContain("Story 7.12");
        documentation.ShouldContain("Story 7.15");
        documentation.ShouldContain("Story 7.16");
        documentation.ShouldContain("Story 7.17");
        documentation.ShouldContain("git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants");

        foreach (string category in _nightlyCategories.Concat(_policyCategories))
        {
            documentation.ShouldContain(category, Case.Sensitive);
        }
    }

    [Fact]
    public void ExistingPrCiAndContractSpineLanesShouldRemainUsable()
    {
        string ci = ReadText(".github/workflows/ci.yml");
        string contractSpine = ReadText(".github/workflows/contract-spine.yml");

        foreach (string jobName in new[]
                 {
                     "baseline-build-and-unit-gates",
                     "contract-and-parity-gates",
                     "security-and-redaction-gates",
                     "capacity-smoke-gates",
                 })
        {
            ci.ShouldContain(jobName, Case.Sensitive);
        }

        foreach (string script in new[]
                 {
                     "run-contract-spine-gates.ps1",
                     "run-safety-invariant-gates.ps1",
                     "run-governance-completeness-gates.ps1",
                     "run-dapr-policy-conformance-gates.ps1",
                 })
        {
            contractSpine.ShouldContain(script, Case.Sensitive);
        }

        ci.ShouldNotContain("run-nightly-drift-gates.ps1", Case.Insensitive);
        ci.ShouldNotContain("run-scheduled-policy-conformance-gates.ps1", Case.Insensitive);
    }

    [Fact]
    public void ScheduledArtifactsShouldNotIntroduceRecursiveSubmoduleSetup()
    {
        Regex recursiveSubmodule = RecursiveSubmodulePattern();
        foreach (string path in EnumerateScannedFiles())
        {
            recursiveSubmodule.IsMatch(ReadText(path)).ShouldBeFalse($"{path} must not initialize nested submodules recursively.");
        }
    }

    private static void AssertScheduledWorkflow(
        string workflowPath,
        string workflowName,
        string jobName,
        string expectedScript,
        string expectedCron,
        string dispatchInput,
        string expectedDispatchDefault,
        string[] dispatchOptions)
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(workflowPath);
        GetScalar(workflow, "name").ShouldBe(workflowName);
        GetScalar(GetMapping(workflow, "permissions"), "contents").ShouldBe("read");
        GetMapping(workflow, "permissions").Children.Keys.Select(static key => key.ToString()).ShouldBe(["contents"]);

        YamlMappingNode triggers = GetMapping(workflow, "on");
        YamlSequenceNode schedule = GetSequence(triggers, "schedule");
        schedule.Children.Count.ShouldBe(1);
        GetScalar(schedule.Children[0].ShouldBeOfType<YamlMappingNode>(), "cron").ShouldBe(expectedCron);
        YamlMappingNode workflowDispatch = GetMapping(triggers, "workflow_dispatch");
        YamlMappingNode input = GetMapping(GetMapping(workflowDispatch, "inputs"), dispatchInput);
        GetScalar(input, "type").ShouldBe("choice");
        GetScalar(input, "default").ShouldBe(expectedDispatchDefault);
        ReadScalarSequence(input, "options").ShouldBe(dispatchOptions, ignoreOrder: false);

        YamlMappingNode jobs = GetMapping(workflow, "jobs");
        jobs.Children.Count.ShouldBe(1);
        YamlMappingNode job = GetMapping(jobs, jobName);
        GetScalar(job, "name").ShouldBe(jobName);
        GetScalar(job, "runs-on").ShouldBe("ubuntu-latest");

        YamlMappingNode checkout = FindStep(job, "actions/checkout@v6");
        GetScalar(GetMapping(checkout, "with"), "fetch-depth").ShouldBe("1");
        GetScalar(GetMapping(checkout, "with"), "submodules").ShouldBe("false");

        string submoduleCommand = GetScalar(GetSequence(job, "steps").Children.Cast<YamlMappingNode>()
            .Single(step => step.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? value)
                && string.Equals(value.ToString(), "Initialize root-level build submodules", StringComparison.Ordinal)), "run");
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

        string workflowText = ReadText(workflowPath);
        workflowText.ShouldContain("dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false");
        workflowText.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore -m:1");
        workflowText.ShouldContain($"./{expectedScript}");
        workflowText.ShouldNotContain("run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild", Case.Insensitive);
        workflowText.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        AssertNoForbiddenReleaseLanes(workflowText);
    }

    private static void AssertScheduledReportWhenPresent(string relativePath, string gateName, string[] categories)
    {
        if (!File.Exists(RepositoryPath(relativePath)))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(relativePath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe(gateName);
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(relativePath);
        ReadStringArray(root, "categories").ShouldBe(categories);
        AssertReportResults(root, categories);

        AssertMetadataOnlyJson(root);
    }

    private static void AssertReportResults(JsonElement root, string[] categories)
    {
        root.TryGetProperty("results", out JsonElement results).ShouldBeTrue("Scheduled reports must include per-category results.");
        results.ValueKind.ShouldBe(JsonValueKind.Array, "Scheduled report results must be an array.");

        JsonElement[] resultItems = results.EnumerateArray().ToArray();
        resultItems.Select(static item => RequiredString(item, "category")).ShouldBe(categories);

        foreach (JsonElement result in resultItems)
        {
            RequiredString(result, "status").ShouldNotBeNullOrWhiteSpace();
            RequiredString(result, "severity").ShouldNotBeNullOrWhiteSpace();
            result.TryGetProperty("exit_code", out JsonElement exitCode).ShouldBeTrue("Scheduled report result must include an exit_code.");
            exitCode.ValueKind.ShouldBe(JsonValueKind.Number, "Scheduled report exit_code must be numeric metadata.");
            exitCode.TryGetInt32(out _).ShouldBeTrue("Scheduled report exit_code must fit an integer.");
        }
    }

    private static void AssertNoForbiddenReleaseLanes(string text)
    {
        foreach (string forbidden in _forbiddenScheduledWorkflowLanes)
        {
            text.ShouldNotContain(forbidden, Case.Insensitive);
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
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Scheduled report value must not contain an absolute path: {value}");
                ForbiddenDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Scheduled report value must stay metadata-only: {value}");
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

    private static string[] ReadScalarSequence(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>().Children.Cast<YamlScalarNode>().Select(static item => item.Value.ShouldNotBeNull()).ToArray();
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
