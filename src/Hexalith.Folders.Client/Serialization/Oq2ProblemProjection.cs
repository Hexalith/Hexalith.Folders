using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>Projects only declared, fully validated OQ2 problem result types.</summary>
internal static class Oq2ProblemProjection
{
    private static readonly HashSet<string> GenericProblemVisibilities = EnumWireValues<DetailsVisibility>();
    private static readonly HashSet<string> GenericProblemDetailNames = new(StringComparer.Ordinal)
    {
        "attemptedTransition",
        "configuredLimit",
        "currentState",
        "dimension",
        "evidenceSource",
        "finalState",
        "leaseStatus",
        "lockStatus",
        "rangeRule",
        "reasonCategory",
        "retryReasonCode",
        "taskId",
        "todoRef",
        "unit",
        "visibility",
    };
    private static readonly HashSet<string> GenericProblemTuples = new(StringComparer.Ordinal)
    {
        TupleKey(400, "validation_error", "acl_entry_id_mismatch", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "content_evidence_invalid", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "cursor_tampered", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "filter_not_yet_supported", false, "revise_request", ["todoRef", "visibility"]),
        TupleKey(400, "validation_error", "idempotency_key_not_allowed", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "invalid_pagination", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "range_reversed", false, "revise_request", ["rangeRule", "visibility"]),
        TupleKey(400, "validation_error", "unsupported_archive_reason_code", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "unsupported_read_consistency", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "unsupported_request_schema_version", false, "revise_request", ["visibility"]),
        TupleKey(400, "validation_error", "validation_error", false, "revise_request", ["visibility"]),
        TupleKey(401, "authentication_failure", "authentication_required", false, "check_credentials", ["visibility"]),
        TupleKey(404, "tenant_access_denied", "resource_unavailable", false, "no_action", ["visibility"]),
        TupleKey(408, "query_timeout", "c4_query_timeout", false, "revise_request", ["configuredLimit", "unit", "visibility"]),
        TupleKey(408, "query_timeout", "query_timeout", true, "revise_request", ["visibility"]),
        TupleKey(409, "authorization_revocation_detected", "authorization_revocation_detected", false, "contact_operator", ["currentState", "visibility"]),
        TupleKey(409, "dirty_workspace", "dirty_workspace", false, "contact_operator", ["visibility"]),
        TupleKey(409, "duplicate_binding", "duplicate_binding", false, "revise_request", ["visibility"]),
        TupleKey(409, "idempotency_conflict", "idempotency_conflict", false, "revise_request", ["visibility"]),
        TupleKey(409, "idempotency_key_expired", "idempotency_key_expired", false, "refresh_state_then_submit_with_new_key", ["visibility"]),
        TupleKey(409, "lock_conflict", "workspace_locked", true, "retry", ["lockStatus", "visibility"]),
        TupleKey(409, "projection_stale", "projection_stale", true, "retry", ["visibility"]),
        TupleKey(409, "reconciliation_required", "reconciliation_required", false, "wait_for_reconciliation", ["visibility"]),
        TupleKey(409, "repository_conflict", "repository_conflict", false, "revise_request", ["visibility"]),
        TupleKey(409, "unknown_provider_outcome", "unknown_provider_outcome", false, "wait_for_reconciliation", ["visibility"]),
        TupleKey(410, "lock_expired", "lock_expired", true, "retry", ["leaseStatus", "visibility"]),
        TupleKey(413, "input_limit_exceeded", "c4_input_limit_exceeded", false, "revise_request", ["visibility"]),
        TupleKey(413, "input_limit_exceeded", "d9_inline_limit_exceeded", true, "revise_request", ["visibility"]),
        TupleKey(413, "response_limit_exceeded", "c4_response_budget_exceeded", false, "revise_request", ["configuredLimit", "unit", "visibility"]),
        TupleKey(413, "response_limit_exceeded", "response_limit_exceeded", false, "revise_request", ["visibility"]),
        TupleKey(416, "range_unsatisfiable", "range_unsatisfiable", false, "revise_request", ["visibility"]),
        TupleKey(422, "commit_failed", "commit_failed", false, "contact_operator", ["visibility"]),
        TupleKey(422, "input_limit_exceeded", "c4_input_limit_exceeded", false, "revise_request", ["dimension", "visibility"]),
        TupleKey(422, "input_limit_exceeded", "c4_range_limit_exceeded", false, "revise_request", ["configuredLimit", "unit", "visibility"]),
        TupleKey(422, "input_limit_exceeded", "file_content_limit_exceeded", false, "revise_request", ["visibility"]),
        TupleKey(422, "input_limit_exceeded", "input_limit_exceeded", false, "revise_request", ["visibility"]),
        TupleKey(422, "provider_readiness_failed", "provider_readiness_failed", false, "contact_operator", ["visibility"]),
        TupleKey(422, "state_transition_invalid", "state_transition_invalid", false, "revise_request", ["attemptedTransition", "currentState", "visibility"]),
        TupleKey(422, "state_transition_invalid", "state_transition_invalid", false, "revise_request", ["visibility"]),
        TupleKey(422, "unsupported_provider_capability", "unsupported_provider_capability", false, "contact_operator", ["visibility"]),
        TupleKey(422, "workspace_preparation_failed", "workspace_preparation_failed", false, "revise_request", ["visibility"]),
        TupleKey(423, "lock_conflict", "workspace_locked", true, "retry", ["lockStatus", "visibility"]),
        TupleKey(428, "authorization_revocation_detected", "authorization_revocation_detected", false, "contact_operator", ["currentState", "visibility"]),
        TupleKey(429, "provider_rate_limited", "provider_rate_limited", true, "retry", ["visibility"]),
        TupleKey(503, "file_policy_unavailable", "file_policy_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "idempotency_admission_unavailable", "idempotency_admission_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "internal_error", "archive_state_unsupported", false, "no_action", ["visibility"]),
        TupleKey(503, "internal_error", "read_model_unavailable", false, "no_action", ["visibility"]),
        TupleKey(503, "projection_unavailable", "projection_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "provider_failure_known", "provider_failure_known", false, "do_not_retry", ["visibility"]),
        TupleKey(503, "provider_unavailable", "provider_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "read_model_unavailable", "projection_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "read_model_unavailable", "evidence_unavailable", true, "retry", ["visibility"]),
        TupleKey(503, "reconciliation_required", "reconciliation_required", false, "wait_for_reconciliation", ["visibility"]),
        TupleKey(503, "unknown_provider_outcome", "unknown_provider_outcome", false, "wait_for_reconciliation", ["visibility"]),
    };

    private static readonly ExactProblemTuple AuthenticationRequired = new(
        401, "authentication_failure", "authentication_required", "Authentication required",
        "Authentication is required.", false, "check_credentials", "redacted");
    private static readonly ExactProblemTuple SafeDenial = new(
        404, "tenant_access_denied", "resource_unavailable", "Resource not available",
        "The requested resource is unavailable.", false, "no_action", "redacted");
    private static readonly ExactProblemTuple AuthorityUnavailable = new(
        503, "read_model_unavailable", "projection_unavailable", "Authorization evidence unavailable",
        "Authorization evidence is temporarily unavailable.", true, "retry", "redacted");
    private static readonly ExactProblemTuple FileSafeDenial = new(
        404, "tenant_access_denied", "resource_unavailable", "Access unavailable",
        "The requested resource is unavailable.", false, "no_action", "redacted");
    private static readonly ExactProblemTuple FileRangeUnsatisfiable = new(
        416, "range_unsatisfiable", "range_unsatisfiable", "Range unsatisfiable",
        "The requested byte range cannot be satisfied.", false, "revise_request", "metadata_only");
    private static readonly ExactProblemTuple FilePolicyUnavailable = new(
        503, "file_policy_unavailable", "file_policy_unavailable", "File policy unavailable",
        "The file policy cannot be verified for this request.", true, "retry", "redacted");
    private static readonly ExactProblemTuple FileContentEvidenceInvalid = new(
        400, "validation_error", "content_evidence_invalid", "Content evidence invalid",
        "The supplied content evidence is not valid.", false, "revise_request", "metadata_only");
    private static readonly ExactProblemTuple FileInlineTransportRequired = new(
        413, "input_limit_exceeded", "d9_inline_limit_exceeded", "Inline payload too large",
        "The inline payload exceeds the configured D-9 boundary.", true, "revise_request", "metadata_only");
    private static readonly ExactProblemTuple FileContentLimitExceeded = new(
        422, "input_limit_exceeded", "file_content_limit_exceeded", "File content limit exceeded",
        "The file content exceeds the permitted maximum.", false, "revise_request", "metadata_only");
    private static readonly ExactProblemTuple ReadModelUnavailable = new(
        503, "read_model_unavailable", "projection_unavailable", "Read model unavailable",
        "Projection data is temporarily unavailable.", true, "retry", "metadata_only");
    private static readonly ExactProblemTuple ProjectionUnavailable = new(
        503, "projection_unavailable", "projection_unavailable", "Projection unavailable",
        "Projection data is temporarily unavailable.", true, "retry", "metadata_only");
    private static readonly ExactProblemTuple ProviderUnavailable = new(
        503, "provider_unavailable", "provider_unavailable", "Provider unavailable",
        "Provider dependency is temporarily unavailable.", true, "retry", "metadata_only");
    private static readonly ExactProblemTuple ProviderFailureKnown = new(
        503, "provider_failure_known", "provider_failure_known", "Provider failure known",
        "Provider reported a known terminal failure for the requested workspace operation.", false, "do_not_retry", "metadata_only");
    private static readonly ExactProblemTuple UnknownProviderOutcome = new(
        503, "unknown_provider_outcome", "unknown_provider_outcome", "Unknown provider outcome",
        "Provider outcome is unknown for the requested workspace operation.", false, "wait_for_reconciliation", "metadata_only");
    private static readonly ExactProblemTuple ReconciliationRequired = new(
        503, "reconciliation_required", "reconciliation_required", "Reconciliation required",
        "The mutation outcome requires reconciliation before it can be finalized.", false, "wait_for_reconciliation", "metadata_only");
    private static readonly ExactProblemTuple CandidateProjectionUnavailable = new(
        503, "read_model_unavailable", "projection_unavailable", "Candidate request or downstream outcome",
        "The request could not be completed.", true, "retry", "metadata_only");
    private static readonly ExactProblemTuple CandidateIdempotencyAdmissionUnavailable = new(
        503, "idempotency_admission_unavailable", "idempotency_admission_unavailable", "Candidate request or downstream outcome",
        "The request could not be completed.", true, "retry", "metadata_only");
    private static readonly ExactProblemTuple CandidateArchiveStateUnsupported = new(
        503, "internal_error", "archive_state_unsupported", "Candidate request or downstream outcome",
        "The request could not be completed.", false, "no_action", "metadata_only");
    private static readonly ExactProblemTuple CandidateReadModelUnavailable = new(
        503, "internal_error", "read_model_unavailable", "Candidate request or downstream outcome",
        "The request could not be completed.", false, "no_action", "metadata_only");

    private static readonly HashSet<string> ReadModelUnavailableTypes = new(StringComparer.Ordinal)
    {
        "GetFolderLifecycleStatusUnavailableProblem",
        "ListFolderAclEntriesUnavailableProblem",
        "GetEffectivePermissionsUnavailableProblem",
        "GetProviderBindingUnavailableProblem",
        "GetProviderSupportEvidenceUnavailableProblem",
        "GetRepositoryBindingUnavailableProblem",
        "GetBranchRefPolicyUnavailableProblem",
        "GetWorkspaceLockUnavailableProblem",
        "GetWorkspaceRetryEligibilityUnavailableProblem",
        "GetWorkspaceTransitionEvidenceUnavailableProblem",
        "SearchFolderIndexedFilesUnavailableProblem",
        "GetFolderIndexingStatusUnavailableProblem",
        "GetWorkspaceStatusUnavailableProblem",
        "GetWorkspaceCleanupStatusUnavailableProblem",
        "GetTaskStatusUnavailableProblem",
        "GetCommitEvidenceUnavailableProblem",
        "GetProviderOutcomeUnavailableProblem",
        "GetReconciliationStatusUnavailableProblem",
        "GetAuditRecordUnavailableProblem",
        "ListOperationTimelineUnavailableProblem",
        "GetOperationTimelineEntryUnavailableProblem",
        "GetReadinessDiagnosticsUnavailableProblem",
        "GetLockDiagnosticsUnavailableProblem",
        "GetDirtyStateDiagnosticsUnavailableProblem",
        "GetFailedOperationDiagnosticsUnavailableProblem",
        "GetProviderStatusDiagnosticsUnavailableProblem",
        "GetSyncStatusDiagnosticsUnavailableProblem",
    };

    private static readonly HashSet<string> ProviderUnavailableTypes = new(StringComparer.Ordinal)
    {
        "ConfigureProviderBindingUnavailableProblem",
        "ValidateProviderReadinessUnavailableProblem",
        "CreateRepositoryBackedFolderUnavailableProblem",
        "BindRepositoryUnavailableProblem",
        "LockWorkspaceUnavailableProblem",
        "ReleaseWorkspaceLockUnavailableProblem",
    };

    private static readonly HashSet<string> FileContextUnavailableTypes = new(StringComparer.Ordinal)
    {
        "ListFolderFilesUnavailableProblem",
        "GetFolderFileMetadataUnavailableProblem",
        "SearchFolderFilesUnavailableProblem",
        "GlobFolderFilesUnavailableProblem",
        "ReadFileRangeUnavailableProblem",
    };

    private static readonly HashSet<string> IdempotentMutationUnavailableTypes = new(StringComparer.Ordinal)
    {
        "CreateFolderUnavailableProblem",
        "ArchiveFolderUnavailableProblem",
        "UpdateFolderAclEntryUnavailableProblem",
        "ConfigureProviderBindingUnavailableProblem",
        "CreateRepositoryBackedFolderUnavailableProblem",
        "BindRepositoryUnavailableProblem",
        "ConfigureBranchRefPolicyUnavailableProblem",
        "PrepareWorkspaceUnavailableProblem",
        "LockWorkspaceUnavailableProblem",
        "ReleaseWorkspaceLockUnavailableProblem",
        "AddFileUnavailableProblem",
        "ChangeFileUnavailableProblem",
        "RemoveFileUnavailableProblem",
        "CommitWorkspaceUnavailableProblem",
    };

    public static (ProblemDetails? Problem, string? Diagnostic) Project(HexalithFoldersApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            return exception switch
            {
                HexalithFoldersApiException<FileSafeResourceUnavailableProblem> value when value.StatusCode == 404 =>
                    ProjectExact(value, FileSafeDenial),
                HexalithFoldersApiException<FileRangeUnsatisfiableProblem> value when value.StatusCode == 416 =>
                    ProjectExact(value, FileRangeUnsatisfiable),
                HexalithFoldersApiException<FilePolicyUnavailableProblem> value when value.StatusCode == 503 =>
                    ProjectExact(value, FilePolicyUnavailable),
                HexalithFoldersApiException<FileContentEvidenceInvalidProblem> value when value.StatusCode == 400 =>
                    ProjectExact(value, FileContentEvidenceInvalid),
                HexalithFoldersApiException<FileInlineTransportRequiredProblem> value when value.StatusCode == 413 =>
                    ProjectExact(value, FileInlineTransportRequired),
                HexalithFoldersApiException<FileContentLimitExceededProblem> value when value.StatusCode == 422 =>
                    ProjectExact(value, FileContentLimitExceeded),
                HexalithFoldersApiException<AuthenticationFailureProblem> value => ProjectExact(value, AuthenticationRequired),
                HexalithFoldersApiException<SafeDenialProblem> value => ProjectExact(value, SafeDenial),
                HexalithFoldersApiException<AuthorityUnavailableProblem> value => ProjectExact(value, AuthorityUnavailable),
                HexalithFoldersApiException<ProblemDetails> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileContentEvidenceInvalidOrValidationProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileContentLimitExceededOrWorkspaceTransitionProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileMutationUnavailableProblem> value =>
                    ProjectExact(value, FilePolicyUnavailable, ReconciliationRequired, AuthorityUnavailable),
                HexalithFoldersApiException<FileContextUnavailableProblem> value =>
                    ProjectExact(value, FilePolicyUnavailable, ReadModelUnavailable, AuthorityUnavailable),
                _ => ProjectUnknownTypedProblem(exception),
            };
        }
        catch (Exception projectionFailure) when (projectionFailure is JsonException
            or InvalidOperationException
            or FormatException
            or OverflowException)
        {
            return (null, projectionFailure.GetType().Name);
        }
    }

    private static (ProblemDetails? Problem, string? Diagnostic) ProjectProblem<T>(HexalithFoldersApiException<T> exception, int httpStatus)
        where T : ProblemDetails
    {
        if (exception.Result is null)
        {
            return (null, "typed_problem_result_missing");
        }

        JObject wire = JObject.Parse(exception.Response, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        });
        string[] requiredNames =
        [
            "type", "title", "status", "category", "code", "message", "correlationId",
            "retryable", "clientAction", "details",
        ];
        HashSet<string> allowedNames = new(requiredNames, StringComparer.Ordinal)
        {
            "detail",
            "instance",
        };
        if (requiredNames.Any(name => wire.Property(name, StringComparison.Ordinal) is null)
            || wire.Properties().Any(property => !allowedNames.Contains(property.Name))
            || !IsUriReference(wire["type"])
            || wire["title"]?.Type != JTokenType.String
            || !IsIntegerInRange(wire["status"], 100, 599)
            || wire["category"]?.Type != JTokenType.String
            || wire["code"]?.Type != JTokenType.String
            || wire["message"]?.Type != JTokenType.String
            || wire["correlationId"]?.Type != JTokenType.String
            || wire["retryable"]?.Type != JTokenType.Boolean
            || wire["clientAction"]?.Type != JTokenType.String
            || wire.Property("detail", StringComparison.Ordinal) is { Value.Type: not JTokenType.String }
            || (wire.Property("instance", StringComparison.Ordinal) is { } instance
                && !IsUriReference(instance.Value))
            || wire["details"] is not JObject details
            || details.Property("visibility", StringComparison.Ordinal) is null
            || details.Properties().Any(property => !GenericProblemDetailNames.Contains(property.Name)
                || property.Value.Type != JTokenType.String)
            || !GenericProblemVisibilities.Contains(details.Value<string>("visibility") ?? string.Empty)
            || !IsOpaqueIdentifier((string?)wire["correlationId"])
            || !GenericProblemTuples.Contains(TupleKey(
                wire.Value<int>("status"),
                wire.Value<string>("category") ?? string.Empty,
                wire.Value<string>("code") ?? string.Empty,
                wire.Value<bool>("retryable"),
                wire.Value<string>("clientAction") ?? string.Empty,
                details.Properties().Select(static property => property.Name)))
            || !IsGenericTupleAllowedForOperation(
                exception.OriginatingOperationId,
                wire.Value<int>("status"),
                wire.Value<string>("category") ?? string.Empty,
                wire.Value<string>("code") ?? string.Empty,
                wire.Value<bool>("retryable"),
                wire.Value<string>("clientAction") ?? string.Empty,
                details.Properties().Select(static property => property.Name)))
        {
            return (null, "problem_shape_mismatch");
        }

        ProblemDetails problem = wire.ToObject<ProblemDetails>()
            ?? throw new JsonSerializationException("The typed problem result could not be projected to ProblemDetails.");
        return problem.Status == httpStatus
            ? (problem, null)
            : (null, "http_status_mismatch");
    }

    private static bool IsGenericTupleAllowedForOperation(
        string? operationId,
        int status,
        string category,
        string code,
        bool retryable,
        string clientAction,
        IEnumerable<string> detailKeys)
    {
        if (operationId is null)
        {
            return false;
        }

        return HexalithFoldersGeneratedOperationCatalog.AllowsGenericProblemTuple(
            operationId,
            TupleKey(status, category, code, retryable, clientAction, detailKeys));
    }

    private static (ProblemDetails? Problem, string? Diagnostic) ProjectUnknownTypedProblem(
        HexalithFoldersApiException exception)
    {
        object? typedResult = exception.GetType().GetProperty("Result")?.GetValue(exception);
        Type resultType = typedResult?.GetType() ?? typeof(void);
        bool generatedOperationUnavailableProblem = resultType.Namespace == typeof(ProblemDetails).Namespace
            && resultType.Name.EndsWith("UnavailableProblem", StringComparison.Ordinal);
        if (typedResult is not ProblemDetails && !generatedOperationUnavailableProblem)
        {
            return (null, "unsupported_problem_result_type");
        }

        ExactProblemTuple[]? allowed = AllowedUnavailableTuples(resultType.Name);
        return allowed is null
            ? (null, "unsupported_problem_result_type")
            : ProjectExact(exception, allowed);
    }

    private static (ProblemDetails? Problem, string? Diagnostic) ProjectExact(
        HexalithFoldersApiException exception,
        params ExactProblemTuple[] allowed)
    {
        JObject wire = JObject.Parse(exception.Response, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        });
        string[] expectedNames =
        [
            "type", "title", "status", "category", "code", "message", "correlationId",
            "retryable", "clientAction", "details",
        ];
        if (wire.Properties().Select(static property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expectedNames.Order(StringComparer.Ordinal), StringComparer.Ordinal) is false
            || wire["details"] is not JObject details
            || details.Properties().Select(static property => property.Name).SequenceEqual(["visibility"], StringComparer.Ordinal) is false)
        {
            return (null, "exact_problem_shape_mismatch");
        }

        string? correlationId = wire["correlationId"]?.Type == JTokenType.String
            ? wire.Value<string>("correlationId")
            : null;
        if (!IsOpaqueIdentifier(correlationId))
        {
            return (null, "exact_problem_correlation_mismatch");
        }

        bool matches = allowed.Any(tuple => tuple.Matches(wire, details));
        if (!matches)
        {
            return (null, "exact_problem_tuple_mismatch");
        }

        ProblemDetails problem = wire.ToObject<ProblemDetails>()
            ?? throw new JsonSerializationException("The exact problem result could not be projected to ProblemDetails.");
        return problem.Status == exception.StatusCode
            ? (problem, null)
            : (null, "http_status_mismatch");
    }

    private static ExactProblemTuple[]? AllowedUnavailableTuples(string resultTypeName)
    {
        if (!HexalithFoldersGeneratedOperationCatalog.Routes.Any(
            route => string.Equals(route.OperationId + "UnavailableProblem", resultTypeName, StringComparison.Ordinal)))
        {
            return null;
        }

        List<ExactProblemTuple> allowed = [];
        if (ReadModelUnavailableTypes.Contains(resultTypeName))
        {
            allowed.Add(ReadModelUnavailable);
        }

        if (ProviderUnavailableTypes.Contains(resultTypeName))
        {
            allowed.Add(ProviderUnavailable);
        }

        if (FileContextUnavailableTypes.Contains(resultTypeName))
        {
            allowed.AddRange([FilePolicyUnavailable, ReadModelUnavailable]);
        }

        allowed.AddRange(resultTypeName switch
        {
            "PrepareWorkspaceUnavailableProblem" => [ProviderUnavailable, UnknownProviderOutcome],
            "AddFileUnavailableProblem" or "ChangeFileUnavailableProblem" or "RemoveFileUnavailableProblem" =>
                [FilePolicyUnavailable, ReconciliationRequired],
            "CommitWorkspaceUnavailableProblem" => [ProviderUnavailable, ProviderFailureKnown],
            "ListAuditTrailUnavailableProblem" or "GetProjectionFreshnessUnavailableProblem" =>
                [ProjectionUnavailable],
            "GetFolderLifecycleStatusUnavailableProblem" =>
                [CandidateArchiveStateUnsupported, CandidateReadModelUnavailable],
            _ => [],
        });
        if (IdempotentMutationUnavailableTypes.Contains(resultTypeName))
        {
            allowed.Add(CandidateIdempotencyAdmissionUnavailable);
        }

        allowed.Add(CandidateProjectionUnavailable);
        allowed.Add(AuthorityUnavailable);
        return allowed.Distinct().ToArray();
    }

    internal static void ValidateGeneratedUnavailableProblem(string resultTypeName, JObject wire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultTypeName);
        ArgumentNullException.ThrowIfNull(wire);
        ExactProblemTuple[] allowed = AllowedUnavailableTuples(resultTypeName)
            ?? throw new JsonSerializationException($"Unsupported operation-unavailable problem type '{resultTypeName}'.");
        string[] expectedNames =
        [
            "type", "title", "status", "category", "code", "message", "correlationId",
            "retryable", "clientAction", "details",
        ];
        if (!wire.Properties().Select(static property => property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(expectedNames.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || wire["details"] is not JObject details
            || !details.Properties().Select(static property => property.Name)
                .SequenceEqual(["visibility"], StringComparer.Ordinal)
            || wire["correlationId"]?.Type != JTokenType.String
            || !IsOpaqueIdentifier(wire.Value<string>("correlationId"))
            || !allowed.Any(tuple => tuple.Matches(wire, details)))
        {
            throw new JsonSerializationException(
                $"The payload is not a declared exact tuple for '{resultTypeName}'.");
        }
    }

    private static string TupleKey(
        int status,
        string category,
        string code,
        bool retryable,
        string clientAction,
        IEnumerable<string> detailNames)
        => string.Join(
            '|',
            status,
            category,
            code,
            retryable ? "true" : "false",
            clientAction,
            string.Join(',', detailNames.Order(StringComparer.Ordinal)));

    private static bool IsOpaqueIdentifier(string? value)
        => value is not null
            && value.Length is >= 16 and <= 128
            && char.IsAsciiLetterOrDigit(value[0])
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static bool IsIntegerInRange(JToken? value, int minimum, int maximum)
        => value?.Type == JTokenType.Integer
            && int.TryParse(value.ToString(Formatting.None), out int parsed)
            && parsed >= minimum
            && parsed <= maximum;

    private static bool IsUriReference(JToken? value)
        => value?.Type == JTokenType.String
            && Uri.IsWellFormedUriString(value.Value<string>(), UriKind.RelativeOrAbsolute);

    private static HashSet<string> EnumWireValues<TEnum>()
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(static value =>
            {
                string memberName = value.ToString();
                FieldInfo field = typeof(TEnum).GetField(memberName)
                    ?? throw new InvalidOperationException($"Generated enum '{typeof(TEnum).Name}' has no member '{memberName}'.");
                return field.GetCustomAttribute<EnumMemberAttribute>()?.Value
                    ?? throw new InvalidOperationException($"Generated enum '{typeof(TEnum).Name}.{memberName}' has no wire value.");
            })
            .ToHashSet(StringComparer.Ordinal);

    private sealed record ExactProblemTuple(
        int Status,
        string Category,
        string Code,
        string Title,
        string Message,
        bool Retryable,
        string ClientAction,
        string Visibility)
    {
        public bool Matches(JObject wire, JObject details)
            => wire["type"]?.Type == JTokenType.String
                && wire["title"]?.Type == JTokenType.String
                && wire["status"]?.Type == JTokenType.Integer
                && wire["category"]?.Type == JTokenType.String
                && wire["code"]?.Type == JTokenType.String
                && wire["message"]?.Type == JTokenType.String
                && wire["retryable"]?.Type == JTokenType.Boolean
                && wire["clientAction"]?.Type == JTokenType.String
                && details["visibility"]?.Type == JTokenType.String
                && wire.Value<string>("type") == "about:blank"
                && wire.Value<string>("title") == Title
                && wire.Value<int>("status") == Status
                && wire.Value<string>("category") == Category
                && wire.Value<string>("code") == Code
                && wire.Value<string>("message") == Message
                && wire.Value<bool>("retryable") == Retryable
                && wire.Value<string>("clientAction") == ClientAction
                && details.Value<string>("visibility") == Visibility;
    }
}
