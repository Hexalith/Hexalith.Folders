using Bunit;
using Bunit.TestDoubles;

using Hexalith.Folders.UI.Components.Layout;
using Hexalith.Folders.UI.Components.Pages;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Contracts.Storage;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Shell.State.Theme;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class ShellCompositionTests
{
    [Fact]
    public async Task MainLayout_RendersFrontComposerShell()
    {
        BunitContext ctx = new();
        try
        {
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;

            // The FrontComposer shell's FcAccountMenu resolves AuthenticationStateProvider during
            // OnInitializedAsync (it reads the signed-in principal's claims). Register bUnit's fake
            // authorization so the placeholder provider does not throw; an unauthenticated state is
            // sufficient because the menu degrades to its anonymous "sign in" affordance.
            ctx.AddAuthorization();
            ctx.Services.AddLogging();
            ctx.Services.AddFluentUIComponents();
            ctx.Services.AddHexalithFrontComposerQuickstart();
            ctx.Services.Replace(ServiceDescriptor.Scoped<IStorageService, InMemoryStorageService>());
            ctx.Services.Replace(ServiceDescriptor.Scoped<IUserContextAccessor>(_ =>
            {
                IUserContextAccessor accessor = Substitute.For<IUserContextAccessor>();
                accessor.TenantId.Returns("test-tenant");
                accessor.UserId.Returns("test-user");
                return accessor;
            }));
            ctx.Services.Replace(ServiceDescriptor.Scoped<IThemeService>(_ => Substitute.For<IThemeService>()));

            // Stub the JS modules the shell loads on first render so bUnit's loose runtime resolves them.
            ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-beforeunload.js");
            ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-prefers-color-scheme.js");
            ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-keyboard.js");
            ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-focus.js");

            RenderFragment childContent = builder =>
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "data-testid", "inside-shell");
                builder.AddContent(2, "X");
                builder.CloseElement();
            };

            IRenderedComponent<MainLayout> rendered = ctx.Render<MainLayout>(parameters => parameters
                .Add(p => p.Body, childContent));

            rendered.WaitForAssertion(() =>
            {
                rendered.Markup.ShouldContain("fc-shell-root");
                rendered.Find("[data-testid=\"inside-shell\"]").ShouldNotBeNull();
            });
        }
        finally
        {
            // FluentUI's DataGridFocusScope is IAsyncDisposable only; the default sync Dispose
            // on the bUnit service provider would throw. Drain via DisposeAsync explicitly.
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public void Home_RendersWithoutMutationControls()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Production);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        rendered.Find("h1").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-home-root\"]").ShouldNotBeNull();

        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("fluentinputform").ShouldBeEmpty();
        rendered.FindAll("fluentdialog").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
    }

    [Fact]
    public void Home_RendersProviderSupportNavLink_PointingToProvidersSupport()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Production);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        // AC #11: the console home exposes a hand-authored nav entry to the tenant-scoped provider-support
        // capability matrix (Story 6.7), reachable in every environment (not a dev-only gallery link).
        rendered.Find("[data-testid=\"console-page-home-provider-support-link\"] a")
            .GetAttribute("href").ShouldBe("/providers/support");
    }

    [Fact]
    public void Home_RendersDevGalleryLink_InDevelopmentOnly()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Development);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        rendered.Find("[data-testid=\"console-page-home-dev-gallery-link\"]").ShouldNotBeNull();
        rendered.Markup.ShouldContain("/dev/state-label-gallery");
    }

    [Fact]
    public void Home_HidesDevGalleryLink_InProduction()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Production);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        rendered.FindAll("[data-testid=\"console-page-home-dev-gallery-link\"]").ShouldBeEmpty();
        rendered.Markup.ShouldNotContain("/dev/state-label-gallery");
    }

    [Fact]
    public void Home_RendersRedactionGalleryLink_InDevelopmentOnly()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Development);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        rendered.Find("[data-testid=\"console-page-home-dev-redaction-gallery-link\"]").ShouldNotBeNull();
        rendered.Markup.ShouldContain("/dev/redaction-gallery");
    }

    [Fact]
    public void Home_HidesRedactionGalleryLink_InProduction()
    {
        using BunitContext ctx = CreateHomeContext(Environments.Production);
        IRenderedComponent<Home> rendered = ctx.Render<Home>();

        rendered.FindAll("[data-testid=\"console-page-home-dev-redaction-gallery-link\"]").ShouldBeEmpty();
        rendered.Markup.ShouldNotContain("/dev/redaction-gallery");
    }

    private static BunitContext CreateHomeContext(string environmentName)
    {
        BunitContext ctx = new();
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        ctx.Services.AddSingleton(env);
        return ctx;
    }

    [Fact]
    public void Composition_DoesNotResolveAnyServerOnlyType()
    {
        (IServiceCollection services, _, _) = CompositionRootFactory.Build(
            CompositionRootFactory.WithAuthority("https://example.invalid/realm"));

        // No descriptor's service-type or implementation-type may carry server-only namespace prefixes.
        // We assert at the descriptor list level — provider build introspection isn't required, and a
        // server-only registration would surface here regardless of whether anything resolved it.
        IEnumerable<string> registeredTypeNames = services
            .SelectMany(d => new[] { d.ServiceType.FullName, d.ImplementationType?.FullName })
            .Where(name => name is not null)
            .Cast<string>();

        foreach (string name in registeredTypeNames)
        {
            name.ShouldNotStartWith("Hexalith.Folders.Server.");
            name.ShouldNotStartWith("Hexalith.Folders.Aggregates.");
            name.ShouldNotStartWith("Hexalith.Folders.Domain.");
            name.ShouldNotStartWith("Hexalith.Folders.Workers.");
        }
    }
}
