using System.Collections.Generic;
using System.Net.Http;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

// xUnit1051 fires on the NSubstitute arg-matcher setups below, which now configure the CancellationToken
// overload of the IClient reads (the overload the page calls after Story 6.10) with an
// Arg.Any<CancellationToken>() matcher. These are substitute configuration, not cancellable test
// operations, so passing TestContext.Current.CancellationToken would be wrong here — suppress for the file.
#pragma warning disable xUnit1051

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.7 / AC #1 / AC #3 / AC #4 / AC #5 / AC #7 / AC #8 / AC #9 / AC #10 / AC #12 — the Provider
/// readiness view (§3.3) renders provider identity, disposition-primary readiness, the credential-reference
/// identifier (visible-vs-redacted-vs-unknown), advisory failure metadata (honest Unknown when no operation
/// context), the safe-denial path without existence leak, and never invokes the validate/configure methods.
/// </summary>
public sealed class ProviderPageTests
{
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void RendersIdentity_ReadinessDisposition_AndBindingBadge()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);

        // Disposition is the PRIMARY readiness visual (server-computed, fed straight into the badge).
        rendered.Find("[data-testid=\"console-page-provider-section-readiness\"] [data-testid=\"operator-disposition-badge\"]").ShouldNotBeNull();
        // Readiness reason is secondary metadata (the operator-sanitized Status string).
        rendered.Find("[data-testid=\"console-page-provider-readiness-status\"]").TextContent.ShouldBe("ready");

        // Repository binding state renders through a badge (non-color-only, AC #7) — never a bare span.
        rendered.Find("[data-testid=\"console-page-provider-repository-binding\"] [data-testid=\"fc-status-badge\"]").ShouldNotBeNull();

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void CredentialReferenceRedacted_RendersLock_AndNeverTheValue()
    {
        // Binding present ⇒ ProviderBinding.Redaction == credential_reference_redacted ⇒ the credential
        // reference is hidden by tenant policy, even though the diagnostics identifier carries a value.
        (BunitContext ctx, IClient client) = ArrangeHappyPath(
            bindingReference: new RedactableDiagnosticIdentifier
            {
                Value = "must-not-render",
                Classification = DiagnosticFieldClassification.Operator_sanitized,
            });
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-credential-reference\"]").ShouldNotBeNull());

        // Renders the redacted affordance (lock + explanatory text), distinct from unknown/missing.
        rendered.Find("[data-testid=\"console-page-provider-credential-reference\"] [data-fc-disclosure=\"redacted\"]").ShouldNotBeNull();
        // Defense in depth: the value is never emitted on a redacted field.
        rendered.Markup.ShouldNotContain("must-not-render");
    }

    [Fact]
    public void CredentialReferenceNotRedacted_RendersUnknown_NeverTheProviderBindingReferenceValue()
    {
        // The passive read path never returns a credential-reference VALUE (`nonSecretCredentialReference`
        // is a configure-request input only; `ProviderBinding.Redaction` is a required single-valued marker).
        // Even when the diagnostics carry a providerBindingReference value and NO ProviderBinding is loaded,
        // the credential-reference field must render an honest Unknown — never the provider-binding reference
        // borrowed and mislabeled as the credential reference (AC #3 / §2.3: redacted != unknown != wrong id).
        (BunitContext ctx, IClient client) = ArrangeHappyPath(
            withBinding: false,
            bindingReference: new RedactableDiagnosticIdentifier
            {
                Value = "must-not-appear-as-credential",
                Classification = DiagnosticFieldClassification.Operator_sanitized,
            });
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-credential-reference\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-credential-reference\"] [data-fc-disclosure=\"unknown\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-credential-reference-id\"]").ShouldBeEmpty();
        // The provider-binding reference value is NEVER surfaced as the credential reference.
        rendered.Markup.ShouldNotContain("must-not-appear-as-credential");
        // §3.3 accessibility label is still present (on the field's <dt>).
        rendered.Markup.ShouldContain("reference identifier (not a secret)");
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void CredentialReferenceUnknown_RendersUnknown_NotRedacted()
    {
        // No binding, no diagnostics identifier ⇒ honest Unknown (distinct from redacted/missing).
        (BunitContext ctx, IClient client) = ArrangeHappyPath(withBinding: false, bindingReference: null, includeDiagnosticsReference: false);
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-credential-reference\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-credential-reference\"] [data-fc-disclosure=\"unknown\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-credential-reference\"] [data-fc-disclosure=\"redacted\"]").ShouldBeEmpty();
    }

    [Fact]
    public void NoOperationContext_RendersHonestUnknownFailure_AndNoWorkspaceNote()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-failure\"]").ShouldNotBeNull());

        // No operationId/workspaceId in context ⇒ honest Unknown, never a fabricated retry/remediation value.
        rendered.Find("[data-testid=\"console-page-provider-no-operation\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-provider-no-workspace-context\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-retryability\"]").ShouldBeEmpty();
    }

    [Fact]
    public void WithOperationContext_RendersAdvisoryFailureMetadata()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetSyncStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Sync());
        client.GetProviderOutcomeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Outcome());

        // Workspace/operation context arrives via the query string ([SupplyParameterFromQuery]); in bUnit
        // these are supplied by navigating the URL rather than via the parameter builder.
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/folders/folder-1/provider?WorkspaceId=workspace-1&OperationId=op-1");

        IRenderedComponent<Provider> rendered = ctx.Render<Provider>(p => p
            .Add(c => c.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-retryability\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-failure-category\"]").ShouldNotBeNull();
        // Sync zone renders when a workspace context is resolved.
        rendered.Find("[data-testid=\"console-page-provider-section-sync\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-no-workspace-context\"]").ShouldBeEmpty();
        // Advisory display only — never an action affordance.
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void DeniedPrimaryRead_RendersSafeDenial_WithoutSections_OrExistenceLeak()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        const string body = """{"category":"tenant_access_denied","correlationId":"corr-y","retryable":false}""";
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HexalithFoldersApiException("denied", 403, body, EmptyHeaders, innerException: null));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-error-panel\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-error-category\"]").TextContent.ShouldBe("tenant_access_denied");
        rendered.FindAll("[data-testid=\"console-page-provider-section-identity\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-section-readiness\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void TransportFailure_RendersReadModelUnavailable_NotACrash()
    {
        // Confirms the page degrades to the §3.8 read-model-unavailable state when the API is unreachable
        // (a transport failure, not a canonical denial) — this is what makes the E2E smoke test pass
        // against the backend-less hermetic host, and never leaks a transport error.
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldNotBeNull());

        rendered.Find("[data-testid=\"console-page-provider-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void Renders_NoMutationAffordances()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void NeverInvokes_ValidateOrConfigure_OnLoad()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        // AC #5/#10: the active validation probe and the mutating configure command are never called.
        _ = client.DidNotReceive().ValidateProviderReadinessAsync(
            Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<ValidateProviderReadinessRequest>());
        _ = client.DidNotReceive().ConfigureProviderBindingAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfigureProviderBindingRequest>());
    }

    [Fact]
    public void RendersTenantScopeBanner_ScopeFirst_OnProviderPage()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        // AC #2 / UX-DR6: the tenant scope banner renders, scope-before-evidence — it precedes the heading
        // and the provider identity section (tenant comes from the context accessor, not the FolderId route).
        rendered.Find("[data-testid=\"tenant-scope-banner\"]").ShouldNotBeNull();
        string markup = rendered.Markup;
        int banner = markup.IndexOf("tenant-scope-banner", StringComparison.Ordinal);
        int heading = markup.IndexOf("<h1", StringComparison.Ordinal);
        int identity = markup.IndexOf("console-page-provider-section-identity", StringComparison.Ordinal);
        banner.ShouldBeGreaterThanOrEqualTo(0);
        banner.ShouldBeLessThan(heading);
        heading.ShouldBeLessThan(identity);
    }

    [Fact]
    public void CorrelationSection_RendersCorrelationAndProviderCorrelation_AsSafeCopyId()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-correlation\"]").ShouldNotBeNull());

        // AC #7: identifiers render as monospace safe-copy. The per-load correlation id and the provider
        // correlation reference both render inside the correlation section as SafeCopyId values.
        rendered.Find("[data-testid=\"console-page-provider-section-correlation\"] [data-testid=\"safe-copy\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-provider-correlation\"]").ShouldNotBeNull();
        rendered.Find("[data-testid=\"console-page-provider-provider-correlation\"]").TextContent.ShouldBe("prov-corr-1");
    }

    [Fact]
    public void SensitiveMetadataTier_RendersResolvedLabel_WhenPresent()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-sensitive-tier\"]").ShouldNotBeNull());

        // concern #17: the repository binding's sensitive-metadata tier surfaces its resolved label
        // (the happy-path repository binding is Tenant_sensitive).
        rendered.Find("[data-testid=\"console-page-provider-sensitive-tier\"]").TextContent.ShouldBe("Tenant-sensitive");
    }

    [Fact]
    public void FreshnessCurrent_RendersCurrent_NotStaleNorUnavailable()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-freshness-current\"]").ShouldNotBeNull());

        // UX-DR26 / AC #8: available, non-stale evidence is labelled Current — never Stale/Unavailable/Unknown.
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-stale\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-unavailable\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-unknown\"]").ShouldBeEmpty();
    }

    [Fact]
    public void StaleEvidence_RendersStaleFreshness_WithReasonCode_NeverCurrentOrUnavailable()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(DiagnosticsWith(stale: true, staleReasonCode: "projection_lag"));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-freshness-stale\"]").ShouldNotBeNull());

        // UX-DR26: stale evidence is labelled honestly (with its stable reason code) and NEVER as Current;
        // AC #8: stale/delayed != unavailable.
        rendered.Find("[data-testid=\"console-page-provider-freshness-stale\"]").TextContent.ShouldContain("projection_lag");
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-current\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-unavailable\"]").ShouldBeEmpty();
    }

    [Fact]
    public void UnavailableAvailability_RendersUnavailableFreshness_StillRendersSections()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(DiagnosticsWith(availability: ProjectionAvailability.Unavailable, unavailableReasonCode: "read_model_down"));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-freshness-unavailable\"]").ShouldNotBeNull());

        // AC #8: an Unavailable projection (read model down) is distinct from stale AND from the page-level
        // read-model-unavailable empty state — the diagnostics loaded, so the sections still render.
        rendered.Find("[data-testid=\"console-page-provider-freshness-unavailable\"]").TextContent.ShouldContain("read_model_down");
        rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull();
        rendered.FindAll("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-current\"]").ShouldBeEmpty();
    }

    [Fact]
    public void UnknownAvailability_RendersUnknownFreshness_NeverCurrent()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(DiagnosticsWith(availability: ProjectionAvailability.Unknown));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-freshness-unknown\"]").ShouldNotBeNull());

        // UX-DR26 reference-pending C5 fallthrough: Unknown availability renders Unknown, never Current.
        rendered.FindAll("[data-testid=\"console-page-provider-freshness-current\"]").ShouldBeEmpty();
    }

    [Fact]
    public void ReadinessReasonUnknown_RendersUnknownAffordance_DispositionStillPrimary()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(DiagnosticsWith(status: null));

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-readiness\"]").ShouldNotBeNull());

        // AC #4 / AC #8: when the (secondary) readiness reason is absent it renders an honest Unknown
        // affordance — never blank — while the (primary) disposition badge still renders.
        rendered.Find("[data-testid=\"console-page-provider-readiness-reason\"] [data-fc-disclosure=\"unknown\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-readiness-status\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-provider-section-readiness\"] [data-testid=\"operator-disposition-badge\"]").ShouldNotBeNull();
    }

    [Fact]
    public void IdentityFieldsUnknown_RenderUnknownAffordance_WhenBindingAbsent_NotRedacted()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;

        // No provider binding, no repository binding, and no diagnostics credential reference resolved.
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(DiagnosticsWith(includeBindingReference: false));
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(new FolderLifecycleStatus
            {
                FolderId = "folder-1",
                LifecycleState = LifecycleState.Ready,
                Archived = false,
                ProviderBindingRef = null,
                RepositoryBindingId = null,
                Freshness = Fresh(),
            });

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        // AC #8 (redacted != unknown != missing): every unresolved identity field — including the repository
        // binding state — renders the honest Unknown affordance, never a redacted lock and never a stale badge.
        rendered.Find("[data-testid=\"console-page-provider-repository-binding\"] [data-fc-disclosure=\"unknown\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-section-identity\"] [data-fc-disclosure=\"redacted\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-section-identity\"] [data-testid=\"fc-status-badge\"]").ShouldBeEmpty();
        rendered.FindAll("[data-testid=\"console-page-provider-section-identity\"] [data-fc-disclosure=\"unknown\"]").Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void WorkspaceContextWithoutOperation_RendersSync_ButHonestUnknownFailure()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        client.GetSyncStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Sync());

        // Workspace context only (no OperationId) — supplied via the query string ([SupplyParameterFromQuery]).
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/folders/folder-1/provider?WorkspaceId=workspace-1");

        IRenderedComponent<Provider> rendered = ctx.Render<Provider>(p => p
            .Add(c => c.FolderId, "folder-1"));

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-sync\"]").ShouldNotBeNull());

        // AC #5: with a workspace context the sync zone resolves, but with NO operation context the failure
        // zone still renders the honest "no recent provider operation" Unknown — never a fabricated retry value.
        rendered.FindAll("[data-testid=\"console-page-provider-no-workspace-context\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-provider-no-operation\"]").ShouldNotBeNull();
        rendered.FindAll("[data-testid=\"console-page-provider-retryability\"]").ShouldBeEmpty();
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void PrimaryRead_ReceivesCancellationToken()
    {
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the primary read is threaded the page's per-load CancellationToken so the
        // F-7 Cancel affordance can abort the in-flight request.
        _ = client.Received(1).GetProviderStatusDiagnosticsAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelDuringLoad_RendersNeutralCancelledReloadState_NotErrorNorUnavailable()
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        ControllableTimeProvider clock = (ControllableTimeProvider)ctx.Services.GetRequiredService<TimeProvider>();

        // The PRIMARY read observes the token and only completes (by throwing) when the operator cancels —
        // exactly the in-flight read the F-7 Cancel affordance aborts. The advisory permissions read and the
        // supplementary reads stay unstubbed (returning defaults) so the flow reaches this primary read.
        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                CancellationToken ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return (ProviderStatusDiagnostics)null!;
            });

        IRenderedComponent<Provider> rendered = Render(ctx);

        // The loading branch renders SkeletonState with the page's preserved loading testid.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-loading\"]").ShouldNotBeNull());

        // Advance past the 2 s threshold so "still loading… [Cancel]" appears, then cancel.
        clock.Advance(TimeSpan.FromSeconds(2));
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-still-loading-cancel\"]").ShouldNotBeNull());
        rendered.Find("[data-testid=\"console-still-loading-cancel\"]").Click();

        // AC #5: Cancel resolves to the neutral cancelled state — a stable, non-error idle view with a
        // read-only reload — NOT the safe-denial panel and NOT the read-model-unavailable empty state.
        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-reload\"]").ShouldNotBeNull());
        rendered.FindAll("[data-testid=\"console-error-panel\"]").ShouldBeEmpty();
        rendered.FindAll("[data-fc-empty-reason=\"read_model_unavailable\"]").ShouldBeEmpty();
        rendered.Find("[data-testid=\"console-page-provider-root\"]").ShouldNotBeNull();
        rendered.FindAll("h1").Count.ShouldBe(1);
        rendered.ShouldHaveNoMutationAffordances();
    }

    [Fact]
    public void SupplementaryReads_ReceiveCancellationToken()
    {
        // The happy-path primary diagnostics resolve non-null, so the load proceeds past the primary read
        // into the supplementary reads.
        (BunitContext ctx, IClient client) = ArrangeHappyPath();
        using BunitContext _ctx = ctx;

        IRenderedComponent<Provider> rendered = Render(ctx);

        rendered.WaitForAssertion(() =>
            rendered.Find("[data-testid=\"console-page-provider-section-identity\"]").ShouldNotBeNull());

        // Story 6.10 AC #5/#14: the per-load CancellationToken is threaded into the supplementary reads, not
        // just the primary read. GetFolderLifecycleStatusAsync runs unconditionally once the primary
        // diagnostics resolve, so its CancellationToken overload must receive the token.
        _ = client.Received(1).GetFolderLifecycleStatusAsync(
            "folder-1", Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>());
    }

    private static IRenderedComponent<Provider> Render(BunitContext ctx)
        => ctx.Render<Provider>(p => p.Add(c => c.FolderId, "folder-1"));

    private static (BunitContext Ctx, IClient Client) ArrangeHappyPath(
        RedactableDiagnosticIdentifier? bindingReference = null,
        bool withBinding = true,
        bool includeDiagnosticsReference = true)
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();

        client.GetProviderStatusDiagnosticsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Diagnostics(bindingReference, includeDiagnosticsReference));
        client.GetFolderLifecycleStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Lifecycle(providerRef: withBinding ? "pbr-1" : null));
        client.GetProviderBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Binding());
        client.GetRepositoryBindingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ReadConsistencyClass?>(), Arg.Any<CancellationToken>())
            .Returns(Repository());

        return (ctx, client);
    }

    private static ProviderStatusDiagnostics Diagnostics(
        RedactableDiagnosticIdentifier? bindingReference,
        bool includeReference)
        => new()
        {
            Status = "ready",
            Disposition = OperatorDispositionLabel.Available,
            Trust = new DiagnosticTrustEvidence { Availability = ProjectionAvailability.Available },
            Freshness = Fresh(),
            ProviderBindingReference = includeReference
                ? bindingReference ?? new RedactableDiagnosticIdentifier
                {
                    Value = "diag-ref-1",
                    Classification = DiagnosticFieldClassification.Operator_sanitized,
                }
                : null,
            ProviderCorrelationReference = "prov-corr-1",
        };

    private static ProviderStatusDiagnostics DiagnosticsWith(
        string? status = "ready",
        OperatorDispositionLabel disposition = OperatorDispositionLabel.Available,
        ProjectionAvailability availability = ProjectionAvailability.Available,
        bool stale = false,
        string? staleReasonCode = null,
        string? unavailableReasonCode = null,
        bool includeBindingReference = true)
        => new()
        {
            Status = status,
            Disposition = disposition,
            Trust = new DiagnosticTrustEvidence
            {
                Availability = availability,
                StaleReasonCode = staleReasonCode,
                UnavailableReasonCode = unavailableReasonCode,
            },
            Freshness = new FreshnessMetadata
            {
                Stale = stale,
                ObservedAt = DateTimeOffset.UnixEpoch,
                ProjectionWatermark = "wm-1",
                ReadConsistency = ReadConsistencyClass.Eventually_consistent,
            },
            ProviderBindingReference = includeBindingReference
                ? new RedactableDiagnosticIdentifier
                {
                    Value = "diag-ref-1",
                    Classification = DiagnosticFieldClassification.Operator_sanitized,
                }
                : null,
            ProviderCorrelationReference = "prov-corr-1",
        };

    private static FolderLifecycleStatus Lifecycle(string? providerRef)
        => new()
        {
            FolderId = "folder-1",
            LifecycleState = LifecycleState.Ready,
            Archived = false,
            ProviderBindingRef = providerRef,
            RepositoryBindingId = "rb-1",
            Freshness = Fresh(),
        };

    private static ProviderBinding Binding()
        => new()
        {
            ProviderBindingRef = "pbr-1",
            ProviderFamilyRef = "github",
            CapabilityProfileRef = "cap-1",
            Redaction = ProviderBindingRedaction.Credential_reference_redacted,
            Freshness = Fresh(),
        };

    private static RepositoryBinding Repository()
        => new()
        {
            RepositoryBindingId = "rb-1",
            FolderId = "folder-1",
            ProviderBindingRef = "pbr-1",
            BindingState = RepositoryBindingBindingState.Bound,
            SensitiveMetadataTier = SensitiveMetadataTier.Tenant_sensitive,
            Freshness = Fresh(),
        };

    private static SyncStatusDiagnostics Sync()
        => new()
        {
            Status = "syncing",
            Disposition = OperatorDispositionLabel.Available,
            Trust = new DiagnosticTrustEvidence { Availability = ProjectionAvailability.Available },
            Freshness = Fresh(),
            ProjectedState = LifecycleState.Ready,
            ProviderOutcomeState = ProviderOutcomeState.Known_success,
        };

    private static ProviderOutcome Outcome()
        => new()
        {
            OperationId = "op-1",
            State = ProviderOutcomeState.Known_failure,
            SanitizedStatusClass = CanonicalErrorCategory.Provider_unavailable,
            ProviderCorrelationReference = "prov-corr-op",
            RetryEligibility = new RetryEligibility { Eligible = true, ReasonCode = "provider_unavailable", AdvisoryOnly = true },
            RetryAfter = new RetryAfterMetadata { RetryAfterSeconds = 30, AdvisoryOnly = true },
            Freshness = Fresh(),
        };

    private static FreshnessMetadata Fresh()
        => new()
        {
            Stale = false,
            ObservedAt = DateTimeOffset.UnixEpoch,
            ProjectionWatermark = "wm-1",
            ReadConsistency = ReadConsistencyClass.Eventually_consistent,
        };
}
