using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
{
    [Theory]
    [InlineData("'paths': { '/synthetic': { 'get': { 'operationId': Allowed } } }")]
    [InlineData("\"path\\u0073\": { \"/synthetic\": { \"get\": { \"operation\\u0049d\": Allowed } } }")]
    public void OpenApiStructuralKeysAcceptQuotedAndEscapedStrings(string yaml) =>
        LoadOpenApiOperationIds(ParseGovernanceTestYaml(yaml)).ShouldBe(["Allowed"]);

    [Fact]
    public void OpenApiFileRejectsEscapedDuplicateKeysWithBoundedDiagnostics()
    {
        string path = Path.Combine(RepositoryRoot, "_bmad-output", "gates", $"synthetic-duplicate-{Guid.NewGuid():N}.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "paths: { /synthetic: { get: { operationId: Allowed, \"operation\\u0049d\": private-marker } } }");
        try
        {
            InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(path));

            error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: yaml-malformed: " + ToRepositoryPath(path));
            error.InnerException.ShouldBeNull();
            error.ToString().ShouldNotContain("private-marker");
            AssertMetadataOnly(error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("!!str")]
    [InlineData("!")]
    public void OpenApiStructuralKeysUseResolvedStringMeaning(string tag)
    {
        YamlMappingNode root = ParseGovernanceTestYaml($"{tag} paths: {{ {tag} /synthetic: {{ {tag} get: {{ {tag} operationId: Allowed }} }} }}");

        LoadOpenApiOperationIds(root).ShouldBe(["Allowed"]);
    }

    [Theory]
    [InlineData("paths: { /synthetic: { !!str $ref: private-marker, get: { operationId: Allowed } } }", "openapi-path-item-invalid")]
    [InlineData("paths: { /synthetic: { get: { !!str $ref: private-marker, operationId: Allowed } } }", "openapi-operation-invalid")]
    [InlineData("paths: { /synthetic: { ! $ref: private-marker, get: { operationId: Allowed } } }", "openapi-path-item-invalid")]
    [InlineData("paths: { /synthetic: { get: { ! $ref: private-marker, operationId: Allowed } } }", "openapi-operation-invalid")]
    [InlineData("{ paths: {}, !!str paths: { /synthetic: { get: { operationId: Hidden } } } }", "openapi-paths-invalid")]
    [InlineData("paths: { /synthetic: {}, !!str /synthetic: { get: { operationId: Hidden } } }", "openapi-path-key-invalid")]
    [InlineData("paths: { /synthetic: { get: { operationId: Allowed }, !!str get: { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { get: { operationId: Allowed, !!str operationId: Hidden } } }", "openapi-operation-invalid")]
    public void OpenApiRejectsTaggedReferencesAndSemanticallyDuplicateKeys(string yaml, string reason) =>
        AssertOpenApiShapeRejected(yaml, reason);

    [Theory]
    [InlineData("!!str { paths: {} }", "openapi-paths-invalid")]
    [InlineData("paths: !!str { /synthetic: { get: { operationId: Hidden } } }", "openapi-paths-invalid")]
    [InlineData("paths: !!seq {}", "openapi-paths-invalid")]
    [InlineData("paths: !private-marker {}", "openapi-paths-invalid")]
    [InlineData("paths: { /synthetic: !!str { get: { operationId: Hidden } } }", "openapi-path-item-invalid")]
    [InlineData("paths: { /synthetic: !!seq {} }", "openapi-path-item-invalid")]
    [InlineData("paths: { /synthetic: { get: !!str { operationId: Hidden } } }", "openapi-operation-invalid")]
    [InlineData("paths: { /synthetic: { get: !private-marker { operationId: Hidden } } }", "openapi-operation-invalid")]
    public void OpenApiRejectsIncompatibleStructuralCollectionTags(string yaml, string reason) =>
        AssertOpenApiShapeRejected(yaml, reason);

    [Theory]
    [InlineData("!!map")]
    [InlineData("!")]
    public void OpenApiAcceptsCompatibleCollectionTags(string tag) =>
        LoadOpenApiOperationIds(ParseGovernanceTestYaml($"{tag} {{ paths: {tag} {{ /synthetic: {tag} {{ get: {tag} {{ operationId: Allowed }} }} }} }}"))
            .ShouldBe(["Allowed"]);

    [Theory]
    [InlineData("summary: { operationId: private-marker }")]
    [InlineData("description: [private-marker]")]
    [InlineData("summary: null")]
    [InlineData("summary:")]
    [InlineData("description: 123")]
    [InlineData("summary: !!bool true")]
    [InlineData("parameters: { operationId: private-marker }")]
    [InlineData("parameters: null")]
    [InlineData("servers: private-marker")]
    [InlineData("servers: !!str []")]
    [InlineData("parameters: !!map []")]
    [InlineData("parameters: !private-marker []")]
    public void OpenApiRejectsMalformedKnownPathMetadataShapes(string metadata) =>
        AssertOpenApiShapeRejected($"paths:\n  /synthetic:\n    {metadata}\n    get: {{ operationId: Allowed }}", "openapi-path-metadata-invalid");

    [Fact]
    public void OpenApiAcceptsEmptyStringMetadataAndTaggedSequences()
    {
        YamlMappingNode root = ParseGovernanceTestYaml("""
            paths:
              /synthetic:
                !!str summary: ''
                description: !!str null
                parameters: !!seq [{ $ref: '#/components/parameters/Synthetic' }]
                servers: ! [{ url: 'https://synthetic.invalid' }]
                x-synthetic: !custom { operationId: Ignored }
                get: { operationId: Allowed }
              /other:
                summary: !!str ''
                description: '  '
                parameters: []
                servers: !!seq []
                get: { operationId: Other }
            """);

        LoadOpenApiOperationIds(root).ShouldBe(["Allowed", "Other"]);
    }

    private static void AssertOpenApiShapeRejected(string yaml, string reason)
    {
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(ParseGovernanceTestYaml(yaml)));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: " + reason);
        error.InnerException.ShouldBeNull();
        error.ToString().ShouldNotContain("private-marker");
        error.ToString().ShouldNotContain("Hidden");
        AssertMetadataOnly(error.Message);
    }

    [Theory]
    [InlineData("!!int 123")]
    [InlineData("!!bool true")]
    [InlineData("!!float '1.5'")]
    [InlineData("!!timestamp '2026-10-28'")]
    [InlineData("!synthetic private-marker")]
    [InlineData("123")]
    [InlineData("-123")]
    [InlineData("+123")]
    [InlineData("0o17")]
    [InlineData("0xAB")]
    [InlineData("1.5")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("1e3")]
    [InlineData("-.Inf")]
    [InlineData(".NaN")]
    [InlineData(".nan")]
    [InlineData("+.INF")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890")]
    [InlineData("true")]
    [InlineData("FALSE")]
    public void OpenApiOperationIdsRejectNonStringScalarTypes(string yaml)
    {
        YamlMappingNode root = ParseGovernanceTestYaml($"paths: {{ /synthetic: {{ get: {{ operationId: {yaml} }} }} }}");

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(root));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: openapi-operation-invalid");
        error.InnerException.ShouldBeNull();
        AssertMetadataOnly(error.Message);
    }

    [Theory]
    [InlineData("'123'", "123")]
    [InlineData("\"true\"", "true")]
    [InlineData("!!str 123", "123")]
    [InlineData("!!str false", "false")]
    [InlineData("! 123", "123")]
    [InlineData("! null", "null")]
    [InlineData("1eSynthetic", "1eSynthetic")]
    [InlineData("2026-10-28", "2026-10-28")]
    [InlineData("tRuE", "tRuE")]
    [InlineData("nUlL", "nUlL")]
    [InlineData("1_000", "1_000")]
    [InlineData("0o8", "0o8")]
    [InlineData("0XAB", "0XAB")]
    public void OpenApiOperationIdsPreserveYamlCoreStrings(string yaml, string expected) =>
        LoadOpenApiOperationIds(ParseGovernanceTestYaml($"paths: {{ /synthetic: {{ get: {{ operationId: {yaml} }} }} }}"))
            .ShouldBe([expected]);

    [Theory]
    [InlineData("|-")]
    [InlineData(">-")]
    public void OpenApiOperationIdsPreserveBlockStrings(string style) =>
        LoadOpenApiOperationIds(ParseGovernanceTestYaml($"paths:\n  /synthetic:\n    get:\n      operationId: {style}\n        123"))
            .ShouldBe(["123"]);

    [Theory]
    [InlineData("paths: { private-marker: { get: { operationId: Hidden } } }", "openapi-path-key-invalid")]
    [InlineData("paths: { ? [private-marker] : { get: { operationId: Hidden } } }", "openapi-path-key-invalid")]
    [InlineData("paths: { ? { private-marker: value } : { get: { operationId: Hidden } } }", "openapi-path-key-invalid")]
    [InlineData("paths: { !!int '/synthetic': { get: { operationId: Hidden } } }", "openapi-path-key-invalid")]
    [InlineData("paths: { x-: private-marker }", "openapi-path-key-invalid")]
    [InlineData("paths: { /synthetic: { ? [private-marker] : { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { ? { private-marker: value } : { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { !!int get: { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { true: { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { GET: { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { private-marker: { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { '': { operationId: Hidden } } }", "openapi-method-key-invalid")]
    [InlineData("paths: { /synthetic: { x-: private-marker } }", "openapi-method-key-invalid")]
    public void OpenApiEnumerationRejectsMalformedKeysInsteadOfOmittingOperations(string yaml, string reason)
    {
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadOpenApiOperationIds(ParseGovernanceTestYaml(yaml)));

        error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: " + reason);
        error.ToString().ShouldNotContain("private-marker");
        error.InnerException.ShouldBeNull();
        AssertMetadataOnly(error.Message);
    }

    [Fact]
    public void OpenApiEnumerationAcceptsExtensionsMetadataAndEveryHttpMethod()
    {
        YamlMappingNode root = ParseGovernanceTestYaml("""
            paths:
              x-scalar: synthetic
              x-sequence: [synthetic]
              x-mapping: { private-marker: { operationId: Ignored } }
              /:
                summary: Synthetic
                description: Synthetic path
                parameters: []
                servers: []
                x-scalar: synthetic
                x-sequence: [synthetic]
                get: { operationId: Get }
                put: { operationId: Put }
                post: { operationId: Post }
                patch: { operationId: Patch }
                delete: { operationId: Delete }
                head: { operationId: Head }
                options: { operationId: Options }
                trace: { operationId: Trace }
              /synthetic/{syntheticId}:
                get: { operationId: Nested }
            """);

        LoadOpenApiOperationIds(root).ShouldBe(["Delete", "Get", "Head", "Nested", "Options", "Patch", "Post", "Put", "Trace"]);
    }

    [Theory]
    [InlineData("field: *provider_token_private_marker")]
    [InlineData("field: [provider_token_private_marker")]
    [InlineData("provider_token_private_marker: one\nprovider_token_private_marker: two")]
    public void MalformedYamlFileDiagnosticsOmitParserDetailsFromCompleteException(string yaml)
    {
        string path = Path.Combine(RepositoryRoot, "_bmad-output", "gates", $"synthetic-yaml-{Guid.NewGuid():N}.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml);
        try
        {
            InvalidOperationException error = Should.Throw<InvalidOperationException>(() => LoadYamlStream(path));

            error.Message.ShouldBe("GOVERNANCE-PREREQUISITE-DRIFT: yaml-malformed: " + ToRepositoryPath(path));
            error.InnerException.ShouldBeNull();
            error.ToString().ShouldNotContain("private_marker");
            AssertMetadataOnly(error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
