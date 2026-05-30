using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class SafetyInvariantGateTests
{
    private const string GateName = "safety-invariant";
    private const string OpenApiRelativePath = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml";
    private static readonly string _repositoryRootPath = FindRepositoryRoot();
    private static readonly string _corpusFilePath = Path.Combine(_repositoryRootPath, "tests", "fixtures", "audit-leakage-corpus.json");
    private static readonly string _inventoryFilePath = Path.Combine(_repositoryRootPath, "tests", "fixtures", "safety-channel-inventory.json");
    private static readonly string _quarantineFilePath = Path.Combine(_repositoryRootPath, "tests", "fixtures", "quarantine", "safety-negative-controls.json");
    private static readonly string _openApiFilePath = Path.Combine(_repositoryRootPath, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string _workflowFilePath = Path.Combine(_repositoryRootPath, ".github", "workflows", "contract-spine.yml");
    private static readonly string _gateScriptFilePath = Path.Combine(_repositoryRootPath, "tests", "tools", "run-safety-invariant-gates.ps1");
    private static readonly string _gateDocumentationFilePath = Path.Combine(_repositoryRootPath, "docs", "contract", "safety-invariant-ci-gates.md");

    private static readonly string[] _allowedClassificationNames =
    [
        "synthetic-sentinel",
        "tenant-sensitive",
        "confidential",
        "metadata-placeholder",
        "safe-provenance",
        "unauthorized-resource-hint",
        "generated-context-sensitive",
    ];

    private static readonly string[] _allowedSurfaceNames =
    [
        "logs",
        "traces",
        "span-names",
        "metric-labels",
        "metric-names",
        "event-names",
        "counters",
        "telemetry-attributes",
        "exception-metadata",
        "baggage",
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

    private static readonly string[] _requiredTelemetrySurfaceNames =
    [
        "traces",
        "span-names",
        "metric-labels",
        "metric-names",
        "event-names",
        "counters",
        "telemetry-attributes",
        "exception-metadata",
        "baggage",
    ];

    [Fact]
    public void SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(_corpusFilePath));
        JsonElement root = document.RootElement;

        RequiredString(root, "schema_version").ShouldBe("1.0.0");
        RequiredBoolean(RequiredObject(root, "ownership"), "synthetic_data_only").ShouldBeTrue();
        RequiredArray(root, "classification_vocabulary").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(_allowedClassificationNames.Order(StringComparer.Ordinal));
        RequiredArray(root, "forbidden_output_surfaces").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(_allowedSurfaceNames.Order(StringComparer.Ordinal));

        JsonElement samples = RequiredArray(root, "sentinel_samples");
        samples.GetArrayLength().ShouldBe(18, "Story 1.15 pins the current corpus size so removing safety categories is reviewer-visible.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> categories = new(StringComparer.Ordinal);
        foreach (JsonElement sample in samples.EnumerateArray())
        {
            string id = RequiredString(sample, "id");
            ids.Add(id).ShouldBeTrue($"Duplicate sentinel sample id '{id}'.");
            RequiredBoolean(sample, "synthetic_sentinel").ShouldBeTrue(id);
            RequiredBoolean(sample, "synthetic_data_only").ShouldBeTrue(id);
            RequiredString(sample, "value").ShouldNotBeNullOrWhiteSpace(id);
            RequiredString(sample, "safe_notes").ShouldContain("synthetic", Case.Insensitive, id);
            _allowedClassificationNames.ShouldContain(RequiredString(sample, "classification"), id);
            categories.Add(RequiredString(sample, "category"));
            RequiredArray(sample, "forbidden_output_surfaces").SelectText().ShouldNotBeEmpty(id);
            RequiredArray(sample, "allowed_provenance_representations").SelectText().ShouldNotBeEmpty(id);
            RequiredArray(sample, "participates_in").SelectText().ShouldContain("positive", id);
        }

        categories.Order(StringComparer.Ordinal).ShouldBe(new[]
        {
            "actor-metadata",
            "branch-name",
            "commit-message",
            "correlation-id",
            "credential-shaped-value",
            "diagnostic-echo",
            "diff",
            "file-content",
            "generated-context",
            "local-absolute-path",
            "path-metadata",
            "production-url",
            "provider-payload",
            "repository-name",
            "safe-provenance",
            "secret-shaped-string",
            "tenant-data",
            "unauthorized-resource",
        }.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TelemetrySurfaceVocabularyIsExplicitAndInventoryAddressable()
    {
        using JsonDocument corpus = JsonDocument.Parse(ReadRequiredFile(_corpusFilePath));
        string[] surfaces = RequiredArray(corpus.RootElement, "forbidden_output_surfaces").SelectText();
        foreach (string surface in _requiredTelemetrySurfaceNames)
        {
            surfaces.ShouldContain(surface, $"AC 17 requires telemetry surface '{surface}' to be a first-class scan channel.");
        }

        SentinelSample[] samples = LoadSentinelSamples();
        foreach (string surface in _requiredTelemetrySurfaceNames)
        {
            samples.ShouldContain(
                sample => sample.ForbiddenOutputSurfaces.Contains(surface, StringComparer.Ordinal),
                $"AC 17 requires at least one synthetic sentinel to exercise telemetry surface '{surface}'.");
        }

        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        string[] channels = RequiredArray(inventory.RootElement, "channels")
            .EnumerateArray()
            .Select(channel => RequiredString(channel, "channel"))
            .ToArray();
        foreach (string surface in _requiredTelemetrySurfaceNames)
        {
            channels.ShouldContain(surface, $"AC 17 requires inventory entry for telemetry surface '{surface}'.");
        }
    }

    [Fact]
    public void SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined()
    {
        string corpus = ReadRequiredFile(_corpusFilePath);
        AssertMetadataOnly(corpus);

        using JsonDocument quarantine = JsonDocument.Parse(ReadRequiredFile(_quarantineFilePath));
        RequiredBoolean(RequiredObject(quarantine.RootElement, "ownership"), "quarantined_negative_controls").ShouldBeTrue();

        foreach (JsonElement control in RequiredArray(quarantine.RootElement, "negative_controls").EnumerateArray())
        {
            RequiredString(control, "id").ShouldStartWith("negative-control-");
            RequiredBoolean(control, "synthetic_data_only").ShouldBeTrue();
            RequiredBoolean(control, "normative_example").ShouldBeFalse();
            RequiredString(control, "contaminated_payload").ShouldNotBeNullOrWhiteSpace();
        }

        using JsonDocument corpusDocument = JsonDocument.Parse(corpus);
        string[] negativeParticipants = RequiredArray(corpusDocument.RootElement, "sentinel_samples")
            .EnumerateArray()
            .Where(sample => RequiredArray(sample, "participates_in").SelectText().Contains("negative-control", StringComparer.Ordinal))
            .Select(sample => RequiredString(sample, "id"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] quarantinedSamples = RequiredArray(quarantine.RootElement, "negative_controls")
            .EnumerateArray()
            .Select(control => RequiredString(control, "sample_id"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        quarantinedSamples.ShouldBe(negativeParticipants, "Every corpus sample marked negative-control must have an opt-in quarantined control.");
    }

    [Fact]
    public void ChannelInventoryResolvesCoveredSourcesAndBoundsMissingChannels()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
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
            string coverageNotes = RequiredString(channel, "coverage_notes");
            owner.ShouldNotBeNullOrWhiteSpace(name);
            owner.ShouldNotContain("-x-", Case.Sensitive, $"{name} owning_story must not use wildcard placeholders like '2-x-...' (AC 15).");
            owner.ShouldNotEndWith("-x", $"{name} owning_story must be a concrete story ID, not a wildcard (AC 15).");
            diagnostic.ShouldBeOneOf("covered", "SAFETY-CHANNEL-MISSING", "SAFETY-PREREQUISITE-DRIFT");
            safeAbsence.ShouldBeOneOf("covered", "SAFETY-CHANNEL-MISSING", "SAFETY-PREREQUISITE-DRIFT");
            AssertMetadataOnly(diagnostic);
            AssertMetadataOnly(safeAbsence);
            coverageNotes.ShouldNotBeNullOrWhiteSpace(name);
            AssertMetadataOnly(coverageNotes);
            if (channel.TryGetProperty("last_evaluated_at", out JsonElement lastEvaluatedAt))
            {
                lastEvaluatedAt.ValueKind.ShouldBe(JsonValueKind.String, name);
                AssertLastEvaluatedAt(lastEvaluatedAt.GetString() ?? string.Empty, name);
            }

            JsonElement sources = RequiredArray(channel, "artifact_sources");
            if (status == "covered")
            {
                sources.GetArrayLength().ShouldBeGreaterThan(0, $"{name} claims coverage but declares no source.");
                foreach (string source in sources.SelectText())
                {
                    AssertRepositoryRelativePath(source, name);
                    IsWithinIncludeRoots(source, includeRoots).ShouldBeTrue($"{name} source must be constrained by include_roots.");
                    PathExists(source).ShouldBeTrue($"{name} points to stale source '{source}'.");
                }
            }
            else
            {
                status.ShouldBeOneOf("reference-pending", "prerequisite-drift");
                sources.GetArrayLength().ShouldBe(0, $"{name} is {status} and must not claim artifact coverage.");
                safeAbsence.ShouldNotBe("covered", $"{name} is {status}; safe_absence_diagnostic must not be 'covered'.");
                string absenceReason = RequiredString(channel, "absence_reason");
                absenceReason.ShouldNotBeNullOrWhiteSpace(name);
                AssertMetadataOnly(absenceReason);
            }
        }
    }

    [Fact]
    public void StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts()
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        Dictionary<string, JsonElement> channels = RequiredArray(inventory.RootElement, "channels")
            .EnumerateArray()
            .ToDictionary(channel => RequiredString(channel, "channel"), StringComparer.Ordinal);

        foreach (string channelName in new[] { "audit-records", "projections", "provider-diagnostics", "console-payloads" })
        {
            JsonElement channel = channels[channelName];
            RequiredString(channel, "prerequisite_status").ShouldBe("covered", $"{channelName} has current OpenAPI/generated-client artifacts and must not remain blanket pending.");
            RequiredArray(channel, "artifact_sources").SelectText().ShouldContain(OpenApiRelativePath, $"{channelName} must scan the contract artifact that currently owns its examples.");
            AssertLastEvaluatedAt(RequiredString(channel, "last_evaluated_at"), channelName);
        }

        JsonElement events = channels["events"];
        AssertLastEvaluatedAt(RequiredString(events, "last_evaluated_at"), "events");
        RequiredString(events, "safe_absence_diagnostic").ShouldBeOneOf("covered", "SAFETY-PREREQUISITE-DRIFT");
    }

    [Fact]
    public void StoryFourteenTelemetryChannelsAreCoveredByRuntimeArtifacts()
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        Dictionary<string, JsonElement> channels = RequiredArray(inventory.RootElement, "channels")
            .EnumerateArray()
            .ToDictionary(channel => RequiredString(channel, "channel"), StringComparer.Ordinal);

        string[] storyFourteenChannels =
        [
            "logs",
            "traces",
            "span-names",
            "metric-labels",
            "metric-names",
            "event-names",
            "counters",
            "telemetry-attributes",
            "exception-metadata",
            "baggage",
            "events",
        ];

        foreach (string channelName in storyFourteenChannels)
        {
            JsonElement channel = channels[channelName];
            RequiredString(channel, "owning_story").ShouldBe("4-14-emit-metadata-only-audit-and-observability");
            RequiredString(channel, "prerequisite_status").ShouldBe("covered", $"{channelName} is now owned by Story 4.14 runtime artifacts and must not remain drift.");
            RequiredString(channel, "diagnostic").ShouldBe("covered", channelName);
            RequiredString(channel, "safe_absence_diagnostic").ShouldBe("covered", channelName);
            RequiredBoolean(channel, "scan_forbidden_values").ShouldBeTrue(channelName);
            RequiredArray(channel, "artifact_sources").GetArrayLength().ShouldBeGreaterThan(0, channelName);
            AssertLastEvaluatedAt(RequiredString(channel, "last_evaluated_at"), channelName);
        }
    }

    [Fact]
    public void MissingChannelDiagnosticsAreEmittedAsBoundedRuntimeEvidence()
    {
        SafetyManifestDiagnostic[] diagnostics = BuildMissingChannelDiagnostics();

        foreach (SafetyManifestDiagnostic diagnostic in diagnostics)
        {
            diagnostic.Gate.ShouldBe(GateName);
            diagnostic.RuleId.ShouldBeOneOf("SAFETY-CHANNEL-MISSING", "SAFETY-PREREQUISITE-DRIFT");
            diagnostic.OutputChannel.ShouldNotBeNullOrWhiteSpace();
            diagnostic.OwnerStory.ShouldNotBeNullOrWhiteSpace();
            diagnostic.Remediation.ShouldNotBeNullOrWhiteSpace();
            AssertMetadataOnly(diagnostic.ToString());
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
            SentinelSample sample = samples.SingleOrDefault(s => s.Id == diagnostic.SampleId)
                ?? throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", "negative-control-quarantine", "negative control sample reference is not declared in the corpus");
            AssertDoesNotContainForbiddenValue(diagnostic.ToString(), sample, diagnostic.OutputChannel, diagnostic.RepositoryPath);
            AssertMetadataOnly(diagnostic.ToString());
        }

        SafetyScanDiagnostic[] normalFindings = ScanManifestCoveredArtifacts(samples);
        normalFindings.ShouldBeEmpty(string.Join(Environment.NewLine, normalFindings.Select(d => d.ToString())));
    }

    [Fact]
    public void OpenApiExamplesAndContextQueriesRemainMetadataOnly()
    {
        string openApi = ReadRequiredFile(_openApiFilePath);
        foreach (SentinelSample sample in LoadSentinelSamples().Where(sample => sample.ForbiddenOutputSurfaces.Contains("openapi-examples", StringComparer.Ordinal) || sample.ForbiddenOutputSurfaces.Contains("problem-details-examples", StringComparer.Ordinal)))
        {
            AssertDoesNotContainForbiddenValue(openApi, sample, "openapi-examples", OpenApiRelativePath);
        }

        YamlMappingNode root = LoadYamlMapping(_openApiFilePath);
        string[] requiredOperationIds = ["SearchFolderFiles", "GlobFolderFiles", "ReadFileRange", "GetFolderFileMetadata"];
        Operation[] contextOperations = EnumerateOperations(root)
            .Where(operation => requiredOperationIds.Contains(operation.OperationId, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        contextOperations.Select(operation => operation.OperationId).Order(StringComparer.OrdinalIgnoreCase)
            .ShouldBe(requiredOperationIds.Order(StringComparer.OrdinalIgnoreCase), "Context-query operations must be present so AC 9 cannot pass vacuously.");

        foreach (Operation operation in contextOperations)
        {
            YamlSequenceNode order = RequiredSequence(RequiredMapping(operation.Node, "x-hexalith-authorization"), "order");
            string[] orderSteps = order.Children
                .Select(node => RequiredScalar(node, "authorization order"))
                .ToArray();
            orderSteps.Length.ShouldBeGreaterThanOrEqualTo(4, $"{operation.OperationId}: AC 9 requires tenant_access, folder_acl, path_policy at the front and query_execution at the end.");
            orderSteps.Take(3).ToArray().ShouldBe(["tenant_access", "folder_acl", "path_policy"], $"{operation.OperationId}: AC 9 authorization-before-observation prefix.");
            orderSteps[^1].ShouldBe("query_execution", $"{operation.OperationId}: AC 9 requires execution to be the final authorization step.");

            string serialized = SerializeYaml(operation.Node);
            AssertContainsText(serialized, "authorization", operation.OperationId);
            AssertDoesNotContainText(serialized, "search-first", operation.OperationId);
            AssertDoesNotContainText(serialized, "filter-later", operation.OperationId);
        }
    }

    [Fact]
    public void SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence()
    {
        string openApi = ReadRequiredFile(_openApiFilePath);
        foreach (string stateName in new[] { "wrong-tenant", "unauthorized", "hidden", "redacted", "missing", "unknown", "stale" })
        {
            AssertContainsText(openApi, stateName, $"safe-state:{stateName}");
        }

        AssertContainsText(openApi, "projection_unavailable", "safe-state:projection_unavailable");

        YamlMappingNode root = LoadYamlMapping(_openApiFilePath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");
        foreach (string exampleName in new[] { "SafeDenial403Forbidden", "SafeDenial404NotFound", "PrincipalMismatchSafeDenialProblem" })
        {
            string serialized = SerializeYaml(RequiredMapping(examples, exampleName));
            (serialized.Contains("resource_unavailable", StringComparison.OrdinalIgnoreCase) || serialized.Contains("safe_denial", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(exampleName);
            (serialized.Contains("redacted", StringComparison.OrdinalIgnoreCase) || serialized.Contains("metadata_only", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(exampleName);
            AssertDoesNotContainWholeToken(serialized, "count", exampleName);
            AssertDoesNotContainWholeToken(serialized, "cursor", exampleName);
            AssertDoesNotContainWholeToken(serialized, "stack", exampleName);
        }

        string[] projectionAvailability = RequiredSequence(RequiredMapping(RequiredMapping(RequiredMapping(root, "components"), "schemas"), "ProjectionAvailability"), "enum")
            .Children
            .Select(node => RequiredScalar(node, "ProjectionAvailability"))
            .ToArray();
        projectionAvailability.Order(StringComparer.Ordinal).ShouldBe(new[] { "available", "redacted", "stale", "unknown", "unavailable" }.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WorkflowAndDocumentationExposeSameOfflineSafetyGate()
    {
        string workflow = ReadRequiredFile(_workflowFilePath);
        string script = ReadRequiredFile(_gateScriptFilePath);
        string documentation = ReadRequiredFile(_gateDocumentationFilePath);

        workflow.ShouldContain("./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild");
        // AC 5: safety gate must run even when an earlier step fails so leakage cannot hide behind a contract regression.
        workflow.ShouldContain("if: ${{ !cancelled() }}");
        workflow.ShouldContain("dotnet restore Hexalith.Folders.slnx");
        workflow.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore");
        workflow.ShouldNotContain("upload-artifact", Case.Insensitive);
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj");
        script.ShouldContain("FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests");
        script.ShouldContain("dotnet restore Hexalith.Folders.slnx");
        script.ShouldContain("dotnet build Hexalith.Folders.slnx --no-restore");
        script.ShouldContain("[Alias('NoRestore')]");
        script.ShouldContain("SAFETY-PREREQUISITE-DRIFT");
        script.ShouldContain("Hexalith.Folders.Contracts.Tests.dll");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldNotContain("--recursive", Case.Insensitive);

        documentation.ShouldContain(".\\tests\\tools\\run-safety-invariant-gates.ps1");
        documentation.ShouldContain("-SkipRestoreBuild");
        documentation.ShouldContain("SAFETY-PREREQUISITE-DRIFT");
        documentation.ShouldContain("reference-pending");
        documentation.ShouldContain("Story 1.16");
        AssertMetadataOnly(documentation, allowPatternDocumentation: true);
    }

    private static SafetyScanDiagnostic[] ScanManifestCoveredArtifacts(IReadOnlyList<SentinelSample> samples)
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        HashSet<SafetyScanDiagnostic> diagnostics = [];
        string[] includeRoots = RequiredArray(inventory.RootElement, "include_roots").SelectText();

        foreach (JsonElement channel in RequiredArray(inventory.RootElement, "channels").EnumerateArray())
        {
            if (RequiredString(channel, "prerequisite_status") != "covered" || !RequiredBoolean(channel, "scan_forbidden_values"))
            {
                continue;
            }

            string channelName = RequiredString(channel, "channel");
            foreach (string source in RequiredArray(channel, "artifact_sources").SelectText())
            {
                if (!IsWithinIncludeRoots(source, includeRoots))
                {
                    throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", channelName, "artifact source is outside declared include roots");
                }

                foreach (string file in EnumerateSourceFiles(source))
                {
                    if (IsExcludedByInventory(file))
                    {
                        continue;
                    }

                    string text = File.ReadAllText(Path.Combine(_repositoryRootPath, NormalizeForFileSystem(file)));
                    diagnostics.UnionWith(ScanText(file, channelName, text, samples));
                }
            }
        }

        return diagnostics.ToArray();
    }

    private static SafetyScanDiagnostic[] ScanNegativeControls(IReadOnlyList<SentinelSample> samples)
    {
        JsonElement quarantineChannel = InventoryChannel("negative-control-quarantine");
        RequiredBoolean(quarantineChannel, "opt_in_scan_forbidden_values").ShouldBeTrue("Negative controls must be explicitly opt-in and separate from normal artifact scans.");

        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(_quarantineFilePath));
        List<SafetyScanDiagnostic> diagnostics = [];
        foreach (JsonElement control in RequiredArray(document.RootElement, "negative_controls").EnumerateArray())
        {
            string payload = RequiredString(control, "contaminated_payload");
            string channel = RequiredString(control, "output_channel");
            string sampleId = RequiredString(control, "sample_id");
            samples.Any(sample => sample.Id == sampleId).ShouldBeTrue($"SAFETY-PREREQUISITE-DRIFT: channel=negative-control-quarantine; sample_id={sampleId}; remediation=Declare sample before quarantine use.");
            diagnostics.AddRange(ScanText("tests/fixtures/quarantine/safety-negative-controls.json", channel, payload, samples));
        }

        return diagnostics.ToArray();
    }

    private static JsonElement InventoryChannel(string channelName)
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        foreach (JsonElement channel in RequiredArray(inventory.RootElement, "channels").EnumerateArray())
        {
            if (RequiredString(channel, "channel") == channelName)
            {
                return channel.Clone();
            }
        }

        throw new InvalidOperationException($"{GateName}:SAFETY-PREREQUISITE-DRIFT: channel={channelName}; remediation=Declare the channel in safety-channel-inventory.json.");
    }

    private static IEnumerable<SafetyScanDiagnostic> ScanText(string repositoryPath, string channel, string text, IEnumerable<SentinelSample> samples)
    {
        foreach (SentinelSample sample in samples)
        {
            if (sample.Classification == "safe-provenance")
            {
                continue;
            }

            if (!sample.ForbiddenOutputSurfaces.Contains(channel, StringComparer.Ordinal))
            {
                continue;
            }

            if (ContainsForbiddenValue(text, sample.Value))
            {
                yield return new(GateName, "SAFETY-FORBIDDEN-VALUE", repositoryPath, channel, sample.Id, sample.Classification, sample.Category, "Replace the raw value with an allowed provenance-safe representation.");
            }
        }
    }

    private static SafetyManifestDiagnostic[] BuildMissingChannelDiagnostics()
    {
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(_inventoryFilePath));
        List<SafetyManifestDiagnostic> diagnostics = [];
        foreach (JsonElement channel in RequiredArray(inventory.RootElement, "channels").EnumerateArray())
        {
            string status = RequiredString(channel, "prerequisite_status");
            if (status == "covered")
            {
                continue;
            }

            diagnostics.Add(new SafetyManifestDiagnostic(
                GateName,
                RequiredString(channel, "safe_absence_diagnostic"),
                RequiredString(channel, "channel"),
                RequiredString(channel, "owning_story"),
                RequiredString(channel, "absence_reason"),
                "Add a repository-relative artifact source when the owning story lands."));
        }

        return diagnostics.ToArray();
    }

    private static SentinelSample[] LoadSentinelSamples()
    {
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(_corpusFilePath));
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
        string normalized = repositoryPath.Replace("\\", "/", StringComparison.Ordinal);
        string absolute = Path.Combine(_repositoryRootPath, NormalizeForFileSystem(repositoryPath));
        if (File.Exists(absolute))
        {
            if ((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) != 0)
            {
                throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", normalized, "declared artifact source is a reparse point and must not be followed");
            }

            if (IsExcludedByInventory(normalized))
            {
                throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", normalized, "declared artifact source matches structured_exclusions");
            }

            if (IsBinaryFile(normalized))
            {
                throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", normalized, "declared artifact source is a binary file");
            }

            yield return normalized;
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

    private static readonly Lazy<Regex[]> _structuredExclusionRegexList = new(() =>
    {
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllText(_inventoryFilePath));
        return RequiredArray(inventory.RootElement, "structured_exclusions")
            .SelectText()
            .Select(pattern => new Regex(BuildGlobRegex(pattern), RegexOptions.Compiled | RegexOptions.CultureInvariant))
            .ToArray();
    });

    private static bool IsExcludedByInventory(string repositoryPath)
    {
        string normalized = repositoryPath.Replace("\\", "/", StringComparison.Ordinal);
        return _structuredExclusionRegexList.Value.Any(regex => regex.IsMatch(normalized));
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
        string absolute = Path.Combine(_repositoryRootPath, NormalizeForFileSystem(repositoryPath));
        return File.Exists(absolute) || Directory.Exists(absolute);
    }

    private static void AssertRepositoryRelativePath(string repositoryPath, string because)
    {
        repositoryPath.ShouldNotBeNullOrWhiteSpace(because);
        Path.IsPathFullyQualified(repositoryPath).ShouldBeFalse(because);
        Regex.IsMatch(repositoryPath, "^[A-Za-z]:[\\\\/]").ShouldBeFalse(because);
        repositoryPath.StartsWith("/", StringComparison.Ordinal).ShouldBeFalse(because);
        repositoryPath.ShouldNotContain("\\", Case.Sensitive, because);
        repositoryPath.StartsWith("../", StringComparison.Ordinal).ShouldBeFalse(because);
        repositoryPath.Split('/').ShouldNotContain("..", because);
    }

    private static void AssertMetadataOnly(string value, bool allowPatternDocumentation = false)
    {
        string[] repositoryRootMarkers =
        [
            _repositoryRootPath,
            _repositoryRootPath.Replace("\\", "/", StringComparison.Ordinal),
            _repositoryRootPath.Replace("\\", "\\\\", StringComparison.Ordinal),
        ];

        // Strip the repository root prefix before scanning so that on Linux/macOS
        // CI runners (where _repositoryRootPath may itself begin with /home/ or /Users/)
        // the generic absolute-path detectors below still fire on a leaked
        // /home/alice/... or /Users/bob/... path that is not the repo root.
        string sanitizedValue = value;
        foreach (string rootMarker in repositoryRootMarkers)
        {
            sanitizedValue = sanitizedValue.Replace(rootMarker, "<repo-root>", StringComparison.OrdinalIgnoreCase);
        }

        // Drive-letter PATH markers can appear in cross-platform path examples in
        // documentation; only these two markers are exempt under allowPatternDocumentation.
        string[] absolutePathPatternMarkers = ["C:\\", "D:\\"];

        // Secret/credential/URL markers and Unix absolute-path markers must NEVER
        // appear in any output channel, including documentation. Documents should
        // describe these patterns abstractly or with placeholders like {value}.
        string[] alwaysForbidden =
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
            "/home/",
            "/Users/",
        ];

        IEnumerable<string> forbidden = repositoryRootMarkers.Concat(alwaysForbidden);
        if (!allowPatternDocumentation)
        {
            forbidden = forbidden.Concat(absolutePathPatternMarkers);
        }

        foreach (string forbiddenValue in forbidden)
        {
            if (sanitizedValue.Contains(forbiddenValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{GateName}:SAFETY-FORBIDDEN-DIAGNOSTIC: classification=confidential; category=metadata-only-output; remediation=Remove raw sensitive diagnostic material.");
            }
        }
    }

    private static void AssertDoesNotContainForbiddenValue(string text, SentinelSample sample, string channel, string repositoryPath)
    {
        if (ContainsForbiddenValue(text, sample.Value))
        {
            throw new InvalidOperationException(new SafetyScanDiagnostic(
                GateName,
                "SAFETY-FORBIDDEN-VALUE",
                repositoryPath,
                channel,
                sample.Id,
                sample.Classification,
                sample.Category,
                "Replace the raw value with an allowed provenance-safe representation.").ToString());
        }
    }

    private static bool ContainsForbiddenValue(string text, string forbiddenValue)
    {
        int index = 0;
        while (index <= text.Length - forbiddenValue.Length)
        {
            int found = text.IndexOf(forbiddenValue, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            if (HasTokenBoundary(text, found) && HasTokenBoundary(text, found + forbiddenValue.Length))
            {
                return true;
            }

            index = found + 1;
        }

        return false;
    }

    private static bool HasTokenBoundary(string text, int position)
    {
        // The caller invokes this twice per candidate match: once at the match start
        // (position == match start) and once at the match end (position == match end).
        // A boundary exists when at least one of the two adjacent characters (the one
        // before and the one at position) is not alphanumeric — so `/`, `.`, `-`, `_`,
        // whitespace, and structural punctuation all count as boundaries. This catches
        // `count` inside `account` (both adjacent chars are letters → no boundary) but
        // allows `count` at the end of `count.` (`.` is non-alphanumeric).
        if (position <= 0 || position >= text.Length)
        {
            return true;
        }

        return !char.IsLetterOrDigit(text[position - 1]) || !char.IsLetterOrDigit(text[position]);
    }

    private static void AssertLastEvaluatedAt(string value, string context)
    {
        DateOnly parsed;
        try
        {
            parsed = DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", context, "last_evaluated_at must be ISO-8601 yyyy-MM-dd");
        }

        if (parsed < new DateOnly(2026, 5, 18))
        {
            throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", context, "last_evaluated_at must be on or after 2026-05-18 Round 4 baseline");
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
        Path.GetRelativePath(_repositoryRootPath, path).Replace("\\", "/", StringComparison.Ordinal);

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static string FindRepositoryRoot()
    {
        string? githubWorkspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        bool isCi = string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(githubWorkspace) && File.Exists(Path.Combine(githubWorkspace, "Hexalith.Folders.slnx")))
        {
            return githubWorkspace;
        }

        // Hard-fail only on CI where GITHUB_WORKSPACE is expected to be authoritative.
        // On local dev a stale GITHUB_WORKSPACE (e.g., Codespaces or `gh act`) should
        // not block the seed-based fallback.
        if (isCi && !string.IsNullOrEmpty(githubWorkspace))
        {
            throw new InvalidOperationException("SAFETY-PREREQUISITE-DRIFT: repository-root-unresolved; remediation=Set GITHUB_WORKSPACE to the Hexalith.Folders checkout root.");
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

    private static bool IsWithinIncludeRoots(string source, IEnumerable<string> includeRoots)
    {
        string normalized = source.Replace("\\", "/", StringComparison.Ordinal).TrimEnd('/');
        foreach (string includeRoot in includeRoots)
        {
            string root = includeRoot.Replace("\\", "/", StringComparison.Ordinal).TrimEnd('/');
            string rootAbsolute = Path.Combine(_repositoryRootPath, NormalizeForFileSystem(root));
            if (File.Exists(rootAbsolute))
            {
                if (normalized.Equals(root, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (normalized.Equals(root, StringComparison.Ordinal) || normalized.StartsWith(root + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GlobMatches(string repositoryPath, string pattern) =>
        Regex.IsMatch(repositoryPath, BuildGlobRegex(pattern), RegexOptions.CultureInvariant);

    private static string BuildGlobRegex(string pattern)
    {
        string normalizedPattern = pattern.Replace("\\", "/", StringComparison.Ordinal);
        // Basename-only globs (no `/`) like `*.dll` should match nested files too,
        // matching .gitignore semantics.
        if (!normalizedPattern.Contains('/', StringComparison.Ordinal))
        {
            normalizedPattern = "**/" + normalizedPattern;
        }

        return "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("/\\*\\*", "(?:/.*)?", StringComparison.Ordinal)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
    }

    private static void AssertContainsText(string text, string expected, string context)
    {
        if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", context, "required metadata-only marker is absent");
        }
    }

    private static void AssertDoesNotContainText(string text, string forbidden, string context)
    {
        if (text.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
        {
            throw BoundedDiagnosticException("SAFETY-FORBIDDEN-DIAGNOSTIC", context, "forbidden metadata-only marker was present");
        }
    }

    private static void AssertDoesNotContainWholeToken(string text, string forbidden, string context)
    {
        // Standard word boundary catches "count", "Count", "COUNT" at non-letter boundaries
        // and rejects "account", "discount" where the prior char is a letter.
        string standardBoundary = $@"\b{Regex.Escape(forbidden)}\b";

        // Camel/Pascal boundary catches "Count" in "pageCount" / "RowCount" / "MyStackTrace"
        // by requiring an initial-cap form preceded by a lowercase letter and not followed
        // by a lowercase letter (which would indicate a different word like "Countdown").
        string capitalized = char.ToUpperInvariant(forbidden[0]) + forbidden[1..].ToLowerInvariant();
        string camelBoundary = $@"(?<=[a-z]){Regex.Escape(capitalized)}(?![a-z])";

        bool matched = Regex.IsMatch(text, standardBoundary, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, camelBoundary, RegexOptions.CultureInvariant);
        if (matched)
        {
            throw BoundedDiagnosticException("SAFETY-FORBIDDEN-DIAGNOSTIC", context, "forbidden resource-existence token was present");
        }
    }

    private static InvalidOperationException BoundedDiagnosticException(string ruleId, string channel, string remediation) =>
        new($"{GateName}:{ruleId}: channel={channel}; remediation={remediation}.");

    private sealed record Operation(string OperationId, YamlMappingNode Node);

    private sealed record SentinelSample(string Id, string Value, string Classification, string Category, string[] ForbiddenOutputSurfaces);

    private sealed record SafetyManifestDiagnostic(string Gate, string RuleId, string OutputChannel, string OwnerStory, string AbsenceReason, string Remediation)
    {
        public override string ToString() =>
            $"{Gate}:{RuleId}: channel={OutputChannel}; owner={OwnerStory}; reason_hash={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AbsenceReason))).ToLowerInvariant()[..12]}; remediation={Remediation}";
    }

    private sealed record SafetyScanDiagnostic(string Gate, string RuleId, string RepositoryPath, string OutputChannel, string SampleId, string Classification, string Category, string Remediation)
    {
        public override string ToString()
        {
            string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RepositoryPath))).ToLowerInvariant()[..12];
            return $"{Gate}:{RuleId}: path_hash={pathHash}; channel={OutputChannel}; sample_id={SampleId}; classification={Classification}; category={Category}; remediation={Remediation}";
        }
    }
}

internal static class JsonElementSafetyExtensions
{
    public static string[] SelectText(this JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
}
