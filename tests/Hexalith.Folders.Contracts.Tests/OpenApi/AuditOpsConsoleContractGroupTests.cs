using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class AuditOpsConsoleContractGroupTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string OpenApiPath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string ContractNotesPath = Path.Combine(RepositoryRoot, "docs", "contract", "audit-ops-console-contract-groups.md");
    private static readonly string AuditLeakageCorpusPath = Path.Combine(RepositoryRoot, "tests", "fixtures", "audit-leakage-corpus.json");

    private static readonly string[] AuditOperationIds =
    [
        "ListAuditTrail",
        "GetAuditRecord",
        "ListOperationTimeline",
        "GetOperationTimelineEntry",
    ];

    private static readonly string[] OpsConsoleOperationIds =
    [
        "GetReadinessDiagnostics",
        "GetLockDiagnostics",
        "GetDirtyStateDiagnostics",
        "GetFailedOperationDiagnostics",
        "GetProviderStatusDiagnostics",
        "GetSyncStatusDiagnostics",
        "GetProjectionFreshness",
    ];

    private static readonly string[] StoryOperationIds = [.. AuditOperationIds, .. OpsConsoleOperationIds];

    [Fact]
    public void AuditOpsConsoleOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] allOperations = EnumerateOperations(root).ToArray();
        Operation[] operations = allOperations.Where(o => StoryOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        operations.Select(o => o.OperationId).Order(StringComparer.Ordinal).ShouldBe(StoryOperationIds.Order(StringComparer.Ordinal));
        operations.Select(o => o.OperationId).ShouldBeUnique();

        // Broader negative-case guard: operationIds must also be unique across the entire spec, not only
        // within this story's allow-list. A Story 1.11 operationId colliding with an unrelated operationId
        // elsewhere would silently slip past the per-story uniqueness check.
        allOperations.Select(o => o.OperationId).ShouldBeUnique();

        Dictionary<string, (string Method, string Path)> expected = new(StringComparer.Ordinal)
        {
            ["ListAuditTrail"] = ("get", "/api/v1/folders/{folderId}/audit-trail"),
            ["GetAuditRecord"] = ("get", "/api/v1/folders/{folderId}/audit-trail/{auditRecordId}"),
            ["ListOperationTimeline"] = ("get", "/api/v1/folders/{folderId}/operation-timeline"),
            ["GetOperationTimelineEntry"] = ("get", "/api/v1/folders/{folderId}/operation-timeline/{timelineEntryId}"),
            ["GetReadinessDiagnostics"] = ("get", "/api/v1/ops-console/readiness-diagnostics"),
            ["GetLockDiagnostics"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics"),
            ["GetDirtyStateDiagnostics"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics"),
            ["GetFailedOperationDiagnostics"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics"),
            ["GetProviderStatusDiagnostics"] = ("get", "/api/v1/folders/{folderId}/ops-console/provider-status-diagnostics"),
            ["GetSyncStatusDiagnostics"] = ("get", "/api/v1/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics"),
            ["GetProjectionFreshness"] = ("get", "/api/v1/ops-console/projection-freshness"),
        };

        foreach (Operation operation in operations)
        {
            (string method, string path) = expected[operation.OperationId];
            operation.Method.ShouldBe(method, operation.OperationId);
            operation.Path.ShouldBe(path, operation.OperationId);
        }

        ResolveRefs(root);
    }

    [Fact]
    public void AuditOpsConsoleQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => StoryOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        foreach (Operation operation in operations)
        {
            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            // Reject the canonical $ref AND any inline declaration that names the header `Idempotency-Key`,
            // so a future contributor cannot reintroduce idempotency on a query via an alternate $ref path
            // or by declaring the header inline.
            parameters.Any(p => GetOptionalScalar(p, "$ref") == "#/components/parameters/IdempotencyKey").ShouldBeFalse(operation.OperationId);
            parameters.Any(p => string.Equals(GetOptionalScalar(p, "name"), "Idempotency-Key", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeFalse(operation.OperationId);

            RequiredMapping(operation.Node, "x-hexalith-read-consistency");
            RequiredMapping(operation.Node, "x-hexalith-correlation");
            RequiredMapping(operation.Node, "x-hexalith-authorization");
            RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories");
            RequiredSequence(operation.Node, "x-hexalith-audit-metadata-keys");
            RequiredMapping(operation.Node, "x-hexalith-parity-dimensions");

            string serializedOperation = SerializeYaml(operation.Node);
            serializedOperation.ShouldContain("tenant authority", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("authentication-context-and-eventstore-envelope", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("safe-denial", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("metadata-only", Case.Insensitive, operation.OperationId);
            serializedOperation.ShouldContain("diagnostic audience", Case.Insensitive, operation.OperationId);

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            foreach (string expected in new[] { "authentication_failure", "tenant_access_denied", "read_model_unavailable", "projection_stale", "projection_unavailable", "not_found", "redacted", "internal_error" })
            {
                categories.ShouldContain(expected, operation.OperationId);
            }

            if (AuditOperationIds.Contains(operation.OperationId, StringComparer.Ordinal))
            {
                categories.ShouldContain("audit_access_denied", operation.OperationId);
            }
        }
    }

    [Fact]
    public void AuditOpsConsoleSchemas_DeclareAudienceClassificationFreshnessAndMetadataOnlyExamples()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        foreach (string schemaName in new[]
        {
            "AuditTrailPage",
            "AuditRecord",
            "OperationTimelinePage",
            "OperationTimelineEntry",
            "ReadinessDiagnostics",
            "LockDiagnostics",
            "DirtyStateDiagnostics",
            "FailedOperationDiagnostics",
            "ProviderStatusDiagnostics",
            "SyncStatusDiagnostics",
            "ProjectionFreshnessDiagnostics",
            "DiagnosticAudience",
            "DiagnosticFieldClassification",
            "DiagnosticTrustEvidence",
            "DiagnosticSafeIdentifier",
            "ChangedPathEvidence",
            "ProjectionAvailability",
        })
        {
            RequiredMapping(schemas, schemaName);
        }

        RequiredEnumValues(schemas, "DiagnosticAudience").ShouldBe(["consumer", "authorized_operator"]);
        RequiredEnumValues(schemas, "DiagnosticFieldClassification").ShouldBe(["consumer_safe", "operator_sanitized", "forbidden"]);
        RequiredEnumValues(schemas, "ProjectionAvailability").ShouldBe(["available", "stale", "unavailable", "redacted", "unknown"]);

        string[] operatorLabels = RequiredEnumValues(schemas, "OperatorDispositionLabel");
        foreach (string label in new[] { "auto_recovering", "available", "degraded_but_serving", "awaiting_human", "terminal_until_intervention" })
        {
            operatorLabels.ShouldContain(label);
        }

        foreach (string exampleName in new[]
        {
            "AuditTrailPage",
            "AuditRecordRedacted",
            "OperationTimelinePage",
            "OperationTimelineEntry",
            "ReadinessDiagnostics",
            "LockDiagnostics",
            "DirtyStateDiagnostics",
            "FailedOperationDiagnostics",
            "ProviderStatusDiagnostics",
            "SyncStatusDiagnostics",
            "ProjectionFreshnessDiagnostics",
            "ProjectionStaleProblem",
            "ProjectionUnavailableProblem",
        })
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string combinedExamples = string.Join("\n", examples.Children.Select(entry => SerializeYaml(entry.Value)));

        // Shape-based forbidden patterns. These complement the corpus-driven sentinel check below.
        // Patterns target raw leak shapes, not legitimate vocabulary substrings; the previous "secret" /
        // "token_" substring tests are removed because they false-positive on `secret`/`credential_sensitive`
        // vocabulary references and false-negative on encoded tokens.
        string[] forbiddenLeakPatterns =
        [
            "diff --git",
            "rawCommitMessage",
            "raw_commit_message",
            "rawChangedPaths",
            "rawProviderPayload",
            "generatedContext",
            "github.com/",
            "forgejo.example",
            "C:\\",
            "/home/",
            "@example.com",
            "Bearer ",
        ];

        foreach (string forbidden in forbiddenLeakPatterns)
        {
            combinedExamples.ShouldNotContain(forbidden, Case.Insensitive, $"Story 1.11 examples must stay synthetic and metadata-only: {forbidden}");
        }

        // Project-context rule: redaction/sentinel tests MUST iterate `tests/fixtures/audit-leakage-corpus.json`
        // and assert that none of its synthetic sentinel values are baked into contract examples. The corpus is
        // the canonical source for leakage canaries; a hardcoded inline list would drift.
        File.Exists(AuditLeakageCorpusPath).ShouldBeTrue(AuditLeakageCorpusPath);
        using JsonDocument corpus = JsonDocument.Parse(File.ReadAllText(AuditLeakageCorpusPath));
        JsonElement sentinels = corpus.RootElement.GetProperty("sentinel_samples");
        sentinels.GetArrayLength().ShouldBeGreaterThan(0);
        foreach (JsonElement sentinel in sentinels.EnumerateArray())
        {
            string sentinelValue = sentinel.GetProperty("value").GetString() ?? string.Empty;
            sentinelValue.ShouldNotBeNullOrWhiteSpace();
            combinedExamples.ShouldNotContain(
                sentinelValue,
                Case.Insensitive,
                $"audit-leakage-corpus sentinel `{sentinel.GetProperty("id").GetString()}` must not appear in Story 1.11 contract examples.");
        }

        string changedPathEvidence = SerializeYaml(RequiredMapping(schemas, "ChangedPathEvidence"));
        changedPathEvidence.ShouldContain("digest", Case.Insensitive);
        changedPathEvidence.ShouldContain("reference", Case.Insensitive);
        changedPathEvidence.ShouldNotContain("raw changed path", Case.Insensitive);
    }

    [Fact]
    public void AuditOpsConsoleOperations_DeclareConsistentSafeDenialResponseCodes()
    {
        // AC 7 / AC 19 require hidden-resource equivalence across audit and ops-console operations.
        // The minimum safe-denial response set is {401, 403, 404, 503} for every operation that names
        // `projection_stale` in its canonical-error-categories must also expose 409. Asymmetry here would
        // let callers fingerprint authorization audience or projection state via the response-code surface.
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] operations = EnumerateOperations(root).Where(o => StoryOperationIds.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();
        operations.Length.ShouldBe(StoryOperationIds.Length);

        foreach (Operation operation in operations)
        {
            YamlMappingNode responses = RequiredMapping(operation.Node, "responses");
            string[] codes = responses.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToArray();

            foreach (string expected in new[] { "200", "401", "403", "404", "503" })
            {
                codes.ShouldContain(expected, $"{operation.OperationId} must declare HTTP {expected} for safe-denial parity.");
            }

            string[] canonicalCategories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(v => v.Value ?? string.Empty)
                .ToArray();

            if (canonicalCategories.Contains("projection_stale", StringComparer.Ordinal))
            {
                codes.ShouldContain("409", $"{operation.OperationId} declares projection_stale in canonical-error-categories and must therefore declare HTTP 409 for projection-stale parity.");
            }
        }
    }

    [Fact]
    public void AuditOpsConsoleSchemas_CloseReviewPatchGaps()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");

        foreach (string schemaName in new[]
        {
            "PrefixedOpaqueIdentifier",
            "RedactableDiagnosticIdentifier",
            "RedactableAuditActorReference",
            "RedactableAuditOperationReference",
            "RedactableAuditTimestamp",
        })
        {
            RequiredMapping(schemas, schemaName);
        }

        string diagnosticBase = SerializeYaml(RequiredMapping(schemas, "DiagnosticBase"));
        diagnosticBase.ShouldNotContain("additionalProperties: false", Case.Insensitive);
        diagnosticBase.ShouldContain("fieldClassifications", Case.Insensitive);
        diagnosticBase.ShouldContain("minItems: 1", Case.Insensitive);
        diagnosticBase.ShouldContain("if:", Case.Insensitive);
        diagnosticBase.ShouldContain("then:", Case.Insensitive);

        foreach (string schemaName in new[]
        {
            "ReadinessDiagnostics",
            "LockDiagnostics",
            "DirtyStateDiagnostics",
            "FailedOperationDiagnostics",
            "ProviderStatusDiagnostics",
            "SyncStatusDiagnostics",
        })
        {
            string schema = SerializeYaml(RequiredMapping(schemas, schemaName));
            schema.ShouldContain("unevaluatedProperties: false", Case.Insensitive, schemaName);
        }

        string readiness = SerializeYaml(RequiredMapping(schemas, "ReadinessDiagnostics"));
        readiness.ShouldContain("providerSummaryReference", Case.Insensitive);
        readiness.ShouldContain("folderSummaryReference", Case.Insensitive);
        readiness.ShouldContain("workspaceSummaryReference", Case.Insensitive);

        string auditRecord = SerializeYaml(RequiredMapping(schemas, "AuditRecord"));
        auditRecord.ShouldContain("RedactableAuditActorReference", Case.Insensitive);
        auditRecord.ShouldContain("RedactableAuditOperationReference", Case.Insensitive);
        auditRecord.ShouldContain("RedactableAuditTimestamp", Case.Insensitive);

        string operationTimelineEntry = SerializeYaml(RequiredMapping(schemas, "OperationTimelineEntry"));
        operationTimelineEntry.ShouldContain("workspaceReference", Case.Insensitive);
        operationTimelineEntry.ShouldContain("RedactableDiagnosticIdentifier", Case.Insensitive);

        string lockDiagnostics = SerializeYaml(RequiredMapping(schemas, "LockDiagnostics"));
        lockDiagnostics.ShouldContain("RedactableDiagnosticIdentifier", Case.Insensitive);
        string providerStatusDiagnostics = SerializeYaml(RequiredMapping(schemas, "ProviderStatusDiagnostics"));
        providerStatusDiagnostics.ShouldContain("RedactableDiagnosticIdentifier", Case.Insensitive);

        string trustEvidence = SerializeYaml(RequiredMapping(schemas, "DiagnosticTrustEvidence"));
        trustEvidence.ShouldContain("availability", Case.Insensitive);
        trustEvidence.ShouldContain("freshnessAgeMilliseconds", Case.Insensitive);

        string projectionFreshness = SerializeYaml(RequiredMapping(schemas, "ProjectionFreshnessDiagnostics"));
        projectionFreshness.ShouldContain("if:", Case.Insensitive);
        projectionFreshness.ShouldContain("then:", Case.Insensitive);
    }

    [Fact]
    public void AuditOpsConsoleContracts_DeclareCursorAudienceAndBoundaryNegativeCases()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        foreach (string exampleName in new[]
        {
            "AuditTrailPageEmpty",
            "AuditTrailPageLimitBoundary",
            "OperationTimelinePageEmpty",
            "OperationTimelinePageLimitBoundary",
            "CursorTamperProblem",
            "PrincipalMismatchSafeDenialProblem",
            "InvalidSortProblem",
            "BoundaryDuplicatePage",
            "EmptyPageContinuation",
        })
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        string notes = File.Exists(ContractNotesPath) ? File.ReadAllText(ContractNotesPath) : string.Empty;
        string combinedExamples = string.Join("\n", examples.Children.Select(entry => SerializeYaml(entry.Value)));
        string combined = notes + "\n" + combinedExamples;

        foreach (string required in new[]
        {
            "tampered-cursor",
            "changed-filter",
            "tenant/principal-mismatch",
            "invalid-sort",
            "boundary-duplicate",
            "empty-page continuation",
            "auto_recovering",
            "available",
        })
        {
            combined.ShouldContain(required, Case.Insensitive);
        }
    }

    [Fact]
    public void AuditOpsConsoleReviewSweep_PropagatesWidenedCategoriesToEarlierStoryOperations()
    {
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        Operation[] earlierStoryOperations = EnumerateOperations(root)
            .Where(o => !StoryOperationIds.Contains(o.OperationId, StringComparer.Ordinal))
            .ToArray();

        // Triggers for projection-stale / projection-unavailable propagation: any operation that already
        // declares it is read-model- or projection-backed should also declare the widened categories so a
        // partial adoption (declaring projection_stale but not projection_unavailable, or vice versa) is
        // mechanically rejected.
        string[] projectionTouchingTriggers =
        [
            "read_model_unavailable",
            "projection_stale",
            "projection_unavailable",
            "query_timeout",
            "response_limit_exceeded",
        ];

        // Triggers for failed_operation propagation: any operation that contemplates a provider-failure-or-
        // reconciliation outcome should declare the widened canonical category so adopters can switch on a
        // single value regardless of whether the failure was provider, projection, or reconciliation flavored.
        // Availability/rate categories (`provider_unavailable`, `provider_rate_limited`) are intentionally
        // excluded — they describe transient unavailability rather than a terminal operation outcome.
        string[] failedOperationTriggers =
        [
            "provider_failure_known",
            "unknown_provider_outcome",
            "reconciliation_required",
        ];

        List<string> sweptProjectionOperations = new(earlierStoryOperations.Length);
        List<string> sweptFailedOperationOperations = new(earlierStoryOperations.Length);

        foreach (Operation operation in earlierStoryOperations)
        {
            if (!operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-canonical-error-categories")))
            {
                continue;
            }

            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            if (categories.Any(c => projectionTouchingTriggers.Contains(c, StringComparer.Ordinal)))
            {
                categories.ShouldContain("projection_stale", $"{operation.OperationId} must consume the widened projection-stale category.");
                categories.ShouldContain("projection_unavailable", $"{operation.OperationId} must consume the widened projection-unavailable category.");
                sweptProjectionOperations.Add(operation.OperationId);
            }

            if (categories.Any(c => failedOperationTriggers.Contains(c, StringComparer.Ordinal)))
            {
                categories.ShouldContain("failed_operation", $"{operation.OperationId} must consume the widened failed-operation category.");
                sweptFailedOperationOperations.Add(operation.OperationId);
            }
        }

        // Explicit positive coverage assertion: the sweep must reach a non-trivial set of earlier-story
        // operations. A regression where every earlier-story operation drops its projection-touching trigger
        // (silently bypassing the sweep) should fail the test rather than passing vacuously.
        sweptProjectionOperations.Count.ShouldBeGreaterThanOrEqualTo(1, "P-Sweep-1 sweep must reach at least one earlier-story operation; zero hits would indicate the trigger set was bypassed.");
        sweptFailedOperationOperations.Count.ShouldBeGreaterThanOrEqualTo(1, "P-Sweep-1 sweep must reach at least one earlier-story operation declaring a provider-or-reconciliation failure path.");
    }

    [Fact]
    public void AuditOpsConsoleNotes_RecordEvidenceMapAudienceMatrixAndDeferredPolicy()
    {
        File.Exists(ContractNotesPath).ShouldBeTrue(ContractNotesPath);
        string notes = File.ReadAllText(ContractNotesPath);

        foreach (string required in new[]
        {
            "Evidence Map",
            "Audience And Field Classification Matrix",
            "C3 retention",
            "C4 limits",
            "C5 freshness",
            "C6 lifecycle",
            "TODO(reference-pending): C5 projection freshness target",
            "consumer_safe",
            "operator_sanitized",
            "forbidden",
            "read-only",
            "metadata-only",
        })
        {
            notes.ShouldContain(required, Case.Insensitive);
        }

        notes.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);
    }

    [Fact]
    public void AuditOpsConsoleNegativeScope_DoesNotAddRuntimeAdaptersGeneratedClientsUiOrCi()
    {
        string notes = File.Exists(ContractNotesPath) ? File.ReadAllText(ContractNotesPath) : string.Empty;
        string combined = File.ReadAllText(OpenApiPath) + "\n" + notes;

        foreach (string forbidden in new[]
        {
            "repair action",
            "credential reveal",
            "raw diff",
            "file browsing",
            "unrestricted filesystem browsing",
            "FrontComposer page",
            "generated SDK",
            "MCP tool",
            "CLI command",
            "runtime handler",
        })
        {
            combined.ShouldNotContain(forbidden, Case.Insensitive, $"Story 1.11 must stay contract-only and read-only: {forbidden}");
        }

        (string Project, string Pattern, string Description)[] forbiddenCategories =
        [
            ("Hexalith.Folders.Server", "Endpoints/**/*.cs", "REST handlers/endpoints"),
            ("Hexalith.Folders.Server", "Commands/**/*.cs", "server-side commands"),
            ("Hexalith.Folders", "Aggregates/**/*.cs", "domain aggregate behavior"),
            ("Hexalith.Folders", "Providers/**/*.cs", "provider adapters"),
            ("Hexalith.Folders.Cli", "Commands/**/*.cs", "CLI commands"),
            ("Hexalith.Folders.Mcp", "Tools/**/*.cs", "MCP tools"),
            ("Hexalith.Folders.Workers", "Handlers/**/*.cs", "worker event handlers"),
            ("Hexalith.Folders.UI", "Pages/**/*.razor", "UI pages"),
        ];

        string srcRoot = Path.Combine(RepositoryRoot, "src");
        foreach ((string project, string pattern, string description) in forbiddenCategories)
        {
            string projectRoot = Path.Combine(srcRoot, project);
            if (!Directory.Exists(projectRoot))
            {
                continue;
            }

            string[] matches = Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    return !relative.StartsWith("obj/", StringComparison.Ordinal)
                        && !relative.StartsWith("bin/", StringComparison.Ordinal)
                        && MatchesGlob(relative, pattern);
                })
                .ToArray();

            matches.ShouldBeEmpty($"Story 1.11 negative scope: {description} ({project}/{pattern}) must not be added.");
        }
    }

    [Fact]
    public void AuditOpsConsoleSchemas_EnforceBehavioralInvariantsFromFollowUpReview()
    {
        // Behavioral negative-case coverage for the invariants added by the 2026-05-15 follow-up review:
        //   * `RedactableDiagnosticIdentifier`/`RedactableAuditActorReference`/`RedactableAuditOperationReference`
        //     reject co-presence of `value` and `redaction.visibility: redacted`.
        //   * `RedactableAuditTimestamp` rejects co-presence of `value` and either `precision: redacted` or
        //     `redaction.visibility: redacted`.
        //   * `DiagnosticBase` and `ProjectionFreshnessDiagnostics` bind `trust.availability` (or `availability`)
        //     to `freshness.stale` only when both fields are required in the conditional branches.
        //   * `WorkspaceLockStatus.required` references only declared properties (catches partial renames).
        //   * `LockDiagnostics`/`ProviderStatusDiagnostics`/`ReadinessDiagnostics` carry audience-conditional
        //     classification constraints on their wrapped identifier fields.
        //   * Every page example conforms to `PaginationMetadata` required: [limit, isTruncated] and rejects
        //     the legacy `hasMore` key under `additionalProperties: false`.
        YamlMappingNode root = LoadYamlMapping(OpenApiPath);
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        foreach (string wrapperName in new[]
        {
            "RedactableDiagnosticIdentifier",
            "RedactableAuditActorReference",
            "RedactableAuditOperationReference",
        })
        {
            YamlMappingNode wrapper = RequiredMapping(schemas, wrapperName);
            string serialized = SerializeYaml(wrapper);
            serialized.ShouldContain("allOf:", Case.Insensitive, wrapperName);
            serialized.ShouldContain("visibility:", Case.Insensitive, wrapperName);
            serialized.ShouldContain("const: redacted", Case.Insensitive, wrapperName);
            serialized.ShouldContain("not:", Case.Insensitive, $"{wrapperName} must carry a `not: required: [value]` clause that forbids cleartext when redacted.");
            serialized.ShouldMatch(@"not:[\s\S]*?required:[\s\S]*?value", $"{wrapperName} must include `value` inside the forbidden-when-redacted (`not: required:`) clause.");
        }

        YamlMappingNode timestampWrapper = RequiredMapping(schemas, "RedactableAuditTimestamp");
        string timestampSerialized = SerializeYaml(timestampWrapper);
        timestampSerialized.ShouldContain("precision:", Case.Insensitive);
        timestampSerialized.ShouldContain("const: redacted", Case.Insensitive);
        timestampSerialized.ShouldContain("not:", Case.Insensitive, "RedactableAuditTimestamp must forbid `value` when `precision: redacted` and when `redaction.visibility: redacted`.");
        timestampSerialized.ShouldMatch(@"not:[\s\S]*?required:[\s\S]*?value", "RedactableAuditTimestamp must include `value` inside the forbidden-when-redacted (`not: required:`) clause.");

        YamlMappingNode diagnosticBase = RequiredMapping(schemas, "DiagnosticBase");
        string diagnosticBaseSerialized = SerializeYaml(diagnosticBase);
        // The if/then branches must require `availability` (so the conditional fires only when the field is
        // present) and require `stale` inside the matching `then.freshness` (so the constraint doesn't pass
        // vacuously when `stale` is omitted). The substring shape below mirrors how YamlDotNet serializes the
        // nested `required: [availability]` and `required: [stale]` inline-flow markers.
        diagnosticBaseSerialized.ShouldContain("required:", Case.Insensitive);
        diagnosticBaseSerialized.ShouldMatch(@"if:[\s\S]*?required:[\s\S]*?availability[\s\S]*?then:[\s\S]*?required:[\s\S]*?stale", "DiagnosticBase if/then must require both `availability` (inside `if`) and `stale` (inside `then`) so the invariant is enforced rather than passing vacuously.");

        YamlMappingNode projectionFreshness = RequiredMapping(schemas, "ProjectionFreshnessDiagnostics");
        string projectionFreshnessSerialized = SerializeYaml(projectionFreshness);
        projectionFreshnessSerialized.ShouldMatch(@"if:[\s\S]*?required:[\s\S]*?availability[\s\S]*?then:[\s\S]*?required:[\s\S]*?stale", "ProjectionFreshnessDiagnostics if/then must require both `availability` (inside `if`) and `stale` (inside `then`).");
        projectionFreshnessSerialized.ShouldContain("unevaluatedProperties: false", Case.Insensitive, "ProjectionFreshnessDiagnostics must use unevaluatedProperties: false for consistency with the six other diagnostic subclasses composed via allOf.");

        // WorkspaceLockStatus correctness gate: every `required` entry must appear in `properties`. This catches
        // partial-rename regressions where a property gets a new name but the required list still references
        // the old name (the schema would become unsatisfiable under `additionalProperties: false`).
        YamlMappingNode workspaceLockStatus = RequiredMapping(schemas, "WorkspaceLockStatus");
        string[] requiredKeys = RequiredSequence(workspaceLockStatus, "required").OfType<YamlScalarNode>().Select(n => n.Value ?? string.Empty).ToArray();
        YamlMappingNode workspaceLockProperties = RequiredMapping(workspaceLockStatus, "properties");
        string[] declaredKeys = workspaceLockProperties.Children.Keys.OfType<YamlScalarNode>().Select(n => n.Value ?? string.Empty).ToArray();
        foreach (string requiredKey in requiredKeys)
        {
            declaredKeys.ShouldContain(requiredKey, $"WorkspaceLockStatus declares `{requiredKey}` in `required:` but does not declare it in `properties:`. The schema would be unsatisfiable under `additionalProperties: false`.");
        }

        // Audience-conditional classification structural assertion: each diagnostic subclass that carries a
        // RedactableDiagnosticIdentifier field must constrain its classification value by `audience`.
        foreach (string subclassName in new[]
        {
            "LockDiagnostics",
            "ProviderStatusDiagnostics",
            "ReadinessDiagnostics",
        })
        {
            string subclassSerialized = SerializeYaml(RequiredMapping(schemas, subclassName));
            subclassSerialized.ShouldContain("audience:", Case.Insensitive, subclassName);
            subclassSerialized.ShouldContain("const: consumer", Case.Insensitive, $"{subclassName} must constrain wrapper classification when `audience: consumer`.");
            subclassSerialized.ShouldContain("const: authorized_operator", Case.Insensitive, $"{subclassName} must constrain wrapper classification when `audience: authorized_operator`.");
            subclassSerialized.ShouldContain("const: consumer_safe", Case.Insensitive, $"{subclassName} must require `consumer_safe` classification for consumer audiences.");
            subclassSerialized.ShouldContain("const: operator_sanitized", Case.Insensitive, $"{subclassName} must require `operator_sanitized` classification for operator audiences.");
        }

        // Pagination example shape: every example that declares a `page` mapping must use `isTruncated` and
        // must NOT use the legacy `hasMore` key (rejected by `additionalProperties: false` on
        // `PaginationMetadata`). This guards against the P2 regression class where new examples drift from
        // the schema's authoritative key vocabulary.
        foreach (KeyValuePair<YamlNode, YamlNode> exampleEntry in examples.Children)
        {
            string exampleName = exampleEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            if (exampleEntry.Value is not YamlMappingNode exampleNode || !exampleNode.Children.TryGetValue(new YamlScalarNode("value"), out YamlNode? valueNode) || valueNode is not YamlMappingNode value)
            {
                continue;
            }

            if (!value.Children.TryGetValue(new YamlScalarNode("page"), out YamlNode? pageNode) || pageNode is not YamlMappingNode page)
            {
                continue;
            }

            page.Children.ContainsKey(new YamlScalarNode("hasMore")).ShouldBeFalse($"Example `{exampleName}` uses the legacy `hasMore` key; `PaginationMetadata` requires `isTruncated` under `additionalProperties: false`.");
            page.Children.ContainsKey(new YamlScalarNode("isTruncated")).ShouldBeTrue($"Example `{exampleName}` is missing the required `isTruncated` key from `PaginationMetadata.required`.");
            page.Children.ContainsKey(new YamlScalarNode("limit")).ShouldBeTrue($"Example `{exampleName}` is missing the required `limit` key from `PaginationMetadata.required`.");
        }

        // PrefixedOpaqueIdentifier boundary alignment: the shared pattern and per-prefix sibling patterns must
        // all use the `{8,119}` tail so that the pattern range and the `minLength: 16 / maxLength: 128` length
        // range agree at both boundaries for every known prefix (`actorref_`, `digest_`, `changeref_`, `provref_`).
        YamlMappingNode prefixedSchema = RequiredMapping(schemas, "PrefixedOpaqueIdentifier");
        GetScalar(prefixedSchema, "pattern").ShouldContain("{8,119}", Case.Sensitive, "PrefixedOpaqueIdentifier pattern tail must be {8,119} to keep both length-extreme boundaries reachable for the registered prefixes.");
        GetScalar(prefixedSchema, "pattern").ShouldContain("[a-z][a-z0-9]{2,}_", Case.Sensitive, "PrefixedOpaqueIdentifier prefix must require at least three lowercase characters so a single-letter namespace cannot defeat domain classification.");
    }

    private static bool MatchesGlob(string relativePath, string pattern)
    {
        string regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(relativePath, regexPattern);
    }

    private sealed record Operation(string Path, string Method, string OperationId, YamlMappingNode Node, YamlMappingNode PathItem);

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

    private static string[] RequiredEnumValues(YamlMappingNode schemas, string schemaName)
    {
        return RequiredSequence(RequiredMapping(schemas, schemaName), "enum")
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();
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
