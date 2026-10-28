using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
{
    [Theory]
    [InlineData("!!str null", "null")]
    [InlineData("!!str ~", "~")]
    [InlineData("!!str 123", "123")]
    [InlineData("!!int 123", "123")]
    [InlineData("!!bool true", "true")]
    [InlineData("'null'", "null")]
    [InlineData("\"~\"", "~")]
    [InlineData("|-\n  123", "123")]
    [InlineData(">-\n  123", "123")]
    public void CloningGovernanceRowsPreservesScalarMeaningAndStyle(string yaml, string expectedValue)
    {
        YamlMappingNode source = ParseGovernanceTestYaml("value: " + yaml);
        YamlMappingNode clone = CloneRow(source);
        YamlScalarNode original = source.Children[new YamlScalarNode("value")].ShouldBeOfType<YamlScalarNode>();
        YamlScalarNode copied = clone.Children[new YamlScalarNode("value")].ShouldBeOfType<YamlScalarNode>();

        copied.Value.ShouldBe(expectedValue);
        copied.Tag.ShouldBe(original.Tag);
        copied.Style.ShouldBe(original.Style);
        ReferenceEquals(original, copied).ShouldBeFalse();
    }

    [Fact]
    public void CloningGovernanceRowsPreservesAliasIdentityWhileIsolatingNestedMutations()
    {
        YamlMappingNode source = ParseGovernanceTestYaml("""
            evidence: &synthetic !evidence
              approved: !!bool true
              authorities: !authorities [!!str 123, 'null']
            alias: *synthetic
            sequence: [*synthetic]
            """);
        YamlMappingNode clone = CloneRow(source);
        YamlMappingNode originalEvidence = RequiredMapping(source, "evidence");
        YamlMappingNode copiedEvidence = RequiredMapping(clone, "evidence");
        YamlSequenceNode originalAuthorities = RequiredSequence(originalEvidence, "authorities");
        YamlSequenceNode copiedAuthorities = RequiredSequence(copiedEvidence, "authorities");

        copiedEvidence.Anchor.ShouldBe(originalEvidence.Anchor);
        copiedEvidence.Anchor.ToString().ShouldBe("synthetic");
        copiedEvidence.Tag.ShouldBe(originalEvidence.Tag);
        copiedEvidence.Style.ShouldBe(originalEvidence.Style);
        copiedAuthorities.Tag.ShouldBe(originalAuthorities.Tag);
        copiedAuthorities.Style.ShouldBe(originalAuthorities.Style);
        ReferenceEquals(originalEvidence, copiedEvidence).ShouldBeFalse();
        ReferenceEquals(originalAuthorities, copiedAuthorities).ShouldBeFalse();
        RequiredMapping(clone, "alias").ShouldBeSameAs(copiedEvidence);
        RequiredSequence(clone, "sequence").Children[0].ShouldBeSameAs(copiedEvidence);

        SetScalar(copiedEvidence, "approved", "false");
        copiedAuthorities.Children.Add(new YamlScalarNode("synthetic-authority"));

        RequiredScalar(RequiredMapping(clone, "alias"), "approved").ShouldBe("false");
        RequiredScalar(originalEvidence, "approved").ShouldBe("true");
        originalAuthorities.Children.Count.ShouldBe(2);
        copiedAuthorities.Children.Count.ShouldBe(3);
    }

    [Fact]
    public void CloningGovernanceRowsPreservesRecursiveAliasesAndDistinctEqualNodes()
    {
        YamlMappingNode source = ParseGovernanceTestYaml("""
            cycle: &synthetic !cycle [*synthetic]
            first: [!!str 123]
            second: [!!str 123]
            """);

        YamlMappingNode clone = CloneRow(source);
        YamlSequenceNode copiedCycle = RequiredSequence(clone, "cycle");
        YamlSequenceNode copiedFirst = RequiredSequence(clone, "first");
        YamlSequenceNode copiedSecond = RequiredSequence(clone, "second");

        copiedCycle.Children[0].ShouldBeSameAs(copiedCycle);
        ReferenceEquals(copiedCycle, RequiredSequence(source, "cycle")).ShouldBeFalse();
        copiedCycle.Tag.ShouldBe(RequiredSequence(source, "cycle").Tag);
        copiedCycle.Anchor.ShouldBe(RequiredSequence(source, "cycle").Anchor);
        ReferenceEquals(copiedFirst, copiedSecond).ShouldBeFalse();
        copiedFirst.Children.Add(new YamlScalarNode("synthetic"));
        copiedSecond.Children.Count.ShouldBe(1);
        RequiredSequence(source, "first").Children.Count.ShouldBe(1);
    }

    [Fact]
    public void CloningGovernanceRowsPreservesRecursiveMappingsAndScalarAliases()
    {
        YamlMappingNode source = ParseGovernanceTestYaml("""
            mapping: &synthetic { self: *synthetic }
            first: &scalar !!str 123
            second: *scalar
            """);

        YamlMappingNode clone = CloneRow(source);
        YamlMappingNode copiedMapping = RequiredMapping(clone, "mapping");
        YamlScalarNode copiedScalar = clone.Children[new YamlScalarNode("first")].ShouldBeOfType<YamlScalarNode>();

        RequiredMapping(copiedMapping, "self").ShouldBeSameAs(copiedMapping);
        ReferenceEquals(copiedMapping, RequiredMapping(source, "mapping")).ShouldBeFalse();
        clone.Children[new YamlScalarNode("second")].ShouldBeSameAs(copiedScalar);
        copiedScalar.Tag.ShouldBe(source.Children[new YamlScalarNode("first")].Tag);
        copiedScalar.Anchor.ShouldBe(source.Children[new YamlScalarNode("first")].Anchor);

        SetScalar(copiedMapping, "added", "synthetic");
        copiedScalar.Value = "456";

        RequiredMapping(source, "mapping").Children.Count.ShouldBe(1);
        RequiredScalar(clone, "second").ShouldBe("456");
        RequiredScalar(source, "first").ShouldBe("123");
    }
}
