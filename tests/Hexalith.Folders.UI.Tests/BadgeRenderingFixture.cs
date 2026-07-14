using Bunit;
using Bunit.TestDoubles;

using Hexalith.FrontComposer.Contracts.Storage;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Shell.State.Theme;
using Hexalith.FrontComposer.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Shared bUnit context bootstrapper for Story 6.3 component tests. Reuses the Story 6.2
/// shell composition (FluentUI + FrontComposer quickstart + in-memory storage + substituted
/// theme service) so badge + metadata + gallery tests don't duplicate the boilerplate.
/// </summary>
internal static class BadgeRenderingFixture
{
    public static BunitContext Create()
    {
        BunitContext ctx = new();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLogging();

        // Story 6.10 — a controllable TimeProvider so any SkeletonState rendered by a page/component test can
        // be injected (and, where a test casts it back, advanced deterministically). Not advancing it keeps
        // SkeletonState in its ≤400 ms band (root + label, no bars), so existing loading-state assertions hold.
        ctx.Services.AddSingleton<System.TimeProvider>(new ControllableTimeProvider());
        ctx.Services.AddFluentUIComponents();
        ctx.Services.AddHexalithFrontComposerQuickstart();
        ctx.Services.Replace(ServiceDescriptor.Scoped<IStorageService, InMemoryStorageService>());
        ctx.Services.Replace(ServiceDescriptor.Scoped<IThemeService>(_ => Substitute.For<IThemeService>()));

        ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-beforeunload.js");
        ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-prefers-color-scheme.js");
        ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-keyboard.js");
        ctx.JSInterop.SetupModule("./_content/Hexalith.FrontComposer.Shell/js/fc-focus.js");

        return ctx;
    }
}
