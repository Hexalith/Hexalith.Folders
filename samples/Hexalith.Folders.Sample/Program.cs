using System;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client;
using Hexalith.Folders.Client.Generated;

using Hexalith.Folders.Sample;

using Microsoft.Extensions.DependencyInjection;

// Hexalith.Folders SDK sample — drives the canonical lifecycle through the typed client and the
// convenience helpers. It is designed to run against the local AppHost Aspire topology.
//
// To run end-to-end:
//   1. Launch the AppHost:  dotnet run --project src/Hexalith.Folders.AppHost
//      (Aspire dashboard:    https://localhost:17000)
//   2. Set FOLDERS_BASE_ADDRESS to the Folders server endpoint shown in the dashboard, then run this sample.
//
// Without FOLDERS_BASE_ADDRESS the sample prints these instructions and exits 0 (no network call), keeping
// it usable in environments with no running services.

Console.WriteLine($"{FoldersClientModule.Name} sample");

string? baseAddressValue = Environment.GetEnvironmentVariable("FOLDERS_BASE_ADDRESS");
if (string.IsNullOrWhiteSpace(baseAddressValue) || !Uri.TryCreate(baseAddressValue, UriKind.Absolute, out Uri? baseAddress))
{
    Console.WriteLine("Set FOLDERS_BASE_ADDRESS to the Folders server endpoint to run the lifecycle.");
    Console.WriteLine("Launch the AppHost first: dotnet run --project src/Hexalith.Folders.AppHost (dashboard https://localhost:17000).");
    return 0;
}

// In a real run, replace the placeholder token factory with your token acquisition (for example, reading a
// bearer token from your identity provider). Returning a blank token exercises the unauthenticated path.
using ServiceProvider provider = FoldersSampleHost.BuildServiceProvider(
    baseAddress,
    static _ => ValueTask.FromResult(Environment.GetEnvironmentVariable("FOLDERS_BEARER_TOKEN")));

IClient client = provider.GetRequiredService<IClient>();

FolderLifecycleSample sample = new(client);
await sample.RunAsync(
    new FolderLifecycleInputs { TaskId = "task_01HZY7Z6N7J4Q2X8Y9V0TSK001" },
    CancellationToken.None).ConfigureAwait(false);

return 0;
