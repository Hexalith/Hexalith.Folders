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
            .ShouldNotContain("#/components/parameters/TaskId");
        Sequence(Mapping(effectivePermissions, "x-hexalith-authorization"), "notApplicableScopes")
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
        YamlMappingNode details = Mapping(Mapping(problem, "properties"), "details");
        Scalar(details, "additionalProperties").ShouldBe("false");
        Sequence(details, "required").Children.Select(Scalar).ShouldContain("visibility");

        YamlMappingNode responses = Mapping(components, "responses");
        AssertExactProblemResponse(responses, schemas, "AuthenticationFailure401", "AuthenticationFailureProblem", "401", "authentication_failure", "authentication_required", "false", "check_credentials");
        AssertExactProblemResponse(responses, schemas, "SafeDenial404", "SafeDenialProblem", "404", "tenant_access_denied", "resource_unavailable", "false", "no_action");
        AssertExactProblemResponse(responses, schemas, "ProtectedOperationUnavailable503", "AuthorityUnavailableProblem", "503", "read_model_unavailable", "projection_unavailable", "true", "retry");
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
