using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;
using Hexalith.Folders.Aspire;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Inspects the real AppHost model without starting its external resources.</summary>
public sealed class FoldersAppChannelCompositionTests
{
    /// <summary>Keeps the default sidecar port while allowing an isolated live lane to avoid an occupied listener.</summary>
    [Theory]
    [InlineData(null, 3501)]
    [InlineData("35991", 35991)]
    public async Task EventStoreSidecarUsesTheSelectedHttpPort(string? selectedPort, int expectedPort)
    {
        string[] args = selectedPort is null
            ? ["--EnableKeycloak=false"]
            : ["--EnableKeycloak=false", "--Folders:EventStoreDaprHttpPort=" + selectedPort];
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(args, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        ProjectResource eventStore = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        IDaprSidecarResource sidecar = eventStore.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.DaprHttpPort.ShouldBe(expectedPort);
    }

    [Fact]
    public async Task EachProjectSharesItsOwnSecretAppChannelParameterWithItsSidecar()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        const string tokenVariable = HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable;
        HashSet<ParameterResource> tokens = new(ReferenceEqualityComparer.Instance);
        foreach (string appId in new[] { FoldersAspireModule.TenantsAppId, FoldersAspireModule.FoldersAppId, FoldersAspireModule.FoldersWorkersAppId })
        {
            ProjectResource project = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == appId);
            IDaprSidecarResource sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            Dictionary<string, object> projectEnvironment = await EnvironmentAsync(project).ConfigureAwait(true);
            Dictionary<string, object> sidecarEnvironment = await EnvironmentAsync(sidecar).ConfigureAwait(true);
            ParameterResource token = SecretParameter(projectEnvironment, tokenVariable);

            sidecarEnvironment[tokenVariable].ShouldBeSameAs(token);
            tokens.Add(token).ShouldBeTrue();
            builder.Resources.OfType<ParameterResource>().Single(resource => resource.Name == $"{appId}-app-api-token")
                .ShouldBeSameAs(token);
        }
    }

    [Fact]
    public async Task ReceiptAdmissionAndWorkloadSecretsReachTheirExpectedResources()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=true", "--KeycloakPersistent=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using var builderScope = builder.ConfigureAwait(true);
        Dictionary<string, object> eventStore = await EnvironmentAsync(builder.Resources.Single(resource => resource.Name == FoldersAspireModule.EventStoreAppId))
            .ConfigureAwait(true);
        Dictionary<string, object> folders = await EnvironmentAsync(builder.Resources.Single(resource => resource.Name == FoldersAspireModule.FoldersAppId))
            .ConfigureAwait(true);
        Dictionary<string, object> keycloak = await EnvironmentAsync(builder.Resources.OfType<KeycloakResource>().Single())
            .ConfigureAwait(true);

        const string receiptVariable = "Authentication__HumanAuthorityReceipt__SigningKey";
        ParameterResource receipt = SecretParameter(eventStore, receiptVariable);
        folders[receiptVariable].ShouldBeSameAs(receipt);

        const string admission = "EventStore__IdempotencyAdmission__";
        eventStore[admission + "Enabled"].ShouldBe("true");
        eventStore[admission + "DigestKeySource"].ShouldBe("Configuration");
        eventStore[admission + "ActiveDigestKeyVersion"].ShouldBe("local1");
        ParameterResource digestKey = SecretParameter(eventStore, admission + "DigestKeys__local1");
        digestKey.ShouldNotBeSameAs(receipt);

        eventStore[HexalithEventStoreTrustedEffectExtensions.WorkloadClientIdVariable].ShouldBe("eventstore");
        ParameterResource workloadSecret = SecretParameter(eventStore, HexalithEventStoreTrustedEffectExtensions.WorkloadClientSecretVariable);
        keycloak["HEXALITH_EVENTSTORE_WORKLOAD_CLIENT_SECRET"].ShouldBeSameAs(workloadSecret);
        workloadSecret.ShouldNotBeSameAs(receipt);
        workloadSecret.ShouldNotBeSameAs(digestKey);
    }

    private static ParameterResource SecretParameter(Dictionary<string, object> environment, string variable)
    {
        environment.ShouldContainKey(variable);
        ParameterResource parameter = environment[variable].ShouldBeOfType<ParameterResource>();
        parameter.Secret.ShouldBeTrue();
        return parameter;
    }

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IResource resource)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource, values, TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context).ConfigureAwait(true);
        return values;
    }
}
