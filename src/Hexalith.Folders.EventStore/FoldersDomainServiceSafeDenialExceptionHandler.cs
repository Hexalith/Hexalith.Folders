using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.EventStore;

/// <summary>Projects only the closed trusted denial status; no downstream body crosses the gateway.</summary>
internal sealed class FoldersDomainServiceSafeDenialExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainServiceSafeDenialException denial)
        {
            return false;
        }

        if (httpContext.RequestAborted.IsCancellationRequested || httpContext.Response.HasStarted)
        {
            return true;
        }

        string code = denial.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => "authentication_failure",
            StatusCodes.Status403Forbidden => "authorization_denied",
            StatusCodes.Status404NotFound => "not_found_to_caller",
            _ => "read_model_unavailable",
        };
        await Results.Problem(
            type: $"https://hexalith.io/problems/{code}",
            title: denial.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Authentication required",
                StatusCodes.Status404NotFound => "Resource not available",
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable",
                _ => "Authorization denied",
            },
            statusCode: denial.StatusCode,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = code,
                ["code"] = code,
                ["retryable"] = denial.StatusCode == StatusCodes.Status503ServiceUnavailable,
                ["details"] = new Dictionary<string, object?> { ["visibility"] = "metadata_only" },
            }).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
