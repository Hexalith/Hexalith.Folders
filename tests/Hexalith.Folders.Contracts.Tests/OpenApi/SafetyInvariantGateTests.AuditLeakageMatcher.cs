using System.Globalization;
using System.Text.Json;
using Hexalith.Folders.Tests.Shared.AuditLeakage;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class SafetyInvariantGateTests
{
    [Fact]
    public void AuditLeakageMatcherDetectsEveryQuarantinedSampleInRawAndEscapedPayloads()
    {
        IReadOnlyList<AuditLeakageSample> samples = LoadSentinelSamples();
        using JsonDocument document = ParseSafetyJson(ReadRequiredFile(_quarantineFilePath));
        foreach (JsonElement control in RequiredArray(document.RootElement, "negative_controls").EnumerateArray())
        {
            string sampleId = RequiredString(control, "sample_id");
            AuditLeakageSample sample = samples.Single(sample => sample.Id == sampleId);
            string controlId = RequiredString(control, "id");
            string payload = RequiredString(control, "contaminated_payload");
            AuditLeakageMatcher.Contains(payload, sample.Value).ShouldBeTrue(
                $"Runtime scan missed quarantined control '{controlId}'.");

            string escaped = string.Concat(sample.Value.ToLowerInvariant().Select(
                character => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture)));
            string propertyPayload = "{\"" + escaped + "\":\"value\"}";
            string nestedPayload = "{\"items\":[{\"note\":\"" + escaped + "\"}]}";
            using JsonDocument propertyDocument = JsonDocument.Parse(propertyPayload);
            using JsonDocument nestedDocument = JsonDocument.Parse(nestedPayload);
            AuditLeakageMatcher.Contains(propertyPayload, sample.Value, requireTokenBoundaries: true).ShouldBeTrue(
                $"Artifact scan missed an escaped property name for '{controlId}'.");
            AuditLeakageMatcher.Contains(nestedPayload, sample.Value).ShouldBeTrue(
                $"Runtime scan missed an escaped nested value for '{controlId}'.");
        }
    }

    [Fact]
    public void AuditLeakageMatcherAppliesBoundariesToArtifactMatches()
    {
        const string value = "marker";
        const string embedded = "beforeMARKERafter";
        AuditLeakageMatcher.Contains(embedded, value).ShouldBeTrue();
        AuditLeakageMatcher.Contains(embedded, value, requireTokenBoundaries: true).ShouldBeFalse();
        AuditLeakageMatcher.Contains(embedded + " [MARKER]", value, requireTokenBoundaries: true).ShouldBeTrue();
        AuditLeakageMatcher.Contains("""{"value":"MARKER\u005a"}""", value, requireTokenBoundaries: true).ShouldBeFalse();
        AuditLeakageMatcher.Contains("[unstructured output: MARKER", value).ShouldBeTrue();
        AuditLeakageMatcher.Contains("clean output", value).ShouldBeFalse();
    }

    [Fact]
    public void AuditLeakageMatcherScansNonStringJsonNodes()
    {
        using JsonDocument document = JsonDocument.Parse("{\"attempts\":20240229,\"stale\":true}");
        AuditLeakageMatcher.Contains(document.RootElement.GetProperty("attempts"), "20240229").ShouldBeTrue();
        AuditLeakageMatcher.Contains(document.RootElement.GetProperty("stale"), "true").ShouldBeTrue();
        AuditLeakageMatcher.Contains(document.RootElement.GetProperty("attempts"), "19700101").ShouldBeFalse();
    }

    [Fact]
    public void AuditLeakageMatcherHonorsSampleChannelAndParticipation()
    {
        AuditLeakageSample correlation = AuditLeakageCorpus.Get("correlation-metadata");
        string correlationPayload = JsonSerializer.Serialize(new { correlation = correlation.Value });
        AuditLeakageMatcher.LeaksOn(correlation, "events", correlationPayload).ShouldBeFalse();
        AuditLeakageMatcher.LeaksOn(correlation, "console-payloads", correlationPayload).ShouldBeTrue();

        AuditLeakageSample operation = AuditLeakageCorpus.Get("safe-provenance-operation-id");
        AuditLeakageMatcher.LeaksOn(operation, "events", operation.Value).ShouldBeFalse();
        AuditLeakageMatcher.LeaksOn(operation, "provider-diagnostics", operation.Value).ShouldBeTrue();
        Should.Throw<ArgumentException>(() => AuditLeakageMatcher.LeaksOn(operation, "unknown-channel", operation.Value));
        AuditLeakageMatcher.LeaksOn(
            operation with { ParticipatesIn = ["negative-control"] },
            "provider-diagnostics",
            operation.Value).ShouldBeFalse();
    }

    [Fact]
    public void ArtifactScanChecksSafeProvenanceAgainstTheDeclaredChannel()
    {
        AuditLeakageSample sample = AuditLeakageCorpus.Get("safe-provenance-operation-id");
        const string artifact = "tests/fixtures/synthetic-operation.txt";

        ScanText(artifact, "events", sample.Value, [sample]).ShouldBeEmpty();
        SafetyScanDiagnostic diagnostic = ScanText(artifact, "provider-diagnostics", sample.Value, [sample]).ShouldHaveSingleItem();
        diagnostic.SampleId.ShouldBe(sample.Id);
        diagnostic.OutputChannel.ShouldBe("provider-diagnostics");
        diagnostic.ToString().ShouldNotContain(sample.Value);
    }
}
