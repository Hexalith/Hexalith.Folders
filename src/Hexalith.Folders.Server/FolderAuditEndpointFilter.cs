using System.Diagnostics;

using Hexalith.Folders.Observability;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Server;

public sealed class FolderAuditEndpointFilter(
    IFolderTelemetryEmitter telemetryEmitter,
    ITenantContextAccessor tenantContext) : IEndpointFilter
{
    private readonly IFolderTelemetryEmitter _telemetryEmitter =
        telemetryEmitter ?? throw new ArgumentNullException(nameof(telemetryEmitter));
    private readonly ITenantContextAccessor _tenantContext =
        tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            object? result = await next(context).ConfigureAwait(false);
            stopwatch.Stop();
            if (!ShouldSkipEndpointObservation(context.HttpContext, result))
            {
                await EmitAsync(context.HttpContext, result, null, stopwatch.Elapsed).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            await EmitAsync(context.HttpContext, null, ex, stopwatch.Elapsed).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask EmitAsync(HttpContext httpContext, object? result, Exception? exception, TimeSpan duration)
    {
        int? statusCode = result is IStatusCodeHttpResult statusCodeResult
            ? statusCodeResult.StatusCode
            : httpContext.Response.HasStarted ? httpContext.Response.StatusCode : null;

        string? endpointName = httpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        bool isIdempotentReplay = IsIdempotentReplay(result);
        FolderAuditObservation observation = new FolderAuditObservationBuilder
        {
            OperationKind = OperationKind(httpContext, endpointName),
            Result = Result(statusCode, exception, isIdempotentReplay),
            TenantId = _tenantContext.AuthoritativeTenantId,
            ActorReference = _tenantContext.PrincipalId,
            TaskId = Header(httpContext, "X-Hexalith-Task-Id"),
            OperationId = Header(httpContext, "X-Hexalith-Operation-Id") ?? RouteValue(httpContext, "operationId"),
            CorrelationId = Header(httpContext, "X-Correlation-Id"),
            FolderId = RouteValue(httpContext, "folderId"),
            WorkspaceId = RouteValue(httpContext, "workspaceId"),
            ProviderReference = ProviderReference(result),
            Timestamp = DateTimeOffset.UtcNow,
            Duration = duration,
            RedactionState = exception is null ? FolderAuditRedactionState.MetadataOnly : FolderAuditRedactionState.Redacted,
            SanitizedCategory = Category(statusCode, exception, isIdempotentReplay),
            IsRetry = Header(httpContext, "X-Hexalith-Retry") is not null,
            IsIdempotentReplay = isIdempotentReplay,
            IsDuplicate = statusCode == StatusCodes.Status409Conflict && !isIdempotentReplay,
        }.AddClassification("endpoint.kind", EndpointClassification(httpContext.Request.Method))
            .AddClassification("endpoint.name", EndpointNameClassification(endpointName))
            .Build();

        await _telemetryEmitter.EmitAsync(observation, httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static bool ShouldSkipEndpointObservation(HttpContext httpContext, object? result)
    {
        if (!string.Equals(httpContext.Request.Path.Value, FoldersServerModule.ProcessRoute, StringComparison.Ordinal))
        {
            return false;
        }

        int? statusCode = result is IStatusCodeHttpResult statusCodeResult
            ? statusCodeResult.StatusCode
            : null;

        // Domain command outcomes are observed by FolderDomainProcessor so command type,
        // replay, and rejection categories come from the EventStore result instead of the
        // transport-level 200 envelope. Transport validation/authorization failures still
        // pass through this endpoint filter because the processor never runs for them.
        return statusCode is null or < StatusCodes.Status400BadRequest;
    }

    private static FolderAuditOperationKind OperationKind(HttpContext httpContext, string? endpointName)
    {
        if (string.Equals(httpContext.Request.Path.Value, FoldersServerModule.ProcessRoute, StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.ProcessCommand;
        }

        if (endpointName is not null && endpointName.Contains("Provider", StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.ProviderReadiness;
        }

        if (endpointName is not null && endpointName.Contains("Commit", StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.CommitOperation;
        }

        if (endpointName is not null && endpointName.Contains("Lock", StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.LockOperation;
        }

        if (endpointName is not null && endpointName.Contains("Cleanup", StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.CleanupStatus;
        }

        if (endpointName is "AddWorkspaceFile" or "ChangeWorkspaceFile" or "RemoveWorkspaceFile")
        {
            return FolderAuditOperationKind.FileOperation;
        }

        if (endpointName is not null && endpointName.Contains("File", StringComparison.Ordinal))
        {
            return FolderAuditOperationKind.ContextQuery;
        }

        return HttpMethods.IsGet(httpContext.Request.Method)
            ? FolderAuditOperationKind.RestQuery
            : FolderAuditOperationKind.RestMutation;
    }

    private static FolderAuditResult Result(int? statusCode, Exception? exception, bool isIdempotentReplay)
    {
        if (exception is not null)
        {
            return FolderAuditResult.Failed;
        }

        if (isIdempotentReplay && statusCode is null or < 400)
        {
            return FolderAuditResult.Replayed;
        }

        return statusCode switch
        {
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound => FolderAuditResult.Denied,
            StatusCodes.Status408RequestTimeout => FolderAuditResult.Failed,
            StatusCodes.Status409Conflict => FolderAuditResult.Duplicate,
            StatusCodes.Status422UnprocessableEntity => FolderAuditResult.Rejected,
            StatusCodes.Status429TooManyRequests => FolderAuditResult.Retried,
            StatusCodes.Status503ServiceUnavailable => FolderAuditResult.Unavailable,
            >= 400 => FolderAuditResult.Rejected,
            _ => FolderAuditResult.Success,
        };
    }

    private static string Category(int? statusCode, Exception? exception, bool isIdempotentReplay)
    {
        if (exception is not null)
        {
            return "unhandled_exception";
        }

        if (isIdempotentReplay && statusCode is null or < 400)
        {
            return "idempotent_replay";
        }

        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "validation_error",
            StatusCodes.Status401Unauthorized => "authentication_failure",
            StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound => "authorization_denied",
            StatusCodes.Status408RequestTimeout => "query_timeout",
            StatusCodes.Status409Conflict => "duplicate_operation",
            StatusCodes.Status422UnprocessableEntity => "domain_rejection",
            StatusCodes.Status429TooManyRequests => "provider_rate_limited",
            StatusCodes.Status503ServiceUnavailable => "projection_unavailable",
            >= 500 => "runtime_failure",
            _ => "operation_completed",
        };
    }

    private static string EndpointClassification(string method)
        => HttpMethods.IsGet(method) ? "query" : "mutation";

    private static string EndpointNameClassification(string? endpointName)
        => endpointName switch
        {
            null or "" => "unknown",
            _ when endpointName.Contains("Provider", StringComparison.Ordinal) => "provider_readiness",
            _ when endpointName.Contains("Commit", StringComparison.Ordinal) => "commit",
            _ when endpointName.Contains("Lock", StringComparison.Ordinal) => "lock",
            _ when endpointName.Contains("Cleanup", StringComparison.Ordinal) => "cleanup",
            _ when endpointName.Contains("File", StringComparison.Ordinal) => "file_context",
            _ when endpointName.Contains("Status", StringComparison.Ordinal) => "status",
            _ => "folder_operation",
        };

    private static string? Header(HttpContext httpContext, string name)
        => httpContext.Request.Headers.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues values)
            ? values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            : null;

    private static string? RouteValue(HttpContext httpContext, string name)
        => httpContext.Request.RouteValues.TryGetValue(name, out object? value)
            ? value?.ToString()
            : null;

    private static string? ProviderReference(object? result)
    {
        if (result is not IValueHttpResult valueResult || valueResult.Value is null)
        {
            return null;
        }

        if (valueResult.Value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind == System.Text.Json.JsonValueKind.Object
                && element.TryGetProperty("providerReference", out System.Text.Json.JsonElement providerReference)
                && providerReference.ValueKind == System.Text.Json.JsonValueKind.String
                    ? providerReference.GetString()
                    : null;
        }

        System.Reflection.PropertyInfo? property = valueResult.Value.GetType().GetProperty("ProviderReference")
            ?? valueResult.Value.GetType().GetProperty("providerReference");

        return property?.PropertyType == typeof(string)
            ? property.GetValue(valueResult.Value) as string
            : null;
    }

    private static bool IsIdempotentReplay(object? result)
    {
        if (result is not IValueHttpResult valueResult || valueResult.Value is null)
        {
            return false;
        }

        if (valueResult.Value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind == System.Text.Json.JsonValueKind.Object
                && element.TryGetProperty("idempotentReplay", out System.Text.Json.JsonElement replay)
                && replay.ValueKind == System.Text.Json.JsonValueKind.True;
        }

        Type valueType = valueResult.Value.GetType();
        System.Reflection.PropertyInfo? property = valueType.GetProperty("IdempotentReplay")
            ?? valueType.GetProperty("idempotentReplay");

        return property?.PropertyType == typeof(bool)
            && property.GetValue(valueResult.Value) is true;
    }
}
