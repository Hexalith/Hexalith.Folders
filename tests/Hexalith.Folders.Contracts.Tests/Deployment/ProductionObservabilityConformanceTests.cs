using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ProductionObservabilityConformanceTests
{
    private const string ManifestPath = "deploy/observability/production/observability.yaml";
    private const string PubSubPath = "deploy/dapr/production/pubsub.yaml";
    private const string ServiceDefaultsPath = "src/Hexalith.Folders.ServiceDefaults/Extensions.cs";
    private const string ReadinessCheckPath = "src/Hexalith.Folders.ServiceDefaults/MonitoredSnapshotReadinessCheck.cs";
    private const string TelemetryNamesPath = "src/Hexalith.Folders/Observability/FolderTelemetryNames.cs";
    private const string TelemetryEmitterPath = "src/Hexalith.Folders/Observability/FolderTelemetryEmitter.cs";
    private const string GateScriptPath = "tests/tools/run-production-observability-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string GovernancePath = "docs/exit-criteria/c0-c13-governance-evidence.yaml";
    private const string C2Path = "docs/exit-criteria/c2-freshness.md";
    private const string OperationsPath = "docs/operations/production-observability.md";
    private const string ReportPath = "_bmad-output/gates/production-observability/latest.json";
    private const string EndpointTemplate = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string LiveAlertEvidence = "reference_pending_story_7_12";

    private static readonly string[] ExpectedExporterSignals = ["traces", "metrics", "logs"];

    private static readonly string[] ExpectedMonitoredSnapshots =
        ["dapr_sidecar_health", "tenants_availability_degraded_mode", "projection_lag"];

    private static readonly SignalIntent[] ExpectedSignals =
    [
        new("projection_lag", "warning", "folders.projection.lag", "docs/exit-criteria/c2-freshness.md", "folders-server"),
        new("dead_letter_depth", "warning", "folders.deadletter.depth", "architecture-i7", "folders-workers"),
        new("provider_failure", "error", "folders.provider.failures", "architecture-i7", "folders-workers"),
        new("stale_lock", "warning", "folders.lock.stale", "architecture-process-patterns", "folders-server"),
        new("cleanup_failure", "error", "folders.cleanup.failures", "prd-cleanup-observability", "folders-workers"),
    ];

    private static readonly AlertRuleIntent[] ExpectedAlertRules =
    [
        new("projection_lag", "warning", "docs/exit-criteria/c2-freshness.md", LiveAlertEvidence),
        new("dead_letter_depth", "warning", "architecture-i7", LiveAlertEvidence),
        new("provider_failure", "error", "architecture-i7", LiveAlertEvidence),
        new("stale_lock", "warning", "architecture-process-patterns", LiveAlertEvidence),
        new("cleanup_failure", "error", "prd-cleanup-observability", LiveAlertEvidence),
    ];

    private static readonly string[] ForbiddenRawIdentifierTags =
    [
        "folders.tenant_id",
        "folders.folder_id",
        "folders.workspace_id",
        "folders.provider_reference",
        "folders.actor_reference",
        "folders.correlation_id",
        "folders.task_id",
    ];

    [Fact]
    public void ProductionObservabilityManifestShouldDeclareSanitizedExporterHealthAndSignalIntent()
    {
        YamlMappingNode spec = LoadManifestSpec();

        Scalar(spec, "diagnosticPolicy").ShouldBe("metadata-only");

        YamlMappingNode exporters = Mapping(spec, "exporters");
        Scalar(exporters, "protocol").ShouldBe("otlp");
        Scalar(exporters, "vendorNeutral").ShouldBe("true");
        Scalar(exporters, "endpointTemplate").ShouldBe(EndpointTemplate, "Exporter endpoint must stay a templating sentinel, never a real URL.");
        SequenceText(exporters, "signals").Order(StringComparer.Ordinal)
            .ShouldBe(ExpectedExporterSignals.Order(StringComparer.Ordinal), "All three OpenTelemetry signal families must be exported.");

        string[] probePaths = Sequence(spec, "healthProbes").Children.Cast<YamlMappingNode>()
            .Select(probe => Scalar(probe, "path")).ToArray();
        probePaths.ShouldContain("/health/live");
        probePaths.ShouldContain("/health/ready");

        YamlMappingNode readiness = Sequence(spec, "healthProbes").Children.Cast<YamlMappingNode>()
            .Single(probe => Scalar(probe, "path") == "/health/ready");
        SequenceText(readiness, "monitoredSnapshots").Order(StringComparer.Ordinal)
            .ShouldBe(ExpectedMonitoredSnapshots.Order(StringComparer.Ordinal), "Readiness must aggregate the three I-7 monitored snapshots.");

        SignalIntent[] signals = ParseSignals(spec);
        signals.ShouldBe(ExpectedSignals, ignoreOrder: true);
        signals.Length.ShouldBe(ExpectedSignals.Length);

        SignalIntent projectionLag = signals.Single(signal => signal.Name == "projection_lag");
        projectionLag.ThresholdSource.ShouldBe(C2Path, "Projection-lag threshold must trace to the pinned C2 artifact.");
        YamlMappingNode projectionLagNode = Sequence(spec, "signals").Children.Cast<YamlMappingNode>()
            .Single(signal => Scalar(signal, "name") == "projection_lag");
        Scalar(projectionLagNode, "thresholdMilliseconds").ShouldBe("500", "Projection-lag threshold must reuse the pinned C2 500 ms ceiling.");

        AlertRuleIntent[] alertRules = ParseAlertRules(spec);
        alertRules.ShouldBe(ExpectedAlertRules, ignoreOrder: true);
        alertRules.ShouldAllBe(rule => rule.LiveAlertEvidence == LiveAlertEvidence);

        // Semantic hash over the parsed exporter/signal/alert-rule set: drift from the canonical
        // intent changes the hash, so a vacuous or reordered parse cannot silently pass.
        ComputeSemanticHash(signals, alertRules).ShouldBe(ComputeSemanticHash(ExpectedSignals, ExpectedAlertRules));

        AssertManifestMetadataOnly(ReadText(ManifestPath));
    }

    [Fact]
    public void ServiceDefaultsShouldExportThreeSignalFamiliesAndSplitLivenessReadiness()
    {
        string extensions = ReadText(ServiceDefaultsPath);

        // I-6: all three signal families exported through the same OTLP env seam.
        extensions.ShouldContain("builder.Logging.AddOpenTelemetry", Case.Sensitive);
        Regex.Matches(extensions, @"\.AddOtlpExporter\(\)").Count
            .ShouldBe(3, "Traces, metrics, and logs must each add the OTLP exporter behind the OTEL endpoint seam.");
        extensions.ShouldContain(EndpointTemplate, Case.Sensitive);

        // I-7: liveness/readiness split.
        extensions.ShouldContain("AddHealthChecks", Case.Sensitive);
        extensions.ShouldContain("\"/health/live\"", Case.Sensitive);
        extensions.ShouldContain("\"/health/ready\"", Case.Sensitive);
        extensions.ShouldContain("MonitoredSnapshotReadinessCheck", Case.Sensitive);

        string readiness = ReadText(ReadinessCheckPath);
        readiness.ShouldContain("C2ProjectionLagBudgetMilliseconds = 500", Case.Sensitive);
        readiness.ShouldContain("degraded-but-serving", Case.Sensitive);
        readiness.ShouldContain("HealthStatus.Degraded", Case.Sensitive);
        foreach (string snapshot in ExpectedMonitoredSnapshots)
        {
            readiness.ShouldContain(snapshot, Case.Sensitive);
        }
    }

    [Fact]
    public void OperationalSignalInstrumentsShouldBeBoundedAndCentralizedOnTheSingleMeter()
    {
        string telemetryNames = ReadText(TelemetryNamesPath);
        foreach (string instrument in ExpectedSignals.Select(signal => signal.Instrument))
        {
            telemetryNames.ShouldContain($"\"{instrument}\"", Case.Sensitive);
        }

        telemetryNames.ShouldContain("C2ProjectionLagBudgetMilliseconds = 500", Case.Sensitive);

        // Low-cardinality guard: no raw scoped identifier may appear as a metric/trace tag name.
        foreach (string rawIdentifierTag in ForbiddenRawIdentifierTags)
        {
            telemetryNames.ShouldNotContain($"\"{rawIdentifierTag}\"", Case.Sensitive);
        }

        // Single Meter/ActivitySource: the emitter must not spin up a parallel telemetry pipeline.
        // Both are created target-typed on the shared telemetry-name constants.
        string emitter = ReadText(TelemetryEmitterPath);
        Regex.Matches(emitter, @"new\(FolderTelemetryNames\.MeterName\)").Count.ShouldBe(1, "All instruments must share the single existing Meter.");
        Regex.Matches(emitter, @"new\(FolderTelemetryNames\.ActivitySourceName\)").Count.ShouldBe(1, "All spans must share the single existing ActivitySource.");
        foreach (string method in new[]
        {
            "RecordProjectionLag",
            "RecordDeadLetterDepth",
            "RecordProviderFailure",
            "RecordStaleLock",
            "RecordCleanupFailure",
        })
        {
            emitter.ShouldContain(method, Case.Sensitive);
        }
    }

    [Fact]
    public void DeadLetterTopicsShouldBeDeclaredInProductionPubSub()
    {
        string pubsub = ReadText(PubSubPath);
        pubsub.ShouldContain("deadletter.folders.events", Case.Sensitive);
        pubsub.ShouldContain("deadletter.organizations.events", Case.Sensitive);
    }

    [Fact]
    public void ProductionObservabilityGateScriptShouldFailClosedAndEmitBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);

        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "diagnostic_policy",
            "metadata-only",
            "Push-Location",
            "Pop-Location",
            "GATE-VACUOUS",
            "xunit",
            LiveAlertEvidence,
            "FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiShouldWireObservabilityGate()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-production-observability-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        // Lane separation: the static gate belongs to contract-spine, never to a new ci.yml top-level lane.
        string ci = ReadText(".github/workflows/ci.yml");
        ci.ShouldNotContain("run-production-observability-gates.ps1", Case.Sensitive);

        string baseline = ReadText(BaselineGatePath);
        baseline.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj", Case.Sensitive);
        baseline.ShouldContain("Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests", Case.Sensitive);
    }

    [Fact]
    public void GovernanceEvidenceAndC2DocsShouldRecordStory712Ownership()
    {
        YamlMappingNode governance = LoadSingleYamlDocument(GovernancePath);
        YamlMappingNode c2 = Sequence(governance, "criteria").Children.Cast<YamlMappingNode>()
            .Single(node => Scalar(node, "criterion_id") == "C2");

        Scalar(c2, "status").ShouldBe("approved");
        Scalar(c2, "artifact_path").ShouldBe(C2Path);
        Scalar(c2, "result_summary").ShouldContain("Story 7.12", Case.Sensitive);
        Scalar(c2, "result_summary").ShouldContain("production observability", Case.Insensitive);

        string c2Doc = ReadText(C2Path);
        c2Doc.ShouldContain("500", Case.Sensitive);
        c2Doc.ShouldContain("Story 7.12", Case.Sensitive);
        // The deferred-implementation section must record what 7.12 now implements without redefining the target.
        string deferred = c2Doc[c2Doc.IndexOf("## Deferred implementation", StringComparison.Ordinal)..];
        deferred.ShouldContain("production observability", Case.Insensitive);
    }

    [Fact]
    public void OperationsDocShouldDocumentValidationAndMetadataOnlyPolicy()
    {
        string operations = ReadText(OperationsPath);
        foreach (string required in new[]
        {
            "pwsh ./tests/tools/run-production-observability-gates.ps1",
            ReportPath,
            "metadata-only",
            "reviewer",
            "rerun",
            "git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants",
        })
        {
            operations.ShouldContain(required, Case.Sensitive);
        }

        operations.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        AssertManifestMetadataOnly(operations);
    }

    [Fact]
    public void ProductionObservabilityLatestReportShouldStayMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("production-observability");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeObservabilityEvidence()
    {
        // A manifest missing a required signal must fail the inventory-equality assertion.
        SignalIntent[] missingSignal = ExpectedSignals.Where(signal => signal.Name != "cleanup_failure").ToArray();
        Should.Throw<ShouldAssertException>(() => missingSignal.ShouldBe(ExpectedSignals, ignoreOrder: true));

        // A reordered/mutated alert-rule set changes the semantic hash.
        AlertRuleIntent[] mutated = [.. ExpectedAlertRules[..^1], ExpectedAlertRules[^1] with { Severity = "warning" }];
        ComputeSemanticHash(ExpectedSignals, mutated).ShouldNotBe(ComputeSemanticHash(ExpectedSignals, ExpectedAlertRules));

        // Malformed YAML/JSON must fail to parse rather than pass vacuously.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"production-observability\", "));

        // Unsafe diagnostics, production URLs, and absolute paths are rejected by the SAME scanner that
        // guards the real manifest, not a standalone assertion.
        Should.Throw<ShouldAssertException>(() => AssertManifestMetadataOnly("endpoint: https://prod.example.com/otlp"));
        Should.Throw<ShouldAssertException>(() => AssertManifestMetadataOnly("token: synthetic_pat_value"));
        Should.Throw<ShouldAssertException>(() => AssertManifestMetadataOnly("evidence_path: /home/runner/work/secret"));

        // Recursive-submodule detection: the forbidden recursive form is flagged; the approved root-level command is not.
        string recursiveToken = string.Concat("--", "recursive");
        ("git submodule update --init " + recursiveToken).Contains(recursiveToken, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        "git submodule update --init Hexalith.Commons".Contains(recursiveToken, StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    private static SignalIntent[] ParseSignals(YamlMappingNode spec)
        => Sequence(spec, "signals").Children.Cast<YamlMappingNode>()
            .Select(signal => new SignalIntent(
                Scalar(signal, "name"),
                Scalar(signal, "severity"),
                Scalar(signal, "instrument"),
                Scalar(signal, "thresholdSource"),
                Scalar(signal, "owningComponent")))
            .ToArray();

    private static AlertRuleIntent[] ParseAlertRules(YamlMappingNode spec)
        => Sequence(spec, "alertRules").Children.Cast<YamlMappingNode>()
            .Select(rule => new AlertRuleIntent(
                Scalar(rule, "signal"),
                Scalar(rule, "severity"),
                Scalar(rule, "thresholdSource"),
                Scalar(rule, "liveAlertEvidence")))
            .ToArray();

    private static string ComputeSemanticHash(IEnumerable<SignalIntent> signals, IEnumerable<AlertRuleIntent> alertRules)
    {
        var semantic = new
        {
            Exporters = ExpectedExporterSignals.Order(StringComparer.Ordinal).ToArray(),
            Signals = signals
                .OrderBy(static signal => signal.Name, StringComparer.Ordinal)
                .Select(static signal => new { signal.Name, signal.Severity, signal.Instrument, signal.ThresholdSource, signal.OwningComponent })
                .ToArray(),
            AlertRules = alertRules
                .OrderBy(static rule => rule.Signal, StringComparer.Ordinal)
                .Select(static rule => new { rule.Signal, rule.Severity, rule.ThresholdSource, rule.LiveAlertEvidence })
                .ToArray(),
        };

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(semantic);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static YamlMappingNode LoadManifestSpec()
        => Mapping(LoadSingleYamlDocument(ManifestPath), "spec");

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, relativePath);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string Scalar(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    private static YamlMappingNode Mapping(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode Sequence(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static string[] SequenceText(YamlMappingNode node, string key)
        => Sequence(node, key).Children.Cast<YamlScalarNode>().Select(static scalar => scalar.Value ?? string.Empty).ToArray();

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static void AssertManifestMetadataOnly(string value)
    {
        // Health-probe paths like /health/live and repository-relative doc references are legitimate;
        // only secret material, real URLs, and host-absolute paths are forbidden.
        UnsafeManifestPattern().IsMatch(value).ShouldBeFalse($"Observability artifact must stay metadata-only: {Excerpt(value)}");
        HostAbsolutePathPattern().IsMatch(value).ShouldBeFalse($"Observability artifact must not contain a host-absolute path: {Excerpt(value)}");
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
                AssertManifestMetadataOnly(element.GetString().ShouldNotBeNull());
                break;
        }
    }

    private static string Excerpt(string value) => value.Length <= 80 ? value : value[..80];

    [GeneratedRegex(@"https?://|BEGIN [A-Z ]*PRIVATE KEY|client_secret|api[_-]?key|AccountKey=|bearer\s|password\s*=|\btoken\b|\bsecret\b|\bcredential\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeManifestPattern();

    [GeneratedRegex(@"(?:[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    private sealed record SignalIntent(string Name, string Severity, string Instrument, string ThresholdSource, string OwningComponent);

    private sealed record AlertRuleIntent(string Signal, string Severity, string ThresholdSource, string LiveAlertEvidence);
}
