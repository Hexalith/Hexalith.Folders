using System.Security.Cryptography;
using System.Text;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class Pd10V2CandidateContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CandidatePath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");

    [Fact]
    public void CandidateHasExactOperationAccessStateAndFamilyDenominators()
    {
        YamlMappingNode root = Load(CandidatePath);
        Operation[] operations = Operations(root);
        YamlMappingNode candidate = Mapping(root, "x-hexalith-pd10-candidate");

        Scalar(candidate, "version").ShouldBe("2.0.0");
        Scalar(candidate, "lifecycle").ShouldBe("candidate-awaiting-a6b");
        Scalar(candidate, "productionRouted").ShouldBe("false");
        int.Parse(Scalar(candidate, "operationCount")).ShouldBe(49);
        Values(Sequence(candidate, "accessStates")).ShouldBe(
        [
            "tenant-administrator",
            "tenant-member",
            "delegated-service-agent",
            "tenant-scoped-operator",
            "audit-reviewer",
            "incident-administrator",
            "wrong-tenant",
            "revoked",
            "stale",
            "disabled",
            "unknown",
            "hidden-resource",
            "absent-resource",
            "insufficient-scope",
        ]);
        Values(Sequence(candidate, "protectedOperationFamilies")).ShouldBe(
        [
            "audit-read",
            "console-view",
            "context-read",
            "folder-administration",
            "folder-creation",
            "incident-evidence",
            "index-search",
            "provider-configuration",
            "readiness-and-provider-evidence",
            "status-permission-and-lock-inspection",
            "task-mutation",
        ]);

        operations.Length.ShouldBe(49);
        operations.Select(operation => operation.OperationId).Distinct(StringComparer.Ordinal).Count().ShouldBe(49);
        operations.Select(operation => operation.Identity).Distinct(StringComparer.Ordinal).Count().ShouldBe(49);
        operations.ShouldAllBe(operation => operation.Path.StartsWith("/api/v2", StringComparison.Ordinal));
    }

    [Fact]
    public void CandidateUsesOnlyCanonicalProtectedDenialAndUnavailableSurface()
    {
        YamlMappingNode root = Load(CandidatePath);
        Operation[] operations = Operations(root);
        string[] forbidden = ["not_found", "cross_tenant_access_denied", "audit_access_denied"];

        foreach (Operation operation in operations)
        {
            operation.StatusCodes.ShouldContain("401");
            operation.StatusCodes.ShouldContain("404");
            operation.StatusCodes.ShouldContain("503");
            operation.StatusCodes.ShouldNotContain("403");
            operation.ErrorCategories.ShouldNotContain(category => forbidden.Contains(category, StringComparer.Ordinal));
        }

        YamlMappingNode schema = Mapping(Mapping(Mapping(root, "components"), "schemas"), "ProblemDetails");
        YamlMappingNode details = Mapping(Mapping(schema, "properties"), "details");
        Values(Sequence(details, "required")).ShouldBe(["visibility"]);

        YamlMappingNode vocabulary = Mapping(root, "x-hexalith-closed-error-vocabulary");
        YamlMappingNode schemas = Mapping(Mapping(root, "components"), "schemas");
        string[] cliExitCodes = Values(Sequence(vocabulary, "cliExitCodes"));
        string[] schemaCliExitCodes = Values(Sequence(Mapping(schemas, "CliExitCode"), "enum"));
        cliExitCodes.ShouldBe(schemaCliExitCodes);
        cliExitCodes.ShouldContain("73");
        cliExitCodes.ShouldContain("77");

        string[] mcpFailureKinds = Values(Sequence(vocabulary, "mcpFailureKinds"));
        string[] expectedMcpFailureKinds =
        [
            "usage_error",
            "credential_missing",
            .. operations.SelectMany(operation => operation.ErrorCategories).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ];
        mcpFailureKinds.ShouldBe(expectedMcpFailureKinds);
        mcpFailureKinds.Length.ShouldBe(46);
        mcpFailureKinds.ShouldContain("concurrency_conflict");
        mcpFailureKinds.ShouldNotContain("none");
        Values(Sequence(Mapping(schemas, "McpFailureKind"), "enum")).ShouldContain("concurrency_conflict");
        Values(Sequence(vocabulary, "visibility")).ShouldBe(["redacted", "metadata_only", "unavailable", "absent", "withheld"]);
    }

    [Fact]
    public void CandidateCorrectsFolderAndTaskScopeWithoutRoutingProduction()
    {
        YamlMappingNode root = Load(CandidatePath);
        Operation[] operations = Operations(root);

        operations.Single(operation => operation.OperationId == "GetTaskStatus").Path
            .ShouldBe("/api/v2/folders/{folderId}/tasks/{taskId}/status");
        operations.Single(operation => operation.OperationId == "GetReadinessDiagnostics").Path
            .ShouldBe("/api/v2/folders/{folderId}/ops-console/readiness-diagnostics");
        operations.Single(operation => operation.OperationId == "GetProjectionFreshness").Path
            .ShouldBe("/api/v2/folders/{folderId}/ops-console/projection-freshness");

        Operation effectivePermissions = operations.Single(operation => operation.OperationId == "GetEffectivePermissions");
        effectivePermissions.ParameterReferences.ShouldContain("#/components/parameters/TaskId");

        string program = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Server", "Program.cs"));
        program.ShouldNotContain("/api/v2", Case.Sensitive);
        program.ShouldNotContain("MapPd10V2", Case.Sensitive);
    }

    [Fact]
    public void HistoricalV1SpineRemainsByteStable()
    {
        string path = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
            .ShouldBe("3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2");
    }

    [Fact]
    public void GeneratedClientAndParityOracleConsumeTheCandidate()
    {
        string client = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated", "HexalithFoldersClient.g.cs"));
        client.ShouldContain("api/v2/", Case.Sensitive);
        client.ShouldNotContain("api/v1/", Case.Sensitive);

        YamlSequenceNode parity = LoadSequence(Path.Combine(RepositoryRoot, "tests", "fixtures", "parity-contract.yaml"));
        parity.Children.Count.ShouldBe(49);
        foreach (YamlMappingNode row in parity.Children.Cast<YamlMappingNode>())
        {
            YamlMappingNode transport = Mapping(row, "transport_parity");
            Sequence(transport, "http_status_set").Children.Count.ShouldBeGreaterThan(0);
            Sequence(transport, "error_code_set").Children.Count.ShouldBeGreaterThan(0);
        }
    }

    private static Operation[] Operations(YamlMappingNode root)
    {
        List<Operation> operations = [];
        foreach ((YamlNode pathNode, YamlNode itemNode) in Mapping(root, "paths").Children)
        {
            string path = pathNode.ToString();
            foreach ((YamlNode methodNode, YamlNode operationNode) in ((YamlMappingNode)itemNode).Children)
            {
                string method = methodNode.ToString();
                if (method is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                YamlMappingNode operation = (YamlMappingNode)operationNode;
                operations.Add(new(
                    method,
                    path,
                    Scalar(operation, "operationId"),
                    Values(Sequence(operation, "x-hexalith-canonical-error-categories")),
                    Mapping(operation, "responses").Children.Keys.Select(key => key.ToString()).ToArray(),
                    operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parameters)
                        ? ((YamlSequenceNode)parameters).Children
                            .OfType<YamlMappingNode>()
                            .Select(parameter => parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? reference) ? reference.ToString() : string.Empty)
                            .Where(reference => reference.Length > 0)
                            .ToArray()
                        : []));
            }
        }

        return [.. operations];
    }

    private static YamlMappingNode Load(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static YamlSequenceNode LoadSequence(string path)
    {
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return (YamlSequenceNode)yaml.Documents[0].RootNode;
    }

    private static YamlMappingNode Mapping(YamlMappingNode node, string key) =>
        (YamlMappingNode)node.Children[new YamlScalarNode(key)];

    private static YamlSequenceNode Sequence(YamlMappingNode node, string key) =>
        (YamlSequenceNode)node.Children[new YamlScalarNode(key)];

    private static string Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[new YamlScalarNode(key)]).Value ?? string.Empty;

    private static string[] Values(YamlSequenceNode sequence) =>
        sequence.Children.Select(value => ((YamlScalarNode)value).Value ?? string.Empty).ToArray();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Hexalith.Folders repository root was not found.");
    }

    private sealed record Operation(
        string Method,
        string Path,
        string OperationId,
        IReadOnlyList<string> ErrorCategories,
        IReadOnlyList<string> StatusCodes,
        IReadOnlyList<string> ParameterReferences)
    {
        public string Identity => $"{Method} {Path} {OperationId}";
    }
}
