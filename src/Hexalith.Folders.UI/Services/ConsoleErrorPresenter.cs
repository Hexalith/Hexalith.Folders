using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / §3.9 — translates a thrown <see cref="HexalithFoldersApiException"/> into a
/// metadata-only <see cref="ConsoleErrorView"/> for the safe-denial / safe-error path. Consumes only the
/// SDK's validated canonical A-8 Problem Details projection; it never reparses the raw body, surfaces a stack trace, or exposes a
/// <c>taskId</c> off the error body (not an A-8 extension). Displayed explanations come from
/// <see cref="ConsoleStatusText.ResolveErrorExplanation(string)"/> (our safe copy), never the server
/// message, so denial categories can never be expanded into an existence oracle.
/// </summary>
public static class ConsoleErrorPresenter
{
    /// <summary>
    /// Builds a safe-error view from an SDK exception. <paramref name="fallbackCorrelationId"/> is the
    /// <c>x-correlation-id</c> the page sent, used when the Problem Details body carries none.
    /// </summary>
    public static ConsoleErrorView FromException(HexalithFoldersApiException exception, string fallbackCorrelationId)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string reasonToken = "internal_error";
        string correlationId = fallbackCorrelationId ?? string.Empty;
        bool? retryable = null;
        string? clientAction = null;

        if (exception.ProblemDetails is { } problem)
        {
            string category = ConsoleStatusText.ResolveErrorReasonToken(problem.Category);
            if (ConsoleStatusText.IsKnownReasonToken(category))
            {
                reasonToken = category;
            }

            correlationId = string.IsNullOrWhiteSpace(problem.CorrelationId)
                ? correlationId
                : problem.CorrelationId;
            retryable = problem.Retryable;
            clientAction = ResolveClientAction(problem.ClientAction);
        }

        return new ConsoleErrorView(
            reasonToken,
            ConsoleStatusText.ResolveErrorExplanation(reasonToken),
            correlationId,
            retryable,
            clientAction,
            ResolveDisposition(reasonToken));
    }

    private static ConsoleErrorDisposition ResolveDisposition(string reasonToken)
        => reasonToken switch
        {
            "tenant_access_denied" or "folder_acl_denied" or "authorization_revocation_detected" => ConsoleErrorDisposition.Denied,
            "read_model_unavailable" or "projection_stale" or "projection_unavailable" => ConsoleErrorDisposition.AuthorityUnavailable,
            _ => ConsoleErrorDisposition.Failure,
        };

    private static string ResolveClientAction(ProblemDetailsClientAction action)
        => action switch
        {
            ProblemDetailsClientAction.Retry => "retry",
            ProblemDetailsClientAction.Revise_request => "revise_request",
            ProblemDetailsClientAction.Check_credentials => "check_credentials",
            ProblemDetailsClientAction.Wait_for_reconciliation => "wait_for_reconciliation",
            ProblemDetailsClientAction.Contact_operator => "contact_operator",
            ProblemDetailsClientAction.No_action => "no_action",
            ProblemDetailsClientAction.Refresh_state_then_submit_with_new_key => "refresh_state_then_submit_with_new_key",
            ProblemDetailsClientAction.Do_not_retry => "do_not_retry",
            ProblemDetailsClientAction.Restart_query => "restart_query",
            _ => "no_action",
        };
}
