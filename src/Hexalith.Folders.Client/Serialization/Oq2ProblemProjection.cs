using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>Projects only declared, fully validated OQ2 problem result types.</summary>
internal static class Oq2ProblemProjection
{
    public static (ProblemDetails? Problem, string? Diagnostic) Project(HexalithFoldersApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            return exception switch
            {
                HexalithFoldersApiException<FileSafeResourceUnavailableProblem> value when value.StatusCode == 404 =>
                    (ProjectExact(RoundTrip(value.Result), 404, CanonicalErrorCategory.Tenant_access_denied, CanonicalErrorCode.Resource_unavailable, "Access unavailable", "The requested resource is unavailable.", false, ProblemDetailsClientAction.No_action, "redacted"), null),
                HexalithFoldersApiException<FileRangeUnsatisfiableProblem> value when value.StatusCode == 416 =>
                    (ProjectExact(RoundTrip(value.Result), 416, CanonicalErrorCategory.Range_unsatisfiable, CanonicalErrorCode.Range_unsatisfiable, "Range unsatisfiable", "The requested byte range cannot be satisfied.", false, ProblemDetailsClientAction.Revise_request, "metadata_only"), null),
                HexalithFoldersApiException<FilePolicyUnavailableProblem> value when value.StatusCode == 503 =>
                    (ProjectExact(RoundTrip(value.Result), 503, CanonicalErrorCategory.File_policy_unavailable, CanonicalErrorCode.File_policy_unavailable, "File policy unavailable", "The file policy cannot be verified for this request.", true, ProblemDetailsClientAction.Retry, "redacted"), null),
                HexalithFoldersApiException<FileContentEvidenceInvalidProblem> value when value.StatusCode == 400 =>
                    (ProjectExact(RoundTrip(value.Result), 400, CanonicalErrorCategory.Validation_error, CanonicalErrorCode.Content_evidence_invalid, "Content evidence invalid", "The supplied content evidence is not valid.", false, ProblemDetailsClientAction.Revise_request, "metadata_only"), null),
                HexalithFoldersApiException<FileInlineTransportRequiredProblem> value when value.StatusCode == 413 =>
                    (ProjectExact(RoundTrip(value.Result), 413, CanonicalErrorCategory.Input_limit_exceeded, CanonicalErrorCode.D9_inline_limit_exceeded, "Inline payload too large", "The inline payload exceeds the configured D-9 boundary.", true, ProblemDetailsClientAction.Revise_request, "metadata_only"), null),
                HexalithFoldersApiException<FileContentLimitExceededProblem> value when value.StatusCode == 422 =>
                    (ProjectExact(RoundTrip(value.Result), 422, CanonicalErrorCategory.Input_limit_exceeded, CanonicalErrorCode.File_content_limit_exceeded, "File content limit exceeded", "The file content exceeds the permitted maximum.", false, ProblemDetailsClientAction.Revise_request, "metadata_only"), null),
                HexalithFoldersApiException<AuthenticationFailureProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<SafeDenialProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<AuthorityUnavailableProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<OperationSpecificUnavailableProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileContentEvidenceInvalidOrValidationProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileContentLimitExceededOrWorkspaceTransitionProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileMutationUnavailableProblem> value => ProjectProblem(value, exception.StatusCode),
                HexalithFoldersApiException<FileContextUnavailableProblem> value => ProjectProblem(value, exception.StatusCode),
                _ => (null, "unsupported_problem_result_type"),
            };
        }
        catch (Exception projectionFailure) when (projectionFailure is JsonException or InvalidOperationException)
        {
            return (null, projectionFailure.GetType().Name);
        }
    }

    private static (ProblemDetails? Problem, string? Diagnostic) ProjectProblem<T>(HexalithFoldersApiException<T> exception, int httpStatus)
        where T : ProblemDetails
    {
        T typedProblem = RoundTrip(exception.Result);
        ProblemDetails problem = JsonConvert.DeserializeObject<ProblemDetails>(JsonConvert.SerializeObject(typedProblem))
            ?? throw new JsonSerializationException("The typed problem result could not be projected to ProblemDetails.");
        return problem.Status == httpStatus
            ? (problem, null)
            : (null, "http_status_mismatch");
    }

    private static T RoundTrip<T>(T? value)
        where T : class
    {
        if (value is null)
        {
            throw new JsonSerializationException("The typed problem result is null.");
        }

        string json = JsonConvert.SerializeObject(value);
        return JsonConvert.DeserializeObject<T>(json)
            ?? throw new JsonSerializationException("The typed problem result could not be validated.");
    }

    private static ProblemDetails ProjectExact(
        ExactFileProblem value,
        int status,
        CanonicalErrorCategory category,
        CanonicalErrorCode code,
        string title,
        string message,
        bool retryable,
        ProblemDetailsClientAction clientAction,
        string visibility) => new()
        {
            Type = value.Type,
            Title = title,
            Status = status,
            Category = category,
            Code = code,
            Message = message,
            CorrelationId = value.CorrelationId,
            Retryable = retryable,
            ClientAction = clientAction,
            Details = new Details
            {
                Visibility = visibility switch
                {
                    "redacted" => DetailsVisibility.Redacted,
                    "metadata_only" => DetailsVisibility.Metadata_only,
                    _ => throw new InvalidOperationException($"Unsupported exact problem visibility '{visibility}'."),
                },
            },
        };
}
