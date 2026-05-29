using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

// xUnit1051 fires on NSubstitute arg-matcher setups for IClient methods that have a CancellationToken
// overload; these are substitute configuration (matching the no-token overload the page calls), not
// cancellable operations, so the rule does not apply here.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.7 / AC #1 / AC #2 / AC #6 / AC #7 / AC #9 / AC #10 / AC #12 — the tenant-scoped Provider
/// support page renders the capability-support matrix (FR57) read directly from explicit evidence, with
/// non-color-only badges, cursor pagination, the four empty/denied/unavailable states, and no mutation
/// affordances.
/// </summary>
public sealed class ProviderSupportPageTests
{
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void RendersCapabilityMatrix_WithBadges_AndScopeBannerFirst()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(
                truncated: false,
                cursor: null,
                Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported),
                Row(ProviderCapabilityName.File_operations, ProviderCapabilityState.Unsupported)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-matrix\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-support-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        // Scope-before-evidence (UX-DR6): the tenant banner renders first.
        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();

        rendered.FindAll("[data-testid=\"console-page-provider-support-row\"]").Count.ShouldBe(2);
        // Capability differences read directly from evidence; support state renders through a badge (AC #7).
        rendered.FindAll("[data-testid=\"console-page-provider-support-row\"] [data-testid=\"fc-status-badge\"]").Count.ShouldBe(2);
        rendered.Markup.ShouldContain("Repository creation");
        rendered.Markup.ShouldContain("File operations");

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TruncatedPage_RendersNextCursorLink()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(
                truncated: true,
                cursor: "next-cursor",
                Row(ProviderCapabilityName.Commit_status, ProviderCapabilityState.Temporarily_unavailable)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-next\"]").ShouldNotBeNull());

        string? href = rendered.Find("[data-testid=\"console-page-provider-support-next\"]").GetAttribute("href");
        href.ShouldNotBeNull();
        href!.ShouldContain("cursor=next-cursor");
    }

    [Fact]
    public void NonTruncatedPage_RendersEndOfResults()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(truncated: false, cursor: null, Row(ProviderCapabilityName.Branch_ref_policy, ProviderCapabilityState.Supported)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-end\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-provider-support-next\"]").ShouldBeEmpty();
    }

    [Fact]
    public void EmptyEvidence_RendersNoMatches()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(truncated: false, cursor: null));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"no_matches\"]").ShouldNotBeNull());

        rendered.FindAll("[data-testid=\"console-page-provider-support-matrix\"]").ShouldBeEmpty();
    }

    [Fact]
    public void DeniedRead_RendersSafeDenial_WithoutMatrix()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"tenant_access_denied","correlationId":"corr-y","retryable":false}""";
        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("tenant_access_denied");
        rendered.FindAll("[data-testid=\"console-page-provider-support-matrix\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-support-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void PresentCapabilityProfileRef_RendersMonospaceSafeCopyId()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(truncated: false, cursor: null, Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-row\"]").ShouldNotBeNull());

        // AC #7: a present capability-profile reference renders as a monospace SafeCopyId, never a raw cell.
        rendered.Find("[data-testid=\"console-page-provider-support-row\"] [data-testid=\"safe-copy\"] code")
            .TextContent.ShouldBe("cap-profile-1");
    }

    [Fact]
    public void MissingCapabilityProfileRef_RendersUnknownAffordance_NotEmptyOrLock()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(truncated: false, cursor: null, RowWithoutProfile(ProviderCapabilityName.File_operations, ProviderCapabilityState.Supported)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-row\"]").ShouldNotBeNull());

        // AC #3 / AC #8 (redacted != unknown != missing): a missing profile ref degrades to an honest
        // Unknown affordance — never a redacted lock and never an empty/blank safe-copy cell.
        rendered.Find("[data-testid=\"console-page-provider-support-row\"] [data-fc-disclosure=\"unknown\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-support-row\"] [data-fc-disclosure=\"redacted\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-support-row\"] [data-testid=\"safe-copy\"]").ShouldBeEmpty();
        // The support-state badge still renders (the row is not blank).
        rendered.Find("[data-testid=\"console-page-provider-support-row\"] [data-testid=\"fc-status-badge\"]").ShouldNotBeNull();
    }

    [Fact]
    public void SameCapability_DifferentSupportStatePerProfile_RendersDistinctBadges()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // FR57: the SAME capability shows different support states across two profiles (e.g. GitHub vs Forgejo).
        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(Evidence(
                truncated: false,
                cursor: null,
                Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported, "profile-github"),
                Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Unsupported, "profile-forgejo")));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.FindAll("[data-testid=\"console-page-provider-support-row\"]").Count.ShouldBe(2));

        // AC #6 / FR57: the badge for each row is driven by THAT row's own SupportState evidence (read
        // directly, never inferred), so the same capability renders distinct Success vs Danger slots.
        List<string?> slots = rendered
            .FindAll("[data-testid=\"console-page-provider-support-row\"] [data-testid=\"fc-status-badge\"]")
            .Select(b => b.GetAttribute("data-fc-badge-slot"))
            .ToList();
        slots.Count.ShouldBe(2);
        slots.ShouldContain("Success");
        slots.ShouldContain("Danger");
    }

    [Fact]
    public void StaleEvidence_RendersStaleFreshness_NotCurrent()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(StaleEvidence(Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported)));

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-freshness\"]").ShouldNotBeNull());

        // UX-DR26 / AC #8: stale evidence is labelled Stale on the support matrix, never presented as Current.
        string freshness = rendered.Find("[data-testid=\"console-page-provider-support-freshness\"]").TextContent;
        freshness.ShouldContain("Stale");
        freshness.ShouldNotContain("Current");
    }

    [Fact]
    public void MissingFreshness_RendersUnknown_NotCurrentNorFabricatedTimestamp()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // The projection returned evidence rows but no freshness metadata. UX-DR26 / AC #8: absent freshness
        // must render an honest "Unknown", never "Current", and never a fabricated 0001-01-01 observed-at.
        client.GetProviderSupportEvidenceAsync(Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<string>(), Arg.Any<int?>())
            .Returns(new ProviderSupportEvidenceList
            {
                Items = [Row(ProviderCapabilityName.Repository_creation, ProviderCapabilityState.Supported)],
                Page = new PaginationMetadata { Cursor = string.Empty, Limit = 50, IsTruncated = false },
                Freshness = null!,
            });

        IRenderedComponent<ProviderSupport> rendered = ctx.Render<ProviderSupport>();

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-support-freshness\"]").ShouldNotBeNull());

        string freshness = rendered.Find("[data-testid=\"console-page-provider-support-freshness\"]").TextContent;
        freshness.ShouldContain("Unknown");
        freshness.ShouldNotContain("Current");
        freshness.ShouldNotContain("0001");
    }

    private static ProviderSupportEvidenceList Evidence(bool truncated, string? cursor, params ProviderSupportEvidence[] items)
        => new()
        {
            Items = [.. items],
            Page = new PaginationMetadata { Cursor = cursor ?? string.Empty, Limit = 50, IsTruncated = truncated },
            Freshness = new FreshnessMetadata
            {
                Stale = false,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };

    private static ProviderSupportEvidence Row(ProviderCapabilityName capability, ProviderCapabilityState state)
        => new()
        {
            CapabilityProfileRef = "cap-profile-1",
            Capability = capability,
            SupportState = state,
        };

    private static ProviderSupportEvidence Row(ProviderCapabilityName capability, ProviderCapabilityState state, string profileRef)
        => new()
        {
            CapabilityProfileRef = profileRef,
            Capability = capability,
            SupportState = state,
        };

    private static ProviderSupportEvidence RowWithoutProfile(ProviderCapabilityName capability, ProviderCapabilityState state)
        => new()
        {
            CapabilityProfileRef = null,
            Capability = capability,
            SupportState = state,
        };

    private static ProviderSupportEvidenceList StaleEvidence(params ProviderSupportEvidence[] items)
        => new()
        {
            Items = [.. items],
            Page = new PaginationMetadata { Cursor = string.Empty, Limit = 50, IsTruncated = false },
            Freshness = new FreshnessMetadata
            {
                Stale = true,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
        };
}
