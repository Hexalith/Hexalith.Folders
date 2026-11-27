using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Queries;
using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.EventStore;

/// <summary>Maps trusted Folders domain-service failures to metadata-only Gateway problems.</summary>
internal sealed class FoldersDomainServiceSafeDenialExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        int status = exception switch
        {
            DomainServiceSafeDenialException denial => denial.StatusCode,
            // A callback HTTP 503 indicates catalog refusal. EventStore 5.5.1 carries the
            // projection failure sentinel in the query exception instead.
            QueryExecutionFailedException { Domain: "folders", Detail: QueryAdapterFailureReason.ReadModelUnavailable } query
                when FoldersRuntimeCatalogDeclarations.Routes.Any(route =>
                    route is { Kind: "query", Execution.QueryKind: "projection" } && route.MessageType == query.QueryType)
                => StatusCodes.Status503ServiceUnavailable,
            _ => 0,
        };
        if (status == 0)
        {
            return false;
        }

        if (httpContext.RequestAborted.IsCancellationRequested || httpContext.Response.HasStarted)
        {
            return true;
        }

        string code = status switch
        {
            StatusCodes.Status401Unauthorized => "authentication_failure",
            StatusCodes.Status403Forbidden => "authorization_denied",
            StatusCodes.Status404NotFound => "not_found_to_caller",
            _ => "read_model_unavailable",
        };
        await Results.Problem(
            type: $"https://hexalith.io/problems/{code}",
            title: status switch
            {
                StatusCodes.Status401Unauthorized => "Authentication required",
                StatusCodes.Status404NotFound => "Resource not available",
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable",
                _ => "Authorization denied",
            },
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = code,
                ["code"] = code,
                ["retryable"] = status == StatusCodes.Status503ServiceUnavailable,
                ["details"] = new Dictionary<string, object?> { ["visibility"] = "metadata_only" },
            }).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
