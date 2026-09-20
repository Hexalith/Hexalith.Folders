using System.Security.Cryptography;

using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class Pd10V2ContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string V1Path = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string V2Path = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");
    private static readonly string PreviousSpinePath = Path.Combine(RepositoryRoot, "tests", "fixtures", "previous-spine.yaml");
    private static readonly string ConformanceSetPath = Path.Combine(RepositoryRoot, "_bmad-output", "planning-artifacts", "generated-v2-conformance-set-2026-09-17.yaml");

    [Fact]
    public void V2PublishesExactly49ProtectedOperationsWithClosedPreObservationOutcomes()
    {
        YamlMappingNode root = LoadMapping(V2Path);
        YamlMappingNode paths = Mapping(root, "paths");
        List<(string Path, YamlMappingNode Operation)> operations = [];
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in paths.Children)
        {
            string path = Scalar(pathEntry.Key);
            path.ShouldStartWith("/api/v2/");
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();
            foreach (string method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (pathItem.Children.TryGetValue(new YamlScalarNode(method), out YamlNode? operation))
                {
                    operations.Add((path, operation.ShouldBeOfType<YamlMappingNode>()));
                }
            }
        }

        operations.Count.ShouldBe(49);
        operations.Select(item => Scalar(item.Operation, "operationId")).Distinct(StringComparer.Ordinal).Count().ShouldBe(49);

        foreach ((string _, YamlMappingNode operation) in operations)
        {
            string operationId = Scalar(operation, "operationId");
            string[] statuses = Mapping(operation, "responses").Children.Keys.Select(Scalar).ToArray();
            statuses.ShouldContain("401", operationId);
            statuses.ShouldContain("404", operationId);
            statuses.ShouldContain("503", operationId);
            statuses.ShouldNotContain("403", operationId);

            string[] categories = Sequence(operation, "x-hexalith-canonical-error-categories").Children.Select(Scalar).ToArray();
            categories.ShouldContain("authentication_failure", operationId);
            categories.ShouldContain("tenant_access_denied", operationId);
            categories.ShouldContain("read_model_unavailable", operationId);
            categories.ShouldNotContain("not_found", operationId);
            categories.ShouldNotContain("cross_tenant_access_denied", operationId);
            categories.ShouldNotContain("audit_access_denied", operationId);

            YamlMappingNode authorization = Mapping(operation, "x-hexalith-authorization");
            Scalar(authorization, "candidateVersion").ShouldBe("2.0.0", operationId);
            Scalar(authorization, "operationFamily").ShouldNotBeNullOrWhiteSpace(operationId);
            Sequence(authorization, "evaluationOrder").Children.Select(Scalar).ToArray().ShouldBe(
            [
                "authentication",
                "authority_evidence_availability",
                "tenant_access",
                "principal_and_delegation_intersection",
                "folder_acl_allow",
                "family_grant",
                "resource_scope_binding",
                "freshness_revalidation",
                "observation",
            ], operationId);
            Scalar(authorization, "freshNegativeOutcome").ShouldBe("safe-denial-404", operationId);
            Scalar(authorization, "unusableAuthorityOutcome").ShouldBe("authority-unavailable-503", operationId);
        }
    }

    [Fact]
    public void EveryOperation503ResolvesAuthorityUnavailableAsAStandardDisjointSchemaBranch()
    {
        const string authorityReference = "#/components/schemas/AuthorityUnavailableProblem";
        const string operationSpecificReference = "#/components/schemas/OperationSpecificUnavailableProblem";
        YamlMappingNode historicalRoot = LoadMapping(V1Path);
        YamlMappingNode candidateRoot = LoadMapping(V2Path);
        IReadOnlyDictionary<string, YamlMappingNode> historicalOperations = OperationsById(historicalRoot);
        IReadOnlyDictionary<string, YamlMappingNode> candidateOperations = OperationsById(candidateRoot);
        YamlMappingNode authorityExample = Mapping(
            ResolveMapping(candidateRoot, Mapping(Mapping(Mapping(candidateRoot, "components"), "examples"), "AuthorityUnavailable")),
            "value");

        candidateOperations.Count.ShouldBe(49);
        foreach ((string operationId, YamlMappingNode candidateOperation) in candidateOperations)
        {
            YamlMappingNode candidate503 = Mapping(Mapping(candidateOperation, "responses"), "503");
            candidate503.Children.ContainsKey(new YamlScalarNode("x-hexalith-authority-unavailable-schema"))
                .ShouldBeFalse($"{operationId} must expose authority unavailability through the standard response schema");

            YamlMappingNode resolvedCandidate503 = ResolveMapping(candidateRoot, candidate503);
            resolvedCandidate503.Children.ContainsKey(new YamlScalarNode("x-hexalith-authority-unavailable-schema"))
                .ShouldBeFalse($"{operationId} must not rely on extension-only authority coverage");
            YamlMappingNode candidateSchema = Mapping(
                Mapping(Mapping(resolvedCandidate503, "content"), "application/problem+json"),
                "schema");
            ValidateAgainstSchema(candidateRoot, candidateSchema, authorityExample, operationId + ".responses.503")
                .ShouldBeEmpty($"{operationId} must resolve the exact authority-unavailable envelope through one unambiguous schema branch");

            YamlMappingNode historicalOperation = historicalOperations[operationId];
            YamlMappingNode historicalResponses = Mapping(historicalOperation, "responses");
            if (!historicalResponses.Children.TryGetValue(new YamlScalarNode("503"), out YamlNode? historical503Node))
            {
                Scalar(candidateSchema, "$ref").ShouldBe(authorityReference, operationId);
                continue;
            }

            YamlMappingNode resolvedHistorical503 = ResolveMapping(
                historicalRoot,
                historical503Node.ShouldBeOfType<YamlMappingNode>());
            YamlMappingNode historicalSchema = Mapping(
                Mapping(Mapping(resolvedHistorical503, "content"), "application/problem+json"),
                "schema");
            string historicalSchemaReference = Scalar(historicalSchema, "$ref");

            if (historicalSchemaReference is "#/components/schemas/FileMutationUnavailableProblem"
                or "#/components/schemas/FileContextUnavailableProblem")
            {
                Scalar(candidateSchema, "$ref").ShouldBe(historicalSchemaReference, operationId);
                string[] historicalBranches = DirectReferences(Sequence(ResolveMapping(historicalRoot, historicalSchema), "oneOf"));
                string[] candidateBranches = DirectReferences(Sequence(ResolveMapping(candidateRoot, candidateSchema), "oneOf"));
                candidateBranches.ShouldContain(authorityReference, operationId);
                foreach (string expectedHistoricalBranch in historicalBranches)
                {
                    candidateBranches.ShouldContain(expectedHistoricalBranch, operationId);
                }
                continue;
            }

            historicalSchemaReference.ShouldBe("#/components/schemas/ProblemDetails", operationId);
            Scalar(candidateSchema, "$ref").ShouldBe(operationSpecificReference, operationId);
            YamlSequenceNode union = Sequence(ResolveMapping(candidateRoot, candidateSchema), "oneOf");
            union.Children.Count.ShouldBe(2, operationId);
            DirectReferences(union).ShouldBe([authorityReference], ignoreOrder: false, customMessage: operationId);

            YamlMappingNode historicalBranch = union.Children.Cast<YamlMappingNode>()
                .Single(branch => !branch.Children.ContainsKey(new YamlScalarNode("$ref")));
            YamlSequenceNode historicalAllOf = Sequence(historicalBranch, "allOf");
            DirectReferences(historicalAllOf).ShouldContain(historicalSchemaReference, operationId);
            historicalAllOf.Children.Cast<YamlMappingNode>()
                .Any(branch => branch.Children.TryGetValue(new YamlScalarNode("not"), out YamlNode? notNode)
                    && Scalar(notNode.ShouldBeOfType<YamlMappingNode>(), "$ref") == authorityReference)
                .ShouldBeTrue($"{operationId} historical branch must exclude the exact authority branch");
        }
    }

    [Fact]
    public void FolderScopedDiagnosticsAndTaskStatusCarryExplicitFolderAuthorityAndTaskBinding()
    {
        YamlMappingNode paths = Mapping(LoadMapping(V2Path), "paths");
        paths.Children.Keys.Select(Scalar).ShouldContain("/api/v2/folders/{folderId}/ops-console/readiness-diagnostics");
        paths.Children.Keys.Select(Scalar).ShouldContain("/api/v2/folders/{folderId}/ops-console/projection-freshness");
        string taskPath = "/api/v2/folders/{folderId}/tasks/{taskId}/status";
        paths.Children.Keys.Select(Scalar).ShouldContain(taskPath);

        YamlMappingNode task = Mapping(Mapping(paths, taskPath), "get");
        YamlMappingNode authorization = Mapping(task, "x-hexalith-authorization");
        Scalar(authorization, "taskBinding").ShouldBe("task.folderId == route.folderId");
        Sequence(authorization, "requiredScopes").Children.Select(Scalar).ShouldContain("folder");

        YamlMappingNode effectivePermissions = Mapping(Mapping(paths, "/api/v2/folders/{folderId}/effective-permissions"), "get");
        Sequence(effectivePermissions, "parameters").Children.OfType<YamlMappingNode>()
            .Select(parameter => parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? node) ? Scalar(node) : string.Empty)
            .ShouldContain("#/components/parameters/TaskId");
        Sequence(Mapping(effectivePermissions, "x-hexalith-authorization"), "requiredScopes")
            .Children.Select(Scalar).ShouldContain("task");
    }

    [Fact]
    public void ErrorAndAdapterVocabulariesAreClosedForPd10()
    {
        YamlMappingNode components = Mapping(LoadMapping(V2Path), "components");
        YamlMappingNode schemas = Mapping(components, "schemas");
        string[] categories = Sequence(Mapping(schemas, "CanonicalErrorCategory"), "enum").Children.Select(Scalar).ToArray();
        categories.ShouldContain("concurrency_conflict");
        categories.ShouldNotContain("not_found");
        categories.ShouldNotContain("cross_tenant_access_denied");
        categories.ShouldNotContain("audit_access_denied");

        Sequence(Mapping(schemas, "CliExitCode"), "enum").Children.Select(Scalar).ShouldContain("77");
        Sequence(Mapping(schemas, "McpFailureKind"), "enum").Children.Select(Scalar).ShouldContain("concurrency_conflict");

        YamlMappingNode problem = Mapping(schemas, "ProblemDetails");
        Scalar(problem, "additionalProperties").ShouldBe("false");
        YamlMappingNode problemProperties = Mapping(problem, "properties");
        Scalar(Mapping(problemProperties, "code"), "$ref").ShouldBe("#/components/schemas/CanonicalErrorCode");
        Scalar(Mapping(Mapping(Mapping(schemas, "ExactFileProblem"), "properties"), "code"), "$ref")
            .ShouldBe("#/components/schemas/CanonicalErrorCode");
        Sequence(Mapping(schemas, "CanonicalErrorCode"), "enum").Children.Count.ShouldBeGreaterThan(1);
        YamlMappingNode details = Mapping(problemProperties, "details");
        Scalar(details, "additionalProperties").ShouldBe("false");
        Sequence(details, "required").Children.Select(Scalar).ShouldContain("visibility");

        YamlMappingNode responses = Mapping(components, "responses");
        AssertExactProblemResponse(responses, schemas, "AuthenticationFailure401", "AuthenticationFailureProblem", "401", "authentication_failure", "authentication_required", "false", "check_credentials");
        AssertExactProblemResponse(responses, schemas, "SafeDenial404", "SafeDenialProblem", "404", "tenant_access_denied", "resource_unavailable", "false", "no_action");
        AssertExactProblemResponse(responses, schemas, "ProtectedOperationUnavailable503", "AuthorityUnavailableProblem", "503", "read_model_unavailable", "projection_unavailable", "true", "retry");
    }

    [Fact]
    public void EveryProblemExampleUsesTheClosedProblemVocabularyAndItsResolvedDeclaredSchema()
    {
        YamlMappingNode root = LoadMapping(V2Path);
        YamlMappingNode components = Mapping(root, "components");
        YamlMappingNode componentExamples = Mapping(components, "examples");
        YamlMappingNode problemSchema = Mapping(Mapping(components, "schemas"), "ProblemDetails");

        foreach ((YamlNode nameNode, YamlNode exampleNode) in componentExamples.Children)
        {
            YamlMappingNode example = exampleNode.ShouldBeOfType<YamlMappingNode>();
            if (!example.Children.TryGetValue(new YamlScalarNode("value"), out YamlNode? value)
                || value is not YamlMappingNode valueMapping
                || !valueMapping.Children.ContainsKey(new YamlScalarNode("category")))
            {
                continue;
            }

            ValidateAgainstSchema(root, problemSchema, valueMapping, $"components.examples.{Scalar(nameNode)}")
                .ShouldBeEmpty($"problem component example {Scalar(nameNode)} must satisfy the closed ProblemDetails schema");
        }

        foreach ((string location, YamlMappingNode response) in ProblemResponses(root))
        {
            YamlMappingNode resolvedResponse = ResolveMapping(root, response);
            if (!resolvedResponse.Children.TryGetValue(new YamlScalarNode("content"), out YamlNode? contentNode)
                || contentNode is not YamlMappingNode content
                || !content.Children.TryGetValue(new YamlScalarNode("application/problem+json"), out YamlNode? mediaNode)
                || mediaNode is not YamlMappingNode media
                || !media.Children.TryGetValue(new YamlScalarNode("schema"), out YamlNode? schemaNode)
                || schemaNode is not YamlMappingNode schema
                || !media.Children.TryGetValue(new YamlScalarNode("examples"), out YamlNode? examplesNode)
                || examplesNode is not YamlMappingNode examples)
            {
                continue;
            }

            foreach ((YamlNode exampleNameNode, YamlNode exampleNode) in examples.Children)
            {
                YamlMappingNode resolvedExample = ResolveMapping(root, exampleNode.ShouldBeOfType<YamlMappingNode>());
                YamlNode value = resolvedExample.Children[new YamlScalarNode("value")];
                string label = $"{location}.examples.{Scalar(exampleNameNode)}";
                ValidateAgainstSchema(root, schema, value, label)
                    .ShouldBeEmpty($"{label} must satisfy its resolved application/problem+json response schema");
            }
        }
    }

    [Fact]
    public void PreviousSpinePreservesHistoricalV1DigestAndV2StatusAndErrorFingerprints()
    {
        YamlMappingNode baseline = LoadMapping(PreviousSpinePath);
        Scalar(baseline, "historical_v1_sha256").ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(V1Path))));
        Scalar(baseline, "contract_sha256").ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(V2Path))));

        YamlSequenceNode operations = Sequence(baseline, "operations");
        operations.Children.Count.ShouldBe(49);
        foreach (YamlMappingNode operation in operations.Children.Cast<YamlMappingNode>())
        {
            Sequence(operation, "historical_v1_status_codes").Children.Count.ShouldBeGreaterThan(0);
            Sequence(operation, "historical_v1_canonical_error_categories").Children.Count.ShouldBeGreaterThan(0);
            Scalar(operation, "historical_v1_status_code_fingerprint_sha256").Length.ShouldBe(64);
            Scalar(operation, "historical_v1_error_vocabulary_fingerprint_sha256").Length.ShouldBe(64);
            Sequence(operation, "status_codes").Children.Count.ShouldBeGreaterThan(0);
            Sequence(operation, "canonical_error_categories").Children.Count.ShouldBeGreaterThan(0);
            Scalar(operation, "status_code_fingerprint_sha256").Length.ShouldBe(64);
            Scalar(operation, "error_vocabulary_fingerprint_sha256").Length.ShouldBe(64);
        }
    }

    [Fact]
    public void ConformanceSetBindsEveryCandidateArtifactWithoutChangingReleaseAuthority()
    {
        YamlMappingNode manifest = LoadMapping(ConformanceSetPath);
        Scalar(manifest, "schema_version").ShouldBe("1.0.0");
        Scalar(manifest, "evidence_id").ShouldBe("PD10-V2-CONFORMANCE-SET");
        Scalar(manifest, "candidate_version").ShouldBe("2.0.0");
        Scalar(manifest, "baseline_commit").ShouldBe("3f1056d998ac4688f36eb869c516812c1a4ddb71");
        Scalar(manifest, "approval_status").ShouldBe("pending-a6b");
        Scalar(manifest, "production_exposure").ShouldBe("disabled");
        Scalar(manifest, "story_closure_claimed").ShouldBe("false");

        string historicalV1Path = Scalar(manifest, "historical_v1_path");
        historicalV1Path.ShouldBe("src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml");
        string historicalV1Hash = Scalar(manifest, "historical_v1_sha256");
        historicalV1Hash.ShouldBe("3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2");
        historicalV1Hash.ShouldBe(Hash(Path.Combine(RepositoryRoot, historicalV1Path)));

        YamlSequenceNode artifacts = Sequence(manifest, "artifacts");
        artifacts.Children.Count.ShouldBe(int.Parse(Scalar(manifest, "artifact_count"), System.Globalization.CultureInfo.InvariantCulture));
        artifacts.Children.Count.ShouldBeGreaterThan(0);

        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (YamlMappingNode artifact in artifacts.Children.Cast<YamlMappingNode>())
        {
            string relativePath = Scalar(artifact, "path");
            relativePath.ShouldNotContain("\\");
            Path.IsPathFullyQualified(relativePath).ShouldBeFalse(relativePath);
            relativePath.Split('/').ShouldNotContain("..", relativePath);
            paths.Add(relativePath).ShouldBeTrue($"duplicate conformance path: {relativePath}");

            string fullPath = Path.Combine(RepositoryRoot, relativePath);
            File.Exists(fullPath).ShouldBeTrue(relativePath);
            new FileInfo(fullPath).Length.ShouldBe(long.Parse(Scalar(artifact, "bytes"), System.Globalization.CultureInfo.InvariantCulture), relativePath);
            Hash(fullPath).ShouldBe(Scalar(artifact, "sha256"), relativePath);
        }

        paths.ShouldContain("src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml");
        paths.ShouldContain("docs/contract/authorization-matrix.md");
        paths.ShouldContain("tests/fixtures/parity-contract.yaml");
        paths.ShouldNotContain("_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml");
        paths.ShouldNotContain("_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml");
        paths.ShouldAllBe(path => !path.StartsWith("_bmad-output/implementation-artifacts/spec-", StringComparison.Ordinal));

        string matrixPath = Scalar(manifest, "authorization_matrix_path");
        matrixPath.ShouldBe("docs/contract/authorization-matrix.md");
        Scalar(manifest, "authorization_matrix_sha256").ShouldBe(Hash(Path.Combine(RepositoryRoot, matrixPath)));
    }

    private static void AssertExactProblemResponse(
        YamlMappingNode responses,
        YamlMappingNode schemas,
        string responseName,
        string schemaName,
        string status,
        string category,
        string code,
        string retryable,
        string clientAction)
    {
        YamlMappingNode response = Mapping(responses, responseName);
        YamlMappingNode mediaType = Mapping(Mapping(response, "content"), "application/problem+json");
        Scalar(Mapping(mediaType, "schema"), "$ref").ShouldBe($"#/components/schemas/{schemaName}");

        YamlMappingNode exact = Mapping(Mapping(schemas, schemaName), "x-hexalith-exact-envelope");
        Scalar(Mapping(exact, "status"), "const").ShouldBe(status);
        Scalar(Mapping(exact, "category"), "const").ShouldBe(category);
        Scalar(Mapping(exact, "code"), "const").ShouldBe(code);
        Scalar(Mapping(exact, "retryable"), "const").ShouldBe(retryable);
        Scalar(Mapping(exact, "clientAction"), "const").ShouldBe(clientAction);
        Scalar(Mapping(Mapping(Mapping(exact, "details"), "properties"), "visibility"), "const").ShouldBe("redacted");
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static IEnumerable<(string Location, YamlMappingNode Response)> ProblemResponses(YamlMappingNode root)
    {
        foreach ((YamlNode pathNode, YamlNode itemNode) in Mapping(root, "paths").Children)
        {
            foreach ((YamlNode methodNode, YamlNode operationNode) in itemNode.ShouldBeOfType<YamlMappingNode>().Children)
            {
                string method = Scalar(methodNode);
                if (method is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                YamlMappingNode operation = operationNode.ShouldBeOfType<YamlMappingNode>();
                foreach ((YamlNode statusNode, YamlNode responseNode) in Mapping(operation, "responses").Children)
                {
                    yield return ($"paths.{Scalar(pathNode)}.{method}.responses.{Scalar(statusNode)}", responseNode.ShouldBeOfType<YamlMappingNode>());
                }
            }
        }

        foreach ((YamlNode nameNode, YamlNode responseNode) in Mapping(Mapping(root, "components"), "responses").Children)
        {
            yield return ($"components.responses.{Scalar(nameNode)}", responseNode.ShouldBeOfType<YamlMappingNode>());
        }
    }

    private static IReadOnlyDictionary<string, YamlMappingNode> OperationsById(YamlMappingNode root)
    {
        Dictionary<string, YamlMappingNode> operations = new(StringComparer.Ordinal);
        foreach (YamlMappingNode pathItem in Mapping(root, "paths").Children.Values.Cast<YamlMappingNode>())
        {
            foreach ((YamlNode methodNode, YamlNode operationNode) in pathItem.Children)
            {
                if (Scalar(methodNode) is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                YamlMappingNode operation = operationNode.ShouldBeOfType<YamlMappingNode>();
                operations.Add(Scalar(operation, "operationId"), operation);
            }
        }

        return operations;
    }

    private static string[] DirectReferences(YamlSequenceNode schemas) =>
        schemas.Children
            .Cast<YamlMappingNode>()
            .Where(schema => schema.Children.ContainsKey(new YamlScalarNode("$ref")))
            .Select(schema => Scalar(schema, "$ref"))
            .ToArray();

    private static YamlMappingNode ResolveMapping(YamlMappingNode root, YamlMappingNode node)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? reference))
        {
            return node;
        }

        YamlNode current = root;
        foreach (string segment in Scalar(reference).Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            current = current.ShouldBeOfType<YamlMappingNode>().Children[new YamlScalarNode(segment)];
        }

        return current.ShouldBeOfType<YamlMappingNode>();
    }

    private static string[] ValidateAgainstSchema(YamlMappingNode root, YamlMappingNode schema, YamlNode instance, string location)
    {
        List<string> errors = [];
        ValidateAgainstSchema(root, schema, instance, location, errors, 0);
        return [.. errors];
    }

    private static void ValidateAgainstSchema(
        YamlMappingNode root,
        YamlMappingNode schema,
        YamlNode instance,
        string location,
        List<string> errors,
        int depth)
    {
        if (depth > 32)
        {
            errors.Add($"{location}: schema resolution exceeded 32 levels");
            return;
        }

        if (schema.Children.ContainsKey(new YamlScalarNode("$ref")))
        {
            ValidateAgainstSchema(root, ResolveMapping(root, schema), instance, location, errors, depth + 1);
            return;
        }

        foreach (string composition in new[] { "allOf" })
        {
            if (schema.Children.TryGetValue(new YamlScalarNode(composition), out YamlNode? composed))
            {
                foreach (YamlMappingNode branch in composed.ShouldBeOfType<YamlSequenceNode>().Children.Cast<YamlMappingNode>())
                {
                    ValidateAgainstSchema(root, branch, instance, location, errors, depth + 1);
                }
            }
        }

        foreach (string choice in new[] { "oneOf", "anyOf" })
        {
            if (!schema.Children.TryGetValue(new YamlScalarNode(choice), out YamlNode? choiceNode))
            {
                continue;
            }

            int validBranches = 0;
            foreach (YamlMappingNode branch in choiceNode.ShouldBeOfType<YamlSequenceNode>().Children.Cast<YamlMappingNode>())
            {
                List<string> branchErrors = [];
                ValidateAgainstSchema(root, branch, instance, location, branchErrors, depth + 1);
                if (branchErrors.Count == 0)
                {
                    validBranches++;
                }
            }

            if (validBranches == 0 || (choice == "oneOf" && validBranches != 1))
            {
                errors.Add($"{location}: satisfies {validBranches} {choice} branches");
            }
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("not"), out YamlNode? notNode))
        {
            List<string> notErrors = [];
            ValidateAgainstSchema(root, notNode.ShouldBeOfType<YamlMappingNode>(), instance, location, notErrors, depth + 1);
            if (notErrors.Count == 0)
            {
                errors.Add($"{location}: satisfies forbidden schema");
            }
        }

        string? type = schema.Children.TryGetValue(new YamlScalarNode("type"), out YamlNode? typeNode) ? Scalar(typeNode) : null;
        bool hasObjectKeywords = schema.Children.ContainsKey(new YamlScalarNode("properties"))
            || schema.Children.ContainsKey(new YamlScalarNode("required"))
            || schema.Children.ContainsKey(new YamlScalarNode("additionalProperties"));
        if (type == "object" || hasObjectKeywords)
        {
            if (instance is not YamlMappingNode objectValue)
            {
                errors.Add($"{location}: expected object");
                return;
            }

            HashSet<string> properties = [];
            if (schema.Children.TryGetValue(new YamlScalarNode("properties"), out YamlNode? propertiesNode))
            {
                YamlMappingNode propertySchemas = propertiesNode.ShouldBeOfType<YamlMappingNode>();
                properties.UnionWith(propertySchemas.Children.Keys.Select(Scalar));
                foreach ((YamlNode propertyNameNode, YamlNode propertySchemaNode) in propertySchemas.Children)
                {
                    string propertyName = Scalar(propertyNameNode);
                    if (objectValue.Children.TryGetValue(new YamlScalarNode(propertyName), out YamlNode? propertyValue))
                    {
                        ValidateAgainstSchema(root, propertySchemaNode.ShouldBeOfType<YamlMappingNode>(), propertyValue, $"{location}.{propertyName}", errors, depth + 1);
                    }
                }
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("required"), out YamlNode? requiredNode))
            {
                foreach (string required in requiredNode.ShouldBeOfType<YamlSequenceNode>().Children.Select(Scalar))
                {
                    if (!objectValue.Children.ContainsKey(new YamlScalarNode(required)))
                    {
                        errors.Add($"{location}: missing required property {required}");
                    }
                }
            }

            if (schema.Children.TryGetValue(new YamlScalarNode("additionalProperties"), out YamlNode? additionalNode)
                && Scalar(additionalNode) == "false")
            {
                foreach (string property in objectValue.Children.Keys.Select(Scalar).Except(properties, StringComparer.Ordinal))
                {
                    errors.Add($"{location}: undeclared property {property}");
                }
            }
        }
        else if (type == "array" && instance is not YamlSequenceNode)
        {
            errors.Add($"{location}: expected array");
        }
        else if (type == "boolean" && (instance is not YamlScalarNode booleanNode || !bool.TryParse(booleanNode.Value, out _)))
        {
            errors.Add($"{location}: expected boolean");
        }
        else if (type == "integer" && (instance is not YamlScalarNode integerNode || !long.TryParse(integerNode.Value, System.Globalization.CultureInfo.InvariantCulture, out _)))
        {
            errors.Add($"{location}: expected integer");
        }
        else if (type == "string" && instance is not YamlScalarNode)
        {
            errors.Add($"{location}: expected string");
        }

        string scalarValue = instance is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
        if (schema.Children.TryGetValue(new YamlScalarNode("const"), out YamlNode? constNode)
            && scalarValue != Scalar(constNode))
        {
            errors.Add($"{location}: value '{scalarValue}' does not equal const '{Scalar(constNode)}'");
        }

        if (schema.Children.TryGetValue(new YamlScalarNode("enum"), out YamlNode? enumNode)
            && !enumNode.ShouldBeOfType<YamlSequenceNode>().Children.Select(Scalar).Contains(scalarValue, StringComparer.Ordinal))
        {
            errors.Add($"{location}: value '{scalarValue}' is outside the enum");
        }
    }

    private static YamlMappingNode LoadMapping(string path)
    {
        YamlStream yaml = new();
        using StreamReader reader = File.OpenText(path);
        yaml.Load(reader);
        return yaml.Documents.Single().RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlMappingNode Mapping(YamlMappingNode mapping, string key) =>
        mapping.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlMappingNode>();

    private static YamlSequenceNode Sequence(YamlMappingNode mapping, string key) =>
        mapping.Children[new YamlScalarNode(key)].ShouldBeOfType<YamlSequenceNode>();

    private static string Scalar(YamlMappingNode mapping, string key) => Scalar(mapping.Children[new YamlScalarNode(key)]);

    private static string Scalar(YamlNode node) => node.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Hexalith.Folders repository root.");
    }
}
