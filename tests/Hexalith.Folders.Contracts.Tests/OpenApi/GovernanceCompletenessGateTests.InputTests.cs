using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
{
    [Fact]
    public void DuplicateCriteriaProduceOneDiagnosticBeforeValidatingTheirMetadata()
    {
        YamlMappingNode[] rows = LoadCriteriaRows(EvidencePath);
        YamlMappingNode duplicate = CloneRow(rows[0]);
        string criterion = RequiredScalar(duplicate, "criterion_id");
        duplicate.Children.Remove(new YamlScalarNode("owner"));

        GateDiagnostic[] diagnostics = EvaluateExitCriteriaRows(rows.Concat([duplicate, CloneRow(duplicate)]).ToArray());

        diagnostics.ShouldHaveSingleItem().Category.ShouldBe("exit_criteria_duplicate");
        diagnostics[0].Identifier.ShouldBe(criterion);
        AssertMetadataOnly(diagnostics[0].ToString());

        SetScalar(duplicate, "owner", "PLACEHOLDER");
        EvaluateExitCriteriaRows([duplicate]).ShouldContain(d => d.Category == "exit_criteria_malformed");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("'private-marker'")]
    [InlineData("{}")]
    [InlineData("{ $ref: 'private-marker' }")]
    [InlineData("{ $ref: 'private-marker', operationId: Allowed }")]
    [InlineData("{ operationId: null }")]
    [InlineData("{ operationId: ~ }")]
    [InlineData("{ operationId: NULL }")]
    [InlineData("{ operationId: !!null 'private-marker' }")]
    [InlineData("{ operationId: '' }")]
    [InlineData("{ operationId: '  ' }")]
    [InlineData("{ operationId: [private-marker] }")]
    [InlineData("{ operationId: { private-marker: value } }")]
    public void OpenApiOperationsRejectMalformedDeclarationsWithBoundedDrift(string operation)
    {
        YamlMappingNode root = ParseGovernanceTestYaml($"paths:\n  /synthetic:\n    get: {operation}");

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(root));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: openapi-operation-invalid");
        AssertMetadataOnly(error.Message);
    }

    [Theory]
    [InlineData("{}", "openapi-paths-invalid")]
    [InlineData("paths: []", "openapi-paths-invalid")]
    [InlineData("paths: { /synthetic: private-marker }", "openapi-path-item-invalid")]
    [InlineData("paths: { /synthetic: { $ref: private-marker } }", "openapi-path-item-invalid")]
    public void OpenApiPathsRejectUnsupportedShapesWithBoundedDrift(string text, string reason)
    {
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(ParseGovernanceTestYaml(text)));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: " + reason);
    }

    [Fact]
    public void OpenApiOperationsIgnorePathMetadataAndPreserveSortedOperationIds()
    {
        YamlMappingNode root = ParseGovernanceTestYaml("""
            paths:
              /synthetic:
                summary: Synthetic
                description: Synthetic path
                parameters: []
                x-synthetic: { operationId: Ignored }
                post: { operationId: Zulu }
                get: { operationId: Alpha }
              /other:
                parameters: []
                delete: { operationId: Middle }
            """);

        LoadOpenApiOperationIds(root).ShouldBe(["Alpha", "Middle", "Zulu"]);
    }

    [Theory]
    [InlineData("'null'", "null")]
    [InlineData("'~'", "~")]
    [InlineData("!!str null", "null")]
    [InlineData("!!str ~", "~")]
    public void OpenApiOperationIdsPreserveExplicitNullLikeStrings(string yaml, string expected) =>
        LoadOpenApiOperationIds(ParseGovernanceTestYaml($"paths: {{ /synthetic: {{ get: {{ operationId: {yaml} }} }} }}"))
            .ShouldBe([expected]);

    [Theory]
    [InlineData("bin/Synthetic.cs")]
    [InlineData("OBJ/Synthetic.cs")]
    [InlineData("generated/Synthetic.cs")]
    [InlineData("QUARANTINE/Synthetic.cs")]
    [InlineData("/repo/src/bIn/Debug/Synthetic.cs")]
    [InlineData("/repo/tests/oBj/Synthetic.cs")]
    [InlineData("C:\\repo\\src\\gEnErAtEd\\Synthetic.cs")]
    [InlineData("tests\\qUaRaNtInE\\Synthetic.cs")]
    public void GeneratedDirectorySegmentsAreExcludedRegardlessOfCasing(string path) =>
        IsGeneratedOrBuildOutput(path ?? throw new ArgumentNullException(nameof(path))).ShouldBeTrue();

    [Theory]
    [InlineData("src/binary/Synthetic.cs")]
    [InlineData("src/object/Synthetic.cs")]
    [InlineData("tests/GeneratedSource/Synthetic.cs")]
    [InlineData("tests/quarantine-tests/Synthetic.cs")]
    [InlineData("src/SyntheticGenerated.cs")]
    [InlineData("src/bin.cs")]
    public void SourcePathsWithSimilarNamesRemainEligibleForScanning(string path) =>
        IsGeneratedOrBuildOutput(path ?? throw new ArgumentNullException(nameof(path))).ShouldBeFalse();

    [Theory]
    [InlineData("<Project><!-- <TargetFramework>net1.0</TargetFramework> --><PropertyGroup><TargetFramework> net10.0 </TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup Condition=\"'$(Configuration)' == 'Release'\"><TargetFramework>net1.0</TargetFramework></PropertyGroup><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework Condition=\"'$(Configuration)' == 'Release'\">net1.0</TargetFramework><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><PropertyGroup><TargetFramework Condition=\"\">net10.0</TargetFramework></PropertyGroup></Project>")]
    public void RootTargetFrameworkUsesOneUnconditionalXmlDeclaration(string xml) =>
        ReadRootTargetFramework(xml).ShouldBe("net10.0");

    [Theory]
    [InlineData("<Project />")]
    [InlineData("<Other><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Other>")]
    [InlineData("<Project xmlns=\"urn:synthetic\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup xmlns=\"urn:synthetic\"><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework xmlns=\"urn:synthetic\">net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework /></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup Condition=\"'$(Configuration)' == 'Release'\"><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework Condition=\"'$(Configuration)' == 'Release'\">net10.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework>$(private-marker)</TargetFramework></PropertyGroup></Project>")]
    [InlineData("<Project><PropertyGroup><TargetFramework><Nested>net10.0</Nested></TargetFramework></PropertyGroup></Project>")]
    public void RootTargetFrameworkRejectsMissingConditionalOrAmbiguousDeclarations(string xml)
    {
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => ReadRootTargetFramework(xml));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: target-framework-declaration-invalid");
    }

    [Fact]
    public void RootTargetFrameworkRejectsMalformedXmlWithoutParserDetails()
    {
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => ReadRootTargetFramework("<Project private-marker"));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: target-framework-xml-invalid");
        error.InnerException.ShouldBeNull();
    }

    private static YamlMappingNode ParseGovernanceTestYaml(string text)
    {
        YamlStream stream = new();
        stream.Load(new StringReader(text));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }
}
