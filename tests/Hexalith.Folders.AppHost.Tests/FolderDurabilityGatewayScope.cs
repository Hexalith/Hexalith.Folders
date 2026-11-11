using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Uses explicit public scopes while leaving retained storage identities to their owning SDK adapter.</summary>
internal static class FolderDurabilityGatewayScope
{
    /// <summary>Binds one exact supported scope without accepting the reserved storage tenant.</summary>
    internal static void Apply(HttpRequestMessage request, string scope)
        => ApplyCore(request, scope, includeTenantHeader: true);

    /// <summary>Preserves body/bearer tenant binding for human commands and explicit platform namespaces.</summary>
    internal static void ApplyHumanCommand(HttpRequestMessage request, string scope)
        => ApplyCore(request, scope, includeTenantHeader: false);

    private static void ApplyCore(HttpRequestMessage request, string scope, bool includeTenantHeader)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers.Contains("X-Hexalith-Scope") || request.Headers.Contains("X-Hexalith-Tenant"))
            throw new InvalidOperationException("A durability request already has an explicit scope.");
        if (scope is TenantPlatformScopes.Tenants or TenantPlatformScopes.GlobalAdministrators)
            request.Headers.Add("X-Hexalith-Scope", scope);
        else if (TenantCanonicalizer.TryCanonicalize(scope, out string canonical) && scope == canonical)
        {
            if (includeTenantHeader) request.Headers.Add("X-Hexalith-Tenant", canonical);
        }
        else throw new InvalidOperationException("A durability request requires its exact canonical public scope.");
    }

    /// <summary>Constructs the same internal bootstrap operation under its current public namespace.</summary>
    internal static SubmitCommandRequest BootstrapCommand(string messageId, string principal)
        => new(messageId, TenantPlatformScopes.GlobalAdministrators, TenantIdentity.GlobalAdministratorsDomain,
            TenantIdentity.GlobalAdministratorsAggregateId, nameof(BootstrapGlobalAdmin), JsonSerializer.SerializeToElement(new { userId = principal }));

    /// <summary>Reads the original owner source using the configured scope key and real isolated owner sidecar.</summary>
    internal static HttpRequestMessage BootstrapSource(DaprClient sidecar, string gatewayAppId, string callerAppId,
        string assertion, string privateKey, DateTimeOffset now)
    {
        var source = new StreamReadRequest(TenantPlatformScopes.GlobalAdministrators, TenantIdentity.GlobalAdministratorsDomain,
            TenantIdentity.GlobalAdministratorsAggregateId)
        { RequireNativeProof = true };
        HttpRequestMessage request = sidecar.CreateInvokeMethodRequest(HttpMethod.Post, gatewayAppId, "api/v1/streams/read/workload");
        try
        {
            request.Content = JsonContent.Create(source);
            Apply(request, source.Tenant);
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            request.Headers.Add(BoundStreamReadProof.HeaderName, BoundStreamReadProof.Issue(source, callerAppId, privateKey, now));
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }
}
