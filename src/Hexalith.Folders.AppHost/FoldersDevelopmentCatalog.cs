using System.Reflection;
using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;

namespace Hexalith.Folders.AppHost;

/// <summary>Enrolls the final selected runtime cohort through the SDK's independently observed Development catalog protocol.</summary>
public static class FoldersDevelopmentCatalog
{
    /// <summary>Binds existing compiled owners and actual sidecars after the owning topology has selected its identities and backend.</summary>
    public static DevelopmentRouteCatalogComposition Compose(IDistributedApplicationBuilder builder,
        DevelopmentRouteCatalogCompositionOptions options, string stateStorePath, string daprNamespace = "default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        IResourceBuilder<ProjectResource> Project(string name) => builder.CreateResourceBuilder(
            builder.Resources.OfType<ProjectResource>().Single(project => project.Name == name));
        var gateway = Project("eventstore");
        var tenants = Project("tenants");
        var folders = Project("folders");
        var memories = Project("memories");
        Assembly folderOwner = Load(folders.Resource, "Hexalith.Folders.Server.dll");
        Assembly folderAdapters = Load(gateway.Resource, "Hexalith.Folders.EventStore.dll");
        Assembly tenantOwner = Load(tenants.Resource, "Hexalith.Tenants.Contracts.dll");
        Assembly tenantAdapters = Load(tenants.Resource, "Hexalith.Tenants.Server.dll");
        var owners = new List<DevelopmentRouteCatalogOwner>
        {
            new(folders, folderOwner, folderAdapters), new(tenants, tenantOwner, tenantAdapters),
        };
        var folderHosts = builder.Resources.OfType<ProjectResource>().Where(project => project.Name is "folders" or "folders-replica")
            .Select(builder.CreateResourceBuilder).ToArray();
        owners.AddRange(folderHosts.Where(project => project.Resource.Name != "folders")
            .Select(project => new DevelopmentRouteCatalogOwner(project, folderOwner, folderAdapters)));
        string[] domainMethods = ["process", "query", "project", "admin/operational-index-metadata"];
        DevelopmentRouteCatalogInvocation[] DomainGrants() => domainMethods.Select(method =>
            new DevelopmentRouteCatalogInvocation(gateway, method, ["POST"])).ToArray();
        var workers = builder.Resources.OfType<ProjectResource>().Where(project => project.Name.StartsWith("folders-workers", StringComparison.Ordinal))
            .Select(builder.CreateResourceBuilder).ToArray();
        var callers = new[] { tenants, memories }.Concat(folderHosts).Concat(workers)
            .DistinctBy(project => project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar
                .Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId, StringComparer.Ordinal).ToArray();
        var incomingGateway = callers.SelectMany(caller => new[]
        {
            new DevelopmentRouteCatalogInvocation(caller, "api/v1/**", ["GET", "POST"]),
            new DevelopmentRouteCatalogInvocation(caller, "internal/**", ["GET", "POST"]),
        }).ToArray();
        DevelopmentRouteCatalogReceiver[] receivers =
        [
            new(gateway, incomingGateway) { TechnicalHttp = true },
            new(tenants, DomainGrants()), new(memories, []),
            .. folderHosts.Select(project => new DevelopmentRouteCatalogReceiver(project, DomainGrants())),
            .. workers.Select(project => new DevelopmentRouteCatalogReceiver(project, [])),
        ];
        return builder.AddEventStoreDevelopmentRouteCatalog(gateway, owners, receivers, stateStorePath, options, daprNamespace);
    }

    private static Assembly Load(ProjectResource project, string artifact)
    {
        string directory = Path.GetDirectoryName(project.GetProjectMetadata().ProjectPath)!;
        string? configuration = new DirectoryInfo(Path.GetDirectoryName(typeof(FoldersDevelopmentCatalog).Assembly.Location)!).Parent?.Name;
        if (configuration is not ("Debug" or "Release"))
            throw new InvalidOperationException("The selected AppHost build configuration is unavailable.");
        string target = Path.Combine(directory, "bin", configuration, "net10.0", artifact);
        if (!File.Exists(target)) throw new InvalidOperationException("Build the selected source catalog owner before composing its runtime: " + target);
        Assembly loaded = Assembly.LoadFrom(target);
        if (!SHA256.HashData(File.ReadAllBytes(loaded.Location)).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(target))))
            throw new InvalidOperationException("The selected catalog owner differs from the loaded executable artifact.");
        return loaded;
    }
}
