using Hexalith.Folders.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string accessControlConfigPath = ResolveDaprConfigPath(builder.AppHostDirectory, "accesscontrol.yaml");

IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase))
{
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
}

IResourceBuilder<ProjectResource> eventStore = builder.AddProject<Projects.Hexalith_EventStore>(FoldersAspireModule.EventStoreAppId);
IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>(FoldersAspireModule.TenantsAppId);
IResourceBuilder<ProjectResource> folders = builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);
IResourceBuilder<ProjectResource> foldersWorkers = builder.AddProject<Projects.Hexalith_Folders_Workers>(FoldersAspireModule.FoldersWorkersAppId);
IResourceBuilder<ProjectResource> foldersUi = builder.AddProject<Projects.Hexalith_Folders_UI>(FoldersAspireModule.FoldersUiAppId);

_ = builder.AddHexalithFolders(eventStore, tenants, folders, foldersWorkers, foldersUi, accessControlConfigPath);

if (keycloak is not null && realmUrl is not null)
{
    ConfigureJwt(eventStore, keycloak, realmUrl);
    ConfigureJwt(tenants, keycloak, realmUrl);
    ConfigureJwt(folders, keycloak, realmUrl);
    ConfigureJwt(foldersWorkers, keycloak, realmUrl);

    _ = foldersUi
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Folders__Authentication__Authority", realmUrl)
        .WithEnvironment("Folders__Authentication__ClientId", "hexalith-folders");
}

builder.Build().Run();

static void ConfigureJwt(
    IResourceBuilder<ProjectResource> resource,
    IResourceBuilder<KeycloakResource> keycloak,
    ReferenceExpression realmUrl)
{
    _ = resource
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
}

static string ResolveDaprConfigPath(string appHostDirectory, string fileName)
{
    string configPath = Path.Combine(appHostDirectory, "DaprComponents", fileName);
    if (File.Exists(configPath))
    {
        return configPath;
    }

    configPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", fileName);
    if (File.Exists(configPath))
    {
        return configPath;
    }

    throw new FileNotFoundException(
        "DAPR access control configuration not found. "
        + $"Ensure {fileName} exists in the DaprComponents directory.",
        configPath);
}
