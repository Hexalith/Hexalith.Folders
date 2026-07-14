using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Static conformance gate for the Story 7.15 provider integration + canonical error documentation. Every
/// inventory is re-derived from the authoritative source (the provider abstractions, the readiness query
/// sources, the Forgejo supported-version catalog, the generated <c>CanonicalErrorCategory</c> /
/// <c>ProblemDetailsClientAction</c> enums, the parity oracle, <c>FoldersExitCodes</c>, and the MCP
/// <c>FailureKindProjection</c>) and asserted equal to the published docs with exact cardinality, so the docs
/// cannot silently drift. All assertions route through the same scanners the negative controls exercise.
/// </summary>
public sealed partial class ProviderErrorDocsConformanceTests
{
    private const string CredentialModePath = "src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialMode.cs";
    private const string FailureCategoryPath = "src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategory.cs";
    private const string FailureCategoryExtPath = "src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategoryExtensions.cs";
    private const string ReadinessResultPath = "src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessResultCode.cs";
    private const string OperationCatalogPath = "src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs";
    private const string ForgejoCatalogPath = "src/Hexalith.Folders/Providers/Forgejo/ForgejoSupportedVersionCatalog.cs";
    private const string GeneratedClientPath = "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs";
    private const string ParityContractPath = "tests/fixtures/parity-contract.yaml";
    private const string ExitCodesPath = "src/Hexalith.Folders.Cli/FoldersExitCodes.cs";
    private const string FailureKindProjectionPath = "src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs";
    private const string RetryAfterPath = "src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryAfter.cs";
    private const string RetryEligibilityPath = "src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryEligibility.cs";

    private const string ProviderDocPath = "docs/operations/provider-integration-and-testing.md";
    private const string ErrorDocPath = "docs/operations/canonical-error-catalog.md";

    private const string GateScriptPath = "tests/tools/run-provider-error-docs-gates.ps1";
    private const string WorkflowPath = ".github/workflows/contract-spine.yml";
    private const string CiWorkflowPath = ".github/workflows/ci.yml";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string ReportPath = "_bmad-output/gates/provider-error-docs/latest.json";

    private const string ConformanceFqn = "Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests";

    private static readonly string[] RequiredDocs = [ProviderDocPath, ErrorDocPath];

    private static readonly string[] AllowedPlaceholderHostSuffixes =
        [".invalid", ".internal", ".example", ".localhost", ".test"];

    private static readonly string SubmoduleCommand =
        "git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants";

    [Fact]
    public void RequiredProviderErrorDocsExist()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocExists(doc);
        }
    }

    [Fact]
    public void ProviderDocCapabilityOperationInventoryEqualsCatalog()
    {
        HashSet<string> operations = ParseCapabilityOperations();
        operations.Count.ShouldBe(10, "ProviderOperationCatalog must declare exactly 10 canonical operations.");

        HashSet<string> docOps = FirstColumnBacktickTokens(ProviderDocPath, "<!-- provider-capability-operations -->");
        AssertSetEquals(docOps, operations, "provider doc capability operations must equal the catalog exactly.");
    }

    [Fact]
    public void ProviderDocCredentialModeInventoryEqualsEnum()
    {
        HashSet<string> modes = ParseEnumMembers(CredentialModePath);
        modes.Count.ShouldBe(4, "ProviderCredentialMode must define exactly 4 reference-only members.");

        HashSet<string> docModes = FirstColumnBacktickTokens(ProviderDocPath, "<!-- provider-credential-modes -->");
        AssertSetEquals(docModes, modes, "provider doc credential modes must equal ProviderCredentialMode exactly.");

        // Credential references are metadata-only; the safe evidence types must be cited and tokens never.
        string provider = ReadText(ProviderDocPath);
        foreach (string required in new[]
        {
            "never stored secrets",
            "ProviderIdentityIdentifier",
            "ProviderTargetEvidence",
            "DaprProviderCredentialReferenceResolver",
            "reference-only",
        })
        {
            provider.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void ProviderDocReadinessResultCodeInventoryEqualsEnum()
    {
        HashSet<string> codes = ParseEnumMembers(ReadinessResultPath);
        codes.Count.ShouldBe(7, "ProviderReadinessResultCode must define exactly 7 members.");

        HashSet<string> docCodes = FirstColumnBacktickTokens(ProviderDocPath, "<!-- provider-readiness-result-codes -->");
        AssertSetEquals(docCodes, codes, "provider doc readiness result codes must equal ProviderReadinessResultCode exactly.");
    }

    [Fact]
    public void ProviderDocFailureCategoryInventoryEqualsEnumAndRetryability()
    {
        HashSet<string> categories = ParseEnumMembers(FailureCategoryPath);
        categories.Count.ShouldBe(15, "ProviderFailureCategory must define exactly 15 members.");

        HashSet<string> docCategories = FirstColumnBacktickTokens(ProviderDocPath, "<!-- provider-failure-categories -->");
        AssertSetEquals(docCategories, categories, "provider doc failure categories must equal ProviderFailureCategory exactly.");

        // The retryable-by-default trio must be derived from source, not asserted by hand.
        HashSet<string> retryable = ParseRetryableByDefault();
        AssertSetEquals(retryable, ["ProviderUnavailable", "ProviderRateLimited", "ProviderTransientFailure"],
            "IsRetryableByDefault must return exactly the three transient/unavailable/rate-limited categories.");

        string provider = ReadText(ProviderDocPath);
        foreach (string member in retryable)
        {
            provider.ShouldContain(member, Case.Sensitive);
        }

        provider.ShouldContain("Retryable by default", Case.Sensitive);
    }

    [Fact]
    public void ProviderDocForgejoSupportedVersionsEqualCatalogAndPinDrift()
    {
        HashSet<string> versions = ParseForgejoSupportedVersions();
        AssertSetEquals(versions, ["15.0.2", "14.0.5", "11.0.14"],
            "ForgejoSupportedVersionCatalog must pin exactly the three supported versions.");

        HashSet<string> docVersions = FirstColumnBacktickTokens(ProviderDocPath, "<!-- forgejo-supported-versions -->");
        AssertSetEquals(docVersions, versions, "provider doc Forgejo versions must equal the catalog exactly.");

        string provider = ReadText(ProviderDocPath);
        foreach (string required in new[]
        {
            "tests/contracts/forgejo/15.0.2/swagger.v1.json",
            "SchemaDriftBreaking",
            "VersionIncompatible",
            "cannot report ready",
            "typed HTTP",
        })
        {
            provider.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void ProviderDocGitHubBehaviorAndCapabilityDifferencesArePinned()
    {
        string provider = ReadText(ProviderDocPath);
        foreach (string required in new[]
        {
            "OctokitGitHubApiClient",
            "GitHubFailureMapper",
            "GitHubReadinessMapper",
            "implementation detail",
            "N-provider capable",
            "not hardcoded to GitHub plus",
            // Capability-difference dimensions; must never claim parity.
            "not at parity",
            "Supported operations",
            "Branch/ref behavior",
            "Credential mode",
            "Rate-limit posture",
            "Readiness behavior",
            "Drift evidence",
        })
        {
            provider.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void ProviderDocPinsKnownFailureHandlingAndNoSilentRetry()
    {
        string provider = ReadText(ProviderDocPath);
        foreach (string required in new[]
        {
            "timeout",
            "branch protection",
            "stale clone",
            "credential revocation",
            "provider drift",
            "rate limit",
            "unknown_provider_outcome",
            "reconciliation_required",
            "must not silently retry",
        })
        {
            provider.ShouldContain(required, Case.Sensitive);
        }
    }

    [Fact]
    public void CanonicalErrorDocGeneratedCategoryInventoryEqualsClient()
    {
        HashSet<string> generated = ParseGeneratedEnumValues("CanonicalErrorCategory");
        generated.Count.ShouldBe(47, "the generated CanonicalErrorCategory must declare exactly 47 members.");

        HashSet<string> docCategories = FirstColumnBacktickTokens(ErrorDocPath, "<!-- generated-canonical-categories -->");
        AssertSetEquals(docCategories, generated, "error doc generated categories must equal the generated enum exactly.");
    }

    [Fact]
    public void CanonicalErrorDocOracleCategoryInventoryEqualsParityContract()
    {
        HashSet<string> oracle = ParseParityOracleCategories();
        oracle.Count.ShouldBe(43, "the parity oracle must carry exactly 43 distinct canonical categories.");

        HashSet<string> docOracle = FirstColumnBacktickTokens(ErrorDocPath, "<!-- oracle-carried-categories -->");
        AssertSetEquals(docOracle, oracle, "error doc oracle categories must equal the parity oracle exactly.");

        // The four generated categories deliberately outside the oracle path are exactly these.
        HashSet<string> generated = ParseGeneratedEnumValues("CanonicalErrorCategory");
        HashSet<string> outsideOracle = new(generated, StringComparer.Ordinal);
        outsideOracle.ExceptWith(oracle);
        AssertSetEquals(outsideOracle, ["success", "client_configuration_error", "credential_missing", "range_unsatisfiable"],
            "exactly success, client_configuration_error, credential_missing, and range_unsatisfiable sit outside the oracle path.");
    }

    [Fact]
    public void CanonicalErrorDocClientActionTokensEqualGeneratedEnum()
    {
        HashSet<string> actions = ParseGeneratedEnumValues("ProblemDetailsClientAction");
        AssertSetEquals(actions, ["retry", "revise_request", "check_credentials", "wait_for_reconciliation", "contact_operator", "no_action"],
            "ProblemDetailsClientAction must declare exactly the 6 wire tokens.");

        HashSet<string> docActions = FirstColumnBacktickTokens(ErrorDocPath, "<!-- client-action-tokens -->");
        AssertSetEquals(docActions, actions, "error doc client-action tokens must equal the generated enum exactly.");
    }

    [Fact]
    public void CanonicalErrorDocCliExitCodesEqualFoldersExitCodes()
    {
        HashSet<string> codes = ParseCliExitCodes();
        codes.Count.ShouldBe(14, "FoldersExitCodes must declare exactly 14 canonical exit-code values.");

        HashSet<string> docCodes = FirstColumnBacktickTokens(ErrorDocPath, "<!-- cli-exit-codes -->");
        AssertSetEquals(docCodes, codes, "error doc CLI exit codes must equal FoldersExitCodes exactly.");
    }

    [Fact]
    public void CanonicalErrorDocMcpFailureKindProjectionRulesEqualSource()
    {
        HashSet<string> docRules = FirstColumnBacktickTokens(ErrorDocPath, "<!-- mcp-failure-kind-projection -->");
        AssertSetEquals(docRules, ["verbatim", "usage_error", "credential_missing", "range_unsatisfiable"],
            "error doc MCP projection rules must pin verbatim plus the two pre-SDK kinds and the range_unsatisfiable fallback.");

        // The projection source must actually carry the pre-SDK kinds and the documented drift fallback.
        string projection = ReadText(FailureKindProjectionPath);
        foreach (string required in new[]
        {
            "UsageError = \"usage_error\"",
            "CredentialMissing = \"credential_missing\"",
            "range_unsatisfiable",
            "_ => InternalError,",
        })
        {
            projection.ShouldContain(required, Case.Sensitive);
        }

        ReadText(ErrorDocPath).ShouldContain("internal_error", Case.Sensitive);
    }

    [Fact]
    public void CanonicalErrorDocRetryAfterFieldsAreAdvisoryOnly()
    {
        HashSet<string> docFields = FirstColumnBacktickTokens(ErrorDocPath, "<!-- retry-after-advisory-fields -->");
        AssertSetEquals(docFields, ["RetryAfterSeconds", "AdvisoryOnly", "Eligible", "ReasonCode"],
            "error doc retry-after fields must pin the advisory-only signal fields.");

        // Both records must default AdvisoryOnly to true in source.
        ReadText(RetryAfterPath).ShouldContain("AdvisoryOnly = true", Case.Sensitive);
        ReadText(RetryEligibilityPath).ShouldContain("AdvisoryOnly = true", Case.Sensitive);

        string error = ReadText(ErrorDocPath);
        error.ShouldContain("advisory-only", Case.Sensitive);
        error.ShouldContain("mutation, repair, auto-unlock, or implicit retry", Case.Sensitive);
    }

    [Fact]
    public void CanonicalErrorDocCrossLinksConsumerAndOpsDocsWithoutDuplication()
    {
        string error = ReadText(ErrorDocPath);
        foreach (string link in new[]
        {
            "../sdk/api-reference.md",
            "../sdk/cli-reference.md",
            "../sdk/mcp-reference.md",
            "../sdk/authentication.md",
            "audit-and-redaction.md",
            "provider-integration-and-testing.md",
        })
        {
            error.ShouldContain(link, Case.Sensitive);
        }

        // It summarizes; it must not re-author the consumer manuals.
        error.ShouldContain("does **not** re-author", Case.Sensitive);
    }

    [Fact]
    public void AllPublishedProviderErrorDocsStayMetadataOnly()
    {
        foreach (string doc in RequiredDocs)
        {
            AssertDocMetadataOnly(ReadText(doc));
        }

        // Each published doc must carry the operator boilerplate (gate command, metadata-only policy,
        // reviewer/rerun note, and the exact root-level submodule command — never the recursive form).
        foreach (string doc in RequiredDocs)
        {
            string text = ReadText(doc);
            foreach (string required in new[]
            {
                "pwsh ./tests/tools/run-provider-error-docs-gates.ps1",
                ReportPath,
                "metadata-only",
                "reviewer",
                "rerun",
                SubmoduleCommand,
            })
            {
                text.ShouldContain(required, Case.Sensitive);
            }

            AssertNoRecursiveSubmoduleCommand(text);
        }
    }

    [Fact]
    public void ProviderErrorDocsGateScriptFailsClosedAndEmitsBoundedEvidence()
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
            $"FullyQualifiedName~{ConformanceFqn}",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        AssertNoRecursiveSubmoduleCommand(script);
    }

    [Fact]
    public void ContractSpineWorkflowAndBaselineCiWireProviderErrorDocsGate()
    {
        string workflow = ReadText(WorkflowPath);
        workflow.ShouldContain("./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild", Case.Sensitive);
        workflow.ShouldContain("submodules: false", Case.Sensitive);
        workflow.ShouldContain("contents: read", Case.Sensitive);
        AssertNoRecursiveSubmoduleCommand(workflow);

        // The new step must follow the operations-audit-docs step, not broaden a new lane.
        int auditStep = workflow.IndexOf("run-operations-audit-docs-gates.ps1", StringComparison.Ordinal);
        int providerStep = workflow.IndexOf("run-provider-error-docs-gates.ps1", StringComparison.Ordinal);
        auditStep.ShouldBeGreaterThanOrEqualTo(0, "the operations-audit-docs step must remain wired.");
        providerStep.ShouldBeGreaterThan(auditStep, "the provider-error-docs step must come after operations-audit-docs.");

        // Lane separation: the static gate belongs to contract-spine, never to a new ci.yml top-level lane.
        string ci = ReadText(CiWorkflowPath);
        ci.ShouldNotContain("run-provider-error-docs-gates.ps1", Case.Sensitive);

        string baseline = ReadText(BaselineGatePath);
        baseline.ShouldContain(ConformanceFqn, Case.Sensitive);
    }

    [Fact]
    public void ProviderErrorDocsLatestReportStaysMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("provider-error-docs");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeProviderErrorDocsEvidence()
    {
        // 1. Missing doc must fail the existence assertion.
        Should.Throw<ShouldAssertException>(() => AssertDocExists("docs/operations/this-doc-does-not-exist.md"));

        // 2. A wrong inventory count must fail the equality assertion in both directions, using the real source.
        HashSet<string> categories = ParseEnumMembers(FailureCategoryPath);
        HashSet<string> missingOne = new(categories, StringComparer.Ordinal);
        missingOne.Remove(categories.First());
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(missingOne, categories, "missing failure category"));
        HashSet<string> extraOne = new(categories, StringComparer.Ordinal) { "PhantomCategory" };
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(extraOne, categories, "extra failure category"));

        // 3. A stale generated-category inventory (a real member dropped) must fail equality against the source-derived set.
        HashSet<string> generated = ParseGeneratedEnumValues("CanonicalErrorCategory");
        HashSet<string> staleGenerated = new(generated, StringComparer.Ordinal);
        staleGenerated.Remove("redacted");
        Should.Throw<ShouldAssertException>(() => AssertSetEquals(staleGenerated, generated, "stale generated categories"));

        // 4. Leaked absolute paths, bearer/JWT-like tokens, raw provider payload wording carrying a host, and
        //    non-placeholder hosts must each fail the SAME scanner that guards the real docs.
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("evidence at /home/runner/work/leaked.txt"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhY3RvciJ9.signaturesegment"));
        Should.Throw<ShouldAssertException>(() => AssertDocMetadataOnly("raw provider payload from https://github.example-real.com/api"));

        // Approved placeholder forms must pass the same scanner.
        Should.NotThrow(() => AssertDocMetadataOnly(
            "provider console https://folders.localhost.test and dashboard https://localhost:17000"));

        // 5. Malformed JSON and malformed YAML must fail to parse rather than pass vacuously.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"provider-error-docs\", "));
        Should.Throw<YamlDotNet.Core.YamlException>(() =>
        {
            using StringReader reader = new("- a: 1\n  b: : :\n :\n");
            YamlStream stream = new();
            stream.Load(reader);
        });

        // 6. The forbidden recursive submodule command is detected; the exact root-level command is not.
        Should.Throw<ShouldAssertException>(() => AssertNoRecursiveSubmoduleCommand("git submodule update --init " + string.Concat("--", "recursive")));
        Should.NotThrow(() => AssertNoRecursiveSubmoduleCommand(SubmoduleCommand));
    }

    private static HashSet<string> ParseCapabilityOperations()
        => ConstStringValue().Matches(ReadText(OperationCatalogPath))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseEnumMembers(string relativePath)
    {
        string source = ReadText(relativePath);
        int brace = source.IndexOf('{', StringComparison.Ordinal);
        brace.ShouldBeGreaterThanOrEqualTo(0, $"{relativePath} must declare an enum body.");
        int end = source.LastIndexOf('}');
        end.ShouldBeGreaterThan(brace, $"{relativePath} enum body must be closed.");

        return EnumMemberLine().Matches(source[(brace + 1)..end])
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ParseRetryableByDefault()
    {
        string source = ReadText(FailureCategoryExtPath);
        int index = source.IndexOf("IsRetryableByDefault", StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, "ProviderFailureCategoryExtensions must declare IsRetryableByDefault.");
        int end = source.IndexOf(';', index);
        end.ShouldBeGreaterThan(index, "IsRetryableByDefault must be an expression-bodied member.");

        return ProviderFailureCategoryRef().Matches(source[index..end])
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ParseForgejoSupportedVersions()
        => SemanticVersionLiteral().Matches(ReadText(ForgejoCatalogPath))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseCliExitCodes()
        => ConstIntValue().Matches(ReadText(ExitCodesPath))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ParseGeneratedEnumValues(string enumName)
        => GeneratedEnumMemberValue().Matches(ExtractGeneratedEnumBody(enumName))
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string ExtractGeneratedEnumBody(string enumName)
    {
        string source = ReadText(GeneratedClientPath);
        int declaration = source.IndexOf("public enum " + enumName, StringComparison.Ordinal);
        declaration.ShouldBeGreaterThanOrEqualTo(0, $"generated client must declare enum {enumName}.");
        int open = source.IndexOf('{', declaration);
        open.ShouldBeGreaterThan(declaration, $"enum {enumName} must declare a body.");

        int depth = 0;
        int cursor = open;
        for (; cursor < source.Length; cursor++)
        {
            if (source[cursor] == '{')
            {
                depth++;
            }
            else if (source[cursor] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }
        }

        cursor.ShouldBeLessThan(source.Length, $"enum {enumName} body must be closed.");
        return source[(open + 1)..cursor];
    }

    private static HashSet<string> ParseParityOracleCategories()
    {
        YamlSequenceNode operations = LoadSingleYamlDocument(ParityContractPath).ShouldBeOfType<YamlSequenceNode>();
        HashSet<string> categories = new(StringComparer.Ordinal);
        foreach (YamlMappingNode operation in operations.Children.OfType<YamlMappingNode>())
        {
            if (!operation.Children.TryGetValue(new YamlScalarNode("outcome_mapping"), out YamlNode? node)
                || node is not YamlSequenceNode outcomes)
            {
                continue;
            }

            foreach (YamlMappingNode outcome in outcomes.Children.OfType<YamlMappingNode>())
            {
                if (outcome.Children.TryGetValue(new YamlScalarNode("canonical_error_category"), out YamlNode? value)
                    && value is YamlScalarNode scalar && scalar.Value is not null)
                {
                    categories.Add(scalar.Value);
                }
            }
        }

        return categories;
    }

    private static HashSet<string> FirstColumnBacktickTokens(string relativePath, string marker)
    {
        string table = ExtractMarkerTable(ReadText(relativePath), marker);
        HashSet<string> tokens = new(StringComparer.Ordinal);
        foreach (string line in table.Split('\n'))
        {
            Match match = FirstBacktickToken().Match(line);
            if (match.Success)
            {
                tokens.Add(match.Groups[1].Value);
            }
        }

        return tokens;
    }

    private static string ExtractMarkerTable(string doc, string marker)
    {
        int index = doc.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Missing marker '{marker}'.");
        string[] lines = doc[(index + marker.Length)..].Split('\n');
        List<string> table = [];
        bool started = false;
        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith('|'))
            {
                started = true;
                table.Add(line);
            }
            else if (started)
            {
                break;
            }
        }

        table.Count.ShouldBeGreaterThan(0, $"Marker '{marker}' must be followed by a table.");
        return string.Join('\n', table);
    }

    private static void AssertDocExists(string relativePath)
        => File.Exists(RepositoryPath(relativePath)).ShouldBeTrue(relativePath);

    private static void AssertSetEquals(IEnumerable<string> actual, IEnumerable<string> expected, string because)
        => actual.OrderBy(static value => value, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(static value => value, StringComparer.Ordinal), because);

    private static void AssertDocMetadataOnly(string text)
    {
        HostAbsolutePathPattern().IsMatch(text).ShouldBeFalse($"Provider/error doc must not contain a host-absolute path: {Excerpt(text)}");
        SecretMaterialPattern().IsMatch(text).ShouldBeFalse($"Provider/error doc must not contain secret/credential material: {Excerpt(text)}");

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
            allowed.ShouldBeTrue($"Provider/error doc must use placeholder hosts, not real host '{host}'.");
        }
    }

    private static void AssertNoRecursiveSubmoduleCommand(string text)
        => text.Contains(string.Concat("--", "recursive"), StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse("provider/error evidence must never request nested submodule initialization.");

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

    [GeneratedRegex(@"const string \w+ = ""([a-z_]+)""")]
    private static partial Regex ConstStringValue();

    [GeneratedRegex(@"public const int \w+ = (\d+);")]
    private static partial Regex ConstIntValue();

    [GeneratedRegex(@"^\s*([A-Za-z][A-Za-z0-9]*)\s*(?:=\s*\d+\s*)?,", RegexOptions.Multiline)]
    private static partial Regex EnumMemberLine();

    [GeneratedRegex(@"ProviderFailureCategory\.([A-Za-z]+)")]
    private static partial Regex ProviderFailureCategoryRef();

    [GeneratedRegex(@"new\(\s*""(\d+\.\d+\.\d+)""")]
    private static partial Regex SemanticVersionLiteral();

    [GeneratedRegex(@"EnumMember\(Value = @?""([^""]+)""\)")]
    private static partial Regex GeneratedEnumMemberValue();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex FirstBacktickToken();

    // The drive-letter clause requires the letter not to be preceded by another letter, so a URL scheme such
    // as "https:/" is not mistaken for a "C:\" Windows path while a real "C:\Users" path still matches.
    [GeneratedRegex(@"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/home/|/Users/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex HostAbsolutePathPattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY|AccountKey=|client_secret\s*[:=]\s*\S|xox[baprs]-|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.", RegexOptions.CultureInvariant)]
    private static partial Regex SecretMaterialPattern();

    [GeneratedRegex(@"https?://([^/\s)""'\]]+)", RegexOptions.CultureInvariant)]
    private static partial Regex HttpUrlPattern();
}
