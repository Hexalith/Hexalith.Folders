using System.Globalization;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class SafetyInvariantGateTests
{
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ar-EG")]
    [InlineData("tr-TR")]
    public void YamlSafetySerializationPreservesNumericAndTaggedValuesAcrossCultures(string cultureName)
    {
        YamlMappingNode source = ParseSafetyYaml("""
            number: !!float 1234.5
            integer: !!int 1234
            text: !!str 1234.5
            quoted: 'null'
            """);
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        string baseline;
        string actual;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            baseline = SerializeYaml(source);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            actual = SerializeYaml(source);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        actual.ShouldBe(baseline);
        YamlMappingNode roundTrip = ParseSafetyYaml(actual);
        foreach (string key in new[] { "number", "integer", "text", "quoted" })
        {
            YamlScalarNode original = source.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlScalarNode>();
            YamlScalarNode serialized = roundTrip.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlScalarNode>();
            serialized.Value.ShouldBe(original.Value);
            serialized.Tag.ShouldBe(original.Tag);
            serialized.Style.ShouldBe(original.Style);
        }
    }
}
