using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class SafetyInvariantGateTests
{
    private const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxInputDepth = 64;
    private const int MaxYamlEvents = 100_000;
    private const string InvalidDiagnosticMessage = "safety-invariant:SAFETY-FORBIDDEN-DIAGNOSTIC: channel=diagnostic-validation; remediation=Use approved metadata-only diagnostic fields.";
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private static string ReadRequiredFile(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            long inputLength = stream.Length;
            if (inputLength > MaxInputBytes)
            {
                throw InputDiagnostic();
            }

            byte[] bytes = new byte[(int)inputLength];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
            {
                throw InputDiagnostic();
            }

            return DecodeSafetyText(bytes);
        }
        catch (IOException)
        {
            throw InputDiagnostic();
        }
        catch (UnauthorizedAccessException)
        {
            throw InputDiagnostic();
        }
    }

    private static string DecodeSafetyText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxInputBytes)
        {
            throw InputDiagnostic();
        }

        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            bytes = bytes[Encoding.UTF8.Preamble.Length..];
        }

        try
        {
            string text = _strictUtf8.GetString(bytes);
            // NUL also catches unmarked UTF-16/UTF-32 that otherwise looks like UTF-8.
            if (text.Contains('\0'))
            {
                throw InputDiagnostic();
            }

            return text;
        }
        catch (DecoderFallbackException)
        {
            throw InputDiagnostic();
        }
    }

    private static JsonDocument ParseSafetyJson(string text)
    {
        ValidateInputSize(text);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxInputDepth });
        }
        catch (JsonException)
        {
            throw InputDiagnostic();
        }

        try
        {
            ValidateUniqueJsonProperties(document.RootElement);
            return document;
        }
        catch (InvalidOperationException)
        {
            document.Dispose();
            throw InputDiagnostic();
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void ValidateUniqueJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw InputDiagnostic();
                }

                ValidateUniqueJsonProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateUniqueJsonProperties(item);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            // JsonDocument defers escaped Unicode decoding until strings are read.
            _ = element.GetString();
        }
    }

    private static YamlMappingNode LoadYamlMapping(string path) => ParseSafetyYaml(ReadRequiredFile(path));

    private static YamlMappingNode ParseSafetyYaml(string text)
    {
        ValidateInputSize(text);
        try
        {
            using StringReader preflightReader = new(text);
            Parser parser = new(preflightReader);
            int depth = 0;
            int eventCount = 0;
            while (parser.MoveNext())
            {
                if (++eventCount > MaxYamlEvents || parser.Current is AnchorAlias)
                {
                    throw InputDiagnostic();
                }

                if (parser.Current is MappingStart or SequenceStart)
                {
                    if (++depth > MaxInputDepth)
                    {
                        throw InputDiagnostic();
                    }
                }
                else if (parser.Current is MappingEnd or SequenceEnd)
                {
                    depth--;
                }
            }

            using StringReader reader = new(text);
            YamlStream yaml = new();
            yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                throw InputDiagnostic();
            }

            return mapping;
        }
        catch (YamlException)
        {
            throw InputDiagnostic();
        }
        catch (ArgumentException)
        {
            // Invalid YAML Unicode code points can escape the parser as argument errors.
            throw InputDiagnostic();
        }
    }

    private static void ValidateInputSize(string text)
    {
        try
        {
            if (_strictUtf8.GetByteCount(text) > MaxInputBytes)
            {
                throw InputDiagnostic();
            }
        }
        catch (EncoderFallbackException)
        {
            throw InputDiagnostic();
        }
    }

    private static InvalidOperationException InputDiagnostic() => BoundedDiagnosticException(
        "SAFETY-PREREQUISITE-DRIFT",
        "gate-inputs",
        "Use a readable UTF-8 input within the documented size, structure and depth limits");

    private static InvalidOperationException BoundedDiagnosticException(string ruleId, string channel, string remediation)
    {
        if (ruleId is not ("SAFETY-CHANNEL-MISSING" or "SAFETY-PREREQUISITE-DRIFT" or "SAFETY-FORBIDDEN-VALUE" or "SAFETY-FORBIDDEN-DIAGNOSTIC")
            || string.IsNullOrWhiteSpace(channel) || channel.Length > 128
            || !Regex.IsMatch(channel, "\\A[A-Za-z0-9][A-Za-z0-9_.:-]*\\z", RegexOptions.CultureInvariant)
            || string.IsNullOrWhiteSpace(remediation) || remediation.Length > 256
            || remediation.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029'))
        {
            return new InvalidOperationException(InvalidDiagnosticMessage);
        }

        try
        {
            AssertMetadataOnly(channel);
            AssertMetadataOnly(remediation);
        }
        catch (InvalidOperationException)
        {
            return new InvalidOperationException(InvalidDiagnosticMessage);
        }

        return new InvalidOperationException($"{GateName}:{ruleId}: channel={channel}; remediation={remediation}.");
    }
}
