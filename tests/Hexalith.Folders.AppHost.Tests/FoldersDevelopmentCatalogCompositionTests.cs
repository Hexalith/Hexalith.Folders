using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Checks the real compiled owner and final-sidecar composition without starting any backend or child process.</summary>
public sealed class FoldersDevelopmentCatalogCompositionTests
{
    [Fact]
    public async Task SelectedOwnerCatalogBindsFinalAppIdsAndKeepsAllIntentPolicies()
    {
        string temporary = Directory.CreateTempSubdirectory("folders-catalog-model-").FullName;
        string? catalogDirectory = null;
        TestContext.Current.TestOutputHelper!.WriteLine("CATALOG_MODEL_OWNED " + JsonSerializer.Serialize(new { temporary }));
        try
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
                ["--EnableKeycloak=false"], TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var builderScope = builder.ConfigureAwait(true);
            foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
            foreach (DaprSidecarAnnotation sidecar in project.Annotations.OfType<DaprSidecarAnnotation>())
            {
                var annotation = sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecar.Sidecar.Annotations.Remove(annotation);
                sidecar.Sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(annotation.Options with { AppId = project.Name + "-selected" }));
            }
            string source = builder.Resources.OfType<DaprComponentResource>().Single(component => component.Name == "statestore").Options!.LocalPath!;
            string isolated = Path.Combine(temporary, "model-only-state.yaml");
            await File.WriteAllTextAsync(isolated, (await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken).ConfigureAwait(true))
                .Replace("localhost:6379", "127.0.0.1:1", StringComparison.Ordinal), TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var catalog = Hexalith.Folders.AppHost.FoldersDevelopmentCatalog.Compose(builder,
                new(Path.Combine(temporary, "custody"), "folders-routing-authority", "folders-routing-authority-v1", "model1",
                    new Dictionary<string, string> { ["model1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "model1"), isolated);
            catalogDirectory = catalog.Directory;
            TestContext.Current.TestOutputHelper!.WriteLine("CATALOG_MODEL_OWNED " + JsonSerializer.Serialize(new { temporary, catalogDirectory }));
            var envelope = RouteCatalogCodec.Decode(await File.ReadAllBytesAsync(Path.Combine(catalog.Directory, "catalog.json"), TestContext.Current.CancellationToken).ConfigureAwait(true), catalog.RootDigest);
            envelope.SchemaVersion.ShouldBe(3);
            envelope.Idempotency.Count(entry => entry.Domain == "folders").ShouldBe(18);
            envelope.Idempotency.Count(entry => entry.Domain != "folders").ShouldBe(12);
            envelope.Routes.Where(route => route.RouteId.StartsWith("folders-", StringComparison.Ordinal)).ShouldAllBe(route => route.AppId == "folders-selected");
            envelope.Idempotency.ShouldAllBe(entry => entry.ConsumerAppId == "eventstore-selected");
            envelope.Idempotency.Single(entry => entry.OperationId == "commit-workspace").RetentionTier.ShouldBe("commit");
            var folderOperations = envelope.Operations!.Where(operation => operation.RouteId.StartsWith("folders-", StringComparison.Ordinal)).ToArray();
            folderOperations.Where(operation => operation.Kind == "command").Count().ShouldBe(18);
            folderOperations.Where(operation => operation.Kind == "command")
                .ShouldAllBe(operation => operation.Ingress == "public" && operation.Eligibility == "agent-eligible");
            folderOperations.Where(operation => operation.Kind != "command").Count().ShouldBe(5);
            folderOperations.Where(operation => operation.Kind != "command").ShouldAllBe(operation => operation.Ingress == "internal");
            folderOperations.ShouldAllBe(operation => operation.ContractVersion == "v1");
            using JsonDocument acl = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(catalog.Directory, "folders.configuration.json"), TestContext.Current.CancellationToken).ConfigureAwait(true));
            acl.RootElement.GetProperty("spec").GetProperty("accessControl").GetProperty("defaultAction").GetString().ShouldBe("deny");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
            TestContext.Current.TestOutputHelper!.WriteLine("CATALOG_MODEL_CLEANUP " + JsonSerializer.Serialize(new
            {
                temporary, temporaryExists = Directory.Exists(temporary), catalogDirectory,
                catalogDirectoryExists = catalogDirectory is not null && Directory.Exists(catalogDirectory),
            }));
        }
    }
}
