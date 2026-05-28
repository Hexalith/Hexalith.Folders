using Hexalith.Folders.UI.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Boot-smoke for the AC #8 AppHost composition. The AC asks the test to assert two boot scenarios:
/// Keycloak ON (Authority present) and Keycloak OFF (Authority missing). The Folders AppHost sidecar
/// composition is exercised through <c>CompositionRoot.ConfigureServices</c> — the same call that
/// <c>Program.cs</c> runs at boot — so the AC #3 fail-closed gate is verified end-to-end without
/// bringing up a real Aspire host (which would require an Aspire.Hosting.Testing package reference
/// that this story constrains to bunit only).
/// </summary>
public sealed class AppHostBootSmokeTests
{
    [Fact]
    public void Boot_With_Authority_Present_Succeeds_In_Development()
    {
        Should.NotThrow(() => CompositionRootFactory.Build(
            CompositionRootFactory.WithAuthority("https://keycloak.invalid/realms/hexalith"),
            Environments.Development));
    }

    [Fact]
    public void Boot_With_Authority_Present_Succeeds_In_Production()
    {
        Should.NotThrow(() => CompositionRootFactory.Build(
            CompositionRootFactory.WithAuthority("https://keycloak.invalid/realms/hexalith"),
            Environments.Production));
    }

    [Fact]
    public void Boot_Without_Authority_Succeeds_In_Development()
    {
        // Keycloak=false + Development → AC #3 permits missing Authority so the developer can iterate.
        Should.NotThrow(() => CompositionRootFactory.Build(
            configuration: null,
            environmentName: Environments.Development));
    }

    [Fact]
    public void Boot_Without_Authority_FailsClosed_In_Production()
    {
        // Keycloak=false + Production → AC #3 fail-closed: refusing to start without real auth.
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CompositionRootFactory.Build(configuration: null, environmentName: Environments.Production));

        ex.Message.ShouldContain(FoldersAuthenticationOptions.SectionName);
        ex.Message.ShouldContain("Authority");
    }

    [Fact]
    public void Boot_With_HermeticTestMode_Succeeds_In_Test_Environment()
    {
        Should.NotThrow(() => CompositionRootFactory.Build(
            CompositionRootFactory.WithHermeticTestMode(),
            CompositionRoot.TestEnvironmentName));
    }

    [Fact]
    public void Boot_With_HermeticTestMode_FailsClosed_In_Production()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CompositionRootFactory.Build(
                CompositionRootFactory.WithHermeticTestMode(),
                Environments.Production));

        ex.Message.ShouldContain(FoldersAuthenticationOptions.HermeticTestMode);
        ex.Message.ShouldContain("Development or Test");
    }
}
