using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Pins delivery, source and authority checks independently so a receipt cannot stand in for bootstrap.</summary>
public sealed class FolderDurabilityBootstrapEvidenceTests
{
    [Fact]
    public async Task RealKeycloakModelReplacesItsRunModeImportWithoutAMountAssumption()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=true", "--KeycloakPersistent=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        KeycloakResource security = builder.Resources.OfType<KeycloakResource>().Single(resource => resource.Name == "security");
        ContainerFileSystemCallbackAnnotation original = security.Annotations.OfType<ContainerFileSystemCallbackAnnotation>()
            .Single(annotation => annotation.DestinationPath == "/opt/keycloak/data/import");
        FolderDurabilityBootstrapConfiguration.RealmImport(builder.CreateResourceBuilder(security), Path.Combine(builder.AppHostDirectory, "KeycloakRealms"));
        ContainerFileSystemCallbackAnnotation replacement = security.Annotations.OfType<ContainerFileSystemCallbackAnnotation>()
            .Single(annotation => annotation.DestinationPath == "/opt/keycloak/data/import");
        replacement.ShouldNotBeSameAs(original);
        replacement.DefaultOwner.ShouldBe(1000);
        security.Annotations.OfType<ContainerMountAnnotation>().ShouldNotContain(mount => mount.Target == "/opt/keycloak/data/import");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("attempt")]
    [InlineData("root")]
    [InlineData("principal")]
    [InlineData("phase")]
    [InlineData("response")]
    [InlineData("http")]
    [InlineData("generation")]
    [InlineData("message")]
    [InlineData("binding")]
    public void DeliveryRequiresTheActualCommittedAttemptAndAcceptedHttpResponse(string fault)
    {
        var catalog = new RouteCatalogGenerationReadback(1, fault == "generation" ? 0 : 1, new string('a', 64),
            new string('b', 64), new string('c', 64), "Development", [], true)
        { AttemptId = "actual-attempt", ActivationBindingDigest = fault == "binding" ? null : new string('e', 64) };
        byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            AttemptId = fault == "attempt" ? "another" : catalog.AttemptId,
            RootDigest = fault == "root" ? new string('d', 64) : catalog.RootDigest,
            PrincipalId = fault == "principal" ? "another" : "administrator",
            Phase = fault == "phase" ? "attempted" : fault == "response" ? "response-received" : "accepted",
            HttpStatus = fault == "http" ? 403 : 202,
            MessageId = fault == "message" ? "" : "message",
            CorrelationId = "correlation",
        });
        if (fault == "valid") FolderDurabilityBootstrapEvidence.Receipt(receipt, catalog, "administrator").GetProperty("MessageId").GetString().ShouldBe("message");
        else Should.Throw<InvalidOperationException>(() => FolderDurabilityBootstrapEvidence.Receipt(receipt, catalog, "administrator"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("principal")]
    [InlineData("actor")]
    [InlineData("correlation")]
    [InlineData("causation")]
    [InlineData("event")]
    [InlineData("truncated")]
    [InlineData("version")]
    public void OnlyTheOriginalPersistedHumanAttributedSourceEstablishesBootstrap(string fault)
    {
        var item = new StreamReadEvent(1, fault == "event" ? "UnexpectedEvent" : "Hexalith.Tenants.Contracts.Events.GlobalAdministratorSet",
            JsonSerializer.SerializeToUtf8Bytes(new { tenantId = "system", userId = "administrator", actorUserId = fault == "actor" ? "workload:tenants" : "administrator", setAt = DateTimeOffset.UtcNow }),
            "json", 1, "original-event-message", fault == "correlation" ? "foreign" : "correlation", fault == "causation" ? "correlation" : "command-message", DateTimeOffset.UtcNow,
            fault == "principal" ? "workload:tenants" : "administrator")
        { PayloadVersion = fault == "version" ? 2 : 1 };
        var page = new StreamReadPage("system", "global-administrators", "global-administrators", [item], new(0, 1, 1, 1, 1, fault == "truncated", null));
        if (fault == "valid") FolderDurabilityBootstrapEvidence.Source(page, "command-message", "correlation", "administrator");
        else Should.Throw<InvalidOperationException>(() => FolderDurabilityBootstrapEvidence.Source(page, "command-message", "correlation", "administrator"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("principal")]
    [InlineData("version")]
    [InlineData("time")]
    public void DeliveryAndSourceStillRequirePersistedAdministratorMembership(string fault)
    {
        JsonElement authority = JsonSerializer.SerializeToElement(new
        {
            administrators = new[] { fault == "principal" ? "workload:tenants" : "administrator" },
            projectionVersion = fault == "version" ? "tenant-sequence:0" : "tenant-sequence:1",
            projectedAt = fault == "time" ? default : DateTimeOffset.UtcNow,
        });
        if (fault == "valid") FolderDurabilityBootstrapEvidence.Authority(authority, "administrator");
        else Should.Throw<InvalidOperationException>(() => FolderDurabilityBootstrapEvidence.Authority(authority, "administrator"));
    }

    /// <summary>Provisions actual peer identities and equivalent canonical administrator scopes before startup.</summary>
    [Fact]
    public void IsolatedIssuerImportIsReadyBeforeEitherOwnerOrGatewayStarts()
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        JsonObject realm = FolderDurabilityBootstrapConfiguration.Realm(original, "eventstore-selected", "tenants-selected", "folders-selected");
        AssertOnlyExistingAdministratorScopeChanges(original, realm);
        JsonArray clients = realm["clients"]!.AsArray();
        clients.ShouldNotContain(client => client!["clientId"]!.GetValue<string>() == "eventstore" || client["clientId"]!.GetValue<string>() == "tenants");
        JsonObject gateway = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore-selected")!.AsObject();
        gateway["secret"]!.GetValue<string>().ShouldBe(original["clients"]!.AsArray().Single(client => client!["clientId"]!.GetValue<string>() == "eventstore")!["secret"]!.GetValue<string>());
        foreach (string peer in new[] { "folders-selected", "tenants-selected" })
        {
            gateway["optionalClientScopes"]!.AsArray().ShouldContain(scope => scope!.GetValue<string>() == "eventstore-audience." + peer);
            var audience = realm["clientScopes"]!.AsArray().Single(scope => scope!["name"]!.GetValue<string>() == "eventstore-audience." + peer)!;
            audience["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe(peer);
        }
        JsonObject owner = clients.Single(client => client!["clientId"]!.GetValue<string>() == "tenants-selected")!.AsObject();
        owner["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ShouldBe(new[]
            { "eventstore-audience.eventstore", "eventstore-operation.eventstore.command-submit", "eventstore-operation.eventstore.stream-read" });
        owner["protocolMappers"]!.AsArray().Single(mapper => mapper!["name"]!.GetValue<string>() == "authenticated-surface")!["config"]!["claim.value"]!.GetValue<string>().ShouldBe("service");
    }

    [Fact]
    public void ImportedAdministratorHasExactlyTheCanonicalPermissionsNeededByItsExistingWorkflow()
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        string before = original.ToJsonString();
        JsonObject realm = FolderDurabilityBootstrapConfiguration.Realm(original);
        string[] required =
        [
            TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == BootstrapGlobalAdmin.CommandType).Route.PlatformAdmission!.RequiredHumanPermission,
            TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == CreateTenant.CommandType).Route.PlatformAdmission!.RequiredHumanPermission,
            TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == AddUserToTenant.CommandType).Route.PlatformAdmission!.RequiredHumanPermission,
            TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == UpdateTenant.CommandType).Route.PlatformAdmission!.RequiredHumanPermission,
            TenantCatalogDeclarations.Queries.Single(query => query.MessageType == "get-global-administrators").PlatformAdmission!.RequiredHumanPermission,
            TenantCatalogDeclarations.Queries.Single(query => query.MessageType == "list-tenants").PlatformAdmission!.RequiredHumanPermission,
        ];
        required.ShouldBe(new[] { "global-administrators:bootstrap", "tenants:create", "tenants:add-user", "tenants:update", "global-administrators:read", "tenants:read" });
        TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == BootstrapGlobalAdmin.CommandType)
            .Route.PlatformAdmission!.Namespace.ShouldBe("platform:global-administrators");
        foreach (string operation in new[] { CreateTenant.CommandType, AddUserToTenant.CommandType, UpdateTenant.CommandType })
            TenantCatalogDeclarations.Commands.Single(command => command.Route.MessageType == operation)
                .Route.PlatformAdmission!.Namespace.ShouldBe("platform:tenants");
        JsonObject administrator = realm["users"]!.AsArray().Single(user => user!["id"]!.GetValue<string>() == "admin-user")!.AsObject();
        foreach (string permission in required)
            administrator["attributes"]!["permissions"]!.AsArray().Count(item => item!.GetValue<string>() == permission).ShouldBe(1);
        administrator["username"]!.GetValue<string>().ShouldBe("admin-user");
        administrator["attributes"]!["global_admin"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("true");
        string[] grants = administrator["attributes"]!["tenants"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
        grants.ShouldBe(new[] { "tenant-a", "tenant-b" });
        TenantCanonicalizer.TryCanonicalizeGrants(grants, out _).ShouldBeTrue();
        foreach (JsonNode? user in realm["users"]!.AsArray().Where(user => user!["id"]!.GetValue<string>() != "admin-user"))
        {
            user!["attributes"]!["permissions"]!.AsArray().ShouldNotContain(permission => required.Contains(permission!.GetValue<string>(), StringComparer.Ordinal));
            user.ToJsonString().ShouldBe(original["users"]!.AsArray().Single(candidate => candidate!["id"]!.GetValue<string>() == user["id"]!.GetValue<string>())!.ToJsonString());
        }
        original.ToJsonString().ShouldBe(before);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("not-admin")]
    public void ImportCannotGrantBootstrapToAnotherOrNondesignatedPrincipal(string fault)
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        JsonObject administrator = original["users"]!.AsArray().Single(user => user!["id"]!.GetValue<string>() == "admin-user")!.AsObject();
        if (fault == "subject") administrator["id"] = "another-principal";
        else administrator["attributes"]!["global_admin"] = new JsonArray("false");
        string before = original.ToJsonString();
        Should.Throw<InvalidOperationException>(() => FolderDurabilityBootstrapConfiguration.Realm(original));
        original.ToJsonString().ShouldBe(before);
    }

    [Fact]
    public void ActualIssuerProfilesPreserveHumansAndKeepTheOwnerServiceAccountSeparate()
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        string before = original.ToJsonString();
        JsonObject realm = FolderDurabilityBootstrapConfiguration.Realm(original);
        original.ToJsonString().ShouldBe(before);
        AssertOnlyExistingAdministratorScopeChanges(original, realm);
        JsonObject owner = realm["clients"]!.AsArray().Single(client => client!["clientId"]!.GetValue<string>() == "tenants")!.AsObject();
        owner["publicClient"]!.GetValue<bool>().ShouldBeFalse(); owner["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        owner["defaultClientScopes"]!.AsArray().ShouldBeEmpty();
        owner["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ShouldBe(new[]
            { "eventstore-audience.eventstore", "eventstore-operation.eventstore.command-submit", "eventstore-operation.eventstore.stream-read" });
        foreach (var client in realm["clients"]!.AsArray())
        {
            string id = client!["clientId"]!.GetValue<string>();
            JsonObject mapper = client["protocolMappers"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "authenticated-surface")!.AsObject();
            mapper["config"]!["claim.value"]!.GetValue<string>().ShouldBe(id is "eventstore" or "tenants" or "folders" or "folders-workers" ? "service" : "ui");
            if (id == "tenants") mapper.ToJsonString().ShouldNotContain("global_admin");
        }
    }

    private static void AssertOnlyExistingAdministratorScopeChanges(JsonObject original, JsonObject realm)
    {
        JsonArray expected = original["users"]!.DeepClone().AsArray();
        JsonObject administrator = expected.Single(user => user!["id"]!.GetValue<string>() == "admin-user")!.AsObject();
        JsonArray permissions = administrator["attributes"]!["permissions"]!.AsArray();
        foreach (string permission in new[] { "global-administrators:bootstrap", "tenants:create", "tenants:add-user", "tenants:update", "global-administrators:read", "tenants:read" })
            permissions.Add(permission);
        JsonArray grants = administrator["attributes"]!["tenants"]!.AsArray();
        grants.Remove(grants.Single(grant => grant!.GetValue<string>() == "system"));
        realm["users"]!.ToJsonString().ShouldBe(expected.ToJsonString());
    }
}
