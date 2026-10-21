using System.Globalization;
using System.Reflection;
using System.Security.Claims;

using Bunit;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.FrontComposer.Shell.State.Navigation;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Tenants.Audit;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantUsers;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.Tenants.UI.State.UserTenants;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components.Authorization;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class GlobalAdministratorCorrectionPanelTests : FluentBunitContext
{
    [Theory]
    [InlineData("en", "desktop")]
    [InlineData("en", "narrow")]
    [InlineData("fr", "desktop")]
    [InlineData("fr", "narrow")]
    public void Global_preview_exports_localized_rendered_markup_for_browser_validation(string culture, string viewport)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Services.AddLocalization();
            Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
            if (viewport == "narrow")
            {
                Services.GetRequiredService<TenantHighImpactViewportObservation>().Observe(ViewportTier.Phone);
            }
            IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RestoreIntent())
                .Add(component => component.CurrentProjection, Projection("other-admin")));
            cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBe(viewport == "narrow");
            foreach (string field in new[]
            {
                "tenants-correction-original-evidence", "tenants-correction-original-time",
                "tenants-correction-scope", "tenants-correction-target-user",
                "tenants-correction-current-state", "tenants-correction-admin-count",
                "tenants-correction-resulting-count", "tenants-correction-provenance",
                "tenants-correction-authority-admission", "tenants-correction-preview-data",
            })
            {
                cut.Find($"[data-testid='{field}']").ShouldNotBeNull();
            }

            cut.Find("[data-testid='tenants-correction-original-time']").TextContent.Trim()
                .ShouldBe("2026-06-01 10:00:00 UTC");
            cut.Find("[data-testid='tenants-correction-provenance']").TextContent.Trim()
                .ShouldBe(culture == "fr"
                    ? "Projection système complète et actuelle avec une version ordonnée."
                    : "Complete, current system projection with an ordered version.");
            cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
                .ShouldBe(culture == "fr"
                    ? viewport == "narrow"
                        ? "La confirmation est indisponible pour la raison indiquée ci-dessus."
                        : "L’autorité de plateforme et l’admission de commande sont prêtes. La confirmation les vérifie à nouveau."
                    : viewport == "narrow"
                        ? "Confirm is unavailable for the reason shown above."
                        : "Platform authority and command admission are ready. Confirm rechecks both.");

            string? directory = Environment.GetEnvironmentVariable("TENANTS_CORRECTION_FIXTURE_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"global-correction-preview-{viewport}-{culture}.html"), cut.Markup);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    public GlobalAdministratorCorrectionPanelTests()
    {
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        Services.AddSingleton(viewport);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider());
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            Audit("proof", "GlobalAdministratorSet")));
    }

    [Fact]
    public async Task DisposedCorrectionPanelDoesNotReactToPrincipalChange()
    {
        var authentication = new StubAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        GlobalAdministratorCorrectionPanel instance = cut.Instance;
        long generationBeforeChange = PrivateField<long>(instance, "_authorizationGeneration");

        await cut.InvokeAsync(() => authentication.Publish(Task.FromResult(StubAuthenticationStateProvider.AuthenticatedState())));
        cut.WaitForAssertion(() =>
        {
            PrivateField<long>(instance, "_authorizationGeneration").ShouldBe(generationBeforeChange + 1);
            cut.Find("[data-testid='tenants-correction-panel']");
        });

        await DisposeComponentsAsync();
        PrivateField<bool>(instance, "_disposed").ShouldBeTrue();
        long generationAfterDispose = PrivateField<long>(instance, "_authorizationGeneration");
        authentication.Publish(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));

        PrivateField<long>(instance, "_authorizationGeneration").ShouldBe(generationAfterDispose);
    }

    [Fact]
    public void Initial_correction_authorization_ignores_non_authoritative_synchronous_reflection_until_resolver_completes()
    {
        var pendingAuthorization = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition
        {
            SynchronousReflection = TenantLifecycleAuthorizationReflectionState.Authorized,
            AuthorizationTask = pendingAuthorization.Task,
        });
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.FindAll("[data-testid='tenants-correction-confirm']").ShouldBeEmpty();

        pendingAuthorization.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse());
    }

    [Fact]
    public void Restore_preview_shows_fixed_scope_command_and_no_tenant_role_selector()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RestoreIntent();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-panel']").GetAttribute("role").ShouldBe("region");
        cut.Find("[data-testid='tenants-correction-domain']").TextContent.ShouldContain("Global administrators");
        cut.Find("[data-testid='tenants-correction-command']").TextContent.ShouldContain("Set global administrator");
        cut.Find("[data-testid='tenants-correction-scope']").TextContent.ShouldContain("global-administrators");
        cut.Find("[data-testid='tenants-correction-target-user']").TextContent.ShouldContain("admin-user");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.ShouldContain("Absent");
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("1");
        cut.Find("[data-testid='tenants-correction-resulting-count']").TextContent.Trim().ShouldBe("2");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
        // Platform-governance correction must never offer a tenant role selector (AC1/AC2).
        cut.FindAll("[data-testid='tenants-correction-role']").ShouldBeEmpty();
        cut.VisibleText().ShouldNotContain("tenant role", Case.Insensitive);
        cut.VisibleText().ShouldNotContain("member", Case.Insensitive);
    }

    [Fact]
    public void Unordered_projection_marks_provenance_unverified_and_blocks_confirm()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin") with { ProjectionVersion = "legacy" }));

        cut.Find("[data-testid='tenants-correction-provenance']").TextContent.Trim()
            .ShouldBe("Current complete projection evidence is unavailable.");
        cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Confirm is unavailable for the reason shown above.");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Restore_recheck_with_changed_count_and_version_sends_no_command()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin", "new-admin") with { ProjectionVersion = "tenant-sequence:2" },
            Audit("proof", "GlobalAdministratorSet"))
        {
            AutoAdvanceProjectionVersion = false,
        });
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        commandGateway.SetRequests.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_preview_shows_resulting_count_and_self_removal_warning_with_another_administrator()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition
        {
            CallerSubject = "admin-user",
        });
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-resulting-count']")
            .TextContent.Trim().ShouldBe("1"));
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("2");
        cut.Find("[data-testid='tenants-correction-preview-data']").TextContent
            .ShouldContain("This removes your own global-administrator authority.");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Unchanged_parent_rerender_keeps_complete_revoke_preview_and_confirm_ready()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        GlobalAdministratorsSnapshot projection = Projection("admin-user", "other-admin");
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.RemovePreview?.IsComplete.ShouldBe(true));
        GlobalAdministratorRemovePreview preview = cut.Instance.Snapshot!.RemovePreview!;

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));

        cut.Instance.Snapshot!.RemovePreview.ShouldBeSameAs(preview);
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Remove_preview_does_not_override_stale_projection_at_the_same_version()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        GlobalAdministratorsSnapshot projection = Projection("admin-user", "other-admin");
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.RemovePreview?.IsComplete.ShouldBe(true));

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection with { Freshness = ReadModelFreshnessState.Stale }));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.Correction.Unavailable.CurrentProjectionUnavailable");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Unavailable_revoke_recheck_hides_count_and_presence_without_dispatch()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commands = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commands);
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin"))
            .Add(component => component.ProjectionRefreshProvider,
                () => Task.FromException<GlobalAdministratorsSnapshot?>(new HttpRequestException("projection unavailable"))));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-admin-count']")
            .TextContent.Trim().ShouldBe("2"));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-admin-count']")
            .TextContent.Trim().ShouldBe("-"));
        cut.Find("[data-testid='tenants-correction-resulting-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        commands.RemoveRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Null_restore_recheck_hides_retained_projection_facts_without_dispatch()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commands = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commands);
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.ProjectionRefreshProvider,
                () => Task.FromResult<GlobalAdministratorsSnapshot?>(null)));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.SafeMessageKey
            .ShouldBe("Tenants.Correction.Unavailable.CurrentProjectionUnavailable"));
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-resulting-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-provenance']").TextContent.Trim()
            .ShouldBe("Current complete projection evidence is unavailable.");
        commands.SetRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Null_postcommand_projection_hides_retained_facts_until_complete_refresh_confirms()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commands = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commands);
        GlobalAdministratorsSnapshot before = Projection("other-admin");
        GlobalAdministratorsSnapshot? postcommand = null;
        int reads = 0;
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, before)
            .Add(component => component.ProjectionRefreshProvider,
                () => Task.FromResult(++reads == 1 ? before : postcommand)));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.SafeMessageKey
            .ShouldBe("Tenants.Correction.Unavailable.CurrentProjectionUnavailable"));
        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        cut.Instance.Snapshot.LastConfirmedProjectionEvidence.ShouldBeSameAs(before);
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-resulting-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-provenance']").TextContent.Trim()
            .ShouldBe("Current complete projection evidence is unavailable.");
        commands.SetRequests.ShouldHaveSingleItem();
        string messageId = cut.Instance.Snapshot.MessageId!;

        postcommand = Projection("admin-user", "other-admin") with { ProjectionVersion = "tenant-sequence:2" };
        cut.Find("[data-testid='tenants-correction-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot!.MessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBeNull();
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("2");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.ShouldContain("Present");
        commands.SetRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Changed_revoke_preview_blocks_dispatch_until_operator_reviews_current_facts()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commands = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commands);
        GlobalAdministratorsSnapshot original = Projection("admin-user", "other-admin");
        GlobalAdministratorsSnapshot changed = Projection("admin-user", "other-admin", "third-admin") with
        {
            ProjectionVersion = "tenant-sequence:2",
        };
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, original)
            .Add(component => component.ProjectionRefreshProvider,
                () => Task.FromResult<GlobalAdministratorsSnapshot?>(changed)));
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.RemovePreview?.IsComplete.ShouldBe(true));
        cut.Instance.Snapshot!.RemovePreview!.CurrentAdministratorCount.ShouldBe(2);

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Find("[data-testid='tenants-correction-admin-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-resulting-count']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-current-state']").TextContent.Trim().ShouldBe("-");
        cut.Find("[data-testid='tenants-correction-provenance']").TextContent.Trim()
            .ShouldBe("Current complete projection evidence is unavailable.");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        commands.RemoveRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Authority_admission_stays_blocked_until_remove_preview_completes()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        var pending = new TaskCompletionSource<GlobalAdministratorRemovePreview>(TaskCreationOptions.RunContinuationsAsynchronously);
        GlobalAdministratorsSnapshot projection = Projection("admin-user", "other-admin");
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition
        {
            RemovePreviewTask = pending.Task,
        });

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, projection));
        cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Confirm is unavailable for the reason shown above.");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();

        pending.SetResult(GlobalAdministratorRemovePreview.Create("admin-user", "operator", projection, isAuthorized: true));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Platform authority and command admission are ready. Confirm rechecks both."));
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void RestorePreviewEscapesFormatIdentityButSubmitsOriginalValue()
    {
        const string target = "admin\u200Duser";
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        TenantCorrectionStartIntent baseline = RestoreIntent();
        var previewInputs = new Dictionary<string, string>(baseline.RequiredPreviewInputs, StringComparer.Ordinal)
        {
            ["userId"] = target,
        };
        TenantCorrectionStartIntent intent = baseline with
        {
            TargetUserId = target,
            RequiredPreviewInputs = previewInputs,
        };

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-target-user']")
            .TextContent.ShouldContain(@"admin\{U+200D}user");
        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        commandGateway.SetRequests[0].UserId.ShouldBe(target);
    }

    [Fact]
    public void RestorePreviewTokenizesControlsDefaultIgnorablesAndLiteralEscapeLookingText()
    {
        const string target = "  admin\u0001\u034F\uFE0F\\{U+200D}  ";
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent baseline = RestoreIntent();
        var previewInputs = new Dictionary<string, string>(baseline.RequiredPreviewInputs, StringComparer.Ordinal)
        {
            ["userId"] = target,
        };
        TenantCorrectionStartIntent intent = baseline with
        {
            TargetUserId = target,
            RequiredPreviewInputs = previewInputs,
        };

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-target-user']").TextContent.ShouldContain(
            @"  admin\{U+0001}\{U+034F}\{U+FE0F}\\{U+200D}  ");
    }

    [Fact]
    public void PreviewAndLifecycleUseExpandedFluentAccordionSections()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.FindAll("fluent-accordion").ShouldHaveSingleItem();
        cut.FindAll("fluent-accordion-item").Count.ShouldBe(2);
        cut.FindAll("fluent-accordion-item").ShouldAllBe(item => item.HasAttribute("expanded")
            && item.GetAttribute("heading-level") == "4");
        cut.Find("#tenants-correction-title").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public void Restore_submits_set_command_once_and_links_projection_confirmed_corrective_proof()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int reads = 0;
        StubTenantQueryGateway queryGateway = new(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"))
        {
            GlobalAdministratorProvider = _ => ++reads == 1
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        commandGateway.SetRequests[0].UserId.ShouldBe("admin-user");
        commandGateway.RemoveRequests.ShouldBeEmpty();
        commandGateway.StatusHandles.ShouldHaveSingleItem().ShouldBe(new TenantCommandTrackingHandle(
            commandGateway.TrackedMessageIds.ShouldHaveSingleItem(),
            "tracking-safe",
            GlobalAdministratorGrantPreview.FixedAggregateId));
        queryGateway.GlobalAdminRequests.Count.ShouldBe(2);
        queryGateway.AuditRequests.ShouldHaveSingleItem().TenantId.ShouldBe("system");
        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Find("[data-testid='tenants-correction-state']").TextContent.ShouldContain("Projection confirms the intended state", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-proof-link']").GetAttribute("href").ShouldBe("/tenants/system/audit?receiptReference=event-corrective");
        cut.Find("[data-testid='tenants-correction-proof-link']").TextContent.ShouldContain("2026-06-01 10:05:00 UTC");
        cut.Markup.ShouldNotContain("undone", Case.Insensitive);
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
    }

    [Fact]
    public void Confirmed_correction_can_refresh_a_delayed_audit_receipt_without_redispatch()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        bool auditReady = false;
        int projectionReads = 0;
        TenantAuditSnapshot correctiveAudit = Audit("event-corrective", "GlobalAdministratorSet");
        StubTenantQueryGateway queryGateway = new(
            ProjectionAfterGrant("admin-user", "other-admin"), correctiveAudit)
        {
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
            AuditProvider = _ => auditReady ? correctiveAudit : correctiveAudit with { Rows = [] },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        Services.AddSingleton(new GlobalAdministratorCorrectionProofRegistry());
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
        auditReady = true;
        cut.Find("[data-testid='tenants-correction-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        cut.Find("[data-testid='tenants-correction-proof-link']").GetAttribute("href")
            .ShouldBe("/tenants/system/audit?receiptReference=event-corrective");
        commandGateway.SetRequests.ShouldHaveSingleItem();
        commandGateway.StatusHandles.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Closing_panel_during_protected_proof_lookup_retains_verified_pair_in_circuit()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        var pendingAudit = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        int projectionReads = 0;
        StubTenantQueryGateway queryGateway = new(
            ProjectionAfterGrant("admin-user", "other-admin"), Audit("event-corrective", "GlobalAdministratorSet"))
        {
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
            AsyncAuditProvider = _ => pendingAudit.Task,
        };
        var registry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(registry);
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => queryGateway.AuditRequests.Count.ShouldBe(1));
        cut.Dispose();
        TenantAuditRequest request = queryGateway.AuditRequests[0];
        pendingAudit.SetResult(Audit("event-corrective", "GlobalAdministratorSet") with
        {
            From = request.From,
            To = request.To,
            RequestCursor = request.Cursor,
            RequestPageSize = request.PageSize,
            Stream = request.Stream,
        });
        await submit;

        registry.FindAll("event-safe-reference").Count.ShouldBe(1);
        commandGateway.SetRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Corrective_proof_walks_protected_pages_and_rejects_a_similar_unrelated_event()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int projectionReads = 0;
        TenantAuditRow unrelated = CorrectiveRow("unrelated-event", "GlobalAdministratorSet") with
        {
            CommandCorrelationId = "other-command",
        };
        StubTenantQueryGateway queryGateway = new(
            ProjectionAfterGrant("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"))
        {
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
            AuditProvider = request => request.Cursor is null
                ? Audit("unrelated-event", "GlobalAdministratorSet") with
                {
                    Rows = [unrelated],
                    HasMore = true,
                    NextCursor = "proof-page-2",
                }
                : Audit("event-corrective", "GlobalAdministratorSet"),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        cut.Instance.Snapshot!.ProofLink.ShouldNotBeNull().CorrectiveAuditReference.ShouldBe("event-corrective");
        queryGateway.AuditRequests.Select(static request => request.Cursor).ShouldBe([null, "proof-page-2"]);
    }

    [Fact]
    public void Similar_unrelated_audit_event_cannot_create_a_paired_link()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int projectionReads = 0;
        StubTenantQueryGateway queryGateway = new(
            ProjectionAfterGrant("admin-user", "other-admin"),
            Audit("unrelated-event", "GlobalAdministratorSet"))
        {
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
            AuditProvider = _ => Audit("unrelated-event", "GlobalAdministratorSet") with
            {
                Rows = [CorrectiveRow("unrelated-event", "GlobalAdministratorSet") with
                {
                    CommandCorrelationId = "another-command",
                }],
            },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        cut.Instance.Snapshot.ProofLink.ShouldBeNull();
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
    }

    [Fact]
    public void Restore_uses_projection_refresh_provider_without_second_global_admin_query_and_still_links_proof()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        StubTenantQueryGateway queryGateway = new(
            Projection("unused-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"));
        int projectionRefreshCount = 0;
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.ProjectionRefreshProvider, () =>
            {
                projectionRefreshCount++;
                return Task.FromResult<GlobalAdministratorsSnapshot?>(projectionRefreshCount == 1
                    ? Projection("other-admin")
                    : ProjectionAfterGrant("admin-user", "other-admin"));
            }));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        projectionRefreshCount.ShouldBe(2);
        queryGateway.GlobalAdminRequests.ShouldBeEmpty();
        queryGateway.AuditRequests.ShouldHaveSingleItem().TenantId.ShouldBe("system");
        cut.Find("[data-testid='tenants-correction-state']").TextContent.ShouldContain("Projection confirms the intended state", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-proof-link']").GetAttribute("href").ShouldBe("/tenants/system/audit?receiptReference=event-corrective");
    }

    [Fact]
    public void Revoke_of_last_global_administrator_is_hard_blocked_before_submit()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user")));

        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-safe-message']").TextContent.ShouldContain("last global administrator");
        cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Confirm is unavailable for the reason shown above.");
        cut.Find("[data-testid='tenants-correction-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        commandGateway.RemoveRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Pre_submit_preview_live_updates_last_admin_hard_stop_when_projection_refreshes_while_open()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();

        // Open a revoke preview against a two-admin projection: submittable.
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();

        // While the panel stays open (same audit intent), the parent passes a refreshed projection in which
        // only the target remains. The last-administrator hard stop must re-engage instead of staying frozen
        // at open-time (the pre-submit preview lives; a submitted/terminal state would be preserved).
        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user") with { ProjectionVersion = "tenant-sequence:2" }));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot!.SafeMessageKey.ShouldBe("Tenants.Correction.GlobalAdmin.LastAdministrator");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void ExistingGrantTargetFailsClosedThenLiveUpdatesWhenTargetBecomesAbsent()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RestoreIntent();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.AlreadyApplied);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe(
            "Tenants.GlobalAdministrators.Grant.Preview.Unavailable.TargetExists");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Confirm is unavailable for the reason shown above.");

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot!.CanSubmit.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Pre_submit_blocked_last_administrator_live_updates_to_submittable_when_projection_refreshes_while_open()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot!.SafeMessageKey.ShouldBe("Tenants.Correction.GlobalAdmin.LastAdministrator");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot!.CanSubmit.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Revoke_submits_remove_command_and_confirms_only_on_absent_projection()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int projectionReads = 0;
        StubTenantQueryGateway queryGateway = new(
            Projection("other-admin"),
            Audit("event-corrective", "GlobalAdministratorRemoved"))
        {
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? Projection("admin-user", "other-admin")
                : Projection("other-admin") with { ProjectionVersion = "tenant-sequence:3" },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => commandGateway.RemoveRequests.ShouldHaveSingleItem());
        commandGateway.RemoveRequests[0].UserId.ShouldBe("admin-user");
        commandGateway.SetRequests.ShouldBeEmpty();
        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Find("[data-testid='tenants-correction-proof-link']").GetAttribute("href").ShouldBe("/tenants/system/audit?receiptReference=event-corrective");
        cut.Find("[data-testid='tenants-correction-authority-admission']").TextContent.Trim()
            .ShouldBe("Confirm is unavailable for the reason shown above.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reached_sequence_with_wrong_postcondition_releases_global_lease(bool revoke)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway();
        GlobalAdministratorsSnapshot unchanged = revoke
            ? Projection("admin-user", "other-admin")
            : Projection("other-admin");
        int projectionReads = 0;
        var queryGateway = new StubTenantQueryGateway(
            unchanged, Audit("proof", revoke ? "GlobalAdministratorRemoved" : "GlobalAdministratorSet"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = _ => ++projectionReads == 1
                ? unchanged
                : unchanged with { ProjectionVersion = "tenant-sequence:2" },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, revoke ? RevokeIntent() : RestoreIntent())
            .Add(component => component.CurrentProjection, unchanged));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
        cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.Instance.Snapshot.SafeRecoveryKey.ShouldBeNull();
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent.Trim()
            .ShouldBe("Restore the blocked authorization, lifecycle, viewport, or projection evidence before retrying.");
        cut.Find("[data-testid='tenants-correction-audit-state']").TextContent.Trim()
            .ShouldBe("Corrective audit evidence is unavailable.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_status_not_beyond_preview_baseline_releases_global_lease(bool revoke)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(
                CommandStatus.Completed,
                EventCount: 1,
                HasVerifiedCommandIdentity: true)
            {
                CommittedEventSequence = 1,
            },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        GlobalAdministratorsSnapshot baseline = revoke
            ? Projection("admin-user", "other-admin")
            : Projection("other-admin");
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            baseline,
            TenantAuditSnapshot.Unavailable(new TenantAuditRequest("system"))));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, revoke ? RevokeIntent() : RestoreIntent())
            .Add(component => component.CurrentProjection, baseline));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot!.SafeMessageKey.ShouldBe("Tenants.Correction.Unavailable.CommandProofUnavailable");
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent.Trim()
            .ShouldBe("The completed correction cannot yet be verified against current tenant evidence. Refresh status or escalate before relying on the result.");
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent.Trim()
            .ShouldBe(revoke
                ? "Refresh the complete fixed-scope projection before continuing."
                : "Refresh the complete fixed-scope projection and rebuild the preview.");
        if (revoke)
        {
            commandGateway.RemoveRequests.ShouldHaveSingleItem();
            commandGateway.SetRequests.ShouldBeEmpty();
        }
        else
        {
            commandGateway.SetRequests.ShouldHaveSingleItem();
            commandGateway.RemoveRequests.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Restore_confirmation_walks_all_pages_before_accepting_later_page_presence()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int queryReads = 0;
        StubTenantQueryGateway queryGateway = new(
            Projection("unused-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = request => queryReads++ < 2
                ? request.Cursor is null
                    ? ProjectionPage(["other-admin"], "page-2", hasMore: true)
                    : ProjectionPage(["third-admin"], null, hasMore: false)
                : request.Cursor is null
                    ? ProjectionPage(["other-admin"], "page-2", hasMore: true) with { ProjectionVersion = "tenant-sequence:2" }
                    : ProjectionPage(["admin-user"], null, hasMore: false) with { ProjectionVersion = "tenant-sequence:2" },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin", "third-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        queryGateway.GlobalAdminRequests.Select(static request => request.Cursor).ShouldBe([null, "page-2", null, "page-2"]);
        cut.Instance.Snapshot!.LastConfirmedProjectionEvidence.ShouldNotBeNull()
            .IsCompleteEvidence.ShouldBeTrue();
    }

    [Fact]
    public void Revoke_confirmation_walks_all_pages_before_accepting_complete_absence()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        int completeWalks = 0;
        StubTenantQueryGateway queryGateway = new(
            Projection("unused-admin"),
            Audit("event-corrective", "GlobalAdministratorRemoved"))
        {
            GlobalAdministratorProvider = request => request.Cursor is null && ++completeWalks == 1
                ? Projection("admin-user", "other-admin")
                : request.Cursor is null
                    ? ProjectionPage(["other-admin"], "page-2", hasMore: true) with { ProjectionVersion = "tenant-sequence:3" }
                    : ProjectionPage(["third-admin"], null, hasMore: false) with { ProjectionVersion = "tenant-sequence:3" },
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        queryGateway.GlobalAdminRequests.Select(static request => request.Cursor).ShouldBe([null, null, "page-2"]);
        cut.Instance.Snapshot!.TargetCurrentlyPresent.ShouldBeFalse();
        cut.Instance.Snapshot.CurrentAdministratorCount.ShouldBe(2);
    }

    [Fact]
    public void Rejected_remove_stays_rejected_without_false_success()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new()
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Rejected(
                "The last global administrator cannot be removed.",
                "LastGlobalAdministrator")),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);

        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            TenantAuditSnapshot.Unavailable(new TenantAuditRequest("system"))));

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Rejected));
        cut.Instance.Snapshot!.RejectionCode.ShouldBe("LastGlobalAdministrator");
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-state']").TextContent
            .ShouldNotContain("Projection confirms the intended state", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldContain("review the rejection", Case.Insensitive);
    }

    [Fact]
    public async Task AmbiguousRemoveCorrectionRetriesTheSameIdentityAndReleasesAfterProjectionProof()
    {
        int projectionReads = 0;
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
        };
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = _ => ++projectionReads >= 3
                ? Projection("other-admin") with { ProjectionVersion = "tenant-sequence:3" }
                : Projection("admin-user", "other-admin"),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        cut.Instance.Snapshot!.IsSubmissionAmbiguous.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-refresh']").TextContent
            .ShouldContain("Retry removal delivery", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldContain("same tracked removal", Case.Insensitive);

        commandGateway.RemoveResultTask = Task.FromResult(
            TenantCommandSubmissionResult.Accepted("ignored", "correlation-remove"));
        await cut.Find("[data-testid='tenants-correction-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.TrackedMessageIds.ShouldBe([messageId, messageId]);
        commandGateway.StatusHandles.ShouldHaveSingleItem().ShouldBe(new TenantCommandTrackingHandle(
            messageId,
            "correlation-remove",
            GlobalAdministratorRemovePreview.FixedAggregateId));
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task ChangedCallerBeforeAmbiguousCorrectionRemoveRetryRetainsIdentityWithoutRedispatch()
    {
        var composition = new StubTenantsBffComposition();
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
        };
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved")));

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        composition.CallerSubject = "admin-user";

        await cut.Find("[data-testid='tenants-correction-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        GlobalAdministratorCorrectionSnapshot retained = cut.Instance.Snapshot.ShouldNotBeNull();
        retained.IsSubmissionAmbiguous.ShouldBeTrue();
        retained.MessageId.ShouldBe(messageId);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task CorrelationlessRemoveAdoptionDoesNotAutomaticallyRetryDelivery()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved")));
        TenantCorrectionStartIntent intent = RevokeIntent();
        GlobalAdministratorsSnapshot projection = Projection("admin-user", "other-admin");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> first =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));
        await first.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        first.Instance.Snapshot!.IsSubmissionAmbiguous.ShouldBeTrue();
        first.Instance.Dispose();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));
        await WaitUntilAsync(
            () => replacement.Instance.Snapshot?.MessageId == messageId,
            TimeSpan.FromSeconds(5));

        await Task.Delay(50);
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        replacement.Instance.Snapshot!.IsSubmissionAmbiguous.ShouldBeTrue();
        replacement.Find("[data-testid='tenants-correction-refresh']").TextContent
            .ShouldContain("Retry removal delivery", Case.Insensitive);
        replacement.Find("[data-testid='tenants-correction-refresh']")
            .GetAttribute("data-recovery-kind").ShouldBe("delivery-retry");
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreUnableToVerifyIsRetainedAndAdoptedByReplacementPanel()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SetResultTask = Task.FromResult(
                TenantCommandSubmissionResult.Accepted("ignored", "correlation-safe")),
            Status = TenantCommandStatusResult.Unknown("Status unavailable."),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            Audit("proof", "GlobalAdministratorSet")));
        TenantCorrectionStartIntent intent = RestoreIntent();
        GlobalAdministratorsSnapshot projection = Projection("other-admin");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> first =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));
        await first.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        first.WaitForAssertion(() => first.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        string messageId = first.Instance.Snapshot!.MessageId.ShouldNotBeNull();
        first.Instance.Snapshot.CorrelationId.ShouldBe("correlation-safe");
        first.Instance.Dispose();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));

        replacement.WaitForAssertion(() =>
        {
            replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            replacement.Instance.Snapshot.MessageId.ShouldBe(messageId);
            replacement.Instance.Snapshot.CorrelationId.ShouldBe("correlation-safe");
            replacement.Instance.Snapshot.IsSubmissionAmbiguous.ShouldBeFalse();
        });
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        commandGateway.StatusHandles.ShouldAllBe(handle => handle.MessageId == messageId);
    }

    [Fact]
    public async Task CorrelatedRemoveStatusRefreshStaysAvailableOnUnsafeViewport()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved")));
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Accepted);
            cut.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeFalse();
        });

        viewport.Observe(ViewportTier.Phone);

        cut.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement refresh = cut.Find("[data-testid='tenants-correction-refresh']");
            refresh.HasAttribute("disabled").ShouldBeFalse();
            refresh.GetAttribute("data-recovery-kind").ShouldBe("status");
            refresh.ClassList.ShouldNotContain("ga-correction-panel__mutation-initiation");
        });
    }

    [Fact]
    public async Task AmbiguousRemoveRetryIsWithdrawnWhenViewportBecomesUnsafe()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved")));
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find("[data-testid='tenants-correction-refresh']")
            .GetAttribute("data-recovery-kind").ShouldBe("delivery-retry");

        viewport.Observe(ViewportTier.Phone);
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='tenants-correction-refresh']").ShouldBeEmpty());
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AmbiguousRemoveRetryExplainsWhenItsLeaseDispatchCannotBegin()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved"))
        {
            AutoAdvanceProjectionVersion = false,
        });
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        GlobalAdministratorCorrectionSnapshot expected = cut.Instance.Snapshot.ShouldNotBeNull();
        expected.IsSubmissionAmbiguous.ShouldBeTrue();
        TenantAggregateCommandLease lease =
            PrivateField<TenantAggregateCommandLease>(cut.Instance, "_admissionLease");
        object owner = PrivateField<object>(cut.Instance, "_admissionOwner");
        GlobalAdministratorReconciliationState dispatchBasis = expected.ToReconciliation().ShouldNotBeNull();
        lease.TryBeginReconciliationDispatch(owner, dispatchBasis, out long competingToken).ShouldBeTrue();

        long generation = PrivateField<long>(cut.Instance, "_operationGeneration");
        MethodInfo retryMethod = typeof(GlobalAdministratorCorrectionPanel).GetMethod(
            "RetryAmbiguousDeliveryAsync",
            BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        await ((Task)retryMethod.Invoke(cut.Instance, [generation])!).WaitAsync(TimeSpan.FromSeconds(5));

        GlobalAdministratorCorrectionSnapshot blocked = cut.Instance.Snapshot.ShouldNotBeNull();
        blocked.SafeMessageKey.ShouldBe(
            "Tenants.GlobalAdministrators.Availability.Remove.Unavailable.AggregateBusy");
        blocked.SafeRecoveryKey.ShouldBe(
            "Tenants.GlobalAdministrators.Availability.Recovery.AggregateBusy");
        blocked.MessageId.ShouldBe(expected.MessageId);
        commandGateway.RemoveRequests.ShouldHaveSingleItem();

        lease.TryAbortReconciliationDispatch(competingToken).ShouldBeTrue();
    }

    [Fact]
    public void NarrowViewportMutationInitiationUsesDeepSelectorForFluentRetry()
    {
        string styles = File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "GlobalAdministratorCorrectionPanel.razor.css"));
        int breakpointIndex = styles.IndexOf("@media (max-width: 767px)", StringComparison.Ordinal);
        breakpointIndex.ShouldBeGreaterThan(-1);
        styles[breakpointIndex..].ShouldContain("::deep .ga-correction-panel__mutation-initiation");
        styles[..breakpointIndex].Contains("::deep .ga-correction-panel__mutation-initiation", StringComparison.Ordinal)
            .ShouldBeFalse("hoisting the retry rule out of the media query hides it at every width");
    }

    [Fact]
    public void OrdinaryUnableToVerifyUsesRemovalRecoveryInsteadOfGenericCorrectionCopy()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        GlobalAdministratorCorrectionSnapshot unable = cut.Instance.Snapshot! with
        {
            LifecycleState = TenantCommandLifecycleState.UnableToVerify,
            SafeMessageKey = "Tenants.GlobalAdministrators.Remove.Status.Unknown",
            SafeRecoveryKey = null,
        };
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, unable);
        cut.Render();

        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldBe("Refresh the complete fixed-scope projection before continuing.");
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldNotContain("Restore the blocked authorization");
    }

    [Fact]
    public async Task RemovalRetryPreflightKeepsUnableToVerifyWithoutASecondDispatch()
    {
        int projectionReads = 0;
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = Task.FromResult(new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.AlreadyApplied,
                "ignored",
                "unsupported-correlation")),
        };
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved"))
        {
            GlobalAdministratorProvider = _ => ++projectionReads >= 2
                ? Projection("other-admin") with { ProjectionVersion = "tenant-sequence:9" }
                : Projection("admin-user", "other-admin"),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RevokeIntent())
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.IsSubmissionAmbiguous.ShouldBeTrue();

        await cut.Find("[data-testid='tenants-correction-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot.IsSubmissionAmbiguous.ShouldBeTrue();
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("ambiguous")]
    [InlineData("unsupported")]
    [InlineData("rejected")]
    public async Task InitialRemoveDeliveryCompletionSurvivesDisposalAndReplacement(string outcome)
    {
        var delivery = new TaskCompletionSource<TenantCommandSubmissionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var statusHold = new TaskCompletionSource<TenantCommandStatusResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway
        {
            RemoveResultTask = delivery.Task,
            StatusResultTask = outcome == "accepted" ? statusHold.Task : null,
            Status = new TenantCommandStatusResult(
                CommandStatus.Received,
                HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved")));
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantCorrectionStartIntent intent = RevokeIntent();
        GlobalAdministratorsSnapshot projection = Projection("admin-user", "other-admin");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> first =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));
        Task submit = first.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.RemoveSubmissionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        TenantAggregateCommandLease initialLease = PrivateField<TenantAggregateCommandLease>(
            first.Instance,
            "_admissionLease");
        initialLease.IsReconciliationDispatchInFlight.ShouldBeTrue();
        first.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeTrue();
        first.Instance.Dispose();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, projection));
        await WaitUntilAsync(
            () => replacement.Instance.Snapshot?.MessageId == messageId,
            TimeSpan.FromSeconds(5));
        replacement.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeTrue();

        delivery.SetResult(outcome switch
        {
            "accepted" => TenantCommandSubmissionResult.Accepted("ignored", "correlation-safe"),
            "ambiguous" => TenantCommandSubmissionResult.Ambiguous(
                "ignored",
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"),
            "unsupported" => new TenantCommandSubmissionResult(
                TenantCommandLifecycleState.AlreadyApplied,
                "ignored",
                "unsupported-correlation"),
            _ => TenantCommandSubmissionResult.Rejected("rejected", "LastGlobalAdministrator"),
        });

        if (outcome == "accepted")
        {
            await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            replacement.WaitForAssertion(() =>
            {
                replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Accepted);
                replacement.Instance.Snapshot.CorrelationId.ShouldBe("correlation-safe");
            });
            statusHold.SetResult(new TenantCommandStatusResult(
                CommandStatus.Received,
                HasVerifiedCommandIdentity: true));
        }
        else if (outcome == "ambiguous")
        {
            replacement.WaitForAssertion(() =>
                replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.RequestSent));
        }
        else if (outcome == "unsupported")
        {
            replacement.WaitForAssertion(() =>
                replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        }
        else
        {
            replacement.WaitForAssertion(() =>
                replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Rejected));
        }

        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe(messageId);
        initialLease.IsReconciliationDispatchInFlight.ShouldBeFalse();
        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators())
            .ShouldBe(outcome != "rejected");
    }

    [Theory]
    [InlineData("Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Evidence")]
    [InlineData("Tenants.GlobalAdministrators.Remove.Preview.Unavailable.TargetMissing")]
    [InlineData("Tenants.GlobalAdministrators.Remove.Preview.Unavailable.LastAdministrator")]
    public void EveryRemovalPreviewProjectionBlockRecoversFromLiveParentEvidence(string blockKey)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        GlobalAdministratorCorrectionSnapshot blocked = cut.Instance.Snapshot! with
        {
            LifecycleState = TenantCommandLifecycleState.UnableToVerify,
            SafeMessageKey = blockKey,
            SafeRecoveryKey = "Tenants.GlobalAdministrators.Remove.Preview.Recovery.Refresh",
        };
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, blocked);

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Previewed);
        cut.Instance.Snapshot.CanSubmit.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("failed")]
    [InlineData("ambiguous")]
    [InlineData("tracked")]
    [InlineData("publish-failed")]
    [InlineData("timed-out")]
    [InlineData("unable")]
    public void RemovalCorrectionLifecycleUsesItsSnapshotSpecificRecovery(string scenario)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        string messageId = NUlid.Ulid.NewUlid().ToString();
        GlobalAdministratorCorrectionSnapshot requestSent = cut.Instance.Snapshot!.RequestSent(messageId);
        GlobalAdministratorCorrectionSnapshot accepted = requestSent.Accepted(
            TenantCommandSubmissionResult.Accepted(messageId, "correlation-remove"));
        GlobalAdministratorCorrectionSnapshot snapshot = scenario switch
        {
            "rejected" => requestSent.ApplySubmissionFailure(
                TenantCommandSubmissionResult.Rejected("rejected", "LastGlobalAdministrator")),
            "failed" => requestSent.ApplySubmissionFailure(
                TenantCommandSubmissionResult.Failed("failed")),
            "ambiguous" => requestSent.ApplySubmissionFailure(
                TenantCommandSubmissionResult.Ambiguous(
                    messageId,
                    "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous")),
            "tracked" => accepted,
            "publish-failed" => accepted.ApplyStatus(new TenantCommandStatusResult(
                CommandStatus.PublishFailed,
                HasVerifiedCommandIdentity: true)),
            "timed-out" => accepted.ApplyStatus(new TenantCommandStatusResult(
                CommandStatus.TimedOut,
                HasVerifiedCommandIdentity: true)),
            "unable" => accepted.ApplyStatus(TenantCommandStatusResult.Unknown("unknown")),
            _ => throw new InvalidOperationException($"Unknown scenario '{scenario}'."),
        };
        string expectedRecovery = scenario switch
        {
            "rejected" => "Refresh current evidence and review the rejection before a new attempt.",
            "failed" => "Refresh current evidence before starting a new attempt.",
            "ambiguous" => "Refresh evidence, then retry this same tracked removal attempt.",
            "tracked" => "Refresh the complete fixed-scope projection before continuing.",
            "publish-failed" => "Refresh tracked status until publication or terminal evidence is available.",
            "timed-out" => "Refresh the tracked attempt before considering another removal.",
            "unable" => "Refresh the complete fixed-scope projection before continuing.",
            _ => throw new InvalidOperationException(),
        };
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object owner = typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionOwner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(owner).ShouldBeTrue();
        if (snapshot.ToReconciliation() is { } reconciliation)
        {
            lease.TryAdvanceReconciliation(owner, reconciliation).ShouldBeTrue();
        }
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionLease", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, lease);
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, snapshot);

        cut.Render();

        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldBe(expectedRecovery);
    }

    [Theory]
    [InlineData("pending", "Tracked grant status is pending; keep the same command identity and refresh.")]
    [InlineData("unknown", "Tracked grant status is unavailable; keep the same command identity and retry verification.")]
    [InlineData("rejected", "The tracked grant was rejected. Refresh platform authority before trying another action.")]
    [InlineData("publish-failed", "The grant event could not be published. Keep the same tracked command and retry verification.")]
    [InlineData("timed-out", "The tracked grant timed out before its result could be verified.")]
    public void RestoreAccessStatusRendersStableLocalizedGrantCopy(string scenario, string expectedCopy)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RestoreIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, Projection("other-admin")));
        string messageId = NUlid.Ulid.NewUlid().ToString();
        GlobalAdministratorCorrectionSnapshot accepted = cut.Instance.Snapshot!
            .RequestSent(messageId)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, "correlation-safe"));
        TenantCommandStatusResult status = scenario switch
        {
            "pending" => TenantCommandStatusResult.Pending("gateway pending text"),
            "unknown" => TenantCommandStatusResult.Unknown("gateway unknown text"),
            "rejected" => new TenantCommandStatusResult(
                CommandStatus.Rejected,
                SafeMessage: "gateway rejected text",
                RejectionCode: "RejectedForTest",
                HasVerifiedCommandIdentity: true),
            "publish-failed" => new TenantCommandStatusResult(
                CommandStatus.PublishFailed,
                SafeMessage: "gateway publish text",
                HasVerifiedCommandIdentity: true),
            "timed-out" => new TenantCommandStatusResult(
                CommandStatus.TimedOut,
                SafeMessage: "gateway timeout text",
                HasVerifiedCommandIdentity: true),
            _ => throw new InvalidOperationException($"Unknown scenario '{scenario}'."),
        };
        GlobalAdministratorCorrectionSnapshot snapshot = accepted.ApplyStatus(status);
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object owner = typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease.ShouldNotBeNull().TryMarkDispatched(owner).ShouldBeTrue();
        if (snapshot.ToReconciliation() is { } reconciliation)
        {
            lease.TryAdvanceReconciliation(owner, reconciliation).ShouldBeTrue();
        }

        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionLease", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, lease);
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, snapshot);

        cut.Render();

        cut.Find("[data-testid='tenants-correction-safe-message']").TextContent.ShouldBe(expectedCopy);
        cut.Markup.ShouldNotContain("gateway ", Case.Insensitive);
    }

    [Fact]
    public void RemovalStatusWithGrantGatewayKeyRendersRemovalSpecificCopy()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        TenantCorrectionStartIntent intent = RevokeIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, intent)
                .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        string messageId = NUlid.Ulid.NewUlid().ToString();
        GlobalAdministratorCorrectionSnapshot snapshot = cut.Instance.Snapshot!
            .RequestSent(messageId)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, "correlation-remove"))
            .ApplyStatus(new TenantCommandStatusResult(
                CommandStatus.PublishFailed,
                SafeMessage: "gateway grant publish text",
                HasVerifiedCommandIdentity: true,
                SafeMessageKey: "Tenants.GlobalAdministrators.Grant.Status.PublishFailed"));
        TenantAggregateCommandAdmissionGate admissionGate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object owner = typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease.ShouldNotBeNull().TryMarkDispatched(owner).ShouldBeTrue();
        if (snapshot.ToReconciliation() is { } reconciliation)
        {
            lease.TryAdvanceReconciliation(owner, reconciliation).ShouldBeTrue();
        }

        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_admissionLease", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, lease);
        typeof(GlobalAdministratorCorrectionPanel)
            .GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cut.Instance, snapshot);

        cut.Render();

        cut.Find("[data-testid='tenants-correction-safe-message']").TextContent
            .ShouldBe("The removal event could not be published.");
        cut.Markup.ShouldNotContain("grant event", Case.Insensitive);
        cut.Markup.ShouldNotContain("gateway grant", Case.Insensitive);
    }

    [Fact]
    public void TrackedZeroEventGrantStatusFailsClosedAcrossParentRerender()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new()
        {
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 0, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            TenantAuditSnapshot.Unavailable(new TenantAuditRequest("system"))));
        TenantCorrectionStartIntent intent = RestoreIntent();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot!.HasCommandTracking.ShouldBeTrue();

        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        cut.Instance.Snapshot!.HasCommandTracking.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        commandGateway.SetRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Malformed_original_timestamp_confirms_projection_but_does_not_link_corrective_proof()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new();
        StubTenantQueryGateway queryGateway = new(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, WithOriginalTimestamp(RestoreIntent(), "not-a-timestamp"))
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        cut.Instance.Snapshot!.ProofLink.ShouldBeNull();
        queryGateway.AuditRequests.ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-correction-proof-link']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-correction-refresh']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Rejected_correction_survives_a_parent_re_render_without_re_arming_submit()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        StubTenantCommandGateway commandGateway = new()
        {
            RemoveResultTask = Task.FromResult(TenantCommandSubmissionResult.Rejected(
                "The last global administrator cannot be removed.",
                "LastGlobalAdministrator")),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            TenantAuditSnapshot.Unavailable(new TenantAuditRequest("system"))));
        TenantCorrectionStartIntent intent = RevokeIntent();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Rejected));

        // A parent re-render (for example an audit pager navigation that keeps this panel open)
        // re-passes the same intent with a refreshed projection. The terminal rejection must not reset
        // to a fresh, re-armed preview and must not discard the rejection evidence (AC4/AC8).
        cut.Render(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin", "third-admin")));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Rejected);
        cut.Instance.Snapshot!.RejectionCode.ShouldBe("LastGlobalAdministrator");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        commandGateway.RemoveRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Prevents_duplicate_submission_while_command_is_in_flight()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TaskCompletionSource<TenantCommandSubmissionResult> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway commandGateway = new() { SetResultTask = pending.Task };
        StubTenantQueryGateway queryGateway = new(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"));
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();
        cut.WaitForAssertion(() => commandGateway.SetRequests.Count.ShouldBe(1));
        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        commandGateway.SetRequests.Count.ShouldBe(1);
        pending.SetResult(TenantCommandSubmissionResult.Accepted("message-safe", "tracking-safe"));
        cut.WaitForAssertion(() => commandGateway.StatusHandles.Count.ShouldBe(1));
        commandGateway.SetRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Replacing_grant_panel_during_dispatch_retains_exact_request_and_completion()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var pending = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { SetResultTask = pending.Task };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        TenantCorrectionStartIntent intent = RestoreIntent();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> original = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = original.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        original.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        string messageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        original.Instance.Dispose();

        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.MessageId.ShouldBe(messageId));
        replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.RequestSent);
        replacement.Instance.Snapshot.CorrelationId.ShouldBeNull();
        replacement.Instance.Snapshot.OriginalAuditReference.ShouldBe(intent.OriginalAuditReference);
        commandGateway.SetRequests.Count.ShouldBe(1);

        pending.SetResult(TenantCommandSubmissionResult.Accepted(messageId, "tracking-safe"));
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.SetRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Superseded_accepted_correction_is_immediately_adopted_resumed_and_confirmed()
    {
        var pending = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { SetResultTask = pending.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        pending.SetResult(TenantCommandSubmissionResult.Accepted("message-safe", "tracking-safe"));
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        IncrementPrivateGeneration(cut.Instance, "_operationGeneration");
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed);
        commandGateway.StatusHandles.Count.ShouldBe(1);
        string callerOwnedMessageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        commandGateway.StatusHandles.ShouldAllBe(handle => handle == new TenantCommandTrackingHandle(
            callerOwnedMessageId,
            "tracking-safe",
            GlobalAdministratorGrantPreview.FixedAggregateId));
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        var replacementOwner = new object();
        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
    }

    [Fact]
    public async Task Superseded_grant_with_transport_exception_resumes_same_attempt_and_releases_lease()
    {
        var pending = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { SetResultTask = pending.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        int reads = 0;
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = _ => ++reads < 3
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
        });
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        commandGateway.SetResultTask = Task.FromResult(TenantCommandSubmissionResult.Accepted("message-safe", "tracking-safe"));
        pending.SetException(new HttpRequestException("transport detail"));
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        IncrementPrivateGeneration(cut.Instance, "_operationGeneration");
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        string messageId = cut.Instance.Snapshot!.MessageId.ShouldNotBeNull();
        commandGateway.TrackedMessageIds.ShouldBe([messageId, messageId]);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("failed")]
    public async Task Superseded_terminal_correction_completion_releases_aggregate_lease(string outcome)
    {
        var pending = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { SetResultTask = pending.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        pending.SetResult(outcome == "rejected"
            ? TenantCommandSubmissionResult.Rejected("The correction was rejected.", "RejectedForTest")
            : TenantCommandSubmissionResult.Failed("The correction failed."));
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        IncrementPrivateGeneration(cut.Instance, "_operationGeneration");
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out TenantAggregateCommandLease? replacementLease).ShouldBeTrue();
        replacementLease!.TryAbandonBeforeDispatch(replacementOwner).ShouldBeTrue();
    }

    [Fact]
    public async Task AmbiguousGrantExceptionRetainsLeaseAndSameCommandIdentity()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SetResultTask = Task.FromException<TenantCommandSubmissionResult>(new HttpRequestException("transport detail")),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        int reads = 0;
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = _ => ++reads < 3
                ? Projection("other-admin")
                : ProjectionAfterGrant("admin-user", "other-admin"),
        });
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.RequestSent);
        cut.Instance.Snapshot.IsSubmissionAmbiguous.ShouldBeTrue();
        string retainedMessageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        retainedMessageId.ShouldBe(cut.Instance.Snapshot.MessageId);
        cut.Find("[data-testid='tenants-correction-refresh']").TextContent
            .ShouldContain("same tracked command", Case.Insensitive);
        cut.Markup.ShouldNotContain("transport detail", Case.Insensitive);
        TenantAggregateCommandAdmissionGate admissionGate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var replacementOwner = new object();
        admissionGate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            replacementOwner,
            out _).ShouldBeFalse();

        commandGateway.SetResultTask = Task.FromResult(
            TenantCommandSubmissionResult.Accepted(retainedMessageId, "tracking-safe"));
        await cut.Find("[data-testid='tenants-correction-refresh']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.TrackedMessageIds.ShouldBe([retainedMessageId, retainedMessageId]);
        commandGateway.SetRequests.ShouldAllBe(request => request.UserId == "admin-user");
        admissionGate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task MismatchedGrantAcceptanceRetainsLeaseAndOffersSameIdentityDeliveryRetry()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SetResultTask = Task.FromResult(
                TenantCommandSubmissionResult.Accepted("different-message", "tracking-safe")),
            ReturnConfiguredTrackedIdentity = true,
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        await cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        string callerOwnedMessageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.RequestSent);
        cut.Instance.Snapshot.MessageId.ShouldBe(callerOwnedMessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBeNull();
        cut.Instance.Snapshot.IsSubmissionAmbiguous.ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-refresh']").TextContent
            .ShouldContain("same tracked command", Case.Insensitive);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Fact]
    public void AdoptedAmbiguousGrantRendersItsSameCommandDeliveryRecovery()
    {
        var pendingRetry = new TaskCompletionSource<TenantCommandSubmissionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { SetResultTask = pendingRetry.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        TenantAggregateCommandAdmissionGate gate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var originalOwner = new object();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            originalOwner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(originalOwner).ShouldBeTrue();
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            "admin-user",
            Projection("other-admin"),
            isAuthorized: true);
        lease.TryRetainReconciliation(
            originalOwner,
            new GlobalAdministratorReconciliationState(
                GlobalAdministratorActionKind.Grant,
                "admin-user",
                "message-safe",
                CorrelationId: null,
                TenantCommandLifecycleState.RequestSent,
                preview,
                IsSubmissionAmbiguous: true,
                SafeMessageKey: "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous",
                SafeRecoveryKey: "Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery")
            {
                SourceAuditReference = "event-safe-reference",
                SourceAuditTimestampUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            })
            .ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RestoreIntent())
                .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.WaitForAssertion(() => commandGateway.SetRequests.ShouldHaveSingleItem());
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldContain("ambiguous", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldContain("retained command identity", Case.Insensitive);
        commandGateway.TrackedMessageIds.ShouldHaveSingleItem().ShouldBe("message-safe");
        commandGateway.SetRequests.ShouldHaveSingleItem().UserId.ShouldBe("admin-user");

        GlobalAdministratorCorrectionSnapshot adoptedSnapshot = cut.Instance.Snapshot!;
        pendingRetry.SetResult(TenantCommandSubmissionResult.Ambiguous(
            "message-safe",
            "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"));
        cut.WaitForAssertion(() => ReferenceEquals(cut.Instance.Snapshot, adoptedSnapshot).ShouldBeFalse());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Superseded_status_completion_is_immediately_adopted_resumed_and_confirmed(long committedSequence)
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { StatusResultTask = pendingStatus.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = committedSequence });
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        IncrementPrivateGeneration(cut.Instance, "_operationGeneration");
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        if (committedSequence == 1)
        {
            cut.Instance.Snapshot!.LifecycleState.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        }
        else
        {
            cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed);
        }
        commandGateway.StatusHandles.Count.ShouldBe(committedSequence == 1 ? 1 : 2);
        string callerOwnedMessageId = commandGateway.TrackedMessageIds.ShouldHaveSingleItem();
        commandGateway.StatusHandles.ShouldAllBe(handle => handle == new TenantCommandTrackingHandle(
            callerOwnedMessageId,
            "tracking-safe",
            GlobalAdministratorGrantPreview.FixedAggregateId));
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task Superseded_projection_completion_queued_behind_the_renderer_cannot_confirm_correction()
    {
        var projectionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingProjection = new TaskCompletionSource<GlobalAdministratorsSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"),
            Audit("event-corrective", "GlobalAdministratorSet")));
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.ProjectionRefreshProvider, async () =>
            {
                projectionEntered.SetResult();
                return await pendingProjection.Task;
            }));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await projectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        (Task rendererBlock, TaskCompletionSource releaseRenderer) = await BlockRendererAsync(cut);
        pendingProjection.SetResult(Projection("admin-user", "other-admin"));
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        IncrementPrivateGeneration(cut.Instance, "_operationGeneration");
        releaseRenderer.SetResult();
        await Task.WhenAll(rendererBlock, submit).WaitAsync(TimeSpan.FromSeconds(5));

        cut.Instance.Snapshot!.LifecycleState.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public void Correction_lease_arming_and_component_field_write_are_renderer_guarded()
    {
        string source = File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Audit",
            "GlobalAdministratorCorrectionPanel.razor"));
        int submitStart = source.IndexOf("private async Task SubmitAsync()", StringComparison.Ordinal);
        int submitEnd = source.IndexOf("private async Task RefreshStatusAsync()", submitStart, StringComparison.Ordinal);
        string submit = source[submitStart..submitEnd];
        int assignment = submit.IndexOf("_admissionLease = acquiredLease", StringComparison.Ordinal);
        int callback = submit.LastIndexOf("await InvokeRendererSafelyAsync(() =>", assignment, StringComparison.Ordinal);
        int operationGuard = submit.IndexOf("CanApplyOperation(generation)", callback, StringComparison.Ordinal);
        int dispatchGuard = submit.IndexOf("acquiredLease.TryBeginInitialReconciliationDispatch(", callback, StringComparison.Ordinal);
        int grantBranch = submit.IndexOf(
            "else if (requestSent.ToCompletionReconciliation() is not { } grantDispatchBasis",
            callback,
            StringComparison.Ordinal);

        submitStart.ShouldBeGreaterThan(-1);
        submitEnd.ShouldBeGreaterThan(submitStart);
        callback.ShouldBeGreaterThan(-1);
        operationGuard.ShouldBeInRange(callback, assignment);
        dispatchGuard.ShouldBeInRange(callback, assignment);
        grantBranch.ShouldBeInRange(callback, assignment);
        dispatchGuard.ShouldBeLessThan(grantBranch);
        int grantDispatch = submit.IndexOf(
            "acquiredLease.TryBeginInitialReconciliationDispatch(",
            grantBranch,
            StringComparison.Ordinal);
        grantDispatch.ShouldBeInRange(grantBranch, assignment);
        int grantBasisArgument = submit.IndexOf("grantDispatchBasis,", grantDispatch, StringComparison.Ordinal);
        int grantDispatchClose = submit.IndexOf("out completionToken)", grantDispatch, StringComparison.Ordinal);
        grantDispatchClose.ShouldBeGreaterThan(grantDispatch);
        grantBasisArgument.ShouldBeInRange(grantDispatch, grantDispatchClose);
        submit.ShouldNotContain("TryMarkDispatched(_admissionOwner)");
        submit.ShouldContain("_ = acquiredLease.TryAbandonBeforeDispatch(_admissionOwner);");
        submit.ShouldNotContain("out _admissionLease");
    }

    [Fact]
    public void Close_uses_callback_for_focus_return()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        bool closed = false;

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.OnClose, () => closed = true));

        cut.Find("[data-testid='tenants-correction-close']").Click();

        closed.ShouldBeTrue();
    }

    [Fact]
    public void Unavailable_intent_blocks_submission_and_keeps_original_evidence_visible()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        TenantCorrectionStartIntent intent = RestoreIntent(hasCommandSupport: false);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent));

        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent.ShouldContain("not connected");
        cut.Find("[data-testid='tenants-correction-original-evidence']").TextContent.ShouldContain("event-safe-reference");
    }

    [Fact]
    public async Task SubmittedCorrectionIsAdoptedAndCompletedByGlobalAdministratorsPage()
    {
        bool commandApplied = false;
        var commandGateway = new StubTenantCommandGateway
        {
            Status = TenantCommandStatusResult.Unknown("Status is temporarily unavailable."),
        };
        var queryGateway = new StubTenantQueryGateway(Projection("other-admin"), Audit("proof", "GlobalAdministratorSet"))
        {
            GlobalAdministratorProvider = _ => commandApplied
                ? Projection("admin-user", "other-admin") with { ProjectionVersion = "tenant-sequence:2" }
                : Projection("other-admin"),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        var proofRegistry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(proofRegistry);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> correction = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        correction.Find("[data-testid='tenants-correction-confirm']").Click();
        correction.WaitForAssertion(() => correction.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        correction.Instance.Dispose();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        commandGateway.Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        commandApplied = true;
        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();

        page.WaitForAssertion(() => gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse());
        page.Find("[data-testid='tenants-global-admin-grant-state']").TextContent.ShouldContain("Confirmed");
        GlobalAdministratorReconciliationState completion = proofRegistry.PendingCompletions().ShouldHaveSingleItem();
        completion.SourceAuditReference.ShouldBe("event-safe-reference");
        completion.SourceAuditTimestampUtc.ShouldBe(DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture));
        completion.CommittedEventSequence.ShouldBe(2);
        completion.ConfirmedProjectionEvidence.ShouldNotBeNull().ProjectionVersion.ShouldBe("tenant-sequence:2");
        commandGateway.SetRequests.ShouldHaveSingleItem().UserId.ShouldBe("admin-user");
    }

    [Fact]
    public void RendererReplacementRetainsPositiveGrantEventEvidenceAndReleasesOnQualifiedProjection()
    {
        var commandGateway = new StubTenantCommandGateway
        {
            Status = new TenantCommandStatusResult(
                CommandStatus.Received,
                EventCount: 0,
                HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ProjectionAfterGrant("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet"))
        {
            SimulateFirstReadPreflight = false,
        });
        TenantAggregateCommandAdmissionGate gate =
            Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        var originalOwner = new object();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            originalOwner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease!.TryMarkDispatched(originalOwner).ShouldBeTrue();
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            "admin-user",
            Projection("other-admin"),
            isAuthorized: true);
        lease.TryRetainReconciliation(
            originalOwner,
            new GlobalAdministratorReconciliationState(
                GlobalAdministratorActionKind.Grant,
                "admin-user",
                "message-safe",
                "tracking-safe",
                TenantCommandLifecycleState.ProjectionPending,
                preview,
                HasCommandEventEvidence: true)
            {
                CommittedEventSequence = 2,
                SubmittedAtUtc = DateTimeOffset.Parse("2026-06-01T10:01:00Z", CultureInfo.InvariantCulture),
                SourceAuditReference = "event-safe-reference",
                SourceAuditTimestampUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            }).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement =
            Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
                .Add(component => component.Intent, RestoreIntent())
                .Add(component => component.CurrentProjection, Projection("other-admin")));

        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState
            .ShouldBe(TenantCommandLifecycleState.Confirmed));
        replacement.Instance.Snapshot!.HasCommandEventEvidence.ShouldBeTrue();
        replacement.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        replacement.Find("[data-testid='tenants-correction-proof-link']");
        commandGateway.StatusHandles.ShouldHaveSingleItem().MessageId.ShouldBe("message-safe");
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task Page_origin_removal_cannot_be_adopted_as_an_audit_correction()
    {
        bool commandApplied = false;
        var commandGateway = new StubTenantCommandGateway
        {
            Status = TenantCommandStatusResult.Unknown("Status is temporarily unavailable."),
        };
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorRemoved"))
        {
            AutoAdvanceProjectionVersion = false,
            GlobalAdministratorProvider = _ => commandApplied
                ? Projection("other-admin") with { ProjectionVersion = "tenant-sequence:3" }
                : Projection("admin-user", "other-admin"),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();

        IRenderedComponent<GlobalAdministratorsPage> page = Render<GlobalAdministratorsPage>();
        page.Find("[data-testid='tenants-global-admin-remove'][data-user-id='admin-user']").Click();
        page.WaitForElement("[data-testid='tenants-global-admin-remove-preview']");
        page.Find("[data-testid='tenants-global-admin-remove-acknowledge']").Change("admin-user");
        page.WaitForAssertion(() => page.Find("[data-testid='tenants-global-admin-remove-submit']")
            .HasAttribute("disabled").ShouldBeFalse());
        page.Find("[data-testid='tenants-global-admin-remove-submit']").Click();
        page.WaitForAssertion(() => page.Find("[data-testid='tenants-global-admin-remove-state']").TextContent.ShouldContain("UnableToVerify"));
        await page.InvokeAsync(async () => await page.Instance.DisposeAsync());
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        int priorStatusReads = commandGateway.StatusHandles.Count;

        commandGateway.Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        commandApplied = true;
        IRenderedComponent<GlobalAdministratorCorrectionPanel> correction = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        correction.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.AlreadyApplied);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        commandGateway.StatusHandles.Count.ShouldBe(priorStatusReads);
        commandGateway.RemoveRequests.ShouldHaveSingleItem().UserId.ShouldBe("admin-user");
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("remove")]
    public void UnavailableQueryGatewayBlocksBothCorrectionActionsWithoutDispatch(string action)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new UnavailableTenantQueryGateway());
        TenantCorrectionStartIntent intent = action == "remove" ? RevokeIntent() : RestoreIntent();

        GlobalAdministratorsSnapshot projection = action == "remove"
            ? Projection("admin-user", "other-admin")
            : Projection("other-admin");
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));

        AngleSharp.Dom.IElement confirm = cut.Find("[data-testid='tenants-correction-confirm']");
        confirm.HasAttribute("disabled").ShouldBeTrue();
        confirm.GetAttribute("aria-describedby").ShouldBe(
            "tenants-correction-unavailable tenants-correction-unavailable-recovery");
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldContain("requery support is missing", Case.Insensitive);
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent
            .ShouldContain("Restore dispatch, status, and requery support", Case.Insensitive);
        commandGateway.SetRequests.ShouldBeEmpty();
        commandGateway.RemoveRequests.ShouldBeEmpty();
        commandGateway.StatusHandles.ShouldBeEmpty();
    }

    [Fact]
    public void CorrectiveProofFailurePreservesConfirmedProjectionTruth()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var proofRegistry = new GlobalAdministratorCorrectionProofRegistry();
        Services.AddSingleton(proofRegistry);
        var commandGateway = new StubTenantCommandGateway();
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("unused", "GlobalAdministratorSet"))
        {
            AuditException = new HttpRequestException("audit transport detail"),
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        cut.Find("[data-testid='tenants-correction-confirm']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        GlobalAdministratorReconciliationState pending = proofRegistry.PendingCompletions().ShouldHaveSingleItem();
        pending.ConfirmedProjectionEvidence.ShouldNotBeNull().ProjectionVersion.ShouldBe("tenant-sequence:2");
        cut.Find("[data-testid='tenants-correction-state']").TextContent
            .ShouldContain("Projection confirms", Case.Insensitive);
        cut.Markup.ShouldNotContain("audit transport detail", Case.Insensitive);
    }

    [Theory]
    [InlineData("grant", "gateway-dispatch")]
    [InlineData("grant", "gateway-status")]
    [InlineData("grant", "composition-dispatch")]
    [InlineData("grant", "composition-status")]
    [InlineData("grant", "composition-requery")]
    [InlineData("remove", "gateway-dispatch")]
    [InlineData("remove", "gateway-status")]
    [InlineData("remove", "composition-dispatch")]
    [InlineData("remove", "composition-status")]
    [InlineData("remove", "composition-requery")]
    public void EachDeclaredLifecycleSeamBlocksBothActionsIndependently(string action, string missingSeam)
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway
        {
            SupportsGlobalAdministratorDispatch = missingSeam != "gateway-dispatch",
            SupportsCommandStatusLookup = missingSeam != "gateway-status",
        };
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition
        {
            IsGlobalAdministratorDispatchConnected = missingSeam != "composition-dispatch",
            IsGlobalAdministratorStatusConnected = missingSeam != "composition-status",
            IsGlobalAdministratorRequeryConnected = missingSeam != "composition-requery",
        });
        TenantCorrectionStartIntent intent = action == "remove" ? RevokeIntent() : RestoreIntent();
        GlobalAdministratorsSnapshot projection = action == "remove"
            ? Projection("admin-user", "other-admin")
            : Projection("other-admin");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));

        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent
            .ShouldContain("support is missing", Case.Insensitive);
        commandGateway.SetRequests.ShouldBeEmpty();
        commandGateway.RemoveRequests.ShouldBeEmpty();
        commandGateway.StatusHandles.ShouldBeEmpty();
    }

    [Fact]
    public void MismatchingCorrectionLeavesRetainedAttemptForMatchingReplacement()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin") with { ProjectionVersion = "tenant-sequence:2" },
            Audit("proof", "GlobalAdministratorRemoved"))
        {
            SimulateFirstReadPreflight = false,
        });
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        object originalOwner = new();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            originalOwner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease.ShouldNotBeNull();
        lease.TryMarkDispatched(originalOwner).ShouldBeTrue();
        lease.TryRetainReconciliation(
            originalOwner,
            new(
                GlobalAdministratorActionKind.Remove,
                "admin-user",
                "message-safe",
                "tracking-safe",
                TenantCommandLifecycleState.Accepted,
                RemovePreview: GlobalAdministratorRemovePreview.Create(
                    "admin-user",
                    "operator",
                    Projection("admin-user", "other-admin"),
                    isAuthorized: true))
            {
                SourceAuditReference = "event-safe-reference",
                SourceAuditTimestampUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            }).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> mismatch = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        commandGateway.StatusHandles.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        mismatch.Dispose();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> match = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));

        match.WaitForAssertion(() => match.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.StatusHandles.ShouldHaveSingleItem().ShouldBe(
            new TenantCommandTrackingHandle(
                "message-safe",
                "tracking-safe",
                GlobalAdministratorGrantPreview.FixedAggregateId));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void Same_target_correction_from_another_audit_row_cannot_adopt_retained_attempt()
    {
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("other-admin"), Audit("proof", "GlobalAdministratorSet")));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        SeedRetainedAttempt(gate, GlobalAdministratorActionKind.Grant, "admin-user");

        TenantCorrectionStartIntent wrongIntent = RestoreIntent();
        Dictionary<string, string> wrongInputs = new(wrongIntent.RequiredPreviewInputs, StringComparer.Ordinal)
        {
            ["originalAuditReference"] = "event-other-source",
        };
        wrongIntent = wrongIntent with
        {
            OriginalAuditReference = "event-other-source",
            RequiredPreviewInputs = wrongInputs,
        };
        IRenderedComponent<GlobalAdministratorCorrectionPanel> wrong = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, wrongIntent)
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        wrong.Instance.Snapshot!.MessageId.ShouldBeNull();
        commandGateway.StatusHandles.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        IRenderedComponent<GlobalAdministratorCorrectionPanel> matching = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        matching.WaitForAssertion(() => matching.Instance.Snapshot!.MessageId.ShouldBe("message-safe"));
        wrong.Instance.Snapshot!.MessageId.ShouldBeNull();
    }

    [Theory]
    [InlineData("viewport")]
    [InlineData("gateway-dispatch")]
    [InlineData("gateway-status")]
    [InlineData("composition-dispatch")]
    [InlineData("composition-status")]
    [InlineData("composition-requery")]
    public async Task StatusGateChangePreventsProjectionIoAndLeavesAttemptAdoptable(string changedGate)
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { StatusResultTask = pendingStatus.Task };
        var composition = new StubTenantsBffComposition();
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet"));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        SetLiveGate(changedGate, enabled: false, commandGateway, composition, viewport);
        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 });
        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        queryGateway.GlobalAdminRequests.Count.ShouldBe(1);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        cut.Instance.Dispose();

        SetLiveGate(changedGate, enabled: true, commandGateway, composition, viewport);
        commandGateway.StatusResultTask = null;
        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        replacement.Find("[data-testid='tenants-correction-proof-link']");
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("viewport")]
    [InlineData("gateway-dispatch")]
    [InlineData("gateway-status")]
    [InlineData("composition-dispatch")]
    [InlineData("composition-status")]
    [InlineData("composition-requery")]
    public void LiveGateChangeAfterOuterSubmitCheckPreventsDispatch(string changedGate)
    {
        var commandGateway = new StubTenantCommandGateway();
        var composition = new StubTenantsBffComposition();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();

        bool changed = false;
        EventHandler changeAfterAcquisition = (_, _) =>
        {
            if (!changed)
            {
                changed = true;
                SetLiveGate(changedGate, enabled: false, commandGateway, composition, viewport);
            }
        };
        gate.StateChanged += changeAfterAcquisition;
        try
        {
            cut.Find("[data-testid='tenants-correction-confirm']").Click();
        }
        finally
        {
            gate.StateChanged -= changeAfterAcquisition;
        }

        changed.ShouldBeTrue();
        commandGateway.SetRequests.ShouldBeEmpty();
        commandGateway.RemoveRequests.ShouldBeEmpty();
        commandGateway.StatusHandles.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void RetainedAttemptWaitsForConcreteRequeryAndRetriesAdoptionAfterSupportRestoration()
    {
        var commandGateway = new StubTenantCommandGateway();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new UnavailableTenantQueryGateway());
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        SeedRetainedAttempt(gate, GlobalAdministratorActionKind.Grant, "admin-user");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        commandGateway.StatusHandles.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        cut.Render(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.ProjectionRefreshProvider, () =>
                Task.FromResult<GlobalAdministratorsSnapshot?>(ProjectionAfterGrant("admin-user", "other-admin"))));

        cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.StatusHandles.ShouldHaveSingleItem();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public async Task AdoptedCorrelatedReconciliationDefersStatusLookupUntilHeldRefreshReleases()
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway
        {
            StatusResultTask = pendingStatus.Task,
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet")));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        SeedRetainedAttempt(gate, GlobalAdministratorActionKind.Grant, "admin-user");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(
                component => component.ProjectionRefreshProvider,
                () => Task.FromResult<GlobalAdministratorsSnapshot?>(Projection("other-admin"))));

        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.StatusHandles.ShouldHaveSingleItem();

        TenantAggregateCommandLease lease = PrivateField<TenantAggregateCommandLease>(cut.Instance, "_admissionLease");
        object owner = PrivateField<object>(cut.Instance, "_admissionOwner");
        GlobalAdministratorGrantPreview preview = GlobalAdministratorGrantPreview.Create(
            "admin-user",
            Projection("other-admin"),
            isAuthorized: true);
        lease.TryAdvanceReconciliation(
            owner,
            new(
                GlobalAdministratorActionKind.Grant,
                "admin-user",
                "message-safe",
                "tracking-safe",
                TenantCommandLifecycleState.ProjectionPending,
                preview,
                HasCommandEventEvidence: true)
            {
                SourceAuditReference = "event-safe-reference",
                SourceAuditTimestampUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            }).ShouldBeTrue();
        Task notify = (Task)typeof(GlobalAdministratorCorrectionPanel)
            .GetMethod(
                "RenderAvailabilityChangeAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(cut.Instance, null)!;
        await notify.WaitAsync(TimeSpan.FromSeconds(5));
        commandGateway.StatusHandles.ShouldHaveSingleItem();

        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        await WaitUntilAsync(() => commandGateway.StatusHandles.Count >= 2, TimeSpan.FromSeconds(5));
        commandGateway.StatusHandles.Count.ShouldBe(2);
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("changed")]
    public async Task NullOrChangedIntentReturnsAcceptedTrackingToMatchingReplacement(string intentChange)
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { StatusResultTask = pendingStatus.Task };
        var queryGateway = new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet"));
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(queryGateway);
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(parameters => parameters
            .Add(component => component.Intent, intentChange == "null" ? null : RevokeIntent())
            .Add(component => component.CurrentProjection, Projection("admin-user", "other-admin")));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();

        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 });
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
        queryGateway.GlobalAdminRequests.Count.ShouldBe(1);
        cut.Dispose();

        commandGateway.StatusResultTask = null;
        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    [Fact]
    public void FailingAuthenticationCannotClaimRetainedAttemptAndAuthorizedReplacementCompletesIt()
    {
        var commandGateway = new StubTenantCommandGateway();
        var authentication = new StubAuthenticationStateProvider
        {
            AuthenticationStateTask = Task.FromException<AuthenticationState>(new InvalidOperationException("auth detail")),
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            ProjectionAfterGrant("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet"))
        {
            SimulateFirstReadPreflight = false,
        });
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        SeedRetainedAttempt(gate, GlobalAdministratorActionKind.Grant, "admin-user");

        IRenderedComponent<GlobalAdministratorCorrectionPanel> hidden = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        hidden.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty();
        commandGateway.StatusHandles.ShouldBeEmpty();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        hidden.Dispose();

        authentication.AuthenticationStateTask = Task.FromResult(StubAuthenticationStateProvider.AuthenticatedState());
        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
        replacement.Markup.ShouldNotContain("auth detail", Case.Insensitive);
    }

    [Fact]
    public async Task SupersededAuthorizationCompletionCannotOverwriteNewerAuthorizedState()
    {
        var firstAuthorization = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAuthorization = new TaskCompletionSource<TenantLifecycleAuthorizationReflectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int resolution = 0;
        var composition = new StubTenantsBffComposition
        {
            AuthorizationResolver = _ => Interlocked.Increment(ref resolution) switch
            {
                1 => ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized),
                2 => AwaitAuthorization(firstAuthorization.Task, firstEntered),
                3 => AwaitAuthorization(secondAuthorization.Task, secondEntered),
                _ => ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized),
            },
        };
        var authentication = new StubAuthenticationStateProvider();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        cut.Find("[data-testid='tenants-correction-panel']");

        authentication.Publish(Task.FromResult(StubAuthenticationStateProvider.AuthenticatedState()));
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-correction-panel']").ShouldBeEmpty());
        authentication.Publish(Task.FromResult(StubAuthenticationStateProvider.AuthenticatedState()));
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        secondAuthorization.SetResult(TenantLifecycleAuthorizationReflectionState.Authorized);
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-correction-panel']"));
        firstAuthorization.SetResult(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await Task.Yield();
        }

        cut.Find("[data-testid='tenants-correction-panel']");
        cut.Find("[data-testid='tenants-correction-confirm']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Theory]
    [InlineData("already-applied", TenantCommandLifecycleState.AlreadyApplied, "Close this correction")]
    [InlineData("confirmed", TenantCommandLifecycleState.Confirmed, "Keep the confirmed projection")]
    [InlineData("rejected", TenantCommandLifecycleState.Rejected, "Review the rejection")]
    [InlineData("failed", TenantCommandLifecycleState.Failed, "Refresh current evidence")]
    [InlineData("unavailable", TenantCommandLifecycleState.UnableToVerify, "Restore the blocked authorization")]
    public void DisabledLifecycleStatesRenderTruthfulAssociatedReasonAndRecovery(
        string scenario,
        TenantCommandLifecycleState expectedState,
        string expectedRecovery)
    {
        var commandGateway = new StubTenantCommandGateway
        {
            SetResultTask = scenario switch
            {
                "rejected" => Task.FromResult(TenantCommandSubmissionResult.Rejected("Rejected safely.", "RejectedForTest")),
                "failed" => Task.FromResult(TenantCommandSubmissionResult.Failed("Failed safely.")),
                _ => null,
            },
        };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet")));
        TenantCorrectionStartIntent intent = scenario == "unavailable" ? RestoreIntent(hasCommandSupport: false) : RestoreIntent();
        GlobalAdministratorsSnapshot projection = scenario == "already-applied"
            ? Projection("admin-user", "other-admin")
            : Projection("other-admin");
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, intent)
            .Add(component => component.CurrentProjection, projection));

        if (scenario is "confirmed" or "rejected" or "failed")
        {
            cut.Find("[data-testid='tenants-correction-confirm']").Click();
            cut.WaitForAssertion(() => cut.Instance.Snapshot!.LifecycleState.ShouldBe(expectedState));
        }
        else
        {
            cut.Instance.Snapshot!.LifecycleState.ShouldBe(expectedState);
        }

        AngleSharp.Dom.IElement confirm = cut.Find("[data-testid='tenants-correction-confirm']");
        confirm.HasAttribute("disabled").ShouldBeTrue();
        confirm.GetAttribute("aria-describedby").ShouldBe(
            "tenants-correction-unavailable tenants-correction-unavailable-recovery");
        cut.Find("[data-testid='tenants-correction-unavailable-reason']").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent.ShouldContain(expectedRecovery);
    }

    [Fact]
    public async Task TrackedRefreshRefusalRendersAssociatedLiveRecovery()
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { StatusResultTask = pendingStatus.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        TenantHighImpactViewportObservation viewport = Services.GetRequiredService<TenantHighImpactViewportObservation>();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewport.Observe(ViewportTier.Phone);

        cut.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement refresh = cut.Find("[data-testid='tenants-correction-refresh']");
            refresh.HasAttribute("disabled").ShouldBeTrue();
            refresh.GetAttribute("aria-describedby").ShouldBe(
                "tenants-correction-unavailable tenants-correction-unavailable-recovery");
            cut.Find("[data-testid='tenants-correction-unavailable-recovery']").TextContent.ShouldNotBeNullOrWhiteSpace();
        });

        cut.Dispose();
        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 });
        await submit.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("close")]
    [InlineData("cancel")]
    [InlineData("escape")]
    public async Task NonterminalCloseInteractionsRemainLockedAndReplacementCanReconcile(string interaction)
    {
        var pendingStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandGateway = new StubTenantCommandGateway { StatusResultTask = pendingStatus.Task };
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantQueryGateway>(new StubTenantQueryGateway(
            Projection("admin-user", "other-admin"),
            Audit("proof", "GlobalAdministratorSet")));
        TenantAggregateCommandAdmissionGate gate = Services.GetRequiredService<TenantAggregateCommandAdmissionGate>();
        bool closed = false;
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin"))
            .Add(component => component.OnClose, () => closed = true));

        Task submit = cut.Find("[data-testid='tenants-correction-confirm']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await commandGateway.StatusEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (interaction == "escape")
        {
            cut.Find("[data-testid='tenants-correction-panel']")
                .KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            cut.Find($"[data-testid='tenants-correction-{interaction}']").Click();
        }

        closed.ShouldBeFalse();
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeTrue();
        cut.Instance.Dispose();
        IRenderedComponent<GlobalAdministratorCorrectionPanel> replacement = Render<GlobalAdministratorCorrectionPanel>(parameters => parameters
            .Add(component => component.Intent, RestoreIntent())
            .Add(component => component.CurrentProjection, Projection("other-admin")));
        pendingStatus.SetResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 });
        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.LifecycleState.ShouldBe(TenantCommandLifecycleState.Confirmed));
        replacement.WaitForAssertion(() => replacement.Instance.Snapshot!.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable));
        replacement.Find("[data-testid='tenants-correction-proof-link']");
        gate.IsLocked(TenantCommandAggregateLock.ForGlobalAdministrators()).ShouldBeFalse();
    }

    private static async ValueTask<TenantLifecycleAuthorizationReflectionState> AwaitAuthorization(
        Task<TenantLifecycleAuthorizationReflectionState> authorization,
        TaskCompletionSource entered)
    {
        entered.TrySetResult();
        return await authorization.ConfigureAwait(false);
    }

    private static void SeedRetainedAttempt(
        TenantAggregateCommandAdmissionGate gate,
        GlobalAdministratorActionKind actionKind,
        string targetUserId)
    {
        object owner = new();
        gate.TryAcquireLease(
            TenantCommandAggregateLock.ForGlobalAdministrators(),
            owner,
            out TenantAggregateCommandLease? lease).ShouldBeTrue();
        lease.ShouldNotBeNull();
        lease.TryMarkDispatched(owner).ShouldBeTrue();
        GlobalAdministratorGrantPreview? preview = actionKind is GlobalAdministratorActionKind.Grant
            ? GlobalAdministratorGrantPreview.Create(
                targetUserId,
                Projection("other-admin"),
                isAuthorized: true)
            : null;
        lease.TryRetainReconciliation(
            owner,
            new(
                actionKind,
                targetUserId,
                "message-safe",
                "tracking-safe",
                TenantCommandLifecycleState.Accepted,
                preview)
            {
                SourceAuditReference = "event-safe-reference",
                SourceAuditTimestampUtc = DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            }).ShouldBeTrue();
    }

    private static void SetLiveGate(
        string gate,
        bool enabled,
        StubTenantCommandGateway commandGateway,
        StubTenantsBffComposition composition,
        TenantHighImpactViewportObservation viewport)
    {
        switch (gate)
        {
            case "viewport":
                viewport.Observe(enabled ? ViewportTier.Desktop : ViewportTier.Phone);
                break;
            case "gateway-dispatch":
                commandGateway.SupportsGlobalAdministratorDispatch = enabled;
                break;
            case "gateway-status":
                commandGateway.SupportsCommandStatusLookup = enabled;
                break;
            case "composition-dispatch":
                composition.IsGlobalAdministratorDispatchConnected = enabled;
                break;
            case "composition-status":
                composition.IsGlobalAdministratorStatusConnected = enabled;
                break;
            case "composition-requery":
                composition.IsGlobalAdministratorRequeryConnected = enabled;
                break;
            default:
                throw new InvalidOperationException($"Unknown live gate '{gate}'.");
        }
    }

    private static TenantCorrectionStartIntent RestoreIntent(bool hasCommandSupport = true)
        => TenantCorrectionStartIntent.Evaluate(Context("GlobalAdministratorRemoved", hasCommandSupport));

    private static TenantCorrectionStartIntent RevokeIntent(bool hasCommandSupport = true)
        => TenantCorrectionStartIntent.Evaluate(Context("GlobalAdministratorSet", hasCommandSupport));

    private static TenantCorrectionStartIntent WithOriginalTimestamp(TenantCorrectionStartIntent intent, string value)
    {
        Dictionary<string, string> inputs = new(intent.RequiredPreviewInputs, StringComparer.Ordinal)
        {
            ["originalTimestamp"] = value,
        };

        return intent with { RequiredPreviewInputs = inputs };
    }

    private static TenantCorrectionStartContext Context(string eventType, bool hasCommandSupport)
        => new(
            TenantAuditReceipt.FromRow(Row(eventType)),
            Row(eventType),
            IsAuthorized: true,
            HasCurrentProjectionSnapshot: true,
            CurrentProjectionSnapshotReference: "Current global administrator projection is available.",
            HasTenantCommandSupport: false,
            HasGlobalAdministratorCommandSupport: hasCommandSupport);

    private static TenantAuditRow Row(string eventType)
        => new(
            "event-safe-reference",
            eventType,
            AuditEventCategory.Access,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:00:00Z", CultureInfo.InvariantCulture),
            "system",
            "admin-user",
            "system",
            eventType,
            "userId: admin-user",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked,
            new TenantAuditNarrative(UserId: "admin-user"));

    private static GlobalAdministratorsSnapshot Projection(params string[] userIds)
        => ProjectionPage(userIds, nextCursor: null, hasMore: false) with
        {
            IsCompleteEvidence = true,
        };

    private static GlobalAdministratorsSnapshot ProjectionAfterGrant(params string[] userIds)
        => Projection(userIds) with { ProjectionVersion = "tenant-sequence:2" };

    private static GlobalAdministratorsSnapshot ProjectionPage(
        IReadOnlyList<string> userIds,
        string? nextCursor,
        bool hasMore)
        => GlobalAdministratorsSnapshot.Ready(
            userIds.Select(userId => new GlobalAdministratorRow(
                userId,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)).ToArray(),
            nextCursor,
            hasMore,
            eTag: "\"ga-etag\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "tenant-sequence:1",
        };

    private static TenantAuditSnapshot Audit(string eventReference, string eventType)
        => TenantAuditSnapshot.Ready(
            [CorrectiveRow(eventReference, eventType)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"audit-etag\"",
            freshness: ReadModelFreshnessState.Current,
            request: new TenantAuditRequest("system")) with
        {
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "tenant-sequence:2",
        };

    private static TenantAuditRow CorrectiveRow(string eventReference, string eventType)
        => new(
            eventReference,
            eventType,
            AuditEventCategory.Access,
            "actor-user",
            DateTimeOffset.Parse("2026-06-01T10:05:00Z", CultureInfo.InvariantCulture),
            "system",
            "admin-user",
            "system",
            eventType,
            "userId: admin-user",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked,
            new TenantAuditNarrative(UserId: "admin-user"))
        {
            CommandCorrelationId = "tracking-safe",
            AggregateSequence = 2,
        };

    private static T PrivateField<T>(object instance, string name)
    {
        object? value = typeof(GlobalAdministratorCorrectionPanel)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(instance);
        value.ShouldNotBeNull();
        return (T)value;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellation.Token).ConfigureAwait(true);
        }
    }

    private static void IncrementPrivateGeneration(GlobalAdministratorCorrectionPanel instance, string name)
    {
        System.Reflection.FieldInfo field = typeof(GlobalAdministratorCorrectionPanel)
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(instance, (long)field.GetValue(instance)! + 1);
    }

    private static async Task<(Task RendererBlock, TaskCompletionSource ReleaseRenderer)> BlockRendererAsync(
        IRenderedComponent<GlobalAdministratorCorrectionPanel> cut)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task block = Task.Run(() => cut.InvokeAsync(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return (block, release);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => DateTimeOffset.Parse("2026-06-01T10:01:00Z", CultureInfo.InvariantCulture);
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsGlobalAdministratorDispatch { get; set; } = true;

        public bool SupportsTrackedGlobalAdministratorDispatch { get; set; } = true;

        public bool SupportsTrackedGlobalAdministratorRemoveDispatch { get; set; } = true;

        public bool SupportsCommandStatusLookup { get; set; } = true;

        public List<SetGlobalAdministrator> SetRequests { get; } = [];

        public List<string> TrackedMessageIds { get; } = [];

        public List<RemoveGlobalAdministrator> RemoveRequests { get; } = [];

        public List<TenantCommandTrackingHandle> StatusHandles { get; } = [];

        public Task<TenantCommandSubmissionResult>? SetResultTask { get; set; }

        public bool ReturnConfiguredTrackedIdentity { get; init; }

        public Task<TenantCommandSubmissionResult>? RemoveResultTask { get; set; }

        public TenantCommandStatusResult Status { get; set; }
            = new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };

        public Task<TenantCommandStatusResult>? StatusResultTask { get; set; }

        public TaskCompletionSource StatusEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource RemoveSubmissionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TenantCommandSubmissionResult> SetGlobalAdministratorAsync(
            SetGlobalAdministrator request,
            CancellationToken cancellationToken = default)
        {
            SetRequests.Add(request);
            return SetResultTask ?? Task.FromResult(TenantCommandSubmissionResult.Accepted("message-safe", "tracking-safe"));
        }

        public async Task<TenantCommandSubmissionResult> SetGlobalAdministratorTrackedAsync(
            SetGlobalAdministrator request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            SetRequests.Add(request);
            TrackedMessageIds.Add(messageId);
            TenantCommandSubmissionResult result = SetResultTask is null
                ? TenantCommandSubmissionResult.Accepted(messageId, "tracking-safe")
                : await SetResultTask.ConfigureAwait(false);
            return ReturnConfiguredTrackedIdentity ? result : result with { MessageId = messageId };
        }

        public Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorAsync(
            RemoveGlobalAdministrator request,
            CancellationToken cancellationToken = default)
        {
            RemoveRequests.Add(request);
            return RemoveResultTask ?? Task.FromResult(TenantCommandSubmissionResult.Accepted("message-safe", "tracking-safe"));
        }

        public async Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorTrackedAsync(
            RemoveGlobalAdministrator request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            RemoveRequests.Add(request);
            TrackedMessageIds.Add(messageId);
            _ = RemoveSubmissionEntered.TrySetResult();
            TenantCommandSubmissionResult result = RemoveResultTask is null
                ? TenantCommandSubmissionResult.Accepted(messageId, "tracking-safe")
                : await RemoveResultTask.ConfigureAwait(false);
            return ReturnConfiguredTrackedIdentity ? result : result with { MessageId = messageId };
        }

        public async Task<TenantCommandStatusResult> GetStatusAsync(
            TenantCommandTrackingHandle handle,
            CancellationToken cancellationToken = default)
        {
            StatusHandles.Add(handle);
            if (StatusResultTask is not null)
            {
                _ = StatusEntered.TrySetResult();
                return await StatusResultTask.ConfigureAwait(false);
            }

            return Status;
        }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        public Task<AuthenticationState> AuthenticationStateTask { get; set; }
            = Task.FromResult(AuthenticatedState());

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => AuthenticationStateTask;

        public void Publish(Task<AuthenticationState> authenticationStateTask)
        {
            AuthenticationStateTask = authenticationStateTask;
            NotifyAuthenticationStateChanged(authenticationStateTask);
        }

        public static AuthenticationState AuthenticatedState()
            => new(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "test")));
    }

    private sealed class StubTenantsBffComposition : ITenantsBffComposition
    {
        public bool IsReadSurfaceConnected => true;

        public bool IsCommandSurfaceConnected => true;

        public bool IsGlobalAdministratorDispatchConnected { get; set; } = true;

        public bool IsGlobalAdministratorStatusConnected { get; set; } = true;

        public bool IsGlobalAdministratorRequeryConnected { get; set; } = true;

        public bool IsGlobalAdministratorGrantPreviewReady => true;

        public bool IsGlobalAdministratorRemovePreviewReady => true;

        public string CallerSubject { get; set; } = "operator";

        public TenantLifecycleAuthorizationReflectionState SynchronousReflection { get; init; }
            = TenantLifecycleAuthorizationReflectionState.Authorized;

        public Task<TenantLifecycleAuthorizationReflectionState>? AuthorizationTask { get; init; }

        public Func<CancellationToken, ValueTask<TenantLifecycleAuthorizationReflectionState>>? AuthorizationResolver { get; init; }

        public Task<GlobalAdministratorRemovePreview>? RemovePreviewTask { get; init; }

        public TenantLifecycleAuthorizationReflectionState GlobalAdministratorsAuthorizationReflection
            => SynchronousReflection;

        public ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveGlobalAdministratorsAuthorizationAsync(
            CancellationToken cancellationToken = default)
            => AuthorizationResolver is not null
                ? AuthorizationResolver(cancellationToken)
                : AuthorizationTask is null
                ? ValueTask.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized)
                : new ValueTask<TenantLifecycleAuthorizationReflectionState>(AuthorizationTask);

        public ValueTask<GlobalAdministratorGrantPreview> ComposeGlobalAdministratorGrantPreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(GlobalAdministratorGrantPreview.Create(
                targetUserId,
                completeSnapshot,
                isAuthorized: true));

        public ValueTask<GlobalAdministratorRemovePreview> ComposeGlobalAdministratorRemovePreviewAsync(
            string targetUserId,
            GlobalAdministratorsSnapshot completeSnapshot,
            CancellationToken cancellationToken = default)
            => RemovePreviewTask is not null
                ? new ValueTask<GlobalAdministratorRemovePreview>(RemovePreviewTask)
                : ValueTask.FromResult(GlobalAdministratorRemovePreview.Create(
                    targetUserId,
                    CallerSubject,
                    completeSnapshot,
                    isAuthorized: true));
    }

    private sealed class StubTenantQueryGateway(GlobalAdministratorsSnapshot projection, TenantAuditSnapshot audit) : ITenantQueryGateway
    {
        /// <summary>
        /// Explicit because <c>ITenantQueryGateway.GetTenantUsersAsync</c> is no longer a default interface
        /// method. This stub returns <c>Unavailable</c> deliberately: these tests do not exercise the member
        /// read, and an unavailable member surface is the correct fail-closed shape for them. Note this is
        /// the same value the removed default interface method returned, so a member-read regression is NOT
        /// caught here -- it is caught by the member-specific suites in
        /// <c>TenantDetailSurfaceTests</c>. (An earlier version of this remark claimed the opposite.)
        /// </summary>
        public Task<TenantUsersSnapshot> GetTenantUsersAsync(
            TenantUsersRequest request,
            TenantUsersSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(TenantUsersSnapshot.Unavailable(request.TenantId));
        }

        public List<GlobalAdministratorsRequest> GlobalAdminRequests { get; } = [];

        public List<TenantAuditRequest> AuditRequests { get; } = [];

        public Func<GlobalAdministratorsRequest, GlobalAdministratorsSnapshot>? GlobalAdministratorProvider { get; init; }

        public bool AutoAdvanceProjectionVersion { get; init; } = true;

        public bool SimulateFirstReadPreflight { get; init; } = true;

        public Exception? AuditException { get; init; }

        public Func<TenantAuditRequest, TenantAuditSnapshot>? AuditProvider { get; init; }

        public Func<TenantAuditRequest, Task<TenantAuditSnapshot>>? AsyncAuditProvider { get; init; }

        public Task<GlobalAdministratorsSnapshot> GetGlobalAdministratorsAsync(
            GlobalAdministratorsRequest request,
            GlobalAdministratorsSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            GlobalAdminRequests.Add(request);
            GlobalAdministratorsSnapshot result = GlobalAdministratorProvider?.Invoke(request) ?? projection;
            if (SimulateFirstReadPreflight && GlobalAdministratorProvider is null && GlobalAdminRequests.Count == 1
                && audit.Rows.SingleOrDefault() is { } corrective)
            {
                if (corrective.EventType is "GlobalAdministratorSet"
                    && result.Rows.Any(row => string.Equals(row.UserId, corrective.Target, StringComparison.Ordinal)))
                {
                    result = result with
                    {
                        Rows = result.Rows.Where(row => !string.Equals(row.UserId, corrective.Target, StringComparison.Ordinal)).ToArray(),
                    };
                }
                else if (corrective.EventType is "GlobalAdministratorRemoved"
                    && !result.Rows.Any(row => string.Equals(row.UserId, corrective.Target, StringComparison.Ordinal)))
                {
                    result = result with
                    {
                        Rows = [.. result.Rows, new GlobalAdministratorRow(
                            corrective.Target, ReadModelFreshnessState.Current, ProjectionLifecycleState.Current)],
                    };
                }
            }
            return Task.FromResult(result with
            {
                RequestCursor = request.Cursor,
                RequestPageSize = request.PageSize,
                ProjectionVersion = AutoAdvanceProjectionVersion && GlobalAdminRequests.Count > 1 && result.ProjectionVersion is "tenant-sequence:1"
                    ? "tenant-sequence:2" : result.ProjectionVersion,
            });
        }

        public Task<(GlobalAdministratorAuditProof? Proof, bool Complete)> FindGlobalAdministratorCorrectionProofAsync(
            string correlationId,
            long committedEventSequence,
            string targetUserId,
            string correctiveEventType,
            DateTimeOffset originalTimestamp,
            DateTimeOffset submittedAtUtc,
            CancellationToken cancellationToken = default)
            => GlobalAdministratorAuditProofLookup.FindAsync(
                (request, token) => GetTenantAuditAsync(request, null, token),
                correlationId,
                committedEventSequence,
                targetUserId,
                correctiveEventType,
                originalTimestamp,
                submittedAtUtc,
                cancellationToken);

        public Task<TenantAuditSnapshot> GetTenantAuditAsync(
            TenantAuditRequest request,
            TenantAuditSnapshot? previous,
            CancellationToken cancellationToken = default)
        {
            AuditRequests.Add(request);
            if (AuditException is not null)
            {
                return Task.FromException<TenantAuditSnapshot>(AuditException);
            }

            if (AsyncAuditProvider is not null)
            {
                return AsyncAuditProvider(request);
            }

            return Task.FromResult((AuditProvider?.Invoke(request) ?? audit) with
            {
                From = request.From,
                To = request.To,
                Category = request.Category?.ToString(),
                RequestCursor = request.Cursor,
                RequestPageSize = request.PageSize,
                Stream = request.Stream,
            });
        }

        public Task<TenantDetailSnapshot> GetTenantAsync(TenantDetailRequest request, TenantDetailSnapshot? previous, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TenantListSnapshot> ListTenantsAsync(TenantListRequest request, TenantListSnapshot? previous, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserTenantMembershipSnapshot> GetMyTenantsAsync(UserTenantMembershipRequest request, UserTenantMembershipSnapshot? previous, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserTenantMembershipSnapshot> GetUserTenantsAsync(UserTenantMembershipRequest request, UserTenantMembershipSnapshot? previous, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        public LocalizedString this[string name] => new(name, Values.TryGetValue(name, out string? value) ? value : name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.CurrentCulture, Values.TryGetValue(name, out string? value) ? value : name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Values.Select(static value => new LocalizedString(value.Key, value.Value));

        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.Correction.Title"] = "Start correction",
            ["Tenants.Correction.Close"] = "Close correction start",
            ["Tenants.Correction.Confirm.Submit"] = "Submit corrective command",
            ["Tenants.Correction.Confirm.Refresh"] = "Refresh status",
            ["Tenants.GlobalAdministrators.Remove.Refresh"] = "Refresh status",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry"] = "Retry delivery with the same tracked command",
            ["Tenants.Correction.Confirm.Cancel"] = "Cancel",
            ["Tenants.Correction.Field.OriginalEvidence"] = "Original evidence",
            ["Tenants.Correction.Field.Domain"] = "Command domain",
            ["Tenants.Correction.Field.Command"] = "Intended command",
            ["Tenants.Correction.Field.PreviewData"] = "Required preview data",
            ["Tenants.Correction.Lifecycle.Title"] = "Correction lifecycle",
            ["Tenants.Correction.Domain.GlobalAdministrators"] = "Global administrators",
            ["Tenants.Correction.Command.SetGlobalAdministrator"] = "Set global administrator",
            ["Tenants.Correction.Command.RemoveGlobalAdministrator"] = "Remove global administrator",
            ["Tenants.Correction.PreviewInput.userId"] = "User",
            ["Tenants.Correction.GlobalAdmin.Preview.Scope"] = "Platform authority scope",
            ["Tenants.Correction.GlobalAdmin.Preview.Scope.Value"] = "system / global-administrators / global-administrators",
            ["Tenants.Correction.GlobalAdmin.Preview.AdminCount"] = "Current global administrator count",
            ["Tenants.Correction.GlobalAdmin.Preview.CurrentState"] = "Target in current projection",
            ["Tenants.Correction.GlobalAdmin.Preview.CurrentState.Present"] = "Present in the current platform authority projection.",
            ["Tenants.Correction.GlobalAdmin.Preview.CurrentState.Absent"] = "Absent from the current platform authority projection.",
            ["Tenants.Correction.GlobalAdmin.Preview.Provenance.Current"] = "Complete, current system projection with an ordered version.",
            ["Tenants.Correction.GlobalAdmin.Preview.Provenance.Unverified"] = "Current complete projection evidence is unavailable.",
            ["Tenants.Correction.GlobalAdmin.Preview.AuthorityAdmission.Ready"] = "Platform authority and command admission are ready. Confirm rechecks both.",
            ["Tenants.Correction.GlobalAdmin.Preview.AuthorityAdmission.Blocked"] = "Confirm is unavailable for the reason shown above.",
            ["Tenants.Correction.GlobalAdmin.Preview.LastAdminImpact"] = "Last-administrator impact",
            ["Tenants.Correction.GlobalAdmin.Preview.LastAdminImpact.Value"] = "The last global administrator cannot be removed; at least one global administrator must remain.",
            ["Tenants.GlobalAdministrators.Remove.Preview.CallerTargetContext.Self.Value"] = "This removes your own global-administrator authority.",
            ["Tenants.Correction.GlobalAdmin.Preview.Consequences"] = "Known consequences",
            ["Tenants.Correction.GlobalAdmin.Preview.Consequence.Restore"] = "A new platform authority grant event may be appended when the fixed projection confirms the target is absent.",
            ["Tenants.Correction.GlobalAdmin.Preview.Consequence.Revoke"] = "A new platform authority removal event may be appended when the fixed projection confirms the target is present.",
            ["Tenants.Correction.GlobalAdmin.Preview.Unknowns"] = "Known unknowns",
            ["Tenants.Correction.GlobalAdmin.Preview.Unknowns.Value"] = "Status lookup and live notifications can prompt a re-query but never prove a platform authority change without fixed projection truth.",
            ["Tenants.Correction.GlobalAdmin.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.Correction.GlobalAdmin.Preview.AuditExpectation.Value"] = "Corrective system-scope audit evidence is expected after the command is accepted and the fixed projection confirms the intended state.",
            ["Tenants.Correction.GlobalAdmin.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.Correction.GlobalAdmin.Preview.RecoveryPath.Value"] = "Retry status lookup, inspect audit, continue read-only, or escalate using support-safe references. A hard reload or new session loses in-flight attempt tracking and paired receipt links.",
            ["Tenants.Correction.GlobalAdmin.AlreadyRemoved"] = "The current platform authority projection already shows this user is not a global administrator.",
            ["Tenants.Correction.GlobalAdmin.LastAdministrator"] = "The last global administrator cannot be removed. Keep the current projection visible and add another global administrator before starting this correction.",
            ["Tenants.Correction.GlobalAdmin.State.AlreadyApplied"] = "The fixed projection already reflects the intended platform authority state; no correction success is asserted.",
            ["Tenants.Correction.GlobalAdmin.State.UnableToVerify"] = "The platform authority correction cannot be verified from current evidence.",
            ["Tenants.Correction.State.Previewed"] = "Preview is ready for deliberate confirmation.",
            ["Tenants.Correction.State.RequestSent"] = "Corrective command request was sent.",
            ["Tenants.Correction.State.Accepted"] = "Command accepted; projection confirmation is pending.",
            ["Tenants.Correction.State.ProjectionPending"] = "Command events are stored; projection confirmation is pending.",
            ["Tenants.Correction.State.Confirmed"] = "Projection confirms the intended state; waiting for corrective audit proof.",
            ["Tenants.Correction.State.AlreadyApplied"] = "The intended change was already applied when checked.",
            ["Tenants.Correction.State.Rejected"] = "Corrective command was rejected.",
            ["Tenants.Correction.State.Failed"] = "Corrective command failed before acceptance.",
            ["Tenants.Correction.State.Degraded"] = "Command processing is degraded; refresh status or inspect audit evidence.",
            ["Tenants.Correction.State.UnableToVerify"] = "Correction cannot be verified from current evidence.",
            ["Tenants.Correction.Audit.AuditPending"] = "Corrective audit evidence is pending.",
            ["Tenants.Correction.Audit.AuditDelayed"] = "Corrective audit evidence is delayed.",
            ["Tenants.Correction.Audit.AuditUnavailable"] = "Corrective audit evidence is unavailable.",
            ["Tenants.Correction.Audit.MissingSupport"] = "Corrective audit support is unavailable.",
            ["Tenants.Correction.Proof.Link"] = "View corrective proof from {0}",
            ["Tenants.Correction.Unavailable.GlobalAdministratorCommandSupportUnavailable"] = "Global administrator correction commands are not connected.",
            ["Tenants.Correction.Unavailable.CurrentProjectionUnavailable"] = "Current projection evidence is unavailable.",
            ["Tenants.Correction.Unavailable.CommandProofUnavailable"] = "The completed correction cannot yet be verified against current tenant evidence. Refresh status or escalate before relying on the result.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.Refresh"] = "Refresh the complete fixed-scope projection and rebuild the preview.",
            ["Tenants.Correction.Recovery.AlreadyApplied"] = "Close this correction; the current projection already reflects the intended state.",
            ["Tenants.Correction.Recovery.Confirmed"] = "Keep the confirmed projection visible while corrective audit evidence catches up.",
            ["Tenants.Correction.Recovery.Rejected"] = "Review the rejection, refresh current evidence, and start a new correction only if it is still required.",
            ["Tenants.Correction.Recovery.Failed"] = "Refresh current evidence before starting another correction attempt.",
            ["Tenants.Correction.Recovery.Tracked"] = "Refresh command status and the fixed projection until terminal evidence is available.",
            ["Tenants.Correction.Recovery.Unavailable"] = "Restore the blocked authorization, lifecycle, viewport, or projection evidence before retrying.",
            ["Tenants.GlobalAdministrators.Availability.Grant.Unavailable.MissingLifecycleSupport"] = "Grant is unavailable because dispatch, status, or requery support is missing.",
            ["Tenants.GlobalAdministrators.Availability.Remove.Unavailable.MissingLifecycleSupport"] = "Remove is unavailable because dispatch, status, or requery support is missing.",
            ["Tenants.GlobalAdministrators.Availability.Recovery.MissingLifecycleSupport"] = "Restore dispatch, status, and requery support, then retry.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Unavailable.TargetExists"] = "The exact target is already present in the complete global-administrator projection, so no grant was dispatched.",
            ["Tenants.GlobalAdministrators.Grant.Preview.Recovery.TargetExists"] = "Keep the confirmed rows unchanged and choose a target absent from the complete projection.",
            ["Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous"] = "Grant delivery is ambiguous. Refresh or retry with the same retained command identity.",
            ["Tenants.GlobalAdministrators.Grant.DeliveryRetry.Recovery"] = "Retry only with the retained command identity; do not create a new grant attempt.",
            ["Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"] = "Command status did not match the exact retained grant identity.",
            ["Tenants.GlobalAdministrators.Grant.UnableToVerify.EventEvidence"] = "Exact command status did not prove that the grant produced an event.",
            ["Tenants.GlobalAdministrators.Grant.Status.Pending"] = "Tracked grant status is pending; keep the same command identity and refresh.",
            ["Tenants.GlobalAdministrators.Grant.Status.Unknown"] = "Tracked grant status is unavailable; keep the same command identity and retry verification.",
            ["Tenants.GlobalAdministrators.Grant.Status.Rejected"] = "The tracked grant was rejected. Refresh platform authority before trying another action.",
            ["Tenants.GlobalAdministrators.Grant.Status.PublishFailed"] = "The grant event could not be published. Keep the same tracked command and retry verification.",
            ["Tenants.GlobalAdministrators.Grant.Status.TimedOut"] = "The tracked grant timed out before its result could be verified.",
            ["Tenants.GlobalAdministrators.Remove.DeliveryRetry"] = "Retry removal delivery",
            ["Tenants.GlobalAdministrators.Remove.DeliveryRetry.Recovery"] = "Refresh evidence, then retry this same tracked removal attempt.",
            ["Tenants.GlobalAdministrators.Remove.Recovery.Rejected"] = "Refresh current evidence and review the rejection before a new attempt.",
            ["Tenants.GlobalAdministrators.Remove.Recovery.Failed"] = "Refresh current evidence before starting a new attempt.",
            ["Tenants.GlobalAdministrators.Remove.Recovery.PublishFailed"] = "Refresh tracked status until publication or terminal evidence is available.",
            ["Tenants.GlobalAdministrators.Remove.Recovery.TimedOut"] = "Refresh the tracked attempt before considering another removal.",
            ["Tenants.GlobalAdministrators.Remove.Preview.Recovery.Refresh"] = "Refresh the complete fixed-scope projection before continuing.",
            ["Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous"] = "Delivery is uncertain; retry only with this attempt’s retained identifier.",
            ["Tenants.GlobalAdministrators.Remove.Status.Unknown"] = "Tracked removal status is unavailable.",
            ["Tenants.GlobalAdministrators.Remove.Status.PublishFailed"] = "The removal event could not be published.",
        };
    }
}
