using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class SafetyInvariantGateTests
{
    [Theory]
    [InlineData("{\"key\":1,\"key\":2}")]
    [InlineData("{\"key\":1,\"\\u006bey\":2}")]
    [InlineData("{\"items\":[{\"nested\":1,\"nested\":2}]}")]
    public void JsonInputRejectsDuplicateDecodedProperties(string text)
    {
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument document = ParseSafetyJson(text);
        });
    }

    [Fact]
    public void JsonInputAllowsCaseDistinctAndSeparateObjectProperties()
    {
        using JsonDocument document = ParseSafetyJson("{\"key\":1,\"Key\":2,\"items\":[{\"key\":3},{\"key\":4}]}");
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(2);
    }

    [Theory]
    [InlineData("{\"\\uD800\":0}")]
    [InlineData("{\"\\uDFFF\":0}")]
    [InlineData("{\"key\":\"\\uD800\"}")]
    [InlineData("{\"items\":[\"\\uDFFF\"]}")]
    public void JsonInputRejectsInvalidEscapedUnicodeWithoutDecodingDetails(string text)
    {
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument document = ParseSafetyJson(text);
        });
    }

    [Theory]
    [InlineData("value: \"\\UFFFFFFFF\"")]
    [InlineData("value: \"\\U00110000\"")]
    [InlineData("value: \"\\U0000D800\"")]
    public void YamlInputRejectsInvalidUnicodeCodePointsWithoutArgumentDetails(string text)
    {
        AssertSafetyInputRejected(() => ParseSafetyYaml(text));
    }

    [Fact]
    public void StructuredInputRejectsMalformedDataWithoutParserDetails()
    {
        string[] malformed = ["{\"private-marker\":", "value: [private-marker", "value: 1\n---\nvalue: 2", "- scalar-root", ""];
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument document = ParseSafetyJson(malformed[0]);
        });
        foreach (string text in malformed.Skip(1))
        {
            AssertSafetyInputRejected(() => ParseSafetyYaml(text));
        }
    }

    [Fact]
    public void JsonInputEnforcesExactDepthBoundary()
    {
        string text = new string('[', MaxInputDepth) + "0" + new string(']', MaxInputDepth);
        using JsonDocument document = ParseSafetyJson(text);
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument rejected = ParseSafetyJson("[" + text + "]");
        });
    }

    [Fact]
    public void YamlInputEnforcesExactDepthBoundary()
    {
        string text = new string('[', MaxInputDepth - 1) + "0" + new string(']', MaxInputDepth - 1);
        ParseSafetyYaml("value: " + text).Children.Count.ShouldBe(1);
        AssertSafetyInputRejected(() => ParseSafetyYaml("value: [" + text + "]"));
    }

    [Theory]
    [InlineData("value: &a [*a]")]
    [InlineData("first: &a [0]\nsecond: *a")]
    public void YamlInputRejectsAliasesBeforeRepresentationLoading(string text)
    {
        AssertSafetyInputRejected(() => ParseSafetyYaml(text));
    }

    [Fact]
    public void YamlInputRejectsExcessiveEventCountBelowSizeLimit()
    {
        string text = "value: [" + string.Join(',', Enumerable.Repeat("0", MaxYamlEvents)) + "]";
        Encoding.UTF8.GetByteCount(text).ShouldBeLessThan(MaxInputBytes);
        AssertSafetyInputRejected(() => ParseSafetyYaml(text));
    }

    [Fact]
    public void StructuredInputEnforcesUtf8ByteSizeBoundary()
    {
        string json = "\"" + new string('a', MaxInputBytes - 2) + "\"";
        using JsonDocument document = ParseSafetyJson(json);
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument rejected = ParseSafetyJson(json + " ");
        });

        string yaml = "value: " + new string('a', MaxInputBytes - 7);
        ParseSafetyYaml(yaml).Children.Count.ShouldBe(1);
        AssertSafetyInputRejected(() => ParseSafetyYaml(yaml + " "));
        string multibyte = "\"" + new string('\u00e9', MaxInputBytes / 2) + "\"";
        AssertSafetyInputRejected(() =>
        {
            using JsonDocument rejected = ParseSafetyJson(multibyte);
        });
    }

    [Fact]
    public void TextInputAcceptsStrictUtf8WithOptionalBom()
    {
        string text = "value: caf\u00e9";
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        DecodeSafetyText(bytes).ShouldBe(text);
        DecodeSafetyText([.. Encoding.UTF8.Preamble, .. bytes]).ShouldBe(text);
    }

    [Fact]
    public void TextInputRejectsUnsupportedEncodingsAndReplacementFallback()
    {
        byte[][] inputs =
        [
            [0xff],
            [0xc3, 0x28],
            [0xe9],
            [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("value")],
            Encoding.Unicode.GetBytes("value"),
            Encoding.UTF32.GetBytes("value"),
        ];
        foreach (byte[] bytes in inputs)
        {
            AssertSafetyInputRejected(() => DecodeSafetyText(bytes));
        }
    }

    [Fact]
    public void FileInputEnforcesSizeEncodingAndSafeReadFailures()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes("value")]);
            ReadRequiredFile(path).ShouldBe("value");
            File.WriteAllBytes(path, [0xff]);
            AssertSafetyInputRejected(() => ReadRequiredFile(path));

            using (FileStream stream = File.OpenWrite(path))
            {
                stream.SetLength(MaxInputBytes + 1);
            }

            AssertSafetyInputRejected(() => ReadRequiredFile(path));
            File.Delete(path);
            AssertSafetyInputRejected(() => ReadRequiredFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MetadataDiagnosticsRejectAbsolutePathsIncludingTheCheckout()
    {
        string[] paths = ["/tmp/private-marker", "/var/private-marker", "/home/private-marker", "/Users/private-marker", _repositoryRootPath];
        foreach (string path in paths)
        {
            foreach (bool allowPatternDocumentation in new[] { false, true })
            {
                Exception? failure = Record.Exception(() => AssertMetadataOnly(path, allowPatternDocumentation));
                (failure is InvalidOperationException).ShouldBeTrue("Absolute paths must be rejected without reflecting the input.");
                AssertMetadataOnly(failure!.Message);
            }
        }
    }

    [Theory]
    [InlineData("docs/PrivateArtifact.md", "docs/privateartifact.md")]
    [InlineData("docs/PrivateDirectory/one.md", "docs/privatedirectory/two.md")]
    public void ArtifactPathsRejectCaseCollisionsWithoutEchoingPaths(string first, string second)
    {
        Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);
        RegisterArtifactPath(paths, first, "events");
        RegisterArtifactPath(paths, first, "logs");
        RegisterArtifactPath(paths, "docs/other.md", "events");

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => RegisterArtifactPath(paths, second, "events"));
        error.Message.ShouldBe("safety-invariant:SAFETY-PREREQUISITE-DRIFT: channel=events; remediation=Use one spelling for each scanned artifact path.");
    }

    [Theory]
    [InlineData("docs/./PrivateArtifact.md", "docs//privateartifact.md")]
    [InlineData("docs/PrivateDirectory/.", "docs//privatedirectory/other.md")]
    public void DeclaredArtifactPathsDetectCollisionsBeforeCheckingTheFilesystem(string first, string second)
    {
        using JsonDocument inventory = ParseSafetyJson(JsonSerializer.Serialize(new
        {
            channels = new[]
            {
                new { channel = "logs", prerequisite_status = "covered", artifact_sources = new[] { first } },
                new { channel = "events", prerequisite_status = "covered", artifact_sources = new[] { second } },
            },
        }));

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => RegisterDeclaredArtifactPaths(inventory.RootElement));
        error.Message.ShouldBe("safety-invariant:SAFETY-PREREQUISITE-DRIFT: channel=events; remediation=Use one spelling for each scanned artifact path.");
    }

    [Fact]
    public void MissingChannelDiagnosticsUseChannelHintsAndPreserveTheDefault()
    {
        using JsonDocument inventory = ParseSafetyJson(MissingChannelInventory("\"Add the event metadata artifact.\""));
        BuildMissingChannelDiagnostics(inventory.RootElement).ShouldHaveSingleItem()
            .Remediation.ShouldBe("Add the event metadata artifact.");

        using JsonDocument noHint = ParseSafetyJson(MissingChannelInventory(null));
        BuildMissingChannelDiagnostics(noHint.RootElement).ShouldHaveSingleItem()
            .Remediation.ShouldBe("Add a repository-relative artifact source when the owning story lands.");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"\"")]
    [InlineData("\"private-marker\\nforged\"")]
    [InlineData("\"/tmp/private-marker\"")]
    public void MissingChannelDiagnosticsRejectInvalidHintsWithoutEchoingThem(string hint)
    {
        using JsonDocument inventory = ParseSafetyJson(MissingChannelInventory(hint));
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => BuildMissingChannelDiagnostics(inventory.RootElement));
        error.Message.ShouldNotContain("private-marker");
        AssertMetadataOnly(error.Message);
    }

    [Fact]
    public void MissingMarkerAndMissingContextOperationHaveDistinctBoundedDiagnostics()
    {
        AssertContainsText("MARKER", "marker", "safe-state:stale");
        AssertContainsText("ALTERNATIVE", "marker", "safe-denial-examples", "alternative");
        InvalidOperationException missingMarker = Should.Throw<InvalidOperationException>(() =>
            AssertContainsText("private-content", "private-marker", "safe-state:stale", "private-alternative"));
        missingMarker.Message.ShouldBe("safety-invariant:SAFETY-REQUIRED-MARKER-ABSENT: channel=safe-state:stale; remediation=Restore the required metadata-only marker.");
        InvalidOperationException missingSafeState = Should.Throw<InvalidOperationException>(() => AssertRequiredSafeStateMarkers("private-content"));
        missingSafeState.Message.ShouldBe("safety-invariant:SAFETY-REQUIRED-MARKER-ABSENT: channel=safe-states; remediation=Restore the required metadata-only marker.");

        AssertRequiredContextOperations(["SearchFolderFiles", "GlobFolderFiles"], ["GlobFolderFiles", "SearchFolderFiles"]);
        InvalidOperationException missingOperation = Should.Throw<InvalidOperationException>(() =>
            AssertRequiredContextOperations([], ["private-operation"]));
        missingOperation.Message.ShouldBe("safety-invariant:SAFETY-PREREQUISITE-DRIFT: channel=context-queries; remediation=Restore the required context-query operations.");

        var missingId = ParseSafetyYaml("paths: { /private-route: { get: { summary: private-content } } }");
        InvalidOperationException missingIdError = Should.Throw<InvalidOperationException>(() => EnumerateOperations(missingId).ToArray());
        missingIdError.Message.ShouldBe("safety-invariant:SAFETY-PREREQUISITE-DRIFT: channel=context-queries; remediation=Declare an operation ID for each HTTP operation.");
    }

    private static string MissingChannelInventory(string? hint) =>
        $$"""
        { "channels": [ {
          "channel": "events",
          "prerequisite_status": "reference-pending",
          "safe_absence_diagnostic": "SAFETY-CHANNEL-MISSING",
          "owning_story": "1-15-wire-safety-invariant-ci-gates",
          "absence_reason": "The event source is pending."
          {{(hint is null ? string.Empty : ", \"remediation_hint\": " + hint)}}
        } ] }
        """;

    [Fact]
    public void BoundedDiagnosticsRetainApprovedMetadata()
    {
        InvalidOperationException error = BoundedDiagnosticException("SAFETY-PREREQUISITE-DRIFT", "events", "Declare the expected source");
        error.Message.ShouldBe("safety-invariant:SAFETY-PREREQUISITE-DRIFT: channel=events; remediation=Declare the expected source.");
        error.InnerException.ShouldBeNull();
    }

    [Fact]
    public void BoundedDiagnosticsRejectUnknownUnsafeAndOversizedFields()
    {
        string[] rules = ["SAFETY-PREREQUISITE-DRIFT", "arbitrary-rule", "", "rule\nforged", null!];
        foreach (string rule in rules.Skip(1))
        {
            AssertInvalidDiagnostic(rule, "events", "Use approved metadata");
        }

        string[] channels = ["/tmp/private-marker", "events\nforged", "events; forged=value", new string('x', 129), "", null!];
        foreach (string channel in channels)
        {
            AssertInvalidDiagnostic(rules[0], channel, "Use approved metadata");
        }

        string[] remediations = ["/var/private-marker", "password=private-marker", _repositoryRootPath, "hint\nforged", "hint\u2028forged", "hint\u2029forged", new string('x', 257), "", null!];
        foreach (string remediation in remediations)
        {
            AssertInvalidDiagnostic(rules[0], "events", remediation);
        }
    }

    private static void AssertInvalidDiagnostic(string ruleId, string channel, string remediation)
    {
        InvalidOperationException error = BoundedDiagnosticException(ruleId, channel, remediation);
        string.Equals(error.Message, InvalidDiagnosticMessage, StringComparison.Ordinal).ShouldBeTrue("Invalid diagnostic fields must produce fixed safe output.");
        error.InnerException.ShouldBeNull();
    }

    private static void AssertSafetyInputRejected(Action action)
    {
        Exception? failure = Record.Exception(action);
        (failure is InvalidOperationException).ShouldBeTrue("Invalid safety inputs must fail with a bounded diagnostic.");
        string.Equals(failure?.Message, InputDiagnostic().Message, StringComparison.Ordinal).ShouldBeTrue("Input errors must not expose parser or file details.");
        (failure?.InnerException is null).ShouldBeTrue("Input errors must not retain unsafe inner exceptions.");
    }
}
