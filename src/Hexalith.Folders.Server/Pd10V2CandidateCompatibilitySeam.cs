using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

/// <summary>Provides the isolated, opt-in PD10 v2 candidate compatibility pipeline.</summary>
public static class Pd10V2CandidateCompatibilitySeam
{
    private const string CandidatePrefix = "/api/v2";

    /// <summary>Adds the candidate-only authorization and historical transport seam.</summary>
    public static IApplicationBuilder UsePd10V2CandidateCompatibilitySeam(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            string path = context.Request.Path.Value ?? string.Empty;
            if (!IsCandidatePath(path))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!Pd10ProtectedOperationCatalog.TryResolve(
                    context.Request.Method,
                    path,
                    out Pd10ProtectedOperationDescriptor? descriptor,
                    out IReadOnlyDictionary<string, string> routeValues)
                || descriptor is null)
            {
                await WriteProblemAsync(context, Pd10AuthorizationOutcome.SafeDenial, null).ConfigureAwait(false);
                return;
            }

            Pd10AuthorizationContext authorization;
            try
            {
                authorization = await AuthorizeAsync(context, descriptor, routeValues).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                authorization = Unusable(descriptor);
            }

            Func<CancellationToken, ValueTask<Pd10TaskFolderBindingState>>? binding =
                descriptor.TaskBinding == Pd10TaskBindingRule.RouteTaskBelongsToRouteFolder
                    ? token => VerifyTaskBindingAsync(context, routeValues, token)
                    : null;
            Pd10ProtectedOperationResult<bool> result = await Pd10ProtectedOperationExecutor.ExecuteAsync(
                authorization,
                binding,
                async token =>
                {
                    PathString originalPath = context.Request.Path;
                    try
                    {
                        context.Request.Path = Pd10ProtectedOperationCatalog.HistoricalPath(descriptor, routeValues);
                        await RewriteRequestAsync(context.Request, token).ConfigureAwait(false);
                        await InvokeHistoricalAsync(context, next).ConfigureAwait(false);
                        return true;
                    }
                    finally
                    {
                        context.Request.Path = originalPath;
                    }
                },
                context.RequestAborted).ConfigureAwait(false);
            if (!result.Outcome.IsAllowed)
            {
                await WriteProblemAsync(context, result.Outcome, null).ConfigureAwait(false);
            }
        });
    }

    private static async ValueTask<Pd10AuthorizationContext> AuthorizeAsync(
        HttpContext context,
        Pd10ProtectedOperationDescriptor descriptor,
        IReadOnlyDictionary<string, string> routeValues)
    {
        ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        string? tenantId = tenant.AuthoritativeTenantId;
        string? principalId = tenant.PrincipalId;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId))
        {
            return new(false, Pd10AuthorityEvidenceState.Unavailable, false, false, false, false, RequiresBinding(descriptor));
        }

        EventStoreClaimTransformEvidence claim = context.RequestServices
            .GetRequiredService<IEventStoreClaimTransformEvidenceAccessor>()
            .GetEvidence(descriptor.ActionToken);
        if (!claim.IsPresent || claim.Malformed)
        {
            return Unusable(descriptor);
        }

        if (!string.Equals(claim.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(claim.PrincipalId, principalId, StringComparison.Ordinal)
            || !claim.HasPermissionFor(descriptor.ActionToken))
        {
            return Denied(descriptor);
        }

        string? folderId = descriptor.FolderScope switch
        {
            Pd10FolderScopeRule.None => null,
            Pd10FolderScopeRule.RouteFolder => Value(routeValues, "folderId"),
            Pd10FolderScopeRule.RequestFolder => await ReadRequestFolderAsync(context.Request, context.RequestAborted).ConfigureAwait(false),
            _ => null,
        };
        if (descriptor.FolderScope != Pd10FolderScopeRule.None && string.IsNullOrWhiteSpace(folderId))
        {
            return Unusable(descriptor);
        }

        if (folderId is null)
        {
            TenantAccessOutcome outcome = (await context.RequestServices.GetRequiredService<TenantAccessAuthorizer>()
                .AuthorizeMutationAsync(
                    new TenantAccessAuthorizationContext(tenantId, principalId, tenantId),
                    context.RequestAborted)
                .ConfigureAwait(false)).Outcome;
            return outcome switch
            {
                TenantAccessOutcome.Allowed => Allowed(descriptor),
                TenantAccessOutcome.StaleProjection => Unusable(descriptor, Pd10AuthorityEvidenceState.Stale),
                TenantAccessOutcome.UnavailableProjection => Unusable(descriptor, Pd10AuthorityEvidenceState.Unavailable),
                TenantAccessOutcome.MalformedEvidence or TenantAccessOutcome.ReplayConflict =>
                    Unusable(descriptor, Pd10AuthorityEvidenceState.Conflicting),
                _ => Denied(descriptor),
            };
        }

        LayeredFolderOperationPolicy policy = descriptor.PolicyClass == FolderOperationPolicyClass.StrictRead
            ? LayeredFolderOperationPolicy.StrictRead()
            : LayeredFolderOperationPolicy.Mutation();
        LayeredFolderAuthorizationResult result = await context.RequestServices
            .GetRequiredService<LayeredFolderAuthorizationService>()
            .AuthorizeAsync(
                new LayeredFolderAuthorizationContext(
                    tenantId,
                    principalId,
                    "actor_present",
                    descriptor.ActionToken,
                    policy,
                    claim,
                    folderId,
                    Header(context, "X-Correlation-Id"),
                    Header(context, "X-Hexalith-Task-Id") ?? Value(routeValues, "taskId"),
                    ClientTenantIds(context),
                    ClientPrincipalIds(context)),
                context.RequestAborted)
            .ConfigureAwait(false);
        if (result.IsAllowed)
        {
            return Allowed(descriptor);
        }

        if (result.Decision.OutcomeCode == LayeredAuthorizationOutcomeCodes.AuthenticationDenied)
        {
            return new(false, Pd10AuthorityEvidenceState.Unavailable, false, false, false, false, RequiresBinding(descriptor));
        }

        Pd10AuthorityEvidenceState state = result.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale or LayeredAuthorizationOutcomeCodes.FolderAclStale =>
                Pd10AuthorityEvidenceState.Stale,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable =>
                Pd10AuthorityEvidenceState.Unavailable,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => Pd10AuthorityEvidenceState.Incomplete,
            _ when result.Decision.Retryable => Pd10AuthorityEvidenceState.Unavailable,
            _ => Pd10AuthorityEvidenceState.Fresh,
        };
        return state == Pd10AuthorityEvidenceState.Fresh ? Denied(descriptor) : Unusable(descriptor, state);
    }

    private static async ValueTask<Pd10TaskFolderBindingState> VerifyTaskBindingAsync(
        HttpContext context,
        IReadOnlyDictionary<string, string> routeValues,
        CancellationToken cancellationToken)
    {
        string? folderId = Value(routeValues, "folderId");
        string? taskId = Value(routeValues, "taskId");
        ITenantContextAccessor tenant = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        if (string.IsNullOrWhiteSpace(folderId) || string.IsNullOrWhiteSpace(taskId)
            || string.IsNullOrWhiteSpace(tenant.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(tenant.PrincipalId))
        {
            return Pd10TaskFolderBindingState.NotBound;
        }

        TaskStatusReadModelResult result;
        try
        {
            result = await context.RequestServices.GetRequiredService<ITaskStatusReadModel>().GetAsync(
                new TaskStatusReadModelRequest(
                    tenant.AuthoritativeTenantId,
                    taskId,
                    tenant.PrincipalId,
                    TaskStatusQueryHandler.ActionToken,
                    Header(context, "X-Correlation-Id"),
                    "eventually_consistent"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        if (result.Status == TaskStatusReadModelStatus.NotFound)
        {
            return Pd10TaskFolderBindingState.NotBound;
        }

        if (result.Status != TaskStatusReadModelStatus.Available
            || result.Snapshot is null
            || result.Freshness.Stale
            || result.Snapshot.Freshness.Stale)
        {
            return Pd10TaskFolderBindingState.Unavailable;
        }

        return result.Snapshot.ManagedTenantId == tenant.AuthoritativeTenantId
            && result.Snapshot.FolderId == folderId
            && result.Snapshot.TaskId == taskId
                ? Pd10TaskFolderBindingState.Bound
                : Pd10TaskFolderBindingState.NotBound;
    }

    private static async Task InvokeHistoricalAsync(HttpContext context, RequestDelegate next)
    {
        Stream destination = context.Response.Body;
        using MemoryStream captured = new();
        context.Response.Body = captured;
        try
        {
            await next(context).ConfigureAwait(false);
            captured.Position = 0;
            Pd10AuthorizationOutcome? canonical = context.Response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => Pd10AuthorizationOutcome.AuthenticationRequired,
                StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound => Pd10AuthorizationOutcome.SafeDenial,
                StatusCodes.Status503ServiceUnavailable when IsAuthorityUnavailable(captured) => Pd10AuthorizationOutcome.AuthorityUnavailable,
                _ => null,
            };
            captured.Position = 0;
            if (canonical is not null)
            {
                string? correlationId = ReadCorrelationId(captured);
                context.Response.Body = destination;
                await WriteProblemAsync(context, canonical, correlationId).ConfigureAwait(false);
                return;
            }

            context.Response.ContentLength = captured.Length;
            await captured.CopyToAsync(destination, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = destination;
        }
    }

    private static bool IsAuthorityUnavailable(Stream body)
    {
        body.Position = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            string? category = StringProperty(root, "category");
            string? code = StringProperty(root, "code");
            string? evidence = root.TryGetProperty("details", out JsonElement details)
                && details.ValueKind == JsonValueKind.Object ? StringProperty(details, "evidenceSource") : null;
            return evidence == "authorization_decision"
                || category is "policy_evidence_unavailable" or "authorization_evidence_unavailable"
                || code is "policy_evidence_unavailable" or "authorization_evidence_unavailable"
                    or "tenant_projection_stale" or "tenant_projection_unavailable"
                    or "folder_acl_stale" or "folder_acl_unavailable";
        }
        catch (JsonException)
        {
            return true;
        }
        finally
        {
            body.Position = 0;
        }
    }

    private static string? ReadCorrelationId(Stream body)
    {
        body.Position = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? StringProperty(document.RootElement, "correlationId") : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            body.Position = 0;
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, Pd10AuthorizationOutcome outcome, string? correlationId)
    {
        (string title, string message) = outcome.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ("Authentication required", "Authentication is required."),
            StatusCodes.Status404NotFound => ("Resource not available", "The requested resource is unavailable."),
            _ => ("Authorization evidence unavailable", "Authorization evidence is temporarily unavailable."),
        };
        context.Response.Headers.Clear();
        context.Response.StatusCode = outcome.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            type = "about:blank",
            title,
            status = outcome.StatusCode,
            category = outcome.Category,
            code = outcome.Code,
            message,
            correlationId = correlationId ?? Header(context, "X-Correlation-Id") ?? "correlation_absent",
            retryable = outcome.Retryable,
            clientAction = outcome.ClientAction,
            details = new { visibility = outcome.Visibility },
        }, cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<string?> ReadRequestFolderAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? StringProperty(document.RootElement, "folderId") : null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task RewriteRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body == Stream.Null || request.ContentType is null
            || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using StreamReader reader = new(request.Body, Encoding.UTF8, false, leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        byte[] original = Encoding.UTF8.GetBytes(json);
        try
        {
            JsonNode? root = JsonNode.Parse(json);
            RewriteVersions(root);
            byte[] rewritten = Encoding.UTF8.GetBytes(root?.ToJsonString() ?? json);
            request.Body = new MemoryStream(rewritten, writable: false);
            request.ContentLength = rewritten.Length;
        }
        catch (JsonException)
        {
            request.Body = new MemoryStream(original, writable: false);
            request.ContentLength = original.Length;
        }
    }

    private static void RewriteVersions(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string name, JsonNode? child) in obj.ToArray())
            {
                if (name == "requestSchemaVersion" && child is JsonValue value
                    && value.TryGetValue(out string? version) && version == "v2")
                {
                    obj[name] = "v1";
                }
                else
                {
                    RewriteVersions(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                RewriteVersions(child);
            }
        }
    }

    private static Pd10AuthorizationContext Allowed(Pd10ProtectedOperationDescriptor descriptor)
        => new(true, Pd10AuthorityEvidenceState.Fresh, true, true, true, true, RequiresBinding(descriptor));

    private static Pd10AuthorizationContext Denied(Pd10ProtectedOperationDescriptor descriptor)
        => new(true, Pd10AuthorityEvidenceState.Fresh, false, false, false, false, RequiresBinding(descriptor));

    private static Pd10AuthorizationContext Unusable(
        Pd10ProtectedOperationDescriptor descriptor,
        Pd10AuthorityEvidenceState state = Pd10AuthorityEvidenceState.Incomplete)
        => new(true, state, false, false, false, false, RequiresBinding(descriptor));

    private static bool RequiresBinding(Pd10ProtectedOperationDescriptor descriptor)
        => descriptor.TaskBinding != Pd10TaskBindingRule.None;

    private static bool IsCandidatePath(string path)
        => path == CandidatePrefix || path.StartsWith(CandidatePrefix + "/", StringComparison.Ordinal);

    private static string? Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) ? value : null;

    private static string? Header(HttpContext context, string name) => context.Request.Headers[name].FirstOrDefault();

    private static string? StringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static IReadOnlyDictionary<string, string?> ClientTenantIds(HttpContext context)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_tenant_id"] = context.Request.Query["tenantId"].FirstOrDefault(),
            ["query_managed_tenant_id"] = context.Request.Query["managedTenantId"].FirstOrDefault(),
            ["header_hexalith_tenant_id"] = Header(context, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = Header(context, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = Header(context, "X-Forwarded-Tenant"),
        };

    private static IReadOnlyDictionary<string, string?> ClientPrincipalIds(HttpContext context)
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = Header(context, "X-Principal-Id"),
            ["forwarded_principal_id"] = Header(context, "X-Forwarded-Principal"),
        };
}
