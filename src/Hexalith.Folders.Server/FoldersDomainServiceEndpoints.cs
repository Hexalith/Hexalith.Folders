using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.Folders.Server;

public static class FoldersDomainServiceEndpoints
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapFoldersDomainServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(FoldersServerModule.ProcessRoute, async (
            DomainServiceRequest request,
            FoldersDomainServiceRequestHandler handler,
            CancellationToken cancellationToken)
            => await handler.ProcessAsync(request, cancellationToken).ConfigureAwait(false));

        endpoints.MapPost(FoldersServerModule.ProjectRoute, (ProjectionRequest _) =>
            Results.Problem(
                type: "https://hexalith.dev/errors/folders/projection-not-implemented",
                title: "Folders projection endpoint is not implemented yet.",
                statusCode: StatusCodes.Status501NotImplemented,
                extensions: new Dictionary<string, object?>
                {
                    ["category"] = "not_implemented",
                    ["code"] = "projection_not_implemented",
                }));

        endpoints.MapGet("/api/v1/folders/{folderId}/effective-permissions", async (
            string folderId,
            HttpContext httpContext,
            EffectivePermissionsQueryHandler handler,
            ITenantContextAccessor tenantContext,
            CancellationToken cancellationToken)
            =>
        {
            EffectivePermissionsQueryResult result = await handler.HandleAsync(
                new EffectivePermissionsQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId ?? string.Empty,
                    ReadHeader(httpContext, "X-Correlation-Id"),
                    TaskContextId: ReadHeader(httpContext, "X-Hexalith-Task-Id"),
                    WorkspaceContextId: ReadHeader(httpContext, "X-Hexalith-Workspace-Id"),
                    ClientControlledTenantIds: ClientTenantIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(result);
        });

        return endpoints;
    }

    private static IResult ToHttpResult(EffectivePermissionsQueryResult result)
        => result.Code switch
        {
            EffectivePermissionsResultCode.Allowed
                or EffectivePermissionsResultCode.DeniedSafe
                or EffectivePermissionsResultCode.ProjectionStale => Results.Json(
                    new EffectivePermissionsResponse(
                        result.FolderId ?? string.Empty,
                        result.Permissions.Select(PermissionToken).ToArray(),
                        result.AuthorizationOutcome,
                        new FreshnessMetadataResponse(
                            result.Freshness.ReadConsistency,
                            result.Freshness.ObservedAt,
                            result.Freshness.ProjectionWatermark,
                            result.Freshness.Stale)),
                    ResponseJsonOptions),
            EffectivePermissionsResultCode.AuthenticationRequired => SafeProblem(
                StatusCodes.Status401Unauthorized,
                "authentication_failure",
                retryable: false),
            EffectivePermissionsResultCode.NotFoundSafe => SafeProblem(
                StatusCodes.Status404NotFound,
                "denied_safe",
                retryable: false),
            EffectivePermissionsResultCode.ReadModelUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                "read_model_unavailable",
                retryable: true),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                "denied_safe",
                retryable: false),
        };

    private static IResult SafeProblem(int statusCode, string code, bool retryable)
        => Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: statusCode == StatusCodes.Status503ServiceUnavailable
                ? "Read model unavailable."
                : "Authorization denied.",
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = statusCode == StatusCodes.Status401Unauthorized
                    ? "authentication"
                    : statusCode == StatusCodes.Status503ServiceUnavailable
                        ? "read_model"
                        : "authorization",
                ["code"] = code,
                ["retryable"] = retryable,
            });

    private static string PermissionToken(EffectivePermissionLevel permission)
        => permission switch
        {
            EffectivePermissionLevel.Read => "read",
            EffectivePermissionLevel.Write => "write",
            EffectivePermissionLevel.Administer => "administer",
            _ => "read",
        };

    private static IReadOnlyDictionary<string, string?> ClientTenantIds(HttpContext httpContext)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_tenant_id"] = ReadQuery(httpContext, "tenantId"),
            ["query_managed_tenant_id"] = ReadQuery(httpContext, "managedTenantId"),
            ["header_hexalith_tenant_id"] = ReadHeader(httpContext, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = ReadHeader(httpContext, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = ReadHeader(httpContext, "X-Forwarded-Tenant"),
        };

    private static string? ReadHeader(HttpContext httpContext, string name)
    {
        string value = httpContext.Request.Headers.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues values)
            ? values.ToString()
            : string.Empty;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadQuery(HttpContext httpContext, string name)
    {
        string value = httpContext.Request.Query.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues values)
            ? values.ToString()
            : string.Empty;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record EffectivePermissionsResponse(
        string FolderId,
        IReadOnlyList<string> Permissions,
        string AuthorizationOutcome,
        FreshnessMetadataResponse Freshness);

    private sealed record FreshnessMetadataResponse(
        string ReadConsistency,
        DateTimeOffset ObservedAt,
        string? ProjectionWatermark,
        bool Stale);
}
