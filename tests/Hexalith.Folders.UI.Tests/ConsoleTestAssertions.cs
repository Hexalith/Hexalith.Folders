using Bunit;

using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 — shared bUnit assertions for the diagnostic pages/components, including the 5-selector
/// command-suppression guard mirrored across every read-only page test.
/// </summary>
internal static class ConsoleTestAssertions
{
    /// <summary>Asserts the rendered output exposes none of the five mutation affordances (read-only console).</summary>
    public static void ShouldHaveNoMutationAffordances<TComponent>(this IRenderedComponent<TComponent> rendered)
        where TComponent : IComponent
    {
        rendered.FindAll("form").ShouldBeEmpty();
        rendered.FindAll("fluentinputform").ShouldBeEmpty();
        rendered.FindAll("fluentdialog").ShouldBeEmpty();
        rendered.FindAll("[data-fc-command]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-mutation]").ShouldBeEmpty();
    }
}
