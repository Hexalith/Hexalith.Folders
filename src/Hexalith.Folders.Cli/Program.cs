using System.Reflection;

using Hexalith.Folders.Cli;
using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Cli.Infrastructure;

// --version / -v short-circuit (assembly informational version), mirroring the reference adapter.
if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
{
    string version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";
    System.Console.WriteLine(version);
    return FoldersExitCodes.Success;
}

CliDependencies dependencies = new()
{
    Console = new SystemCliConsole(),
    Credentials = new CredentialResolver(),
    ClientFactory = FoldersClientFactory.Create,
};

CliApplication application = new(dependencies);
return await application.RunAsync(args).ConfigureAwait(false);
