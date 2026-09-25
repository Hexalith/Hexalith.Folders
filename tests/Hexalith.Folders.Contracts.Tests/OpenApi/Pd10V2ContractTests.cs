using System.Security.Cryptography;
using System.Text;

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
            categories.ShouldNotContain("folder_acl_denied", operationId);

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
        Sequence(Mapping(effectivePermissions, "x-hexalith-authorization"), "notApplicableScopes")
            .Children.Select(Scalar).ShouldNotContain("task");
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
        categories.ShouldNotContain("folder_acl_denied");

        Sequence(Mapping(schemas, "CliExitCode"), "enum").Children.Select(Scalar).ShouldContain("77");
        Sequence(Mapping(schemas, "McpFailureKind"), "enum").Children.Select(Scalar).ShouldContain("concurrency_conflict");

        YamlMappingNode problem = Mapping(schemas, "ProblemDetails");
        Scalar(problem, "additionalProperties").ShouldBe("false");
        YamlMappingNode details = Mapping(Mapping(problem, "properties"), "details");
        Scalar(details, "additionalProperties").ShouldBe("false");
        Sequence(details, "required").Children.Select(Scalar).ShouldContain("visibility");

        YamlMappingNode responses = Mapping(components, "responses");
        AssertExactProblemResponse(responses, schemas, "AuthenticationFailure401", "AuthenticationFailureProblem", "401", "authentication_failure", "authentication_required", "false", "check_credentials");
        AssertExactProblemResponse(responses, schemas, "SafeDenial404", "SafeDenialProblem", "404", "tenant_access_denied", "resource_unavailable", "false", "no_action");
        AssertExactProblemResponse(responses, schemas, "ProtectedOperationUnavailable503", "AuthorityUnavailableProblem", "503", "read_model_unavailable", "projection_unavailable", "true", "retry");
    }

    [Fact]
    public void DirectCandidateRuntimeProblemsStayInsideTheClosedContractVocabulary()
    {
        YamlMappingNode contract = LoadMapping(V2Path);
        YamlMappingNode schemas = Mapping(Mapping(contract, "components"), "schemas");
        string[] codes = Sequence(Mapping(schemas, "CanonicalErrorCode"), "enum").Children.Select(Scalar).ToArray();
        string[] actions = Sequence(Mapping(Mapping(Mapping(schemas, "ProblemDetails"), "properties"), "clientAction"), "enum")
            .Children.Select(Scalar).ToArray();
        string[] detailKeys = Mapping(Mapping(Mapping(Mapping(schemas, "ProblemDetails"), "properties"), "details"), "properties")
            .Children.Keys.Select(Scalar).ToArray();

        codes.ShouldContain("authentication_required");
        codes.ShouldContain("resource_unavailable");
        codes.ShouldContain("projection_unavailable");
        codes.ShouldContain("c4_input_limit_exceeded");
        string[] runtimeValidationCodes =
        [
            "acl_entry_id_mismatch",
            "cursor_tampered",
            "idempotency_key_not_allowed",
            "invalid_pagination",
            "unsupported_read_consistency",
            "unsupported_request_schema_version",
        ];
        foreach (string code in runtimeValidationCodes)
        {
            codes.ShouldContain(code);
        }
        actions.ShouldContain("check_credentials");
        actions.ShouldContain("no_action");
        actions.ShouldContain("retry");
        actions.ShouldContain("revise_request");
        detailKeys.ShouldContain("visibility");
        detailKeys.ShouldContain("evidenceSource");
        detailKeys.ShouldContain("reasonCategory");
        detailKeys.ShouldContain("retryReasonCode");

        YamlSequenceNode inventory = Sequence(contract, "x-hexalith-runtime-problem-inventory");
        string[] expectedRuntimeTuples =
        [
            "400|validation_error|acl_entry_id_mismatch|false|revise_request|visibility",
            "400|validation_error|content_evidence_invalid|false|revise_request|visibility",
            "400|validation_error|cursor_tampered|false|revise_request|visibility",
            "400|validation_error|filter_not_yet_supported|false|revise_request|todoRef,visibility",
            "400|validation_error|idempotency_key_not_allowed|false|revise_request|visibility",
            "400|validation_error|invalid_pagination|false|revise_request|visibility",
            "400|validation_error|range_reversed|false|revise_request|rangeRule,visibility",
            "400|validation_error|unsupported_archive_reason_code|false|revise_request|visibility",
            "400|validation_error|unsupported_read_consistency|false|revise_request|visibility",
            "400|validation_error|unsupported_request_schema_version|false|revise_request|visibility",
            "400|validation_error|validation_error|false|revise_request|visibility",
            "401|authentication_failure|authentication_required|false|check_credentials|visibility",
            "404|tenant_access_denied|resource_unavailable|false|no_action|visibility",
            "408|query_timeout|c4_query_timeout|false|revise_request|configuredLimit,unit,visibility",
            "408|query_timeout|query_timeout|true|revise_request|visibility",
            "409|dirty_workspace|dirty_workspace|false|contact_operator|visibility",
            "409|duplicate_binding|duplicate_binding|false|revise_request|visibility",
            "409|idempotency_conflict|idempotency_conflict|false|revise_request|visibility",
            "409|idempotency_key_expired|idempotency_key_expired|false|refresh_state_then_submit_with_new_key|visibility",
            "409|lock_conflict|workspace_locked|true|retry|lockStatus,visibility",
            "409|projection_stale|projection_stale|true|retry|visibility",
            "409|reconciliation_required|reconciliation_required|false|wait_for_reconciliation|finalState,visibility",
            "409|reconciliation_required|reconciliation_required|false|wait_for_reconciliation|visibility",
            "409|repository_conflict|repository_conflict|false|revise_request|visibility",
            "409|unknown_provider_outcome|unknown_provider_outcome|false|wait_for_reconciliation|visibility",
            "410|lock_expired|lock_expired|true|retry|leaseStatus,visibility",
            "413|input_limit_exceeded|c4_input_limit_exceeded|false|revise_request|visibility",
            "413|input_limit_exceeded|d9_inline_limit_exceeded|true|revise_request|visibility",
            "413|response_limit_exceeded|c4_response_budget_exceeded|false|revise_request|configuredLimit,unit,visibility",
            "413|response_limit_exceeded|response_limit_exceeded|false|revise_request|visibility",
            "416|range_unsatisfiable|range_unsatisfiable|false|revise_request|visibility",
            "422|commit_failed|commit_failed|false|contact_operator|visibility",
            "422|input_limit_exceeded|c4_input_limit_exceeded|false|revise_request|dimension,visibility",
            "422|input_limit_exceeded|c4_range_limit_exceeded|false|revise_request|configuredLimit,unit,visibility",
            "422|input_limit_exceeded|file_content_limit_exceeded|false|revise_request|visibility",
            "422|input_limit_exceeded|input_limit_exceeded|false|revise_request|visibility",
            "422|provider_readiness_failed|provider_readiness_failed|false|contact_operator|visibility",
            "422|state_transition_invalid|state_transition_invalid|false|revise_request|attemptedTransition,currentState,visibility",
            "422|state_transition_invalid|state_transition_invalid|false|revise_request|visibility",
            "422|unsupported_provider_capability|unsupported_provider_capability|false|contact_operator|visibility",
            "422|workspace_preparation_failed|workspace_preparation_failed|false|revise_request|visibility",
            "423|lock_conflict|workspace_locked|true|retry|lockStatus,visibility",
            "428|authorization_revocation_detected|authorization_revocation_detected|false|contact_operator|currentState,visibility",
            "429|provider_rate_limited|provider_rate_limited|true|retry|visibility",
            "503|file_policy_unavailable|file_policy_unavailable|true|retry|visibility",
            "503|idempotency_admission_unavailable|idempotency_admission_unavailable|true|retry|visibility",
            "503|internal_error|archive_state_unsupported|false|no_action|visibility",
            "503|internal_error|read_model_unavailable|false|no_action|visibility",
            "503|projection_unavailable|projection_unavailable|true|retry|visibility",
            "503|provider_failure_known|provider_failure_known|false|do_not_retry|visibility",
            "503|provider_unavailable|provider_unavailable|true|retry|visibility",
            "503|read_model_unavailable|evidence_unavailable|true|retry|visibility",
            "503|read_model_unavailable|projection_unavailable|true|retry|visibility",
            "503|reconciliation_required|reconciliation_required|false|wait_for_reconciliation|visibility",
            "503|unknown_provider_outcome|unknown_provider_outcome|false|wait_for_reconciliation|finalState,visibility",
            "503|unknown_provider_outcome|unknown_provider_outcome|false|wait_for_reconciliation|visibility",
        ];
        inventory.Children.Cast<YamlMappingNode>()
            .Select(item => string.Join(
                '|',
                Scalar(item, "status"),
                Scalar(item, "category"),
                Scalar(item, "code"),
                Scalar(item, "retryable"),
                Scalar(item, "clientAction"),
                string.Join(',', Sequence(item, "detailKeys").Children.Select(Scalar))))
            .ShouldBe(expectedRuntimeTuples);

        string seam = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Pd10V2CandidateCompatibilitySeam.cs"));
        seam.ShouldContain("code = \"c4_input_limit_exceeded\"");
        seam.ShouldContain("clientAction = \"revise_request\"");
        seam.ShouldNotContain("code = \"input_limit_exceeded\"");
        seam.ShouldNotContain("clientAction = \"fix_request\"");
        seam.Split("details = new { visibility", StringSplitOptions.None).Length.ShouldBe(4,
            "all three direct runtime problem writers must expose only the required visibility detail");
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
            string kind = Scalar(artifact, "kind");
            if (kind == "gitlink")
            {
                Directory.Exists(fullPath).ShouldBeTrue(relativePath);
                string objectId = Scalar(artifact, "git_object");
                objectId.Length.ShouldBe(40, relativePath);
                objectId.Length.ShouldBe(int.Parse(Scalar(artifact, "bytes"), System.Globalization.CultureInfo.InvariantCulture), relativePath);
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(objectId)))
                    .ShouldBe(Scalar(artifact, "sha256"), relativePath);
            }
            else
            {
                kind.ShouldBe("file", relativePath);
                File.Exists(fullPath).ShouldBeTrue(relativePath);
                new FileInfo(fullPath).Length.ShouldBe(long.Parse(Scalar(artifact, "bytes"), System.Globalization.CultureInfo.InvariantCulture), relativePath);
                Hash(fullPath).ShouldBe(Scalar(artifact, "sha256"), relativePath);
            }
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
        ExactLiteral(exact, "status").ShouldBe(status);
        ExactLiteral(exact, "category").ShouldBe(category);
        ExactLiteral(exact, "code").ShouldBe(code);
        ExactLiteral(exact, "retryable").ShouldBe(retryable);
        ExactLiteral(exact, "clientAction").ShouldBe(clientAction);
        ExactLiteral(Mapping(Mapping(exact, "details"), "properties"), "visibility").ShouldBe("redacted");

        YamlMappingNode exactRestriction = Sequence(Mapping(schemas, schemaName), "allOf").Children[1]
            .ShouldBeOfType<YamlMappingNode>();
        string[][] forbiddenInheritedFields = Sequence(Mapping(exactRestriction, "not"), "anyOf").Children
            .Select(branch => Sequence(branch.ShouldBeOfType<YamlMappingNode>(), "required").Children.Select(Scalar).ToArray())
            .ToArray();
        forbiddenInheritedFields.ShouldBe([new[] { "detail" }, new[] { "instance" }]);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string ExactLiteral(YamlMappingNode mapping, string key)
        => Scalar(Sequence(Mapping(mapping, key), "enum").Children.Single());

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
