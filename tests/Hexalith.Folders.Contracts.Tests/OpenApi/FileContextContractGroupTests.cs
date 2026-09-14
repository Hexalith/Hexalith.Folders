using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class FileContextContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ExtensionVocabularyPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "extensions", "hexalith-extension-vocabulary.yaml");
    private static readonly string ContractNotesPath = Path.Combine(RepositoryRoot, "docs", "contract", "file-context-contract-groups.md");

    private static readonly string[] FileContextOperationIds =
    [
        "AddFile",
        "ChangeFile",
        "RemoveFile",
        "ListFolderFiles",
        "GetFolderFileMetadata",
        "SearchFolderFiles",
        "GlobFolderFiles",
        "ReadFileRange",
    ];

    private static readonly string[] MutatingOperationIds =
    [
        "AddFile",
        "ChangeFile",
        "RemoveFile",
    ];

    [Fact]
    public void FileContextOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        operations.Select(o => o.OperationId).Order(StringComparer.Ordinal).ShouldBe(FileContextOperationIds.Order(StringComparer.Ordinal));
        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.Select(o => o.Path).All(p => p.StartsWith("/api/v1/folders/{folderId}/workspaces/{workspaceId}/", StringComparison.Ordinal)).ShouldBeTrue();

        string[] forbiddenPathFragments =
        [
            "/commits",
            "/audit",
            "/ops-console",
            "/provider-bindings",
            "/repository-bindings",
        ];

        foreach (Operation operation in operations)
        {
            foreach (string forbidden in forbiddenPathFragments)
            {
                operation.Path.ShouldNotContain(forbidden, Case.Sensitive, $"{operation.OperationId} belongs to a downstream story.");
            }
        }

        ResolveRefs(root);
    }

    [Fact]
    public void Oq2CanonicalPolicyPinsPathVocabularyPrecedenceContentAndSafeDenial()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string policy = File.ReadAllText(ContractNotesPath);

        string[] requiredStatements =
        [
            "Policy version: `1.1.0`",
            "Approved by: `Administrator` for PM, Architecture, and Security",
            "2026-09-14",
            "at most 500 characters",
            "ASCII `A-Z a-z 0-9 . _ - /`",
            "caller's exact accepted spelling",
            "ordinal-ignore-case semantics",
            "touched entry and each existing ancestor",
            "no-follow",
            "`content_allowed`",
            "`metadata_only`",
            "`excluded`",
            "`restricted`",
            "non-empty include allowlist",
            "Exclusions are then evaluated and always win",
            "later include never reverses an exclusion",
            "invalid, empty,",
            "oversized, stale, unreadable, or unavailable policy fails closed",
            "1,048,576 bytes",
            "through 100 caller-ordered",
            "10,485,760 aggregate add/change bytes",
            "strict UTF-8 validation",
            "one UTF-8 BOM at byte zero",
            "HTTP 404",
            "`category: tenant_access_denied`",
            "`code: resource_unavailable`",
            "HTTP 416 is reserved for a caller that is already authorized",
            "Stories 12.1, 12.3, and 4.20 and FR32-FR35 runtime proof remain incomplete",
        ];

        foreach (string statement in requiredStatements)
        {
            policy.ShouldContain(statement, Case.Sensitive);
        }

        policy.ShouldNotContain("safe-denial-matrix follow-up", Case.Insensitive);
        policy.ShouldNotContain("path-policy-class definition story", Case.Insensitive);
        policy.ShouldNotContain("parser-policy story", Case.Insensitive);
        policy.ShouldNotContain("tenant_sensitive_document", Case.Sensitive);
    }

    [Fact]
    public void Oq2OpenApiPinsCanonicalPathClassesMutationBoundsAndRouting()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode policy = RequiredMapping(root, "x-hexalith-file-policy");
        YamlMappingNode vocabulary = LoadYamlMapping(ExtensionVocabularyPath);
        YamlMappingNode policyDefinition = RequiredMapping(vocabulary, "x-hexalith-file-policy");
        YamlMappingNode policySchema = RequiredMapping(policyDefinition, "valueSchema");
        SchemaAccepts(vocabulary, policySchema, policy).ShouldBeTrue("the complete root policy instance must satisfy the registered valueSchema");
        SchemaAccepts(vocabulary, policySchema, RequiredMapping(policyDefinition, "example"))
            .ShouldBeTrue("the vocabulary example must satisfy its own registered valueSchema");

        YamlMappingNode missingNestedPolicyField = CloneMapping(policy);
        RequiredMapping(missingNestedPolicyField, "policySnapshot").Children.Remove(new YamlScalarNode("unavailableOutcome"));
        SchemaAccepts(vocabulary, policySchema, missingNestedPolicyField).ShouldBeFalse("nested required policy members must fail closed");

        YamlMappingNode extraNestedPolicyField = CloneMapping(policy);
        RequiredMapping(extraNestedPolicyField, "pathProfile").Add("unknownProfileRule", "forbidden");
        SchemaAccepts(vocabulary, policySchema, extraNestedPolicyField).ShouldBeFalse("nested unknown policy members must fail closed");

        GetScalar(policy, "version").ShouldBe("1.1.0");
        GetScalar(policy, "canonicalArtifact").ShouldBe("docs/contract/file-context-contract-groups.md");

        YamlMappingNode pathProfile = RequiredMapping(policy, "pathProfile");
        GetScalar(pathProfile, "characterProfile").ShouldBe("ASCII A-Z a-z 0-9 . _ - /");
        GetScalar(pathProfile, "maximumCharacters").ShouldBe("500");
        GetScalar(pathProfile, "unicodeNormalization").ShouldBe("NFC");
        GetScalar(pathProfile, "collisionComparison").ShouldBe("ordinal-ignore-case");
        GetScalar(pathProfile, "componentCollisionComparison").ShouldBe("ordinal-ignore-case");
        GetScalar(pathProfile, "callerSpelling").ShouldBe("preserve-without-retargeting");
        GetScalar(pathProfile, "linkHandling").ShouldBe("reject-touched-entry-or-ancestor-without-following");
        GetScalar(pathProfile, "trailingSpaceOrDot").ShouldBe("reject");
        RequiredSequence(pathProfile, "restrictedRoots").Children.Cast<YamlScalarNode>().Select(node => node.Value).ShouldBe([".git"]);

        RequiredSequence(policy, "policyClasses").Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["content_allowed", "metadata_only", "excluded", "restricted"]);

        YamlMappingNode precedence = RequiredMapping(policy, "policyPrecedence");
        GetScalar(precedence, "includeAllowlistRequired").ShouldBe("true");
        GetScalar(precedence, "exclusionsAlwaysWin").ShouldBe("true");
        GetScalar(precedence, "reInclusionSupported").ShouldBe("false");
        GetScalar(precedence, "invalidOrUnavailablePolicy").ShouldBe("fail-closed");

        YamlMappingNode limits = RequiredMapping(policy, "mutationLimits");
        GetScalar(limits, "inlineTransportBytes").ShouldBe("262144");
        GetScalar(limits, "perFileBytes").ShouldBe("1048576");
        GetScalar(limits, "maximumChanges").ShouldBe("100");
        GetScalar(limits, "aggregateBytes").ShouldBe("10485760");
        GetScalar(limits, "validation").ShouldBe("atomic-apply-none-on-any-failure");

        YamlMappingNode readability = RequiredMapping(policy, "contentReadability");
        GetScalar(readability, "encoding").ShouldBe("strict-utf-8-with-optional-leading-bom");
        GetScalar(readability, "maximumBytes").ShouldBe("1048576");
        RequiredSequence(readability, "allowedControls").Children.Cast<YamlScalarNode>().Select(node => node.Value).ShouldBe(["tab", "carriage-return", "line-feed"]);
        GetScalar(readability, "binaryAndOtherEncodings").ShouldBe("metadata-only");
        GetScalar(readability, "visibleDirectoryClass").ShouldBe("metadata_only");
        GetScalar(readability, "truncation").ShouldBe("forbidden");

        YamlMappingNode hidden = RequiredMapping(policy, "hiddenPathOutcome");
        GetScalar(hidden, "status").ShouldBe("404");
        GetScalar(hidden, "category").ShouldBe("tenant_access_denied");
        GetScalar(hidden, "code").ShouldBe("resource_unavailable");
        GetScalar(policy, "authorizedUnsatisfiableRangeStatus").ShouldBe("416");

        foreach (Operation operation in EnumerateOperations(root).Where(operation => FileContextOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)))
        {
            GetScalar(RequiredMapping(RequiredMapping(operation.Node, "responses"), "404"), "$ref")
                .ShouldBe("#/components/responses/FileSafeAuthorizationDenial404", operation.OperationId);
        }

        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode pathMetadata = RequiredMapping(schemas, "PathMetadata");
        YamlMappingNode pathProperties = RequiredMapping(pathMetadata, "properties");
        YamlMappingNode normalizedPath = RequiredMapping(pathProperties, "normalizedPath");
        GetScalar(normalizedPath, "maxLength").ShouldBe("500");
        string pathPattern = GetScalar(normalizedPath, "pattern");
        Regex.IsMatch("Docs/A-1_b.c", pathPattern, RegexOptions.CultureInvariant).ShouldBeTrue();
        foreach (string rejected in new[] { "/docs/a.md", "docs/a.md/", "docs//a.md", "docs/./a.md", "docs/../a.md", "docs/con.txt", "docs/a.", "docs/a./b", "docs/a ", "docs\\a.md", "docs/é.md" })
        {
            Regex.IsMatch(rejected, pathPattern, RegexOptions.CultureInvariant).ShouldBeFalse(rejected);
        }

        YamlMappingNode pathPolicyClass = RequiredMapping(pathProperties, "pathPolicyClass");
        RequiredSequence(pathPolicyClass, "enum").Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
            .ShouldBe(["content_allowed", "metadata_only", "excluded", "restricted"]);
        pathPolicyClass.Children.ContainsKey(new YamlScalarNode("nullable")).ShouldBeFalse("policy class is required and never null");

        YamlMappingNode mutation = RequiredMapping(schemas, "FileMutationRequest");
        GetScalar(RequiredMapping(RequiredMapping(mutation, "properties"), "byteLength"), "maximum").ShouldBe("1048576");
        GetScalar(RequiredMapping(RequiredMapping(schemas, "PutFileStream"), "properties").Children[new YamlScalarNode("declaredLength")].ShouldBeOfType<YamlMappingNode>(), "maximum").ShouldBe("1048576");
        GetScalar(RequiredMapping(RequiredMapping(schemas, "PutFileStream"), "properties").Children[new YamlScalarNode("observedLength")].ShouldBeOfType<YamlMappingNode>(), "maximum").ShouldBe("1048576");

        YamlMappingNode batch = RequiredMapping(schemas, "MutateFilesRequest");
        YamlMappingNode changes = RequiredMapping(RequiredMapping(batch, "properties"), "changes");
        GetScalar(changes, "minItems").ShouldBe("1");
        GetScalar(changes, "maxItems").ShouldBe("100");
        foreach ((string operationId, string schemaName) in new[] { ("AddFile", "AddFileRequest"), ("ChangeFile", "ChangeFileRequest"), ("RemoveFile", "RemoveFileRequest") })
        {
            Operation operation = EnumerateOperations(root).Single(candidate => candidate.OperationId == operationId);
            YamlMappingNode requestSchema = RequiredMapping(RequiredMapping(RequiredMapping(RequiredMapping(operation.Node, "requestBody"), "content"), "application/json"), "schema");
            GetScalar(requestSchema, "$ref").ShouldBe($"#/components/schemas/{schemaName}");
            YamlMappingNode endpointSchema = RequiredMapping(schemas, schemaName);
            string allowedKind = operationId.Replace("File", string.Empty).ToLowerInvariant();
            YamlMappingNode narrowedBranch = RequiredSequence(endpointSchema, "allOf").Children
                .Cast<YamlMappingNode>()
                .Single(branch => branch.Children.ContainsKey(new YamlScalarNode("properties")));
            RequiredSequence(RequiredMapping(RequiredMapping(narrowedBranch, "properties"), "fileOperationKind"), "enum")
                .Children.Cast<YamlScalarNode>().Select(node => node.Value ?? string.Empty)
                .ShouldBe([allowedKind], $"{operationId} must expose only its matching endpoint kind");
        }

        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");
        YamlMappingNode addExample = RequiredMapping(RequiredMapping(examples, "AddFileInlineRequest"), "value");
        YamlMappingNode changeExample = RequiredMapping(RequiredMapping(examples, "ChangeFileStreamRequest"), "value");
        YamlMappingNode removeExample = RequiredMapping(RequiredMapping(examples, "RemoveFileRequest"), "value");
        SchemaAccepts(root, RequiredMapping(schemas, "AddFileRequest"), addExample).ShouldBeTrue("valid inline add branch");
        SchemaAccepts(root, RequiredMapping(schemas, "ChangeFileRequest"), changeExample).ShouldBeTrue("valid streamed change branch");
        SchemaAccepts(root, RequiredMapping(schemas, "RemoveFileRequest"), removeExample).ShouldBeTrue("valid metadata-only removal branch");

        YamlMappingNode incompleteInline = CloneMapping(addExample);
        incompleteInline.Children.Remove(new YamlScalarNode("inlineContent"));
        SchemaAccepts(root, RequiredMapping(schemas, "AddFileRequest"), incompleteInline).ShouldBeFalse("inline conditional requires inlineContent");

        YamlMappingNode forbiddenRemovalEvidence = CloneMapping(removeExample);
        forbiddenRemovalEvidence.Add("byteLength", "0");
        SchemaAccepts(root, RequiredMapping(schemas, "RemoveFileRequest"), forbiddenRemovalEvidence).ShouldBeFalse("removal conditional omits content evidence");

        YamlMappingNode streamBelowBoundary = CloneMapping(changeExample);
        streamBelowBoundary.Children[new YamlScalarNode("byteLength")] = new YamlScalarNode("262144");
        SchemaAccepts(root, RequiredMapping(schemas, "ChangeFileRequest"), streamBelowBoundary).ShouldBeFalse("stream conditional enforces its lower bound");

        Operation rangeRead = EnumerateOperations(root).Single(operation => operation.OperationId == "ReadFileRange");
        YamlMappingNode responses = RequiredMapping(rangeRead.Node, "responses");
        GetScalar(RequiredMapping(responses, "404"), "$ref").ShouldBe("#/components/responses/FileSafeAuthorizationDenial404");
        GetScalar(RequiredMapping(responses, "416"), "$ref").ShouldBe("#/components/responses/FileRangeUnsatisfiable416");

        string[] rangeCategories = RequiredSequence(rangeRead.Node, "x-hexalith-canonical-error-categories")
            .Children.Cast<YamlScalarNode>().Select(node => node.Value ?? string.Empty).ToArray();
        rangeCategories.ShouldContain("range_unsatisfiable");
        rangeCategories.ShouldNotContain("redacted");

        YamlMappingNode safe404 = RequiredMapping(RequiredMapping(RequiredMapping(root, "components"), "examples"), "SafeDenial404NotFound");
        YamlMappingNode safe404Value = RequiredMapping(safe404, "value");
        GetScalar(safe404Value, "status").ShouldBe("404");
        GetScalar(safe404Value, "category").ShouldBe("tenant_access_denied");
        GetScalar(safe404Value, "code").ShouldBe("resource_unavailable");

        string openApi = File.ReadAllText(OpenApiPath);
        openApi.ShouldNotContain("ReadFileRangeRedactedProblem", Case.Sensitive);
        openApi.ShouldNotContain("safe-denial-matrix follow-up", Case.Insensitive);
        openApi.ShouldNotContain("path-policy-class definition story", Case.Insensitive);
        openApi.ShouldNotContain("tenant_sensitive_document", Case.Sensitive);
    }

    [Fact]
    public void Oq2PolicyExtensionClosesGrammarBatchReadabilityRangeAndSnapshotOutcomes()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode policy = RequiredMapping(root, "x-hexalith-file-policy");

        YamlMappingNode grammar = RequiredMapping(policy, "ruleGrammar");
        GetScalar(grammar, "anchoring").ShouldBe("workspace-root");
        GetScalar(grammar, "comparison").ShouldBe("ordinal-ignore-case");
        GetScalar(grammar, "minimumRules").ShouldBe("1");
        GetScalar(grammar, "maximumRules").ShouldBe("100");
        GetScalar(grammar, "minimumRuleCharacters").ShouldBe("1");
        GetScalar(grammar, "maximumRuleCharacters").ShouldBe("256");
        GetScalar(grammar, "maximumAggregateCharacters").ShouldBe("25600");
        GetScalar(grammar, "segmentSeparator").ShouldBe("/");
        GetScalar(grammar, "singleSegmentWildcard").ShouldBe("*");
        GetScalar(grammar, "singleCharacterWildcard").ShouldBe("?");
        GetScalar(grammar, "recursiveWildcard").ShouldBe("complete-segment-only");
        GetScalar(grammar, "recursiveWildcardMatches").ShouldBe("zero-or-more-segments");
        RequiredSequence(grammar, "unsupported").Children.Cast<YamlScalarNode>().Select(node => node.Value)
            .ShouldBe(["escapes", "negation", "character-classes", "braces", "re-inclusion"]);

        YamlMappingNode batch = RequiredMapping(policy, "batchSemantics");
        GetScalar(batch, "requestSchema").ShouldBe("MutateFilesRequest");
        GetScalar(batch, "ordering").ShouldBe("caller-order");
        GetScalar(batch, "operationIdUniqueness").ShouldBe("ordinal-ignore-case");
        GetScalar(batch, "pathUniqueness").ShouldBe("ordinal-ignore-case");
        GetScalar(batch, "removeBytes").ShouldBe("0");
        GetScalar(batch, "publicEndpoints").ShouldBe("matching-one-item-adapters");

        YamlMappingNode inlineEvidence = RequiredMapping(policy, "inlineEvidence");
        GetScalar(inlineEvidence, "decodedByteMaximum").ShouldBe("262144");
        GetScalar(inlineEvidence, "decodedLength").ShouldBe("equals-byteLength");
        GetScalar(inlineEvidence, "trustedHash").ShouldBe("equals-contentHashReference");
        GetScalar(inlineEvidence, "malformedOrMismatched").ShouldBe("content_evidence_invalid");

        YamlMappingNode directTargets = RequiredMapping(policy, "directMetadataTargets");
        GetScalar(directTargets, "mixedHiddenOrMissing").ShouldBe("reject-whole-request");
        GetScalar(directTargets, "visibleSubset").ShouldBe("forbidden");

        YamlMappingNode ranges = RequiredMapping(policy, "rangeRouting");
        GetScalar(ranges, "emptyAtEof").ShouldBe("200");
        GetScalar(ranges, "startBeyondEof").ShouldBe("416");
        GetScalar(ranges, "nonEmptyStartAtEof").ShouldBe("416");
        GetScalar(ranges, "endBeyondEofFromVisibleStart").ShouldBe("206");

        YamlMappingNode snapshot = RequiredMapping(policy, "policySnapshot");
        GetScalar(snapshot, "binding").ShouldBe("immutable-version-and-digest");
        RequiredSequence(snapshot, "recheckBefore").Children.Cast<YamlScalarNode>().Select(node => node.Value)
            .ShouldBe(["atomic-apply", "response-shaping"]);
        GetScalar(snapshot, "driftBehavior").ShouldBe("discard-and-fail-closed");
        YamlMappingNode unavailable = RequiredMapping(snapshot, "unavailableOutcome");
        GetScalar(unavailable, "status").ShouldBe("503");
        GetScalar(unavailable, "category").ShouldBe("file_policy_unavailable");
        GetScalar(unavailable, "code").ShouldBe("file_policy_unavailable");
        GetScalar(unavailable, "retryable").ShouldBe("true");
        GetScalar(unavailable, "clientAction").ShouldBe("retry");

        string[] precedence = RequiredSequence(policy, "contentFailurePrecedence")
            .Children.Cast<YamlMappingNode>()
            .Select(item => $"{GetScalar(item, "status")}:{GetScalar(item, "category")}:{GetScalar(item, "code")}:{GetScalar(item, "retryable")}:{GetScalar(item, "clientAction")}")
            .ToArray();
        precedence.ShouldBe(
        [
            "422:input_limit_exceeded:file_content_limit_exceeded:false:revise_request",
            "413:input_limit_exceeded:d9_inline_limit_exceeded:true:revise_request",
            "400:validation_error:content_evidence_invalid:false:revise_request",
        ]);
    }

    [Fact]
    public void Oq2FileResponsesAndSuccessShapesAreScopedAndClosed()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode components = RequiredMapping(root, "components");
        YamlMappingNode schemas = RequiredMapping(components, "schemas");
        YamlMappingNode responses = RequiredMapping(components, "responses");

        AssertExactProblem(schemas, "FileSafeResourceUnavailableProblem", "404", "tenant_access_denied", "resource_unavailable", "false", "no_action", "Access unavailable", "The requested resource is unavailable.");
        AssertExactProblem(schemas, "FileRangeUnsatisfiableProblem", "416", "range_unsatisfiable", "range_unsatisfiable", "false", "revise_request", "Range unsatisfiable", "The requested byte range cannot be satisfied.");
        AssertExactProblem(schemas, "FilePolicyUnavailableProblem", "503", "file_policy_unavailable", "file_policy_unavailable", "true", "retry", "File policy unavailable", "The file policy cannot be verified for this request.");
        AssertExactProblem(schemas, "FileContentEvidenceInvalidProblem", "400", "validation_error", "content_evidence_invalid", "false", "revise_request", "Content evidence invalid", "The supplied content evidence is not valid.");
        AssertExactProblem(schemas, "FileInlineTransportRequiredProblem", "413", "input_limit_exceeded", "d9_inline_limit_exceeded", "true", "revise_request", "Inline payload too large", "The inline payload exceeds the configured D-9 boundary.");
        AssertExactProblem(schemas, "FileContentLimitExceededProblem", "422", "input_limit_exceeded", "file_content_limit_exceeded", "false", "revise_request", "File content limit exceeded", "The file content exceeds the permitted maximum.");

        RequiredMapping(responses, "SafeAuthorizationDenial404").Children.ShouldNotBeEmpty();
        GetScalar(RequiredMapping(RequiredMapping(RequiredMapping(responses, "SafeAuthorizationDenial404"), "content"), "application/problem+json").Children[new YamlScalarNode("schema")].ShouldBeOfType<YamlMappingNode>(), "$ref")
            .ShouldBe("#/components/schemas/SafeAuthorizationDenial", "the unrelated shared response must remain broad");

        SerializeYaml(RequiredMapping(schemas, "VisiblePathMetadata")).ShouldContain("content_allowed", Case.Sensitive);
        SerializeYaml(RequiredMapping(schemas, "VisiblePathMetadata")).ShouldContain("metadata_only", Case.Sensitive);
        SerializeYaml(RequiredMapping(schemas, "ContentAllowedPathMetadata")).ShouldNotContain("metadata_only", Case.Sensitive);
        YamlMappingNode contentItemOverlay = RequiredSequence(RequiredMapping(schemas, "ContentAllowedFileMetadataItem"), "allOf").Children[1].ShouldBeOfType<YamlMappingNode>();
        RequiredSequence(RequiredMapping(RequiredMapping(contentItemOverlay, "properties"), "kind"), "enum")
            .Children.Cast<YamlScalarNode>().Select(node => node.Value).ShouldBe(["file"]);

        Operation search = EnumerateOperations(root).Single(operation => operation.OperationId == "SearchFolderFiles");
        SerializeYaml(search.Node).ShouldContain("#/components/schemas/FileSearchResult", Case.Sensitive);
        GetScalar(RequiredMapping(RequiredMapping(RequiredMapping(RequiredMapping(search.Node, "responses"), "200"), "content"), "application/json")
            .Children[new YamlScalarNode("examples")].ShouldBeOfType<YamlMappingNode>()
            .Children[new YamlScalarNode("synthetic")].ShouldBeOfType<YamlMappingNode>(), "$ref")
            .ShouldBe("#/components/examples/FileSearchResult");

        Operation tree = EnumerateOperations(root).Single(operation => operation.OperationId == "ListFolderFiles");
        GetScalar(RequiredMapping(RequiredMapping(RequiredMapping(RequiredMapping(tree.Node, "responses"), "200"), "content"), "application/json")
            .Children[new YamlScalarNode("examples")].ShouldBeOfType<YamlMappingNode>()
            .Children[new YamlScalarNode("synthetic")].ShouldBeOfType<YamlMappingNode>(), "$ref")
            .ShouldBe("#/components/examples/FileTreeResult");

        YamlMappingNode searchResult = RequiredMapping(schemas, "FileSearchResult");
        GetScalar(RequiredMapping(RequiredMapping(searchResult, "properties"), "items"), "maxItems").ShouldBe("500");

        YamlMappingNode rangeRequestProperties = RequiredMapping(RequiredMapping(schemas, "FileRangeReadRequest"), "properties");
        GetScalar(RequiredMapping(rangeRequestProperties, "startOffset"), "format").ShouldBe("int64");
        GetScalar(RequiredMapping(rangeRequestProperties, "endOffset"), "format").ShouldBe("int64");
        YamlMappingNode rangeResponses = RequiredMapping(EnumerateOperations(root).Single(operation => operation.OperationId == "ReadFileRange").Node, "responses");
        GetScalar(RequiredMapping(RequiredMapping(RequiredMapping(RequiredMapping(rangeResponses, "200"), "content"), "application/json"), "schema"), "$ref")
            .ShouldBe("#/components/schemas/FileRangeReadCompleteResult");
        GetScalar(RequiredMapping(RequiredMapping(RequiredMapping(RequiredMapping(rangeResponses, "206"), "content"), "application/json"), "schema"), "$ref")
            .ShouldBe("#/components/schemas/FileRangeReadPartialResult");

        YamlMappingNode examples = RequiredMapping(components, "examples");
        SchemaAccepts(root, searchResult, RequiredMapping(RequiredMapping(examples, "FileSearchResult"), "value"))
            .ShouldBeTrue("the bound search example must satisfy the search-only result schema");
        YamlMappingNode truncatedSearchWithoutReason = CloneMapping(RequiredMapping(RequiredMapping(examples, "FileSearchResult"), "value"));
        RequiredMapping(truncatedSearchWithoutReason, "page").Children[new YamlScalarNode("isTruncated")] = new YamlScalarNode("true");
        SchemaAccepts(root, searchResult, truncatedSearchWithoutReason)
            .ShouldBeFalse("a truncated search page must carry its canonical truncation reason");
        YamlMappingNode untruncatedSearchWithReason = CloneMapping(RequiredMapping(RequiredMapping(examples, "FileSearchResult"), "value"));
        RequiredMapping(untruncatedSearchWithReason, "page").Children[new YamlScalarNode("truncatedReason")] = new YamlScalarNode("not_truncated");
        SchemaAccepts(root, searchResult, untruncatedSearchWithReason)
            .ShouldBeFalse("an untruncated search page must omit its optional reason");
        YamlMappingNode truncatedSearchWithNotTruncatedLimitReason = CloneMapping(RequiredMapping(RequiredMapping(examples, "FileSearchResult"), "value"));
        RequiredMapping(truncatedSearchWithNotTruncatedLimitReason, "page").Children[new YamlScalarNode("isTruncated")] = new YamlScalarNode("true");
        RequiredMapping(truncatedSearchWithNotTruncatedLimitReason, "page").Children[new YamlScalarNode("truncatedReason")] = new YamlScalarNode("result_count_limit");
        RequiredMapping(truncatedSearchWithNotTruncatedLimitReason, "limits").Children[new YamlScalarNode("isTruncated")] = new YamlScalarNode("true");
        SchemaAccepts(root, searchResult, truncatedSearchWithNotTruncatedLimitReason)
            .ShouldBeFalse("a truncated search limit must not report not_truncated");
        SchemaAccepts(root, RequiredMapping(schemas, "FileTreeResult"), RequiredMapping(RequiredMapping(examples, "FileTreeResult"), "value"))
            .ShouldBeTrue("the bound tree example must satisfy the tree result schema");
        SchemaAccepts(root, RequiredMapping(schemas, "FileRangeReadCompleteResult"), RequiredMapping(RequiredMapping(examples, "FileRangeReadCompleteResult"), "value"))
            .ShouldBeTrue("the 200 example must satisfy the complete range schema");
        SchemaAccepts(root, RequiredMapping(schemas, "FileRangeReadPartialResult"), RequiredMapping(RequiredMapping(examples, "FileRangeReadPartialResult"), "value"))
            .ShouldBeTrue("the 206 example must satisfy the partial range schema");

        foreach (Operation operation in EnumerateOperations(root).Where(operation => FileContextOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)))
        {
            YamlMappingNode operationResponses = RequiredMapping(operation.Node, "responses");
            GetScalar(RequiredMapping(operationResponses, "404"), "$ref").ShouldBe("#/components/responses/FileSafeAuthorizationDenial404", operation.OperationId);
            string expectedUnavailableResponse = MutatingOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)
                ? "#/components/responses/FileMutationUnavailable503"
                : "#/components/responses/FileContextUnavailable503";
            GetScalar(RequiredMapping(operationResponses, "503"), "$ref").ShouldBe(expectedUnavailableResponse, operation.OperationId);
            RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .Children.Cast<YamlScalarNode>().Select(node => node.Value).ShouldContain("file_policy_unavailable", operation.OperationId);
        }

        YamlMappingNode inlineBytes = RequiredMapping(RequiredMapping(RequiredMapping(schemas, "PutFileInline"), "properties"), "contentBytes");
        GetScalar(inlineBytes, "x-hexalith-decoded-byte-maximum").ShouldBe("262144");
        YamlMappingNode rangeBytes = RequiredMapping(RequiredMapping(RequiredMapping(schemas, "FileRangeReadResult"), "properties"), "contentBytes");
        GetScalar(rangeBytes, "x-hexalith-decoded-byte-maximum").ShouldBe("262144");
        rangeBytes.Children.ContainsKey(new YamlScalarNode("maxBytes")).ShouldBeFalse();
    }

    [Fact]
    public void Oq2SameStatusResponseUnionsPreserveLegacyOutcomesAndFailClosed()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode components = RequiredMapping(root, "components");
        YamlMappingNode schemas = RequiredMapping(components, "schemas");
        YamlMappingNode examples = RequiredMapping(components, "examples");

        foreach (string operationId in new[] { "AddFile", "ChangeFile" })
        {
            YamlMappingNode responses = RequiredMapping(EnumerateOperations(root).Single(operation => operation.OperationId == operationId).Node, "responses");
            GetScalar(RequiredMapping(responses, "400"), "$ref").ShouldBe("#/components/responses/FileContentEvidenceInvalid400", operationId);
            GetScalar(RequiredMapping(responses, "422"), "$ref").ShouldBe("#/components/responses/FileContentLimitExceeded422", operationId);
        }

        foreach (Operation operation in EnumerateOperations(root).Where(operation => MutatingOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)))
        {
            GetScalar(RequiredMapping(RequiredMapping(operation.Node, "responses"), "503"), "$ref")
                .ShouldBe("#/components/responses/FileMutationUnavailable503", operation.OperationId);
        }

        foreach (Operation operation in EnumerateOperations(root).Where(operation => FileContextOperationIds.Contains(operation.OperationId, StringComparer.Ordinal) && !MutatingOperationIds.Contains(operation.OperationId, StringComparer.Ordinal)))
        {
            GetScalar(RequiredMapping(RequiredMapping(operation.Node, "responses"), "503"), "$ref")
                .ShouldBe("#/components/responses/FileContextUnavailable503", operation.OperationId);
        }

        (string Schema, string ExactExample, string LegacyExample)[] cases =
        [
            ("FileContentEvidenceInvalidOrValidationProblem", "FileContentEvidenceInvalidProblem", "FileValidationFailureProblem"),
            ("FileContentLimitExceededOrWorkspaceTransitionProblem", "FileContentLimitExceededProblem", "FileMutationWorkspaceTransitionInvalidProblem"),
            ("FileMutationUnavailableProblem", "FilePolicyUnavailableProblem", "FileMutationReconciliationRequiredProblem"),
            ("FileContextUnavailableProblem", "FilePolicyUnavailableProblem", "ReadModelUnavailable"),
        ];

        foreach ((string schemaName, string exactExample, string legacyExample) in cases)
        {
            YamlMappingNode union = RequiredMapping(schemas, schemaName);
            YamlSequenceNode branches = RequiredSequence(union, "oneOf");
            branches.Children.Count.ShouldBe(2, schemaName);

            YamlMappingNode exact = RequiredMapping(RequiredMapping(examples, exactExample), "value");
            YamlMappingNode legacy = RequiredMapping(RequiredMapping(examples, legacyExample), "value");
            SchemaAccepts(root, union, exact).ShouldBeTrue($"{schemaName} exact branch");
            SchemaAccepts(root, union, legacy).ShouldBeTrue($"{schemaName} legacy branch");
            branches.Children.Count(branch => SchemaAccepts(root, branch.ShouldBeOfType<YamlMappingNode>(), exact)).ShouldBe(1, $"{schemaName} exact disjointness");
            branches.Children.Count(branch => SchemaAccepts(root, branch.ShouldBeOfType<YamlMappingNode>(), legacy)).ShouldBe(1, $"{schemaName} legacy disjointness");

            YamlMappingNode malformedExact = CloneMapping(exact);
            malformedExact.Children[new YamlScalarNode("retryable")] = new YamlScalarNode(GetScalar(exact, "retryable") == "true" ? "false" : "true");
            SchemaAccepts(root, union, malformedExact).ShouldBeFalse($"{schemaName} must not let an exact category/code fall through after another exact field is malformed");

            YamlMappingNode malformedCategory = CloneMapping(exact);
            malformedCategory.Children[new YamlScalarNode("category")] = new YamlScalarNode("internal_error");
            SchemaAccepts(root, union, malformedCategory).ShouldBeFalse($"{schemaName} must not let an exact code fall through with a malformed category");

            if (schemaName != "FileContentEvidenceInvalidOrValidationProblem")
            {
                YamlMappingNode malformedCode = CloneMapping(exact);
                malformedCode.Children[new YamlScalarNode("code")] = new YamlScalarNode("malformed_exact_code");
                SchemaAccepts(root, union, malformedCode).ShouldBeFalse($"{schemaName} must not let its distinct exact category fall through with a malformed code");
            }
        }
    }

    [Fact]
    public void DecodedByteMaximumAcceptsBoundaryAndRejectsOverBoundaryOrMalformedBase64()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode contentBytesSchema = RequiredMapping(RequiredMapping(RequiredMapping(schemas, "PutFileInline"), "properties"), "contentBytes");

        GetScalar(contentBytesSchema, "contentEncoding").ShouldBe("base64");
        GetScalar(contentBytesSchema, "x-hexalith-decoded-byte-maximum").ShouldBe("262144");

        string atBoundary = Convert.ToBase64String(new byte[262144]);
        string overBoundary = Convert.ToBase64String(new byte[262145]);
        atBoundary.Length.ShouldBe(overBoundary.Length, "encoded maxLength alone cannot distinguish these decoded sizes");

        SatisfiesDecodedByteMaximum(contentBytesSchema, atBoundary).ShouldBeTrue();
        SatisfiesDecodedByteMaximum(contentBytesSchema, overBoundary).ShouldBeFalse();
        SatisfiesDecodedByteMaximum(contentBytesSchema, "not-base64%%%").ShouldBeFalse();
    }

    [Fact]
    public void FileMutations_DeclareIdempotencyLockScopeAndD9Transport()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations.Where(o => MutatingOperationIds.Contains(o.OperationId, StringComparer.Ordinal)))
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeTrue(operation.OperationId);
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/FolderId").ShouldBeTrue(operation.OperationId);
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/WorkspaceId").ShouldBeTrue(operation.OperationId);
            parameters.Any(p =>
                GetOptionalScalar(p, "$ref") == "#/components/parameters/TaskId"
                || (GetOptionalScalar(p, "name") == "X-Hexalith-Task-Id" && GetOptionalScalar(p, "required") == "true")).ShouldBeTrue(operation.OperationId);

            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-ttl-tier")).ShouldBeTrue(operation.OperationId);

            string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray(), $"{operation.OperationId} equivalence fields must be lexicographically ordered.");

            string[] expectedEquivalence = operation.OperationId switch
            {
                "AddFile" or "ChangeFile" => new[] { "content_hash_reference", "file_operation_kind", "operation_id", "path_metadata", "path_policy_class", "task_id", "workspace_id" },
                "RemoveFile" => new[] { "file_operation_kind", "operation_id", "path_metadata", "path_policy_class", "task_id", "workspace_id" },
                _ => throw new InvalidOperationException($"Unexpected mutating operation '{operation.OperationId}' — extend the equivalence membership matrix when adding mutations."),
            };
            equivalence.ShouldBe(expectedEquivalence, $"{operation.OperationId} equivalence membership must match the Story 1.5 field list exactly.");
            equivalence.ShouldNotContain("tenant_id", "tenant authority is envelope-derived and must not be client-controlled OpenAPI equivalence.");

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("held workspace lock", Case.Insensitive, operation.OperationId);
            if (operation.OperationId is "AddFile" or "ChangeFile")
            {
                serializedOperation.ShouldContain("PutFileInline", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("PutFileStream", Case.Sensitive, operation.OperationId);
                serializedOperation.ShouldContain("262144", Case.Sensitive, operation.OperationId);
                YamlMappingNode response = RequiredMapping(RequiredMapping(operation.Node, "responses"), "413");
                GetScalar(response, "$ref").ShouldBe("#/components/responses/FileInlineTransportRequired413");
                YamlMappingNode retryResponse = RequiredMapping(
                    RequiredMapping(RequiredMapping(root, "components"), "responses"),
                    "FileInlineTransportRequired413");
                RequiredMapping(retryResponse, "headers").Children.ContainsKey(new YamlScalarNode("X-Hexalith-Retry-Transport"))
                    .ShouldBeTrue(operation.OperationId);
            }

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "workspace_locked", "lock_expired", "lock_not_owned", "authorization_revocation_detected", "path_validation_failed", "file_operation_failed", "state_transition_invalid", "unknown_provider_outcome", "reconciliation_required", "idempotency_conflict" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }

            categories.ShouldContain("file_policy_unavailable", operation.OperationId);
        }
    }

    [Fact]
    public void ContextQueries_OmitIdempotencyAndRequireAuthorizationBeforeObservation()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => FileContextOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations.Where(o => !MutatingOperationIds.Contains(o.OperationId, StringComparer.Ordinal)))
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("tenant access, folder ACL, path policy, sensitivity classification, C4 bounds, then query execution", Case.Sensitive, operation.OperationId);
            serializedOperation.ShouldContain("snapshot_per_task", Case.Sensitive, operation.OperationId);
            serializedOperation.ShouldContain("safe-denial", Case.Insensitive, operation.OperationId);

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "tenant_access_denied", "folder_acl_denied", "path_validation_failed", "input_limit_exceeded", "response_limit_exceeded", "query_timeout", "read_model_unavailable" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }

            categories.ShouldContain("file_policy_unavailable", operation.OperationId);
            categories.ShouldNotContain("redacted", operation.OperationId);

            if (operation.OperationId == "ReadFileRange")
            {
                categories.ShouldContain("range_unsatisfiable", operation.OperationId);
                categories.ShouldNotContain("redacted", operation.OperationId);
            }
        }
    }

    [Fact]
    public void FileContextSchemas_DeclareC4BoundsMetadataOnlyExamplesAndRangeSemantics()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        RequiredMapping(schemas, "PathMetadata");
        RequiredMapping(schemas, "FileMutationRequest");
        RequiredMapping(schemas, "FileMutationAccepted");
        RequiredMapping(schemas, "FileTreeResult");
        RequiredMapping(schemas, "FileMetadataResult");
        RequiredMapping(schemas, "FileSearchRequest");
        RequiredMapping(schemas, "FileGlobRequest");
        RequiredMapping(schemas, "FileRangeReadRequest");
        RequiredMapping(schemas, "FileRangeReadResult");
        RequiredMapping(schemas, "ContextQueryLimitMetadata");

        string serialized = File.ReadAllText(OpenApiPath);
        serialized.ShouldContain("maxItems: 100", Case.Sensitive);
        serialized.ShouldContain("maxResultCount: 2000", Case.Sensitive);
        serialized.ShouldContain("maxResultCount: 500", Case.Sensitive);
        serialized.ShouldContain("x-hexalith-decoded-byte-maximum: 262144", Case.Sensitive);
        serialized.ShouldContain("x-hexalith-response-budget-bytes: 1048576", Case.Sensitive);
        serialized.ShouldContain("x-hexalith-query-timeout-ms: 2000", Case.Sensitive);
        serialized.ShouldContain("TODO(reference-pending): docs/exit-criteria/c4-input-limits.md PM approval state is proposed", Case.Sensitive);

        // Targeted schema assertions (P14): bound specific schema nodes to specific C4 values
        // so substring drift in unrelated schemas does not silently shield this story's bounds.
        YamlMappingNode fileMetadataRequest = RequiredMapping(schemas, "FileMetadataRequest");
        YamlMappingNode paths = RequiredMapping(RequiredMapping(fileMetadataRequest, "properties"), "paths");
        GetScalar(paths, "maxItems").ShouldBe("100");

        YamlMappingNode fileTreeResultSchema = RequiredMapping(schemas, "FileTreeResult");
        YamlMappingNode treeItems = RequiredMapping(RequiredMapping(fileTreeResultSchema, "properties"), "items");
        GetScalar(treeItems, "maxItems").ShouldBe("2000");

        YamlMappingNode fileSearchRequest = RequiredMapping(schemas, "FileSearchRequest");
        YamlMappingNode searchLimit = RequiredMapping(RequiredMapping(fileSearchRequest, "properties"), "limit");
        GetScalar(searchLimit, "maximum").ShouldBe("500");

        YamlMappingNode fileGlobRequest = RequiredMapping(schemas, "FileGlobRequest");
        YamlMappingNode globLimit = RequiredMapping(RequiredMapping(fileGlobRequest, "properties"), "limit");
        GetScalar(globLimit, "maximum").ShouldBe("500");

        YamlMappingNode contextLimits = RequiredMapping(schemas, "ContextQueryLimitMetadata");
        YamlMappingNode actualBytes = RequiredMapping(RequiredMapping(contextLimits, "properties"), "actualBytes");
        GetScalar(actualBytes, "maximum").ShouldBe("1048576");

        YamlMappingNode contentHashReference = RequiredMapping(schemas, "ContentHashReference");
        GetScalar(contentHashReference, "pattern").ShouldStartWith("^hashref_");

        string[] requiredExamples =
        [
            "AddFileInlineRequest",
            "ChangeFileStreamRequest",
            "RemoveFileRequest",
            "FileTreeResult",
            "FileTreeResultTruncated",
            "FileMetadataResult",
            "SearchFolderFilesRequest",
            "GlobFolderFilesRequest",
            "ReadFileRangeMinimumRequest",
            "ReadFileRangeMaximumRequest",
            "ReadFileRangeInvalidReversedProblem",
            "ReadFileRangeOverBoundProblem",
            "ReadFileRangeUnsatisfiableProblem",
            "SafeDenial404NotFound",
            "ContextInputLimitExceededProblem",
        ];

        foreach (string exampleName in requiredExamples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string combinedExamples = string.Join("\n", requiredExamples.Select(name => SerializeYaml(RequiredMapping(examples, name))));
        string[] forbiddenLeakPatterns =
        [
            "diff --git",
            "BEGIN ",
            "-----BEGIN",
            "PRIVATE KEY",
            "ssh-rsa ",
            "xoxb-",
            "xoxp-",
            "ghp_",
            "ghs_",
            "github_pat_",
            "AKIA",
            "AIza",
            "eyJ",
            "BEGIN_PGP",
            "github.com/",
            "gitlab.com/",
            "C:\\",
            "D:\\",
            "/home/",
            "/Users/",
            "matchedLine",
            "snippet",
            "rawSearchText",
            "generatedContext",
            "providerPayload",
        ];
        foreach (string forbidden in forbiddenLeakPatterns)
        {
            combinedExamples.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void FileContextContractNotes_RecordDeferredOwnersAndNegativeScope()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string notes = File.ReadAllText(ContractNotesPath);

        notes.ShouldContain("Story 1.10", Case.Sensitive);
        notes.ShouldContain("Story 1.11", Case.Sensitive);
        notes.ShouldContain("Epic 4", Case.Sensitive);
        notes.ShouldContain("Epic 5", Case.Sensitive);
        notes.ShouldContain("Story 6.6", Case.Sensitive);
        notes.ShouldContain("semantic indexing", Case.Insensitive);
        notes.ShouldContain("deferred", Case.Insensitive);
        notes.ShouldContain("contract-only", Case.Insensitive);

        string[] forbiddenRoots =
        [
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "FileEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Endpoints", "ContextQueryEndpoints.cs"),
            Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Workers", "Providers"),
        ];

        foreach (string forbiddenRoot in forbiddenRoots)
        {
            File.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
            Directory.Exists(forbiddenRoot).ShouldBeFalse(forbiddenRoot);
        }
    }

    private sealed record Operation(string Path, string Method, string OperationId, YamlMappingNode Node, YamlMappingNode PathItem);

    private static void AssertExactProblem(
        YamlMappingNode schemas,
        string schemaName,
        string status,
        string category,
        string code,
        string retryable,
        string clientAction,
        string title,
        string message)
    {
        YamlSequenceNode allOf = RequiredSequence(RequiredMapping(schemas, schemaName), "allOf");
        GetScalar(allOf.Children[0].ShouldBeOfType<YamlMappingNode>(), "$ref").ShouldBe("#/components/schemas/ExactFileProblem", schemaName);
        YamlMappingNode properties = RequiredMapping(allOf.Children[1].ShouldBeOfType<YamlMappingNode>(), "properties");

        RequiredSequence(RequiredMapping(properties, "status"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(status);
        RequiredSequence(RequiredMapping(properties, "category"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(category);
        RequiredSequence(RequiredMapping(properties, "code"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(code);
        RequiredSequence(RequiredMapping(properties, "title"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(title);
        RequiredSequence(RequiredMapping(properties, "message"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(message);
        GetScalar(RequiredMapping(properties, "retryable"), "const").ShouldBe(retryable);
        RequiredSequence(RequiredMapping(properties, "clientAction"), "enum").Children.Single().ShouldBeOfType<YamlScalarNode>().Value.ShouldBe(clientAction);
    }

    private static bool SatisfiesDecodedByteMaximum(YamlMappingNode schema, string encoded)
    {
        if (GetScalar(schema, "contentEncoding") != "base64"
            || !int.TryParse(GetScalar(schema, "x-hexalith-decoded-byte-maximum"), out int maximum))
        {
            return false;
        }

        byte[] buffer = new byte[(encoded.Length / 4 * 3) + 3];
        return Convert.TryFromBase64String(encoded, buffer, out int bytesWritten) && bytesWritten <= maximum;
    }

    private static bool SchemaAccepts(YamlMappingNode root, YamlMappingNode schema, YamlNode instance)
    {
        if (schema.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? referenceNode))
        {
            string reference = referenceNode.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            if (!reference.StartsWith("#/", StringComparison.Ordinal)
                || ResolvePointer(root, reference[2..].Split('/')) is not YamlMappingNode referencedSchema
                || !SchemaAccepts(root, referencedSchema, instance))
            {
                return false;
            }
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("allOf"), out YamlNode? allOfNode)
            && allOfNode.ShouldBeOfType<YamlSequenceNode>().Children.Any(branch => !SchemaAccepts(root, branch.ShouldBeOfType<YamlMappingNode>(), instance)))
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("anyOf"), out YamlNode? anyOfNode)
            && !anyOfNode.ShouldBeOfType<YamlSequenceNode>().Children.Any(branch => SchemaAccepts(root, branch.ShouldBeOfType<YamlMappingNode>(), instance)))
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("oneOf"), out YamlNode? oneOfNode)
            && oneOfNode.ShouldBeOfType<YamlSequenceNode>().Children.Count(branch => SchemaAccepts(root, branch.ShouldBeOfType<YamlMappingNode>(), instance)) != 1)
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("not"), out YamlNode? notNode)
            && SchemaAccepts(root, notNode.ShouldBeOfType<YamlMappingNode>(), instance))
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("if"), out YamlNode? ifNode))
        {
            bool condition = SchemaAccepts(root, ifNode.ShouldBeOfType<YamlMappingNode>(), instance);
            if (condition
                && schema.Children.TryGetValue(new YamlScalarNode("then"), out YamlNode? thenNode)
                && !SchemaAccepts(root, thenNode.ShouldBeOfType<YamlMappingNode>(), instance))
            {
                return false;
            }

            if (!condition
                && schema.Children.TryGetValue(new YamlScalarNode("else"), out YamlNode? elseNode)
                && !SchemaAccepts(root, elseNode.ShouldBeOfType<YamlMappingNode>(), instance))
            {
                return false;
            }
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("type"), out YamlNode? typeNode)
            && !MatchesType(typeNode.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty, instance))
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("enum"), out YamlNode? enumNode)
            && (instance is not YamlScalarNode enumValue
                || !enumNode.ShouldBeOfType<YamlSequenceNode>().OfType<YamlScalarNode>().Any(value => value.Value == enumValue.Value)))
        {
            return false;
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("const"), out YamlNode? constNode)
            && (instance is not YamlScalarNode constValue || constNode.ShouldBeOfType<YamlScalarNode>().Value != constValue.Value))
        {
            return false;
        }

        if (instance is YamlScalarNode scalar)
        {
            string value = scalar.Value ?? string.Empty;
            if (schema.Children.TryGetValue(new YamlScalarNode("minLength"), out YamlNode? minLengthNode)
                && value.Length < int.Parse(minLengthNode.ShouldBeOfType<YamlScalarNode>().Value!))
            {
                return false;
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("maxLength"), out YamlNode? maxLengthNode)
                && value.Length > int.Parse(maxLengthNode.ShouldBeOfType<YamlScalarNode>().Value!))
            {
                return false;
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("pattern"), out YamlNode? patternNode)
                && !Regex.IsMatch(value, patternNode.ShouldBeOfType<YamlScalarNode>().Value!, RegexOptions.CultureInvariant))
            {
                return false;
            }

            if (long.TryParse(value, out long integer))
            {
                if (schema.Children.TryGetValue(new YamlScalarNode("minimum"), out YamlNode? minimumNode)
                    && integer < long.Parse(minimumNode.ShouldBeOfType<YamlScalarNode>().Value!))
                {
                    return false;
                }

                if (schema.Children.TryGetValue(new YamlScalarNode("maximum"), out YamlNode? maximumNode)
                    && integer > long.Parse(maximumNode.ShouldBeOfType<YamlScalarNode>().Value!))
                {
                    return false;
                }
            }
        }

        if (instance is YamlMappingNode instanceMapping)
        {
            if (schema.Children.TryGetValue(new YamlScalarNode("required"), out YamlNode? requiredNode)
                && requiredNode.ShouldBeOfType<YamlSequenceNode>().OfType<YamlScalarNode>()
                    .Any(required => !instanceMapping.Children.ContainsKey(new YamlScalarNode(required.Value))))
            {
                return false;
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("dependentRequired"), out YamlNode? dependentRequiredNode))
            {
                foreach (KeyValuePair<YamlNode, YamlNode> dependency in dependentRequiredNode.ShouldBeOfType<YamlMappingNode>().Children)
                {
                    if (instanceMapping.Children.ContainsKey(dependency.Key)
                        && dependency.Value.ShouldBeOfType<YamlSequenceNode>().OfType<YamlScalarNode>()
                            .Any(required => !instanceMapping.Children.ContainsKey(new YamlScalarNode(required.Value))))
                    {
                        return false;
                    }
                }
            }

            YamlMappingNode? properties = schema.Children.TryGetValue(new YamlScalarNode("properties"), out YamlNode? propertiesNode)
                ? propertiesNode.ShouldBeOfType<YamlMappingNode>()
                : null;
            if (properties is not null)
            {
                foreach (KeyValuePair<YamlNode, YamlNode> property in properties.Children)
                {
                    if (instanceMapping.Children.TryGetValue(property.Key, out YamlNode? propertyValue)
                        && !SchemaAccepts(root, property.Value.ShouldBeOfType<YamlMappingNode>(), propertyValue))
                    {
                        return false;
                    }
                }
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("additionalProperties"), out YamlNode? additionalProperties))
            {
                foreach (KeyValuePair<YamlNode, YamlNode> property in instanceMapping.Children.Where(property => properties is null || !properties.Children.ContainsKey(property.Key)))
                {
                    if (additionalProperties is YamlScalarNode { Value: "false" }
                        || (additionalProperties is YamlMappingNode additionalSchema && !SchemaAccepts(root, additionalSchema, property.Value)))
                    {
                        return false;
                    }
                }
            }
        }
        else if (instance is YamlSequenceNode instanceSequence)
        {
            if (schema.Children.TryGetValue(new YamlScalarNode("minItems"), out YamlNode? minItemsNode)
                && instanceSequence.Children.Count < int.Parse(minItemsNode.ShouldBeOfType<YamlScalarNode>().Value!))
            {
                return false;
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("maxItems"), out YamlNode? maxItemsNode)
                && instanceSequence.Children.Count > int.Parse(maxItemsNode.ShouldBeOfType<YamlScalarNode>().Value!))
            {
                return false;
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("uniqueItems"), out YamlNode? uniqueItemsNode)
                && uniqueItemsNode.ShouldBeOfType<YamlScalarNode>().Value == "true"
                && instanceSequence.Children.Select(SerializeYaml).Distinct(StringComparer.Ordinal).Count() != instanceSequence.Children.Count)
            {
                return false;
            }

            int prefixCount = 0;
            if (schema.Children.TryGetValue(new YamlScalarNode("prefixItems"), out YamlNode? prefixItemsNode))
            {
                YamlSequenceNode prefixSchemas = prefixItemsNode.ShouldBeOfType<YamlSequenceNode>();
                prefixCount = prefixSchemas.Children.Count;
                for (int index = 0; index < Math.Min(instanceSequence.Children.Count, prefixCount); index++)
                {
                    if (!SchemaAccepts(root, prefixSchemas.Children[index].ShouldBeOfType<YamlMappingNode>(), instanceSequence.Children[index]))
                    {
                        return false;
                    }
                }
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("items"), out YamlNode? itemsNode))
            {
                if (itemsNode is YamlScalarNode { Value: "false" } && instanceSequence.Children.Count > prefixCount)
                {
                    return false;
                }

                if (itemsNode is YamlMappingNode itemSchema
                    && instanceSequence.Children.Any(item => !SchemaAccepts(root, itemSchema, item)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MatchesType(string type, YamlNode instance) => type switch
    {
        "object" => instance is YamlMappingNode,
        "array" => instance is YamlSequenceNode,
        "integer" => instance is YamlScalarNode integer && long.TryParse(integer.Value, out _),
        "number" => instance is YamlScalarNode number && double.TryParse(number.Value, out _),
        "boolean" => instance is YamlScalarNode boolean && boolean.Value is "true" or "false",
        "string" => instance is YamlScalarNode text && text.Value is not "true" and not "false" && !double.TryParse(text.Value, out _),
        _ => true,
    };

    private static YamlMappingNode CloneMapping(YamlMappingNode source)
    {
        using StringReader reader = new(SerializeYaml(source));
        YamlStream yaml = new();
        yaml.Load(reader);
        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static IEnumerable<Operation> EnumerateOperations(YamlMappingNode root)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            string path = pathEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = methodEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                if (method is "get" or "put" or "post" or "patch" or "delete")
                {
                    YamlMappingNode operation = methodEntry.Value.ShouldBeOfType<YamlMappingNode>();
                    yield return new Operation(path, method, GetScalar(operation, "operationId"), operation, pathItem);
                }
            }
        }
    }

    private static IEnumerable<YamlMappingNode> EnumerateParameters(YamlMappingNode pathItem, YamlMappingNode operation)
    {
        if (pathItem.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? pathParametersNode))
        {
            foreach (YamlNode parameter in pathParametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }

        if (operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
        {
            foreach (YamlNode parameter in parametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }
    }

    private static void ResolveRefs(YamlMappingNode root)
    {
        foreach (string reference in EnumerateRefs(root))
        {
            reference.StartsWith("#/", StringComparison.Ordinal).ShouldBeTrue(reference);
            ResolvePointer(root, reference[2..].Split('/')).ShouldNotBeNull(reference);
        }
    }

    private static IEnumerable<string> EnumerateRefs(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
            {
                if (child.Key is YamlScalarNode { Value: "$ref" } && child.Value is YamlScalarNode reference)
                {
                    yield return reference.Value ?? string.Empty;
                }

                foreach (string nestedReference in EnumerateRefs(child.Value))
                {
                    yield return nestedReference;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (YamlNode child in sequence.Children)
            {
                foreach (string reference in EnumerateRefs(child))
                {
                    yield return reference;
                }
            }
        }
    }

    private static YamlNode? ResolvePointer(YamlNode root, IReadOnlyList<string> segments)
    {
        YamlNode current = root;

        foreach (string segment in segments)
        {
            string key = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current is not YamlMappingNode mapping || !mapping.Children.TryGetValue(new YamlScalarNode(key), out current!))
            {
                return null;
            }
        }

        return current;
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        path.ShouldSatisfyAllConditions(() => File.Exists(path).ShouldBeTrue(path));

        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);

        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
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

    private static string GetScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
    }

    private static string? GetOptionalScalar(YamlMappingNode mapping, string key)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static string SerializeYaml(YamlNode node)
    {
        YamlStream stream = new(new YamlDocument(node));
        using StringWriter writer = new();
        stream.Save(writer, false);

        return writer.ToString();
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            current = parent?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
