using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Server.Content;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>Connects the owner operation and its receipt lookup to the authenticated host.</summary>
internal static class FolderProvisioningEndpoints
{
    internal const string Scheme = "FoldersProvisioningLookup";
    internal const string WorkloadSection = "Authentication:FoldersProvisioningLookup";

    internal static void AddFoldersProvisioning(this IServiceCollection services)
    {
        services.AddAuthentication().AddEventStoreWorkloadScheme(Scheme, WorkloadSection);
        services.AddEventStoreWorkloadPolicies(Scheme, [FolderProvisioningContract.LookupOperation]);
        services.AddHttpClient(nameof(DurableTaskStepLookupClient), client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddEventStoreGatewayAuthoritativeEventStreamReader(FoldersServerModule.DomainName);
        services.AddHttpClient(nameof(FolderProvisioningStreamPresence), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.MaxResponseContentBufferSize = 16_384;
        });
        services.AddScoped<DurableTaskStepLookupClient>();
        services.AddScoped<FolderProvisioningStreamPresence>();
        services.AddScoped<FolderProvisioningService>();
    }

    internal static void MapFoldersProvisioning(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(FolderProvisioningContract.LookupPath, LookupAsync)
            .RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.OperationPolicy(Scheme, FolderProvisioningContract.LookupOperation));
    }

    private static async Task<IResult> LookupAsync(HttpContext context, FolderProvisioningService provisioning,
        IConfiguration configuration, CancellationToken token)
    {
        string? worker = configuration[FolderProvisioningService.Section + "WorkerWorkload"];
        ClaimsPrincipal caller = context.User;
        if (string.IsNullOrWhiteSpace(worker)
            || !caller.HasClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, worker)
            || !caller.HasClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, FoldersServerModule.DomainName)
            || caller.FindFirstValue(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType) is not { } tenant)
        {
            return Results.NotFound();
        }

        FolderProvisioningRequest? request;
        try
        {
            byte[] body = await FoldersDomainServiceEndpoints.ReadBoundedContentAsync(context.Request.Body, 4096, token)
                .ConfigureAwait(false);
            request = JsonSerializer.Deserialize<FolderProvisioningRequest>(body, FolderProvisioningService.WireJson);
        }
        catch (Exception exception) when (exception is JsonException or WorkspaceContentPolicyException or IOException)
        {
            return Results.BadRequest();
        }

        if (request is null)
        {
            return Results.BadRequest();
        }

        context.Response.Headers.CacheControl = "no-store";
        return await provisioning.LookupAsync(tenant, request, token).ConfigureAwait(false);
    }
}
