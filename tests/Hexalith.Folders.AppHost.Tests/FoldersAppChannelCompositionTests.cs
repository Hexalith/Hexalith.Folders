using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Inspects the real AppHost model without starting its external resources.</summary>
public sealed class FoldersAppChannelCompositionTests
{
    /// <summary>Requires Tenants and its own sidecar to use the same private per-run SDK token parameter.</summary>
    [Fact]
    public async Task TenantsAndItsOwnSidecarShareOneSecretAppChannelParameter()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_Folders_AppHost>(["--EnableKeycloak=false"], TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable builderScope = builder.ConfigureAwait(true);
        ProjectResource tenants = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "tenants");
        IDaprSidecarResource sidecar = tenants.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        object projectToken = await TokenParameterAsync(tenants).ConfigureAwait(true);
        object sidecarToken = await TokenParameterAsync(sidecar).ConfigureAwait(true);
        ParameterResource token = projectToken.ShouldBeOfType<ParameterResource>();
        token.Secret.ShouldBeTrue();
        sidecarToken.ShouldBeSameAs(token);
        builder.Resources.OfType<ParameterResource>().Count(resource => resource.Name == "tenants-app-api-token").ShouldBe(1);
    }

    private static async Task<object> TokenParameterAsync(IResource resource)
    {
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            resource, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context).ConfigureAwait(true);
        context.EnvironmentVariables.ShouldContainKey(HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable);
        return context.EnvironmentVariables[HexalithEventStoreAppChannelExtensions.AppChannelTokenVariable];
    }
}
