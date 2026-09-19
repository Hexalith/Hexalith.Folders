using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class AuthorizationMatrixContractTests
{
    private const string MatrixRepositoryPath = "docs/contract/authorization-matrix.md";
    private const string PrdActorTablePath = "_bmad-output/planning-artifacts/prd.md";
    private const string CandidateMatrixVersion = "2.0.0";
    private const string SafeDenialOutcome = "safe-denial-404";
    private const string AuthorityUnavailableOutcome = "authority-unavailable-503";

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string MatrixPath = Path.Combine(RepositoryRoot, "docs", "contract", "authorization-matrix.md");
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");
    private static readonly string PrdPath = Path.Combine(RepositoryRoot, "_bmad-output", "planning-artifacts", "prd.md");

    private static readonly Regex AbsoluteWindowsDrivePathPattern = new(
        @"[A-Za-z]:\\",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // The canonical actor names from the PRD Actors table. These are the only actor names used for
    // authorization decisions, so the matrix must carry one decision row per actor and family.
    private static readonly string[] CanonicalActors =
    [
        "tenant-administrator",
        "tenant-member",
        "delegated-service-agent",
        "tenant-scoped-operator",
        "audit-reviewer",
        "incident-administrator",
    ];

    // The canonical negative cases from the PRD Actors table. `absent-resource` and `insufficient-scope`
    // are the two additional protected denial states the approved decision routes through the same envelope.
    private static readonly string[] CanonicalNegativeStates =
    [
        "wrong-tenant",
        "revoked",
        "stale",
        "disabled",
        "unknown",
        "hidden-resource",
    ];

    private static readonly string[] AdditionalDenialStates =
    [
        "absent-resource",
        "insufficient-scope",
    ];

    // The 11 protected operation families from the PRD. `incident-evidence` is retained even though it has
    // no current public Contract Spine operation; dropping it is drift, not simplification.
    private static readonly string[] CanonicalFamilies =
    [
        "provider-configuration",
        "readiness-and-provider-evidence",
        "folder-creation",
        "incident-evidence",
        "folder-administration",
        "task-mutation",
        "context-read",
        "status-permission-and-lock-inspection",
        "audit-read",
        "console-view",
        "index-search",
    ];

    private static readonly string AllFamiliesToken = $"all-{CanonicalFamilies.Length}";

    private static readonly string[] FolderScopedFamilies =
    [
        "folder-administration",
        "task-mutation",
        "context-read",
        "status-permission-and-lock-inspection",
        "audit-read",
        "console-view",
        "index-search",
    ];

    // The eight FR8 scope dimensions. Every operation row must account for all eight, either as applicable
    // or as explicitly not applicable; a silently missing dimension is a failing conformance defect.
    private static readonly string[] ScopeDimensions =
    [
        "tenant",
        "principal",
        "delegated-actor",
        "provider",
        "repository",
        "folder",
        "workspace",
        "task",
    ];

    private static readonly string[] AlwaysApplicableDimensions =
    [
        "tenant",
        "principal",
        "delegated-actor",
    ];

    private static readonly string[] DecisionVocabulary =
    [
        "allow",
        "allow-explicit-grant",
        "allow-delegated-intersection",
        "deny",
    ];

    // The C13 parity classification vocabulary. It is a transport classification and must stay disjoint from
    // the authorization families so that OQ3 never repurposes the generated parity field.
    private static readonly string[] C13TransportClassifications =
    [
        "mutating_command",
        "query_status",
        "context_query",
        "audit",
        "operations_console_projection",
    ];

    private static readonly string[] RequiredGapIds =
    [
        "G1",
        "G2",
        "G3",
        "G4",
        "G5",
        "G6",
        "G7",
        "G8",
        "G9",
        "G10",
        "G11",
    ];

    [Fact]
    public void AuthorizationMatrixPublishesCompleteActorFamilyOperationAndScopeDenominator()
    {
        File.Exists(MatrixPath).ShouldBeTrue("OQ3 requires the canonical authorization-matrix artifact.");

        MatrixDocument matrix = LoadMatrix();
        SpineOperation[] spine = LoadSpineOperations();

        spine.Length.ShouldBe(49, "the authorization denominator is counted against the current Contract Spine inventory.");

        MatrixDiagnostic[] diagnostics = EvaluateMatrix(matrix, spine);

        foreach (MatrixDiagnostic diagnostic in diagnostics)
        {
            AssertMetadataOnly(diagnostic.ToString());
        }

        diagnostics.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));

        matrix.Families.Select(family => family.Family).ToArray().ShouldBe(CanonicalFamilies, ignoreOrder: true);
        matrix.Decisions.Length.ShouldBe(CanonicalActors.Length * CanonicalFamilies.Length);
        matrix.Operations.Length.ShouldBe(spine.Length);
        matrix.Operations.Select(operation => operation.OperationId).Distinct(StringComparer.Ordinal).Count().ShouldBe(spine.Length);
        matrix.NegativeStates.Select(state => state.State).ToArray()
            .ShouldBe(CanonicalNegativeStates.Concat(AdditionalDenialStates).ToArray(), ignoreOrder: true);
        matrix.Gaps.Select(gap => gap.GapId).ToArray().ShouldBe(RequiredGapIds, ignoreOrder: true);
    }

    [Fact]
    public void AuthorizationMatrixBindsApprovedVersionEvaluationOrderAndSafeOutcomes()
    {
        string matrixText = File.ReadAllText(MatrixPath);

        matrixText.ShouldContain($"Matrix version: `{CandidateMatrixVersion}`", Case.Sensitive);
        matrixText.ShouldContain("Status: `candidate-awaiting-a6b`", Case.Sensitive);
        matrixText.ShouldContain("Required A6b approval: Product + Architecture + Security", Case.Sensitive);
        matrixText.ShouldContain("Historical v1 governance evidence: `docs/contract/oq3-authorization-evidence.yaml`", Case.Sensitive);

        // S-4 authorization layering is preserved verbatim and never reordered by the matrix.
        matrixText.ShouldContain(
            "jwt_validation -> eventstore_claim_transform -> tenant_access_projection_fail_closed_on_stale -> folder_acl -> eventstore_validators -> dapr_deny_by_default_policies_and_mtls",
            Case.Sensitive);

        // Authorization before observation, including the authority-availability check ahead of any lookup.
        matrixText.ShouldContain(
            "authentication -> authority_evidence_availability -> tenant_access -> principal_and_delegation_intersection -> folder_acl_allow -> family_grant -> resource_scope_binding -> freshness_revalidation -> observation",
            Case.Sensitive);

        // FR10 metadata-only allow/deny audit evidence carries exactly six fields.
        matrixText.ShouldContain("actor, tenant, operation, operation_family, result, correlation_id", Case.Sensitive);

        string[] lines = File.ReadAllLines(MatrixPath);
        string[][] outcomes = ParseTable(
            lines,
            ["Outcome", "Status", "Category", "Code", "Retryable", "Client action", "Details visibility", "When"]);

        outcomes.Length.ShouldBe(3, "the canonical design keeps exactly one authentication failure, one safe denial, and one authority-unavailable outcome.");
        string[] statuses = [.. outcomes.Select(row => Unquote(row[1]))];
        statuses.ShouldBe(["401", "404", "503"]);
        statuses.ShouldNotContain("403", "post-authentication 403 is retired from the canonical design.");

        string[] safeDenial = outcomes.Single(row => Unquote(row[0]) == SafeDenialOutcome);
        Unquote(safeDenial[2]).ShouldBe("tenant_access_denied");
        Unquote(safeDenial[3]).ShouldBe("resource_unavailable");
        Unquote(safeDenial[4]).ShouldBe("false", "a safe denial must never invite a retry that probes existence.");
        Unquote(safeDenial[6]).ShouldBe("redacted");

        // The authority-unavailable outcome is the only retryable one: it preserves retry semantics for a
        // transient authority-evidence failure without hinting that the target resource exists.
        string[] authorityUnavailable = outcomes.Single(row => Unquote(row[0]) == "authority-unavailable-503");
        Unquote(authorityUnavailable[2]).ShouldBe("read_model_unavailable");
        Unquote(authorityUnavailable[4]).ShouldBe("true");
        Unquote(authorityUnavailable[5]).ShouldBe("retry");
        Unquote(authorityUnavailable[6]).ShouldBe("redacted");
        authorityUnavailable[7].ShouldContain("before any protected-resource lookup", Case.Sensitive);

        outcomes.Count(row => Unquote(row[4]) == "true").ShouldBe(1);

        AssertMetadataOnly(matrixText);
    }

    [Fact]
    public void AuthorizationMatrixNegativeControlsFailClosedForOperationActorFamilyCaseAndScopeDrift()
    {
        MatrixDocument matrix = LoadMatrix();
        SpineOperation[] spine = LoadSpineOperations();
        const string unexpectedValue = "tenant-secret-unexpected-value";

        MatrixDiagnostic[] removedOperation = EvaluateMatrix(
            matrix with { Operations = [.. matrix.Operations.Skip(1)] },
            spine);
        removedOperation.ShouldContain(diagnostic => diagnostic.Category == "oq3_operation_unmapped");

        OperationRow first = matrix.Operations[0];
        MatrixDiagnostic[] duplicateOperation = EvaluateMatrix(
            matrix with { Operations = [.. matrix.Operations, first] },
            spine);
        duplicateOperation.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_operation_duplicate" && diagnostic.Identifier == $"OQ3:{first.OperationId}");

        MatrixDiagnostic[] unknownOperation = EvaluateMatrix(
            matrix with
            {
                Operations = [.. matrix.Operations, first with { OperationId = unexpectedValue }],
            },
            spine);
        unknownOperation.ShouldContain(diagnostic => diagnostic.Category == "oq3_operation_unknown");

        MatrixDiagnostic[] mismatchedFamily = EvaluateMatrix(
            matrix with
            {
                Operations = [first with { Family = unexpectedValue }, .. matrix.Operations.Skip(1)],
            },
            spine);
        mismatchedFamily.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_matrix_mismatch" && diagnostic.Identifier == $"OQ3:{first.OperationId}:family");

        MatrixDiagnostic[] droppedFamily = EvaluateMatrix(
            matrix with
            {
                Families = [.. matrix.Families.Where(family => family.Family != "incident-evidence")],
            },
            spine);
        droppedFamily.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_family_uncovered" && diagnostic.Identifier == "OQ3:incident-evidence");

        MatrixDiagnostic[] droppedActorRow = EvaluateMatrix(
            matrix with
            {
                Decisions = [.. matrix.Decisions.Where(decision =>
                    decision.AccessState != "audit-reviewer" || decision.Family != "context-read")],
            },
            spine);
        droppedActorRow.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_actor_uncovered" && diagnostic.Identifier == "OQ3:audit-reviewer:context-read");

        MatrixDiagnostic[] unknownDecision = EvaluateMatrix(
            matrix with
            {
                Decisions = [matrix.Decisions[0] with { Decision = unexpectedValue }, .. matrix.Decisions.Skip(1)],
            },
            spine);
        unknownDecision.ShouldContain(diagnostic => diagnostic.Category == "oq3_matrix_mismatch");

        MatrixDiagnostic[] weakenedDecisionOutcome = EvaluateMatrix(
            matrix with
            {
                Decisions =
                [
                    matrix.Decisions[0] with { MissingConjunctOutcome = "forbidden-403" },
                    .. matrix.Decisions.Skip(1),
                ],
            },
            spine);
        weakenedDecisionOutcome.ShouldContain(diagnostic => diagnostic.Category == "oq3_denial_shape_mismatch");

        MatrixDiagnostic[] droppedNegativeCase = EvaluateMatrix(
            matrix with
            {
                NegativeStates = [.. matrix.NegativeStates.Where(state => state.State != "revoked")],
            },
            spine);
        droppedNegativeCase.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_actor_uncovered" && diagnostic.Identifier == "OQ3:revoked");

        MatrixDiagnostic[] weakenedDenial = EvaluateMatrix(
            matrix with
            {
                NegativeStates =
                [
                    matrix.NegativeStates[0] with { Outcome = "forbidden-403" },
                    .. matrix.NegativeStates.Skip(1),
                ],
            },
            spine);
        weakenedDenial.ShouldContain(diagnostic => diagnostic.Category == "oq3_denial_shape_mismatch");

        MatrixDiagnostic[] partialNegativeCoverage = EvaluateMatrix(
            matrix with
            {
                NegativeStates =
                [
                    matrix.NegativeStates[0] with { FamiliesCovered = "some" },
                    .. matrix.NegativeStates.Skip(1),
                ],
            },
            spine);
        partialNegativeCoverage.ShouldContain(diagnostic => diagnostic.Category == "oq3_matrix_mismatch");

        MatrixDiagnostic[] droppedScope = EvaluateMatrix(
            matrix with
            {
                Operations =
                [
                    first with { NotApplicable = [] },
                    .. matrix.Operations.Skip(1),
                ],
            },
            spine);
        droppedScope.ShouldContain(diagnostic => diagnostic.Category == "oq3_scope_incomplete");

        OperationRow folderScoped = matrix.Operations.First(operation => operation.Applicable.Contains("folder", StringComparer.Ordinal)
            && FolderScopedFamilies.Contains(operation.Family, StringComparer.Ordinal));
        MatrixDiagnostic[] undeclaredFolderGap = EvaluateMatrix(
            matrix with
            {
                Operations =
                [
                    .. matrix.Operations.Where(operation => operation.OperationId != folderScoped.OperationId),
                    folderScoped with
                    {
                        Applicable = [.. folderScoped.Applicable.Where(dimension => dimension != "folder")],
                        NotApplicable = [.. ScopeDimensions.Where(dimension =>
                            dimension == "folder" || folderScoped.NotApplicable.Contains(dimension, StringComparer.Ordinal))],
                    },
                ],
            },
            spine);
        undeclaredFolderGap.ShouldContain(diagnostic => diagnostic.Category == "oq3_gap_unrecorded");

        OperationRow providerEvidenced = matrix.Operations.First(operation => operation.OperationId == "ConfigureProviderBinding");
        MatrixDiagnostic[] droppedProviderScope = EvaluateMatrix(
            matrix with
            {
                Operations =
                [
                    .. matrix.Operations.Where(operation => operation.OperationId != providerEvidenced.OperationId),
                    providerEvidenced with
                    {
                        Applicable = [.. providerEvidenced.Applicable.Where(dimension => dimension != "provider")],
                        NotApplicable = [.. ScopeDimensions.Where(dimension =>
                            dimension == "provider" || providerEvidenced.NotApplicable.Contains(dimension, StringComparer.Ordinal))],
                    },
                ],
            },
            spine);
        droppedProviderScope.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_scope_incomplete" && diagnostic.Identifier == "OQ3:ConfigureProviderBinding:provider");

        // Removing the derivation gap must expose every provider/repository dimension the Spine does not evidence.
        MatrixDiagnostic[] undeclaredBindingDerivation = EvaluateMatrix(
            matrix with { Gaps = [.. matrix.Gaps.Where(gap => gap.GapId != "G11")] },
            spine);
        undeclaredBindingDerivation.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_scope_incomplete" && diagnostic.Identifier == "OQ3:LockWorkspace:provider");
        undeclaredBindingDerivation.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_scope_incomplete" && diagnostic.Identifier == "OQ3:LockWorkspace:repository");

        MatrixDiagnostic[] droppedGap = EvaluateMatrix(
            matrix with { Gaps = [.. matrix.Gaps.Where(gap => gap.GapId != "G4")] },
            spine);
        droppedGap.ShouldContain(diagnostic =>
            diagnostic.Category == "oq3_gap_unrecorded" && diagnostic.Identifier == "OQ3:G4");

        MatrixDiagnostic[] unreadableGapEvidence = EvaluateMatrix(
            matrix with
            {
                Gaps = [matrix.Gaps[0] with { EvidencePath = "../outside/evidence.md" }, .. matrix.Gaps.Skip(1)],
            },
            spine);
        unreadableGapEvidence.ShouldContain(diagnostic => diagnostic.Category == "oq3_matrix_mismatch");

        foreach (MatrixDiagnostic diagnostic in removedOperation
            .Concat(duplicateOperation)
            .Concat(unknownOperation)
            .Concat(mismatchedFamily)
            .Concat(droppedFamily)
            .Concat(droppedActorRow)
            .Concat(unknownDecision)
            .Concat(weakenedDecisionOutcome)
            .Concat(droppedNegativeCase)
            .Concat(weakenedDenial)
            .Concat(partialNegativeCoverage)
            .Concat(droppedScope)
            .Concat(undeclaredFolderGap)
            .Concat(droppedProviderScope)
            .Concat(undeclaredBindingDerivation)
            .Concat(droppedGap)
            .Concat(unreadableGapEvidence))
        {
            AssertMetadataOnly(diagnostic.ToString());
            diagnostic.ToString().ShouldNotContain(unexpectedValue, Case.Sensitive);
        }
    }

    [Fact]
    public void AuthorizationMatrixRecordsDownstreamGapsWithoutClaimingRuntimeCompletion()
    {
        MatrixDocument matrix = LoadMatrix();
        string matrixText = File.ReadAllText(MatrixPath);

        foreach (GapRow gap in matrix.Gaps)
        {
            IsRepositoryRelativePath(gap.EvidencePath).ShouldBeTrue(gap.GapId);
            PathExists(gap.EvidencePath).ShouldBeTrue(gap.GapId);
        }

        matrixText.ShouldContain("Runtime authorization enforcement | incomplete", Case.Sensitive);
        matrixText.ShouldContain("Contract Spine conformance | complete for the generated, non-routed v2 candidate", Case.Sensitive);
        matrixText.ShouldContain("Incident-evidence operation surface | absent", Case.Sensitive);

        // The retained incident-evidence family stays in the actor denominator with no current public operation.
        matrix.Families.Single(family => family.Family == "incident-evidence").OperationCount.ShouldBe(0);
        matrix.Decisions.Count(decision => decision.Family == "incident-evidence").ShouldBe(CanonicalActors.Length);
    }

    [Fact]
    public void AuthorizationMatrixDenominatorTracksThePrdActorFamilyAndScopeInventories()
    {
        string[] prd = File.ReadAllLines(PrdPath);

        string[][] actorRows = ParseTable(
            prd,
            ["Canonical actor", "Synonyms used in this PRD", "Authority source", "Typical journeys and FRs"],
            PrdActorTablePath);

        string[] prdActors =
        [
            .. actorRows.Where(row => Unquote(row[0]) != "Negative cases").Select(row => Slug(Unquote(row[0]))),
        ];
        prdActors.ShouldBe(CanonicalActors, "the matrix actor denominator must track the PRD Actors table.");

        string[] prdNegativeCases =
        [
            .. actorRows.Single(row => Unquote(row[0]) == "Negative cases")[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim().Split(' ')[0]),
        ];
        prdNegativeCases.ShouldBe(CanonicalNegativeStates, "the matrix negative-case denominator must track the PRD Actors table.");

        string[] prdFamilies =
        [
            .. ParseTable(
                    prd,
                    ["Family", "Scope", "Protected operations", "FRs", "Spine grant today"],
                    PrdActorTablePath)
                .Select(row => Slug(Unquote(row[0]))),
        ];
        prdFamilies.ShouldBe(CanonicalFamilies, "the matrix family denominator must track the PRD protected-operation-families table.");

        string fr8 = prd.Single(line => line.StartsWith("- FR8:", StringComparison.Ordinal));
        int start = fr8.IndexOf("against ", StringComparison.Ordinal) + "against ".Length;
        int end = fr8.IndexOf(" scope;", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0, "FR8 must declare the evaluated scope dimensions.");
        end.ShouldBeGreaterThan(start, "FR8 must terminate its scope-dimension list.");

        string[] prdDimensions =
        [
            .. fr8[start..end]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => Slug(entry.Trim().StartsWith("and ", StringComparison.Ordinal) ? entry.Trim()[4..] : entry.Trim())),
        ];
        prdDimensions.ShouldBe(ScopeDimensions, "the matrix scope denominator must track the FR8 scope-dimension list.");
    }

    [Fact]
    public void AuthorizationMatrixCandidateClosesHistoricalContractVocabularyGaps()
    {
        MatrixDocument matrix = LoadMatrix();
        SpineOperation[] spine = LoadSpineOperations();
        string[] leakingCategories = ["not_found", "cross_tenant_access_denied", "audit_access_denied"];

        spine.Length.ShouldBe(49);
        spine.ShouldAllBe(operation => operation.ResponseStatusCodes.Contains("401", StringComparer.Ordinal));
        spine.ShouldAllBe(operation => operation.ResponseStatusCodes.Contains("404", StringComparer.Ordinal));
        spine.ShouldAllBe(operation => operation.ResponseStatusCodes.Contains("503", StringComparer.Ordinal));
        spine.ShouldAllBe(operation => !operation.ResponseStatusCodes.Contains("403", StringComparer.Ordinal));
        spine.ShouldAllBe(operation => !leakingCategories.Any(category => operation.ErrorCategories.Contains(category, StringComparer.Ordinal)));
        File.ReadAllText(MatrixPath).ShouldContain("generated v2 candidate closes `G1`, `G2`, `G3`", Case.Sensitive);
    }

    [Fact]
    public void AuthorizationMatrixFamilyVocabularyStaysDisjointFromC13TransportClassification()
    {
        MatrixDocument matrix = LoadMatrix();

        foreach (string family in matrix.Families.Select(row => row.Family))
        {
            C13TransportClassifications.ShouldNotContain(family, "OQ3 must not repurpose the generated C13 operation_family field.");
        }

        foreach (string classification in C13TransportClassifications)
        {
            matrix.Operations.Select(operation => operation.Family).ToArray().ShouldNotContain(classification);
        }
    }

    private static MatrixDiagnostic[] EvaluateMatrix(MatrixDocument matrix, IReadOnlyList<SpineOperation> spine)
    {
        List<MatrixDiagnostic> diagnostics = [];

        void Add(string category, string identifier) =>
            diagnostics.Add(new(category, $"OQ3:{identifier}", MatrixRepositoryPath));

        EvaluateFamilies(matrix, Add);
        EvaluateDecisions(matrix, Add);
        EvaluateNegativeStates(matrix, Add);
        EvaluateOperations(matrix, spine, Add);
        EvaluateGaps(matrix, Add);

        return [.. diagnostics];
    }

    private static void EvaluateFamilies(MatrixDocument matrix, Action<string, string> add)
    {
        foreach (string family in CanonicalFamilies.Where(family =>
            matrix.Families.Count(row => row.Family == family) != 1))
        {
            add("oq3_family_uncovered", family);
        }

        for (int unexpected = matrix.Families.Count(row => !CanonicalFamilies.Contains(row.Family, StringComparer.Ordinal)); unexpected > 0; unexpected--)
        {
            add("oq3_matrix_mismatch", "family-vocabulary");
        }

        foreach (FamilyRow row in matrix.Families.Where(row => row.Scope is not ("tenant" or "folder")))
        {
            add("oq3_matrix_mismatch", $"{BoundedFamily(row.Family)}:scope");
        }

        foreach (FamilyRow row in matrix.Families)
        {
            int mapped = matrix.Operations.Count(operation => operation.Family == row.Family);
            if (mapped != row.OperationCount)
            {
                add("oq3_matrix_mismatch", $"{BoundedFamily(row.Family)}:operation-count");
            }
        }
    }

    // Never interpolate raw document text into a diagnostic: values outside the canonical vocabulary collapse
    // to a constant identifier so a bounded metadata-only guarantee survives arbitrary table content.
    private static string BoundedFamily(string family) =>
        CanonicalFamilies.Contains(family, StringComparer.Ordinal) ? family : "family-vocabulary";

    private static void EvaluateDecisions(MatrixDocument matrix, Action<string, string> add)
    {
        foreach (string actor in CanonicalActors)
        {
            foreach (string family in CanonicalFamilies)
            {
                int matches = matrix.Decisions.Count(decision => decision.AccessState == actor && decision.Family == family);
                if (matches != 1)
                {
                    add("oq3_actor_uncovered", $"{actor}:{family}");
                }
            }
        }

        foreach (DecisionRow decision in matrix.Decisions)
        {
            if (!CanonicalActors.Contains(decision.AccessState, StringComparer.Ordinal)
                || !CanonicalFamilies.Contains(decision.Family, StringComparer.Ordinal))
            {
                add("oq3_matrix_mismatch", "decision-vocabulary");
                continue;
            }

            if (!DecisionVocabulary.Contains(decision.Decision, StringComparer.Ordinal))
            {
                add("oq3_matrix_mismatch", $"{decision.AccessState}:{decision.Family}:decision");
            }
        }

        if (matrix.Decisions.Length != CanonicalActors.Length * CanonicalFamilies.Length)
        {
            add("oq3_matrix_mismatch", "decision-count");
        }

        // Delegation may never elevate: an agent row is either denied or the explicit intersection decision.
        foreach (DecisionRow decision in matrix.Decisions.Where(decision => decision.AccessState == "delegated-service-agent"
            && decision.Decision is not ("deny" or "allow-delegated-intersection")))
        {
            add("oq3_matrix_mismatch", $"{BoundedFamily(decision.Family)}:delegation");
        }

        // An elevated-read or explicit-grant row whose missing conjunct resolves to anything but the canonical
        // safe denial would reintroduce a status-distinct post-authentication denial.
        foreach (DecisionRow decision in matrix.Decisions.Where(decision => decision.MissingConjunctOutcome != SafeDenialOutcome))
        {
            add("oq3_denial_shape_mismatch", $"{decision.AccessState}:{decision.Family}");
        }
    }

    private static void EvaluateNegativeStates(MatrixDocument matrix, Action<string, string> add)
    {
        foreach (string state in CanonicalNegativeStates.Concat(AdditionalDenialStates).Where(state =>
            matrix.NegativeStates.Count(row => row.State == state) != 1))
        {
            add("oq3_actor_uncovered", state);
        }

        foreach (NegativeStateRow row in matrix.NegativeStates)
        {
            if (!CanonicalNegativeStates.Concat(AdditionalDenialStates).Contains(row.State, StringComparer.Ordinal))
            {
                add("oq3_matrix_mismatch", "negative-state-vocabulary");
                continue;
            }

            string expectedOutcome = row.State == "stale" ? AuthorityUnavailableOutcome : SafeDenialOutcome;
            if (row.Outcome != expectedOutcome)
            {
                add("oq3_denial_shape_mismatch", row.State);
            }

            if (row.FamiliesCovered != AllFamiliesToken)
            {
                add("oq3_matrix_mismatch", $"{row.State}:families");
            }
        }
    }

    private static void EvaluateOperations(MatrixDocument matrix, IReadOnlyList<SpineOperation> spine, Action<string, string> add)
    {
        foreach (SpineOperation operation in spine.Where(operation =>
            !matrix.Operations.Any(row => row.OperationId == operation.OperationId)))
        {
            add("oq3_operation_unmapped", operation.OperationId);
        }

        foreach (IGrouping<string, OperationRow> group in matrix.Operations
            .GroupBy(operation => operation.OperationId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            add("oq3_operation_duplicate", group.Key);
        }

        foreach (OperationRow row in matrix.Operations)
        {
            SpineOperation? declared = spine.FirstOrDefault(operation => operation.OperationId == row.OperationId);
            if (declared is null)
            {
                add("oq3_operation_unknown", "spine-inventory");
                continue;
            }

            if (declared.Method != row.Method || declared.Path != row.Path)
            {
                add("oq3_matrix_mismatch", $"{row.OperationId}:identity");
            }

            if (!CanonicalFamilies.Contains(row.Family, StringComparer.Ordinal))
            {
                add("oq3_matrix_mismatch", $"{row.OperationId}:family");
            }

            EvaluateScopeDimensions(matrix, row, declared, add);
        }
    }

    private static void EvaluateScopeDimensions(
        MatrixDocument matrix,
        OperationRow row,
        SpineOperation declared,
        Action<string, string> add)
    {
        string[] union = [.. row.Applicable, .. row.NotApplicable];
        if (union.Length != ScopeDimensions.Length
            || union.Distinct(StringComparer.Ordinal).Count() != ScopeDimensions.Length
            || ScopeDimensions.Any(dimension => !union.Contains(dimension, StringComparer.Ordinal)))
        {
            add("oq3_scope_incomplete", $"{row.OperationId}:coverage");
        }

        foreach (string dimension in AlwaysApplicableDimensions.Where(dimension =>
            !row.Applicable.Contains(dimension, StringComparer.Ordinal)))
        {
            add("oq3_scope_incomplete", $"{row.OperationId}:{dimension}");
        }

        if (declared.HasFolderScope && !row.Applicable.Contains("folder", StringComparer.Ordinal))
        {
            add("oq3_scope_incomplete", $"{row.OperationId}:folder");
        }

        if (declared.HasWorkspaceScope && !row.Applicable.Contains("workspace", StringComparer.Ordinal))
        {
            add("oq3_scope_incomplete", $"{row.OperationId}:workspace");
        }

        if (declared.HasTaskScope && !row.Applicable.Contains("task", StringComparer.Ordinal))
        {
            add("oq3_scope_incomplete", $"{row.OperationId}:task");
        }

        // Provider and repository are checked in both directions: the Spine may not silently lose a dimension
        // its path or parameters carry, and the matrix may not silently claim one the Spine does not evidence
        // unless the derivation is recorded in the gap table.
        (string dimension, bool evidenced)[] bindingDimensions =
        [
            ("provider", declared.HasProviderScope),
            ("repository", declared.HasRepositoryScope),
        ];
        foreach ((string dimension, bool evidenced) in bindingDimensions)
        {
            bool applicable = row.Applicable.Contains(dimension, StringComparer.Ordinal);
            if (evidenced && !applicable)
            {
                add("oq3_scope_incomplete", $"{row.OperationId}:{dimension}");
            }
            else if (applicable && !evidenced && !IsRecordedGapOperation(matrix, row.OperationId))
            {
                add("oq3_scope_incomplete", $"{row.OperationId}:{dimension}");
            }
        }

        // A folder-level family whose operation carries no folder scope is drift that must be recorded in the
        // gap table rather than silently accepted.
        if (FolderScopedFamilies.Contains(row.Family, StringComparer.Ordinal)
            && !row.Applicable.Contains("folder", StringComparer.Ordinal)
            && !IsRecordedGapOperation(matrix, row.OperationId))
        {
            add("oq3_gap_unrecorded", $"{row.OperationId}:folder-scope");
        }
    }

    private static bool IsRecordedGapOperation(MatrixDocument matrix, string operationId) =>
        matrix.Gaps.Any(gap => gap.Operations.Contains(operationId, StringComparer.Ordinal));

    private static void EvaluateGaps(MatrixDocument matrix, Action<string, string> add)
    {
        foreach (string gapId in RequiredGapIds.Where(gapId => matrix.Gaps.Count(gap => gap.GapId == gapId) != 1))
        {
            add("oq3_gap_unrecorded", gapId);
        }

        foreach (GapRow gap in matrix.Gaps.Where(gap => !IsRepositoryRelativePath(gap.EvidencePath) || !PathExists(gap.EvidencePath)))
        {
            add("oq3_matrix_mismatch", $"{gap.GapId}:evidence-path");
        }
    }

    private static MatrixDocument LoadMatrix()
    {
        if (!File.Exists(MatrixPath))
        {
            throw new InvalidOperationException($"GOVERNANCE-PREREQUISITE-DRIFT: matrix-not-found: {MatrixRepositoryPath}");
        }

        string[] lines = File.ReadAllLines(MatrixPath);

        FamilyRow[] families =
        [
            .. ParseTable(lines, ["Family", "Scope", "Authority conjunct", "Governing conjuncts", "FRs", "Operations"])
                .Select(row => new FamilyRow(Unquote(row[0]), Unquote(row[1]), ParseCount(Unquote(row[5])))),
        ];

        DecisionRow[] decisions =
        [
            .. ParseTable(lines, ["Access state", "Operation family", "Decision", "Governing conjuncts", "Outcome when a conjunct is missing"])
                .Select(row => new DecisionRow(Unquote(row[0]), Unquote(row[1]), Unquote(row[2]), Unquote(row[4]))),
        ];

        NegativeStateRow[] negativeStates =
        [
            .. ParseTable(lines, ["Negative access state", "Condition", "Evaluated at", "Canonical outcome", "Families covered"])
                .Select(row => new NegativeStateRow(Unquote(row[0]), Unquote(row[3]), Unquote(row[4]))),
        ];

        OperationRow[] operations =
        [
            .. ParseTable(lines, ["Operation", "Method", "Path", "Family", "Applicable scope dimensions", "Not-applicable scope dimensions"])
                .Select(row => new OperationRow(
                    Unquote(row[0]),
                    Unquote(row[1]),
                    Unquote(row[2]),
                    Unquote(row[3]),
                    ParseTokens(row[4]),
                    ParseTokens(row[5]))),
        ];

        GapRow[] gaps =
        [
            .. ParseTable(lines, ["Gap", "Surface", "Description", "Downstream owner", "Evidence path", "Operations"])
                .Select(row => new GapRow(Unquote(row[0]), row[2], Unquote(row[4]), ParseTokens(row[5]))),
        ];

        return new MatrixDocument(families, decisions, negativeStates, operations, gaps);
    }

    private static SpineOperation[] LoadSpineOperations()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode parameters = RequiredMapping(RequiredMapping(root, "components"), "parameters");
        YamlMappingNode paths = RequiredMapping(root, "paths");
        List<SpineOperation> operations = [];

        foreach (KeyValuePair<YamlNode, YamlNode> path in paths.Children)
        {
            string route = ((YamlScalarNode)path.Key).Value ?? string.Empty;
            YamlMappingNode pathItem = (YamlMappingNode)path.Value;

            // Path-item-level parameters apply to every operation under the route; ignoring them would
            // let a scope dimension declared once for the whole path pass unchecked.
            string[] sharedNames = [.. ResolveParameterNames(pathItem, parameters)];

            foreach (KeyValuePair<YamlNode, YamlNode> method in pathItem.Children)
            {
                string verb = ((YamlScalarNode)method.Key).Value ?? string.Empty;
                if (verb is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                YamlMappingNode operation = (YamlMappingNode)method.Value;
                string[] parameterNames = [.. sharedNames, .. ResolveParameterNames(operation, parameters)];
                bool namesProvider = route.Contains("provider", StringComparison.OrdinalIgnoreCase)
                    || parameterNames.Any(name => name.Contains("provider", StringComparison.OrdinalIgnoreCase));
                bool namesRepository = route.Contains("repositor", StringComparison.OrdinalIgnoreCase)
                    || parameterNames.Any(name => name.Contains("repositor", StringComparison.OrdinalIgnoreCase));

                operations.Add(new SpineOperation(
                    RequiredScalar(operation, "operationId"),
                    verb.ToUpperInvariant(),
                    route,
                    parameterNames.Contains("folderId", StringComparer.Ordinal),
                    parameterNames.Contains("workspaceId", StringComparer.Ordinal),
                    parameterNames.Contains("taskId", StringComparer.Ordinal)
                        || parameterNames.Contains("X-Hexalith-Task-Id", StringComparer.Ordinal),
                    namesProvider,
                    namesRepository,
                    [.. ChildKeys(operation, "responses")],
                    [.. ChildValues(operation, "x-hexalith-canonical-error-categories")]));
            }
        }

        return [.. operations];
    }

    private static IEnumerable<string> ChildKeys(YamlMappingNode operation, string key) =>
        operation.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node) && node is YamlMappingNode mapping
            ? mapping.Children.Keys.OfType<YamlScalarNode>().Select(scalar => scalar.Value ?? string.Empty)
            : [];

    private static IEnumerable<string> ChildValues(YamlMappingNode operation, string key) =>
        operation.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node) && node is YamlSequenceNode sequence
            ? sequence.Children.OfType<YamlScalarNode>().Select(scalar => scalar.Value ?? string.Empty)
            : [];

    private static IEnumerable<string> ResolveParameterNames(YamlMappingNode owner, YamlMappingNode parameters)
    {
        if (!owner.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? node)
            || node is not YamlSequenceNode sequence)
        {
            yield break;
        }

        foreach (YamlMappingNode parameter in sequence.Children.OfType<YamlMappingNode>())
        {
            if (parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? reference)
                && reference is YamlScalarNode { Value: { Length: > 0 } referenceValue })
            {
                string key = referenceValue.Split('/')[^1];
                if (!parameters.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? resolved)
                    || resolved is not YamlMappingNode resolvedMapping)
                {
                    // Silently dropping an unresolvable reference would make every scope check fail open.
                    throw new InvalidOperationException(
                        $"GOVERNANCE-PREREQUISITE-DRIFT: parameter-ref-unresolved: {key}");
                }

                yield return RequiredScalar(resolvedMapping, "name");
                continue;
            }

            yield return RequiredScalar(parameter, "name");
        }
    }

    private static string[][] ParseTable(string[] lines, string[] expectedHeader, string sourcePath = MatrixRepositoryPath)
    {
        for (int index = 0; index < lines.Length; index++)
        {
            string[] cells = SplitRow(lines[index]);
            if (cells.Length != expectedHeader.Length || !cells.SequenceEqual(expectedHeader, StringComparer.Ordinal))
            {
                continue;
            }

            List<string[]> rows = [];
            for (int cursor = index + 2; cursor < lines.Length && lines[cursor].StartsWith('|'); cursor++)
            {
                string[] row = SplitRow(lines[cursor]);
                if (row.Length != expectedHeader.Length)
                {
                    throw new InvalidOperationException(
                        $"GOVERNANCE-PREREQUISITE-DRIFT: matrix-table-malformed: {sourcePath}: {expectedHeader[0]}");
                }

                rows.Add(row);
            }

            return [.. rows];
        }

        throw new InvalidOperationException(
            $"GOVERNANCE-PREREQUISITE-DRIFT: matrix-table-missing: {sourcePath}: {expectedHeader[0]}");
    }

    private static string[] SplitRow(string line)
    {
        if (!line.StartsWith('|'))
        {
            return [];
        }

        return [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];
    }

    private static string Unquote(string cell) => cell.Trim().Trim('`').Trim();

    private static string Slug(string value) =>
        value.Replace(",", string.Empty, StringComparison.Ordinal).Replace(' ', '-').ToLowerInvariant();

    private static string GapDescription(MatrixDocument matrix, string gapId) =>
        matrix.Gaps.Single(gap => gap.GapId == gapId).Description;

    private static string[] ParseTokens(string cell)
    {
        string value = cell.Trim();
        return value is "none" or ""
            ? []
            : [.. value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(Unquote)];
    }

    private static int ParseCount(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : -1;

    private static bool PathExists(string repositoryPath) =>
        File.Exists(Path.Combine(RepositoryRoot, repositoryPath.Replace('/', Path.DirectorySeparatorChar)));

    private static bool IsRepositoryRelativePath(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        string normalized = repositoryPath.Replace('\\', '/');
        return !Path.IsPathFullyQualified(normalized)
            && !normalized.StartsWith('/')
            && !normalized.StartsWith("../", StringComparison.Ordinal)
            && !normalized.Split('/').Contains("..", StringComparer.Ordinal);
    }

    private static void AssertMetadataOnly(string value)
    {
        string[] forbidden =
        [
            "diff --git",
            "provider_token",
            "credential_material",
            "raw_payload",
            "file_content",
            "cache-key-value",
            "https://github.com/",
            "https://api.github.com",
            "https://prod.",
            RepositoryRoot,
            RepositoryRoot.Replace("\\", "/", StringComparison.Ordinal),
            "/home/",
            "/Users/",
        ];

        foreach (string forbiddenValue in forbidden)
        {
            value.ShouldNotContain(forbiddenValue, Case.Insensitive);
        }

        AbsoluteWindowsDrivePathPattern.IsMatch(value).ShouldBeFalse(value);
    }

    private static YamlMappingNode LoadYamlMapping(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: yaml-not-found: contract spine");
        }

        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static string RequiredScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);
        string? scalar = value.ShouldBeOfType<YamlScalarNode>().Value;
        scalar.ShouldNotBeNullOrWhiteSpace(key);
        return scalar!;
    }

    private static string FindRepositoryRoot()
    {
        foreach (string seed in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? current = new(seed);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: repository root was not found.");
    }

    private sealed record FamilyRow(string Family, string Scope, int OperationCount);

    private sealed record DecisionRow(string AccessState, string Family, string Decision, string MissingConjunctOutcome);

    private sealed record NegativeStateRow(string State, string Outcome, string FamiliesCovered);

    private sealed record OperationRow(
        string OperationId,
        string Method,
        string Path,
        string Family,
        string[] Applicable,
        string[] NotApplicable);

    private sealed record GapRow(string GapId, string Description, string EvidencePath, string[] Operations);

    private sealed record SpineOperation(
        string OperationId,
        string Method,
        string Path,
        bool HasFolderScope,
        bool HasWorkspaceScope,
        bool HasTaskScope,
        bool HasProviderScope,
        bool HasRepositoryScope,
        string[] ResponseStatusCodes,
        string[] ErrorCategories);

    private sealed record MatrixDocument(
        FamilyRow[] Families,
        DecisionRow[] Decisions,
        NegativeStateRow[] NegativeStates,
        OperationRow[] Operations,
        GapRow[] Gaps);

    private sealed record MatrixDiagnostic(string Category, string Identifier, string RepositoryPath)
    {
        public override string ToString() =>
            $"authorization-matrix:{Category}: id={Identifier}; path={RepositoryPath}";
    }
}
