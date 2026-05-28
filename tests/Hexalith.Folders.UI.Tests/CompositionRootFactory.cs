using Hexalith.Folders.UI.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Convenience helpers that build a service collection equivalent to <c>Program.cs</c>'s
/// boot so tests can introspect what the production composition resolves.
/// </summary>
internal static class CompositionRootFactory
{
    /// <summary>
    /// Builds a configuration + service collection like Program.cs would, with the supplied
    /// configuration overrides and environment.
    /// </summary>
    public static (IServiceCollection Services, IConfiguration Configuration, IHostEnvironment Environment) Build(
        IDictionary<string, string?>? configuration = null,
        string? environmentName = null)
    {
        environmentName ??= Environments.Development;
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? new Dictionary<string, string?>())
            .Build();

        IHostEnvironment env = CreateEnvironment(environmentName);

        ServiceCollection services = [];
        CompositionRoot.ConfigureServices(services, config, env);
        return (services, config, env);
    }

    public static IHostEnvironment CreateEnvironment(string environmentName)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        env.ApplicationName.Returns("Hexalith.Folders.UI.Tests");
        env.ContentRootPath.Returns(AppContext.BaseDirectory);
        return env;
    }

    public static IConfiguration BuildConfiguration(IDictionary<string, string?>? configuration = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? new Dictionary<string, string?>())
            .Build();

    public static IDictionary<string, string?> WithAuthority(string authority)
        => new Dictionary<string, string?>
        {
            [$"{FoldersAuthenticationOptions.SectionName}:Authority"] = authority,
            [$"{FoldersAuthenticationOptions.SectionName}:ClientId"] = "hexalith-folders",
        };

    public static IDictionary<string, string?> WithHermeticTestMode()
        => new Dictionary<string, string?>
        {
            [$"{FoldersAuthenticationOptions.SectionName}:Mode"] = FoldersAuthenticationOptions.HermeticTestMode,
        };
}
