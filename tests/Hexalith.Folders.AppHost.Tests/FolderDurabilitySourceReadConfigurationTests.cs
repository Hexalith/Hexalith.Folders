using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Hexalith.EventStore.Aspire;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Qualifies actual realm enrollment and supported caller-list composition without starting a topology.</summary>
public sealed class FolderDurabilitySourceReadConfigurationTests
{
    /// <summary>The real issuer grants only stream-read to each own service identity and never changes a human.</summary>
    [Fact]
    public void ActualSourceRealmHasOnlyIndependentServiceCredentialsAndMetadataScopes()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Hexalith.Folders.AppHost/KeycloakRealms/hexalith-realm.json"));
        JsonObject original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        JsonObject bootstrapped = FolderDurabilityBootstrapConfiguration.Realm(original,
            "eventstore-isolated", "tenants-isolated", "folders-isolated");
        string[] callers = ["folders-isolated", "folders-workers-isolated", "folders-proof-a", "folders-proof-b"];
        JsonObject realm = FolderDurabilitySourceReadConfiguration.Realm(bootstrapped, callers);
        JsonNode.DeepEquals(bootstrapped["users"], realm["users"]).ShouldBeTrue();
        JsonArray clients = realm["clients"]!.AsArray();
        clients.ShouldNotContain(client => client!["clientId"]!.GetValue<string>() == "folders"
            || client["clientId"]!.GetValue<string>() == "folders-workers");
        for (int index = 0; index < callers.Length; index++)
        {
            JsonObject client = clients.Single(client => client!["clientId"]!.GetValue<string>() == callers[index])!.AsObject();
            client["publicClient"]!.GetValue<bool>().ShouldBeFalse();
            client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
            client["standardFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["fullScopeAllowed"]!.GetValue<bool>().ShouldBeFalse();
            client["defaultClientScopes"]!.AsArray().ShouldBeEmpty();
            client["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ShouldBe(new[]
                { "eventstore-audience.eventstore", "eventstore-operation.eventstore.stream-read" });
            client["secret"]!.GetValue<string>().ShouldBe("${HEXALITH_FOLDERS_SOURCE_" + index + "_CLIENT_SECRET}");
            client["protocolMappers"]!.AsArray().Single()!["config"]!["claim.value"]!.GetValue<string>().ShouldBe("service");
        }
        realm["clientScopes"]!.AsArray().Select(scope => scope!["name"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(realm["clientScopes"]!.AsArray().Count);
        Should.Throw<ArgumentException>(() => FolderDurabilitySourceReadConfiguration.Realm(bootstrapped, ["tenants-isolated"]));
    }

    /// <summary>The actual model keeps distinct source secrets and merges all original/bootstrap/source callers.</summary>
    [Fact]
    public async Task ActualAppHostUsesOwnSecretParametersAndPreservesExistingCallerEnrollment()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var scope = builder.ConfigureAwait(true);
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore");
        ProjectResource server = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders");
        ProjectResource worker = builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders-workers");
        Dictionary<string, object> receiver = await EnvironmentAsync(builder, gateway);
        Dictionary<string, object> serverEnvironment = await EnvironmentAsync(builder, server);
        Dictionary<string, object> workerEnvironment = await EnvironmentAsync(builder, worker);
        foreach (string caller in new[] { "folders", "folders-workers" })
        {
            receiver["Authentication__BoundStreamRead__MetadataOnlyCallers__" + caller].ShouldBe("true");
            receiver["Authentication__BoundStreamRead__AllowedDomains__" + caller].ShouldBe("folders");
        }
        serverEnvironment["Authentication__WorkloadIssuer__Workload"].ShouldBe("folders");
        workerEnvironment["Authentication__WorkloadIssuer__Workload"].ShouldBe("folders-workers");
        ReferenceEquals(serverEnvironment["Authentication__WorkloadIssuer__ClientSecret"],
            workerEnvironment["Authentication__WorkloadIssuer__ClientSecret"]).ShouldBeFalse();
        ReferenceEquals(serverEnvironment["Authentication__BoundStreamRead__PrivateKey"],
            workerEnvironment["Authentication__BoundStreamRead__PrivateKey"]).ShouldBeFalse();
        builder.Resources.OfType<ParameterResource>().Where(parameter => parameter.Name.EndsWith("-source-client-secret", StringComparison.Ordinal)
            || parameter.Name.EndsWith("-source-proof-key", StringComparison.Ordinal)).Count().ShouldBe(4);
        builder.Resources.OfType<ParameterResource>().Where(parameter => parameter.Name.EndsWith("-source-client-secret", StringComparison.Ordinal)
            || parameter.Name.EndsWith("-source-proof-key", StringComparison.Ordinal)).ShouldAllBe(parameter => parameter.Secret);

        string[] before = Callers(receiver);
        string[] actual = ["tenants-isolated", "folders-isolated", "folders-workers-isolated", "folders-proof-a", "folders-proof-b"];
        foreach (string caller in actual) builder.CreateResourceBuilder(gateway).WithEventStoreTrustedEffectSubmitter(caller);
        string[] after = Callers(await EnvironmentAsync(builder, gateway));
        after.ShouldBe(before.Concat(actual).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string[] Callers(Dictionary<string, object> environment) => environment
        .Where(setting => setting.Key.StartsWith(HexalithEventStoreTrustedEffectExtensions.AllowedCallersVariablePrefix, StringComparison.Ordinal))
        .OrderBy(setting => int.Parse(setting.Key[HexalithEventStoreTrustedEffectExtensions.AllowedCallersVariablePrefix.Length..],
            System.Globalization.CultureInfo.InvariantCulture)).Select(setting => (string)setting.Value).ToArray();

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationTestingBuilder builder, ProjectResource project)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, project, values,
                TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return values;
    }
}
