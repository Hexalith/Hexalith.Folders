using Bunit;

using Hexalith.Folders.UI.Components.Pages;
using Hexalith.FrontComposer.Contracts.Registration;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class NavigationContractTests
{
    [Fact]
    public void Tenants_RendersWithoutMutationControls()
    {
        using BunitContext ctx = new();
        IRenderedComponent<Tenants> rendered = ctx.Render<Tenants>();

        rendered.Find("h1").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-tenants-root\"]").ShouldNotBeNull();

        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("fluentinputform").ShouldBeEmpty();
        rendered.FindAll("fluentdialog").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
    }

    [Fact]
    public void Console_DoesNotRegisterAnyDomainCommandManifest()
    {
        (IServiceCollection services, _, _) = CompositionRootFactory.Build(
            CompositionRootFactory.WithAuthority("https://example.invalid/realm"));

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: false);
        using IServiceScope scope = provider.CreateScope();
        IFrontComposerRegistry registry = scope.ServiceProvider.GetRequiredService<IFrontComposerRegistry>();

        registry.GetManifests().ShouldBeEmpty();
    }
}
