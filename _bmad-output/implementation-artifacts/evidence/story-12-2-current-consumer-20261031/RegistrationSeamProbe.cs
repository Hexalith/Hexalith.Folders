using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

Assembly tests = Assembly.Load("Hexalith.Folders.Server.Tests");
Type factory = tests.GetType("Hexalith.Folders.Server.Tests.FoldersProductionTestHost", true)!;
WebApplicationBuilder builder = (WebApplicationBuilder)factory.GetMethod("CreateBuilder",
    BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
bool addRegistration = args.Contains("register-http", StringComparer.Ordinal);
if (addRegistration)
{
    builder.Services.AddEventStoreHttpHostSecurity();
}

WebApplication app = builder.Build();
await using var applicationLifetime = app.ConfigureAwait(false);
var identities = new[]
{
    typeof(FoldersServerHostComposition).Assembly,
    typeof(EventStoreDomainServiceExtensions).Assembly,
    typeof(EventStoreHttpHostSecurityExtensions).Assembly,
}.Select(assembly => new
{
    Name = assembly.GetName().FullName,
    assembly.Location,
    Mvid = assembly.ManifestModule.ModuleVersionId,
}).ToArray();
try
{
    app.UseFoldersServerPipeline();
    IReadOnlyList<string> failures = await EventStoreHttpEndpointInventory.ValidateAsync(
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints), app.Services).ConfigureAwait(false);
    Console.WriteLine(JsonSerializer.Serialize(new { addRegistration, identities, failures }));
    return failures.Count == 0 ? 0 : 2;
}
catch (Exception failure)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        addRegistration,
        identities,
        FailureType = failure.GetType().FullName,
        failure.Message,
        failure.StackTrace,
    }));
    return 1;
}
