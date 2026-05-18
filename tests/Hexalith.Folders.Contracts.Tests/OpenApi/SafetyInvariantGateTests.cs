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

    private static readonly string[] RequiredTelemetrySurfaces =
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
        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(CorpusPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "schema_version").ShouldBe("1.0.0");
        RequiredBoolean(RequiredObject(root, "ownership"), "synthetic_data_only").ShouldBeTrue();
        RequiredArray(root, "classification_vocabulary").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(AllowedClassifications.Order(StringComparer.Ordinal));
        RequiredArray(root, "forbidden_output_surfaces").SelectText().Order(StringComparer.Ordinal)
            .ShouldBe(AllowedSurfaces.Order(StringComparer.Ordinal));

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
            AllowedClassifications.ShouldContain(RequiredString(sample, "classification"), id);
            categories.Add(RequiredString(sample, "category")).ShouldBeTrue(id);
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
        using JsonDocument corpus = JsonDocument.Parse(ReadRequiredFile(CorpusPath));
        string[] surfaces = RequiredArray(corpus.RootElement, "forbidden_output_surfaces").SelectText();
        foreach (string surface in RequiredTelemetrySurfaces)
        {
            surfaces.ShouldContain(surface, $"AC 17 requires telemetry surface '{surface}' to be a first-class scan channel.");
        }

        SentinelSample[] samples = LoadSentinelSamples();
        foreach (string surface in RequiredTelemetrySurfaces)
        {
            samples.ShouldContain(
                sample => sample.ForbiddenOutputSurfaces.Contains(surface, StringComparer.Ordinal),
                $"AC 17 requires at least one synthetic sentinel to exercise telemetry surface '{surface}'.");
        }

        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
        string[] channels = RequiredArray(inventory.RootElement, "channels")
            .EnumerateArray()
            .Select(channel => RequiredString(channel, "channel"))
            .ToArray();
        foreach (string surface in RequiredTelemetrySurfaces)
        {
            channels.ShouldContain(surface, $"AC 17 requires inventory entry for telemetry surface '{surface}'.");
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
                (lastEvaluatedAt.GetString() ?? string.Empty).ShouldBe("2026-05-18", $"{name} structured manifest freshness evidence.");
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
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
        Dictionary<string, JsonElement> channels = RequiredArray(inventory.RootElement, "channels")
            .EnumerateArray()
            .ToDictionary(channel => RequiredString(channel, "channel"), StringComparer.Ordinal);

        foreach (string channelName in new[] { "audit-records", "projections", "console-payloads" })
        {
            JsonElement channel = channels[channelName];
            RequiredString(channel, "prerequisite_status").ShouldBe("covered", $"{channelName} has current OpenAPI/generated-client artifacts and must not remain blanket pending.");
            RequiredArray(channel, "artifact_sources").SelectText().ShouldContain(OpenApiRelativePath, $"{channelName} must scan the contract artifact that currently owns its examples.");
            RequiredString(channel, "last_evaluated_at").ShouldBe("2026-05-18", channelName);
        }

        JsonElement events = channels["events"];
        RequiredString(events, "last_evaluated_at").ShouldBe("2026-05-18", "events must carry structured freshness evidence.");
        RequiredString(events, "safe_absence_diagnostic").ShouldBe("SAFETY-PREREQUISITE-DRIFT");
    }

    [Fact]
    public void MissingChannelDiagnosticsAreEmittedAsBoundedRuntimeEvidence()
    {
        SafetyManifestDiagnostic[] diagnostics = BuildMissingChannelDiagnostics();
        diagnostics.ShouldNotBeEmpty();

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
        string openApi = ReadRequiredFile(OpenApiPath);
        foreach (SentinelSample sample in LoadSentinelSamples().Where(sample => sample.ForbiddenOutputSurfaces.Contains("openapi-examples", StringComparer.Ordinal) || sample.ForbiddenOutputSurfaces.Contains("problem-details-examples", StringComparer.Ordinal)))
        {
            AssertDoesNotContainForbiddenValue(openApi, sample, "openapi-examples", OpenApiRelativePath);
        }

        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
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
        string openApi = ReadRequiredFile(OpenApiPath);
        foreach (string stateName in new[] { "wrong-tenant", "unauthorized", "hidden", "redacted", "missing", "unknown", "stale" })
        {
            AssertContainsText(openApi, stateName, $"safe-state:{stateName}");
        }

        AssertContainsText(openApi, "projection_unavailable", "safe-state:projection_unavailable");

        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
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
        string workflow = ReadRequiredFile(WorkflowPath);
        string script = ReadRequiredFile(GateScriptPath);
        string documentation = ReadRequiredFile(GateDocumentationPath);

        workflow.ShouldContain("./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild");
        workflow.ShouldNotContain("if: always()", Case.Insensitive);
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
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
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

                    string text = File.ReadAllText(Path.Combine(RepositoryRoot, NormalizeForFileSystem(file)));
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

        using JsonDocument document = JsonDocument.Parse(ReadRequiredFile(QuarantinePath));
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
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
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
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
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
            string normalized = repositoryPath.Replace("\\", "/", StringComparison.Ordinal);
            if (!IsExcludedByInventory(normalized) && !IsBinaryFile(normalized))
            {
                yield return normalized;
            }

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
        using JsonDocument inventory = JsonDocument.Parse(ReadRequiredFile(InventoryPath));
        return RequiredArray(inventory.RootElement, "structured_exclusions")
            .SelectText()
            .Any(pattern => GlobMatches(normalized, pattern));
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
        Regex.IsMatch(repositoryPath, "^[A-Za-z]:[\\\\/]").ShouldBeFalse(because);
        repositoryPath.StartsWith("/", StringComparison.Ordinal).ShouldBeFalse(because);
        repositoryPath.ShouldNotContain("\\", Case.Sensitive, because);
        repositoryPath.StartsWith("../", StringComparison.Ordinal).ShouldBeFalse(because);
        repositoryPath.Split('/').ShouldNotContain("..", because);
    }

    private static void AssertMetadataOnly(string value, bool allowPatternDocumentation = false)
    {
        string[] repositoryMarkers =
        [
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            RepositoryRoot.Replace("\\", "\\\\", StringComparison.Ordinal),
        ];

        string[] forbidden = allowPatternDocumentation
            ? repositoryMarkers
            : repositoryMarkers.Concat(new[]
        {
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
            "C:\\",
            "D:\\",
        }).Concat(GenericAbsolutePathMarkersExceptRepositoryRoot(repositoryMarkers)).ToArray();

        foreach (string forbiddenValue in forbidden)
        {
            if (value.Contains(forbiddenValue, StringComparison.OrdinalIgnoreCase))
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
        return text.Contains(forbiddenValue, StringComparison.OrdinalIgnoreCase);
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
        if (!string.IsNullOrEmpty(githubWorkspace))
        {
            if (File.Exists(Path.Combine(githubWorkspace, "Hexalith.Folders.slnx")))
            {
                return githubWorkspace;
            }

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
        return includeRoots
            .Select(root => root.Replace("\\", "/", StringComparison.Ordinal).TrimEnd('/'))
            .Any(root => normalized.Equals(root, StringComparison.Ordinal) || normalized.StartsWith(root + "/", StringComparison.Ordinal));
    }

    private static bool GlobMatches(string repositoryPath, string pattern)
    {
        string normalizedPattern = pattern.Replace("\\", "/", StringComparison.Ordinal);
        string regex = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("/\\*\\*", "(?:/.*)?", StringComparison.Ordinal)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(repositoryPath, regex, RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> GenericAbsolutePathMarkersExceptRepositoryRoot(IEnumerable<string> repositoryMarkers)
    {
        foreach (string marker in new[] { "/home/", "/Users/" })
        {
            if (!repositoryMarkers.Any(repositoryMarker => repositoryMarker.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                yield return marker;
            }
        }
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
        if (Regex.IsMatch(text, $@"\b{Regex.Escape(forbidden)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
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
