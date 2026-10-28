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
