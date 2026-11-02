using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Exercises the canonical Platform helper through actual generated-sidecar model callbacks, without launching infrastructure.</summary>
public sealed class FolderDurabilityMtlsCompositionTests
{
    /// <summary>Checks both preserved CLR/default behavior and selected namespaces at the final native runtime boundary.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("folders-durability-model", false)]
    [InlineData("folders-durability-model", true)]
    public async Task GeneratedNativeModelRetainsActualNamespaceAndPrivateControlPlane(string? selectedNamespace, bool missingRuntime)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string runtimeHome = Directory.CreateTempSubdirectory("folders-mtls-model-").FullName;
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [],
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
            AssemblyName = typeof(FolderDurabilityMtlsCompositionTests).Assembly.GetName().Name,
        });
        builder.AddDapr(options => options.EnableTelemetry = false);
        var application = builder.AddProject<Projects.Hexalith_Folders_Server>("identity-probe")
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "identity-probe-selected", AppProtocol = "http", RuntimePath = runtimeHome }));
        string sentry = Path.Combine(root, "references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/sentry.yaml");
        var owned = selectedNamespace is null
            ? DaprSelfHostedMtls.AddLocalApplicationInfrastructure(builder, root, sentry)
            : DaprSelfHostedMtls.AddLocalApplicationInfrastructure(builder, root, sentry, selectedNamespace);
        TestContext.Current.TestOutputHelper!.WriteLine("MTLS_MODEL_BINDING " + JsonSerializer.Serialize(new
        {
            runDirectory = owned.RunDirectory, certificateDirectory = owned.CertificateDirectory, runtimeHome,
            applicationNamespace = selectedNamespace ?? "default", missingRuntime,
        }));
        try
        {
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(runtimeHome, "model-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(runtimeHome, "model-dashboard-not-started");
            string runtime = Path.Combine(runtimeHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
            Directory.CreateDirectory(Path.GetDirectoryName(runtime)!);
            if (!missingRuntime) await File.WriteAllTextAsync(runtime, "model-only runtime; never launched", TestContext.Current.CancellationToken);
            foreach (string credential in new[] { "ca.crt", "issuer.crt", "issuer.key" })
                await File.WriteAllTextAsync(Path.Combine(owned.CertificateDirectory, credential), "model-only-" + credential, TestContext.Current.CancellationToken);
            await using DistributedApplication app = builder.Build();
            DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
            IDistributedApplicationEventing events = app.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (IDistributedApplicationEventingSubscriber subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
                await subscriber.SubscribeAsync(events, builder.ExecutionContext, TestContext.Current.CancellationToken);
            if (missingRuntime)
            {
                await Should.ThrowAsync<InvalidDataException>(() => events.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken));
                return;
            }
            await events.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken);
            foreach (IResource resource in model.Resources)
                foreach (EndpointAnnotation endpoint in resource.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", endpoint.Port ?? endpoint.TargetPort ?? 32123);
            ExecutableResource actual = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "identity-probe-dapr-cli");
            actual.Annotations.OfType<ExecutableAnnotation>().Last().Command.ShouldBe(runtime);
            List<object> arguments = [];
            foreach (CommandLineArgsCallbackAnnotation callback in actual.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
            arguments.ShouldContain("--enable-mtls");
            arguments.ShouldNotContain("run");
            int port = model.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
            arguments[arguments.IndexOf("--sentry-address") + 1].ShouldBe("localhost:" + port);
            var environment = new EnvironmentCallbackContext(builder.ExecutionContext, actual, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
            foreach (EnvironmentCallbackAnnotation callback in actual.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(environment);
            environment.EnvironmentVariables["NAMESPACE"].ShouldBe(selectedNamespace ?? "default");
            environment.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"].ShouldBe("default");
            environment.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"].ShouldBe("localhost");
            environment.EnvironmentVariables["DAPR_TRUST_ANCHORS"].ShouldBe("model-only-ca.crt");
            environment.EnvironmentVariables["DAPR_CERT_CHAIN"].ShouldBe("model-only-issuer.crt");
            environment.EnvironmentVariables["DAPR_CERT_KEY"].ShouldBe("model-only-issuer.key");
            foreach (string name in new[] { "dapr-sentry", "dapr-placement-mtls", "dapr-scheduler-mtls" })
                model.Resources.ShouldContain(resource => resource.Name == name);
            actual.Annotations.OfType<EndpointAnnotation>().ShouldAllBe(endpoint => !endpoint.IsProxied);
        }
        finally
        {
            (await CompositionWorkspace.TryDeleteAsync(owned.CertificateDirectory, CancellationToken.None)).ShouldBeTrue();
            (await CompositionWorkspace.TryDeleteAsync(owned.RunDirectory, CancellationToken.None)).ShouldBeTrue();
            (await CompositionWorkspace.TryDeleteAsync(runtimeHome, CancellationToken.None)).ShouldBeTrue();
            Directory.Exists(owned.CertificateDirectory).ShouldBeFalse();
            Directory.Exists(owned.RunDirectory).ShouldBeFalse();
            TestContext.Current.TestOutputHelper!.WriteLine("MTLS_MODEL_CLEANUP " + JsonSerializer.Serialize(new
            {
                runDirectory = owned.RunDirectory, certificateDirectory = owned.CertificateDirectory, runtimeHome,
                runDirectoryExists = Directory.Exists(owned.RunDirectory), certificateDirectoryExists = Directory.Exists(owned.CertificateDirectory),
                runtimeHomeExists = Directory.Exists(runtimeHome),
            }));
        }
    }

    /// <summary>Invalid selected namespaces fail before any identity infrastructure is created.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingNamespaceNeverFallsBackToTheCanonicalNamespace(string invalid)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        { Args = [], DisableDashboard = true, AssemblyName = typeof(FolderDurabilityMtlsCompositionTests).Assembly.GetName().Name });
        Should.Throw<ArgumentException>(() => DaprSelfHostedMtls.AddLocalApplicationInfrastructure(builder, Directory.GetCurrentDirectory(), "unneeded.yaml", invalid));
        builder.Resources.ShouldNotContain(resource => resource.Name == "dapr-sentry");
    }
}
