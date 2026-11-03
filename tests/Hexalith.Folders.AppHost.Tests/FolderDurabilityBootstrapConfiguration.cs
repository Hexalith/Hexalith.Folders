using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Configures the real disposable issuer's existing human and bound owner workload profiles.</summary>
internal static class FolderDurabilityBootstrapConfiguration
{
    /// <summary>Selects the SDK's durable polling mode only for the isolated gateway's bootstrap domain.</summary>
    internal static IResourceBuilder<ProjectResource> ProjectionPolling(IResourceBuilder<ProjectResource> gateway)
        => gateway.WithEnvironment("EventStore__Projections__Domains__global-administrators__RefreshIntervalMs", "1000");

    /// <summary>Replaces the real run-mode file import through the current public Keycloak API.</summary>
    internal static void RealmImport(IResourceBuilder<KeycloakResource> security, string directory)
    {
        ContainerFileSystemCallbackAnnotation original = security.Resource.Annotations
            .OfType<ContainerFileSystemCallbackAnnotation>().Single(annotation => annotation.DestinationPath == "/opt/keycloak/data/import");
        security.Resource.Annotations.Remove(original);
        security.WithRealmImport(directory);
    }

    /// <summary>Aligns the disposable designated administrator and owner workload with their existing operations.</summary>
    internal static JsonObject Realm(JsonObject original, string eventStoreAppId = "eventstore", string tenantsAppId = "tenants", string foldersAppId = "folders")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventStoreAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantsAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(foldersAppId);
        JsonObject realm = original.DeepClone().AsObject();
        JsonObject administrator = realm["users"]!.AsArray().Single(user => user!["id"]!.GetValue<string>() == "admin-user"
            && user["username"]!.GetValue<string>() == "admin-user")!.AsObject();
        if (!administrator["attributes"]!["global_admin"]!.AsArray().Any(value => value!.GetValue<string>() == "true"))
            throw new InvalidOperationException("The disposable bootstrap principal is not the designated administrator.");
        JsonArray permissions = administrator["attributes"]!["permissions"]!.AsArray();
        foreach (string permission in new[] { "global-administrators:bootstrap", "tenants:create", "tenants:add-user", "global-administrators:read", "tenants:read" })
            if (!permissions.Any(value => value!.GetValue<string>() == permission)) permissions.Add(permission);
        JsonArray tenantGrants = administrator["attributes"]!["tenants"]!.AsArray();
        for (int index = tenantGrants.Count - 1; index >= 0; index--)
            if (tenantGrants[index]!.GetValue<string>() == "system") tenantGrants.RemoveAt(index);
        JsonArray clients = realm["clients"]!.AsArray();
        JsonArray scopes = realm["clientScopes"]!.AsArray();
        if (!scopes.Any(node => node!["name"]!.GetValue<string>() == "eventstore-audience.eventstore"))
        {
            JsonObject audience = scopes.Single(node => node!["name"]!.GetValue<string>() == "eventstore-audience.tenants")!.DeepClone().AsObject();
            audience["name"] = "eventstore-audience.eventstore";
            audience["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"] = "eventstore";
            scopes.Add(audience);
        }
        foreach (string operation in new[] { "eventstore:command-submit", "eventstore:stream-read" })
        {
            if (scopes.Any(node => node!["name"]!.GetValue<string>() == "eventstore-operation." + operation.Replace(':', '.'))) continue;
            JsonObject scope = scopes.Single(node => node!["name"]!.GetValue<string>() == "eventstore-operation.domain-service.process")!.DeepClone().AsObject();
            scope["name"] = "eventstore-operation." + operation.Replace(':', '.');
            scope["protocolMappers"]!.AsArray().Single()!["config"]!["claim.value"] = operation;
            scopes.Add(scope);
        }

        JsonObject tenant = clients.Single(node => node!["clientId"]!.GetValue<string>() == "eventstore")!.DeepClone().AsObject();
        tenant["clientId"] = tenantsAppId;
        tenant["secret"] = "${HEXALITH_TENANTS_WORKLOAD_CLIENT_SECRET}";
        tenant["name"] = "Tenants owner bootstrap and source authority";
        tenant["optionalClientScopes"] = new JsonArray("eventstore-audience.eventstore",
            "eventstore-operation.eventstore.command-submit", "eventstore-operation.eventstore.stream-read");
        clients.Add(tenant);
        JsonObject gateway = clients.Single(node => node!["clientId"]!.GetValue<string>() == "eventstore")!.AsObject();
        gateway["clientId"] = eventStoreAppId;
        foreach ((string canonical, string physical) in new[] { ("folders", foldersAppId), ("tenants", tenantsAppId) })
        {
            if (canonical == physical) continue;
            JsonObject selected = scopes.Single(node => node!["name"]!.GetValue<string>() == "eventstore-audience." + canonical)!.DeepClone().AsObject();
            selected["name"] = "eventstore-audience." + physical;
            selected["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"] = physical;
            scopes.Add(selected);
            gateway["optionalClientScopes"]!.AsArray().Add("eventstore-audience." + physical);
        }
        foreach (JsonNode? node in clients)
        {
            JsonObject client = node!.AsObject();
            string id = client["clientId"]!.GetValue<string>();
            bool service = id == eventStoreAppId || id == tenantsAppId;
            if (id is not ("hexalith-eventstore" or "hexalith-folders") && !service) continue;
            JsonArray mappers = client["protocolMappers"]?.AsArray() ?? [];
            if (client["protocolMappers"] is null) client["protocolMappers"] = mappers;
            mappers.Add(new JsonObject
            {
                ["name"] = "authenticated-surface",
                ["protocol"] = "openid-connect",
                ["protocolMapper"] = "oidc-hardcoded-claim-mapper",
                ["consentRequired"] = false,
                ["config"] = new JsonObject
                {
                    ["claim.name"] = "eventstore:surface",
                    ["claim.value"] = service ? "service" : "ui",
                    ["jsonType.label"] = "String",
                    ["access.token.claim"] = "true",
                    ["id.token.claim"] = "false",
                    ["userinfo.token.claim"] = "false",
                },
            });
        }

        return realm;
    }
}
