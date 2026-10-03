using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

/// <summary>Authorizes direct historical mutations before they can submit a keyed EventStore command.</summary>
internal static class HistoricalMutationAuthorization
{
    private const long MaximumRequestBodyBytes = 1_048_576;

    public static IApplicationBuilder UseHistoricalMutationAuthorization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            if (Pd10V2CandidateCompatibilitySeam.IsHistoricalDispatch(context)
                || !context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase)
                || !(HttpMethods.IsPost(context.Request.Method)
                    || HttpMethods.IsPut(context.Request.Method)
                    || HttpMethods.IsPatch(context.Request.Method)
                    || HttpMethods.IsDelete(context.Request.Method)))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!Pd10ProtectedOperationCatalog.TryResolveHistorical(
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty,
                    out Pd10ProtectedOperationDescriptor? descriptor,
                    out IReadOnlyDictionary<string, string> routeValues)
                || descriptor is null)
            {
                await Results.NotFound().ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            if (descriptor.PolicyClass != FolderOperationPolicyClass.Mutation)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            try
            {
                ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
                LayeredFolderAuthorizationService authorizer = context.RequestServices
                    .GetRequiredService<LayeredFolderAuthorizationService>();
                IEventStoreClaimTransformEvidenceAccessor claimAccessor = context.RequestServices
                    .GetRequiredService<IEventStoreClaimTransformEvidenceAccessor>();
                string? folderId = descriptor.FolderScope == Pd10FolderScopeRule.RouteFolder
                    && routeValues.TryGetValue("folderId", out string? routeFolder)
                        ? routeFolder
                        : null;
                string? correlationId = SafeIdentifier(context, "X-Correlation-Id");
                string? taskId = SafeIdentifier(context, "X-Hexalith-Task-Id");
                LayeredFolderAuthorizationContext authorizationContext = new(
                    tenant.AuthoritativeTenantId,
                    tenant.PrincipalId,
                    tenant.PrincipalId,
                    descriptor.HistoricalActionToken,
                    LayeredFolderOperationPolicy.Mutation(),
                    claimAccessor.GetEvidence(descriptor.HistoricalActionToken),
                    folderId,
                    correlationId,
                    taskId,
                    ClientControlledValues(context,
                        ("query_tenant_id", false, "tenantId"),
                        ("query_managed_tenant_id", false, "managedTenantId"),
                        ("header_hexalith_tenant_id", true, "X-Hexalith-Tenant-Id"),
                        ("header_tenant_id", true, "X-Tenant-Id"),
                        ("forwarded_tenant_id", true, "X-Forwarded-Tenant")),
                    ClientControlledValues(context,
                        ("query_principal_id", false, "principalId"),
                        ("header_principal_id", true, "X-Principal-Id"),
                        ("forwarded_principal_id", true, "X-Forwarded-Principal")));

                LayeredFolderAuthorizationResult authorization =
                    descriptor.FolderScope is Pd10FolderScopeRule.None or Pd10FolderScopeRule.RequestFolder
                        ? await authorizer.AuthorizeTenantScopedAsync(authorizationContext, context.RequestAborted).ConfigureAwait(false)
                        : await authorizer.AuthorizeAsync(authorizationContext, context.RequestAborted).ConfigureAwait(false);
                if (!authorization.IsAllowed)
                {
                    await FolderAuthorizationDenialMapper.ToHttpResult(authorization)
                        .ExecuteAsync(context).ConfigureAwait(false);
                    return;
                }

                if (descriptor.FolderScope == Pd10FolderScopeRule.RequestFolder)
                {
                    string? requestFolderId = await ReadRequestFolderIdAsync(context.Request, context.RequestAborted)
                        .ConfigureAwait(false);
                    if (requestFolderId is null)
                    {
                        await Results.BadRequest().ExecuteAsync(context).ConfigureAwait(false);
                        return;
                    }

                    authorization = await authorizer.AuthorizeAsync(
                        authorizationContext with { OperationScope = requestFolderId },
                        context.RequestAborted).ConfigureAwait(false);
                    if (!authorization.IsAllowed)
                    {
                        await FolderAuthorizationDenialMapper.ToHttpResult(authorization)
                            .ExecuteAsync(context).ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await FolderProblemDetailsFactory.ForDomain(
                    StatusCodes.Status503ServiceUnavailable,
                    "read_model_unavailable",
                    "evidence_unavailable",
                    retryable: true,
                    SafeIdentifier(context, "X-Correlation-Id"),
                    SafeIdentifier(context, "X-Hexalith-Task-Id"))
                    .ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    private static async Task<string?> ReadRequestFolderIdAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBodyBytes || request.Body == Stream.Null)
        {
            return null;
        }

        request.EnableBuffering(bufferLimit: MaximumRequestBodyBytes);
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root)
                || !root.TryGetProperty("folderId", out JsonElement folder)
                || folder.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? folderId = folder.GetString();
            return FolderCanonicalSegmentIdentifier.IsValid(folderId) ? folderId : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            return element.EnumerateObject().Any(property =>
                !names.Add(property.Name) || HasDuplicateProperties(property.Value));
        }

        return element.ValueKind == JsonValueKind.Array
            && element.EnumerateArray().Any(HasDuplicateProperties);
    }

    private static string? SafeIdentifier(HttpContext context, string name)
    {
        string? value = FolderHttpHeaderReader.ReadHeader(context, name);
        return value is not null
            && FolderCanonicalSegmentIdentifier.IsValid(value)
            && !FolderSensitiveDiagnosticDetector.IsSensitive(value)
                ? value
                : null;
    }

    private static IReadOnlyDictionary<string, string?> ClientControlledValues(
        HttpContext context,
        params (string Source, bool IsHeader, string Name)[] sources)
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        foreach ((string source, bool isHeader, string name) in sources)
        {
            IEnumerable<string?> supplied = isHeader
                ? context.Request.Headers[name]
                : context.Request.Query[name];
            int index = 0;
            foreach (string? value in supplied)
            {
                values[$"{source}[{index++}]"] = value;
            }
        }

        return values;
    }
}
