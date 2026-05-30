using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Static conformance gate for the Story 7.13 consumer references. Every inventory is re-derived from the
/// authoritative source (the OpenAPI Contract Spine, the parity oracle, <c>FoldersExitCodes</c>) and asserted
/// equal to the published docs with exact cardinality, so the docs cannot silently drift from the contract.
/// All assertions route through the same parsers/scanners the negative controls exercise.
/// </summary>
public sealed partial class ConsumerDocsConformanceTests
{
    private const string SpinePath = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml";
    private const string ParityPath = "tests/fixtures/parity-contract.yaml";
    private const string ExitCodesPath = "src/Hexalith.Folders.Cli/FoldersExitCodes.cs";
    private const string ManifestPath = "tests/fixtures/pattern-example-manifest.yaml";
    private const string ArchitecturePath = "_bmad-output/planning-artifacts/architecture.md";

    private const string ApiReferencePath = "docs/sdk/api-reference.md";
    private const string QuickstartPath = "docs/sdk/quickstart.md";
    private const string CliReferencePath = "docs/sdk/cli-reference.md";
    private const string McpReferencePath = "docs/sdk/mcp-reference.md";
    private const string AuthenticationPath = "docs/sdk/authentication.md";
    private const string WorkspaceDiagramPath = "docs/diagrams/workspace-lifecycle.md";
    private const string FileCommitDiagramPath = "docs/diagrams/file-commit-flow.md";
    private const string AuthAclDiagramPath = "docs/diagrams/auth-acl-decision-flow.md";

    private const string GateScriptPath = "tests/tools/run-consumer-docs-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string ReportPath = "_bmad-output/gates/consumer-docs/latest.json";

    private const string GoldenLifecycleExampleId = "consumer-golden-lifecycle-ordering";

    private static readonly string[] RequiredDocs =
    [
        ApiReferencePath,
        QuickstartPath,
        CliReferencePath,
        McpReferencePath,
        AuthenticationPath,
        WorkspaceDiagramPath,
        FileCommitDiagramPath,
        AuthAclDiagramPath,
    ];

    private static readonly string[] PreSdkFailureKinds = ["usage_error", "credential_missing"];

    private static readonly string[] AllowedPlaceholderHostSuffixes =
        [".invalid", ".internal", ".example", ".localhost", ".test"];

    [Fact]
    public void RequiredConsumerReferenceDocsExist()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocExists(doc);
        }
    }

    [Fact]
    public void ApiReferenceOperationAndTagInventoryEqualsSpine()
    {
        (HashSet<string> spineOps, HashSet<string> spineTags) = ParseSpineInventory();

        // Sanity floor: the spine must carry the canonical 47-operation / 9-tag surface, so the equality
        // below cannot pass against an empty or truncated parse.
        spineOps.Count.ShouldBe(47);
        spineTags.Count.ShouldBe(9);

        string doc = ReadText(ApiReferencePath);

        HashSet<string> docOps = new(StringComparer.Ordinal);
        foreach (Match match in DocOperationRow().Matches(doc))
        {
            docOps.Add(match.Groups[1].Value);
        }

        HashSet<string> docTags = new(StringComparer.Ordinal);
        foreach (Match match in DocTagHeading().Matches(doc))
        {
            docTags.Add(match.Groups[1].Value);
        }

        AssertSetEquals(docOps, spineOps, "API reference operation inventory must equal the spine exactly.");
        AssertSetEquals(docTags, spineTags, "API reference tag-group inventory must equal the spine exactly.");
    }

    [Fact]
    public void CliReferenceExitCodeRowsEqualFoldersExitCodes()
    {
        (HashSet<int> codes, Dictionary<string, int> byName) = ParseFoldersExitCodes();
        codes.Count.ShouldBe(14, "FoldersExitCodes must define the canonical 14-code table.");

        string doc = ReadText(CliReferencePath);
        HashSet<int> docCodes = new();
        Dictionary<string, int> docByName = new(StringComparer.Ordinal);
        foreach (Match match in DocExitCodeRow().Matches(doc))
        {
            int code = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            docCodes.Add(code);
            docByName[match.Groups[2].Value] = code;
        }

        AssertSetEquals(docCodes.Select(c => c.ToString(CultureInfo.InvariantCulture)),
            codes.Select(c => c.ToString(CultureInfo.InvariantCulture)),
            "CLI reference exit-code rows must equal the FoldersExitCodes set exactly.");

        // Each documented constant must project to the same code the source declares (no relabeled rows).
        foreach ((string name, int code) in byName)
        {
            docByName.ShouldContainKey(name);
            docByName[name].ShouldBe(code, name);
        }
    }

    [Fact]
    public void McpReferenceEnumeratesAllFortySevenToolsAndTwoResources()
    {
        (HashSet<string> spineOps, _) = ParseSpineInventory();
        HashSet<string> expectedTools = spineOps.Select(Kebab).ToHashSet(StringComparer.Ordinal);
        expectedTools.Count.ShouldBe(47);

        string doc = ReadText(McpReferencePath);

        Dictionary<string, string> docTools = new(StringComparer.Ordinal);
        foreach (Match match in McpToolRow().Matches(doc))
        {
            docTools[match.Groups[1].Value] = match.Groups[2].Value;
        }

        AssertSetEquals(docTools.Keys, expectedTools, "MCP reference must enumerate exactly the 47 spine tools.");

        // Every documented tool name must be the kebab-case of its operationId (no renamed tools).
        foreach ((string tool, string operationId) in docTools)
        {
            Kebab(operationId).ShouldBe(tool, operationId);
            spineOps.ShouldContain(operationId, tool);
        }

        // Exactly two read-only resources with their exact URI templates.
        HashSet<string> resourceNames = new(StringComparer.Ordinal);
        foreach (Match match in McpResourceUri().Matches(doc))
        {
            resourceNames.Add(match.Groups[1].Value);
        }

        AssertSetEquals(resourceNames, ["folder-tree", "audit-trail"], "MCP reference must enumerate exactly 2 resources.");
        doc.ShouldContain("folders://folder-tree/{folderId}/{workspaceId}/{taskId}");
        doc.ShouldContain("folders://audit-trail/{folderId}");
    }

    [Fact]
    public void McpReferenceFailureKindCatalogEqualsOraclePlusPreSdkKinds()
    {
        HashSet<string> oracleKinds = ParseOracleFailureKinds();
        oracleKinds.Count.ShouldBe(43, "The oracle must carry the canonical 43 outcome_mapping failure kinds.");
        oracleKinds.ShouldNotContain("none");

        HashSet<string> expected = new(oracleKinds, StringComparer.Ordinal);
        foreach (string preSdk in PreSdkFailureKinds)
        {
            expected.Add(preSdk);
        }

        expected.Count.ShouldBe(45);

        HashSet<string> docKinds = ParseDocFailureKinds(ReadText(McpReferencePath));
        AssertSetEquals(docKinds, expected, "MCP failure-kind catalog must equal the oracle set plus the 2 pre-SDK kinds.");
    }

    [Fact]
    public void AllPublishedConsumerDocsStayMetadataOnly()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocMetadataOnly(ReadText(doc));
        }
    }

    [Fact]
    public void ConsumerDocsGateScriptFailsClosedAndEmitsBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);
        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "diagnostic_policy",
            "metadata-only",
            "Push-Location",
            "Pop-Location",
            "GATE-VACUOUS",
            "xunit",
            "FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiWireConsumerDocsGate()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-consumer-docs-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        // Lane separation: the static gate belongs to contract-spine, never to a new ci.yml top-level lane.
        string ci = ReadText(".github/workflows/ci.yml");
        ci.ShouldNotContain("run-consumer-docs-gates.ps1", Case.Sensitive);

        string baseline = ReadText(BaselineGatePath);
        baseline.ShouldContain("Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests", Case.Sensitive);
        baseline.ShouldContain("samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj", Case.Sensitive);
    }

    [Fact]
    public void ConsumerDocsLatestReportStaysMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("consumer-docs");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void PatternExampleManifestRegistersCompilableGoldenLifecycleExample()
    {
        YamlMappingNode entry = FindManifestExample(GoldenLifecycleExampleId);
        AssertManifestEntryValid(entry);
        Scalar(entry, "classification").ShouldBe("compilable-csharp");
        File.Exists(RepositoryPath("tests/tools/pattern-examples/ConsumerGoldenLifecycleExample.cs")).ShouldBeTrue();
    }

    [Fact]
    public void ApiReferenceDocumentsSecurityHeadersAndProblemDetailsFromSpine()
    {
        YamlMappingNode root = LoadSingleYamlDocument(SpinePath).ShouldBeOfType<YamlMappingNode>();
        Scalar(Mapping(root, "info"), "title").ShouldBe("Hexalith.Folders API");
        Scalar(Mapping(root, "info"), "version").ShouldBe("v1");

        YamlMappingNode oidc = Mapping(Mapping(Mapping(root, "components"), "securitySchemes"), "oidcBearer");
        Scalar(oidc, "type").ShouldBe("openIdConnect");

        string doc = ReadText(ApiReferencePath);
        foreach (string required in new[]
        {
            "OpenAPI 3.1.0",
            "Hexalith.Folders API",
            "version `v1`",
            "`oidcBearer`",
            "`type: openIdConnect`",
            "`Idempotency-Key`",
            "`X-Correlation-Id`",
            "`X-Hexalith-Task-Id`",
            "non-mutating (`GET`) operations MUST NOT accept `Idempotency-Key`",
            "`ValidateProviderReadiness`",
            "`POST /api/v1/provider-readiness/validations`",
            "`ProblemDetails`",
            "`category`",
            "`code`",
            "`message`",
            "`correlationId`",
            "`taskId`",
            "`retryable`",
            "`clientAction`",
            "`details.visibility`",
        })
        {
            doc.ShouldContain(required, Case.Sensitive);
        }

        YamlSequenceNode problemRequired = Sequence(Mapping(Mapping(Mapping(root, "components"), "schemas"), "ProblemDetails"), "required");
        AssertSetEquals(problemRequired.Children.OfType<YamlScalarNode>().Select(node => node.Value.ShouldNotBeNull()),
            ["type", "title", "status", "category", "code", "message", "correlationId", "retryable", "clientAction", "details"],
            "ProblemDetails required fields must stay aligned with the spine.");

        int idempotencyKeyedOperations = 0;
        int getOperations = 0;
        bool validateProviderReadinessChecked = false;

        foreach ((string method, YamlMappingNode operation) in EnumerateSpineOperations())
        {
            string operationId = Scalar(operation, "operationId");
            HashSet<string> parameterNames = ReadOperationParameterNames(operation);

            if (method == "get")
            {
                getOperations++;
                parameterNames.ShouldNotContain("IdempotencyKey", operationId);
                parameterNames.ShouldNotContain("Idempotency-Key", operationId);
            }

            if (OperationHasRequiredIdempotency(operation))
            {
                idempotencyKeyedOperations++;
                parameterNames.ShouldContain("IdempotencyKey", operationId);
                parameterNames.ShouldContain("CorrelationId", operationId);
                (parameterNames.Contains("TaskId") || parameterNames.Contains("X-Hexalith-Task-Id")).ShouldBeTrue(operationId);
            }

            if (operationId == "ValidateProviderReadiness")
            {
                method.ShouldBe("post");
                OperationHasRequiredIdempotency(operation).ShouldBeFalse(operationId);
                parameterNames.ShouldNotContain("IdempotencyKey", operationId);
                validateProviderReadinessChecked = true;
            }
        }

        idempotencyKeyedOperations.ShouldBeGreaterThan(0);
        getOperations.ShouldBeGreaterThan(0);
        validateProviderReadinessChecked.ShouldBeTrue();
    }

    [Fact]
    public void SdkReferencesPinGoldenLifecycleAndIdempotencyHelperTrap()
    {
        string api = ReadText(ApiReferencePath);
        string quickstart = ReadText(QuickstartPath);
        string example = ReadText("tests/tools/pattern-examples/ConsumerGoldenLifecycleExample.cs");

        string lifecycleSection = SectionFrom(api, "### Golden lifecycle ordering");
        AssertIncreasing(lifecycleSection,
        [
            "ConfigureProviderBinding",
            "ValidateProviderReadiness",
            "CreateRepositoryBackedFolder",
            "PrepareWorkspace",
            "LockWorkspace",
            "UploadFile",
            "CommitWorkspace",
            "GetWorkspaceStatus",
            "ListAuditTrail",
        ]);

        foreach (string operation in new[]
        {
            "ConfigureProviderBindingAsync",
            "ValidateProviderReadinessAsync",
            "CreateRepositoryBackedFolderAsync",
            "PrepareWorkspaceAsync",
            "LockWorkspaceAsync",
            "CommitWorkspaceAsync",
            "GetWorkspaceStatusAsync",
            "ListAuditTrailAsync",
        })
        {
            example.ShouldContain(operation, Case.Sensitive);
        }

        api.ShouldContain("PrepareWorkspaceRequest.ComputeIdempotencyHash(folderId, workspaceId, taskId)", Case.Sensitive);
        api.ShouldContain("`(folderId, workspaceId, taskId)`", Case.Sensitive);
        api.ShouldContain("`(folderId, taskId, workspaceId)`", Case.Sensitive);
        api.ShouldContain("HexalithFoldersGeneratedArtifacts.HelperSchemaVersion", Case.Sensitive);
        quickstart.ShouldContain("HexalithFoldersGeneratedArtifacts.HelperSchemaVersion", Case.Sensitive);
        NormalizeWhitespace(quickstart).ShouldContain("pass spine path order or use named arguments", Case.Sensitive);
    }

    [Fact]
    public void CliReferencePinsCommandGroupsCredentialPrecedenceAndMutationRules()
    {
        string doc = ReadText(CliReferencePath);
        HashSet<string> groups = CliGroupHeading().Matches(doc)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        AssertSetEquals(groups, ["provider", "folder", "workspace", "file", "commit", "context", "audit"],
            "CLI reference must publish exactly the 7 top-level groups.");
        CliCommandRow().Matches(doc).Count.ShouldBe(40, "CLI reference must enumerate every documented leaf command.");

        AssertIncreasing(doc, ["`HEXALITH_TOKEN`", "`~/.hexalith/credentials.json`", "`--token` / `-t`"]);
        foreach (string required in new[]
        {
            "`--base-address`",
            "`HEXALITH_FOLDERS_BASE_ADDRESS`",
            "`--output`",
            "`human`",
            "`json`",
            "`--task-id`",
            "`--idempotency-key`",
            "`--allow-auto-key`",
            "reject `--idempotency-key`",
            "exit `64`",
            "`--request`",
            "inline JSON",
            "`@path`",
            "stdin",
            "`commit create`",
            "System.CommandLine 2.0.8 token table",
        })
        {
            doc.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void McpReferencePinsToolGroupCountsTransportDiscoveryAndFailureDrift()
    {
        string doc = ReadText(McpReferencePath);

        AssertToolCountInSection(doc, "### Provider tools", 4);
        AssertToolCountInSection(doc, "### Folder tools", 11);
        AssertToolCountInSection(doc, "### Workspace tools", 8);
        AssertToolCountInSection(doc, "### File tools", 3);
        AssertToolCountInSection(doc, "### Context tools", 5);
        AssertToolCountInSection(doc, "### Commit tools", 5);
        AssertToolCountInSection(doc, "### Diagnostics tools", 7);
        AssertToolCountInSection(doc, "### Audit tools", 4);

        foreach (string required in new[]
        {
            "Transport: stdio",
            "`stdout` carries the JSON-RPC channel",
            "`stderr` only",
            "Discovery: assembly attributes",
            "`WithToolsFromAssembly()`",
            "`WithResourcesFromAssembly()`",
            "`server-manifest.json`",
            "caller-supplied `idempotencyKey`",
            "no auto-key path",
            "`taskId`",
            "`range_unsatisfiable`",
            "`internal_error`",
            "`unknown_provider_outcome`",
        })
        {
            doc.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void AuthenticationReferencePinsS2ClaimProvenanceAndCredentialSources()
    {
        string doc = ReadText(AuthenticationPath);
        foreach (string handler in new[]
        {
            "samples/Hexalith.Folders.Sample/BearerTokenHandler.cs",
            "src/Hexalith.Folders.Cli/Composition/BearerTokenHandler.cs",
            "src/Hexalith.Folders.Mcp/Composition/BearerTokenHandler.cs",
        })
        {
            doc.ShouldContain(handler, Case.Sensitive);
            File.Exists(RepositoryPath(handler)).ShouldBeTrue(handler);
        }

        foreach (string required in new[]
        {
            "ValidateIssuer = true",
            "ValidateAudience = true",
            "ValidateLifetime = true",
            "ValidateIssuerSigningKey = true",
            "RequireSignedTokens = true",
            "RequireExpirationTime = true",
            "ClockSkew = 30 seconds",
            "10 minutes",
            "1 minute",
            "JWT-only",
            "no token-introspection",
            "`sub`",
            "`eventstore:tenant`",
            "`eventstore:permission`",
            "Comparison inputs only",
            "`Authority`",
            "`MetadataAddress`",
            "`ValidIssuer`",
            "`Audience`",
            "`RequireHttpsMetadata`",
            "`HEXALITH_TOKEN`",
            "`~/.hexalith/credentials.json`",
            "`--token` / `-t`",
            "`api://hexalith-folders-production.invalid`",
        })
        {
            doc.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void LifecycleDiagramsRenderThreeMermaidFencesWithExpectedDiagramTypes()
    {
        Dictionary<string, string> expectedDiagramTypes = new(StringComparer.Ordinal)
        {
            [WorkspaceDiagramPath] = "stateDiagram-v2",
            [FileCommitDiagramPath] = "flowchart TD",
            [AuthAclDiagramPath] = "flowchart TD",
        };

        foreach ((string path, string diagramType) in expectedDiagramTypes)
        {
            string doc = NormalizeLineEndings(ReadText(path));
            CountOccurrences(doc, "```mermaid\n").ShouldBe(1, $"{path} must have exactly one Mermaid fence.");
            doc.ShouldContain($"```mermaid\n{diagramType}", Case.Sensitive);
        }
    }

    [Fact]
    public void WorkspaceLifecycleDiagramDispositionTableMatchesC6StateCatalog()
    {
        Dictionary<string, string> matrix = ParseC6StateDispositions();
        matrix.Count.ShouldBe(11, "C6 must enumerate the canonical 11-state catalog.");

        string diagram = ReadText(WorkspaceDiagramPath);
        foreach ((string state, string disposition) in matrix)
        {
            diagram.ShouldContain($"| `{state}` | {disposition} |", Case.Sensitive);
            diagram.ShouldContain($"state \"{PreferredDispositionLabel(disposition)} · {state}\" as {state}", Case.Sensitive);
        }
    }

    [Fact]
    public void WorkspaceLifecycleDiagramEventLabelsEqualC6EventVocabulary()
    {
        HashSet<string> matrixEvents = ParseC6EventVocabulary();
        matrixEvents.Count.ShouldBe(23, "C6 must enumerate the canonical 23-event vocabulary.");

        string diagram = ReadText(WorkspaceDiagramPath);
        HashSet<string> diagramEvents = StateTransitionEvent().Matches(diagram)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        AssertSetEquals(diagramEvents, matrixEvents,
            "workspace lifecycle diagram transition events must equal the C6 event vocabulary exactly.");
    }

    [Fact]
    public void WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix()
    {
        HashSet<string> matrixEdges = ParseArchitectureC6Transitions();
        matrixEdges.Count.ShouldBe(34, "C6 architecture matrix must enumerate the canonical 34 positive transition edges.");

        string diagram = ReadText(WorkspaceDiagramPath);
        HashSet<string> diagramEdges = DiagramTransitionEdge().Matches(diagram)
            .Select(static match => $"{match.Groups["from"].Value}->{match.Groups["to"].Value}:{match.Groups["event"].Value}")
            .ToHashSet(StringComparer.Ordinal);

        AssertSetEquals(diagramEdges, matrixEdges,
            "workspace lifecycle diagram edges must equal the architecture C6 transition matrix exactly.");
    }

    [Fact]
    public void FileCommitFlowDiagramNodesAreCanonicalSpineOperations()
    {
        (HashSet<string> spineOps, _) = ParseSpineInventory();
        spineOps.Count.ShouldBe(47, "the spine must carry the canonical 47-operation surface.");

        string diagram = ReadText(FileCommitDiagramPath);
        HashSet<string> nodeOps = FlowOperationNode().Matches(diagram)
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        nodeOps.Count.ShouldBe(9, "file-commit flow must trace the 9 canonical commit-path operations.");
        foreach (string op in nodeOps)
        {
            spineOps.ShouldContain(op, $"file-commit flow node '{op}' must be a canonical spine operation.");
        }

        foreach (string required in new[]
        {
            "LockWorkspace",
            "AddFile",
            "ChangeFile",
            "RemoveFile",
            "CommitWorkspace",
            "GetProviderOutcome",
            "GetCommitEvidence",
            "GetReconciliationStatus",
            "GetWorkspaceStatus",
        })
        {
            nodeOps.ShouldContain(required, $"file-commit flow must include the '{required}' node.");
        }
    }

    [Fact]
    public void AuthAclDecisionFlowEncodesFixedSixLayerDenyByDefaultOrder()
    {
        string diagram = ReadText(AuthAclDiagramPath);
        string normalized = NormalizeWhitespace(diagram);
        string expectedOrder = string.Join(" \u2192 ",
        [
            "JWT validation",
            "EventStore claim transform",
            "tenant-access projection freshness",
            "folder ACL",
            "EventStore validator",
            "Dapr deny-by-default policy",
        ]);

        normalized.ShouldContain(expectedOrder, Case.Sensitive);
        foreach (string layerNode in new[]
        {
            "Jwt{\"JWT validation",
            "Claims{\"EventStore claim transform",
            "Freshness{\"Tenant-access projection freshness",
            "Acl{\"Folder ACL evidence",
            "Validator{\"EventStore validator",
            "Dapr{\"Dapr deny-by-default policy",
        })
        {
            diagram.ShouldContain(layerNode, Case.Sensitive);
        }

        diagram.ShouldContain("Deny[\"Deny (safe, metadata-only Problem Details)\"]", Case.Sensitive);
        diagram.ShouldContain("Each layer is **deny-by-default**", Case.Sensitive);
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeConsumerDocsEvidence()
    {
        // 1. Missing doc must fail the existence assertion (same helper the required-docs gate uses).
        Should.Throw<ShouldAssertException>(() => AssertDocExists("docs/sdk/this-doc-does-not-exist.md"));

        // 2. A wrong operation count must fail the inventory-equality assertion in both directions.
        (HashSet<string> spineOps, _) = ParseSpineInventory();
        HashSet<string> missingOne = new(spineOps, StringComparer.Ordinal);
        missingOne.Remove(spineOps.First());
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(missingOne, spineOps, "missing op"));
        HashSet<string> extraOne = new(spineOps, StringComparer.Ordinal) { "PhantomOperation" };
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(extraOne, spineOps, "extra op"));

        // 3. A stale exit-code row (dropped 64, injected 99) must fail the exit-code equality.
        (HashSet<int> codes, _) = ParseFoldersExitCodes();
        HashSet<int> staleCodes = new(codes) { 99 };
        staleCodes.Remove(64);
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(
            staleCodes.Select(c => c.ToString(CultureInfo.InvariantCulture)),
            codes.Select(c => c.ToString(CultureInfo.InvariantCulture)),
            "stale exit code"));

        // 4. Leaked absolute paths, bearer tokens, and non-.invalid issuers must each fail the SAME scanner
        //    that guards the real docs — not a standalone assertion.
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("evidence at /home/runner/work/leaked.txt"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhY3RvciJ9.signaturesegment"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("issuer: https://login.contoso.com/realms/corp"));

        // Approved placeholder forms must pass the same scanner.
        Should.NotThrow(() => AssertDocMetadataOnly(
            "issuer https://oidc.production.invalid/realms/hexalith-folders and dashboard https://localhost:17000"));

        // 5. A malformed manifest entry (synthetic_data_only=false) must fail manifest validation.
        YamlMappingNode malformed = new();
        malformed.Add("example_id", "bad-example");
        malformed.Add("classification", "compilable-csharp");
        malformed.Add("marker", "<!-- hexalith-example: compile-csharp -->");
        malformed.Add("synthetic_data_only", "false");
        malformed.Add("source_path", "tests/tools/pattern-examples/ConsumerGoldenLifecycleExample.cs");
        Should.Throw<ShouldAssertException>(() => AssertManifestEntryValid(malformed));

        // Malformed YAML/JSON must fail to parse rather than pass vacuously.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"consumer-docs\", "));
    }

    private static void AssertDocExists(string relativePath)
        => File.Exists(RepositoryPath(relativePath)).ShouldBeTrue(relativePath);

    private static void AssertSetEquals(IEnumerable<string> actual, IEnumerable<string> expected, string because)
        => actual.OrderBy(static value => value, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(static value => value, StringComparer.Ordinal), because);

    private static IEnumerable<(string Method, YamlMappingNode Operation)> EnumerateSpineOperations()
    {
        YamlMappingNode root = LoadSingleYamlDocument(SpinePath).ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode paths = Mapping(root, "paths");
        HashSet<string> methods = new(["get", "put", "post", "delete", "patch", "head", "options", "trace"], StringComparer.Ordinal);

        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in paths.Children)
        {
            if (pathEntry.Value is not YamlMappingNode pathItem)
            {
                continue;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                if (methodEntry.Key is not YamlScalarNode methodKey
                    || methodKey.Value is null
                    || !methods.Contains(methodKey.Value)
                    || methodEntry.Value is not YamlMappingNode operation)
                {
                    continue;
                }

                yield return (methodKey.Value, operation);
            }
        }
    }

    private static HashSet<string> ReadOperationParameterNames(YamlMappingNode operation)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        if (!operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parameterNode)
            || parameterNode is not YamlSequenceNode parameters)
        {
            return names;
        }

        foreach (YamlMappingNode parameter in parameters.Children.OfType<YamlMappingNode>())
        {
            if (parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? refNode)
                && refNode is YamlScalarNode refScalar
                && refScalar.Value is not null)
            {
                names.Add(refScalar.Value.Split('/').Last());
            }

            if (parameter.Children.TryGetValue(new YamlScalarNode("name"), out YamlNode? nameNode)
                && nameNode is YamlScalarNode nameScalar
                && nameScalar.Value is not null)
            {
                names.Add(nameScalar.Value);
            }
        }

        return names;
    }

    private static bool OperationHasRequiredIdempotency(YamlMappingNode operation)
        => operation.Children.TryGetValue(new YamlScalarNode("x-hexalith-idempotency-key"), out YamlNode? idempotencyNode)
            && idempotencyNode is YamlMappingNode idempotency
            && idempotency.Children.TryGetValue(new YamlScalarNode("required"), out YamlNode? requiredNode)
            && requiredNode is YamlScalarNode required
            && string.Equals(required.Value, "true", StringComparison.OrdinalIgnoreCase);

    private static string SectionFrom(string text, string heading)
    {
        int start = text.IndexOf(heading, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing section heading '{heading}'.");
        int next = text.IndexOf("\n### ", start + heading.Length, StringComparison.Ordinal);
        if (next < 0)
        {
            next = text.Length;
        }

        return text[start..next];
    }

    private static void AssertIncreasing(string text, IReadOnlyList<string> tokens)
    {
        int previous = -1;
        foreach (string token in tokens)
        {
            int current = text.IndexOf(token, previous + 1, StringComparison.Ordinal);
            current.ShouldBeGreaterThan(previous, $"Token '{token}' must appear after the previous token.");
            previous = current;
        }
    }

    private static void AssertToolCountInSection(string doc, string heading, int expectedCount)
        => McpToolRow().Matches(SectionFrom(doc, heading)).Count.ShouldBe(expectedCount, heading);

    private static Dictionary<string, string> ParseC6StateDispositions()
    {
        string matrix = ReadText("docs/exit-criteria/c6-transition-matrix-mapping.md");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match row in C6StateCatalogRow().Matches(matrix))
        {
            result[row.Groups[1].Value] = row.Groups[2].Value.Trim();
        }

        return result;
    }

    private static string PreferredDispositionLabel(string disposition)
    {
        Match match = BacktickToken().Match(disposition);
        return match.Success ? match.Groups[1].Value : disposition;
    }

    private static HashSet<string> ParseC6EventVocabulary()
    {
        string matrix = ReadText("docs/exit-criteria/c6-transition-matrix-mapping.md");
        int line = matrix.IndexOf("event vocabulary copied for drift checking", StringComparison.Ordinal);
        line.ShouldBeGreaterThanOrEqualTo(0, "C6 matrix must declare its event vocabulary line.");
        int contentStart = matrix.IndexOf('`', line);
        contentStart.ShouldBeGreaterThanOrEqualTo(0, "C6 event vocabulary must list backtick-quoted events.");
        int contentEnd = matrix.IndexOf("\n\n", contentStart, StringComparison.Ordinal);
        if (contentEnd < 0)
        {
            contentEnd = matrix.Length;
        }

        return PascalEventToken().Matches(matrix[contentStart..contentEnd])
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ParseArchitectureC6Transitions()
    {
        string architecture = ReadText(ArchitecturePath);
        string section = SectionFrom(architecture, "### Workspace State Transition Matrix (C6 — Enumerated)");
        int tableStart = section.IndexOf("| From → To | Triggering Event | Side Effect |", StringComparison.Ordinal);
        tableStart.ShouldBeGreaterThanOrEqualTo(0, "Architecture C6 transition table must be present.");
        int tableEnd = section.IndexOf("**Implementation enforcement:**", tableStart, StringComparison.Ordinal);
        tableEnd.ShouldBeGreaterThan(tableStart, "Architecture C6 transition table must end before implementation enforcement.");

        HashSet<string> edges = new(StringComparer.Ordinal);
        foreach (Match row in ArchitectureTransitionRow().Matches(section[tableStart..tableEnd]))
        {
            string from = row.Groups["from"].Value == "(none)" ? "[*]" : row.Groups["from"].Value;
            string to = row.Groups["to"].Value;
            foreach (Match eventToken in BacktickToken().Matches(row.Groups["events"].Value))
            {
                string eventName = eventToken.Groups[1].Value;
                if (eventName.Length > 0 && char.IsUpper(eventName[0]))
                {
                    edges.Add($"{from}->{to}:{eventName}");
                }
            }
        }

        return edges;
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string NormalizeLineEndings(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string NormalizeWhitespace(string text)
        => WhitespaceRun().Replace(NormalizeLineEndings(text), " ").Trim();

    private static (HashSet<string> Operations, HashSet<string> Tags) ParseSpineInventory()
    {
        YamlMappingNode root = LoadSingleYamlDocument(SpinePath).ShouldBeOfType<YamlMappingNode>();
        YamlMappingNode paths = Mapping(root, "paths");
        HashSet<string> methods = new(["get", "put", "post", "delete", "patch", "head", "options", "trace"], StringComparer.Ordinal);

        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> tags = new(StringComparer.Ordinal);

        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in paths.Children)
        {
            if (pathEntry.Value is not YamlMappingNode pathItem)
            {
                continue;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                if (methodEntry.Key is not YamlScalarNode methodKey || methodKey.Value is null || !methods.Contains(methodKey.Value))
                {
                    continue;
                }

                if (methodEntry.Value is not YamlMappingNode operation)
                {
                    continue;
                }

                if (operation.Children.TryGetValue(new YamlScalarNode("operationId"), out YamlNode? opId)
                    && opId is YamlScalarNode opIdScalar && opIdScalar.Value is not null)
                {
                    operations.Add(opIdScalar.Value);
                }

                if (operation.Children.TryGetValue(new YamlScalarNode("tags"), out YamlNode? tagNode)
                    && tagNode is YamlSequenceNode tagSequence)
                {
                    foreach (YamlScalarNode tag in tagSequence.Children.OfType<YamlScalarNode>())
                    {
                        if (tag.Value is not null)
                        {
                            tags.Add(tag.Value);
                        }
                    }
                }
            }
        }

        return (operations, tags);
    }

    private static HashSet<string> ParseOracleFailureKinds()
    {
        using StreamReader reader = File.OpenText(RepositoryPath(ParityPath));
        YamlStream stream = new();
        stream.Load(reader);
        YamlSequenceNode operations = stream.Documents[0].RootNode.ShouldBeOfType<YamlSequenceNode>();

        HashSet<string> kinds = new(StringComparer.Ordinal);
        foreach (YamlMappingNode operation in operations.Children.OfType<YamlMappingNode>())
        {
            if (!operation.Children.TryGetValue(new YamlScalarNode("outcome_mapping"), out YamlNode? mappingNode)
                || mappingNode is not YamlSequenceNode outcomeMapping)
            {
                continue;
            }

            foreach (YamlMappingNode row in outcomeMapping.Children.OfType<YamlMappingNode>())
            {
                if (row.Children.TryGetValue(new YamlScalarNode("mcp_failure_kind"), out YamlNode? kindNode)
                    && kindNode is YamlScalarNode kind && kind.Value is not null && kind.Value != "none")
                {
                    kinds.Add(kind.Value);
                }
            }
        }

        return kinds;
    }

    private static (HashSet<int> Codes, Dictionary<string, int> ByName) ParseFoldersExitCodes()
    {
        string source = ReadText(ExitCodesPath);
        HashSet<int> codes = [];
        Dictionary<string, int> byName = new(StringComparer.Ordinal);
        foreach (Match match in ExitCodeConstant().Matches(source))
        {
            int value = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            codes.Add(value);
            byName[match.Groups[1].Value] = value;
        }

        return (codes, byName);
    }

    private static HashSet<string> ParseDocFailureKinds(string doc)
    {
        int marker = doc.IndexOf("<!-- failure-kind-catalog -->", StringComparison.Ordinal);
        marker.ShouldBeGreaterThanOrEqualTo(0, "MCP reference must declare the failure-kind-catalog marker.");
        int fenceStart = doc.IndexOf("```text", marker, StringComparison.Ordinal);
        fenceStart.ShouldBeGreaterThanOrEqualTo(0, "Failure-kind catalog must be a fenced text block.");
        int contentStart = doc.IndexOf('\n', fenceStart) + 1;
        int fenceEnd = doc.IndexOf("```", contentStart, StringComparison.Ordinal);
        fenceEnd.ShouldBeGreaterThanOrEqualTo(0, "Failure-kind catalog fence must be closed.");

        return doc[contentStart..fenceEnd]
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Kebab(string pascalCase)
        => KebabBoundary().Replace(pascalCase, "-$1").ToLowerInvariant();

    private static YamlMappingNode FindManifestExample(string exampleId)
    {
        YamlMappingNode manifest = LoadSingleYamlDocument(ManifestPath).ShouldBeOfType<YamlMappingNode>();
        YamlSequenceNode examples = Sequence(manifest, "examples");
        return examples.Children.OfType<YamlMappingNode>()
            .Single(example => Scalar(example, "example_id") == exampleId);
    }

    private static void AssertManifestEntryValid(YamlMappingNode entry)
    {
        MarkerPattern().IsMatch(Scalar(entry, "marker")).ShouldBeTrue(Scalar(entry, "marker"));
        Scalar(entry, "synthetic_data_only").ShouldBe("true", "Pattern examples must be synthetic-data-only.");
        string classification = Scalar(entry, "classification");
        (classification is "compilable-csharp" or "documentation-only").ShouldBeTrue(classification);
        File.Exists(RepositoryPath(Scalar(entry, "source_path"))).ShouldBeTrue(Scalar(entry, "source_path"));
    }

    private static void AssertDocMetadataOnly(string text)
    {
        HostAbsolutePathPattern().IsMatch(text).ShouldBeFalse($"Consumer doc must not contain a host-absolute path: {Excerpt(text)}");
        SecretMaterialPattern().IsMatch(text).ShouldBeFalse($"Consumer doc must not contain secret/credential material: {Excerpt(text)}");

        foreach (Match match in HttpUrlPattern().Matches(text))
        {
            string host = match.Groups[1].Value;
            int port = host.IndexOf(':');
            if (port >= 0)
            {
                host = host[..port];
            }

            bool allowed = host is "localhost" or "127.0.0.1"
                || AllowedPlaceholderHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal));
            allowed.ShouldBeTrue($"Consumer doc must use .invalid/placeholder issuers, not real host '{host}'.");
        }
    }

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }

                break;
            case JsonValueKind.String:
                AssertDocMetadataOnly(element.GetString().ShouldNotBeNull());
                break;
        }
    }

    private static YamlNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, relativePath);
        return stream.Documents[0].RootNode;
    }

    private static string Scalar(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    private static YamlMappingNode Mapping(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode Sequence(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static string Excerpt(string value) => value.Length <= 80 ? value : value[..80];

    [GeneratedRegex(@"\|\s*`([A-Za-z]+)`\s*\|\s*(?:GET|POST|PUT|PATCH|DELETE)\s*\|\s*`(?:/api/v1[^`]*)`\s*\|")]
    private static partial Regex DocOperationRow();

    [GeneratedRegex(@"### Tag: `([a-z][a-z-]*)`")]
    private static partial Regex DocTagHeading();

    [GeneratedRegex(@"\|\s*`(\d+)`\s*\|\s*`([A-Za-z]+)`\s*\|")]
    private static partial Regex DocExitCodeRow();

    [GeneratedRegex(@"\|\s*`([a-z][a-z0-9-]+)`\s*\|\s*`([A-Z][A-Za-z]+)`\s*\|")]
    private static partial Regex McpToolRow();

    [GeneratedRegex(@"folders://([a-z][a-z-]+)/")]
    private static partial Regex McpResourceUri();

    [GeneratedRegex(@"### `folders ([a-z-]+)`")]
    private static partial Regex CliGroupHeading();

    [GeneratedRegex(@"\|\s*`[a-z]+(?: [a-z-]+)+`\s*\|\s*`[A-Z][A-Za-z]+`\s*\|")]
    private static partial Regex CliCommandRow();

    [GeneratedRegex(@"\|\s*`([a-z_]+)`\s*\|\s*([^|]+?)\s*\|\s*Architecture C6 state catalog", RegexOptions.CultureInvariant)]
    private static partial Regex C6StateCatalogRow();

    [GeneratedRegex(@"`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickToken();

    [GeneratedRegex(@"-->\s*(?:[a-z_]+|\[\*\])\s*:\s*([A-Z][A-Za-z]+)", RegexOptions.CultureInvariant)]
    private static partial Regex StateTransitionEvent();

    [GeneratedRegex(@"^\s*(?<from>\[\*\]|[a-z_]+)\s+-->\s+(?<to>\[\*\]|[a-z_]+)\s*:\s*(?<event>[A-Z][A-Za-z]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DiagramTransitionEdge();

    [GeneratedRegex(@"\|\s*`(?<from>[^`]+)`\s*→\s*`(?<to>[^`]+)`\s*\|\s*(?<events>[^|]+)\|", RegexOptions.CultureInvariant)]
    private static partial Regex ArchitectureTransitionRow();

    [GeneratedRegex("\\[\"([A-Z][A-Za-z]+)\"\\]", RegexOptions.CultureInvariant)]
    private static partial Regex FlowOperationNode();

    [GeneratedRegex(@"`([A-Z][A-Za-z]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex PascalEventToken();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"public const int ([A-Za-z]+) = (\d+);")]
    private static partial Regex ExitCodeConstant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex KebabBoundary();

    [GeneratedRegex(@"^<!-- hexalith-example: [a-z-]+ -->$")]
    private static partial Regex MarkerPattern();

    // The drive-letter clause requires the letter not to be preceded by another letter, so a URL scheme such
    // as "https:/" is not mistaken for a "C:\" Windows path while a real "C:\Users" path still matches.
    [GeneratedRegex(@"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY|AccountKey=|client_secret\s*[:=]\s*\S|xox[baprs]-|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.", RegexOptions.CultureInvariant)]
    private static partial Regex SecretMaterialPattern();

    [GeneratedRegex(@"https?://([^/\s)""'\]]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPattern();
}
