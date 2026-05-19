using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

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
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            EffectivePermissionsQueryResult result = await handler.HandleAsync(
                new EffectivePermissionsQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId ?? string.Empty,
                    correlationId,
                    TaskContextId: ReadHeader(httpContext, "X-Hexalith-Task-Id"),
                    WorkspaceContextId: ReadHeader(httpContext, "X-Hexalith-Workspace-Id"),
                    ClientControlledTenantIds: ClientTenantIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return ToHttpResult(result, correlationId);
        })
        .WithName("GetEffectivePermissions");

        endpoints.MapGet("/api/v1/folders/{folderId}/lifecycle-status", async (
            string folderId,
            HttpContext httpContext,
            FolderLifecycleStatusQueryHandler handler,
            ITenantContextAccessor tenantContext,
            IEventStoreClaimTransformEvidenceAccessor claimTransformEvidence,
            CancellationToken cancellationToken)
            =>
        {
            string? correlationId = ReadHeader(httpContext, "X-Correlation-Id");
            FolderLifecycleStatusQueryResult result = await handler.HandleAsync(
                new FolderLifecycleStatusQuery(
                    folderId,
                    tenantContext.AuthoritativeTenantId,
                    tenantContext.PrincipalId,
                    claimTransformEvidence.GetEvidence("read_metadata"),
                    correlationId,
                    TaskId: ReadHeader(httpContext, "X-Hexalith-Task-Id"),
                    ClientControlledTenantValues: ClientTenantIds(httpContext),
                    ClientControlledPrincipalValues: ClientPrincipalIds(httpContext)),
                cancellationToken).ConfigureAwait(false);

            AddLifecycleHeaders(httpContext, result);
            return ToHttpResult(result, correlationId);
        })
        .WithName("GetFolderLifecycleStatus");

        return endpoints;
    }

    private static IResult ToHttpResult(FolderLifecycleStatusQueryResult result, string? correlationId)
    {
        if (result.AuthorizationDenial is not null)
        {
            return FolderAuthorizationDenialMapper.ToHttpResult(result.AuthorizationDenial);
        }

        return result.Code switch
        {
            FolderLifecycleStatusResultCode.Allowed => Results.Json(
                new FolderLifecycleStatusResponse(
                    result.FolderId ?? string.Empty,
                    result.LifecycleState ?? "inaccessible",
                    result.Archived,
                    result.RepositoryBindingId,
                    result.ProviderBindingRef,
                    new FreshnessMetadataResponse(
                        result.Freshness.ReadConsistency,
                        result.Freshness.ObservedAt,
                        result.Freshness.ProjectionWatermark,
                        result.Freshness.Stale)),
                ResponseJsonOptions),
            FolderLifecycleStatusResultCode.AuthenticationRequired => SafeProblem(
                StatusCodes.Status401Unauthorized,
                category: "authentication_failure",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId),
            FolderLifecycleStatusResultCode.NotFoundSafe => SafeProblem(
                StatusCodes.Status404NotFound,
                category: "not_found_to_caller",
                code: "safe_not_found",
                retryable: false,
                correlationId: correlationId),
            FolderLifecycleStatusResultCode.ReadModelUnavailable
                or FolderLifecycleStatusResultCode.ProjectionStale => SafeProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    category: "read_model_unavailable",
                    code: "read_model_unavailable",
                    retryable: true,
                    correlationId: correlationId),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "authorization_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId),
        };
    }

    private static IResult ToHttpResult(EffectivePermissionsQueryResult result, string? correlationId)
        => result.Code switch
        {
            EffectivePermissionsResultCode.Allowed
                or EffectivePermissionsResultCode.DeniedSafe
                or EffectivePermissionsResultCode.NotFoundSafe
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
                category: "authentication_failure",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId),
            EffectivePermissionsResultCode.ReadModelUnavailable => SafeProblem(
                StatusCodes.Status503ServiceUnavailable,
                category: "read_model_unavailable",
                code: "read_model_unavailable",
                retryable: true,
                correlationId: correlationId),
            _ => SafeProblem(
                StatusCodes.Status403Forbidden,
                category: "tenant_access_denied",
                code: "denied_safe",
                retryable: false,
                correlationId: correlationId),
        };

    private static IResult SafeProblem(int statusCode, string category, string code, bool retryable, string? correlationId)
        => Results.Problem(
            type: $"https://hexalith.dev/errors/folders/{code}",
            title: statusCode switch
            {
                StatusCodes.Status401Unauthorized => "Authentication required.",
                StatusCodes.Status503ServiceUnavailable => "Read model unavailable.",
                _ => "Authorization denied.",
            },
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["category"] = category,
                ["code"] = code,
                ["message"] = MessageFor(category),
                ["correlationId"] = correlationId,
                ["retryable"] = retryable,
                ["clientAction"] = retryable ? "retry" : "no_action",
                ["details"] = new Dictionary<string, object?>
                {
                    ["visibility"] = "metadata_only",
                },
            });

    private static string MessageFor(string category) => category switch
    {
        "authentication_failure" => "Authentication is required to access this resource.",
        "read_model_unavailable" => "The read model is temporarily unavailable. Retry later.",
        _ => "Access is denied. The caller is not authorized for this operation or resource.",
    };

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

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext httpContext)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_principal_id"] = ReadQuery(httpContext, "principalId"),
            ["header_principal_id"] = ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = ReadHeader(httpContext, "X-Forwarded-Principal"),
        };

    private static void AddLifecycleHeaders(HttpContext httpContext, FolderLifecycleStatusQueryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CorrelationId))
        {
            httpContext.Response.Headers["X-Correlation-Id"] = result.CorrelationId;
        }

        httpContext.Response.Headers["X-Hexalith-Freshness"] = result.Freshness.ReadConsistency;
    }

    private static string? ReadHeader(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Headers.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static string? ReadQuery(HttpContext httpContext, string name)
        => FirstNonEmpty(httpContext.Request.Query.TryGetValue(name, out StringValues values) ? values : StringValues.Empty);

    private static string? FirstNonEmpty(StringValues values)
    {
        foreach (string? raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        return null;
    }

    private sealed record EffectivePermissionsResponse(
        string FolderId,
        IReadOnlyList<string> Permissions,
        string AuthorizationOutcome,
        FreshnessMetadataResponse Freshness);

    private sealed record FolderLifecycleStatusResponse(
        string FolderId,
        string LifecycleState,
        bool Archived,
        string? RepositoryBindingId,
        string? ProviderBindingRef,
        FreshnessMetadataResponse Freshness);

    private sealed record FreshnessMetadataResponse(
        string ReadConsistency,
        DateTimeOffset ObservedAt,
        string? ProjectionWatermark,
        bool Stale);
}
