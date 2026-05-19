using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

public static class FolderAuthorizationDenialMapper
{
    public static IResult ToHttpResult(LayeredFolderAuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsAllowed)
        {
            throw new ArgumentException("Only denied authorization results can be mapped to safe Problem Details.", nameof(result));
        }

        LayeredFolderAuthorizationDecisionSnapshot decision = result.Decision;
        (int statusCode, string category) = StatusAndCategory(decision);

        return Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{decision.OutcomeCode}",
            title: Title(statusCode),
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = category,
                ["code"] = decision.OutcomeCode,
                ["message"] = Message(category),
                ["correlationId"] = decision.CorrelationId,
                ["taskId"] = decision.TaskId,
                ["retryable"] = decision.Retryable,
                ["clientAction"] = decision.Retryable ? "retry" : "no_action",
                ["details"] = new Dictionary<string, object?>
                {
                    ["visibility"] = "metadata_only",
                    ["layer"] = decision.TerminalLayer.ToString(),
                    ["policyClass"] = decision.OperationPolicyClass,
                    ["freshnessClass"] = decision.FreshnessClass,
                    ["timingBucket"] = decision.TimingBucket,
                },
            });
    }

    private static (int StatusCode, string Category) StatusAndCategory(LayeredFolderAuthorizationDecisionSnapshot decision)
        => decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied =>
                (StatusCodes.Status401Unauthorized, "authentication_failure"),
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied =>
                (StatusCodes.Status404NotFound, "not_found_to_caller"),
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale =>
                (StatusCodes.Status503ServiceUnavailable, "read_model_unavailable"),
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when decision.Retryable =>
                (StatusCodes.Status503ServiceUnavailable, "policy_evidence_unavailable"),
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed when decision.Retryable =>
                (StatusCodes.Status503ServiceUnavailable, "read_model_unavailable"),
            _ => (StatusCodes.Status403Forbidden, "tenant_access_denied"),
        };

    private static string Title(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Authentication required.",
            StatusCodes.Status404NotFound => "Resource not available.",
            StatusCodes.Status503ServiceUnavailable => "Authorization evidence unavailable.",
            _ => "Authorization denied.",
        };

    private static string Message(string category)
        => category switch
        {
            "authentication_failure" => "Authentication is required to access this resource.",
            "read_model_unavailable" => "Authorization evidence is temporarily unavailable. Retry later.",
            "policy_evidence_unavailable" => "Policy evidence is temporarily unavailable. Retry later.",
            "not_found_to_caller" => "The requested resource is not available to the caller.",
            _ => "Access is denied. The caller is not authorized for this operation or resource.",
        };
}
