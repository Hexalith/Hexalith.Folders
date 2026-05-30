using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class CapacityCalibrationConformanceTests
{
    private const string GateScriptPath = "tests/tools/run-capacity-calibration-gates.ps1";
    private const string ReportPath = "_bmad-output/gates/capacity-calibration/latest.json";
    private const string EvidencePath = "_bmad-output/gates/capacity-calibration/reports/lifecycle-capacity-evidence.json";

    private static readonly string[] RequiredSteps =
    [
        "prepare_workspace",
        "acquire_workspace_lock",
        "mutate_workspace_file",
        "commit_workspace",
        "read_workspace_status",
    ];

    [Fact]
    public void ExitCriteriaArtifactsShouldPinNumericC1C2C5Targets()
    {
        AssertExitCriteriaArtifact(
            "docs/exit-criteria/c1-capacity.md",
            [
                "Maximum concurrent tenants | 4",
                "Folders per tenant | 2",
                "Active workspaces per tenant | 2",
                "Concurrent agent tasks per tenant | 2",
            ]);

        AssertExitCriteriaArtifact(
            "docs/exit-criteria/c2-freshness.md",
            ["Maximum commit-to-status-read freshness lag | 500"]);

        AssertExitCriteriaArtifact(
            "docs/exit-criteria/c5-scalability-quantifiers.md",
            [
                "Tenant scale units | 4",
                "Folder scale units per tenant | 2",
                "Workspace scale units per tenant | 2",
                "Agent task scale units per tenant | 2",
                "Minimum lifecycle iteration rate | 1",
            ]);
    }

    [Fact]
    public void GovernanceEvidenceShouldPointC1C2C5AtCalibrationArtifacts()
    {
        YamlMappingNode governance = LoadSingleYamlDocument("docs/exit-criteria/c0-c13-governance-evidence.yaml");
        YamlSequenceNode criteria = governance.GetCalibrationSequence("criteria");

        AssertCriterion(criteria, "C1", "approved", "docs/exit-criteria/c1-capacity.md");
        AssertCriterion(criteria, "C2", "approved", "docs/exit-criteria/c2-freshness.md");
        AssertCriterion(criteria, "C5", "approved", "docs/exit-criteria/c5-scalability-quantifiers.md");
    }

    [Fact]
    public void CalibrationGateScriptShouldFailClosedAndRunReleaseProfile()
    {
        string script = ReadText(GateScriptPath);

        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            ReportPath,
            EvidencePath,
            "release-calibration",
            "capacity-calibration",
            "$LASTEXITCODE",
            "missing-load-harness-assembly",
            "stale-source-commit",
            "missing-hardware-profile",
            "zero-or-partial-step-execution",
            "missing-c2-freshness-observation",
            "missing-throughput-rate",
            "non-numeric-target",
            "threshold-mismatch",
            "malformed-evidence-json",
            "metadata-only-report",
            "utf8NoBOM",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        script.ShouldContain("dotnet run --no-build --project $loadProjectPath -- --profile release-calibration", Case.Sensitive);

        foreach (string step in RequiredSteps)
        {
            script.ShouldContain($"'{step}'", Case.Sensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void LoadHarnessShouldPreserveQuickSmokeAndExposeCalibrationProfile()
    {
        string profile = ReadText("tests/load/Scenarios/LifecycleCapacityProfile.cs");
        string scenario = ReadText("tests/load/Scenarios/LifecycleCapacityScenario.cs");
        string evidenceWriter = ReadText("tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs");
        string smokeGate = ReadText("tests/tools/run-capacity-smoke-ci-gates.ps1");

        profile.ShouldContain("ReleaseCalibration", Case.Sensitive);
        profile.ShouldContain("\"release-calibration\"", Case.Sensitive);
        profile.ShouldContain("TenantCount: 4", Case.Sensitive);
        profile.ShouldContain("FoldersPerTenant: 2", Case.Sensitive);
        profile.ShouldContain("WorkspacesPerTenant: 2", Case.Sensitive);
        profile.ShouldContain("TasksPerWorkspace: 2", Case.Sensitive);

        foreach (string step in RequiredSteps)
        {
            scenario.ShouldContain(step, Case.Sensitive);
        }

        evidenceWriter.ShouldContain("release_calibrated", Case.Sensitive);
        evidenceWriter.ShouldContain("HardwareProfile", Case.Sensitive);
        evidenceWriter.ShouldContain("StepLatencyStatistics", Case.Sensitive);
        evidenceWriter.ShouldContain("FreshnessObservations", Case.Sensitive);
        evidenceWriter.ShouldContain("TargetComparison", Case.Sensitive);

        smokeGate.ShouldContain("threshold_posture = 'reference_pending'", Case.Sensitive);
        smokeGate.ShouldContain("--profile', 'quick'", Case.Sensitive);
        smokeGate.ShouldNotContain("release-calibration", Case.Sensitive);
    }

    [Fact]
    public void ReleaseWorkflowAndPackageGateShouldRequireCapacityCalibrationBeforePublishing()
    {
        string workflow = ReadText(".github/workflows/release-packages.yml");
        string packageGate = ReadText("tests/tools/run-release-package-gates.ps1");
        string manifest = ReadText("deploy/nuget/release-packages.yaml");

        workflow.ShouldContain("./tests/tools/run-capacity-calibration-gates.ps1", Case.Sensitive);
        workflow.ShouldContain("Run capacity calibration gates", Case.Sensitive);
        workflow.ShouldNotContain("pull_request", Case.Insensitive);

        packageGate.ShouldContain("_bmad-output/gates/capacity-calibration/latest.json", Case.Sensitive);
        packageGate.ShouldContain("stale-capacity-calibration-evidence", Case.Sensitive);
        packageGate.ShouldContain("missing-capacity-target-comparison", Case.Sensitive);
        packageGate.ShouldContain("missing-release-evidence", Case.Sensitive);

        manifest.ShouldContain("- _bmad-output/gates/capacity-calibration/latest.json", Case.Sensitive);
    }

    [Fact]
    public void CapacityCalibrationDocumentationShouldExplainMaintainerHandoff()
    {
        string documentation = ReadText("docs/operations/capacity-calibration.md");

        foreach (string required in new[]
        {
            "pwsh ./tests/tools/run-capacity-calibration-gates.ps1",
            "release-calibration",
            "capacity-smoke-gates",
            "C1",
            "C2",
            "C5",
            "500 milliseconds",
            "metadata-only",
            "Story 7.12",
            ReportPath,
            EvidencePath,
            "git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants",
        })
        {
            documentation.ShouldContain(required, Case.Sensitive);
        }

        documentation.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void CalibrationLatestReportShouldBeMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("capacity-calibration");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "profile_name").ShouldBe("release-calibration");
        RequiredString(root, "source_commit").Length.ShouldBe(40);
        ReadStringArray(root, "required_measured_steps").ShouldBe(RequiredSteps);

        JsonElement targetComparison = root.GetProperty("target_comparison");
        AssertTargetComparison(targetComparison.GetProperty("c1"), "max_concurrent_tenants", 4);
        AssertTargetComparison(targetComparison.GetProperty("c1"), "folders_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c1"), "active_workspaces_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c1"), "concurrent_agent_tasks_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c2"), "max_commit_to_status_read_freshness_ms", 500);
        AssertTargetComparison(targetComparison.GetProperty("c5"), "tenant_scale_units", 4);
        AssertTargetComparison(targetComparison.GetProperty("c5"), "folder_scale_units_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c5"), "workspace_scale_units_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c5"), "agent_task_scale_units_per_tenant", 2);
        AssertTargetComparison(targetComparison.GetProperty("c5"), "minimum_lifecycle_iterations_per_second", 1);

        foreach (string step in RequiredSteps)
        {
            root.GetProperty("measured_steps").EnumerateArray().Select(static x => x.GetString()).ShouldContain(step);
            root.GetProperty("observed_step_counts").GetProperty(step).GetInt32().ShouldBeGreaterThan(0);
            root.GetProperty("latency_stats").GetProperty(step).GetProperty("p95_ms").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
        }

        root.GetProperty("freshness_observations").GetProperty("commit_to_status_read_ms").GetProperty("p95_ms").GetDouble().ShouldBeLessThanOrEqualTo(500);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void CalibrationEvidenceShouldBeMetadataOnlyWhenPresent()
    {
        string fullEvidencePath = RepositoryPath(EvidencePath);
        if (!File.Exists(fullEvidencePath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(EvidencePath));
        JsonElement root = document.RootElement;

        RequiredString(root, "run_id").ShouldBe("capacity-calibration");
        RequiredString(root, "profile_name").ShouldBe("release-calibration");
        RequiredString(root, "thresholds").ShouldBe("release_calibrated");
        RequiredString(root, "git_commit").Length.ShouldBe(40);
        ReadStringArray(root, "measured_steps").ShouldBe(RequiredSteps, ignoreOrder: true);

        foreach (string path in ReadStringArray(root, "result_artifact_paths"))
        {
            Path.IsPathFullyQualified(path).ShouldBeFalse($"Evidence artifact path must be repository-relative: {path}");
            path.ShouldNotContain("..", Case.Sensitive);
        }

        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void MalformedCalibrationEvidenceJsonIsRejectedByTheParser()
    {
        // The gate's Read-Evidence step parses evidence with ConvertFrom-Json inside a try/catch and
        // fails closed on malformed JSON. This exercises the same rejection the gate relies on.
        Should.Throw<JsonException>(() =>
        {
            using JsonDocument _ = JsonDocument.Parse("{ \"gate\": \"capacity-calibration\", ");
        });
    }

    [Fact]
    public void UnsafeDiagnosticContentIsRejectedByMetadataOnlyScan()
    {
        using JsonDocument bearerLeak = JsonDocument.Parse("{\"diagnostic\":\"Authorization: Bearer super-secret-token\"}");
        Should.Throw<ShouldAssertException>(() => AssertMetadataOnlyJson(bearerLeak.RootElement));

        using JsonDocument urlLeak = JsonDocument.Parse("{\"endpoint\":\"https://internal.example/status\"}");
        Should.Throw<ShouldAssertException>(() => AssertMetadataOnlyJson(urlLeak.RootElement));

        using JsonDocument safe = JsonDocument.Parse("{\"profile\":\"release-calibration\",\"observed\":2}");
        Should.NotThrow(() => AssertMetadataOnlyJson(safe.RootElement));
    }

    [Fact]
    public void FailedOrNonNumericTargetComparisonIsRejectedByConformanceCheck()
    {
        using JsonDocument failed = JsonDocument.Parse("{\"row\":{\"target\":4,\"observed\":2,\"passed\":false}}");
        Should.Throw<ShouldAssertException>(() => AssertTargetComparison(failed.RootElement, "row", 4));

        using JsonDocument passing = JsonDocument.Parse("{\"row\":{\"target\":4,\"observed\":4,\"passed\":true}}");
        Should.NotThrow(() => AssertTargetComparison(passing.RootElement, "row", 4));
    }

    [Fact]
    public void PlaceholderAndUnsafeDiagnosticPatternsRejectKnownBadValues()
    {
        PlaceholderPattern().IsMatch("status: TBD").ShouldBeTrue();
        PlaceholderPattern().IsMatch("reference_pending").ShouldBeTrue();
        PlaceholderPattern().IsMatch("decide later").ShouldBeTrue();
        PlaceholderPattern().IsMatch("approved release-calibration target").ShouldBeFalse();

        UnsafeDiagnosticPattern().IsMatch("Authorization: Bearer abc").ShouldBeTrue();
        UnsafeDiagnosticPattern().IsMatch("api_key=12345").ShouldBeTrue();
        UnsafeDiagnosticPattern().IsMatch("commit_to_status_read_ms").ShouldBeFalse();
    }

    private static void AssertExitCriteriaArtifact(string relativePath, string[] expectedTargets)
    {
        string text = ReadText(relativePath);
        foreach (string required in new[] { "status:", "decision owner:", "approval authority:", "last reviewed:", "Run command", "Evidence path", "Rollback or recalibration rule" })
        {
            text.ShouldContain(required, Case.Insensitive);
        }

        foreach (string target in expectedTargets)
        {
            text.ShouldContain(target, Case.Sensitive);
        }

        PlaceholderPattern().IsMatch(text).ShouldBeFalse($"{relativePath} must not contain unresolved target placeholders.");
    }

    private static void AssertCriterion(YamlSequenceNode criteria, string id, string status, string artifactPath)
    {
        YamlMappingNode criterion = criteria.Children.Cast<YamlMappingNode>()
            .Single(node => node.GetCalibrationScalar("criterion_id") == id);

        criterion.GetCalibrationScalar("status").ShouldBe(status);
        criterion.GetCalibrationScalar("artifact_path").ShouldBe(artifactPath);
        criterion.GetCalibrationScalar("verification_command").ShouldContain("run-capacity-calibration-gates.ps1", Case.Sensitive);
        criterion.GetCalibrationSequence("open_policy_placeholders").Children.Count.ShouldBe(0);
    }

    private static void AssertTargetComparison(JsonElement group, string name, double expectedTarget)
    {
        JsonElement comparison = group.GetProperty(name);
        comparison.GetProperty("target").GetDouble().ShouldBe(expectedTarget);
        comparison.GetProperty("observed").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
        comparison.GetProperty("passed").GetBoolean().ShouldBeTrue();
    }

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
                Path.IsPathFullyQualified(value).ShouldBeFalse($"Metadata-only evidence must not contain absolute paths: {value}");
                UnsafeDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Unsafe diagnostic value: {value}");
                break;
        }
    }

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Hexalith.Folders.slnx");
            if (File.Exists(candidate))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    [GeneratedRegex(@"\bTBD\b|\breference_pending\b|\blater\b|prose-only", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|raw file contents|provider payload|environment dump|local absolute path|stack trace|https?://")]
    private static partial Regex UnsafeDiagnosticPattern();
}

internal static class CapacityCalibrationYamlExtensions
{
    public static string GetCalibrationScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    public static YamlSequenceNode GetCalibrationSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }
}
