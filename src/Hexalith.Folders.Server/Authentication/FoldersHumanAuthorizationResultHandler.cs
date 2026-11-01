using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Preserves safe v1 problem envelopes when the named human boundary denies access before handler work.</summary>
internal sealed class FoldersHumanAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _standard = new();

    /// <inheritdoc/>
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        bool humanV1 = context.Request.Path.StartsWithSegments("/api/v1")
            && context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(item => item.Policy == EventStoreHttpHostSecurityExtensions.HumanPolicy) == true;
        await _standard.HandleAsync(next, context, policy, result).ConfigureAwait(false);
        if (!humanV1 || result.Succeeded || context.Response.HasStarted) return;

        int status = result.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        string category = result.Forbidden ? "tenant_access_denied" : "authentication_failure";
        string correlation = FolderCanonicalPathIdentifier.SanitizeCorrelationId(FolderHttpHeaderReader.ReadHeader(context, "X-Correlation-Id"));
        string? task = FolderHttpHeaderReader.ReadHeader(context, "X-Hexalith-Task-Id");
        if (!FolderCanonicalSegmentIdentifier.IsValid(task)) task = null;
        string path = context.Request.Path.Value!;
        IResult problem = path.Contains("/ops-console/", StringComparison.Ordinal)
            ? FolderProblemDetailsFactory.ForOpsConsole(status, category, category, false, correlation)
            : path.StartsWith("/api/v1/provider-readiness/", StringComparison.Ordinal)
                || HttpMethods.IsGet(context.Request.Method) && path.StartsWith("/api/v1/provider-bindings/", StringComparison.Ordinal)
                ? FolderProblemDetailsFactory.ForProviderReadiness(status, category, category, false, correlation)
                : path.Contains("/audit-trail", StringComparison.Ordinal) || path.Contains("/operation-timeline", StringComparison.Ordinal)
                    ? FolderProblemDetailsFactory.ForAudit(status, category, category, false, correlation, task)
                    : FolderProblemDetailsFactory.ForDomain(status, category, category, false, correlation, task);
        await problem.ExecuteAsync(context).ConfigureAwait(false);
    }
}
